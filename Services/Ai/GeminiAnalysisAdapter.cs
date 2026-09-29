using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WarehouseManagement.Options;

namespace WarehouseManagement.Services.Ai;

public sealed class GeminiAnalysisAdapter(HttpClient client, ILogger<GeminiAnalysisAdapter> logger)
{
    private const string ConnectionError = "Không thể kết nối dịch vụ AI. Vui lòng thử lại sau.";
    private const string ServiceError = "Dịch vụ AI hiện không khả dụng. Vui lòng thử lại sau.";
    public static AiConfiguration Validate(AiOptions config)
    {
        if (!Uri.TryCreate(config.GeminiBaseUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != "https" || uri.Host != "generativelanguage.googleapis.com" || uri.Port != 443 ||
            uri.AbsolutePath.TrimEnd('/') != "/v1beta" || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.UserInfo.Length != 0)
            return new("Gemini", "Endpoint Gemini không hợp lệ; cần HTTPS Gemini Developer API chính thức v1beta.");
        if (string.IsNullOrWhiteSpace(config.ApiKey) || config.ApiKey.Any(c => c < '!' || c > '~'))
            return new("Gemini", "API key chưa được cấu hình hợp lệ trên máy chủ.");
        if (string.IsNullOrWhiteSpace(config.Model) || config.Model.Length > 128 ||
            !Regex.IsMatch(config.Model, "^[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.CultureInvariant))
            return new("Gemini", "Model Gemini chưa hợp lệ; dùng ID model không kèm models/ hoặc đường dẫn.");
        if (config.TimeoutSeconds is < 1 or > 120 || config.MaxOutputTokens is < 256 or > 4000)
            return new("Gemini", "Giới hạn thời gian hoặc token AI không hợp lệ.");
        // Local validation only. Remote model capabilities are checked before every generation.
        return new("Gemini", null);
    }

    public async Task<AiAnalysisResult> AnalyzeAsync(AiOptions config, string input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var validation = Validate(config);
        if (!validation.IsReady) return Fail("configuration", validation.Error!);
        if (string.IsNullOrWhiteSpace(input) || Encoding.UTF8.GetByteCount(input) > 65536)
            return Fail("request", "Dữ liệu yêu cầu Gemini rỗng hoặc vượt giới hạn.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));
        int? auditStatus = null, auditTokens = null;
        var auditFinish = "MISSING";
        var auditPhase = "model";
        var auditSuccess = false;
        var auditId = Guid.NewGuid().ToString("N");
        // Model has passed syntax validation, but may still accidentally contain the secret.
        var auditModel = config.Model.Contains(config.ApiKey, StringComparison.Ordinal) ? "REDACTED" : config.Model;
        try
        {
            if (config.Model.Contains(config.ApiKey, StringComparison.Ordinal))
                return Fail("configuration", "Model AI không hợp lệ. Vui lòng kiểm tra cấu hình trên máy chủ.");
            if (AiSensitiveDataGuard.ContainsSensitiveData(input, config.ApiKey))
                return Fail("sensitive_input", "Dữ liệu phân tích có thông tin nhạy cảm; yêu cầu chưa được gửi đến AI.");
            var modelUrl = config.GeminiBaseUrl.TrimEnd('/') + "/models/" + config.Model;
            using var metadataRequest = Request(HttpMethod.Get, modelUrl, config.ApiKey);
            using var metadataResponse = await client.SendAsync(metadataRequest, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            auditStatus = (int)metadataResponse.StatusCode;
            if (!metadataResponse.IsSuccessStatusCode) return await HttpFailure(metadataResponse, true, timeout.Token);
            using var metadata = await ReadJson(metadataResponse, timeout.Token);
            var model = metadata.RootElement;
            if (Text(model, "name") != "models/" + config.Model ||
                !model.TryGetProperty("supportedGenerationMethods", out var methods) || methods.ValueKind != JsonValueKind.Array)
                return Fail("model_response", "Metadata model Gemini không hợp lệ.");
            if (!methods.EnumerateArray().Any(m => m.ValueKind == JsonValueKind.String && m.GetString() == "generateContent"))
                return Fail("model_method", "Model Gemini không hỗ trợ generateContent.");
            if (!model.TryGetProperty("outputTokenLimit", out var limit) || !limit.TryGetInt32(out var maxTokens) || maxTokens <= 0)
                return Fail("model_response", "Metadata giới hạn token của model Gemini không hợp lệ.");
            if (config.MaxOutputTokens > maxTokens)
                return Fail("model_limit", "Giới hạn token cấu hình vượt khả năng của model Gemini.");

            var generation = new Dictionary<string, object>
            {
                ["candidateCount"] = 1, ["maxOutputTokens"] = config.MaxOutputTokens
            };
            // This capability is documented for this exact model; do not send it to other model families.
            var minimalThinking = config.Model == "gemini-3-flash-preview";
            if (minimalThinking) generation["thinkingConfig"] = new { thinkingLevel = "minimal" };
            logger.LogInformation(new EventId(9204, "GeminiRequestSummary"),
                "Gemini request; maxOutputTokens {Limit}; thinking {Thinking}; inputUtf8Bytes {InputBytes}; instructionsUtf8Bytes {InstructionBytes}.",
                config.MaxOutputTokens, minimalThinking ? "minimal" : "provider_default",
                Encoding.UTF8.GetByteCount(input), Encoding.UTF8.GetByteCount(InventoryAnalysisPrompt.Instructions));
            using var request = Request(HttpMethod.Post, modelUrl + ":generateContent", config.ApiKey);
            request.Content = JsonContent.Create(new
            {
                systemInstruction = new { parts = new[] { new { text = InventoryAnalysisPrompt.Instructions } } },
                contents = new[] { new { role = "user", parts = new[] { new { text = input } } } },
                generationConfig = generation
            });
            auditPhase = "generate";
            auditStatus = null;
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            auditStatus = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode) return await HttpFailure(response, false, timeout.Token);
            using var document = await ReadJson(response, timeout.Token);
            var root = document.RootElement;
            if (!GeminiResponseValidator.ValidRoot(root))
                return Fail("response_schema", "Kết quả phân tích AI không đúng định dạng mong đợi. Vui lòng thử lại.");
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("usageMetadata", out var usage) && usage.ValueKind == JsonValueKind.Object &&
                    usage.TryGetProperty("totalTokenCount", out var total) && total.ValueKind == JsonValueKind.Number &&
                    total.TryGetInt32(out var tokens) && tokens >= 0) auditTokens = tokens;
                if (root.TryGetProperty("candidates", out var items) && items.ValueKind == JsonValueKind.Array && items.GetArrayLength() == 1)
                    auditFinish = SafeFinishReason(Text(items[0], "finishReason"));
            }
            LogResponseSummary(root, (int)response.StatusCode, config.MaxOutputTokens);
            if (root.TryGetProperty("promptFeedback", out var feedback) && Text(feedback, "blockReason") is { } reason && reason != "BLOCK_REASON_UNSPECIFIED")
                return Fail("response_blocked", "Gemini từ chối xử lý nội dung yêu cầu.");
            if (!root.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() != 1)
                return Fail("response", "Gemini trả về nội dung rỗng hoặc không đúng định dạng.");
            var candidate = candidates[0];
            var finish = SafeFinishReason(Text(candidate, "finishReason"));
            if (finish == "MAX_TOKENS")
                return Fail("response_max_tokens", "Gemini đã đạt giới hạn token (MAX_TOKENS); báo cáo chưa hoàn chỉnh nên không hiển thị. Quản trị viên cần kiểm tra AI__MaxOutputTokens và ngân sách suy luận của model.");
            if (finish is "SAFETY" or "RECITATION" or "BLOCKLIST" or "PROHIBITED_CONTENT" or "SPII" or "IMAGE_SAFETY")
                return Fail("response_blocked", "Gemini chặn báo cáo (" + finish + "); không có báo cáo hoàn chỉnh để hiển thị.");
            if (finish != "STOP")
                return Fail("response_incomplete", "Gemini chưa trả báo cáo hoàn chỉnh (finishReason=" + finish + "). Quản trị viên cần kiểm tra log chẩn đoán an toàn.");
            if (!GeminiResponseValidator.ValidCompletedCandidate(candidate))
                return Fail("response_schema", "Kết quả phân tích AI không đúng định dạng mong đợi. Vui lòng thử lại.");
            if (!candidate.TryGetProperty("content", out var content) || Text(content, "role") != "model" ||
                !content.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array)
                return Fail("response", "Phản hồi Gemini không đúng định dạng.");
            var report = new StringBuilder();
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("thought", out var thought))
                {
                    if (thought.ValueKind == JsonValueKind.True) continue;
                    if (thought.ValueKind != JsonValueKind.False) return Fail("response", "Phản hồi Gemini không đúng định dạng.");
                }
                var text = Text(part, "text");
                if (text is null) return Fail("response", "Gemini không trả về báo cáo văn bản hợp lệ.");
                report.Append(text);
                if (report.Length > 16000) return Fail("response_limit", "Báo cáo Gemini vượt giới hạn độ dài.");
            }
            var result = report.ToString().Trim();
            if (result.Length == 0 || AiSensitiveDataGuard.ContainsSensitiveData(result, config.ApiKey))
                return Fail("response", "Gemini trả về nội dung rỗng hoặc không hợp lệ.");
            logger.LogInformation(new EventId(9200, "GeminiSuccess"), "Gemini generation succeeded; HTTP {StatusCode}.", (int)response.StatusCode);
            auditSuccess = true;
            return new(result, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return Fail("timeout", ConnectionError); }
        catch (OperationCanceledException) { throw; } // Preserve explicit caller cancellation.
        catch (HttpRequestException) { return Fail("network", ConnectionError); }
        catch (IOException) { return Fail("network", ConnectionError); }
        catch (System.Net.Sockets.SocketException) { return Fail("network", ConnectionError); }
        catch (JsonException) { return Fail("response", "Phản hồi Gemini không phải JSON hợp lệ."); }
        catch (InvalidOperationException) { return Fail("response", "Phản hồi Gemini không đúng định dạng."); }
        catch (ResponseLimitException) { return Fail("response_limit", "Phản hồi Gemini vượt giới hạn cho phép."); }
        // Never log the exception: provider/transport messages may contain credentials or bodies.
        catch (Exception) { return Fail("unexpected", ConnectionError); }
        finally
        {
            logger.LogInformation(new EventId(9205, "GeminiCallAudit"),
                "AI call {CallId}; provider {Provider}; model {Model}; phase {Phase}; HTTP {StatusCode}; finish {FinishReason}; totalTokens {TotalTokens}; success {Success}.",
                auditId, "Gemini", auditModel, auditPhase, auditStatus, auditFinish, auditTokens, auditSuccess);
        }
    }

    private static HttpRequestMessage Request(HttpMethod method, string url, string key)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("x-goog-api-key", key);
        return request;
    }

    private async Task<AiAnalysisResult> HttpFailure(HttpResponseMessage response, bool metadata, CancellationToken token)
    {
        // Examine only known machine-readable reasons. Never return/log the provider message/body.
        var keyError = false;
        if ((int)response.StatusCode == 400)
        {
            try
            {
                using var json = await ReadJson(response, token);
                if (json.RootElement.TryGetProperty("error", out var error) && error.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
                    keyError = details.EnumerateArray().Any(d => Text(d, "reason") is "API_KEY_INVALID" or "API_KEY_EXPIRED" or "API_KEY_SERVICE_BLOCKED");
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException or ResponseLimitException) { }
        }
        var status = (int)response.StatusCode;
        var (code, message) = status switch
        {
            _ when keyError => ("key", ConnectionError),
            401 or 403 => ("key_permission", ConnectionError),
            404 => ("model", "Mô hình AI hiện không khả dụng. Vui lòng thử lại sau."),
            429 => ("quota", "Dịch vụ AI đang bị giới hạn yêu cầu. Vui lòng thử lại sau."),
            >= 500 => ("server", ServiceError),
            >= 300 and < 400 => ("endpoint", ServiceError),
            _ => ("request", ServiceError)
        };
        logger.LogWarning(new EventId(9201, "GeminiHttpFailure"), "Gemini HTTP {StatusCode}; phase {Phase}; category {Category}.", status, metadata ? "model" : "generate", code);
        return AiAnalysisResult.Failed(message, keyError || status is 401 or 403 ? AiFailureKind.Authentication :
            status == 404 ? AiFailureKind.ModelUnavailable : AiFailureKind.HttpError);
    }

    private AiAnalysisResult Fail(string code, string message)
    {
        logger.LogWarning(new EventId(9202, "GeminiFailure"), "Gemini failed: {Category}.", code);
        return AiAnalysisResult.Failed(message, code switch
        {
            "timeout" => AiFailureKind.Timeout,
            "network" => AiFailureKind.Network,
            "unexpected" => AiFailureKind.Unexpected,
            "configuration" => AiFailureKind.Configuration,
            "model_method" => AiFailureKind.ModelUnavailable,
            _ => AiFailureKind.InvalidResponse
        });
    }

    // Provider-controlled strings are never logged verbatim, even if they look like enum values.
    private static string SafeFinishReason(string? value) => value switch
    {
        "STOP" or "MAX_TOKENS" or "SAFETY" or "RECITATION" or "BLOCKLIST" or
        "PROHIBITED_CONTENT" or "SPII" or "IMAGE_SAFETY" or "OTHER" or
        "MALFORMED_FUNCTION_CALL" or "UNEXPECTED_TOOL_CALL" => value,
        null => "MISSING",
        _ => "UNKNOWN"
    };

    private void LogResponseSummary(JsonElement root, int status, int configuredLimit)
    {
        if (root.ValueKind != JsonValueKind.Object) return;
        var count = root.TryGetProperty("candidates", out var candidates) && candidates.ValueKind == JsonValueKind.Array
            ? candidates.GetArrayLength() : 0;
        var finish = count == 1 ? SafeFinishReason(Text(candidates[0], "finishReason")) : "MISSING";
        root.TryGetProperty("usageMetadata", out var usage);
        static int? Tokens(JsonElement obj, string name) => obj.ValueKind == JsonValueKind.Object &&
            obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var number) && number >= 0 ? number : null;
        logger.LogInformation(new EventId(9203, "GeminiResponseSummary"),
            "Gemini HTTP {StatusCode}; candidates {CandidateCount}; finish {FinishReason}; configuredMaxOutputTokens {ConfiguredLimit}; promptTokens {PromptTokens}; candidateTokens {CandidateTokens}; thoughtsTokens {ThoughtsTokens}; totalTokens {TotalTokens}.",
            status, count, finish, configuredLimit, Tokens(usage, "promptTokenCount"),
            Tokens(usage, "candidatesTokenCount"), Tokens(usage, "thoughtsTokenCount"), Tokens(usage, "totalTokenCount"));
    }

    private static string? Text(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static async Task<JsonDocument> ReadJson(HttpResponseMessage response, CancellationToken token)
    {
        if (response.Content.Headers.ContentLength > 131072) throw new ResponseLimitException();
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var buffer = new MemoryStream();
        var bytes = new byte[4096];
        int count;
        while ((count = await stream.ReadAsync(bytes, token)) != 0)
        {
            if (buffer.Length + count > 131072) throw new ResponseLimitException();
            buffer.Write(bytes, 0, count);
        }
        return JsonDocument.Parse(buffer.ToArray());
    }

    private sealed class ResponseLimitException : Exception;
}
