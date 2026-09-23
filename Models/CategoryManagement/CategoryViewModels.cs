using System.ComponentModel.DataAnnotations;

namespace WarehouseManagement.Models.CategoryManagement;

public sealed class CategoryIndexViewModel
{
    public string? SearchTerm { get; init; }

    public bool? IsActive { get; init; }

    public IReadOnlyList<CategoryListItemViewModel> Categories { get; init; } = [];
}

public sealed class CategoryListItemViewModel
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsActive { get; init; }

    public DateTime CreatedAt { get; init; }

    public int ProductCount { get; init; }
}

public sealed class CategoryDetailsViewModel
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsActive { get; init; }

    public DateTime CreatedAt { get; init; }

    public int ProductCount { get; init; }

    public string RowVersion { get; init; } = string.Empty;
}

public class CategoryInputViewModel
{
    [Required(ErrorMessage = "Tên danh mục là bắt buộc.")]
    [StringLength(100, ErrorMessage = "Tên danh mục không được vượt quá 100 ký tự.")]
    [Display(Name = "Tên danh mục")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự.")]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }
}

public sealed class EditCategoryViewModel : CategoryInputViewModel
{
    public int Id { get; set; }

    [Required]
    public string RowVersion { get; set; } = string.Empty;
}
