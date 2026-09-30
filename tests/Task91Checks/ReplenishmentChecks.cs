using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WarehouseManagement.Data;
using WarehouseManagement.Models;
using WarehouseManagement.Models.AiAnalysis;
using WarehouseManagement.Models.InventoryManagement;
using WarehouseManagement.Models.Enums;
using WarehouseManagement.Services.Reporting;
using WarehouseManagement.Services.Ai;

static class ReplenishmentChecks
{
    public static void Run(Action<bool,string> check)
    {
        var inactive = new[]{new Product{IsActive=false,CurrentQuantity=0,MinimumStockLevel=10}}.AsQueryable();
        check(!inactive.Where(p=>p.IsActive).BelowMinimum().Any(),"Inactive product excluded from replenishment query");
        foreach(var pair in new[]{(5m,10m,true),(0m,10m,true),(10m,10m,false),(11m,10m,false),(0m,0m,false)})
        {
            var products=new[]{new Product{CurrentQuantity=pair.Item1,MinimumStockLevel=pair.Item2}}.AsQueryable();
            check(products.BelowMinimum().Any()==pair.Item3,"Replenishment strict threshold: "+pair.Item1+"/"+pair.Item2);
        }
        var rows=Enumerable.Range(1,60).Select(i=>new ReplenishmentProduct(i.ToString(),"fixture","kg",5,10)).ToArray();
        var data=new InventoryAnalysisData{SnapshotAtUtc=DateTime.UtcNow,ReplenishmentCandidates=rows};
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(data));
        check(data.BelowMinimumCount==60 && data.ReplenishmentCandidates.Count==60 && data.ReplenishmentSample.Count==50 && data.ReplenishmentSampleTruncated,"Replenishment keeps full UI list and explicit bounded AI sample");
        check(!json.RootElement.TryGetProperty("ReplenishmentCandidates",out _) && json.RootElement.GetProperty("ReplenishmentSample").GetArrayLength()==50,"Full replenishment list is not sent to provider");
        check(rows.All(r=>r.MinimumShortfall==5),"Shortfall is calculated by backend without changing quantities");
        var empty=data with {ReplenishmentCandidates=[]};
        check(empty.BelowMinimumCount==0 && empty.ReplenishmentSample.Count==0 && !empty.ReplenishmentSampleTruncated,"Empty recommendation dataset remains empty");
        var now = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);
        var productsFixture = new[] { new Product { Id = 1 } }.AsQueryable();
        InventoryTransaction Export(DateTime at, decimal quantity, ReceiptStatus status) => new()
        {
            ProductId = 1, OccurredAt = at, QuantityChange = -quantity,
            TransactionType = InventoryTransactionType.Export,
            ExportReceipt = new ExportReceipt { Status = status }
        };
        var ledger = new[] {
            Export(now.AddDays(-30), 2.5m, ReceiptStatus.Posted),
            Export(now.AddDays(-1), 1.25m, ReceiptStatus.Posted),
            Export(now.AddDays(-30).AddTicks(-1), 100m, ReceiptStatus.Posted),
            Export(now, 100m, ReceiptStatus.Posted),
            Export(now.AddDays(-1), 100m, ReceiptStatus.Draft),
            Export(now.AddDays(-1), 100m, ReceiptStatus.Cancelled)
        }.AsQueryable();
        var recent = -ledger.PostedInPeriod(productsFixture, now.AddDays(-30), now)
            .Where(t => t.TransactionType == InventoryTransactionType.Export).Sum(t => t.QuantityChange);
        check(recent == 3.75m, "30-day export includes start, excludes end/old/Draft/Cancelled");
        var recentData = data with { SnapshotAtUtc = now, ReplenishmentCandidates = [new("LOW", "Low stock", "kg", 5, 10, recent)] };
        check(recentData.RecentExportFromUtc == now.AddDays(-30) && recentData.RecentExportToExclusiveUtc == now,
            "Recent window is explicit and independent of report period");
        check(InventoryAnalysisPrompt.Input(recentData).Contains("\"RecentExportedQuantity\":3.75"), "Prompt carries backend recent exports");
        check(InventoryAnalysisPrompt.Instructions.Contains("Không tự tạo phiếu nhập.") &&
            InventoryAnalysisPrompt.Instructions.Contains("Không thay đổi dữ liệu kho."), "System prompt forbids receipt creation and inventory changes");
    }

    public static async Task Sql(ApplicationDbContext db,InventoryAnalysisData data,Action<bool,string> check)
    {
        var expected=await db.Products.AsNoTracking().Where(p=>p.IsActive && p.CurrentQuantity<p.MinimumStockLevel).OrderBy(p=>p.Code).ToListAsync();
        check(expected.Select(p=>p.Code).SequenceEqual(data.ReplenishmentCandidates.OrderBy(p=>p.Code).Select(p=>p.Code)),"SQL replenishment includes every eligible active product, not only old top10");
        check(expected.All(p=>data.ReplenishmentCandidates.Any(r=>r.Code==p.Code && r.CurrentQuantity==p.CurrentQuantity && r.MinimumStockLevel==p.MinimumStockLevel && r.Unit==p.Unit && r.MinimumShortfall==p.MinimumStockLevel-p.CurrentQuantity)),"SQL replenishment quantities, thresholds, units and shortfalls match");
        check(data.ReplenishmentOutOfStockCount==expected.Count(p=>p.CurrentQuantity==0),"SQL recommendation out-of-stock count matches eligible set");
        foreach (var product in expected)
        {
            var quantity = -(await db.InventoryTransactions.AsNoTracking()
                .Where(t => t.ProductId == product.Id && t.TransactionType == InventoryTransactionType.Export &&
                    t.ExportReceipt!.Status == ReceiptStatus.Posted && t.OccurredAt >= data.RecentExportFromUtc &&
                    t.OccurredAt < data.RecentExportToExclusiveUtc)
                .SumAsync(t => (decimal?)t.QuantityChange) ?? 0m);
            check(data.ReplenishmentCandidates.Single(r => r.Code == product.Code).RecentExportedQuantity == quantity,
                "SQL candidate recent export matches independent 30-day sum");
        }
        check(!db.ChangeTracker.HasChanges(), "Analysis SQL query has no pending entity writes");
    }
}
