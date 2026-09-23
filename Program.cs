using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WarehouseManagement.Authorization;
using WarehouseManagement.Data;
using WarehouseManagement.Models;
using WarehouseManagement.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.SlidingExpiration = true;
});

builder.Services.Configure<AdminSeedOptions>(
    builder.Configuration.GetSection(AdminSeedOptions.SectionName));
builder.Services.Configure<TestAccountSeedOptions>(
    builder.Configuration.GetSection(TestAccountSeedOptions.SectionName));
builder.Services.AddScoped<IdentityDataInitializer>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        ApplicationPolicies.ManageAccounts,
        policy => policy.RequireRole(ApplicationRoles.Admin));
    options.AddPolicy(
        ApplicationPolicies.ViewCategories,
        policy => policy.RequireRole(
            ApplicationRoles.Admin,
            ApplicationRoles.WarehouseStaff,
            ApplicationRoles.Accountant));
    options.AddPolicy(
        ApplicationPolicies.ManageCategories,
        policy => policy.RequireRole(ApplicationRoles.Admin));
    options.AddPolicy(
        ApplicationPolicies.ManageCatalog,
        policy => policy.RequireRole(
            ApplicationRoles.Admin,
            ApplicationRoles.WarehouseStaff));
    options.AddPolicy(
        ApplicationPolicies.ManageSuppliers,
        policy => policy.RequireRole(
            ApplicationRoles.Admin,
            ApplicationRoles.WarehouseStaff));
    options.AddPolicy(
        ApplicationPolicies.CreateImportReceipts,
        policy => policy.RequireRole(
            ApplicationRoles.Admin,
            ApplicationRoles.WarehouseStaff));
    options.AddPolicy(
        ApplicationPolicies.CreateExportReceipts,
        policy => policy.RequireRole(
            ApplicationRoles.Admin,
            ApplicationRoles.WarehouseStaff));
    options.AddPolicy(
        ApplicationPolicies.ViewReports,
        policy => policy.RequireRole(
            ApplicationRoles.Admin,
            ApplicationRoles.Accountant));
    options.AddPolicy(
        ApplicationPolicies.ViewInventory,
        policy => policy.RequireRole(
            ApplicationRoles.Admin,
            ApplicationRoles.WarehouseStaff,
            ApplicationRoles.Accountant));
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var identityInitializer = scope.ServiceProvider
        .GetRequiredService<IdentityDataInitializer>();
    await identityInitializer.InitializeAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
