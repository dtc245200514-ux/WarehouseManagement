namespace WarehouseManagement.Models;

public class Product
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int CategoryId { get; set; }

    public decimal CurrentQuantity { get; set; }

    public decimal MinimumStockLevel { get; set; }

    public decimal? AverageUnitCost { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public Category Category { get; set; } = null!;

    public ICollection<ImportReceiptDetail> ImportReceiptDetails { get; set; } = [];

    public ICollection<ExportReceiptDetail> ExportReceiptDetails { get; set; } = [];

    public ICollection<InventoryTransaction> InventoryTransactions { get; set; } = [];
}
