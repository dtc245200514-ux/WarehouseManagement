namespace WarehouseManagement.Options;

public sealed class AiOptions
{
    public const string SectionName = "AI";
    public string Provider { get; set; } = "Disabled";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";
    public string GeminiBaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/";
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxOutputTokens { get; set; } = 3000;
}
