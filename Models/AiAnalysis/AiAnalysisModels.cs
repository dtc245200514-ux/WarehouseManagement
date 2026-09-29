using System.Globalization;
using System.Text.Json.Serialization;

namespace WarehouseManagement.Models.AiAnalysis;

public sealed record AnalysisPeriod(DateTime? FromUtc, DateTime? ToExclusiveUtc)
{
    public static bool TryCreate(string? from, string? to, out AnalysisPeriod period, out string? error)
    {
        period = new(null, null);
        error = null;
        DateTime? start = null, end = null;
        if (!string.IsNullOrWhiteSpace(from))
        {
            if (!DateTime.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            { error = "Từ ngày không hợp lệ; dùng định dạng yyyy-MM-dd."; return false; }
            start = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }
        if (!string.IsNullOrWhiteSpace(to))
        {
            if (!DateTime.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) || parsed.Date == DateTime.MaxValue.Date)
            { error = "Đến ngày không hợp lệ hoặc vượt phạm vi hỗ trợ."; return false; }
            end = DateTime.SpecifyKind(parsed.AddDays(1), DateTimeKind.Utc);
        }
        if (start.HasValue && end.HasValue && start >= end)
        { error = "Ngày kết thúc phải từ ngày bắt đầu trở đi."; return false; }
        period = new(start, end);
        return true;
    }
}

public sealed record AnalysisProduct(string Code, string Name, string Unit, decimal ImportedQuantity,
    decimal ExportedQuantity, decimal CurrentQuantity, decimal MinimumStockLevel)
{
    public decimal NetPostedChange => ImportedQuantity - ExportedQuantity;
}

public sealed record AnalysisUnit(string Unit, decimal CurrentQuantity);

public sealed record ReplenishmentProduct(string Code, string Name, string Unit, decimal CurrentQuantity, decimal MinimumStockLevel)
{
    public decimal MinimumShortfall => Math.Max(0m, MinimumStockLevel - CurrentQuantity);
}

public sealed record InventoryAnalysisData
{
    public required DateTime SnapshotAtUtc { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToExclusiveUtc { get; init; }
    public int ProductCount { get; init; }
    public decimal ImportedQuantity { get; init; }
    public decimal ExportedQuantity { get; init; }
    public decimal OpeningQuantity { get; init; }
    public decimal ClosingQuantity { get; init; }
    public decimal CurrentQuantity { get; init; }
    public decimal OtherChange { get; init; }
    public decimal NetStockChange => ClosingQuantity - OpeningQuantity;
    public bool HasPostedMovements => ImportReceiptCount > 0 || ExportReceiptCount > 0;
    public int ImportReceiptCount { get; init; }
    public int ExportReceiptCount { get; init; }
    public int AlertCount { get; init; }
    public int OutOfStockCount { get; init; }
    [JsonIgnore]
    public IReadOnlyList<ReplenishmentProduct> ReplenishmentCandidates { get; init; } = [];
    public int BelowMinimumCount => ReplenishmentCandidates.Count;
    public int ReplenishmentOutOfStockCount => ReplenishmentCandidates.Count(p => p.CurrentQuantity == 0m);
    public IReadOnlyList<ReplenishmentProduct> ReplenishmentSample => ReplenishmentCandidates.Take(50).ToArray();
    public bool ReplenishmentSampleTruncated => BelowMinimumCount > 50;
    public IReadOnlyList<AnalysisProduct> TopMovements { get; init; } = [];
    public IReadOnlyList<AnalysisProduct> LowStock { get; init; } = [];
    public IReadOnlyList<AnalysisUnit> CurrentStockByUnit { get; init; } = [];
    public IReadOnlyList<string> Limitations { get; init; } = [];
}

public sealed class AiAnalysisViewModel
{
    public string? FromDate { get; set; }
    public string? ToDate { get; set; }
    public string Mode { get; set; } = "Chưa cấu hình";
    public string? ConfigurationMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public InventoryAnalysisData? Data { get; set; }
    public string? Report { get; set; }
    public bool IsMock { get; set; }
    public DateTime? GeneratedAtUtc { get; set; }
}
