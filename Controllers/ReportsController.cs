using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarehouseManagement.Authorization;
using WarehouseManagement.Data;
using WarehouseManagement.Models;
using WarehouseManagement.Models.Enums;
using WarehouseManagement.Models.Reporting;

namespace WarehouseManagement.Controllers;

public class ReportsController(ApplicationDbContext context) : Controller
{
    private const int PageSize = 20;
    private const int MaximumSearchLength = 200;

    [Authorize(Policy = ApplicationPolicies.ViewInventory)]
    [HttpGet]
    public async Task<IActionResult> Dashboard(
        string? fromDate, string? toDate, int? productId, int? categoryId,
        string? isActive, string? groupBy, CancellationToken cancellationToken)
    {
        var filter = await BuildFilterAsync(fromDate, toDate, productId, categoryId,
            isActive, null, null, groupBy, cancellationToken);
        var products = FilterProducts(filter);
        var period = PostedTransactions(products, filter.FromUtc, filter.ToExclusiveUtc);

        var model = new WarehouseDashboardViewModel
        {
            Filter = filter.ViewModel,
            ActiveProductCount = await products.CountAsync(p => p.IsActive, cancellationToken),
            CategoryCount = await context.Categories.AsNoTracking().CountAsync(cancellationToken),
            CurrentQuantity = await products.SumAsync(p => (decimal?)p.CurrentQuantity, cancellationToken) ?? 0m,
            LowStockCount = await products.CountAsync(p => p.IsActive &&
                p.CurrentQuantity <= p.MinimumStockLevel, cancellationToken),
            ImportedQuantity = await period.Where(t => t.TransactionType == InventoryTransactionType.Import)
                .SumAsync(t => (decimal?)t.QuantityChange, cancellationToken) ?? 0m,
            ExportedQuantity = -(await period.Where(t => t.TransactionType == InventoryTransactionType.Export)
                .SumAsync(t => (decimal?)t.QuantityChange, cancellationToken) ?? 0m),
            Timeline = await GetTimelineAsync(period, filter.ViewModel.GroupBy == "month", cancellationToken)
        };
        return View(model);
    }

    [Authorize(Policy = ApplicationPolicies.ViewReports)]
    [HttpGet]
    public async Task<IActionResult> Index(
        string? fromDate, string? toDate, int? productId, int? categoryId,
        string? isActive, string? searchTerm, string? sortBy, int page = 1,
        CancellationToken cancellationToken = default)
    {
        var filter = await BuildFilterAsync(fromDate, toDate, productId, categoryId,
            isActive, searchTerm, sortBy, null, cancellationToken);
        if (page < 1)
        {
            ModelState.AddModelError(nameof(page), "Trang được chọn không hợp lệ.");
            page = 1;
        }

        var products = FilterProducts(filter);
        if (!string.IsNullOrWhiteSpace(filter.ViewModel.SearchTerm))
        {
            var keyword = filter.ViewModel.SearchTerm;
            products = products.Where(p => p.Code.Contains(keyword) || p.Name.Contains(keyword));
        }

        var start = filter.FromUtc ?? DateTime.MinValue;
        var end = filter.ToExclusiveUtc ?? DateTime.MaxValue;
        var totalCount = await products.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (totalCount + PageSize - 1) / PageSize);
        page = Math.Min(page, totalPages);

        // Sorting by movement is performed in SQL, before pagination.
        var sorted = filter.ViewModel.SortBy switch
        {
            "import" => products.OrderByDescending(p => context.InventoryTransactions
                    .Where(t => t.ProductId == p.Id && t.OccurredAt >= start && t.OccurredAt < end &&
                        t.TransactionType == InventoryTransactionType.Import &&
                        t.ImportReceipt!.Status == ReceiptStatus.Posted)
                    .Sum(t => (decimal?)t.QuantityChange) ?? 0m).ThenBy(p => p.Code),
            "export" => products.OrderByDescending(p => -(context.InventoryTransactions
                    .Where(t => t.ProductId == p.Id && t.OccurredAt >= start && t.OccurredAt < end &&
                        t.TransactionType == InventoryTransactionType.Export &&
                        t.ExportReceipt!.Status == ReceiptStatus.Posted)
                    .Sum(t => (decimal?)t.QuantityChange) ?? 0m)).ThenBy(p => p.Code),
            _ => products.OrderBy(p => p.Code)
        };

        var pageProducts = await sorted.Skip((page - 1) * PageSize).Take(PageSize)
            .Select(p => new { p.Id, p.Code, p.Name, p.Unit, CategoryName = p.Category.Name,
                p.CurrentQuantity, p.MinimumStockLevel })
            .ToListAsync(cancellationToken);
        var pageIds = pageProducts.Select(p => p.Id).ToArray();
        var movements = await context.InventoryTransactions.AsNoTracking()
            .Where(t => pageIds.Contains(t.ProductId) && t.OccurredAt < end)
            .GroupBy(t => t.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Opening = g.Sum(t => t.OccurredAt < start ? t.QuantityChange : 0m),
                Net = g.Sum(t => t.OccurredAt >= start ? t.QuantityChange : 0m),
                Imported = g.Sum(t => t.OccurredAt >= start &&
                    t.TransactionType == InventoryTransactionType.Import &&
                    t.ImportReceipt!.Status == ReceiptStatus.Posted ? t.QuantityChange : 0m),
                Exported = g.Sum(t => t.OccurredAt >= start &&
                    t.TransactionType == InventoryTransactionType.Export &&
                    t.ExportReceipt!.Status == ReceiptStatus.Posted ? -t.QuantityChange : 0m)
            })
            .ToDictionaryAsync(x => x.ProductId, cancellationToken);

        var rows = pageProducts.Select(p =>
        {
            movements.TryGetValue(p.Id, out var movement);
            var opening = movement?.Opening ?? 0m;
            var net = movement?.Net ?? 0m;
            var imported = movement?.Imported ?? 0m;
            var exported = movement?.Exported ?? 0m;
            return new WarehouseReportRowViewModel
            {
                ProductId = p.Id, Code = p.Code, Name = p.Name, Unit = p.Unit,
                CategoryName = p.CategoryName, OpeningQuantity = opening,
                ImportedQuantity = imported, ExportedQuantity = exported,
                OtherChange = net - imported + exported,
                ClosingQuantity = opening + net, CurrentQuantity = p.CurrentQuantity,
                MinimumStockLevel = p.MinimumStockLevel
            };
        }).ToList();

        return View(new WarehouseReportViewModel
        {
            Filter = filter.ViewModel, Rows = rows, Page = page,
            TotalPages = totalPages, TotalCount = totalCount
        });
    }

    private IQueryable<Product> FilterProducts(ParsedFilter filter)
    {
        var query = context.Products.AsNoTracking();
        if (!ModelState.IsValid)
        {
            return query.Where(_ => false);
        }
        if (filter.ViewModel.ProductId is int productId)
            query = query.Where(p => p.Id == productId);
        if (filter.ViewModel.CategoryId is int categoryId)
            query = query.Where(p => p.CategoryId == categoryId);
        if (filter.ViewModel.IsActive is bool isActive)
            query = query.Where(p => p.IsActive == isActive);
        return query;
    }

    private IQueryable<InventoryTransaction> PostedTransactions(
        IQueryable<Product> products, DateTime? fromUtc, DateTime? toExclusiveUtc)
    {
        var productIds = products.Select(p => p.Id);
        var query = context.InventoryTransactions.AsNoTracking()
            .Where(t => productIds.Contains(t.ProductId) &&
                ((t.TransactionType == InventoryTransactionType.Import &&
                  t.ImportReceipt!.Status == ReceiptStatus.Posted) ||
                 (t.TransactionType == InventoryTransactionType.Export &&
                  t.ExportReceipt!.Status == ReceiptStatus.Posted)));
        if (fromUtc.HasValue) query = query.Where(t => t.OccurredAt >= fromUtc.Value);
        if (toExclusiveUtc.HasValue) query = query.Where(t => t.OccurredAt < toExclusiveUtc.Value);
        return query;
    }

    private async Task<IReadOnlyList<ReportTimeBucketViewModel>> GetTimelineAsync(
        IQueryable<InventoryTransaction> period, bool monthly, CancellationToken cancellationToken)
    {
        var importGroups = await period.Where(t => t.TransactionType == InventoryTransactionType.Import)
            .GroupBy(t => new { t.OccurredAt.Year, t.OccurredAt.Month,
                Day = monthly ? 1 : t.OccurredAt.Day })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day,
                Quantity = g.Sum(t => t.QuantityChange),
                Receipts = g.Select(t => t.ImportReceiptId).Distinct().Count() })
            .OrderByDescending(g => g.Year).ThenByDescending(g => g.Month)
            .ThenByDescending(g => g.Day).Take(12).ToListAsync(cancellationToken);
        var exportGroups = await period.Where(t => t.TransactionType == InventoryTransactionType.Export)
            .GroupBy(t => new { t.OccurredAt.Year, t.OccurredAt.Month,
                Day = monthly ? 1 : t.OccurredAt.Day })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day,
                Quantity = -g.Sum(t => t.QuantityChange),
                Receipts = g.Select(t => t.ExportReceiptId).Distinct().Count() })
            .OrderByDescending(g => g.Year).ThenByDescending(g => g.Month)
            .ThenByDescending(g => g.Day).Take(12).ToListAsync(cancellationToken);

        var keys = importGroups.Select(x => (x.Year, x.Month, x.Day))
            .Concat(exportGroups.Select(x => (x.Year, x.Month, x.Day)))
            .Distinct().OrderByDescending(x => x.Year).ThenByDescending(x => x.Month)
            .ThenByDescending(x => x.Day).Take(12).Reverse();
        return keys.Select(key =>
        {
            var imported = importGroups.FirstOrDefault(x => (x.Year, x.Month, x.Day) == key);
            var exported = exportGroups.FirstOrDefault(x => (x.Year, x.Month, x.Day) == key);
            return new ReportTimeBucketViewModel
            {
                Label = monthly ? $"{key.Month:00}/{key.Year}" : $"{key.Day:00}/{key.Month:00}/{key.Year}",
                ImportedQuantity = imported?.Quantity ?? 0m,
                ExportedQuantity = exported?.Quantity ?? 0m,
                ImportReceiptCount = imported?.Receipts ?? 0,
                ExportReceiptCount = exported?.Receipts ?? 0
            };
        }).ToList();
    }

    private async Task<ParsedFilter> BuildFilterAsync(
        string? fromDate, string? toDate, int? productId, int? categoryId,
        string? isActive, string? searchTerm, string? sortBy, string? groupBy,
        CancellationToken cancellationToken)
    {
        DateTime? from = ParseUtcDate(fromDate, nameof(fromDate));
        DateTime? to = ParseUtcDate(toDate, nameof(toDate));
        if (from.HasValue && to.HasValue && from > to)
            ModelState.AddModelError(nameof(toDate), "Ngày kết thúc phải từ ngày bắt đầu trở đi.");
        if (to == DateTime.MaxValue.Date)
            ModelState.AddModelError(nameof(toDate), "Ngày kết thúc vượt phạm vi hỗ trợ.");
        bool? active = null;
        if (!string.IsNullOrWhiteSpace(isActive))
        {
            if (bool.TryParse(isActive, out var parsed)) active = parsed;
            else ModelState.AddModelError(nameof(isActive), "Trạng thái hàng hóa không hợp lệ.");
        }
        if (productId.HasValue && !await context.Products.AsNoTracking()
            .AnyAsync(p => p.Id == productId.Value, cancellationToken))
            ModelState.AddModelError(nameof(productId), "Hàng hóa được chọn không tồn tại.");
        if (categoryId.HasValue && !await context.Categories.AsNoTracking()
            .AnyAsync(c => c.Id == categoryId.Value, cancellationToken))
            ModelState.AddModelError(nameof(categoryId), "Danh mục được chọn không tồn tại.");
        searchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim();
        if (searchTerm?.Length > MaximumSearchLength)
            ModelState.AddModelError(nameof(searchTerm), $"Từ khóa không được vượt quá {MaximumSearchLength} ký tự.");
        sortBy ??= "code";
        if (sortBy is not ("code" or "import" or "export"))
        {
            ModelState.AddModelError(nameof(sortBy), "Cách sắp xếp không hợp lệ.");
            sortBy = "code";
        }
        groupBy ??= "day";
        if (groupBy is not ("day" or "month"))
        {
            ModelState.AddModelError(nameof(groupBy), "Kiểu thống kê thời gian không hợp lệ.");
            groupBy = "day";
        }
        var products = await context.Products.AsNoTracking().OrderBy(p => p.Code)
            .Select(p => new ReportOptionViewModel { Id = p.Id, Text = p.Code + " — " + p.Name })
            .ToListAsync(cancellationToken);
        var categories = await context.Categories.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new ReportOptionViewModel { Id = c.Id, Text = c.Name })
            .ToListAsync(cancellationToken);
        return new ParsedFilter(new ReportFilterViewModel
        {
            FromDate = fromDate, ToDate = toDate, ProductId = productId,
            CategoryId = categoryId, IsActive = active, SearchTerm = searchTerm,
            SortBy = sortBy, GroupBy = groupBy,
            Products = products, Categories = categories
        }, from, to?.AddDays(1));
    }

    private DateTime? ParseUtcDate(string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsed))
            return DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
        ModelState.AddModelError(key, "Ngày không hợp lệ; vui lòng dùng định dạng yyyy-MM-dd.");
        return null;
    }

    private sealed record ParsedFilter(
        ReportFilterViewModel ViewModel, DateTime? FromUtc, DateTime? ToExclusiveUtc);
}
