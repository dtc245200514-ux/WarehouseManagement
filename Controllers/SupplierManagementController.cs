using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WarehouseManagement.Authorization;
using WarehouseManagement.Data;
using WarehouseManagement.Models;
using WarehouseManagement.Models.SupplierManagement;

namespace WarehouseManagement.Controllers;

[Authorize(Policy = ApplicationPolicies.ManageSuppliers)]
public class SupplierManagementController(ApplicationDbContext context) : Controller
{
    private const int MaximumSearchLength = 200;

    [HttpGet]
    public async Task<IActionResult> Index(
        string? searchTerm,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        searchTerm = searchTerm?.Trim();
        var query = context.Suppliers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            if (searchTerm.Length > MaximumSearchLength)
            {
                ModelState.AddModelError(nameof(searchTerm),
                    $"Từ khóa không được vượt quá {MaximumSearchLength} ký tự.");
                query = query.Where(_ => false);
            }
            else
            {
                query = query.Where(supplier =>
                    supplier.Code.Contains(searchTerm) ||
                    supplier.Name.Contains(searchTerm) ||
                    supplier.Phone != null && supplier.Phone.Contains(searchTerm) ||
                    supplier.Email != null && supplier.Email.Contains(searchTerm));
            }
        }

        if (isActive.HasValue)
        {
            query = query.Where(supplier => supplier.IsActive == isActive.Value);
        }

        var suppliers = await query
            .OrderByDescending(supplier => supplier.IsActive)
            .ThenBy(supplier => supplier.Name)
            .Select(supplier => new SupplierListItemViewModel
            {
                Id = supplier.Id,
                Code = supplier.Code,
                Name = supplier.Name,
                Phone = supplier.Phone,
                Email = supplier.Email,
                IsActive = supplier.IsActive,
                CreatedAt = supplier.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return View(new SupplierIndexViewModel
        {
            SearchTerm = searchTerm,
            IsActive = isActive,
            Suppliers = suppliers
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var supplier = await context.Suppliers
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new SupplierDetailsViewModel
            {
                Id = item.Id,
                Code = item.Code,
                Name = item.Name,
                Phone = item.Phone,
                Email = item.Email,
                Address = item.Address,
                IsActive = item.IsActive,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt,
                ImportReceiptCount = item.ImportReceipts.Count,
                RowVersion = Convert.ToBase64String(item.RowVersion)
            })
            .SingleOrDefaultAsync(cancellationToken);

        return supplier is null ? NotFound() : View(supplier);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateSupplierViewModel { IsActive = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CreateSupplierViewModel model,
        CancellationToken cancellationToken)
    {
        Normalize(model);
        model.Code = model.Code?.Trim().ToUpperInvariant() ?? string.Empty;
        await ValidateUniqueCodeAsync(model.Code, cancellationToken);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var supplier = new Supplier
        {
            Code = model.Code,
            Name = model.Name,
            Phone = model.Phone,
            Email = model.Email,
            Address = model.Address,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        context.Suppliers.Add(supplier);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            ModelState.AddModelError(nameof(model.Code), "Mã nhà cung cấp đã tồn tại.");
            return View(model);
        }

        TempData["SuccessMessage"] =
            $"Đã thêm nhà cung cấp {supplier.Code} - {supplier.Name}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var supplier = await context.Suppliers
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (supplier is null)
        {
            return NotFound();
        }

        return View(new EditSupplierViewModel
        {
            SupplierId = supplier.Id,
            Code = supplier.Code,
            Name = supplier.Name,
            Phone = supplier.Phone,
            Email = supplier.Email,
            Address = supplier.Address,
            IsActive = supplier.IsActive,
            RowVersion = Convert.ToBase64String(supplier.RowVersion)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        EditSupplierViewModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.SupplierId)
        {
            return BadRequest();
        }

        var supplier = await context.Suppliers
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (supplier is null)
        {
            return NotFound();
        }

        Normalize(model);
        model.Code = supplier.Code;
        var rowVersion = TryDecodeRowVersion(model.RowVersion);
        if (rowVersion is null)
        {
            ModelState.AddModelError(string.Empty,
                "Dữ liệu đồng bộ không hợp lệ. Vui lòng tải lại trang.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        supplier.Name = model.Name;
        supplier.Phone = model.Phone;
        supplier.Email = model.Email;
        supplier.Address = model.Address;
        supplier.IsActive = model.IsActive;
        supplier.UpdatedAt = DateTime.UtcNow;
        context.Entry(supplier).Property(item => item.RowVersion).OriginalValue = rowVersion!;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            var databaseValues = await exception.Entries.Single()
                .GetDatabaseValuesAsync(cancellationToken);
            if (databaseValues is null)
            {
                return NotFound();
            }

            model.RowVersion = Convert.ToBase64String(
                databaseValues.GetValue<byte[]>(nameof(Supplier.RowVersion))!);
            ModelState.Remove(nameof(model.RowVersion));
            ModelState.AddModelError(string.Empty,
                "Nhà cung cấp đã được cập nhật bởi thao tác khác. Dữ liệu của bạn chưa được lưu; vui lòng kiểm tra và thử lại.");
            return View(model);
        }

        TempData["SuccessMessage"] =
            $"Đã cập nhật nhà cung cấp {supplier.Code} - {supplier.Name}.";
        return RedirectToAction(nameof(Details), new { supplier.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(
        int id,
        string rowVersion,
        CancellationToken cancellationToken)
    {
        var supplier = await context.Suppliers
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (supplier is null)
        {
            return NotFound();
        }

        var concurrencyToken = TryDecodeRowVersion(rowVersion);
        if (concurrencyToken is null)
        {
            TempData["ErrorMessage"] =
                "Dữ liệu đồng bộ không hợp lệ. Vui lòng tải lại trang.";
            return RedirectToAction(nameof(Details), new { supplier.Id });
        }

        supplier.IsActive = !supplier.IsActive;
        supplier.UpdatedAt = DateTime.UtcNow;
        context.Entry(supplier).Property(item => item.RowVersion).OriginalValue = concurrencyToken;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["ErrorMessage"] =
                "Trạng thái nhà cung cấp đã được thay đổi bởi thao tác khác. Vui lòng kiểm tra lại.";
            return RedirectToAction(nameof(Details), new { supplier.Id });
        }

        TempData["SuccessMessage"] = supplier.IsActive
            ? $"Đã kích hoạt nhà cung cấp {supplier.Code}."
            : $"Đã ngừng hoạt động nhà cung cấp {supplier.Code}.";
        return RedirectToAction(nameof(Details), new { supplier.Id });
    }

    private async Task ValidateUniqueCodeAsync(
        string code,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 50)
        {
            return;
        }

        if (await context.Suppliers.AsNoTracking()
            .AnyAsync(supplier => supplier.Code == code, cancellationToken))
        {
            ModelState.AddModelError(nameof(CreateSupplierViewModel.Code),
                "Mã nhà cung cấp đã tồn tại.");
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
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException { Number: 2601 or 2627 };
    }

    private static void Normalize(SupplierInputViewModel model)
    {
        model.Name = model.Name?.Trim() ?? string.Empty;
        model.Phone = NormalizeOptional(model.Phone);
        model.Email = NormalizeOptional(model.Email);
        model.Address = NormalizeOptional(model.Address);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
