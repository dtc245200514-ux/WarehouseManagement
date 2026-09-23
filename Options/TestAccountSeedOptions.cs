namespace WarehouseManagement.Options;

public sealed class TestAccountSeedOptions
{
    public const string SectionName = "TestAccountSeed";

    public bool Enabled { get; set; }

    public TestUserSeedOptions WarehouseStaff { get; set; } = new();

    public TestUserSeedOptions Accountant { get; set; } = new();
}

public sealed class TestUserSeedOptions
{
    public string UserName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
