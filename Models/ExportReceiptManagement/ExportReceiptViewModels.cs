using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using WarehouseManagement.Models.Enums;

namespace WarehouseManagement.Models.ExportReceiptManagement;

public sealed class CreateExportReceiptViewModel
{
    [StringLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự.")]
    [Display(Name = "Lý do xuất kho / ghi chú")]
    public string? Note { get; set; }

    public List<CreateExportReceiptDetailViewModel> Details { get; set; } = [];

    [ValidateNever]
    public string CreatedByName { get; set; } = string.Empty;

    [ValidateNever]
    public IReadOnlyList<ExportReceiptProductOptionViewModel> Products { get; set; } = [];
}

public sealed class CreateExportReceiptDetailViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn hàng hóa.")]
    public int ProductId { get; set; }

    [Range(typeof(decimal), "0.001", "999999999999999.999",
        ErrorMessage = "Số lượng xuất phải lớn hơn 0 và không vượt quá giới hạn cho phép.")]
    public decimal Quantity { get; set; }
}

public sealed class ExportReceiptProductOptionViewModel
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public decimal CurrentQuantity { get; init; }
}

public sealed class ExportReceiptListItemViewModel
{
    public int Id { get; init; }
    public string ReceiptNumber { get; init; } = string.Empty;
    public ReceiptStatus Status { get; init; }
    public string CreatedByName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? PostedAt { get; init; }
    public int DetailCount { get; init; }
    public decimal TotalQuantity { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class ExportReceiptIndexViewModel
{
    public string? SearchTerm { get; init; }
    public ReceiptStatus? Status { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public string? CreatedByUserId { get; init; }
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyList<ExportReceiptCreatorOptionViewModel> Creators { get; init; } = [];
    public IReadOnlyList<ExportReceiptListItemViewModel> Receipts { get; init; } = [];
}

public sealed class ExportReceiptCreatorOptionViewModel
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}

public sealed class ExportReceiptDetailsViewModel
{
    public int Id { get; init; }
    public string ReceiptNumber { get; init; } = string.Empty;
    public ReceiptStatus Status { get; init; }
    public string CreatedByName { get; init; } = string.Empty;
    public string? PostedByName { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? PostedAt { get; init; }
    public string? Note { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public bool CanPost { get; init; }
    public string? PostAvailabilityMessage { get; init; }
    public IReadOnlyList<ExportReceiptDetailItemViewModel> Details { get; init; } = [];
    public IReadOnlyList<ExportInventoryTransactionItemViewModel> InventoryTransactions { get; init; } = [];
    public decimal TotalQuantity { get; init; }
    public ExportReceiptReconciliationViewModel? Reconciliation { get; init; }
}

public sealed class ExportReceiptDetailItemViewModel
{
    public string ProductCode { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal? UnitCost { get; init; }
    public decimal CurrentQuantity { get; init; }
}

public sealed class ExportInventoryTransactionItemViewModel
{
    public InventoryTransactionType TransactionType { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public decimal QuantityChange { get; init; }
    public decimal BalanceBefore { get; init; }
    public decimal BalanceAfter { get; init; }
    public string PerformedByName { get; init; } = string.Empty;
    public DateTime OccurredAt { get; init; }
}

public enum ExportReconciliationStatus
{
    Match,
    Mismatch,
    MissingTransaction,
    DuplicateTransaction,
    InsufficientData,
    SystemError
}

public sealed class ExportReceiptReconciliationViewModel
{
    public ExportReconciliationStatus Status { get; init; }
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<ExportReceiptReconciliationItemViewModel> Items { get; init; } = [];
}

public sealed class ExportReceiptReconciliationItemViewModel
{
    public string ProductCode { get; init; } = string.Empty;
    public decimal ReceiptQuantity { get; init; }
    public decimal CurrentQuantity { get; init; }
    public decimal? TransactionQuantityChange { get; init; }
    public decimal? BalanceBefore { get; init; }
    public decimal? BalanceAfter { get; init; }
    public ExportReconciliationStatus Status { get; init; }
    public string Message { get; init; } = string.Empty;
}
