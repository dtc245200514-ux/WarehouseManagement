using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarehouseManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptDateToImportReceipt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReceiptDate",
                table: "ImportReceipts",
                type: "date",
                nullable: false,
                defaultValueSql: "CONVERT(date, GETDATE())");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceiptDate",
                table: "ImportReceipts");
        }
    }
}
