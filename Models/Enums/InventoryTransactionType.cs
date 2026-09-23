namespace WarehouseManagement.Models.Enums;

// Persisted as tinyint. Do not renumber existing values after migrations exist.
public enum InventoryTransactionType : byte
{
    Import = 0,
    Export = 1,
    ImportCancellation = 2,
    ExportCancellation = 3,
    AdjustmentIncrease = 4,
    AdjustmentDecrease = 5,
    OpeningBalance = 6
}
