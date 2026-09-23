using System.ComponentModel.DataAnnotations;

namespace WarehouseManagement.Models.Account;

public class LoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập email hoặc tên đăng nhập.")]
    [Display(Name = "Email hoặc tên đăng nhập")]
    public string LoginIdentifier { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Ghi nhớ đăng nhập")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
