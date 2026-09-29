using System.Text.Json;

public static class GeminiFormatFixtures
{
    public static readonly (string Name, string Body, bool Valid)[] SecurityCases = [
        ("KeyResponse", Envelope(new[]{new{text="ui-fixture-not-a-real-key"}}),false),
        ("PasswordResponse", Envelope(new[]{new{text="Password: private-fixture-value"}}),false),
        ("AccountResponse", Envelope(new[]{new{text="Username=private-fixture-user; Email=private@example.invalid"}}),false),
        ("CookieResponse", Envelope(new[]{new{text="Cookie=private-fixture-session"}}),false),
        ("HeaderResponse", Envelope(new[]{new{text="Authorization: Bearer private-fixture-value"}}),false)
    ];
    public const string Marker = "format-fixture-report";
    private static string Envelope(object parts, object? usage = null, string finish = "STOP") => JsonSerializer.Serialize(new {
        candidates = new[] { new { finishReason = finish, content = new { role = "model", parts } } },
        usageMetadata = usage ?? new { totalTokenCount = 25 }
    });
    public static readonly (string Name, string Body, bool Valid)[] Cases = [
        ("ValidJson", Envelope(new[] { new { text = Marker } }), true),
        ("InvalidJson", "{broken}", false),
        ("MissingRequiredField", "{\"candidates\":[{\"finishReason\":\"STOP\"}]}", false),
        ("WrongPartsType", Envelope(new { text = Marker }), false),
        ("WrongCandidatesType", "{\"candidates\":{}}", false),
        ("NullText", Envelope(new[] { new { text = (string?)null } }), false),
        ("NullResponse", "null", false),
        ("EmptyResponse", "", false),
        ("WhitespaceResponse", " \r\n\t", false),
        ("PlainText", "Không thể phân tích dữ liệu kho hiện tại.", false),
        ("MarkdownJson", "```json\n{}\n```", false),
        ("TruncatedJson", "{\"candidates\":[", false),
        ("Incomplete", Envelope(new[] { new { text = Marker } }, finish:"MAX_TOKENS"), false),
        ("WrongNumberType", Envelope(new[] { new { text = Marker } }, new { totalTokenCount = "25" }), false),
        ("DuplicateField", "{\"candidates\":[],\"candidates\":[]}", false),
        ("MixedToolPart", Envelope(new[] { new { text = Marker, functionCall = new { name="not-executed" } } }), false),
        ("WrongThoughtType", Envelope(new[] { new { text = Marker, thought="false" } }), false),
        ("ValidSplitText", Envelope(new[] { new { text = "format-fixture-" }, new { text="report" } }), true),
        ("EmptyObject", "{}", false),
        ("NumericText", Envelope(new[] { new { text = 123 } }), false)
    ];
}
