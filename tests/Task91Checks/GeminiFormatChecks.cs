using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using WarehouseManagement.Options;
using WarehouseManagement.Services.Ai;

static class GeminiFormatChecks
{
    public static async Task Run(Action<bool,string> check)
    {
        foreach (var fixture in GeminiFormatFixtures.Cases)
        {
            var handler = new FormatHandler(fixture.Body);
            var adapter = new GeminiAnalysisAdapter(new HttpClient(handler), NullLogger<GeminiAnalysisAdapter>.Instance);
            var result = await adapter.AnalyzeAsync(new AiOptions { Provider="Gemini", Model="test-model", ApiKey="format-test-only-key" }, "fixture input", default);
            check(result.Success == fixture.Valid && (fixture.Valid ? result.Text == GeminiFormatFixtures.Marker : result.Text is null && result.FailureKind == AiFailureKind.InvalidResponse),
                "9.2-D validates before exposing output: " + fixture.Name);
            check(handler.Calls == 2 && (result.Error is null || (!result.Error.Contains("format-test-only-key") && !result.Error.Contains(GeminiFormatFixtures.Marker))),
                "9.2-D no retry or raw output in error: " + fixture.Name);
        }
    }
    private sealed class FormatHandler(string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(request.Method == HttpMethod.Get
                ? "{\"name\":\"models/test-model\",\"supportedGenerationMethods\":[\"generateContent\"],\"outputTokenLimit\":8192}" : body) });
        }
    }
}
