using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WarehouseManagement.Authorization;
using WarehouseManagement.Data;
using WarehouseManagement.Models;
using WarehouseManagement.Options;
using WarehouseManagement.Services.Ai;
using System.Security.Claims;
using System.Threading.RateLimiting;

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
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection(AiOptions.SectionName));
builder.Services.AddScoped<InventoryAnalysisDataService>();
builder.Services.AddHttpClient<GeminiAnalysisAdapter>(client => client.Timeout = Timeout.InfiniteTimeSpan)
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
    .RemoveAllLoggers();
builder.Services.AddHttpClient<IAiAnalysisService, AiAnalysisService>(client =>
    client.Timeout = Timeout.InfiniteTimeSpan)
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
    .RemoveAllLoggers();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("ai-analysis", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
        }));
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync("Bạn đã gửi quá nhiều yêu cầu phân tích. Vui lòng chờ một phút rồi thử lại.", token);
    };
});

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
app.UseRateLimiter();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
