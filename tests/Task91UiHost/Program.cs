using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using WarehouseManagement.Controllers;

[assembly: HostingStartup(typeof(Task91FailureStartup))]

// Test launcher only. Runs the application's actual entry point and MVC pipeline.
// All factory HTTP transports are replaced; no provider request can reach the network.
Environment.SetEnvironmentVariable("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES", "Task91UiHost");
Environment.SetEnvironmentVariable("ASPNETCORE_PREVENTHOSTINGSTARTUP", "false");
Environment.SetEnvironmentVariable("ASPNETCORE_HOSTINGSTARTUPEXCLUDEASSEMBLIES", "");
Environment.SetEnvironmentVariable("ASPNETCORE_APPLICATIONNAME", "WarehouseManagement");
Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
Environment.SetEnvironmentVariable("AdminSeed__Enabled", "false");
Environment.SetEnvironmentVariable("TestAccountSeed__Enabled", "false");
var scenario = Environment.GetEnvironmentVariable("TASK91_UI_SCENARIO") ?? "ServerError";
Environment.SetEnvironmentVariable("AI__Provider", scenario is "GeminiMatrix" or "GeminiFormat" or "GeminiSecurity" ? "Gemini" : "OpenAI");
Environment.SetEnvironmentVariable("AI__Model", "test-model");
Environment.SetEnvironmentVariable("AI__ApiKey", "ui-fixture-not-a-real-key");
Environment.SetEnvironmentVariable("AI__TimeoutSeconds", scenario == "GeminiMatrix" ? "1" : "10");
Environment.SetEnvironmentVariable("AI__MaxOutputTokens", "1500");
if (scenario is not ("ServerError" or "Empty" or "GeminiMatrix" or "GeminiFormat" or "GeminiSecurity")) throw new InvalidOperationException("Unsupported test scenario.");
Console.WriteLine($"TASK 9.1 TEST HOST: {scenario}; all provider HTTP intercepted, no real AI API.");
var entry = typeof(AiAnalysisController).Assembly.EntryPoint!;
try
{
    var result = entry.Invoke(null, new object[] { args });
    if (result is Task task) await task;
}
catch (TargetInvocationException exception) when (exception.InnerException is not null)
{
    // Do not echo configuration or credentials if startup fails.
    Console.Error.WriteLine("Test application startup failed.");
    Environment.ExitCode = 1;
}

public sealed class Task91FailureStartup : IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder.ConfigureServices(services =>
        services.PostConfigureAll<HttpClientFactoryOptions>(options =>
            options.HttpMessageHandlerBuilderActions.Add(handler => handler.PrimaryHandler = new FailureHandler())));
}

sealed class FailureHandler : HttpMessageHandler
{
    private static int generation;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        if (Environment.GetEnvironmentVariable("TASK91_UI_SCENARIO") is "GeminiMatrix" or "GeminiFormat" or "GeminiSecurity")
        {
            if (request.RequestUri?.Host != "generativelanguage.googleapis.com") throw new Exception("Unexpected fixture endpoint");
            if (request.Method == HttpMethod.Get)
                return new(HttpStatusCode.OK) { Content = new StringContent("{\"name\":\"models/test-model\",\"supportedGenerationMethods\":[\"generateContent\"],\"outputTokenLimit\":8192}") };
            var index = Interlocked.Increment(ref generation);
            if (Environment.GetEnvironmentVariable("TASK91_UI_SCENARIO") is "GeminiFormat" or "GeminiSecurity")
            {
                var fixture = (Environment.GetEnvironmentVariable("TASK91_UI_SCENARIO") == "GeminiSecurity" ? GeminiFormatFixtures.SecurityCases : GeminiFormatFixtures.Cases)[index - 1];
                Console.WriteLine("INTERCEPTED Gemini format fixture {0}; no real API.", fixture.Name);
                return new(HttpStatusCode.OK) { Content = new StringContent(fixture.Body) };
            }
            Console.WriteLine("INTERCEPTED Gemini fixture case {0}; no real AI API.", index);
            const string sensitive = "ui-fixture-not-a-real-key Authorization x-goog-api-key provider-private-body";
            if (index == 2) await Task.Delay(Timeout.Infinite, token);
            if (index == 11) throw new HttpRequestException(sensitive);
            if (index == 12) throw new IOException(sensitive);
            if (index == 13) throw new System.Net.Sockets.SocketException();
            if (index == 15) throw new Exception(sensitive);
            var status = index switch { 3 or 14 => 400, 4 => 401, 5 => 403, 6 => 404, 7 => 429, 8 => 500, 9 => 502, 10 => 503, _ => 200 };
            var body = index switch {
                1 => "{\"candidates\":[{\"finishReason\":\"STOP\",\"content\":{\"role\":\"model\",\"parts\":[{\"text\":\"Báo cáo fixture Gemini HTTP; không phải API thật.\"}]}}]}",
                14 => "{\"error\":{\"message\":\"" + sensitive + "\",\"details\":[{\"reason\":\"API_KEY_INVALID\"}]}}",
                16 => "{}",
                17 => "{\"candidates\":[{\"finishReason\":\"MAX_TOKENS\",\"content\":{\"role\":\"model\",\"parts\":[{\"text\":\"partial-must-not-display\"}]}}]}",
                _ => sensitive };
            return new((HttpStatusCode)status) { Content = new StringContent(body) };
        }
        if (request.RequestUri?.Host != "api.openai.com" || request.RequestUri.AbsolutePath != "/v1/responses")
            throw new HttpRequestException("Unexpected test request; network disabled.");
        Console.WriteLine("INTERCEPTED Responses request in memory.");
        await Task.Delay(TimeSpan.FromSeconds(5), token);
        return Environment.GetEnvironmentVariable("TASK91_UI_SCENARIO") == "Empty"
            ? new(HttpStatusCode.OK) { Content = new StringContent("{\"status\":\"completed\",\"output\":[]}") }
            : new(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("ui-fixture-not-a-real-key: provider details must never be displayed") };
    }
}
