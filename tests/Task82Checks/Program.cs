using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using WarehouseManagement.Authorization;
using WarehouseManagement.Controllers;
using WarehouseManagement.Models;
using WarehouseManagement.Models.InventoryManagement;

var checks = 0;
void Check(bool result, string message)
{
    if (!result) throw new InvalidOperationException(message);
    checks++;
    Console.WriteLine($"PASS: {message}");
}
// Pure, isolated boundary values, never inserted into any database.
foreach (var test in new[]
{
    (0m, 10m, StockLevelStatus.OutOfStock, true),
    (5m, 10m, StockLevelStatus.LowStock, true),
    (10m, 10m, StockLevelStatus.LowStock, true),
    (15m, 10m, StockLevelStatus.InStock, false),
    (0m, 0m, StockLevelStatus.OutOfStock, true),
    (0.001m, 0m, StockLevelStatus.InStock, false),
    (10.007m, 10.008m, StockLevelStatus.LowStock, true)
})
{
    Check(StockLevelRules.Classify(test.Item1, test.Item2) == test.Item3,
        $"Stock status {test.Item1}/{test.Item2}");
    var product = new Product { CurrentQuantity = test.Item1, MinimumStockLevel = test.Item2 };
    Check(StockLevelRules.IsAlert.Compile()(product) == test.Item4, "Alert predicate matches boundary");
    Check(new[] { product }.AsQueryable().WithStatus(test.Item3).Count() == 1, "Status filter matches classification");
}
Check(LowStockCsv.TextCell("Muối, \"đặc biệt\"\r\nloại A") == "\"Muối, \"\"đặc biệt\"\"\r\nloại A\"", "CSV comma, quotes, newline escaped");
foreach (var text in new[] { "=1+1", "+SUM(A1)", "-1+1", "@SUM(A1)", "  =1", "\t=1", "\r=1", "\n=1" })
    Check(LowStockCsv.TextCell(text).StartsWith("\"'"), "CSV neutralizes formula-like text");
CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("vi-VN");
var bytes = LowStockCsv.Write([new InventoryListItemViewModel
{
    Code = "HH", Name = "Muối", CurrentQuantity = 1.250m, MinimumStockLevel = 10.008m,
    StockStatus = StockLevelStatus.LowStock
}]);
Check(bytes.Take(3).SequenceEqual(new byte[] { 239, 187, 191 }), "UTF-8 BOM");
var csv = Encoding.UTF8.GetString(bytes);
Check(csv.Contains("Muối") && csv.Contains(",1.250,10.008,-8.758,"), "Vietnamese and invariant three-decimal numbers");
Check(Encoding.UTF8.GetString(LowStockCsv.Write([])).Split('\n', StringSplitOptions.RemoveEmptyEntries).Length == 1, "Empty CSV contains only header");
Check(typeof(InventoryController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
    .Cast<AuthorizeAttribute>().Any(a => a.Policy == ApplicationPolicies.ViewInventory), "Inventory policy retained on partial controller");
Check(typeof(InventoryController).GetMethod("ExportLowStock")!.GetCustomAttributes(typeof(AuthorizeAttribute), true)
    .Cast<AuthorizeAttribute>().Any(a => a.Policy == ApplicationPolicies.ViewReports), "Export also requires ViewReports");
Console.WriteLine($"Completed {checks} isolated checks; no database access.");
