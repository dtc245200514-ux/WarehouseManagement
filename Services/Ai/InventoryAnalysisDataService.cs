using System.Data;
using Microsoft.EntityFrameworkCore;
using WarehouseManagement.Data;
using WarehouseManagement.Models.AiAnalysis;
using WarehouseManagement.Models.Enums;
using WarehouseManagement.Models.InventoryManagement;
using WarehouseManagement.Services.Reporting;

namespace WarehouseManagement.Services.Ai;

public sealed class AnalysisDataException(string message) : Exception(message);

public sealed class InventoryAnalysisDataService(ApplicationDbContext context)
{
    public async Task<InventoryAnalysisData> GetAsync(AnalysisPeriod period, CancellationToken cancellationToken)
    {
        if (period.FromUtc >= period.ToExclusiveUtc)
            throw new AnalysisDataException("Khoảng thời gian không hợp lệ.");
        // A short, read-only consistent snapshot. Release locks before any provider request.
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var products = context.Products.AsNoTracking();
        var ledger = context.InventoryTransactions.AsNoTracking();
        var posted = ledger.PostedInPeriod(products, period.FromUtc, period.ToExclusiveUtc);
        var start = period.FromUtc ?? DateTime.MinValue;
        var end = period.ToExclusiveUtc ?? DateTime.MaxValue;
        if (await products.AnyAsync(p => p.CurrentQuantity !=
                (ledger.Where(t => t.ProductId == p.Id).Sum(t => (decimal?)t.QuantityChange) ?? 0m), cancellationToken) ||
            await ledger.AnyAsync(t => t.ImportReceipt!.Status == ReceiptStatus.Draft ||
                t.ExportReceipt!.Status == ReceiptStatus.Draft, cancellationToken))
            throw new AnalysisDataException("Số liệu tồn hoặc giao dịch chưa khớp. Vui lòng đối chiếu tồn kho trước khi phân tích AI.");

        var movements = posted.GroupBy(t => t.ProductId).Select(g => new
        {
            ProductId = g.Key,
            Imported = g.Sum(t => t.TransactionType == InventoryTransactionType.Import ? t.QuantityChange : 0m),
            Exported = g.Sum(t => t.TransactionType == InventoryTransactionType.Export ? -t.QuantityChange : 0m)
        });
        var totals = await posted.GroupBy(_ => 1).Select(g => new
        {
            Imported = g.Sum(t => t.TransactionType == InventoryTransactionType.Import ? t.QuantityChange : 0m),
            Exported = g.Sum(t => t.TransactionType == InventoryTransactionType.Export ? -t.QuantityChange : 0m)
        }).SingleOrDefaultAsync(cancellationToken);
        var opening = await ledger.Where(t => t.OccurredAt < start).SumAsync(t => (decimal?)t.QuantityChange, cancellationToken) ?? 0m;
        var closing = await ledger.Where(t => t.OccurredAt < end).SumAsync(t => (decimal?)t.QuantityChange, cancellationToken) ?? 0m;
        var current = await products.SumAsync(p => (decimal?)p.CurrentQuantity, cancellationToken) ?? 0m;
        var imported = totals?.Imported ?? 0m;
        var exported = totals?.Exported ?? 0m;
        if (opening < 0 || closing < 0 || current < 0 || imported < 0 || exported < 0)
            throw new AnalysisDataException("Số liệu báo cáo không hợp lệ. Vui lòng đối chiếu lịch sử giao dịch.");
        var top = await (from m in movements join p in products on m.ProductId equals p.Id
            orderby m.Imported + m.Exported descending, p.Code, p.Id
            select new AnalysisProduct(p.Code, p.Name, p.Unit, m.Imported, m.Exported,
                p.CurrentQuantity, p.MinimumStockLevel)).Take(10).ToListAsync(cancellationToken);
        var alerts = products.Where(p => p.IsActive).Where(StockLevelRules.IsAlert);
        var replenishment = await products.Where(p => p.IsActive).BelowMinimum()
            .OrderBy(p => p.CurrentQuantity == 0m ? 0 : 1).ThenBy(p => p.Code).ThenBy(p => p.Id)
            .Select(p => new ReplenishmentProduct(p.Code, p.Name, p.Unit,
                p.CurrentQuantity, p.MinimumStockLevel)).ToListAsync(cancellationToken);
        var low = await alerts.OrderBy(p => p.CurrentQuantity == 0m ? 0 : 1).ThenBy(p => p.Code).ThenBy(p => p.Id)
            .Select(p => new AnalysisProduct(p.Code, p.Name, p.Unit,
                posted.Where(t => t.ProductId == p.Id && t.TransactionType == InventoryTransactionType.Import).Sum(t => (decimal?)t.QuantityChange) ?? 0m,
                -(posted.Where(t => t.ProductId == p.Id && t.TransactionType == InventoryTransactionType.Export).Sum(t => (decimal?)t.QuantityChange) ?? 0m),
                p.CurrentQuantity, p.MinimumStockLevel)).Take(10).ToListAsync(cancellationToken);
        var units = await products.GroupBy(p => p.Unit).OrderBy(g => g.Key)
            .Select(g => new AnalysisUnit(g.Key, g.Sum(p => p.CurrentQuantity))).Take(51).ToListAsync(cancellationToken);
        var limits = new List<string>
        {
            "Thời gian UTC: từ đầu ngày bắt đầu đến trước đầu ngày sau ngày kết thúc. Bỏ trống ngày là toàn bộ lịch sử.",
            "Nhập/xuất chỉ là giao dịch liên kết phiếu Posted; Draft/Cancelled không tính trong hai tổng này.",
            "Tồn đầu/cuối dựa trên toàn bộ sổ; biến động khác được tách riêng như báo cáo 8.1. Tồn hiện tại không phải tồn cuối kỳ lịch sử.",
            "Tổng số lượng nhiều đơn vị không phải một đại lượng vật lý đồng nhất; không suy ra giá trị tiền hoặc giá vốn.",
            "Danh sách biến động và cảnh báo tối đa 10 hàng mỗi loại, không phải toàn bộ danh sách. Cảnh báo dùng tồn hiện tại, chỉ hàng hoạt động, tồn <= ngưỡng (kể cả bằng ngưỡng).",
            "Không có kỳ so sánh trước: chỉ diễn giải chênh lệch tồn cuối–đầu từ NetStockChange, không kết luận nhập/xuất tăng giảm so với kỳ trước hoặc xu hướng tiêu thụ. Không dự báo ngày hết hàng hoặc quyết định số lượng đặt hàng.",
            "Đề xuất nhập thêm chỉ xét mã đang hoạt động có tồn hiện tại < MinimumStockLevel, khác cảnh báo <= ngưỡng. MinimumShortfall là chênh lệch so với mức tối thiểu, không phải số lượng phải nhập. ReplenishmentSample tối đa50 mã; trang hiển thị đầy đủ danh sách phù hợp."
        };
        if (units.Count > 50) limits.Add("Danh sách đơn vị bị giới hạn ở 50 nhóm đầu tiên.");
        if (imported == 0m && exported == 0m) limits.Add("Không có biến động số lượng nhập/xuất Posted trong kỳ; cần phân biệt biến động khác và tồn đầu/cuối, không suy ra toàn bộ sổ không có giao dịch.");
        var data = new InventoryAnalysisData
        {
            SnapshotAtUtc = DateTime.UtcNow, FromUtc = period.FromUtc, ToExclusiveUtc = period.ToExclusiveUtc,
            ProductCount = await products.CountAsync(cancellationToken), ImportedQuantity = imported,
            ExportedQuantity = exported, OpeningQuantity = opening, ClosingQuantity = closing,
            CurrentQuantity = current, OtherChange = closing - opening - imported + exported,
            ImportReceiptCount = await posted.Where(t => t.TransactionType == InventoryTransactionType.Import)
                .Select(t => t.ImportReceiptId).Distinct().CountAsync(cancellationToken),
            ExportReceiptCount = await posted.Where(t => t.TransactionType == InventoryTransactionType.Export)
                .Select(t => t.ExportReceiptId).Distinct().CountAsync(cancellationToken),
            AlertCount = await alerts.CountAsync(cancellationToken),
            OutOfStockCount = await alerts.CountAsync(p => p.CurrentQuantity == 0m, cancellationToken),
            ReplenishmentCandidates = replenishment,
            TopMovements = top, LowStock = low, CurrentStockByUnit = units.Take(50).ToArray(), Limitations = limits
        };
        await transaction.CommitAsync(cancellationToken);
        return data;
    }
}
