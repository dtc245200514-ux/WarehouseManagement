using WarehouseManagement.Models;
using WarehouseManagement.Models.Enums;

namespace WarehouseManagement.Services.Reporting;

public static class StockReportQueries
{
    public static IQueryable<InventoryTransaction> PostedInPeriod(this IQueryable<InventoryTransaction> query,
        IQueryable<Product> products, DateTime? fromUtc, DateTime? toExclusiveUtc)
    {
        var ids = products.Select(p => p.Id);
        query = query.Where(t => ids.Contains(t.ProductId) &&
            ((t.TransactionType == InventoryTransactionType.Import && t.ImportReceipt!.Status == ReceiptStatus.Posted) ||
             (t.TransactionType == InventoryTransactionType.Export && t.ExportReceipt!.Status == ReceiptStatus.Posted)));
        if (fromUtc.HasValue) query = query.Where(t => t.OccurredAt >= fromUtc.Value);
        if (toExclusiveUtc.HasValue) query = query.Where(t => t.OccurredAt < toExclusiveUtc.Value);
        return query;
    }
}
