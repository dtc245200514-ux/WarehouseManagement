using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WarehouseManagement.Controllers;
using WarehouseManagement.Data;
using WarehouseManagement.Models.AiAnalysis;
using WarehouseManagement.Models.Reporting;
using WarehouseManagement.Options;
using WarehouseManagement.Services.Ai;

var checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + message);
    checks++;
    Console.WriteLine("PASS: " + message);
}
const string fakeKey = "unit-test-placeholder-never-sent-to-network";
MovementChecks.Run(Check);
ReplenishmentChecks.Run(Check);
await GeminiFormatChecks.Run(Check);
await SecurityChecks.Run(Check);
var periodFixture = new InventoryAnalysisData { SnapshotAtUtc = new DateTime(2026,9,24,21,38,15,DateTimeKind.Utc),
    FromUtc = new DateTime(2026,9,23,0,0,0,DateTimeKind.Utc), ToExclusiveUtc = new DateTime(2026,9,26,0,0,0,DateTimeKind.Utc) };
var periodPrompt = InventoryAnalysisPrompt.Input(periodFixture);
Check(periodPrompt.StartsWith("Kỳ báo cáo: 23/09/2026 – 25/09/2026"), "Prompt presents inclusive end date, not SQL exclusive boundary");
Check(periodPrompt.Contains("24/09/2026 21:38:15 UTC") && periodPrompt.Contains(JsonSerializer.Serialize(periodFixture)), "Prompt retains exact snapshot time and unchanged DTO JSON");
Check(InventoryAnalysisPrompt.Input(periodFixture with { FromUtc=null, ToExclusiveUtc=null }).StartsWith("Kỳ báo cáo: Toàn bộ lịch sử"), "Unbounded prompt does not invent dates");
Check(InventoryAnalysisPrompt.Input(periodFixture with { FromUtc=null }).StartsWith("Kỳ báo cáo: Đến hết 25/09/2026"), "End-only prompt is inclusive");
Check(InventoryAnalysisPrompt.Input(periodFixture with { ToExclusiveUtc=null }).StartsWith("Kỳ báo cáo: Từ 23/09/2026"), "Start-only prompt preserves open end");
var capturedLogs = new CaptureLogger();
AiOptions Config() => new() { Provider = "OpenAI", ApiKey = fakeKey, Model = "test-model", TimeoutSeconds = 1, MaxOutputTokens = 1500 };
AiAnalysisService Service(AiOptions config, FakeHandler handler, string environment = "Development") =>
    new(new HttpClient(handler), Options.Create(config), new TestEnvironment { EnvironmentName = environment }, capturedLogs,
        new GeminiAnalysisAdapter(new HttpClient(handler), Microsoft.Extensions.Logging.Abstractions.NullLogger<GeminiAnalysisAdapter>.Instance));
string Envelope(string text) => JsonSerializer.Serialize(new
{
    status = "completed", output = new object[]
    {
        new { type = "reasoning", summary = Array.Empty<object>() },
        new { type = "message", role = "assistant", content = new[] { new { type = "output_text", text } } }
    }
});
var sample = new InventoryAnalysisData
{
    SnapshotAtUtc = DateTime.UtcNow, ProductCount = 20, ImportedQuantity = 44.125m,
    ExportedQuantity = 14.750m, ClosingQuantity = 29.375m, CurrentQuantity = 29.375m,
    ImportReceiptCount = 5, ExportReceiptCount = 6, AlertCount = 20, OutOfStockCount = 12,
    Limitations = ["Fixture trong bộ nhớ, không được ghi vào database."]
};
foreach (var setting in new[] { "TimeoutSeconds", "MaxOutputTokens" })
{
    var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["AI:Provider"] = "OpenAI", ["AI:" + setting] = "not-a-number"
    }).Build();
    using var services = new ServiceCollection().Configure<AiOptions>(configuration.GetSection("AI")).BuildServiceProvider();
    var handler = new FakeHandler("{}");
    var service = new AiAnalysisService(new HttpClient(handler), services.GetRequiredService<IOptions<AiOptions>>(), new TestEnvironment(), capturedLogs,
        new GeminiAnalysisAdapter(new HttpClient(handler), Microsoft.Extensions.Logging.Abstractions.NullLogger<GeminiAnalysisAdapter>.Instance));
    AiAnalysisResult? result = null;
    try { result = await service.AnalyzeAsync(sample, default); }
    catch (InvalidOperationException) { /* Assert failure without printing configuration exception details. */ }
    Check(result is { Success: false } && handler.Count == 0, "Malformed numeric configuration handled without exception: " + setting);
}
foreach (var dates in new[] { ("bad", "2026-09-22"), ("2026-09-23", "2026-09-22"), ("", "9999-12-31"), ("2026-02-30", "") })
    Check(!AnalysisPeriod.TryCreate(dates.Item1, dates.Item2, out _, out _), "Invalid period rejected");
Check(AnalysisPeriod.TryCreate("2026-09-22", "2026-09-22", out var oneDay, out _) &&
    oneDay.FromUtc == new DateTime(2026,9,22,0,0,0,DateTimeKind.Utc) &&
    oneDay.ToExclusiveUtc == new DateTime(2026,9,23,0,0,0,DateTimeKind.Utc), "Inclusive day expressed as exclusive next midnight UTC");
Check(AnalysisPeriod.TryCreate(null, null, out var allTime, out _) && allTime.FromUtc is null && allTime.ToExclusiveUtc is null, "No hidden default dates");

var changes = new Action<AiOptions>[]
{
    c => c.Provider = "", c => c.Provider = "Unsupported", c => c.ApiKey = "", c => c.ApiKey = "bad\r\nkey",
    c => c.ApiKey = "bad\0key", c => c.ApiKey = "bad\u0100key",
    c => c.Model = "", c => c.Model = "bad model", c => c.BaseUrl = "http://api.openai.com/v1/",
    c => c.BaseUrl = "https://api.openai.com.evil.invalid/v1/", c => c.BaseUrl = "https://api.openai.com/v1/?key=x",
    c => c.BaseUrl = "https://user@api.openai.com/v1/", c => c.TimeoutSeconds = 0, c => c.MaxOutputTokens = 999999
};
foreach (var change in changes)
{
    var config = Config(); change(config);
    var handler = new FakeHandler(Envelope("Không được gửi"));
    var result = await Service(config, handler).AnalyzeAsync(sample, default);
    Check(!result.Success && handler.Count == 0 && !result.Error!.Contains(fakeKey), "Bad configuration rejected before HTTP; secret absent");
}
var mockHandler = new FakeHandler("{}");
var mock = new AiOptions { Provider = "Mock" };
var mockResult = await Service(mock, mockHandler).AnalyzeAsync(sample, default);
Check(mockResult.Success && mockResult.IsMock && mockResult.Text!.Contains("44.125") && mockResult.Text.Contains("29.375") && mockHandler.Count == 0, "Explicit mock uses supplied numbers without network");
Check(!Service(mock, mockHandler, "Production").Configuration.IsReady, "Mock forbidden in Production");
foreach (var invalid in new InventoryAnalysisData?[] { null, sample with { ProductCount = 0 }, sample with { ImportedQuantity = -1 }, sample with { ClosingQuantity = 100 }, sample with { FromUtc = DateTime.UtcNow, ToExclusiveUtc = DateTime.UtcNow.AddDays(-1) }, sample with { Limitations = [new string('x', 70000)] } })
{
    var handler = new FakeHandler(Envelope("Không được gửi"));
    Check(!(await Service(Config(), handler).AnalyzeAsync(invalid, default)).Success && handler.Count == 0, "Invalid/empty/oversized input not sent");
}
var successHandler = new FakeHandler(Envelope("Tổng nhập chính thức: 44.125."));
var success = await Service(Config(), successHandler).AnalyzeAsync(sample, default);
Check(success.Success && !success.IsMock && success.Text == "Tổng nhập chính thức: 44.125.", "Parses output_text after non-message items");
Check(successHandler.Uri == "https://api.openai.com/v1/responses" && successHandler.Method == HttpMethod.Post && successHandler.BearerPresent, "POST Responses endpoint with backend Bearer auth");
using (var sent = JsonDocument.Parse(successHandler.Body!))
{
    Check(!sent.RootElement.GetProperty("store").GetBoolean() && sent.RootElement.GetProperty("max_output_tokens").GetInt32() == 1500, "Storage disabled and output bounded");
    var input = sent.RootElement.GetProperty("input").GetString()!;
    Check(input == InventoryAnalysisPrompt.Input(sample) && !successHandler.Body!.Contains(fakeKey), "DTO prompt exact; no credential in body");
    Check(!sent.RootElement.TryGetProperty("tools", out _) && sent.RootElement.GetProperty("instructions").GetString()!.Contains("không đáng tin"), "No tools; labels treated as untrusted data");
}
foreach (var status in new[] { 400, 401, 403, 404, 429, 500, 503, 302 })
{
    var handler = new FakeHandler(fakeKey) { Status = (HttpStatusCode)status };
    var result = await Service(Config(), handler).AnalyzeAsync(sample, default);
    Check(!result.Success && result.Text is null && !result.Error!.Contains(fakeKey) && handler.Count == 1, $"HTTP {status}: friendly error, no body disclosure or retry");
}
foreach (var badResponse in new[]
{
    "", "not-json", "[]", "{}", "{\"status\":\"incomplete\",\"output\":[]}",
    "{\"status\":\"completed\",\"output\":{}}", Envelope(" "),
    "{\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"role\":1,\"content\":[]}]}",
    "{\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"role\":\"assistant\",\"content\":[{\"type\":\"refusal\"}]}]}",
    Envelope(new string('x', 16001)), Envelope(fakeKey), new string('x', 131073)
})
    Check(!(await Service(Config(), new FakeHandler(badResponse)).AnalyzeAsync(sample, default)).Success, "Invalid/empty/refusal/oversized/secret response rejected");
var connectionFailure = await Service(Config(), new FakeHandler("") { Failure = new HttpRequestException(fakeKey) }).AnalyzeAsync(sample, default);
Check(!connectionFailure.Success && !connectionFailure.Error!.Contains(fakeKey), "Connection failure hides exception details");
var ioFailure = await Service(Config(), new FakeHandler("") { Failure = new IOException(fakeKey) }).AnalyzeAsync(sample, default);
Check(!ioFailure.Success && !ioFailure.Error!.Contains(fakeKey), "Interrupted IO handled safely");
var timeoutResult = await Service(Config(), new FakeHandler("") { WaitUntilCancelled = true }).AnalyzeAsync(sample, default);
Check(!timeoutResult.Success && timeoutResult.Error!.Contains("thời gian"), "Provider timeout bounded");
using (var cancelled = new CancellationTokenSource())
{
    cancelled.Cancel();
    try { await Service(Config(), new FakeHandler("")).AnalyzeAsync(sample, cancelled.Token); throw new Exception("Caller cancellation swallowed"); }
    catch (OperationCanceledException) { Check(true, "Caller cancellation propagated"); }
}

if (args.Contains("--database"))
{
    var connection = "Server=THI-DIEU;Database=WarehouseManagementDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
    await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options);
    var dataService = new InventoryAnalysisDataService(db);
    foreach (var pair in new (string? From, string? To)[] { (null,null), ("2026-09-22","2026-09-22"), ("2026-09-23","2026-09-23"), (null,"2026-09-21") })
    {
        AnalysisPeriod.TryCreate(pair.From, pair.To, out var period, out _);
        var data = await dataService.GetAsync(period, default);
        await MovementChecks.Sql(db, data, Check);
        await ReplenishmentChecks.Sql(db, data, Check);
        var controller = new ReportsController(db);
        var dashboard = (WarehouseDashboardViewModel)((ViewResult)await controller.Dashboard(pair.From, pair.To,null,null,null,null,default)).Model!;
        var report = (WarehouseReportViewModel)((ViewResult)await controller.Index(pair.From,pair.To,null,null,null,null,null)).Model!;
        Check(data.ImportedQuantity == dashboard.ImportedQuantity && data.ExportedQuantity == dashboard.ExportedQuantity &&
            data.CurrentQuantity == dashboard.CurrentQuantity && data.AlertCount == dashboard.LowStockCount, "SQL AI DTO equals existing dashboard");
        Check(report.TotalPages == 1 && data.OpeningQuantity == report.Rows.Sum(r => r.OpeningQuantity) &&
            data.ClosingQuantity == report.Rows.Sum(r => r.ClosingQuantity) && data.OtherChange == report.Rows.Sum(r => r.OtherChange), "SQL AI balances equal all current report rows");
        Check(data.TopMovements.Count <= 10 && data.LowStock.Count <= 10 && data.CurrentStockByUnit.Count <= 50, "Bounded real-data lists");
        var capture = new FakeHandler(Envelope("Kiểm thử chỉ tại máy này."));
        Check((await Service(Config(), capture).AnalyzeAsync(data, default)).Success, "Real SQL DTO accepted by captured fake HTTP request");
        using var captured = JsonDocument.Parse(capture.Body!);
        Check(captured.RootElement.GetProperty("input").GetString() == InventoryAnalysisPrompt.Input(data), "Exact real SQL data sent into mocked provider request");
        if (pair.From is null && pair.To is null)
        {
            Check(data.ImportedQuantity == 44.125m && data.ExportedQuantity == 14.750m && data.CurrentQuantity == 29.375m &&
                data.ImportReceiptCount == 5 && data.ExportReceiptCount == 6 && data.AlertCount == 20 && data.OutOfStockCount == 12,
                "Independent SQL snapshot totals/receipt counts/alerts");
            Directory.CreateDirectory("bin/Task91Evidence");
            await File.WriteAllTextAsync("bin/Task91Evidence/analysis-input.json", JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
Check(capturedLogs.Messages.Any(m => m.Contains("http_401")) && capturedLogs.Messages.Any(m => m.Contains("timeout")) &&
    capturedLogs.Messages.Any(m => m.Contains("configuration could not be read")), "Safe diagnostic categories recorded for auth, timeout and binding");
Check(capturedLogs.Messages.All(m => !m.Contains(fakeKey) && !m.Contains("not-a-number") && !m.Contains("44.125")) &&
    !capturedLogs.HasException, "Diagnostic logs contain no keys, input, raw configuration or exceptions");
await GeminiChecks.Run(Check);
Console.WriteLine($"Completed {checks} checks. Provider traffic was intercepted in memory; no real AI API was called.");

sealed class CaptureLogger : ILogger<AiAnalysisService>
{
    public List<string> Messages { get; } = [];
    public bool HasException { get; private set; }
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    { Messages.Add(formatter(state, exception)); HasException |= exception is not null; }
}

sealed class FakeHandler(string body) : HttpMessageHandler
{
    public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
    public Exception? Failure { get; init; }
    public bool WaitUntilCancelled { get; init; }
    public int Count { get; private set; }
    public string? Body { get; private set; }
    public string? Uri { get; private set; }
    public HttpMethod? Method { get; private set; }
    public bool BearerPresent { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Count++;
        Body = await request.Content!.ReadAsStringAsync(token);
        Uri = request.RequestUri!.ToString(); Method = request.Method;
        BearerPresent = request.Headers.Authorization?.Scheme == "Bearer";
        if (WaitUntilCancelled) await Task.Delay(Timeout.Infinite, token);
        if (Failure is not null) throw Failure;
        return new HttpResponseMessage(Status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
sealed class TestEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "Task91Checks";
    public string ContentRootPath { get; set; } = ".";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
