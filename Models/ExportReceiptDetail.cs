namespace WarehouseManagement.Models;

public class ExportReceiptDetail
{
    public int Id { get; set; }

    public int ExportReceiptId { get; set; }

    public int ProductId { get; set; }

    public decimal Quantity { get; set; }

    public decimal? UnitCost { get; set; }

    public string? Note { get; set; }

    public ExportReceipt ExportReceipt { get; set; } = null!;

    public Product Product { get; set; } = null!;
}
