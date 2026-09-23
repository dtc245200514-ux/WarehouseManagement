using WarehouseManagement.Models.Enums;

namespace WarehouseManagement.Models;

public class InventoryTransaction
{
    public long Id { get; set; }

    public int ProductId { get; set; }

    public InventoryTransactionType TransactionType { get; set; }

    public decimal QuantityChange { get; set; }

    public decimal BalanceBefore { get; set; }

    public decimal BalanceAfter { get; set; }

    public int? ImportReceiptId { get; set; }

    public int? ExportReceiptId { get; set; }

    public long? ReversedTransactionId { get; set; }

    public string PerformedByUserId { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public string? Note { get; set; }

    public Product Product { get; set; } = null!;

    public ImportReceipt? ImportReceipt { get; set; }

    public ExportReceipt? ExportReceipt { get; set; }

    public InventoryTransaction? ReversedTransaction { get; set; }

    public InventoryTransaction? ReversalTransaction { get; set; }

    public ApplicationUser PerformedByUser { get; set; } = null!;
}
