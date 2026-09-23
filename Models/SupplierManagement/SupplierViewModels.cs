using System.ComponentModel.DataAnnotations;

namespace WarehouseManagement.Models.SupplierManagement;

public sealed class SupplierIndexViewModel
{
    public string? SearchTerm { get; init; }
    public bool? IsActive { get; init; }
    public IReadOnlyList<SupplierListItemViewModel> Suppliers { get; init; } = [];
}

public sealed class SupplierListItemViewModel
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class SupplierDetailsViewModel
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public int ImportReceiptCount { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public class SupplierInputViewModel
{
    [Required(ErrorMessage = "Tên nhà cung cấp là bắt buộc.")]
    [StringLength(200, ErrorMessage = "Tên nhà cung cấp không được vượt quá 200 ký tự.")]
    [Display(Name = "Tên nhà cung cấp")]
    public string Name { get; set; } = string.Empty;

    [StringLength(20, MinimumLength = 7,
        ErrorMessage = "Số điện thoại phải có từ 7 đến 20 ký tự.")]
    [RegularExpression(@"^[0-9+().\s-]+$",
        ErrorMessage = "Số điện thoại chỉ được chứa chữ số, khoảng trắng và các ký tự + - ( ) .")]
    [Display(Name = "Số điện thoại")]
    public string? Phone { get; set; }

    [StringLength(150, ErrorMessage = "Email không được vượt quá 150 ký tự.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [StringLength(300, ErrorMessage = "Địa chỉ không được vượt quá 300 ký tự.")]
    [Display(Name = "Địa chỉ")]
    public string? Address { get; set; }

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;
}

public sealed class CreateSupplierViewModel : SupplierInputViewModel
{
    [Required(ErrorMessage = "Mã nhà cung cấp là bắt buộc.")]
    [StringLength(50, ErrorMessage = "Mã nhà cung cấp không được vượt quá 50 ký tự.")]
    [Display(Name = "Mã nhà cung cấp")]
    public string Code { get; set; } = string.Empty;
}

public sealed class EditSupplierViewModel : SupplierInputViewModel
{
    public int SupplierId { get; set; }
    public string Code { get; set; } = string.Empty;

    [Required]
    public string RowVersion { get; set; } = string.Empty;
}
