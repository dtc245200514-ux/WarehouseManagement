using WarehouseManagement.Models.Enums;

namespace WarehouseManagement.Models;

public class ExportReceipt
{
    public int Id { get; set; }

    public string ReceiptNumber { get; set; } = string.Empty;

    public ReceiptStatus Status { get; set; } = ReceiptStatus.Draft;

    public string CreatedByUserId { get; set; } = string.Empty;

    public string? PostedByUserId { get; set; }

    public string? CancelledByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PostedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? Note { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public ApplicationUser CreatedByUser { get; set; } = null!;

    public ApplicationUser? PostedByUser { get; set; }

    public ApplicationUser? CancelledByUser { get; set; }

    public ICollection<ExportReceiptDetail> Details { get; set; } = [];

    public ICollection<InventoryTransaction> InventoryTransactions { get; set; } = [];
}
