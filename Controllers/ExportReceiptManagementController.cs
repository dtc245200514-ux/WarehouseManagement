using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WarehouseManagement.Authorization;
using WarehouseManagement.Data;
using WarehouseManagement.Models;
using WarehouseManagement.Models.Enums;
using WarehouseManagement.Models.ExportReceiptManagement;

namespace WarehouseManagement.Controllers;

[Authorize(Policy = ApplicationPolicies.CreateExportReceipts)]
public class ExportReceiptManagementController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    ILogger<ExportReceiptManagementController> logger) : Controller
{
    private const int MaximumDetails = 500;
    private const int MaximumSearchLength = 100;
    private const int PageSize = 20;
    private const decimal MaximumQuantity = 999999999999999.999m;

    [HttpGet]
    public async Task<IActionResult> Index(
        string? searchTerm,
        ReceiptStatus? status,
        DateTime? fromDate,
        DateTime? toDate,
        string? createdByUserId,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        searchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim();
        createdByUserId = string.IsNullOrWhiteSpace(createdByUserId)
            ? null : createdByUserId.Trim();

        var query = context.ExportReceipts.AsNoTracking();
        if (searchTerm is not null)
        {
            if (searchTerm.Length > MaximumSearchLength)
            {
                ModelState.AddModelError(nameof(searchTerm),
                    $"Từ khóa không được vượt quá {MaximumSearchLength} ký tự.");
            }
            else
            {
                query = query.Where(receipt => receipt.ReceiptNumber.Contains(searchTerm));
            }
        }

        if (status.HasValue)
        {
            if (!Enum.IsDefined(status.Value))
            {
                ModelState.AddModelError(nameof(status), "Trạng thái phiếu không hợp lệ.");
            }
            else
            {
                query = query.Where(receipt => receipt.Status == status.Value);
            }
        }

        if (toDate.HasValue && toDate.Value.Date == DateTime.MaxValue.Date)
        {
            ModelState.AddModelError(nameof(toDate), "Ngày kết thúc vượt phạm vi hỗ trợ.");
        }
        else if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
        {
            ModelState.AddModelError(nameof(toDate),
                "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.");
        }
        else
        {
            if (fromDate.HasValue)
            {
                var startUtc = DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Local)
                    .ToUniversalTime();
                query = query.Where(receipt => receipt.CreatedAt >= startUtc);
            }

            if (toDate.HasValue)
            {
                var endUtc = DateTime.SpecifyKind(toDate.Value.Date.AddDays(1), DateTimeKind.Local)
                    .ToUniversalTime();
                query = query.Where(receipt => receipt.CreatedAt < endUtc);
            }
        }

        if (createdByUserId is not null)
        {
            query = query.Where(receipt => receipt.CreatedByUserId == createdByUserId);
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

        var receipts = await query
            .OrderByDescending(receipt => receipt.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(receipt => new ExportReceiptListItemViewModel
            {
                Id = receipt.Id,
                ReceiptNumber = receipt.ReceiptNumber,
                Status = receipt.Status,
                CreatedByName = receipt.CreatedByUser.FullName,
                CreatedAt = receipt.CreatedAt,
                PostedAt = receipt.PostedAt,
                DetailCount = receipt.Details.Count,
                TotalQuantity = receipt.Details.Sum(detail => (decimal?)detail.Quantity) ?? 0m,
                RowVersion = Convert.ToBase64String(receipt.RowVersion)
            })
            .ToListAsync(cancellationToken);

        var creators = await context.Users.AsNoTracking()
            .Where(user => user.CreatedExportReceipts.Any())
            .OrderBy(user => user.FullName)
            .Select(user => new ExportReceiptCreatorOptionViewModel
            {
                Id = user.Id,
                DisplayName = user.FullName + " (" + user.UserName + ")"
            })
            .ToListAsync(cancellationToken);

        return View(new ExportReceiptIndexViewModel
        {
            SearchTerm = searchTerm,
            Status = status,
            FromDate = fromDate,
            ToDate = toDate,
            CreatedByUserId = createdByUserId,
            Page = page,
            TotalPages = totalPages,
            TotalCount = totalCount,
            Receipts = receipts,
            Creators = creators
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var receipt = await context.ExportReceipts.AsNoTracking()
            .Include(item => item.CreatedByUser)
            .Include(item => item.PostedByUser)
            .Include(item => item.Details)
                .ThenInclude(detail => detail.Product)
            .AsSplitQuery()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (receipt is null)
        {
            return NotFound();
        }

        var receiptTransactions = await context.InventoryTransactions.AsNoTracking()
            .Where(item => item.ExportReceiptId == id)
            .OrderBy(item => item.OccurredAt)
            .ThenBy(item => item.Id)
            .Include(item => item.Product)
            .Include(item => item.PerformedByUser)
            .ToListAsync(cancellationToken);
        var transactions = receiptTransactions
            .Select(item => new ExportInventoryTransactionItemViewModel
            {
                TransactionType = item.TransactionType,
                ProductCode = item.Product.Code,
                QuantityChange = item.QuantityChange,
                BalanceBefore = item.BalanceBefore,
                BalanceAfter = item.BalanceAfter,
                PerformedByName = item.PerformedByUser.FullName,
                OccurredAt = item.OccurredAt
            })
            .ToList();

        ExportReceiptReconciliationViewModel? reconciliation = null;
        if (receipt.Status == ReceiptStatus.Posted)
        {
            try
            {
                reconciliation = await ReconcilePostedReceiptAsync(
                    receipt, receiptTransactions, cancellationToken);
            }
            catch (Exception exception) when (exception is SqlException or DbUpdateException)
            {
                logger.LogError(exception, "Unable to reconcile export receipt {ReceiptId}.", id);
                reconciliation = new ExportReceiptReconciliationViewModel
                {
                    Status = ExportReconciliationStatus.SystemError,
                    Message = "Không thể đối chiếu dữ liệu tại thời điểm này. Vui lòng thử lại hoặc liên hệ quản trị."
                };
            }
        }

        string? postAvailabilityMessage = null;
        if (receipt.Status == ReceiptStatus.Draft)
        {
            if (transactions.Count > 0)
            {
                postAvailabilityMessage = "Phiếu đã có giao dịch tồn kho; không thể ghi sổ lại.";
            }
            else if (receipt.Details.Count == 0)
            {
                postAvailabilityMessage = "Phiếu không có chi tiết hàng hóa.";
            }
            else if (receipt.Details.Count > MaximumDetails ||
                receipt.Details.GroupBy(detail => detail.ProductId).Any(group => group.Count() > 1))
            {
                postAvailabilityMessage = "Phiếu có quá nhiều chi tiết hoặc hàng hóa bị trùng.";
            }
            else if (receipt.Details.Any(detail =>
                detail.Product is null || !detail.Product.IsActive ||
                detail.Quantity <= 0m || detail.Quantity > MaximumQuantity))
            {
                postAvailabilityMessage = "Phiếu có hàng hóa ngừng hoạt động hoặc số lượng không hợp lệ.";
            }
            else if (receipt.Details.Any(detail =>
                detail.Quantity > detail.Product.CurrentQuantity))
            {
                postAvailabilityMessage = "Tồn hiện tại không đủ để ghi sổ phiếu này.";
            }
        }

        return View(new ExportReceiptDetailsViewModel
        {
            Id = receipt.Id,
            ReceiptNumber = receipt.ReceiptNumber,
            Status = receipt.Status,
            CreatedByName = receipt.CreatedByUser.FullName,
            PostedByName = receipt.PostedByUser?.FullName,
            CreatedAt = receipt.CreatedAt,
            PostedAt = receipt.PostedAt,
            Note = receipt.Note,
            RowVersion = Convert.ToBase64String(receipt.RowVersion),
            CanPost = receipt.Status == ReceiptStatus.Draft && postAvailabilityMessage is null,
            PostAvailabilityMessage = postAvailabilityMessage,
            InventoryTransactions = transactions,
            TotalQuantity = receipt.Details.Sum(detail => detail.Quantity),
            Reconciliation = reconciliation,
            Details = receipt.Details.OrderBy(detail => detail.Product.Code)
                .Select(detail => new ExportReceiptDetailItemViewModel
                {
                    ProductCode = detail.Product.Code,
                    ProductName = detail.Product.Name,
                    Unit = detail.Product.Unit,
                    Quantity = detail.Quantity,
                    UnitCost = detail.UnitCost,
                    CurrentQuantity = detail.Product.CurrentQuantity
                }).ToList()
        });
    }

    private async Task<ExportReceiptReconciliationViewModel> ReconcilePostedReceiptAsync(
        ExportReceipt receipt,
        IReadOnlyList<InventoryTransaction> receiptTransactions,
        CancellationToken cancellationToken)
    {
        if (receipt.Details.Count == 0)
        {
            return new ExportReceiptReconciliationViewModel
            {
                Status = ExportReconciliationStatus.InsufficientData,
                Message = "Phiếu đã ghi sổ nhưng không có chi tiết để đối chiếu."
            };
        }

        var productIds = receipt.Details.Select(detail => detail.ProductId).Distinct().ToArray();
        var history = await context.InventoryTransactions.AsNoTracking()
            .Where(item => productIds.Contains(item.ProductId))
            .OrderBy(item => item.Id)
            .Select(item => new
            {
                item.Id,
                item.ProductId,
                item.QuantityChange,
                item.BalanceBefore,
                item.BalanceAfter
            })
            .ToListAsync(cancellationToken);

        var items = new List<ExportReceiptReconciliationItemViewModel>();
        foreach (var detail in receipt.Details.OrderBy(item => item.Product.Code))
        {
            var matches = receiptTransactions.Where(item =>
                item.ProductId == detail.ProductId &&
                item.TransactionType == InventoryTransactionType.Export).ToList();
            var productHistory = history.Where(item => item.ProductId == detail.ProductId).ToList();
            var ledgerIncomplete = productHistory.Count == 0 ||
                productHistory[0].BalanceBefore != 0m;
            var ledgerMismatch = productHistory.Any(item =>
                    item.BalanceBefore + item.QuantityChange != item.BalanceAfter ||
                    item.BalanceAfter < 0m) ||
                productHistory.Zip(productHistory.Skip(1), (left, right) =>
                    left.BalanceAfter != right.BalanceBefore).Any(broken => broken) ||
                (productHistory.Count > 0 &&
                    productHistory[^1].BalanceAfter != detail.Product.CurrentQuantity);

            ExportReconciliationStatus status;
            string message;
            if (matches.Count == 0)
            {
                status = ExportReconciliationStatus.MissingTransaction;
                message = "Thiếu giao dịch xuất tương ứng với dòng phiếu.";
            }
            else if (matches.Count > 1)
            {
                status = ExportReconciliationStatus.DuplicateTransaction;
                message = "Có nhiều giao dịch xuất cho cùng một hàng hóa trong phiếu.";
            }
            else if (matches[0].QuantityChange != -detail.Quantity ||
                matches[0].BalanceBefore < detail.Quantity || ledgerMismatch)
            {
                status = ExportReconciliationStatus.Mismatch;
                message = "Số lượng phiếu, giao dịch hoặc chuỗi số dư tồn kho không khớp.";
            }
            else if (ledgerIncomplete)
            {
                status = ExportReconciliationStatus.InsufficientData;
                message = "Thiếu số dư đầu kỳ trong lịch sử để xác nhận toàn bộ chuỗi tồn kho.";
            }
            else
            {
                status = ExportReconciliationStatus.Match;
                message = "Số lượng xuất, giao dịch và chuỗi số dư tồn kho khớp.";
            }

            var transaction = matches.FirstOrDefault();
            items.Add(new ExportReceiptReconciliationItemViewModel
            {
                ProductCode = detail.Product.Code,
                ReceiptQuantity = detail.Quantity,
                CurrentQuantity = detail.Product.CurrentQuantity,
                TransactionQuantityChange = transaction?.QuantityChange,
                BalanceBefore = transaction?.BalanceBefore,
                BalanceAfter = transaction?.BalanceAfter,
                Status = status,
                Message = message
            });
        }

        var unexpectedTransaction = receiptTransactions.Any(item =>
            item.TransactionType != InventoryTransactionType.Export ||
            receipt.Details.All(detail => detail.ProductId != item.ProductId));
        var overall = unexpectedTransaction ? ExportReconciliationStatus.Mismatch :
            items.Any(item => item.Status == ExportReconciliationStatus.DuplicateTransaction)
                ? ExportReconciliationStatus.DuplicateTransaction :
            items.Any(item => item.Status == ExportReconciliationStatus.MissingTransaction)
                ? ExportReconciliationStatus.MissingTransaction :
            items.Any(item => item.Status == ExportReconciliationStatus.Mismatch)
                ? ExportReconciliationStatus.Mismatch :
            items.Any(item => item.Status == ExportReconciliationStatus.InsufficientData)
                ? ExportReconciliationStatus.InsufficientData : ExportReconciliationStatus.Match;

        return new ExportReceiptReconciliationViewModel
        {
            Status = overall,
            Message = unexpectedTransaction
                ? "Phiếu có giao dịch liên kết không tương ứng với chi tiết hoặc sai loại."
                : overall == ExportReconciliationStatus.Match
                    ? "Các dòng phiếu, giao dịch xuất và số dư tồn kho hiện tại khớp."
                    : "Có điểm cần kiểm tra trong dữ liệu phiếu hoặc lịch sử tồn kho.",
            Items = items
        };
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null || !user.IsActive)
        {
            return Forbid();
        }

        var model = new CreateExportReceiptViewModel
        {
            Details = [new CreateExportReceiptDetailViewModel()]
        };
        await PopulateOptionsAsync(model, user.FullName, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CreateExportReceiptViewModel model,
        CancellationToken cancellationToken)
    {
        model.Note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim();
        model.Details ??= [];
        ValidateSubmittedDetails(model);

        var user = await userManager.GetUserAsync(User);
        if (user is null || !user.IsActive)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model, user.FullName, cancellationToken);
            return View(model);
        }

        // A Draft does not reserve stock. Serializable keeps the checked stock
        // stable until this Draft and its details are committed.
        await using var transaction = await context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        await ValidateProductsAndStockAsync(model, cancellationToken);
        if (!ModelState.IsValid)
        {
            await transaction.RollbackAsync(cancellationToken);
            await PopulateOptionsAsync(model, user.FullName, cancellationToken);
            return View(model);
        }

        var receipt = new ExportReceipt
        {
            ReceiptNumber = GenerateReceiptNumber(),
            Status = ReceiptStatus.Draft,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            Note = model.Note,
            Details = model.Details.Select(detail => new ExportReceiptDetail
            {
                ProductId = detail.ProductId,
                Quantity = detail.Quantity,
                UnitCost = null
            }).ToList()
        };

        try
        {
            context.ExportReceipts.Add(receipt);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogWarning(exception, "Không thể tạo phiếu xuất Draft do dữ liệu duy nhất bị trùng.");
            ModelState.AddModelError(string.Empty,
                "Không thể lưu phiếu do dữ liệu bị trùng. Vui lòng thử lại.");
            await PopulateOptionsAsync(model, user.FullName, cancellationToken);
            return View(model);
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogError(exception, "Không thể tạo phiếu xuất Draft.");
            ModelState.AddModelError(string.Empty,
                "Không thể lưu phiếu xuất. Không có dữ liệu nào được thay đổi.");
            await PopulateOptionsAsync(model, user.FullName, cancellationToken);
            return View(model);
        }

        TempData["SuccessMessage"] =
            $"Đã lưu phiếu xuất {receipt.ReceiptNumber} ở trạng thái Nháp. Tồn kho chưa thay đổi.";
        return RedirectToAction(nameof(Details), new { receipt.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Post(
        int id,
        string? rowVersion,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null || !user.IsActive)
        {
            return Forbid();
        }

        var concurrencyToken = TryDecodeRowVersion(rowVersion);
        if (concurrencyToken is null)
        {
            TempData["ErrorMessage"] = "Dữ liệu đồng bộ không hợp lệ. Vui lòng tải lại phiếu.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // A Draft does not reserve stock. All stock checks are repeated here,
        // within the same serializable transaction as the stock and ledger writes.
        await using var transaction = await context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var receipt = await context.ExportReceipts
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
                    ? "Phiếu xuất đã được ghi sổ. Tồn kho không bị trừ lần nữa."
                    : "Chỉ phiếu xuất Nháp mới được ghi sổ.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!receipt.RowVersion.SequenceEqual(concurrencyToken))
            {
                TempData["ErrorMessage"] = "Phiếu đã thay đổi. Vui lòng tải lại trước khi ghi sổ.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (receipt.Details.Count == 0 || receipt.Details.Count > MaximumDetails)
            {
                TempData["ErrorMessage"] = "Phiếu xuất không có chi tiết hợp lệ.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (receipt.Details.GroupBy(detail => detail.ProductId)
                .Any(group => group.Count() > 1))
            {
                TempData["ErrorMessage"] = "Phiếu xuất có hàng hóa bị trùng.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (await context.InventoryTransactions.AsNoTracking()
                .AnyAsync(item => item.ExportReceiptId == id, cancellationToken))
            {
                TempData["ErrorMessage"] = "Phiếu đã có giao dịch tồn kho. Hệ thống từ chối ghi sổ lại.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var orderedDetails = receipt.Details.OrderBy(detail => detail.ProductId).ToList();
            // Validate every line before changing any tracked Product or adding ledger entries.
            foreach (var detail in orderedDetails)
            {
                var product = detail.Product;
                if (product is null || !product.IsActive)
                {
                    TempData["ErrorMessage"] = "Hàng hóa không tồn tại hoặc đã ngừng hoạt động.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                if (detail.Quantity <= 0m || detail.Quantity > MaximumQuantity)
                {
                    TempData["ErrorMessage"] = $"Số lượng xuất của {product.Code} không hợp lệ.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var balanceBefore = product.CurrentQuantity;
                if (balanceBefore < detail.Quantity)
                {
                    TempData["ErrorMessage"] =
                        $"Hàng hóa {product.Code} - {product.Name} không đủ tồn " +
                        $"({balanceBefore:0.###}) cho số lượng xuất {detail.Quantity:0.###}.";
                    return RedirectToAction(nameof(Details), new { id });
                }
            }

            context.Entry(receipt).Property(item => item.RowVersion).OriginalValue = concurrencyToken;
            var occurredAt = DateTime.UtcNow;
            foreach (var detail in orderedDetails)
            {
                var product = detail.Product;
                var balanceBefore = product.CurrentQuantity;
                var balanceAfter = balanceBefore - detail.Quantity;
                product.CurrentQuantity = balanceAfter;
                product.UpdatedAt = occurredAt;

                context.InventoryTransactions.Add(new InventoryTransaction
                {
                    ProductId = product.Id,
                    TransactionType = InventoryTransactionType.Export,
                    QuantityChange = -detail.Quantity,
                    BalanceBefore = balanceBefore,
                    BalanceAfter = balanceAfter,
                    ExportReceiptId = receipt.Id,
                    PerformedByUserId = user.Id,
                    OccurredAt = occurredAt,
                    Note = $"Ghi sổ phiếu xuất {receipt.ReceiptNumber}."
                });
            }

            receipt.Status = ReceiptStatus.Posted;
            receipt.PostedByUserId = user.Id;
            receipt.PostedAt = occurredAt;

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            TempData["SuccessMessage"] =
                $"Đã ghi sổ phiếu xuất {receipt.ReceiptNumber}. Tồn kho đã được cập nhật.";
        }
        catch (DbUpdateConcurrencyException exception)
        {
            await RollbackSafelyAsync(transaction, cancellationToken);
            logger.LogWarning(exception, "Xung đột đồng thời khi ghi sổ phiếu xuất {ExportReceiptId}.", id);
            TempData["ErrorMessage"] =
                "Phiếu hoặc tồn kho đã thay đổi. Không có dữ liệu nào được ghi sổ; vui lòng tải lại.";
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await RollbackSafelyAsync(transaction, cancellationToken);
            logger.LogWarning(exception, "Giao dịch xuất kho bị trùng cho phiếu {ExportReceiptId}.", id);
            TempData["ErrorMessage"] = "Phiếu đã có giao dịch xuất kho. Tồn kho không bị trừ lần nữa.";
        }
        catch (Exception exception) when (IsDeadlock(exception))
        {
            // SQL Server rolls back the victim transaction itself (error 1205).
            await RollbackSafelyAsync(transaction, cancellationToken);
            logger.LogWarning(exception, "Deadlock khi ghi sổ phiếu xuất {ExportReceiptId}.", id);
            TempData["ErrorMessage"] = "Dữ liệu đang được xử lý đồng thời. Vui lòng tải lại và thử lại.";
        }
        catch (DbUpdateException exception)
        {
            await RollbackSafelyAsync(transaction, cancellationToken);
            logger.LogError(exception, "Lỗi database khi ghi sổ phiếu xuất {ExportReceiptId}.", id);
            TempData["ErrorMessage"] = "Không thể ghi sổ phiếu xuất. Toàn bộ thay đổi đã được hoàn tác.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private void ValidateSubmittedDetails(CreateExportReceiptViewModel model)
    {
        if (model.Details.Count == 0)
        {
            ModelState.AddModelError(nameof(model.Details),
                "Phiếu xuất phải có ít nhất một hàng hóa.");
            return;
        }

        if (model.Details.Count > MaximumDetails)
        {
            ModelState.AddModelError(nameof(model.Details),
                $"Một phiếu xuất không được vượt quá {MaximumDetails} dòng hàng hóa.");
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
                    "Hàng hóa bị trùng trong phiếu xuất.");
            }

            if (detail.Quantity != decimal.Round(detail.Quantity, 3))
            {
                ModelState.AddModelError($"Details[{index}].Quantity",
                    "Số lượng chỉ được có tối đa 3 chữ số thập phân.");
            }
        }
    }

    private async Task ValidateProductsAndStockAsync(
        CreateExportReceiptViewModel model,
        CancellationToken cancellationToken)
    {
        var productIds = model.Details
            .Where(detail => detail.ProductId > 0)
            .Select(detail => detail.ProductId)
            .Distinct()
            .ToArray();

        var products = await context.Products.AsNoTracking()
            .Where(product => productIds.Contains(product.Id))
            .Select(product => new { product.Id, product.IsActive, product.CurrentQuantity })
            .ToDictionaryAsync(product => product.Id, cancellationToken);

        for (var index = 0; index < model.Details.Count; index++)
        {
            var detail = model.Details[index];
            if (detail.ProductId <= 0)
            {
                continue;
            }

            if (!products.TryGetValue(detail.ProductId, out var product) || !product.IsActive)
            {
                ModelState.AddModelError($"Details[{index}].ProductId",
                    "Hàng hóa không tồn tại hoặc đã ngừng hoạt động.");
            }
            else if (detail.Quantity > product.CurrentQuantity)
            {
                ModelState.AddModelError($"Details[{index}].Quantity",
                    $"Số lượng xuất vượt tồn hiện tại ({product.CurrentQuantity:0.###}).");
            }
        }
    }

    private async Task PopulateOptionsAsync(
        CreateExportReceiptViewModel model,
        string fullName,
        CancellationToken cancellationToken)
    {
        model.CreatedByName = fullName;
        model.Products = await context.Products.AsNoTracking()
            .Where(product => product.IsActive && product.CurrentQuantity > 0)
            .OrderBy(product => product.Code)
            .Select(product => new ExportReceiptProductOptionViewModel
            {
                Id = product.Id,
                Code = product.Code,
                Name = product.Name,
                Unit = product.Unit,
                CurrentQuantity = product.CurrentQuantity
            })
            .ToListAsync(cancellationToken);
    }

    private static string GenerateReceiptNumber()
    {
        return $"PX-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..26].ToUpperInvariant();
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException { Number: 2601 or 2627 };
    }

    private static bool IsDeadlock(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 1205 })
            {
                return true;
            }
        }

        return false;
    }

    private async Task RollbackSafelyAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException)
        {
            // A deadlock victim has already been rolled back by SQL Server.
            // Disposing the transaction still prevents any later commit.
            logger.LogWarning(exception, "Database transaction was already terminated during rollback.");
        }
    }

    private static byte[]? TryDecodeRowVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            var bytes = Convert.FromBase64String(value);
            return bytes.Length == 8 ? bytes : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
