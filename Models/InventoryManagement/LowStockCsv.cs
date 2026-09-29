using System.Globalization;
using System.Text;

namespace WarehouseManagement.Models.InventoryManagement;

public static class LowStockCsv
{
    // Quote every cell; neutralize text that spreadsheet applications could execute.
    public static string TextCell(string value)
    {
        var trimmed = value.TrimStart();
        if (trimmed.Length > 0 && "=+-@".Contains(trimmed[0]) ||
            value.StartsWith('\t') || value.StartsWith('\r') || value.StartsWith('\n'))
            value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    public static byte[] Write(IReadOnlyList<InventoryListItemViewModel> rows)
    {
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", new[] { "Mã hàng hóa", "Tên hàng hóa", "Danh mục", "Đơn vị tính",
            "Tồn hiện tại", "Mức tồn tối thiểu", "Chênh lệch", "Trạng thái cảnh báo" }.Select(TextCell)));
        foreach (var row in rows)
        {
            csv.AppendLine(string.Join(",", TextCell(row.Code), TextCell(row.Name),
                TextCell(row.CategoryName), TextCell(row.Unit),
                row.CurrentQuantity.ToString("0.000", CultureInfo.InvariantCulture),
                row.MinimumStockLevel.ToString("0.000", CultureInfo.InvariantCulture),
                (row.CurrentQuantity - row.MinimumStockLevel).ToString("0.000", CultureInfo.InvariantCulture),
                TextCell(StockLevelRules.Label(row.StockStatus))));
        }
        // BOM permits Excel to detect Vietnamese UTF-8 text.
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }
}
