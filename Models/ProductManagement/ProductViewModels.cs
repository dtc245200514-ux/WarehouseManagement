using System.ComponentModel.DataAnnotations;

namespace WarehouseManagement.Models.ProductManagement;

public sealed class ProductIndexViewModel
{
    public string? SearchTerm { get; init; }
    public int? CategoryId { get; init; }
    public string? Unit { get; init; }
    public bool? IsActive { get; init; }
    public IReadOnlyList<ProductListItemViewModel> Products { get; init; } = [];
    public IReadOnlyList<ProductCategoryOptionViewModel> Categories { get; init; } = [];
    public IReadOnlyList<string> Units { get; init; } = [];
}

public sealed class ProductListItemViewModel
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public decimal CurrentQuantity { get; init; }
    public decimal MinimumStockLevel { get; init; }
    public bool IsActive { get; init; }
}

public sealed class ProductDetailsViewModel
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public bool CategoryIsActive { get; init; }
    public string Unit { get; init; } = string.Empty;
    public decimal CurrentQuantity { get; init; }
    public decimal MinimumStockLevel { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class ProductCategoryOptionViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

public class ProductInputViewModel
{
    [Required(ErrorMessage = "Tên hàng hóa là bắt buộc.")]
    [StringLength(200, ErrorMessage = "Tên hàng hóa không được vượt quá 200 ký tự.")]
    [Display(Name = "Tên hàng hóa")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự.")]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn danh mục.")]
    [Display(Name = "Danh mục")]
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "Đơn vị tính là bắt buộc.")]
    [StringLength(50, ErrorMessage = "Đơn vị tính không được vượt quá 50 ký tự.")]
    [Display(Name = "Đơn vị tính")]
    public string Unit { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "999999999999999.999",
        ErrorMessage = "Mức tồn tối thiểu phải từ 0 trở lên.")]
    [Display(Name = "Mức tồn tối thiểu")]
    public decimal MinimumStockLevel { get; set; }

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;

    public IReadOnlyList<ProductCategoryOptionViewModel> Categories { get; set; } = [];
}

public sealed class CreateProductViewModel : ProductInputViewModel
{
    [Required(ErrorMessage = "Mã hàng là bắt buộc.")]
    [StringLength(50, ErrorMessage = "Mã hàng không được vượt quá 50 ký tự.")]
    [Display(Name = "Mã hàng")]
    public string Code { get; set; } = string.Empty;
}

public sealed class EditProductViewModel : ProductInputViewModel
{
    public int ProductId { get; set; }

    public string Code { get; set; } = string.Empty;

    [Required]
    public string RowVersion { get; set; } = string.Empty;
}
