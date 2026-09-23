using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WarehouseManagement.Authorization;
using WarehouseManagement.Data;
using WarehouseManagement.Models;
using WarehouseManagement.Models.ProductManagement;

namespace WarehouseManagement.Controllers;

[Authorize(Policy = ApplicationPolicies.ManageCatalog)]
public class ProductManagementController(ApplicationDbContext context) : Controller
{
    private const int MaximumSearchLength = 200;

    [HttpGet]
    public async Task<IActionResult> Index(
        string? searchTerm,
        int? categoryId,
        string? unit,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        searchTerm = searchTerm?.Trim();
        unit = unit?.Trim();
        var query = context.Products.AsNoTracking();

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
                query = query.Where(product =>
                    product.Code.Contains(searchTerm) ||
                    product.Name.Contains(searchTerm));
            }
        }

        if (categoryId.HasValue)
        {
            var categoryExists = await context.Categories
                .AsNoTracking()
                .AnyAsync(category => category.Id == categoryId.Value, cancellationToken);
            if (!categoryExists)
            {
                ModelState.AddModelError(nameof(categoryId), "Danh mục được chọn không hợp lệ.");
                query = query.Where(_ => false);
            }
            else
            {
                query = query.Where(product => product.CategoryId == categoryId.Value);
            }
        }

        if (!string.IsNullOrWhiteSpace(unit))
        {
            if (unit.Length > 50)
            {
                ModelState.AddModelError(nameof(unit), "Đơn vị tính không được vượt quá 50 ký tự.");
                query = query.Where(_ => false);
            }
            else
            {
                query = query.Where(product => product.Unit == unit);
            }
        }

        if (isActive.HasValue)
        {
            query = query.Where(product => product.IsActive == isActive.Value);
        }

        var products = await query
            .OrderByDescending(product => product.IsActive)
            .ThenBy(product => product.Code)
            .Select(product => new ProductListItemViewModel
            {
                Id = product.Id,
                Code = product.Code,
                Name = product.Name,
                CategoryName = product.Category.Name,
                Unit = product.Unit,
                CurrentQuantity = product.CurrentQuantity,
                MinimumStockLevel = product.MinimumStockLevel,
                IsActive = product.IsActive
            })
            .ToListAsync(cancellationToken);

        return View(new ProductIndexViewModel
        {
            SearchTerm = searchTerm,
            CategoryId = categoryId,
            Unit = unit,
            IsActive = isActive,
            Products = products,
            Categories = await GetCategoryOptionsAsync(cancellationToken),
            Units = await GetUnitsAsync(cancellationToken)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var product = await context.Products
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new ProductDetailsViewModel
            {
                Id = item.Id,
                Code = item.Code,
                Name = item.Name,
                Description = item.Description,
                CategoryName = item.Category.Name,
                CategoryIsActive = item.Category.IsActive,
                Unit = item.Unit,
                CurrentQuantity = item.CurrentQuantity,
                MinimumStockLevel = item.MinimumStockLevel,
                IsActive = item.IsActive,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt,
                RowVersion = Convert.ToBase64String(item.RowVersion)
            })
            .SingleOrDefaultAsync(cancellationToken);

        return product is null ? NotFound() : View(product);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        return View(new CreateProductViewModel
        {
            IsActive = true,
            Categories = await GetActiveCategoryOptionsAsync(cancellationToken)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CreateProductViewModel model,
        CancellationToken cancellationToken)
    {
        Normalize(model);
        model.Code = model.Code?.Trim().ToUpperInvariant() ?? string.Empty;

        await ValidateCategoryAsync(model.CategoryId, null, cancellationToken);
        await ValidateUniqueCodeAsync(model.Code, cancellationToken);

        if (!ModelState.IsValid)
        {
            model.Categories = await GetActiveCategoryOptionsAsync(cancellationToken);
            return View(model);
        }

        var product = new Product
        {
            Code = model.Code,
            Name = model.Name,
            Description = model.Description,
            CategoryId = model.CategoryId,
            Unit = model.Unit,
            MinimumStockLevel = model.MinimumStockLevel,
            CurrentQuantity = 0m,
            AverageUnitCost = null,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        context.Products.Add(product);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            ModelState.AddModelError(nameof(model.Code), "Mã hàng đã tồn tại.");
            model.Categories = await GetActiveCategoryOptionsAsync(cancellationToken);
            return View(model);
        }

        TempData["SuccessMessage"] = $"Đã thêm hàng hóa {product.Code} - {product.Name}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var product = await context.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        return View(new EditProductViewModel
        {
            ProductId = product.Id,
            Code = product.Code,
            Name = product.Name,
            Description = product.Description,
            CategoryId = product.CategoryId,
            Unit = product.Unit,
            MinimumStockLevel = product.MinimumStockLevel,
            IsActive = product.IsActive,
            RowVersion = Convert.ToBase64String(product.RowVersion),
            Categories = await GetEditableCategoryOptionsAsync(product.CategoryId, cancellationToken)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        EditProductViewModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.ProductId)
        {
            return BadRequest();
        }

        Normalize(model);
        var product = await context.Products
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        model.Code = product.Code;
        await ValidateCategoryAsync(model.CategoryId, product.CategoryId, cancellationToken);
        var rowVersion = TryDecodeRowVersion(model.RowVersion);
        if (rowVersion is null)
        {
            ModelState.AddModelError(string.Empty, "Dữ liệu đồng bộ không hợp lệ. Vui lòng tải lại trang.");
        }

        if (!ModelState.IsValid)
        {
            model.Categories = await GetEditableCategoryOptionsAsync(product.CategoryId, cancellationToken);
            return View(model);
        }

        product.Name = model.Name;
        product.Description = model.Description;
        product.CategoryId = model.CategoryId;
        product.Unit = model.Unit;
        product.MinimumStockLevel = model.MinimumStockLevel;
        product.IsActive = model.IsActive;
        product.UpdatedAt = DateTime.UtcNow;
        context.Entry(product).Property(item => item.RowVersion).OriginalValue = rowVersion!;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            var databaseValues = await exception.Entries.Single().GetDatabaseValuesAsync(cancellationToken);
            if (databaseValues is null)
            {
                return NotFound();
            }

            model.RowVersion = Convert.ToBase64String(
                databaseValues.GetValue<byte[]>(nameof(Product.RowVersion))!);
            ModelState.Remove(nameof(model.RowVersion));
            ModelState.AddModelError(string.Empty,
                "Hàng hóa đã được cập nhật bởi thao tác khác. Dữ liệu của bạn chưa được lưu; vui lòng kiểm tra và thử lại.");
            model.Categories = await GetEditableCategoryOptionsAsync(
                databaseValues.GetValue<int>(nameof(Product.CategoryId)), cancellationToken);
            return View(model);
        }

        TempData["SuccessMessage"] = $"Đã cập nhật hàng hóa {product.Code} - {product.Name}.";
        return RedirectToAction(nameof(Details), new { product.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(
        int id,
        string rowVersion,
        CancellationToken cancellationToken)
    {
        var product = await context.Products
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        var concurrencyToken = TryDecodeRowVersion(rowVersion);
        if (concurrencyToken is null)
        {
            TempData["ErrorMessage"] = "Dữ liệu đồng bộ không hợp lệ. Vui lòng thử lại.";
            return RedirectToAction(nameof(Details), new { product.Id });
        }

        product.IsActive = !product.IsActive;
        product.UpdatedAt = DateTime.UtcNow;
        context.Entry(product).Property(item => item.RowVersion).OriginalValue = concurrencyToken;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["ErrorMessage"] =
                "Trạng thái hàng hóa đã được thay đổi bởi thao tác khác. Vui lòng kiểm tra lại.";
            return RedirectToAction(nameof(Details), new { product.Id });
        }

        TempData["SuccessMessage"] = product.IsActive
            ? $"Đã kích hoạt hàng hóa {product.Code}."
            : $"Đã ngừng hoạt động hàng hóa {product.Code}.";
        return RedirectToAction(nameof(Details), new { product.Id });
    }

    private async Task ValidateUniqueCodeAsync(string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 50)
        {
            return;
        }

        if (await context.Products.AsNoTracking()
            .AnyAsync(product => product.Code == code, cancellationToken))
        {
            ModelState.AddModelError(nameof(CreateProductViewModel.Code), "Mã hàng đã tồn tại.");
        }
    }

    private async Task ValidateCategoryAsync(
        int categoryId,
        int? currentCategoryId,
        CancellationToken cancellationToken)
    {
        if (categoryId <= 0)
        {
            return;
        }

        var category = await context.Categories.AsNoTracking()
            .Where(item => item.Id == categoryId)
            .Select(item => new { item.IsActive })
            .SingleOrDefaultAsync(cancellationToken);
        if (category is null)
        {
            ModelState.AddModelError(nameof(ProductInputViewModel.CategoryId),
                "Danh mục được chọn không tồn tại.");
        }
        else if (!category.IsActive && categoryId != currentCategoryId)
        {
            ModelState.AddModelError(nameof(ProductInputViewModel.CategoryId),
                "Không thể gán hàng hóa vào danh mục đã ngừng hoạt động.");
        }
    }

    private Task<List<ProductCategoryOptionViewModel>> GetCategoryOptionsAsync(
        CancellationToken cancellationToken)
    {
        return context.Categories.AsNoTracking()
            .OrderByDescending(category => category.IsActive)
            .ThenBy(category => category.Name)
            .Select(category => new ProductCategoryOptionViewModel
            {
                Id = category.Id,
                Name = category.Name,
                IsActive = category.IsActive
            })
            .ToListAsync(cancellationToken);
    }

    private Task<List<ProductCategoryOptionViewModel>> GetActiveCategoryOptionsAsync(
        CancellationToken cancellationToken)
    {
        return context.Categories.AsNoTracking()
            .Where(category => category.IsActive)
            .OrderBy(category => category.Name)
            .Select(category => new ProductCategoryOptionViewModel
            {
                Id = category.Id,
                Name = category.Name,
                IsActive = category.IsActive
            })
            .ToListAsync(cancellationToken);
    }

    private Task<List<ProductCategoryOptionViewModel>> GetEditableCategoryOptionsAsync(
        int currentCategoryId,
        CancellationToken cancellationToken)
    {
        return context.Categories.AsNoTracking()
            .Where(category => category.IsActive || category.Id == currentCategoryId)
            .OrderByDescending(category => category.IsActive)
            .ThenBy(category => category.Name)
            .Select(category => new ProductCategoryOptionViewModel
            {
                Id = category.Id,
                Name = category.Name,
                IsActive = category.IsActive
            })
            .ToListAsync(cancellationToken);
    }

    private Task<List<string>> GetUnitsAsync(CancellationToken cancellationToken)
    {
        return context.Products.AsNoTracking()
            .Select(product => product.Unit)
            .Distinct()
            .OrderBy(unit => unit)
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

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException { Number: 2601 or 2627 };
    }

    private static void Normalize(ProductInputViewModel model)
    {
        model.Name = model.Name?.Trim() ?? string.Empty;
        model.Unit = model.Unit?.Trim() ?? string.Empty;
        model.Description = string.IsNullOrWhiteSpace(model.Description)
            ? null
            : model.Description.Trim();
    }
}
