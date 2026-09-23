namespace WarehouseManagement.Authorization;

public static class ApplicationRoles
{
    public const string Admin = "ADMIN";
    public const string WarehouseStaff = "WAREHOUSE_STAFF";
    public const string Accountant = "ACCOUNTANT";

    public static readonly IReadOnlyList<string> All =
    [
        Admin,
        WarehouseStaff,
        Accountant
    ];
}
