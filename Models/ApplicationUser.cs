using Microsoft.AspNetCore.Identity;

namespace WarehouseManagement.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<ImportReceipt> CreatedImportReceipts { get; set; } = [];

    public ICollection<ImportReceipt> PostedImportReceipts { get; set; } = [];

    public ICollection<ImportReceipt> CancelledImportReceipts { get; set; } = [];

    public ICollection<ExportReceipt> CreatedExportReceipts { get; set; } = [];

    public ICollection<ExportReceipt> PostedExportReceipts { get; set; } = [];

    public ICollection<ExportReceipt> CancelledExportReceipts { get; set; } = [];

    public ICollection<InventoryTransaction> PerformedInventoryTransactions { get; set; } = [];
}
