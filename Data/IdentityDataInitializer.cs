using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WarehouseManagement.Authorization;
using WarehouseManagement.Models;
using WarehouseManagement.Options;

namespace WarehouseManagement.Data;

public sealed class IdentityDataInitializer(
    ApplicationDbContext dbContext,
    RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IOptions<AdminSeedOptions> options,
    IOptions<TestAccountSeedOptions> testAccountOptions,
    IHostEnvironment environment,
    ILogger<IdentityDataInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var testSettings = testAccountOptions.Value;

        logger.LogInformation(
            "Effective TestAccountSeed configuration: Enabled={Enabled}; " +
            "WarehouseStaff UserName={WarehouseStaffUserName}, Email={WarehouseStaffEmail}, " +
            "FullName={WarehouseStaffFullName}, PasswordConfigured={WarehouseStaffPasswordConfigured}; " +
            "Accountant UserName={AccountantUserName}, Email={AccountantEmail}, " +
            "FullName={AccountantFullName}, PasswordConfigured={AccountantPasswordConfigured}.",
            testSettings.Enabled,
            testSettings.WarehouseStaff.UserName,
            testSettings.WarehouseStaff.Email,
            testSettings.WarehouseStaff.FullName,
            !string.IsNullOrWhiteSpace(testSettings.WarehouseStaff.Password),
            testSettings.Accountant.UserName,
            testSettings.Accountant.Email,
            testSettings.Accountant.FullName,
            !string.IsNullOrWhiteSpace(testSettings.Accountant.Password));

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var roleName in ApplicationRoles.All)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    EnsureSucceeded(
                        await roleManager.CreateAsync(new IdentityRole(roleName)),
                        $"Không thể tạo role {roleName}.");
                }
            }

            if (settings.Enabled)
            {
                ValidateSettings(settings);
                await EnsureConfiguredUserAsync(
                    settings.UserName,
                    settings.Email,
                    settings.FullName,
                    settings.Password,
                    ApplicationRoles.Admin,
                    "ADMIN");
            }
            else
            {
                logger.LogInformation("ADMIN account initialization is disabled.");
            }

            if (testSettings.Enabled)
            {
                if (!environment.IsDevelopment())
                {
                    throw new InvalidOperationException(
                        "Chỉ được khởi tạo tài khoản kiểm thử trong môi trường Development.");
                }

                ValidateTestAccountSettings(testSettings);

                await EnsureConfiguredUserAsync(
                    testSettings.WarehouseStaff.UserName,
                    testSettings.WarehouseStaff.Email,
                    testSettings.WarehouseStaff.FullName,
                    testSettings.WarehouseStaff.Password,
                    ApplicationRoles.WarehouseStaff,
                    "WAREHOUSE_STAFF test account");

                await EnsureConfiguredUserAsync(
                    testSettings.Accountant.UserName,
                    testSettings.Accountant.Email,
                    testSettings.Accountant.FullName,
                    testSettings.Accountant.Password,
                    ApplicationRoles.Accountant,
                    "ACCOUNTANT test account");
            }
            else
            {
                logger.LogInformation("Development test account initialization is disabled.");
            }

            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation("Identity data initialization completed successfully.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task EnsureConfiguredUserAsync(
        string userName,
        string email,
        string fullName,
        string password,
        string roleName,
        string accountLabel)
    {
        var userByName = await userManager.FindByNameAsync(userName);
        var userByEmail = await userManager.FindByEmailAsync(email);

        if (userByName is not null &&
            userByEmail is not null &&
            userByName.Id != userByEmail.Id)
        {
            throw new InvalidOperationException(
                $"Username và email cấu hình {accountLabel} đang thuộc hai tài khoản khác nhau.");
        }

        var user = userByName ?? userByEmail;

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = userName,
                Email = email,
                FullName = fullName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            EnsureSucceeded(
                await userManager.CreateAsync(user, password),
                $"Không thể tạo {accountLabel}.");
        }
        else
        {
            EnsureExistingUserMatchesConfiguration(
                user,
                userName,
                email,
                fullName,
                accountLabel);

            var existingRoles = await userManager.GetRolesAsync(user);
            var unexpectedRoles = existingRoles
                .Where(existingRole =>
                    !string.Equals(existingRole, roleName, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (unexpectedRoles.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Tài khoản {accountLabel} đang có role không phù hợp: " +
                    $"{string.Join(", ", unexpectedRoles)}. Không tự động thay đổi hoặc nâng quyền.");
            }
        }

        if (!await userManager.IsInRoleAsync(user, roleName))
        {
            EnsureSucceeded(
                await userManager.AddToRoleAsync(user, roleName),
                $"Không thể gán role {roleName} cho {accountLabel}.");
        }
    }

    private static void ValidateSettings(AdminSeedOptions settings)
    {
        var missingKeys = new List<string>();

        if (string.IsNullOrWhiteSpace(settings.UserName))
        {
            missingKeys.Add($"{AdminSeedOptions.SectionName}:UserName");
        }

        if (string.IsNullOrWhiteSpace(settings.Email))
        {
            missingKeys.Add($"{AdminSeedOptions.SectionName}:Email");
        }

        if (string.IsNullOrWhiteSpace(settings.FullName))
        {
            missingKeys.Add($"{AdminSeedOptions.SectionName}:FullName");
        }

        if (string.IsNullOrWhiteSpace(settings.Password))
        {
            missingKeys.Add($"{AdminSeedOptions.SectionName}:Password");
        }

        if (missingKeys.Count > 0)
        {
            throw new InvalidOperationException(
                $"Thiếu cấu hình khởi tạo ADMIN: {string.Join(", ", missingKeys)}.");
        }
    }

    private static void ValidateTestAccountSettings(TestAccountSeedOptions settings)
    {
        ValidateTestUserSettings(settings.WarehouseStaff, "WarehouseStaff");
        ValidateTestUserSettings(settings.Accountant, "Accountant");
    }

    private static void ValidateTestUserSettings(TestUserSeedOptions settings, string accountName)
    {
        var missingKeys = new List<string>();
        var keyPrefix = $"{TestAccountSeedOptions.SectionName}:{accountName}";

        if (string.IsNullOrWhiteSpace(settings.UserName))
        {
            missingKeys.Add($"{keyPrefix}:UserName");
        }

        if (string.IsNullOrWhiteSpace(settings.Email))
        {
            missingKeys.Add($"{keyPrefix}:Email");
        }

        if (string.IsNullOrWhiteSpace(settings.FullName))
        {
            missingKeys.Add($"{keyPrefix}:FullName");
        }

        if (string.IsNullOrWhiteSpace(settings.Password))
        {
            missingKeys.Add($"{keyPrefix}:Password");
        }

        if (missingKeys.Count > 0)
        {
            throw new InvalidOperationException(
                $"Thiếu cấu hình tài khoản kiểm thử: {string.Join(", ", missingKeys)}.");
        }
    }

    private static void EnsureExistingUserMatchesConfiguration(
        ApplicationUser user,
        string userName,
        string email,
        string fullName,
        string accountLabel)
    {
        var mismatchedFields = new List<string>();

        if (!string.Equals(user.UserName, userName, StringComparison.OrdinalIgnoreCase))
        {
            mismatchedFields.Add(nameof(ApplicationUser.UserName));
        }

        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            mismatchedFields.Add(nameof(ApplicationUser.Email));
        }

        if (!string.Equals(user.FullName, fullName, StringComparison.Ordinal))
        {
            mismatchedFields.Add(nameof(ApplicationUser.FullName));
        }

        if (mismatchedFields.Count > 0)
        {
            throw new InvalidOperationException(
                $"Tài khoản hiện có không khớp cấu hình {accountLabel}. " +
                $"Các trường không khớp: {string.Join(", ", mismatchedFields)}. " +
                "Không tự động thay đổi hoặc nâng quyền tài khoản này.");
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string message)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join(
            "; ",
            result.Errors.Select(error => $"{error.Code}: {error.Description}"));

        throw new InvalidOperationException($"{message} {errors}");
    }
}
