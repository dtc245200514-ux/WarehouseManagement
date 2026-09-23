namespace WarehouseManagement.Authorization;

public static class ApplicationPolicies
{
    public const string ManageAccounts = "ManageAccounts";
    public const string ViewCategories = "ViewCategories";
    public const string ManageCategories = "ManageCategories";
    public const string ManageCatalog = "ManageCatalog";
    public const string ManageSuppliers = "ManageSuppliers";
    public const string CreateImportReceipts = "CreateImportReceipts";
    public const string CreateExportReceipts = "CreateExportReceipts";
    public const string ViewReports = "ViewReports";
    public const string ViewInventory = "ViewInventory";
}
