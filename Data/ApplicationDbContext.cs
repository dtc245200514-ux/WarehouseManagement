using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WarehouseManagement.Models;
using WarehouseManagement.Models.Enums;

namespace WarehouseManagement.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<ImportReceipt> ImportReceipts => Set<ImportReceipt>();

    public DbSet<ImportReceiptDetail> ImportReceiptDetails => Set<ImportReceiptDetail>();

    public DbSet<ExportReceipt> ExportReceipts => Set<ExportReceipt>();

    public DbSet<ExportReceiptDetail> ExportReceiptDetails => Set<ExportReceiptDetail>();

    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigureApplicationUser(builder.Entity<ApplicationUser>());
        ConfigureCategory(builder.Entity<Category>());
        ConfigureProduct(builder.Entity<Product>());
        ConfigureSupplier(builder.Entity<Supplier>());
        ConfigureImportReceipt(builder.Entity<ImportReceipt>());
        ConfigureImportReceiptDetail(builder.Entity<ImportReceiptDetail>());
        ConfigureExportReceipt(builder.Entity<ExportReceipt>());
        ConfigureExportReceiptDetail(builder.Entity<ExportReceiptDetail>());
        ConfigureInventoryTransaction(builder.Entity<InventoryTransaction>());
    }

    private static void ConfigureApplicationUser(EntityTypeBuilder<ApplicationUser> entity)
    {
        entity.Property(user => user.FullName)
            .HasMaxLength(150)
            .IsRequired();

        entity.Property(user => user.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        entity.Property(user => user.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        entity.Property(user => user.UpdatedAt)
            .IsRequired(false);
    }

    private static void ConfigureCategory(EntityTypeBuilder<Category> entity)
    {
        entity.HasKey(category => category.Id);

        entity.Property(category => category.Name)
            .HasMaxLength(100)
            .IsRequired();

        entity.Property(category => category.Description)
            .HasMaxLength(500)
            .IsRequired(false);

        entity.Property(category => category.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        entity.Property(category => category.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        entity.Property(category => category.RowVersion)
            .IsRowVersion();

        entity.HasIndex(category => category.Name)
            .IsUnique();

        entity.HasMany(category => category.Products)
            .WithOne(product => product.Category)
            .HasForeignKey(product => product.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureProduct(EntityTypeBuilder<Product> entity)
    {
        entity.HasKey(product => product.Id);

        entity.Property(product => product.Code)
            .HasMaxLength(50)
            .IsRequired();

        entity.Property(product => product.Name)
            .HasMaxLength(200)
            .IsRequired();

        entity.Property(product => product.Unit)
            .HasMaxLength(50)
            .IsRequired();

        entity.Property(product => product.Description)
            .HasMaxLength(500)
            .IsRequired(false);

        entity.Property(product => product.CurrentQuantity)
            .HasPrecision(18, 3)
            .HasDefaultValue(0m)
            .IsRequired();

        entity.Property(product => product.MinimumStockLevel)
            .HasPrecision(18, 3)
            .HasDefaultValue(0m)
            .IsRequired();

        entity.Property(product => product.AverageUnitCost)
            .HasPrecision(18, 2)
            .IsRequired(false);

        entity.Property(product => product.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        entity.Property(product => product.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        entity.Property(product => product.UpdatedAt)
            .IsRequired(false);

        entity.Property(product => product.RowVersion)
            .IsRowVersion();

        entity.HasIndex(product => product.Code)
            .IsUnique();

        entity.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_Products_CurrentQuantity_NonNegative",
                "[CurrentQuantity] >= 0");
            table.HasCheckConstraint(
                "CK_Products_MinimumStockLevel_NonNegative",
                "[MinimumStockLevel] >= 0");
            table.HasCheckConstraint(
                "CK_Products_AverageUnitCost_NonNegative",
                "[AverageUnitCost] IS NULL OR [AverageUnitCost] >= 0");
        });

        entity.HasMany(product => product.ImportReceiptDetails)
            .WithOne(detail => detail.Product)
            .HasForeignKey(detail => detail.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasMany(product => product.ExportReceiptDetails)
            .WithOne(detail => detail.Product)
            .HasForeignKey(detail => detail.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasMany(product => product.InventoryTransactions)
            .WithOne(transaction => transaction.Product)
            .HasForeignKey(transaction => transaction.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureSupplier(EntityTypeBuilder<Supplier> entity)
    {
        entity.HasKey(supplier => supplier.Id);

        entity.Property(supplier => supplier.Code)
            .HasMaxLength(50)
            .IsRequired();

        entity.Property(supplier => supplier.Name)
            .HasMaxLength(200)
            .IsRequired();

        entity.Property(supplier => supplier.Phone)
            .HasMaxLength(20)
            .IsRequired(false);

        entity.Property(supplier => supplier.Email)
            .HasMaxLength(150)
            .IsRequired(false);

        entity.Property(supplier => supplier.Address)
            .HasMaxLength(300)
            .IsRequired(false);

        entity.Property(supplier => supplier.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        entity.Property(supplier => supplier.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        entity.Property(supplier => supplier.UpdatedAt)
            .IsRequired(false);

        entity.Property(supplier => supplier.RowVersion)
            .IsRowVersion();

        entity.HasIndex(supplier => supplier.Code)
            .IsUnique();

        entity.HasMany(supplier => supplier.ImportReceipts)
            .WithOne(receipt => receipt.Supplier)
            .HasForeignKey(receipt => receipt.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureImportReceipt(EntityTypeBuilder<ImportReceipt> entity)
    {
        var draft = (byte)ReceiptStatus.Draft;
        var posted = (byte)ReceiptStatus.Posted;
        var cancelled = (byte)ReceiptStatus.Cancelled;

        entity.HasKey(receipt => receipt.Id);

        entity.Property(receipt => receipt.ReceiptNumber)
            .HasMaxLength(30)
            .IsRequired();

        entity.Property(receipt => receipt.Status)
            .HasConversion<byte>()
            .HasDefaultValue(ReceiptStatus.Draft)
            .IsRequired();

        entity.Property(receipt => receipt.CreatedByUserId)
            .HasMaxLength(450)
            .IsRequired();

        entity.Property(receipt => receipt.ReceiptDate)
            .HasColumnType("date")
            .HasDefaultValueSql("CONVERT(date, GETDATE())")
            .IsRequired();

        entity.Property(receipt => receipt.PostedByUserId)
            .HasMaxLength(450)
            .IsRequired(false);

        entity.Property(receipt => receipt.CancelledByUserId)
            .HasMaxLength(450)
            .IsRequired(false);

        entity.Property(receipt => receipt.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        entity.Property(receipt => receipt.PostedAt)
            .IsRequired(false);

        entity.Property(receipt => receipt.CancelledAt)
            .IsRequired(false);

        entity.Property(receipt => receipt.Note)
            .HasMaxLength(500)
            .IsRequired(false);

        entity.Property(receipt => receipt.RowVersion)
            .IsRowVersion();

        entity.HasIndex(receipt => receipt.ReceiptNumber)
            .IsUnique();

        entity.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_ImportReceipts_Status",
                $"[Status] IN ({draft}, {posted}, {cancelled})");
            table.HasCheckConstraint(
                "CK_ImportReceipts_StatusAudit",
                $"([Status] = {draft} " +
                "AND [PostedByUserId] IS NULL AND [PostedAt] IS NULL " +
                "AND [CancelledByUserId] IS NULL AND [CancelledAt] IS NULL) " +
                $"OR ([Status] = {posted} " +
                "AND [PostedByUserId] IS NOT NULL AND [PostedAt] IS NOT NULL " +
                "AND [CancelledByUserId] IS NULL AND [CancelledAt] IS NULL) " +
                $"OR ([Status] = {cancelled} " +
                "AND [CancelledByUserId] IS NOT NULL AND [CancelledAt] IS NOT NULL " +
                "AND (([PostedByUserId] IS NULL AND [PostedAt] IS NULL) " +
                "OR ([PostedByUserId] IS NOT NULL AND [PostedAt] IS NOT NULL)))");
        });

        entity.HasOne(receipt => receipt.CreatedByUser)
            .WithMany(user => user.CreatedImportReceipts)
            .HasForeignKey(receipt => receipt.CreatedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne(receipt => receipt.PostedByUser)
            .WithMany(user => user.PostedImportReceipts)
            .HasForeignKey(receipt => receipt.PostedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne(receipt => receipt.CancelledByUser)
            .WithMany(user => user.CancelledImportReceipts)
            .HasForeignKey(receipt => receipt.CancelledByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasMany(receipt => receipt.Details)
            .WithOne(detail => detail.ImportReceipt)
            .HasForeignKey(detail => detail.ImportReceiptId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureImportReceiptDetail(EntityTypeBuilder<ImportReceiptDetail> entity)
    {
        entity.HasKey(detail => detail.Id);

        entity.Property(detail => detail.Quantity)
            .HasPrecision(18, 3)
            .IsRequired();

        entity.Property(detail => detail.UnitCost)
            .HasPrecision(18, 2)
            .IsRequired();

        entity.Property(detail => detail.Note)
            .HasMaxLength(300)
            .IsRequired(false);

        entity.HasIndex(detail => new { detail.ImportReceiptId, detail.ProductId })
            .IsUnique();

        entity.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_ImportReceiptDetails_Quantity_Positive",
                "[Quantity] > 0");
            table.HasCheckConstraint(
                "CK_ImportReceiptDetails_UnitCost_NonNegative",
                "[UnitCost] >= 0");
        });
    }

    private static void ConfigureExportReceipt(EntityTypeBuilder<ExportReceipt> entity)
    {
        var draft = (byte)ReceiptStatus.Draft;
        var posted = (byte)ReceiptStatus.Posted;
        var cancelled = (byte)ReceiptStatus.Cancelled;

        entity.HasKey(receipt => receipt.Id);

        entity.Property(receipt => receipt.ReceiptNumber)
            .HasMaxLength(30)
            .IsRequired();

        entity.Property(receipt => receipt.Status)
            .HasConversion<byte>()
            .HasDefaultValue(ReceiptStatus.Draft)
            .IsRequired();

        entity.Property(receipt => receipt.CreatedByUserId)
            .HasMaxLength(450)
            .IsRequired();

        entity.Property(receipt => receipt.PostedByUserId)
            .HasMaxLength(450)
            .IsRequired(false);

        entity.Property(receipt => receipt.CancelledByUserId)
            .HasMaxLength(450)
            .IsRequired(false);

        entity.Property(receipt => receipt.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        entity.Property(receipt => receipt.PostedAt)
            .IsRequired(false);

        entity.Property(receipt => receipt.CancelledAt)
            .IsRequired(false);

        entity.Property(receipt => receipt.Note)
            .HasMaxLength(500)
            .IsRequired(false);

        entity.Property(receipt => receipt.RowVersion)
            .IsRowVersion();

        entity.HasIndex(receipt => receipt.ReceiptNumber)
            .IsUnique();

        entity.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_ExportReceipts_Status",
                $"[Status] IN ({draft}, {posted}, {cancelled})");
            table.HasCheckConstraint(
                "CK_ExportReceipts_StatusAudit",
                $"([Status] = {draft} " +
                "AND [PostedByUserId] IS NULL AND [PostedAt] IS NULL " +
                "AND [CancelledByUserId] IS NULL AND [CancelledAt] IS NULL) " +
                $"OR ([Status] = {posted} " +
                "AND [PostedByUserId] IS NOT NULL AND [PostedAt] IS NOT NULL " +
                "AND [CancelledByUserId] IS NULL AND [CancelledAt] IS NULL) " +
                $"OR ([Status] = {cancelled} " +
                "AND [CancelledByUserId] IS NOT NULL AND [CancelledAt] IS NOT NULL " +
                "AND (([PostedByUserId] IS NULL AND [PostedAt] IS NULL) " +
                "OR ([PostedByUserId] IS NOT NULL AND [PostedAt] IS NOT NULL)))");
        });

        entity.HasOne(receipt => receipt.CreatedByUser)
            .WithMany(user => user.CreatedExportReceipts)
            .HasForeignKey(receipt => receipt.CreatedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne(receipt => receipt.PostedByUser)
            .WithMany(user => user.PostedExportReceipts)
            .HasForeignKey(receipt => receipt.PostedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne(receipt => receipt.CancelledByUser)
            .WithMany(user => user.CancelledExportReceipts)
            .HasForeignKey(receipt => receipt.CancelledByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasMany(receipt => receipt.Details)
            .WithOne(detail => detail.ExportReceipt)
            .HasForeignKey(detail => detail.ExportReceiptId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureExportReceiptDetail(EntityTypeBuilder<ExportReceiptDetail> entity)
    {
        entity.HasKey(detail => detail.Id);

        entity.Property(detail => detail.Quantity)
            .HasPrecision(18, 3)
            .IsRequired();

        entity.Property(detail => detail.UnitCost)
            .HasPrecision(18, 2)
            .IsRequired(false);

        entity.Property(detail => detail.Note)
            .HasMaxLength(300)
            .IsRequired(false);

        entity.HasIndex(detail => new { detail.ExportReceiptId, detail.ProductId })
            .IsUnique();

        entity.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_ExportReceiptDetails_Quantity_Positive",
                "[Quantity] > 0");
            table.HasCheckConstraint(
                "CK_ExportReceiptDetails_UnitCost_NonNegative",
                "[UnitCost] IS NULL OR [UnitCost] >= 0");
        });
    }

    private static void ConfigureInventoryTransaction(EntityTypeBuilder<InventoryTransaction> entity)
    {
        var import = (byte)InventoryTransactionType.Import;
        var export = (byte)InventoryTransactionType.Export;
        var importCancellation = (byte)InventoryTransactionType.ImportCancellation;
        var exportCancellation = (byte)InventoryTransactionType.ExportCancellation;
        var adjustmentIncrease = (byte)InventoryTransactionType.AdjustmentIncrease;
        var adjustmentDecrease = (byte)InventoryTransactionType.AdjustmentDecrease;
        var openingBalance = (byte)InventoryTransactionType.OpeningBalance;

        entity.HasKey(transaction => transaction.Id);

        entity.Property(transaction => transaction.TransactionType)
            .HasConversion<byte>()
            .IsRequired();

        entity.Property(transaction => transaction.QuantityChange)
            .HasPrecision(18, 3)
            .IsRequired();

        entity.Property(transaction => transaction.BalanceBefore)
            .HasPrecision(18, 3)
            .IsRequired();

        entity.Property(transaction => transaction.BalanceAfter)
            .HasPrecision(18, 3)
            .IsRequired();

        entity.Property(transaction => transaction.PerformedByUserId)
            .HasMaxLength(450)
            .IsRequired();

        entity.Property(transaction => transaction.OccurredAt)
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        entity.Property(transaction => transaction.Note)
            .HasMaxLength(500)
            .IsRequired(false);

        entity.HasIndex(transaction => new
        {
            transaction.ProductId,
            transaction.OccurredAt
        });
        entity.HasIndex(transaction => transaction.OccurredAt);
        entity.HasIndex(transaction => transaction.TransactionType);
        entity.HasIndex(transaction => transaction.ImportReceiptId);
        entity.HasIndex(transaction => transaction.ExportReceiptId);

        entity.HasIndex(transaction => new
            {
                transaction.ImportReceiptId,
                transaction.ProductId,
                transaction.TransactionType
            })
            .IsUnique()
            .HasFilter("[ImportReceiptId] IS NOT NULL");

        entity.HasIndex(transaction => new
            {
                transaction.ExportReceiptId,
                transaction.ProductId,
                transaction.TransactionType
            })
            .IsUnique()
            .HasFilter("[ExportReceiptId] IS NOT NULL");

        entity.HasIndex(transaction => transaction.ReversedTransactionId)
            .IsUnique()
            .HasFilter("[ReversedTransactionId] IS NOT NULL");

        entity.HasIndex(transaction => transaction.ProductId)
            .IsUnique()
            .HasDatabaseName("UX_InventoryTransactions_Product_OpeningBalance")
            .HasFilter($"[TransactionType] = {openingBalance}");

        entity.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_InventoryTransactions_Type",
                $"[TransactionType] IN ({import}, {export}, {importCancellation}, " +
                $"{exportCancellation}, {adjustmentIncrease}, {adjustmentDecrease}, {openingBalance})");
            table.HasCheckConstraint(
                "CK_InventoryTransactions_QuantityChange_NonZero",
                "[QuantityChange] <> 0");
            table.HasCheckConstraint(
                "CK_InventoryTransactions_Balances_NonNegative",
                "[BalanceBefore] >= 0 AND [BalanceAfter] >= 0");
            table.HasCheckConstraint(
                "CK_InventoryTransactions_BalanceEquation",
                "[BalanceAfter] = [BalanceBefore] + [QuantityChange]");
            table.HasCheckConstraint(
                "CK_InventoryTransactions_QuantityDirection",
                $"([TransactionType] IN ({import}, {exportCancellation}, {adjustmentIncrease}, {openingBalance}) " +
                "AND [QuantityChange] > 0) " +
                $"OR ([TransactionType] IN ({export}, {importCancellation}, {adjustmentDecrease}) " +
                "AND [QuantityChange] < 0)");
            table.HasCheckConstraint(
                "CK_InventoryTransactions_Source",
                $"([TransactionType] IN ({import}, {importCancellation}) " +
                "AND [ImportReceiptId] IS NOT NULL AND [ExportReceiptId] IS NULL) " +
                $"OR ([TransactionType] IN ({export}, {exportCancellation}) " +
                "AND [ImportReceiptId] IS NULL AND [ExportReceiptId] IS NOT NULL) " +
                $"OR ([TransactionType] IN ({adjustmentIncrease}, {adjustmentDecrease}, {openingBalance}) " +
                "AND [ImportReceiptId] IS NULL AND [ExportReceiptId] IS NULL)");
            table.HasCheckConstraint(
                "CK_InventoryTransactions_ReversalReference",
                $"([TransactionType] IN ({importCancellation}, {exportCancellation}) " +
                "AND [ReversedTransactionId] IS NOT NULL) " +
                $"OR ([TransactionType] NOT IN ({importCancellation}, {exportCancellation}) " +
                "AND [ReversedTransactionId] IS NULL)");
            table.HasCheckConstraint(
                "CK_InventoryTransactions_NoSelfReversal",
                "[ReversedTransactionId] IS NULL OR [ReversedTransactionId] <> [Id]");
        });

        entity.HasOne(transaction => transaction.ImportReceipt)
            .WithMany(receipt => receipt.InventoryTransactions)
            .HasForeignKey(transaction => transaction.ImportReceiptId)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne(transaction => transaction.ExportReceipt)
            .WithMany(receipt => receipt.InventoryTransactions)
            .HasForeignKey(transaction => transaction.ExportReceiptId)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne(transaction => transaction.ReversedTransaction)
            .WithOne(transaction => transaction.ReversalTransaction)
            .HasForeignKey<InventoryTransaction>(transaction => transaction.ReversedTransactionId)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne(transaction => transaction.PerformedByUser)
            .WithMany(user => user.PerformedInventoryTransactions)
            .HasForeignKey(transaction => transaction.PerformedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
