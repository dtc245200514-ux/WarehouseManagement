using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using WarehouseManagement.Models.AiAnalysis;
using WarehouseManagement.Options;

namespace WarehouseManagement.Services.Ai;

public sealed class AiAnalysisService(HttpClient client, IOptions<AiOptions> options,
    IHostEnvironment environment, ILogger<AiAnalysisService> logger, GeminiAnalysisAdapter gemini) : IAiAnalysisService
{
    private const int MaximumResponseBytes = 131072;
    private const int MaximumReportCharacters = 16000;

    public AiConfiguration Configuration
    {
        get
        {
            AiOptions config;
            try { config = options.Value; }
            catch (Exception exception) when (exception is InvalidOperationException or OptionsValidationException)
            {
                // Binding exceptions can contain configuration values; never log the exception.
                logger.LogWarning(new EventId(9100, "AiConfigurationInvalid"), "AI configuration could not be read.");
                return new("Chưa sẵn sàng", "Cấu hình AI không hợp lệ trên máy chủ. Quản trị viên cần kiểm tra kiểu dữ liệu của cấu hình.");
            }
            if (config.Provider == "Mock")
                return environment.IsDevelopment() ? new("Mock — không gọi API thật", null)
                    : new("Chưa sẵn sàng", "Mock chỉ được phép trong môi trường Development.");
            if (config.Provider == "Gemini") return GeminiAnalysisAdapter.Validate(config);
            if (config.Provider != "OpenAI")
                return new("Chưa cấu hình", "AI chưa được cấu hình. Quản trị viên cần chọn provider trên máy chủ.");
            if (!Uri.TryCreate(config.BaseUrl, UriKind.Absolute, out var uri) ||
                uri.Scheme != "https" || uri.Host != "api.openai.com" || uri.Port != 443 ||
                uri.AbsolutePath.TrimEnd('/') != "/v1" || uri.Query.Length != 0 ||
                uri.Fragment.Length != 0 || uri.UserInfo.Length != 0)
                return new("OpenAI", "Endpoint AI không hợp lệ. Chỉ hỗ trợ HTTPS API OpenAI chính thức.");
            if (string.IsNullOrWhiteSpace(config.ApiKey) || config.ApiKey.Any(c => c < '!' || c > '~'))
                return new("OpenAI", "API key chưa được cấu hình hợp lệ trên máy chủ.");
            if (string.IsNullOrWhiteSpace(config.Model) || config.Model.Length > 128 ||
                !Regex.IsMatch(config.Model, "^[A-Za-z0-9][A-Za-z0-9._:/-]*$", RegexOptions.CultureInvariant))
                return new("OpenAI", "Model AI chưa được cấu hình hợp lệ trên máy chủ.");
            if (config.TimeoutSeconds is < 1 or > 120 || config.MaxOutputTokens is < 256 or > 4000)
                return new("OpenAI", "Giới hạn thời gian hoặc token AI không hợp lệ.");
            return new("OpenAI", null);
        }
    }

    public async Task<AiAnalysisResult> AnalyzeAsync(InventoryAnalysisData? data, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var readiness = Configuration;
        if (!readiness.IsReady) return Fail("configuration", readiness.Error!);
        if (data is null || data.ProductCount < 0 || data.ImportedQuantity < 0 || data.ExportedQuantity < 0 ||
            data.CurrentQuantity < 0 || data.OpeningQuantity < 0 || data.ClosingQuantity < 0 ||
            data.FromUtc >= data.ToExclusiveUtc ||
            data.ClosingQuantity != data.OpeningQuantity + data.ImportedQuantity - data.ExportedQuantity + data.OtherChange)
            return Fail("invalid_input", "Dữ liệu phân tích không hợp lệ.");
        if (data.ProductCount == 0) return Fail("empty_input", "Không có hàng hóa để phân tích.");
        var input = InventoryAnalysisPrompt.Input(data);
        if (Encoding.UTF8.GetByteCount(input) > 65536)
            return Fail("input_limit", "Dữ liệu phân tích vượt giới hạn gửi AI.");
        if (options.Value.Provider == "Mock") return Mock(data);

        var config = options.Value;
        if (config.Provider == "Gemini") return await gemini.AnalyzeAsync(config, input, cancellationToken);
        if (AiSensitiveDataGuard.ContainsSensitiveData(input, config.ApiKey))
            return Fail("sensitive_input", "Dữ liệu phân tích có thông tin nhạy cảm; yêu cầu chưa được gửi đến AI.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, config.BaseUrl.TrimEnd('/') + "/responses");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
            request.Content = JsonContent.Create(new
            {
                model = config.Model, instructions = InventoryAnalysisPrompt.Instructions, input,
                max_output_tokens = config.MaxOutputTokens, store = false
            });
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                var message = (int)response.StatusCode switch
                {
                    401 or 403 => "AI từ chối xác thực hoặc quyền truy cập. Quản trị viên cần kiểm tra cấu hình.",
                    400 or 404 => "AI từ chối model hoặc yêu cầu. Quản trị viên cần kiểm tra cấu hình model.",
                    429 => "AI đang giới hạn yêu cầu hoặc hạn mức. Vui lòng thử lại sau.",
                    _ => "Dịch vụ AI tạm thời không khả dụng. Vui lòng thử lại sau."
                };
                // Never return or log provider error bodies, request headers, or prompts.
                return Fail("http_" + (int)response.StatusCode, message);
            }
            if (response.Content.Headers.ContentLength > MaximumResponseBytes)
                return Fail("response_limit", "Phản hồi AI vượt giới hạn cho phép.");
            await using var source = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffer = new MemoryStream();
            var chunk = new byte[4096];
            int read;
            while ((read = await source.ReadAsync(chunk, timeout.Token)) != 0)
            {
                if (buffer.Length + read > MaximumResponseBytes)
                    return Fail("response_limit", "Phản hồi AI vượt giới hạn cho phép.");
                buffer.Write(chunk, 0, read);
            }
            using var document = JsonDocument.Parse(buffer.ToArray());
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("status", out var status) ||
                status.ValueKind != JsonValueKind.String || status.GetString() != "completed")
                return Fail("incomplete_response", "AI chưa trả về báo cáo hoàn chỉnh. Vui lòng thử lại hoặc kiểm tra giới hạn token.");
            if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
                return Fail("invalid_response", "Phản hồi AI không đúng định dạng.");
            var report = new StringBuilder();
            foreach (var item in output.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("type", out var type) ||
                    type.ValueKind != JsonValueKind.String || type.GetString() != "message") continue;
                if (!item.TryGetProperty("role", out var role) || role.GetString() != "assistant" ||
                    !item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                    return Fail("invalid_response", "Phản hồi AI không đúng định dạng.");
                foreach (var part in content.EnumerateArray())
                {
                    if (part.ValueKind != JsonValueKind.Object || !part.TryGetProperty("type", out var partType) || partType.ValueKind != JsonValueKind.String)
                        return Fail("invalid_response", "Phản hồi AI không đúng định dạng.");
                    if (partType.GetString() == "refusal") return Fail("refusal", "AI chưa thể tạo báo cáo cho yêu cầu này.");
                    if (partType.GetString() != "output_text") continue;
                    if (!part.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String)
                        return Fail("invalid_response", "Phản hồi AI không đúng định dạng.");
                    report.AppendLine(text.GetString());
                    if (report.Length > MaximumReportCharacters)
                        return Fail("report_limit", "Nội dung báo cáo AI quá dài.");
                }
            }
            var result = report.ToString().Trim();
            if (result.Length == 0) return Fail("empty_response", "AI trả về nội dung rỗng.");
            if (AiSensitiveDataGuard.ContainsSensitiveData(result, config.ApiKey))
                return Fail("unsafe_response", "Phản hồi AI không hợp lệ.");
            return new(result, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return Fail("timeout", "AI phản hồi quá thời gian cho phép. Vui lòng thử lại sau."); }
        catch (HttpRequestException)
        { return Fail("connection", "Không thể kết nối dịch vụ AI. Vui lòng thử lại sau."); }
        catch (IOException)
        { return Fail("io", "Kết nối AI bị gián đoạn. Vui lòng thử lại sau."); }
        catch (JsonException)
        { return Fail("invalid_json", "Phản hồi AI không phải JSON hợp lệ."); }
        catch (InvalidOperationException)
        { return Fail("invalid_response", "Phản hồi hoặc cấu hình AI không hợp lệ."); }
    }

    private AiAnalysisResult Fail(string code, string message)
    {
        // Only our fixed category or numeric HTTP status is logged, never provider-controlled text.
        logger.LogWarning(new EventId(9101, "AiAnalysisFailure"), "AI analysis failed: {FailureCode}.", code);
        return AiAnalysisResult.Failed(message);
    }

    private static AiAnalysisResult Mock(InventoryAnalysisData data)
    {
        string N(decimal value) => value.ToString("0.000", CultureInfo.InvariantCulture);
        var text = $"""
            BẢN KIỂM THỬ MOCK — không phải phản hồi từ API AI thật.
            1. Tóm tắt biến động kho: Backend tổng hợp {data.ProductCount} mã. {(data.HasPostedMovements ? "Có giao dịch nhập/xuất Posted trong kỳ." : "Không có biến động nhập/xuất Posted trong kỳ.")} Chênh lệch tồn cuối–đầu {N(data.NetStockChange)} (tổng số học nhiều đơn vị). Không có kỳ trước để so sánh nhập/xuất.
            2. Nhập kho: {N(data.ImportedQuantity)} từ {data.ImportReceiptCount} phiếu Posted trong kỳ.
            3. Xuất kho: {N(data.ExportedQuantity)} từ {data.ExportReceiptCount} phiếu Posted trong kỳ.
            4. Tồn kho: đầu kỳ {N(data.OpeningQuantity)}, cuối kỳ {N(data.ClosingQuantity)}, hiện tại {N(data.CurrentQuantity)}, biến động khác {N(data.OtherChange)}.
            5. Hàng hóa cần chú ý: {data.AlertCount} hàng đang hoạt động được cảnh báo, gồm {data.OutOfStockCount} hàng hết.
            6. Nhận xét tham khảo và giới hạn: Đây là mẫu diễn giải xác định để kiểm thử luồng ứng dụng, không đánh giá chất lượng của mô hình AI.
            7. Đề xuất nhập thêm hàng: {(data.BelowMinimumCount == 0 ? "Hiện không có mặt hàng dưới mức tồn tối thiểu theo dữ liệu hệ thống." : $"Có {data.BelowMinimumCount} mã cần xem xét nhập thêm, gồm {data.ReplenishmentOutOfStockCount} mã hết tồn; xem danh sách chính thức trên trang.")} Đây là đề xuất hỗ trợ; không tự động tạo phiếu nhập hoặc thay đổi tồn kho. Chênh lệch không phải số lượng phải nhập.
            {string.Join("\n", data.Limitations)}
            """;
        return new(text, null, true);
    }
}
