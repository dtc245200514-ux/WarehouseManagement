using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WarehouseManagement.Models.AiAnalysis;
using WarehouseManagement.Options;
using WarehouseManagement.Services.Ai;

static class SecurityChecks
{
    public static async Task Run(Action<bool,string> check)
    {
        const string key="security-fixture-key-not-real";
        var config=new AiOptions{Provider="Gemini",Model="test-model",ApiKey=key};
        var data=new InventoryAnalysisData{SnapshotAtUtc=DateTime.UtcNow,ProductCount=1,
            ReplenishmentCandidates=[new("P1","Gạo","kg",0,10)]};
        var logger=new CaptureLog();
        foreach(var payload in new[]{key,"Password: sensitive-fixture","PasswordHash=hash-fixture","Username=private-user", "Email: private@example.invalid", "Phone=0123456789", "Cookie=session-fixture", "SessionId=session-fixture", "Access_Token=token-fixture", "Authorization: Bearer fixture", "secret=private-fixture"})
        {
            var outgoing=new Spy();
            var adapter=new GeminiAnalysisAdapter(new HttpClient(outgoing),logger);
            var contaminated=data with{ReplenishmentCandidates=[new("P1",payload,"kg",0,10)]};
            var inputResult=await adapter.AnalyzeAsync(config,InventoryAnalysisPrompt.Input(contaminated),default);
            check(!inputResult.Success && inputResult.Text is null && outgoing.Calls==0,"Security: sensitive warehouse text rejected before HTTP");
            var incoming=new Spy{Text=payload};
            var response=await new GeminiAnalysisAdapter(new HttpClient(incoming),logger).AnalyzeAsync(config,InventoryAnalysisPrompt.Input(data),default);
            check(!response.Success && response.Text is null && !response.Error!.Contains(payload),"Security: sensitive response withheld without echo");
        }
        var modelSpy=new Spy();
        var badModel=new AiOptions{Provider="Gemini",Model=key,ApiKey=key};
        check(!(await new GeminiAnalysisAdapter(new HttpClient(modelSpy),logger).AnalyzeAsync(badModel,"fixture",default)).Success && modelSpy.Calls==0,"Security: key accidentally used as model cannot enter URL");
        var spy=new Spy();
        var service=new AiAnalysisService(new HttpClient(new Spy()),Options.Create(config),new TestEnvironment(),NullLogger<AiAnalysisService>.Instance,new GeminiAnalysisAdapter(new HttpClient(spy),logger));
        check((await service.AnalyzeAsync(data,default)).Success,"Security: normal warehouse DTO still accepted");
        using var request=JsonDocument.Parse(spy.Body!);
        var input=request.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString()!;
        check(input==InventoryAnalysisPrompt.Input(data),"Security: spy receives exact warehouse DTO prompt only");
        foreach(var forbidden in new[]{"Password","PasswordHash","IdentityUser","UserName","Email","PhoneNumber","AccessToken","Cookie","SessionId","Authorization","ApiKey"})
            check(!input.Contains(forbidden,StringComparison.OrdinalIgnoreCase),"Security: prompt excludes field " + forbidden);
        check(spy.Urls.All(u=>!u.Contains(key)) && !spy.Body!.Contains(key) && spy.KeysOnlyInHeader,"Security: key only in Gemini authentication header, never URL/body");
        check(logger.Lines.All(l=>!l.Contains(key)&&!l.Contains("sensitive-fixture")&&!l.Contains("private@example.invalid")&&!l.Contains("Authorization",StringComparison.OrdinalIgnoreCase)&&!l.Contains("x-goog-api-key",StringComparison.OrdinalIgnoreCase)) && !logger.HasException,"Security: audit logs contain no secret, account, header or exception");
        check(!typeof(AiAnalysisViewModel).GetProperties().Any(p=>p.PropertyType==typeof(AiOptions)||p.Name.Contains("Key")||p.Name.Contains("Password")),"Security: view model exposes no credential configuration");
    }
    private sealed class Spy:HttpMessageHandler
    {
        public string Text {get;init;}="Báo cáo an toàn";
        public int Calls; public string? Body; public List<string> Urls=[]; public bool KeysOnlyInHeader=true;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Calls++;Urls.Add(request.RequestUri!.ToString());KeysOnlyInHeader &= request.Headers.Contains("x-goog-api-key") && request.Headers.Authorization is null;
            if(request.Content is not null)Body=await request.Content.ReadAsStringAsync(token);
            var response=request.Method==HttpMethod.Get ? "{\"name\":\"models/test-model\",\"supportedGenerationMethods\":[\"generateContent\"],\"outputTokenLimit\":8192}" : JsonSerializer.Serialize(new{candidates=new[]{new{finishReason="STOP",content=new{role="model",parts=new[]{new{text=Text}}}}}});
            return new(HttpStatusCode.OK){Content=new StringContent(response)};
        }
    }
    private sealed class CaptureLog:ILogger<GeminiAnalysisAdapter>
    {
        public List<string> Lines=[];public bool HasException;
        public IDisposable? BeginScope<TState>(TState state) where TState:notnull=>null;
        public bool IsEnabled(LogLevel level)=>true;
        public void Log<TState>(LogLevel level,EventId id,TState state,Exception? exception,Func<TState,Exception?,string> formatter){Lines.Add(formatter(state,exception));HasException|=exception is not null;}
    }
}
