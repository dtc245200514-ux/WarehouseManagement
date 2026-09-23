namespace WarehouseManagement.Models.Reporting;

public sealed class ReportFilterViewModel
{
    public string? FromDate { get; init; }
    public string? ToDate { get; init; }
    public int? ProductId { get; init; }
    public int? CategoryId { get; init; }
    public bool? IsActive { get; init; }
    public string? SearchTerm { get; init; }
    public string SortBy { get; init; } = "code";
    public string GroupBy { get; init; } = "day";
    public IReadOnlyList<ReportOptionViewModel> Products { get; init; } = [];
    public IReadOnlyList<ReportOptionViewModel> Categories { get; init; } = [];
}

public sealed class ReportOptionViewModel
{
    public int Id { get; init; }
    public string Text { get; init; } = string.Empty;
}

public sealed class WarehouseDashboardViewModel
{
    public ReportFilterViewModel Filter { get; init; } = new();
    public int ActiveProductCount { get; init; }
    public int CategoryCount { get; init; }
    public decimal CurrentQuantity { get; init; }
    public decimal ImportedQuantity { get; init; }
    public decimal ExportedQuantity { get; init; }
    public int LowStockCount { get; init; }
    public IReadOnlyList<ReportTimeBucketViewModel> Timeline { get; init; } = [];
}

public sealed class WarehouseReportViewModel
{
    public ReportFilterViewModel Filter { get; init; } = new();
    public IReadOnlyList<WarehouseReportRowViewModel> Rows { get; init; } = [];
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int TotalCount { get; init; }
}

public sealed class WarehouseReportRowViewModel
{
    public int ProductId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public decimal OpeningQuantity { get; init; }
    public decimal ImportedQuantity { get; init; }
    public decimal ExportedQuantity { get; init; }
    public decimal OtherChange { get; init; }
    public decimal ClosingQuantity { get; init; }
    public decimal CurrentQuantity { get; init; }
    public decimal MinimumStockLevel { get; init; }
}

public sealed class ReportTimeBucketViewModel
{
    public string Label { get; init; } = string.Empty;
    public decimal ImportedQuantity { get; init; }
    public decimal ExportedQuantity { get; init; }
    public int ImportReceiptCount { get; init; }
    public int ExportReceiptCount { get; init; }
}
