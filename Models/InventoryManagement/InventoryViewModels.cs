using WarehouseManagement.Models.Enums;

namespace WarehouseManagement.Models.InventoryManagement;

public enum StockLevelStatus
{
    InStock,
    LowStock,
    OutOfStock
}

public enum InventoryReconciliationStatus
{
    Match,
    Mismatch,
    InsufficientData
}

public sealed class InventoryIndexViewModel
{
    public string? SearchTerm { get; init; }
    public int? CategoryId { get; init; }
    public StockLevelStatus? StockStatus { get; init; }
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyList<InventoryCategoryOptionViewModel> Categories { get; init; } = [];
    public IReadOnlyList<InventoryListItemViewModel> Products { get; init; } = [];
}

public sealed class InventoryCategoryOptionViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed class InventoryListItemViewModel
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public decimal CurrentQuantity { get; init; }
    public decimal MinimumStockLevel { get; init; }
    public bool IsActive { get; init; }
    public StockLevelStatus StockStatus { get; init; }
}

public sealed class InventoryDetailsViewModel
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public decimal CurrentQuantity { get; init; }
    public decimal MinimumStockLevel { get; init; }
    public bool IsActive { get; init; }
    public StockLevelStatus StockStatus { get; init; }
    public decimal PostedImportQuantity { get; init; }
    public decimal PostedExportQuantity { get; init; }
    public decimal LedgerQuantity { get; init; }
    public DateTime? LastTransactionAt { get; init; }
    public int TransactionCount { get; init; }
    public InventoryReconciliationStatus ReconciliationStatus { get; init; }
    public string ReconciliationMessage { get; init; } = string.Empty;
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public IReadOnlyList<InventoryHistoryItemViewModel> Transactions { get; init; } = [];
}

public sealed class InventoryHistoryItemViewModel
{
    public long Id { get; init; }
    public DateTime OccurredAt { get; init; }
    public InventoryTransactionType TransactionType { get; init; }
    public int? ImportReceiptId { get; init; }
    public int? ExportReceiptId { get; init; }
    public string? ImportReceiptNumber { get; init; }
    public string? ExportReceiptNumber { get; init; }
    public ReceiptStatus? ReceiptStatus { get; init; }
    public decimal QuantityChange { get; init; }
    public decimal BalanceAfter { get; init; }
    public string PerformedByName { get; init; } = string.Empty;
    public string? Note { get; init; }
}

public sealed class InventoryTransactionsViewModel
{
    public int? ProductId { get; init; }
    public InventoryTransactionType? TransactionType { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyList<InventoryTransactionProductOptionViewModel> Products { get; init; } = [];
    public IReadOnlyList<InventoryTransactionListItemViewModel> Transactions { get; init; } = [];
}

public sealed class InventoryTransactionProductOptionViewModel
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public sealed class InventoryTransactionListItemViewModel
{
    public long Id { get; init; }
    public DateTime OccurredAt { get; init; }
    public int ProductId { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public InventoryTransactionType TransactionType { get; init; }
    public decimal QuantityChange { get; init; }
    public decimal BalanceBefore { get; init; }
    public decimal BalanceAfter { get; init; }
    public int? ImportReceiptId { get; init; }
    public string? ImportReceiptNumber { get; init; }
    public int? ExportReceiptId { get; init; }
    public string? ExportReceiptNumber { get; init; }
    public ReceiptStatus? ReceiptStatus { get; init; }
    public string PerformedByName { get; init; } = string.Empty;
    public string? Note { get; init; }
}
