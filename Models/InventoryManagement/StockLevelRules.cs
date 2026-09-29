using System.Linq.Expressions;

namespace WarehouseManagement.Models.InventoryManagement;

// Preserve the existing warehouse rule: reaching the minimum is already a warning.
public static class StockLevelRules
{
    public static readonly Expression<Func<Product, bool>> IsAlert =
        p => p.CurrentQuantity <= p.MinimumStockLevel;

    // Reuse the existing alert scope, excluding equality for replenishment consideration.
    public static IQueryable<Product> BelowMinimum(this IQueryable<Product> query) =>
        query.Where(IsAlert).Where(p => p.CurrentQuantity < p.MinimumStockLevel);

    public static StockLevelStatus Classify(decimal quantity, decimal minimum) =>
        quantity == 0m ? StockLevelStatus.OutOfStock :
        quantity <= minimum ? StockLevelStatus.LowStock : StockLevelStatus.InStock;

    public static IQueryable<Product> WithStatus(this IQueryable<Product> query, StockLevelStatus status) =>
        status switch
        {
            StockLevelStatus.OutOfStock => query.Where(p => p.CurrentQuantity == 0m),
            StockLevelStatus.LowStock => query.Where(p => p.CurrentQuantity > 0m && p.CurrentQuantity <= p.MinimumStockLevel),
            _ => query.Where(p => p.CurrentQuantity > p.MinimumStockLevel)
        };

    public static string Label(StockLevelStatus status) => status switch
    {
        StockLevelStatus.OutOfStock => "Hết hàng",
        StockLevelStatus.LowStock => "Chạm hoặc dưới mức tối thiểu",
        _ => "Bình thường"
    };
}
