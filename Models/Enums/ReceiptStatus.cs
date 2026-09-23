namespace WarehouseManagement.Models.Enums;

// Persisted as tinyint. Do not renumber existing values after migrations exist.
public enum ReceiptStatus : byte
{
    Draft = 0,
    Posted = 1,
    Cancelled = 2
}
