using System.Data.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarehouseManagement.Authorization;
using WarehouseManagement.Models;
using WarehouseManagement.Models.InventoryManagement;

namespace WarehouseManagement.Controllers;

public partial class InventoryController
{
    private const int MaximumExportRows = 10000;

    [HttpGet]
    public async Task<IActionResult> LowStock(string? searchTerm, int? categoryId, string? stockStatus,
        int page = 1, CancellationToken cancellationToken = default)
    {
        searchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim();
        try
        {
            var (query, selectedStatus) = await LowStockQueryAsync(searchTerm, categoryId, stockStatus, cancellationToken);
            if (page < 1)
                ModelState.AddModelError(nameof(page), "Trang được chọn không hợp lệ.");
            if (!ModelState.IsValid) query = query.Where(_ => false);
            var counts = await query.GroupBy(_ => 1).Select(g => new
            {
                Total = g.Count(), Out = g.Count(p => p.CurrentQuantity == 0m)
            }).SingleOrDefaultAsync(cancellationToken);
            var totalPages = Math.Max(1, (int)Math.Ceiling((counts?.Total ?? 0) / (double)PageSize));
            page = Math.Clamp(page, 1, totalPages);
            var products = await LowStockRows(query.OrderBy(p => p.CurrentQuantity == 0m ? 0 : 1)
                    .ThenBy(p => p.Code).ThenBy(p => p.Id).Skip((page - 1) * PageSize).Take(PageSize))
                .ToListAsync(cancellationToken);
            var categories = await context.Categories.AsNoTracking().OrderBy(c => c.Name)
                .Select(c => new InventoryCategoryOptionViewModel { Id = c.Id, Name = c.Name })
                .ToListAsync(cancellationToken);
            return View(new LowStockViewModel
            {
                SearchTerm = searchTerm, CategoryId = categoryId, StockStatus = selectedStatus,
                TotalCount = counts?.Total ?? 0, OutOfStockCount = counts?.Out ?? 0,
                Page = page, TotalPages = totalPages, Products = products, Categories = categories
            });
        }
        catch (DbException exception)
        {
            logger.LogError(exception, "Cannot load low-stock alerts.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ModelState.AddModelError(string.Empty, "Không thể tải cảnh báo tồn kho. Vui lòng thử lại sau.");
            return View(new LowStockViewModel { SearchTerm = searchTerm, CategoryId = categoryId });
        }
    }

    [HttpGet]
    [Authorize(Policy = ApplicationPolicies.ViewReports)]
    public async Task<IActionResult> ExportLowStock(string? searchTerm, int? categoryId, string? stockStatus,
        CancellationToken cancellationToken = default)
    {
        searchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim();
        try
        {
            var (query, _) = await LowStockQueryAsync(searchTerm, categoryId, stockStatus, cancellationToken);
            if (!ModelState.IsValid)
                return BadRequest("Bộ lọc không hợp lệ. Vui lòng quay lại danh sách và kiểm tra điều kiện lọc.");
            // One bounded query exports every matching row, independent of the current page.
            var rows = await LowStockRows(query.OrderBy(p => p.CurrentQuantity == 0m ? 0 : 1)
                    .ThenBy(p => p.Code).ThenBy(p => p.Id).Take(MaximumExportRows + 1))
                .ToListAsync(cancellationToken);
            if (rows.Count > MaximumExportRows)
                return BadRequest("Kết quả vượt 10.000 hàng. Vui lòng thu hẹp bộ lọc trước khi xuất CSV.");
            Response.Headers.CacheControl = "no-store";
            return File(LowStockCsv.Write(rows), "text/csv; charset=utf-8",
                $"canh-bao-ton-kho-{DateTime.UtcNow:yyyyMMdd-HHmmss}-UTC.csv");
        }
        catch (DbException exception)
        {
            logger.LogError(exception, "Cannot export low-stock alerts.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                "Không thể xuất cảnh báo tồn kho. Vui lòng thử lại sau.");
        }
    }

    private async Task<(IQueryable<Product> Query, StockLevelStatus? Status)> LowStockQueryAsync(
        string? searchTerm, int? categoryId, string? stockStatus, CancellationToken cancellationToken)
    {
        var query = context.Products.AsNoTracking().Where(p => p.IsActive).Where(StockLevelRules.IsAlert);
        if (searchTerm?.Length > MaximumSearchLength)
            ModelState.AddModelError(nameof(searchTerm), $"Từ khóa không được vượt quá {MaximumSearchLength} ký tự.");
        else if (searchTerm is not null)
            query = query.Where(p => p.Code.Contains(searchTerm) || p.Name.Contains(searchTerm));
        if (categoryId.HasValue)
        {
            if (!await context.Categories.AsNoTracking().AnyAsync(c => c.Id == categoryId, cancellationToken))
                ModelState.AddModelError(nameof(categoryId), "Danh mục được chọn không hợp lệ.");
            query = query.Where(p => p.CategoryId == categoryId);
        }
        StockLevelStatus? selectedStatus = null;
        if (!string.IsNullOrWhiteSpace(stockStatus))
        {
            if (Enum.TryParse<StockLevelStatus>(stockStatus, true, out var parsed) &&
                parsed is StockLevelStatus.OutOfStock or StockLevelStatus.LowStock)
            {
                selectedStatus = parsed;
                query = query.WithStatus(parsed);
            }
            else ModelState.AddModelError(nameof(stockStatus), "Trạng thái cảnh báo không hợp lệ.");
        }
        return (query, selectedStatus);
    }

    private static IQueryable<InventoryListItemViewModel> LowStockRows(IQueryable<Product> query) =>
        query.Select(p => new InventoryListItemViewModel
        {
            Id = p.Id, Code = p.Code, Name = p.Name, CategoryName = p.Category.Name, Unit = p.Unit,
            CurrentQuantity = p.CurrentQuantity, MinimumStockLevel = p.MinimumStockLevel, IsActive = p.IsActive,
            StockStatus = StockLevelRules.Classify(p.CurrentQuantity, p.MinimumStockLevel)
        });
}
