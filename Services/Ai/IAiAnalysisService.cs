using WarehouseManagement.Models.AiAnalysis;

namespace WarehouseManagement.Services.Ai;

public sealed record AiConfiguration(string Mode, string? Error)
{
    public bool IsReady => Error is null;
}

public enum AiFailureKind { None, Configuration, Timeout, HttpError, Authentication, ModelUnavailable, Network, Unexpected, InvalidResponse }

public sealed record AiAnalysisResult(string? Text, string? Error, bool IsMock = false,
    AiFailureKind FailureKind = AiFailureKind.None)
{
    public bool Success => Error is null && !string.IsNullOrWhiteSpace(Text);
    public static AiAnalysisResult Failed(string message, AiFailureKind kind = AiFailureKind.Unexpected) => new(null, message, FailureKind: kind);
}

public interface IAiAnalysisService
{
    AiConfiguration Configuration { get; }
    Task<AiAnalysisResult> AnalyzeAsync(InventoryAnalysisData? data, CancellationToken cancellationToken);
}
