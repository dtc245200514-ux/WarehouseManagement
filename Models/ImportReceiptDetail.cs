namespace WarehouseManagement.Models;

public class ImportReceiptDetail
{
    public int Id { get; set; }

    public int ImportReceiptId { get; set; }

    public int ProductId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitCost { get; set; }

    public string? Note { get; set; }

    public ImportReceipt ImportReceipt { get; set; } = null!;

    public Product Product { get; set; } = null!;
}
