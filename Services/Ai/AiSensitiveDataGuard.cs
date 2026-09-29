using System.Text.Json;
using System.Text.RegularExpressions;

namespace WarehouseManagement.Services.Ai;

internal static partial class AiSensitiveDataGuard
{
    // Reject rather than redact: changing a product name or report could change its meaning.
    // This is defense in depth, not a general PII detector; the primary boundary is the warehouse DTO.
    public static bool ContainsSensitiveData(string text, string key)
    {
        if (!string.IsNullOrEmpty(key) && (text.Contains(key, StringComparison.Ordinal) ||
            text.Contains(JsonEncodedText.Encode(key).ToString(), StringComparison.Ordinal))) return true;
        return SensitivePattern().IsMatch(text);
    }

    [GeneratedRegex(@"(?ix)
        \bAIza[0-9A-Za-z_-]{30,}\b |
        \bsk-(?:proj-)?[0-9A-Za-z_-]{20,}\b |
        \b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b |
        \b(?:password(?:hash)?|passwd|username|email|phone(?:number)?|access[_-]?token|refresh[_-]?token|session(?:id)?|cookie|authorization|x-goog-api-key|api[_-]?key|secret|mật\s*khẩu)\b\s*[""']?\s*[:=]\s*\S+")]
    private static partial Regex SensitivePattern();
}
