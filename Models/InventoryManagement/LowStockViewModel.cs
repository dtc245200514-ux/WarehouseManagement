namespace WarehouseManagement.Models.InventoryManagement;

public sealed class LowStockViewModel
{
    public string? SearchTerm { get; init; }
    public int? CategoryId { get; init; }
    public StockLevelStatus? StockStatus { get; init; }
    public int TotalCount { get; init; }
    public int OutOfStockCount { get; init; }
    public int LowStockCount => TotalCount - OutOfStockCount;
    public int Page { get; init; } = 1;
    public int TotalPages { get; init; } = 1;
    public IReadOnlyList<InventoryCategoryOptionViewModel> Categories { get; init; } = [];
    public IReadOnlyList<InventoryListItemViewModel> Products { get; init; } = [];
}
