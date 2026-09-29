using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WarehouseManagement.Models.AiAnalysis;
using WarehouseManagement.Options;
using WarehouseManagement.Services.Ai;

static class GeminiChecks
{
    private const string Key = "test-only-gemini-placeholder";
    private const string Metadata = "{\"name\":\"models/test-model\",\"supportedGenerationMethods\":[\"generateContent\"],\"outputTokenLimit\":8192}";
    private static string Report(string text, string finish = "STOP") => JsonSerializer.Serialize(new { candidates = new[] {
        new { finishReason = finish, content = new { role = "model", parts = new object[] { new { thought = true, text = "internal fixture" }, new { text } } } } } });
    private static AiOptions Config() => new() { Provider = "Gemini", ApiKey = Key, Model = "test-model", TimeoutSeconds = 1, MaxOutputTokens = 1500 };
    public static async Task Run(Action<bool, string> check)
    {
        var logs = new LogCapture();
        async Task<AiAnalysisResult> Run(Handler handler, AiOptions? config = null, string input = "SQL DTO fixture", CancellationToken token = default) =>
            await new GeminiAnalysisAdapter(new HttpClient(handler), logs).AnalyzeAsync(config ?? Config(), input, token);
        foreach (var change in new Action<AiOptions>[] {
            c => c.ApiKey="", c=>c.ApiKey="bad\nkey", c=>c.Model="", c=>c.Model="../model", c=>c.Model="models/test-model",
            c=>c.GeminiBaseUrl="https://api.openai.com/v1/", c=>c.GeminiBaseUrl="http://generativelanguage.googleapis.com/v1beta/",
            c=>c.GeminiBaseUrl="https://generativelanguage.googleapis.com/v1beta/?key=bad",
            c=>c.GeminiBaseUrl="https://user@generativelanguage.googleapis.com/v1beta/", c=>c.TimeoutSeconds=0, c=>c.MaxOutputTokens=4001 })
        {
            var config=Config(); change(config); var handler=new Handler();
            check(!(await Run(handler,config)).Success && handler.Calls.Count==0,"Gemini invalid configuration blocks all HTTP");
        }
        var success = new Handler();
        var flashConfig = Config(); flashConfig.Model = "gemini-3-flash-preview"; flashConfig.MaxOutputTokens = 3000;
        var flashHandler = new Handler { MetadataBody = Metadata.Replace("test-model", flashConfig.Model) };
        check((await Run(flashHandler, flashConfig)).Success, "Gemini Flash bounded request accepted");
        using (var flashBody = JsonDocument.Parse(flashHandler.Calls[1].Body!))
        {
            var generation = flashBody.RootElement.GetProperty("generationConfig");
            check(generation.GetProperty("thinkingConfig").GetProperty("thinkingLevel").GetString() == "minimal" &&
                generation.GetProperty("maxOutputTokens").GetInt32() == 3000, "Gemini 3 Flash uses minimal thinking and configured 3000 token limit");
        }
        var result = await Run(success);
        check(logs.Lines.Any(l => l.Contains("provider Gemini; model test-model; phase generate; HTTP 200; finish STOP;") && l.Contains("success True")), "Audit identifies actual selected model and accepted response");
        using (var otherBody = JsonDocument.Parse(success.Calls[1].Body!))
            check(!otherBody.RootElement.GetProperty("generationConfig").TryGetProperty("thinkingConfig", out _), "Other models do not receive unsupported thinking settings");
        check(result.Success && result.Text=="Báo cáo kiểm thử" && !result.IsMock,"Gemini parses final text; skips thought parts");
        check(success.Calls.Count==2 && success.Calls[0].Method==HttpMethod.Get && success.Calls[1].Method==HttpMethod.Post &&
            success.Calls[0].Url=="https://generativelanguage.googleapis.com/v1beta/models/test-model" &&
            success.Calls[1].Url.EndsWith("/models/test-model:generateContent"),"Gemini metadata GET precedes generation POST");
        check(success.Calls.All(c=>c.KeyHeader && !c.Bearer && !c.Url.Contains(Key)),"Gemini key only in header, never URL or Bearer");
        using(var body=JsonDocument.Parse(success.Calls[1].Body!))
        {
            var root=body.RootElement;
            check(root.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString()==InventoryAnalysisPrompt.Instructions &&
                root.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString()=="SQL DTO fixture" &&
                root.GetProperty("generationConfig").GetProperty("maxOutputTokens").GetInt32()==1500 &&
                !root.TryGetProperty("tools",out _) && !root.TryGetProperty("instructions",out _) && !success.Calls[1].Body!.Contains(Key),"Gemini native body preserves prompt; no OpenAI body or secret");
        }
        foreach(var metadata in new[]{"{}","[]","bad-json",Metadata.Replace("generateContent","embedContent"),Metadata.Replace("8192","10"),Metadata.Replace("test-model","other-model")})
        {
            var h=new Handler{MetadataBody=metadata};
            check(!(await Run(h)).Success && h.Calls.Count==1,"Gemini invalid/unsupported model never receives warehouse prompt");
        }
        foreach(var phase in new[]{0,1}) foreach(var code in new[]{400,401,403,404,429,500,502,503,302})
        {
            var h=new Handler{ErrorPhase=phase,ErrorStatus=code};
            var r=await Run(h);
            check(!r.Success && !r.Error!.Contains(Key) && h.Calls.Count==phase+1,"Gemini HTTP error classified without leak/retry: "+code);
            var expectedKind = code is 401 or 403 ? AiFailureKind.Authentication : code == 404 ? AiFailureKind.ModelUnavailable : AiFailureKind.HttpError;
            var expectedMessage = code switch {
                401 or 403 => "Không thể kết nối dịch vụ AI. Vui lòng thử lại sau.",
                404 => "Mô hình AI hiện không khả dụng. Vui lòng thử lại sau.",
                429 => "Dịch vụ AI đang bị giới hạn yêu cầu. Vui lòng thử lại sau.",
                _ => "Dịch vụ AI hiện không khả dụng. Vui lòng thử lại sau." };
            check(r.FailureKind == expectedKind && r.Error == expectedMessage, "Gemini HTTP typed failure and friendly message: " + code);
        }
        var keyFailure=await Run(new Handler{ErrorPhase=0,ErrorStatus=400,ErrorBody="{\"error\":{\"details\":[{\"reason\":\"API_KEY_INVALID\"}]}}"});
        check(keyFailure.FailureKind == AiFailureKind.Authentication && keyFailure.Error == "Không thể kết nối dịch vụ AI. Vui lòng thử lại sau.","Gemini 400 API_KEY_INVALID classified as key with safe connection message");
        var truncated = await Run(new Handler { ReportBody = Report("partial", "MAX_TOKENS") });
        check(logs.Lines.Any(l => l.Contains("finish MAX_TOKENS;") && l.Contains("success False")), "Audit never labels truncated response successful");
        check(!truncated.Success && truncated.Text is null && truncated.Error!.Contains("MAX_TOKENS"), "Gemini token exhaustion identified; partial report withheld");
        var blocked = await Run(new Handler { ReportBody = Report("partial", "SAFETY") });
        check(blocked.Error!.Contains("SAFETY") && !blocked.Error.Contains("MAX_TOKENS"), "Gemini safety block distinct from token exhaustion");
        var untrusted = await Run(new Handler { ReportBody = Report("partial", Key) });
        check(untrusted.Error!.Contains("UNKNOWN") && !untrusted.Error.Contains(Key), "Untrusted finish reason cannot expose secrets");
        await Run(new Handler { ReportBody = "{\"candidates\":[{\"finishReason\":\"MAX_TOKENS\"}],\"usageMetadata\":{\"thoughtsTokenCount\":1490,\"candidatesTokenCount\":10,\"totalTokenCount\":2000}}" });
        check(logs.Lines.Any(l=>l.Contains("finish MAX_TOKENS") && l.Contains("thoughtsTokens 1490") && l.Contains("candidateTokens 10")), "Safe diagnostics retain finish reason and numeric usage");
        check(logs.Lines.Any(l=>l.Contains("totalTokens 2000; success False")), "Audit retains numeric total token count");
        var secretModel = Config(); secretModel.Model = Key;
        await Run(new Handler { MetadataBody = Metadata.Replace("test-model", Key) }, secretModel);
        check(logs.Lines.Any(l=>l.Contains("model REDACTED")), "Audit redacts model accidentally containing key");
        foreach(var body in new[]{"", "null","[]","invalid", "{}", "{\"candidates\":[]}",
            "{\"promptFeedback\":{\"blockReason\":\"SAFETY\"}}", Report(" "), Report("truncated","MAX_TOKENS"),
            Report("blocked","SAFETY"),Report(Key),Report(new string('x',16001)),new string('x',131073),
            "{\"candidates\":[{\"finishReason\":\"STOP\",\"content\":{\"role\":\"model\",\"parts\":[null]}}]}"})
            check(!(await Run(new Handler{ReportBody=body})).Success,"Gemini rejects empty/malformed/blocked/truncated/oversized/secret response");
        foreach(var failure in new Exception[]{new HttpRequestException(Key),new IOException(Key),new System.Net.Sockets.SocketException()})
        {
            var r=await Run(new Handler{Failure=failure});
            check(!r.Success && r.FailureKind == AiFailureKind.Network && r.Error == "Không thể kết nối dịch vụ AI. Vui lòng thử lại sau.","Gemini network error hides exception");
        }
        foreach(var phase in new[]{0,1})
        {
            var timed = await Run(new Handler{Delay=true, DelayPhase=phase});
            check(timed.FailureKind == AiFailureKind.Timeout && timed.Error == "Không thể kết nối dịch vụ AI. Vui lòng thử lại sau.","Gemini timeout covers metadata and generation: " + phase);
        }
        var unexpected = await Run(new Handler{Failure=new Exception(Key + " Authorization x-goog-api-key SQL DTO fixture")});
        check(unexpected.FailureKind == AiFailureKind.Unexpected && unexpected.Error == "Không thể kết nối dịch vụ AI. Vui lòng thử lại sau." && unexpected.Text is null,"Gemini unexpected exception becomes safe typed failure");
        using(var cancel=new CancellationTokenSource())
        {
            cancel.Cancel();
            try { await Run(new Handler(),token:cancel.Token); check(false,"Gemini cancellation"); }
            catch(OperationCanceledException) { check(true,"Gemini caller cancellation propagated"); }
        }
        var data=new InventoryAnalysisData{SnapshotAtUtc=DateTime.UtcNow,ProductCount=1,ImportedQuantity=3,ExportedQuantity=1,ClosingQuantity=2,CurrentQuantity=2};
        var selected=new Handler(); var configGood=Config();
        var service=new AiAnalysisService(new HttpClient(new RejectHandler()),Options.Create(configGood),new TestEnvironment(),NullLogger<AiAnalysisService>.Instance,
            new GeminiAnalysisAdapter(new HttpClient(selected),logs));
        check(service.Configuration.IsReady && (await service.AnalyzeAsync(data,default)).Success,"Provider Gemini selects Gemini adapter, not OpenAI client");
        using(var json=JsonDocument.Parse(selected.Calls[1].Body!))
            check(json.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString()==InventoryAnalysisPrompt.Input(data),"Gemini receives exact service DTO prompt");
        var invalidHandler=new Handler(); var invalidInput=await Run(invalidHandler,input:new string('x',70000));
        check(!invalidInput.Success && invalidHandler.Calls.Count==0,"Gemini oversized input rejected before metadata");
        check(logs.Lines.Any(l=>l.Contains("HTTP 200")) && logs.Lines.Any(l=>l.Contains("quota")) &&
            logs.Lines.All(l=>!l.Contains(Key)&&!l.Contains("SQL DTO fixture")&&!l.Contains("Báo cáo kiểm thử"))&&!logs.HasException,"Gemini safe status/category logs only");
        check(logs.Lines.All(l=>!l.Contains("x-goog-api-key",StringComparison.OrdinalIgnoreCase) && !l.Contains("Authorization",StringComparison.OrdinalIgnoreCase)),"Gemini logs and audit exclude sensitive header names and values");
    }
    private sealed class RejectHandler : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken t)=>throw new Exception("Wrong adapter selected"); }
    private sealed class Handler : HttpMessageHandler
    {
        public string MetadataBody {get;init;}=Metadata;
        public string ReportBody {get;init;}=Report("Báo cáo kiểm thử");
        public int ErrorPhase {get;init;}=-1;
        public int ErrorStatus {get;init;}
        public string ErrorBody {get;init;}=Key;
        public bool Delay {get;init;}
        public int DelayPhase {get;init;}
        public Exception? Failure {get;init;}
        public List<(HttpMethod Method,string Url,string? Body,bool KeyHeader,bool Bearer)> Calls {get;}=[];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            var phase=Calls.Count;
            Calls.Add((request.Method,request.RequestUri!.ToString(),request.Content is null?null:await request.Content.ReadAsStringAsync(token),
                request.Headers.TryGetValues("x-goog-api-key",out var keys)&&keys.Single()==Key,request.Headers.Authorization is not null));
            if(Delay && phase == DelayPhase) await Task.Delay(Timeout.Infinite,token);
            if(Failure is not null) throw Failure;
            return new HttpResponseMessage((HttpStatusCode)(phase==ErrorPhase?ErrorStatus:200))
            {Content=new StringContent(phase==ErrorPhase?ErrorBody:phase==0?MetadataBody:ReportBody)};
        }
    }
    private sealed class LogCapture:ILogger<GeminiAnalysisAdapter>
    {
        public List<string> Lines {get;}=[]; public bool HasException {get;private set;}
        public IDisposable? BeginScope<TState>(TState state) where TState:notnull=>null;
        public bool IsEnabled(LogLevel level)=>true;
        public void Log<TState>(LogLevel level,EventId id,TState state,Exception? exception,Func<TState,Exception?,string> formatter)
        {Lines.Add(formatter(state,exception));HasException|=exception is not null;}
    }
}
