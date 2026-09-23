using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WarehouseManagement.Authorization;
using WarehouseManagement.Data;
using WarehouseManagement.Models;
using WarehouseManagement.Models.CategoryManagement;

namespace WarehouseManagement.Controllers;

[Authorize(Policy = ApplicationPolicies.ViewCategories)]
public class CategoryManagementController(ApplicationDbContext context) : Controller
{
    private const int MaximumSearchLength = 100;

    [HttpGet]
    public async Task<IActionResult> Index(
        string? searchTerm,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        searchTerm = searchTerm?.Trim();
        var query = context.Categories.AsNoTracking();

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
                var hasCategoryId = int.TryParse(searchTerm, out var categoryId);
                query = query.Where(category =>
                    category.Name.Contains(searchTerm) ||
                    hasCategoryId && category.Id == categoryId);
            }
        }

        if (isActive.HasValue)
        {
            query = query.Where(category => category.IsActive == isActive.Value);
        }

        var categories = await query
            .OrderByDescending(category => category.IsActive)
            .ThenBy(category => category.Name)
            .Select(category => new CategoryListItemViewModel
            {
                Id = category.Id,
                Name = category.Name,
                Description = category.Description,
                IsActive = category.IsActive,
                CreatedAt = category.CreatedAt,
                ProductCount = category.Products.Count
            })
            .ToListAsync(cancellationToken);

        return View(new CategoryIndexViewModel
        {
            SearchTerm = searchTerm,
            IsActive = isActive,
            Categories = categories
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var category = await context.Categories
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new CategoryDetailsViewModel
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                IsActive = item.IsActive,
                CreatedAt = item.CreatedAt,
                ProductCount = item.Products.Count,
                RowVersion = Convert.ToBase64String(item.RowVersion)
            })
            .SingleOrDefaultAsync(cancellationToken);

        return category is null ? NotFound() : View(category);
    }

    [Authorize(Policy = ApplicationPolicies.ManageCategories)]
    [HttpGet]
    public IActionResult Create()
    {
        return View(new CategoryInputViewModel());
    }

    [Authorize(Policy = ApplicationPolicies.ManageCategories)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CategoryInputViewModel model,
        CancellationToken cancellationToken)
    {
        Normalize(model);
        await ValidateUniqueNameAsync(model.Name, null, cancellationToken);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var category = new Category
        {
            Name = model.Name,
            Description = model.Description,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Categories.Add(category);
        if (!await TrySaveChangesAsync(nameof(model.Name), cancellationToken))
        {
            return View(model);
        }

        TempData["SuccessMessage"] = $"Đã thêm danh mục {category.Name}.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = ApplicationPolicies.ManageCategories)]
    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var category = await context.Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (category is null)
        {
            return NotFound();
        }

        return View(new EditCategoryViewModel
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            RowVersion = Convert.ToBase64String(category.RowVersion)
        });
    }

    [Authorize(Policy = ApplicationPolicies.ManageCategories)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        EditCategoryViewModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var category = await context.Categories
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (category is null)
        {
            return NotFound();
        }

        Normalize(model);
        await ValidateUniqueNameAsync(model.Name, id, cancellationToken);
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

        category.Name = model.Name;
        category.Description = model.Description;
        context.Entry(category).Property(item => item.RowVersion).OriginalValue = rowVersion!;

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
                databaseValues.GetValue<byte[]>(nameof(Category.RowVersion))!);
            ModelState.Remove(nameof(model.RowVersion));
            ModelState.AddModelError(string.Empty,
                "Danh mục đã được cập nhật bởi thao tác khác. Dữ liệu của bạn chưa được lưu; vui lòng kiểm tra và thử lại.");
            return View(model);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            ModelState.AddModelError(nameof(model.Name), "Tên danh mục đã tồn tại.");
            return View(model);
        }

        TempData["SuccessMessage"] = $"Đã cập nhật danh mục {category.Name}.";
        return RedirectToAction(nameof(Details), new { category.Id });
    }

    [Authorize(Policy = ApplicationPolicies.ManageCategories)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(
        int id,
        string rowVersion,
        CancellationToken cancellationToken)
    {
        var category = await context.Categories
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (category is null)
        {
            return NotFound();
        }

        var concurrencyToken = TryDecodeRowVersion(rowVersion);
        if (concurrencyToken is null)
        {
            TempData["ErrorMessage"] =
                "Dữ liệu đồng bộ không hợp lệ. Vui lòng tải lại trang.";
            return RedirectToAction(nameof(Details), new { category.Id });
        }

        category.IsActive = !category.IsActive;
        context.Entry(category).Property(item => item.RowVersion).OriginalValue = concurrencyToken;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["ErrorMessage"] =
                "Trạng thái danh mục đã được thay đổi bởi thao tác khác. Vui lòng kiểm tra lại.";
            return RedirectToAction(nameof(Details), new { category.Id });
        }

        TempData["SuccessMessage"] = category.IsActive
            ? $"Đã kích hoạt danh mục {category.Name}."
            : $"Đã ngừng hoạt động danh mục {category.Name}.";

        return RedirectToAction(nameof(Details), new { category.Id });
    }

    private async Task ValidateUniqueNameAsync(
        string name,
        int? excludedCategoryId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
        {
            return;
        }

        var duplicateExists = await context.Categories
            .AsNoTracking()
            .AnyAsync(category =>
                category.Name == name &&
                (!excludedCategoryId.HasValue || category.Id != excludedCategoryId.Value),
                cancellationToken);

        if (duplicateExists)
        {
            ModelState.AddModelError(nameof(CategoryInputViewModel.Name),
                "Tên danh mục đã tồn tại.");
        }
    }

    private async Task<bool> TrySaveChangesAsync(
        string propertyName,
        CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            ModelState.AddModelError(propertyName, "Tên danh mục đã tồn tại.");
            return false;
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException { Number: 2601 or 2627 };
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

    private static void Normalize(CategoryInputViewModel model)
    {
        model.Name = model.Name?.Trim() ?? string.Empty;
        model.Description = string.IsNullOrWhiteSpace(model.Description)
            ? null
            : model.Description.Trim();
    }
}
