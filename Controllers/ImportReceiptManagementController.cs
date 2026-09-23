using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using WarehouseManagement.Authorization;
using WarehouseManagement.Data;
using WarehouseManagement.Models;
using WarehouseManagement.Models.Enums;
using WarehouseManagement.Models.ImportReceiptManagement;

namespace WarehouseManagement.Controllers;

[Authorize(Policy = ApplicationPolicies.CreateImportReceipts)]
public class ImportReceiptManagementController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    ILogger<ImportReceiptManagementController> logger) : Controller
{
    private const int MaximumDetails = 500;
    private const int MaximumSearchLength = 100;
    private const decimal MaximumQuantity = 999999999999999.999m;
    private const decimal MaximumUnitCost = 9999999999999999.99m;

    [HttpGet]
    public async Task<IActionResult> Index(
        string? searchTerm,
        int? supplierId,
        ReceiptStatus? status,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken)
    {
        searchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim();
        var query = context.ImportReceipts.AsNoTracking();

        if (searchTerm is not null)
        {
            if (searchTerm.Length > MaximumSearchLength)
            {
                ModelState.AddModelError(nameof(searchTerm),
                    $"Từ khóa không được vượt quá {MaximumSearchLength} ký tự.");
                query = query.Where(_ => false);
            }
            else
            {
                query = query.Where(receipt => receipt.ReceiptNumber.Contains(searchTerm));
            }
        }

        if (supplierId.HasValue)
        {
            if (supplierId.Value <= 0 ||
                !await context.Suppliers.AsNoTracking()
                    .AnyAsync(supplier => supplier.Id == supplierId.Value, cancellationToken))
            {
                ModelState.AddModelError(nameof(supplierId),
                    "Nhà cung cấp được chọn không hợp lệ.");
                query = query.Where(_ => false);
            }
            else
            {
                query = query.Where(receipt => receipt.SupplierId == supplierId.Value);
            }
        }

        if (status.HasValue)
        {
            if (!Enum.IsDefined(status.Value))
            {
                ModelState.AddModelError(nameof(status), "Trạng thái phiếu không hợp lệ.");
                query = query.Where(_ => false);
            }
            else
            {
                query = query.Where(receipt => receipt.Status == status.Value);
            }
        }

        if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
        {
            ModelState.AddModelError(nameof(toDate),
                "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.");
            query = query.Where(_ => false);
        }
        else
        {
            if (fromDate.HasValue)
            {
                var startDate = fromDate.Value.Date;
                query = query.Where(receipt => receipt.ReceiptDate >= startDate);
            }

            if (toDate.HasValue)
            {
                var endDate = toDate.Value.Date;
                query = query.Where(receipt => receipt.ReceiptDate <= endDate);
            }
        }

        var receipts = await query
            .OrderByDescending(receipt => receipt.ReceiptDate)
            .ThenByDescending(receipt => receipt.Id)
            .Select(receipt => new ImportReceiptListItemViewModel
            {
                Id = receipt.Id,
                ReceiptNumber = receipt.ReceiptNumber,
                ReceiptDate = receipt.ReceiptDate,
                SupplierCode = receipt.Supplier.Code,
                SupplierName = receipt.Supplier.Name,
                CreatedByName = receipt.CreatedByUser.FullName,
                Status = receipt.Status,
                TotalAmount = receipt.Details
                    .Sum(detail => (decimal?)(detail.Quantity * detail.UnitCost)) ?? 0m,
                CreatedAt = receipt.CreatedAt,
                PostedAt = receipt.PostedAt,
                RowVersion = Convert.ToBase64String(receipt.RowVersion)
            })
            .ToListAsync(cancellationToken);

        return View(new ImportReceiptIndexViewModel
        {
            SearchTerm = searchTerm,
            SupplierId = supplierId,
            Status = status,
            FromDate = fromDate,
            ToDate = toDate,
            Receipts = receipts,
            Suppliers = await GetSupplierOptionsAsync(false, cancellationToken)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var receipt = await context.ImportReceipts
            .AsNoTracking()
            .Include(item => item.Supplier)
            .Include(item => item.CreatedByUser)
            .Include(item => item.PostedByUser)
            .Include(item => item.Details)
                .ThenInclude(detail => detail.Product)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (receipt is null)
        {
            return NotFound();
        }

        var details = receipt.Details
            .OrderBy(detail => detail.Product.Code)
            .Select(detail => new ImportReceiptDetailItemViewModel
            {
                ProductId = detail.ProductId,
                ProductCode = detail.Product.Code,
                ProductName = detail.Product.Name,
                Unit = detail.Product.Unit,
                Quantity = detail.Quantity,
                UnitCost = detail.UnitCost,
                LineTotal = detail.Quantity * detail.UnitCost
            })
            .ToList();

        return View(new ImportReceiptDetailsViewModel
        {
            Id = receipt.Id,
            ReceiptNumber = receipt.ReceiptNumber,
            ReceiptDate = receipt.ReceiptDate,
            SupplierCode = receipt.Supplier.Code,
            SupplierName = receipt.Supplier.Name,
            CreatedByName = receipt.CreatedByUser.FullName,
            PostedByName = receipt.PostedByUser?.FullName,
            Status = receipt.Status,
            Note = receipt.Note,
            CreatedAt = receipt.CreatedAt,
            PostedAt = receipt.PostedAt,
            TotalAmount = details.Sum(detail => detail.LineTotal),
            RowVersion = Convert.ToBase64String(receipt.RowVersion),
            Details = details
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = new CreateImportReceiptViewModel
        {
            ReceiptDate = DateTime.Today,
            Details = [new CreateImportReceiptDetailViewModel()]
        };

        await PopulateOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CreateImportReceiptViewModel model,
        CancellationToken cancellationToken)
    {
        model.Note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim();
        model.Details ??= [];

        ValidateSubmittedDetails(model);
        await ValidateReferencesAsync(model, cancellationToken);

        var userId = userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId) ||
            !await context.Users.AsNoTracking().AnyAsync(user => user.Id == userId, cancellationToken))
        {
            ModelState.AddModelError(string.Empty,
                "Không xác định được tài khoản đang đăng nhập. Vui lòng đăng nhập lại.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model, cancellationToken);
            return View(model);
        }

        var receipt = new ImportReceipt
        {
            ReceiptNumber = GenerateReceiptNumber(),
            SupplierId = model.SupplierId,
            ReceiptDate = model.ReceiptDate.Date,
            Note = model.Note,
            Status = ReceiptStatus.Draft,
            CreatedByUserId = userId!,
            CreatedAt = DateTime.UtcNow,
            Details = model.Details.Select(detail => new ImportReceiptDetail
            {
                ProductId = detail.ProductId,
                Quantity = detail.Quantity,
                UnitCost = detail.UnitCost
            }).ToList()
        };

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            context.ImportReceipts.Add(receipt);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogWarning(exception,
                "Không thể tạo phiếu nhập Draft do dữ liệu duy nhất bị trùng.");
            ModelState.AddModelError(string.Empty,
                "Không thể lưu phiếu do dữ liệu bị trùng. Vui lòng kiểm tra và thử lại.");
            await PopulateOptionsAsync(model, cancellationToken);
            return View(model);
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogError(exception, "Không thể tạo phiếu nhập Draft.");
            ModelState.AddModelError(string.Empty,
                "Không thể lưu phiếu nhập. Không có dữ liệu nào được thay đổi.");
            await PopulateOptionsAsync(model, cancellationToken);
            return View(model);
        }

        var totalAmount = model.Details.Sum(detail => detail.Quantity * detail.UnitCost);
        TempData["SuccessMessage"] =
            $"Đã lưu phiếu nhập {receipt.ReceiptNumber} ở trạng thái Nháp. " +
            $"Tổng tiền: {totalAmount.ToString("N2", CultureInfo.GetCultureInfo("vi-VN"))}.";
        return RedirectToAction(nameof(Details), new { receipt.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Post(
        int id,
        string rowVersion,
        CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId) ||
            !await context.Users.AsNoTracking()
                .AnyAsync(user => user.Id == userId && user.IsActive, cancellationToken))
        {
            TempData["ErrorMessage"] =
                "Không xác định được tài khoản đang hoạt động. Vui lòng đăng nhập lại.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var concurrencyToken = TryDecodeRowVersion(rowVersion);
        if (concurrencyToken is null)
        {
            TempData["ErrorMessage"] =
                "Dữ liệu đồng bộ không hợp lệ. Vui lòng tải lại phiếu.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await using var databaseTransaction = await context.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            var receipt = await context.ImportReceipts
                .Include(item => item.Supplier)
                .Include(item => item.Details)
                    .ThenInclude(detail => detail.Product)
                .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (receipt is null)
            {
                return NotFound();
            }

            if (receipt.Status != ReceiptStatus.Draft)
            {
                TempData["ErrorMessage"] = receipt.Status == ReceiptStatus.Posted
                    ? "Phiếu nhập đã được ghi sổ trước đó. Tồn kho không được cập nhật lại."
                    : "Chỉ phiếu nhập ở trạng thái Nháp mới được ghi sổ.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (receipt.Details.Count == 0)
            {
                TempData["ErrorMessage"] = "Phiếu nhập không có chi tiết hàng hóa.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (receipt.Details.GroupBy(detail => detail.ProductId).Any(group => group.Count() > 1))
            {
                TempData["ErrorMessage"] = "Phiếu nhập có hàng hóa bị trùng.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (receipt.Details.Any(detail =>
                detail.Product is null || !detail.Product.IsActive ||
                detail.Quantity <= 0m || detail.Quantity > MaximumQuantity ||
                detail.UnitCost < 0m || detail.UnitCost > MaximumUnitCost))
            {
                TempData["ErrorMessage"] =
                    "Phiếu nhập có sản phẩm không hoạt động hoặc dữ liệu số lượng, đơn giá không hợp lệ.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (await context.InventoryTransactions.AsNoTracking()
                .AnyAsync(transaction =>
                    transaction.ImportReceiptId == receipt.Id &&
                    transaction.TransactionType == InventoryTransactionType.Import,
                    cancellationToken))
            {
                TempData["ErrorMessage"] =
                    "Phiếu đã có giao dịch nhập kho. Hệ thống từ chối ghi sổ lại để bảo vệ tồn kho.";
                return RedirectToAction(nameof(Details), new { id });
            }

            context.Entry(receipt)
                .Property(item => item.RowVersion)
                .OriginalValue = concurrencyToken;

            var occurredAt = DateTime.UtcNow;
            foreach (var detail in receipt.Details)
            {
                var product = detail.Product;
                var balanceBefore = product.CurrentQuantity;
                var balanceAfter = checked(balanceBefore + detail.Quantity);
                if (balanceAfter > MaximumQuantity)
                {
                    throw new OverflowException(
                        $"Tồn kho của sản phẩm {product.Code} vượt quá giới hạn cho phép.");
                }

                decimal newAverageUnitCost;
                if (balanceBefore == 0m)
                {
                    newAverageUnitCost = detail.UnitCost;
                }
                else
                {
                    if (!product.AverageUnitCost.HasValue)
                    {
                        throw new InvalidOperationException(
                            $"Sản phẩm {product.Code} đang có tồn kho nhưng chưa có giá vốn bình quân.");
                    }

                    var oldInventoryValue = checked(balanceBefore * product.AverageUnitCost.Value);
                    var importedValue = checked(detail.Quantity * detail.UnitCost);
                    newAverageUnitCost = decimal.Round(
                        checked(oldInventoryValue + importedValue) / balanceAfter,
                        2,
                        MidpointRounding.AwayFromZero);
                }

                if (newAverageUnitCost > MaximumUnitCost)
                {
                    throw new OverflowException(
                        $"Giá vốn bình quân của sản phẩm {product.Code} vượt quá giới hạn cho phép.");
                }

                product.CurrentQuantity = balanceAfter;
                product.AverageUnitCost = newAverageUnitCost;
                product.UpdatedAt = occurredAt;

                context.InventoryTransactions.Add(new InventoryTransaction
                {
                    ProductId = product.Id,
                    TransactionType = InventoryTransactionType.Import,
                    QuantityChange = detail.Quantity,
                    BalanceBefore = balanceBefore,
                    BalanceAfter = balanceAfter,
                    ImportReceiptId = receipt.Id,
                    PerformedByUserId = userId,
                    OccurredAt = occurredAt,
                    Note = $"Ghi sổ phiếu nhập {receipt.ReceiptNumber}."
                });
            }

            receipt.Status = ReceiptStatus.Posted;
            receipt.PostedByUserId = userId;
            receipt.PostedAt = occurredAt;

            await context.SaveChangesAsync(cancellationToken);
            await databaseTransaction.CommitAsync(cancellationToken);

            TempData["SuccessMessage"] =
                $"Đã ghi sổ phiếu nhập {receipt.ReceiptNumber}. Tồn kho đã được cập nhật.";
        }
        catch (DbUpdateConcurrencyException exception)
        {
            await databaseTransaction.RollbackAsync(cancellationToken);
            logger.LogWarning(exception,
                "Xung đột đồng thời khi ghi sổ phiếu nhập {ImportReceiptId}.", id);
            TempData["ErrorMessage"] =
                "Phiếu hoặc tồn kho đã được thao tác khác cập nhật. Không có dữ liệu nào được ghi sổ; vui lòng tải lại.";
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await databaseTransaction.RollbackAsync(cancellationToken);
            logger.LogWarning(exception,
                "Giao dịch tồn kho bị trùng khi ghi sổ phiếu nhập {ImportReceiptId}.", id);
            TempData["ErrorMessage"] =
                "Phiếu đã được xử lý hoặc có giao dịch trùng. Tồn kho không được cập nhật lại.";
        }
        catch (DbUpdateException exception)
        {
            await databaseTransaction.RollbackAsync(cancellationToken);
            logger.LogError(exception, "Lỗi database khi ghi sổ phiếu nhập {ImportReceiptId}.", id);
            TempData["ErrorMessage"] =
                "Không thể ghi sổ phiếu nhập. Toàn bộ thay đổi đã được hoàn tác.";
        }
        catch (OverflowException exception)
        {
            await databaseTransaction.RollbackAsync(cancellationToken);
            logger.LogWarning(exception, "Dữ liệu số vượt giới hạn khi ghi sổ phiếu nhập {ImportReceiptId}.", id);
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (InvalidOperationException exception)
        {
            await databaseTransaction.RollbackAsync(cancellationToken);
            logger.LogWarning(exception, "Dữ liệu giá vốn không hợp lệ khi ghi sổ phiếu nhập {ImportReceiptId}.", id);
            TempData["ErrorMessage"] = exception.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private void ValidateSubmittedDetails(CreateImportReceiptViewModel model)
    {
        if (model.Details.Count == 0)
        {
            ModelState.AddModelError(nameof(model.Details),
                "Phiếu nhập phải có ít nhất một hàng hóa.");
            return;
        }

        if (model.Details.Count > MaximumDetails)
        {
            ModelState.AddModelError(nameof(model.Details),
                $"Một phiếu nhập không được vượt quá {MaximumDetails} dòng hàng hóa.");
        }

        var duplicateProductIds = model.Details
            .Where(detail => detail.ProductId > 0)
            .GroupBy(detail => detail.ProductId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet();

        for (var index = 0; index < model.Details.Count; index++)
        {
            var detail = model.Details[index];
            if (duplicateProductIds.Contains(detail.ProductId))
            {
                ModelState.AddModelError($"Details[{index}].ProductId",
                    "Hàng hóa bị trùng trong phiếu nhập.");
            }

            if (detail.Quantity != decimal.Round(detail.Quantity, 3))
            {
                ModelState.AddModelError($"Details[{index}].Quantity",
                    "Số lượng chỉ được có tối đa 3 chữ số thập phân.");
            }

            if (detail.UnitCost != decimal.Round(detail.UnitCost, 2))
            {
                ModelState.AddModelError($"Details[{index}].UnitCost",
                    "Đơn giá chỉ được có tối đa 2 chữ số thập phân.");
            }
        }

        try
        {
            _ = model.Details.Aggregate(0m,
                (total, detail) => checked(total + checked(detail.Quantity * detail.UnitCost)));
        }
        catch (OverflowException)
        {
            ModelState.AddModelError(nameof(model.Details),
                "Tổng tiền vượt quá giới hạn tính toán cho phép.");
        }
    }

    private async Task ValidateReferencesAsync(
        CreateImportReceiptViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.SupplierId > 0)
        {
            var supplierIsActive = await context.Suppliers
                .AsNoTracking()
                .AnyAsync(supplier =>
                    supplier.Id == model.SupplierId && supplier.IsActive,
                    cancellationToken);
            if (!supplierIsActive)
            {
                ModelState.AddModelError(nameof(model.SupplierId),
                    "Nhà cung cấp không tồn tại hoặc đã ngừng hoạt động.");
            }
        }

        var productIds = model.Details
            .Where(detail => detail.ProductId > 0)
            .Select(detail => detail.ProductId)
            .Distinct()
            .ToArray();

        if (productIds.Length == 0)
        {
            return;
        }

        var activeProductIds = await context.Products
            .AsNoTracking()
            .Where(product => productIds.Contains(product.Id) && product.IsActive)
            .Select(product => product.Id)
            .ToListAsync(cancellationToken);
        var activeProductIdSet = activeProductIds.ToHashSet();

        for (var index = 0; index < model.Details.Count; index++)
        {
            var productId = model.Details[index].ProductId;
            if (productId > 0 && !activeProductIdSet.Contains(productId))
            {
                ModelState.AddModelError($"Details[{index}].ProductId",
                    "Hàng hóa không tồn tại hoặc đã ngừng hoạt động.");
            }
        }
    }

    private async Task PopulateOptionsAsync(
        CreateImportReceiptViewModel model,
        CancellationToken cancellationToken)
    {
        model.Suppliers = await GetSupplierOptionsAsync(true, cancellationToken);

        model.Products = await context.Products
            .AsNoTracking()
            .Where(product => product.IsActive)
            .OrderBy(product => product.Code)
            .Select(product => new ImportReceiptProductOptionViewModel
            {
                Id = product.Id,
                Code = product.Code,
                Name = product.Name,
                Unit = product.Unit
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<ImportReceiptSupplierOptionViewModel>> GetSupplierOptionsAsync(
        bool activeOnly,
        CancellationToken cancellationToken)
    {
        var query = context.Suppliers.AsNoTracking();
        if (activeOnly)
        {
            query = query.Where(supplier => supplier.IsActive);
        }

        return await query
            .OrderBy(supplier => supplier.Code)
            .Select(supplier => new ImportReceiptSupplierOptionViewModel
            {
                Id = supplier.Id,
                Code = supplier.Code,
                Name = supplier.Name
            })
            .ToListAsync(cancellationToken);
    }

    private static byte[]? TryDecodeRowVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string GenerateReceiptNumber()
    {
        return $"PN-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..26].ToUpperInvariant();
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException { Number: 2601 or 2627 };
    }
}
