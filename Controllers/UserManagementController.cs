using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarehouseManagement.Authorization;
using WarehouseManagement.Data;
using WarehouseManagement.Models;
using WarehouseManagement.Models.UserManagement;

namespace WarehouseManagement.Controllers;

[Authorize(Policy = ApplicationPolicies.ManageAccounts)]
public class UserManagementController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager) : Controller
{
    private const int MaximumSearchLength = 200;

    [HttpGet]
    public async Task<IActionResult> Index(
        string? searchTerm,
        string? role,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        searchTerm = searchTerm?.Trim();
        role = role?.Trim().ToUpperInvariant();

        var query = context.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            if (searchTerm.Length > MaximumSearchLength)
            {
                ModelState.AddModelError(nameof(searchTerm), $"Từ khóa không được vượt quá {MaximumSearchLength} ký tự.");
                query = query.Where(_ => false);
            }
            else
            {
                query = query.Where(user =>
                    user.UserName != null && user.UserName.Contains(searchTerm) ||
                    user.Email != null && user.Email.Contains(searchTerm) ||
                    user.FullName.Contains(searchTerm));
            }
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            if (!ApplicationRoles.All.Contains(role, StringComparer.Ordinal))
            {
                ModelState.AddModelError(nameof(role), "Vai trò được chọn không hợp lệ.");
                query = query.Where(_ => false);
            }
            else
            {
                query = query.Where(user =>
                    context.UserRoles.Any(userRole =>
                        userRole.UserId == user.Id &&
                        context.Roles.Any(identityRole =>
                            identityRole.Id == userRole.RoleId &&
                            identityRole.NormalizedName == role)));
            }
        }

        if (isActive.HasValue)
        {
            query = query.Where(user => user.IsActive == isActive.Value);
        }

        var currentUserId = userManager.GetUserId(User);
        var users = await query
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.UserName)
            .Select(user => new UserAccountListItemViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                IsCurrentUser = user.Id == currentUserId
            })
            .ToListAsync(cancellationToken);

        await PopulateRolesAsync(users, cancellationToken);

        return View(new UserAccountIndexViewModel
        {
            SearchTerm = searchTerm,
            Role = role,
            IsActive = isActive,
            AvailableRoles = ApplicationRoles.All,
            Users = users
        });
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateUserAccountViewModel
        {
            AvailableRoles = ApplicationRoles.All,
            IsActive = true
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CreateUserAccountViewModel model,
        CancellationToken cancellationToken)
    {
        Normalize(model);
        model.AvailableRoles = ApplicationRoles.All;

        if (!await IsValidRoleAsync(model.Role))
        {
            ModelState.AddModelError(nameof(model.Role), "Vai trò được chọn không hợp lệ.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new ApplicationUser
        {
            FullName = model.FullName,
            UserName = model.UserName,
            Email = model.Email,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow,
            LockoutEnabled = true,
            LockoutEnd = model.IsActive ? null : DateTimeOffset.MaxValue
        };

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var createResult = await userManager.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            AddIdentityErrors(createResult);
            await transaction.RollbackAsync(cancellationToken);
            return View(model);
        }

        var roleResult = await userManager.AddToRoleAsync(user, model.Role);
        if (!roleResult.Succeeded)
        {
            AddIdentityErrors(roleResult);
            await transaction.RollbackAsync(cancellationToken);
            return View(model);
        }

        await transaction.CommitAsync(cancellationToken);
        TempData["SuccessMessage"] = $"Đã tạo tài khoản {user.UserName} thành công.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        var roles = await userManager.GetRolesAsync(user);
        return View(new EditUserAccountViewModel
        {
            Id = user.Id,
            ConcurrencyStamp = user.ConcurrencyStamp ?? string.Empty,
            FullName = user.FullName,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            Role = roles.FirstOrDefault(ApplicationRoles.All.Contains) ?? string.Empty,
            IsActive = user.IsActive,
            AvailableRoles = ApplicationRoles.All
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        EditUserAccountViewModel model,
        CancellationToken cancellationToken)
    {
        Normalize(model);
        model.AvailableRoles = ApplicationRoles.All;

        if (!await IsValidRoleAsync(model.Role))
        {
            ModelState.AddModelError(nameof(model.Role), "Vai trò được chọn không hợp lệ.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByIdAsync(model.Id);
        if (user is null)
        {
            return NotFound();
        }

        if (!string.Equals(user.ConcurrencyStamp, model.ConcurrencyStamp, StringComparison.Ordinal))
        {
            ModelState.AddModelError(string.Empty, "Tài khoản đã được cập nhật bởi thao tác khác. Vui lòng tải lại trang và thử lại.");
            return View(model);
        }

        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var currentRoles = await userManager.GetRolesAsync(user);
        if (currentRoles.Contains(ApplicationRoles.Admin) &&
            model.Role != ApplicationRoles.Admin &&
            await IsLastActiveAdminAsync(user, cancellationToken))
        {
            ModelState.AddModelError(nameof(model.Role), "Không thể gỡ quyền của ADMIN hoạt động cuối cùng.");
            await transaction.RollbackAsync(cancellationToken);
            return View(model);
        }

        user.FullName = model.FullName;
        user.UserName = model.UserName;
        user.Email = model.Email;
        user.UpdatedAt = DateTime.UtcNow;

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            AddIdentityErrors(updateResult);
            await transaction.RollbackAsync(cancellationToken);
            return View(model);
        }

        if (currentRoles.Count != 1 || !currentRoles.Contains(model.Role))
        {
            if (currentRoles.Count > 0)
            {
                var removeRolesResult = await userManager.RemoveFromRolesAsync(user, currentRoles);
                if (!removeRolesResult.Succeeded)
                {
                    AddIdentityErrors(removeRolesResult);
                    await transaction.RollbackAsync(cancellationToken);
                    return View(model);
                }
            }

            var addRoleResult = await userManager.AddToRoleAsync(user, model.Role);
            if (!addRoleResult.Succeeded)
            {
                AddIdentityErrors(addRoleResult);
                await transaction.RollbackAsync(cancellationToken);
                return View(model);
            }

            var stampResult = await userManager.UpdateSecurityStampAsync(user);
            if (!stampResult.Succeeded)
            {
                AddIdentityErrors(stampResult);
                await transaction.RollbackAsync(cancellationToken);
                return View(model);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        TempData["SuccessMessage"] = $"Đã cập nhật tài khoản {user.UserName} thành công.";
        return RedirectToAction(nameof(Details), new { id = user.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Lock(string? id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (user.Id == userManager.GetUserId(User))
        {
            TempData["ErrorMessage"] = "Bạn không thể tự khóa tài khoản đang đăng nhập.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (!user.IsActive)
        {
            TempData["InfoMessage"] = "Tài khoản này đã bị khóa trước đó.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        if (await userManager.IsInRoleAsync(user, ApplicationRoles.Admin) &&
            await IsLastActiveAdminAsync(user, cancellationToken))
        {
            TempData["ErrorMessage"] = "Không thể khóa ADMIN hoạt động cuối cùng.";
            await transaction.RollbackAsync(cancellationToken);
            return RedirectToAction(nameof(Details), new { id });
        }

        user.IsActive = false;
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        user.UpdatedAt = DateTime.UtcNow;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = GetIdentityErrorMessage(result);
            await transaction.RollbackAsync(cancellationToken);
            return RedirectToAction(nameof(Details), new { id });
        }

        var stampResult = await userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
        {
            TempData["ErrorMessage"] = GetIdentityErrorMessage(stampResult);
            await transaction.RollbackAsync(cancellationToken);
            return RedirectToAction(nameof(Details), new { id });
        }

        await transaction.CommitAsync(cancellationToken);
        TempData["SuccessMessage"] = $"Đã khóa tài khoản {user.UserName}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        user.IsActive = true;
        user.LockoutEnabled = true;
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;
        user.UpdatedAt = DateTime.UtcNow;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = GetIdentityErrorMessage(result);
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["SuccessMessage"] = $"Đã mở khóa tài khoản {user.UserName}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(string? id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var currentUserId = userManager.GetUserId(User);
        var user = await context.Users
            .AsNoTracking()
            .Where(applicationUser => applicationUser.Id == id)
            .Select(applicationUser => new UserAccountDetailsViewModel
            {
                Id = applicationUser.Id,
                FullName = applicationUser.FullName,
                UserName = applicationUser.UserName ?? string.Empty,
                Email = applicationUser.Email ?? string.Empty,
                IsActive = applicationUser.IsActive,
                CreatedAt = applicationUser.CreatedAt,
                UpdatedAt = applicationUser.UpdatedAt,
                IsCurrentUser = applicationUser.Id == currentUserId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        user.Roles = await (
                from userRole in context.UserRoles.AsNoTracking()
                join identityRole in context.Roles.AsNoTracking()
                    on userRole.RoleId equals identityRole.Id
                where userRole.UserId == id
                orderby identityRole.Name
                select identityRole.Name ?? string.Empty)
            .ToListAsync(cancellationToken);

        return View(user);
    }

    private async Task<bool> IsValidRoleAsync(string role)
    {
        return ApplicationRoles.All.Contains(role, StringComparer.Ordinal) &&
            await roleManager.RoleExistsAsync(role);
    }

    private async Task<bool> IsLastActiveAdminAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        if (!user.IsActive)
        {
            return false;
        }

        var activeAdminCount = await context.Users
            .AsNoTracking()
            .CountAsync(candidate =>
                candidate.IsActive &&
                context.UserRoles.Any(userRole =>
                    userRole.UserId == candidate.Id &&
                    context.Roles.Any(role =>
                        role.Id == userRole.RoleId &&
                        role.NormalizedName == ApplicationRoles.Admin)),
                cancellationToken);

        return activeAdminCount <= 1;
    }

    private async Task PopulateRolesAsync(
        List<UserAccountListItemViewModel> users,
        CancellationToken cancellationToken)
    {
        if (users.Count == 0)
        {
            return;
        }

        var userIds = users.Select(user => user.Id).ToList();
        var roleAssignments = await (
                from userRole in context.UserRoles.AsNoTracking()
                join identityRole in context.Roles.AsNoTracking()
                    on userRole.RoleId equals identityRole.Id
                where userIds.Contains(userRole.UserId)
                select new
                {
                    userRole.UserId,
                    RoleName = identityRole.Name ?? string.Empty
                })
            .ToListAsync(cancellationToken);

        var rolesByUser = roleAssignments
            .GroupBy(assignment => assignment.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(assignment => assignment.RoleName)
                    .OrderBy(roleName => roleName)
                    .ToList());

        foreach (var user in users)
        {
            user.Roles = rolesByUser.GetValueOrDefault(user.Id) ?? [];
        }
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, TranslateIdentityError(error));
        }
    }

    private static string GetIdentityErrorMessage(IdentityResult result)
    {
        return string.Join(" ", result.Errors.Select(TranslateIdentityError));
    }

    private static string TranslateIdentityError(IdentityError error)
    {
        return error.Code switch
        {
            "DuplicateUserName" => "Tên đăng nhập đã được sử dụng.",
            "DuplicateEmail" => "Email đã được sử dụng.",
            "PasswordTooShort" => "Mật khẩu chưa đủ độ dài tối thiểu.",
            "PasswordRequiresNonAlphanumeric" => "Mật khẩu phải có ít nhất một ký tự đặc biệt.",
            "PasswordRequiresDigit" => "Mật khẩu phải có ít nhất một chữ số.",
            "PasswordRequiresUpper" => "Mật khẩu phải có ít nhất một chữ hoa.",
            "PasswordRequiresLower" => "Mật khẩu phải có ít nhất một chữ thường.",
            "ConcurrencyFailure" => "Dữ liệu đã được cập nhật bởi thao tác khác. Vui lòng thử lại.",
            _ => "Không thể hoàn thành thao tác với tài khoản."
        };
    }

    private static void Normalize(CreateUserAccountViewModel model)
    {
        model.FullName = model.FullName?.Trim() ?? string.Empty;
        model.UserName = model.UserName?.Trim() ?? string.Empty;
        model.Email = model.Email?.Trim() ?? string.Empty;
        model.Role = model.Role?.Trim().ToUpperInvariant() ?? string.Empty;
    }

    private static void Normalize(EditUserAccountViewModel model)
    {
        model.FullName = model.FullName?.Trim() ?? string.Empty;
        model.UserName = model.UserName?.Trim() ?? string.Empty;
        model.Email = model.Email?.Trim() ?? string.Empty;
        model.Role = model.Role?.Trim().ToUpperInvariant() ?? string.Empty;
    }
}
