using System.ComponentModel.DataAnnotations;

namespace WarehouseManagement.Models.UserManagement;

public class UserAccountIndexViewModel
{
    public string? SearchTerm { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
    public IReadOnlyList<string> AvailableRoles { get; set; } = [];
    public IReadOnlyList<UserAccountListItemViewModel> Users { get; set; } = [];
}

public class UserAccountListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; set; } = [];
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    public bool IsCurrentUser { get; set; }
}

public class UserAccountDetailsViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; set; } = [];
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public bool IsCurrentUser { get; set; }
}

public class CreateUserAccountViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
    [StringLength(150, ErrorMessage = "Họ và tên không được vượt quá 150 ký tự.")]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
    [StringLength(256, MinimumLength = 3, ErrorMessage = "Tên đăng nhập phải từ 3 đến 256 ký tự.")]
    [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Tên đăng nhập chỉ được chứa chữ cái, chữ số, dấu chấm, gạch dưới hoặc gạch ngang.")]
    [Display(Name = "Tên đăng nhập")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [StringLength(256, ErrorMessage = "Email không được vượt quá 256 ký tự.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    [Display(Name = "Xác nhận mật khẩu")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn vai trò.")]
    [Display(Name = "Vai trò")]
    public string Role { get; set; } = string.Empty;

    [Display(Name = "Cho phép đăng nhập ngay")]
    public bool IsActive { get; set; } = true;

    public IReadOnlyList<string> AvailableRoles { get; set; } = [];
}

public class EditUserAccountViewModel
{
    [Required]
    public string Id { get; set; } = string.Empty;

    [Required]
    public string ConcurrencyStamp { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
    [StringLength(150, ErrorMessage = "Họ và tên không được vượt quá 150 ký tự.")]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
    [StringLength(256, MinimumLength = 3, ErrorMessage = "Tên đăng nhập phải từ 3 đến 256 ký tự.")]
    [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Tên đăng nhập chỉ được chứa chữ cái, chữ số, dấu chấm, gạch dưới hoặc gạch ngang.")]
    [Display(Name = "Tên đăng nhập")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [StringLength(256, ErrorMessage = "Email không được vượt quá 256 ký tự.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn vai trò.")]
    [Display(Name = "Vai trò")]
    public string Role { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public IReadOnlyList<string> AvailableRoles { get; set; } = [];
}
