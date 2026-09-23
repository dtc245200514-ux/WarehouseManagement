using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarehouseManagement.Authorization;
using WarehouseManagement.Data;
using WarehouseManagement.Models;
using WarehouseManagement.Models.Enums;
using WarehouseManagement.Models.InventoryManagement;

namespace WarehouseManagement.Controllers;

[Authorize(Policy = ApplicationPolicies.ViewInventory)]
public class InventoryController(ApplicationDbContext context) : Controller
{
    private const int PageSize = 20;
    private const int MaximumSearchLength = 200;

    [HttpGet]
    public async Task<IActionResult> Transactions(
        int? productId,
        string? transactionType,
        string? fromDate,
        string? toDate,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var query = VisibleTransactions();
        if (productId.HasValue)
        {
            if (!await context.Products.AsNoTracking()
                .AnyAsync(product => product.Id == productId.Value, cancellationToken))
            {
                ModelState.AddModelError(nameof(productId), "Hàng hóa được chọn không tồn tại.");
            }
            else
            {
                query = query.Where(transaction => transaction.ProductId == productId.Value);
            }
        }

        InventoryTransactionType? selectedType = null;
        if (!string.IsNullOrWhiteSpace(transactionType))
        {
            if (!Enum.TryParse<InventoryTransactionType>(transactionType, true, out var parsedType) ||
                !Enum.IsDefined(parsedType))
            {
                ModelState.AddModelError(nameof(transactionType), "Loại giao dịch không hợp lệ.");
            }
            else
            {
                selectedType = parsedType;
                query = query.Where(transaction => transaction.TransactionType == parsedType);
            }
        }

        DateTime? startDate = ParseDate(fromDate, nameof(fromDate));
        DateTime? endDate = ParseDate(toDate, nameof(toDate));
        if (startDate.HasValue && endDate.HasValue && startDate > endDate)
        {
            ModelState.AddModelError(nameof(toDate), "Ngày kết thúc phải từ ngày bắt đầu trở đi.");
        }
        if (endDate == DateTime.MaxValue.Date)
        {
            ModelState.AddModelError(nameof(toDate), "Ngày kết thúc vượt phạm vi hỗ trợ.");
        }

        if (page < 1)
        {
            ModelState.AddModelError(nameof(page), "Trang được chọn không hợp lệ.");
            page = 1;
        }

        if (ModelState.IsValid)
        {
            if (startDate.HasValue)
            {
                var startUtc = DateTime.SpecifyKind(startDate.Value, DateTimeKind.Local)
                    .ToUniversalTime();
                query = query.Where(transaction => transaction.OccurredAt >= startUtc);
            }
            if (endDate.HasValue)
            {
                var exclusiveEndUtc = DateTime.SpecifyKind(endDate.Value.AddDays(1), DateTimeKind.Local)
                    .ToUniversalTime();
                query = query.Where(transaction => transaction.OccurredAt < exclusiveEndUtc);
            }
        }
        else
        {
            query = query.Where(_ => false);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (totalCount + PageSize - 1) / PageSize);
        page = Math.Min(page, totalPages);
        var transactions = await ProjectTransactions(query
                .OrderByDescending(transaction => transaction.OccurredAt)
                .ThenByDescending(transaction => transaction.Id)
                .Skip((page - 1) * PageSize)
                .Take(PageSize))
            .ToListAsync(cancellationToken);
        var products = await context.Products.AsNoTracking()
            .OrderBy(product => product.Code)
            .Select(product => new InventoryTransactionProductOptionViewModel
            {
                Id = product.Id,
                Code = product.Code,
                Name = product.Name
            })
            .ToListAsync(cancellationToken);

        return View(new InventoryTransactionsViewModel
        {
            ProductId = productId,
            TransactionType = selectedType,
            FromDate = startDate,
            ToDate = endDate,
            Page = page,
            TotalPages = totalPages,
            TotalCount = totalCount,
            Products = products,
            Transactions = transactions
        });
    }

    [HttpGet]
    public async Task<IActionResult> TransactionDetails(long id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        var transaction = await ProjectTransactions(VisibleTransactions()
                .Where(item => item.Id == id))
            .SingleOrDefaultAsync(cancellationToken);
        return transaction is null ? NotFound() : View(transaction);
    }

    private DateTime? ParseDate(string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsedDate))
        {
            return parsedDate.Date;
        }

        ModelState.AddModelError(key, "Ngày không hợp lệ; vui lòng dùng định dạng ngày/tháng/năm.");
        return null;
    }

    private IQueryable<InventoryTransaction> VisibleTransactions() =>
        context.InventoryTransactions.AsNoTracking().Where(transaction =>
            (transaction.ImportReceiptId == null ||
                transaction.ImportReceipt!.Status != ReceiptStatus.Draft) &&
            (transaction.ExportReceiptId == null ||
                transaction.ExportReceipt!.Status != ReceiptStatus.Draft));

    private static IQueryable<InventoryTransactionListItemViewModel> ProjectTransactions(
        IQueryable<InventoryTransaction> query) => query.Select(transaction =>
        new InventoryTransactionListItemViewModel
        {
            Id = transaction.Id,
            OccurredAt = transaction.OccurredAt,
            ProductId = transaction.ProductId,
            ProductCode = transaction.Product.Code,
            ProductName = transaction.Product.Name,
            Unit = transaction.Product.Unit,
            TransactionType = transaction.TransactionType,
            QuantityChange = transaction.QuantityChange,
            BalanceBefore = transaction.BalanceBefore,
            BalanceAfter = transaction.BalanceAfter,
            ImportReceiptId = transaction.ImportReceiptId,
            ImportReceiptNumber = transaction.ImportReceipt == null
                ? null : transaction.ImportReceipt.ReceiptNumber,
            ExportReceiptId = transaction.ExportReceiptId,
            ExportReceiptNumber = transaction.ExportReceipt == null
                ? null : transaction.ExportReceipt.ReceiptNumber,
            ReceiptStatus = transaction.ImportReceipt != null
                ? (ReceiptStatus?)transaction.ImportReceipt.Status
                : transaction.ExportReceipt != null
                    ? (ReceiptStatus?)transaction.ExportReceipt.Status : null,
            PerformedByName = transaction.PerformedByUser.FullName,
            Note = transaction.Note
        });

    [HttpGet]
    public async Task<IActionResult> Index(
        string? searchTerm,
        int? categoryId,
        string? stockStatus,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        searchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim();
        var query = context.Products.AsNoTracking();

        if (searchTerm is not null)
        {
            if (searchTerm.Length > MaximumSearchLength)
            {
                ModelState.AddModelError(nameof(searchTerm),
                    $"Từ khóa không được vượt quá {MaximumSearchLength} ký tự.");
            }
            else
            {
                query = query.Where(product =>
                    product.Code.Contains(searchTerm) || product.Name.Contains(searchTerm));
            }
        }

        if (categoryId.HasValue)
        {
            if (!await context.Categories.AsNoTracking()
                .AnyAsync(category => category.Id == categoryId.Value, cancellationToken))
            {
                ModelState.AddModelError(nameof(categoryId), "Danh mục được chọn không hợp lệ.");
            }
            else
            {
                query = query.Where(product => product.CategoryId == categoryId.Value);
            }
        }

        StockLevelStatus? selectedStockStatus = null;
        if (!string.IsNullOrWhiteSpace(stockStatus))
        {
            if (!Enum.TryParse<StockLevelStatus>(stockStatus, true, out var parsedStatus) ||
                !Enum.IsDefined(parsedStatus))
            {
                ModelState.AddModelError(nameof(stockStatus), "Trạng thái tồn không hợp lệ.");
            }
            else
            {
                selectedStockStatus = parsedStatus;
                query = parsedStatus switch
                {
                    StockLevelStatus.OutOfStock => query.Where(product => product.CurrentQuantity == 0m),
                    StockLevelStatus.LowStock => query.Where(product =>
                        product.CurrentQuantity > 0m &&
                        product.CurrentQuantity <= product.MinimumStockLevel),
                    _ => query.Where(product => product.CurrentQuantity > product.MinimumStockLevel)
                };
            }
        }

        if (page < 1)
        {
            ModelState.AddModelError(nameof(page), "Trang được chọn không hợp lệ.");
            page = 1;
        }

        if (!ModelState.IsValid)
        {
            query = query.Where(_ => false);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (totalCount + PageSize - 1) / PageSize);
        page = Math.Min(page, totalPages);

        var products = await query
            .OrderBy(product => product.Code)
            .ThenBy(product => product.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(product => new InventoryListItemViewModel
            {
                Id = product.Id,
                Code = product.Code,
                Name = product.Name,
                CategoryName = product.Category.Name,
                Unit = product.Unit,
                CurrentQuantity = product.CurrentQuantity,
                MinimumStockLevel = product.MinimumStockLevel,
                IsActive = product.IsActive,
                StockStatus = product.CurrentQuantity == 0m
                    ? StockLevelStatus.OutOfStock
                    : product.CurrentQuantity <= product.MinimumStockLevel
                        ? StockLevelStatus.LowStock
                        : StockLevelStatus.InStock
            })
            .ToListAsync(cancellationToken);

        var categories = await context.Categories.AsNoTracking()
            .Where(category => category.Products.Any())
            .OrderBy(category => category.Name)
            .Select(category => new InventoryCategoryOptionViewModel
            {
                Id = category.Id,
                Name = category.Name
            })
            .ToListAsync(cancellationToken);

        return View(new InventoryIndexViewModel
        {
            SearchTerm = searchTerm,
            CategoryId = categoryId,
            StockStatus = selectedStockStatus,
            Page = page,
            TotalPages = totalPages,
            TotalCount = totalCount,
            Products = products,
            Categories = categories
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        int id,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        var product = await context.Products.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                item.Id,
                item.Code,
                item.Name,
                CategoryName = item.Category.Name,
                item.Unit,
                item.CurrentQuantity,
                item.MinimumStockLevel,
                item.IsActive
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        if (page < 1)
        {
            ModelState.AddModelError(nameof(page), "Trang lịch sử được chọn không hợp lệ.");
            page = 1;
        }

        var query = context.InventoryTransactions.AsNoTracking()
            .Where(transaction => transaction.ProductId == id);
        var summary = await query.GroupBy(transaction => transaction.ProductId)
            .Select(group => new
            {
                Count = group.Count(),
                LedgerQuantity = group.Sum(transaction => transaction.QuantityChange),
                PostedImportQuantity = group.Sum(transaction =>
                    transaction.TransactionType == InventoryTransactionType.Import &&
                    transaction.ImportReceipt != null &&
                    transaction.ImportReceipt.Status == ReceiptStatus.Posted
                        ? transaction.QuantityChange : 0m),
                PostedExportQuantity = group.Sum(transaction =>
                    transaction.TransactionType == InventoryTransactionType.Export &&
                    transaction.ExportReceipt != null &&
                    transaction.ExportReceipt.Status == ReceiptStatus.Posted
                        ? -transaction.QuantityChange : 0m),
                LastTransactionAt = group.Max(transaction => transaction.OccurredAt)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var transactionCount = summary?.Count ?? 0;
        var ledgerQuantity = summary?.LedgerQuantity ?? 0m;
        var totalPages = Math.Max(1, (transactionCount + PageSize - 1) / PageSize);
        page = Math.Min(page, totalPages);
        var firstBalance = transactionCount == 0 ? 0m : await query
            .OrderBy(transaction => transaction.Id)
            .Select(transaction => transaction.BalanceBefore)
            .FirstAsync(cancellationToken);
        var lastBalance = transactionCount == 0 ? 0m : await query
            .OrderByDescending(transaction => transaction.Id)
            .Select(transaction => transaction.BalanceAfter)
            .FirstAsync(cancellationToken);

        var reconciliationStatus = product.CurrentQuantity != ledgerQuantity ||
            (transactionCount > 0 && lastBalance != product.CurrentQuantity)
                ? InventoryReconciliationStatus.Mismatch
                : transactionCount > 0 && firstBalance != 0m
                    ? InventoryReconciliationStatus.InsufficientData
                    : InventoryReconciliationStatus.Match;
        var reconciliationMessage = reconciliationStatus switch
        {
            InventoryReconciliationStatus.Mismatch =>
                "Tồn hiện tại không khớp tổng biến động hoặc số dư giao dịch cuối; cần kiểm tra lịch sử.",
            InventoryReconciliationStatus.InsufficientData =>
                "Lịch sử bắt đầu từ số dư khác 0; không đủ dữ liệu để xác nhận toàn bộ tồn đầu kỳ.",
            _ when transactionCount == 0 =>
                "Hàng hóa chưa phát sinh giao dịch; tồn hiện tại bằng 0.",
            _ => "Tồn hiện tại khớp tổng biến động và số dư giao dịch cuối."
        };

        var transactions = await query
            .OrderByDescending(transaction => transaction.OccurredAt)
            .ThenByDescending(transaction => transaction.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(transaction => new InventoryHistoryItemViewModel
            {
                Id = transaction.Id,
                OccurredAt = transaction.OccurredAt,
                TransactionType = transaction.TransactionType,
                ImportReceiptId = transaction.ImportReceiptId,
                ExportReceiptId = transaction.ExportReceiptId,
                ImportReceiptNumber = transaction.ImportReceipt == null
                    ? null : transaction.ImportReceipt.ReceiptNumber,
                ExportReceiptNumber = transaction.ExportReceipt == null
                    ? null : transaction.ExportReceipt.ReceiptNumber,
                ReceiptStatus = transaction.ImportReceipt != null
                    ? (ReceiptStatus?)transaction.ImportReceipt.Status
                    : transaction.ExportReceipt != null
                        ? (ReceiptStatus?)transaction.ExportReceipt.Status : null,
                QuantityChange = transaction.QuantityChange,
                BalanceAfter = transaction.BalanceAfter,
                PerformedByName = transaction.PerformedByUser.FullName,
                Note = transaction.Note
            })
            .ToListAsync(cancellationToken);

        return View(new InventoryDetailsViewModel
        {
            Id = product.Id,
            Code = product.Code,
            Name = product.Name,
            CategoryName = product.CategoryName,
            Unit = product.Unit,
            CurrentQuantity = product.CurrentQuantity,
            MinimumStockLevel = product.MinimumStockLevel,
            IsActive = product.IsActive,
            StockStatus = GetStockStatus(product.CurrentQuantity, product.MinimumStockLevel),
            PostedImportQuantity = summary?.PostedImportQuantity ?? 0m,
            PostedExportQuantity = summary?.PostedExportQuantity ?? 0m,
            LedgerQuantity = ledgerQuantity,
            LastTransactionAt = summary?.LastTransactionAt,
            TransactionCount = transactionCount,
            ReconciliationStatus = reconciliationStatus,
            ReconciliationMessage = reconciliationMessage,
            Page = page,
            TotalPages = totalPages,
            Transactions = transactions
        });
    }

    private static StockLevelStatus GetStockStatus(decimal quantity, decimal minimum) =>
        quantity == 0m ? StockLevelStatus.OutOfStock :
        quantity <= minimum ? StockLevelStatus.LowStock : StockLevelStatus.InStock;
}
