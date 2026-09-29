using System.Text.Json;

namespace WarehouseManagement.Services.Ai;

// Validates the provider envelope. The existing business report is plain text,
// not a second JSON document and never an entity to persist.
internal static class GeminiResponseValidator
{
    public static bool ValidRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || HasDuplicateProperties(root)) return false;
        if (root.TryGetProperty("usageMetadata", out var usage))
        {
            if (usage.ValueKind != JsonValueKind.Object) return false;
            foreach (var name in new[] { "promptTokenCount", "candidatesTokenCount", "thoughtsTokenCount", "totalTokenCount" })
                if (usage.TryGetProperty(name, out var count) &&
                    (count.ValueKind != JsonValueKind.Number || !count.TryGetInt32(out var number) || number < 0)) return false;
        }
        if (root.TryGetProperty("promptFeedback", out var feedback) && feedback.ValueKind != JsonValueKind.Object) return false;
        return true;
    }

    public static bool ValidCompletedCandidate(JsonElement candidate)
    {
        if (candidate.ValueKind != JsonValueKind.Object ||
            !candidate.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Object ||
            !content.TryGetProperty("role", out var role) || role.ValueKind != JsonValueKind.String || role.GetString() != "model" ||
            !content.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array || parts.GetArrayLength() == 0) return false;
        var hasReportText = false;
        foreach (var part in parts.EnumerateArray())
        {
            if (part.ValueKind != JsonValueKind.Object ||
                !part.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String) return false;
            // No tool, executable code, image or other payload may masquerade as a report part.
            if (part.TryGetProperty("functionCall", out _) || part.TryGetProperty("functionResponse", out _) ||
                part.TryGetProperty("executableCode", out _) || part.TryGetProperty("codeExecutionResult", out _) ||
                part.TryGetProperty("inlineData", out _) || part.TryGetProperty("fileData", out _)) return false;
            var isThought = false;
            if (part.TryGetProperty("thought", out var thought))
            {
                if (thought.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return false;
                isThought = thought.GetBoolean();
            }
            if (!isThought && !string.IsNullOrWhiteSpace(text.GetString())) hasReportText = true;
        }
        return hasReportText;
    }

    private static bool HasDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
                if (!names.Add(property.Name) || HasDuplicateProperties(property.Value)) return true;
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) if (HasDuplicateProperties(item)) return true;
        return false;
    }
}
