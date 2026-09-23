using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using WarehouseManagement.Models.Enums;

namespace WarehouseManagement.Models.ImportReceiptManagement;

public sealed class CreateImportReceiptViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn nhà cung cấp.")]
    [Display(Name = "Nhà cung cấp")]
    public int SupplierId { get; set; }

    [Required(ErrorMessage = "Ngày nhập là bắt buộc.")]
    [DataType(DataType.Date)]
    [Display(Name = "Ngày nhập")]
    public DateTime ReceiptDate { get; set; }

    [StringLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự.")]
    [Display(Name = "Ghi chú")]
    public string? Note { get; set; }

    public List<CreateImportReceiptDetailViewModel> Details { get; set; } = [];

    [ValidateNever]
    public IReadOnlyList<ImportReceiptSupplierOptionViewModel> Suppliers { get; set; } = [];

    [ValidateNever]
    public IReadOnlyList<ImportReceiptProductOptionViewModel> Products { get; set; } = [];
}

public sealed class CreateImportReceiptDetailViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn hàng hóa.")]
    public int ProductId { get; set; }

    [Range(typeof(decimal), "0.001", "999999999999999.999",
        ErrorMessage = "Số lượng phải lớn hơn 0 và không vượt quá giới hạn cho phép.")]
    public decimal Quantity { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999.99",
        ErrorMessage = "Đơn giá phải từ 0 trở lên và không vượt quá giới hạn cho phép.")]
    public decimal UnitCost { get; set; }
}

public sealed class ImportReceiptSupplierOptionViewModel
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public sealed class ImportReceiptProductOptionViewModel
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
}

public sealed class ImportReceiptIndexViewModel
{
    public string? SearchTerm { get; init; }
    public int? SupplierId { get; init; }
    public ReceiptStatus? Status { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public IReadOnlyList<ImportReceiptListItemViewModel> Receipts { get; init; } = [];
    public IReadOnlyList<ImportReceiptSupplierOptionViewModel> Suppliers { get; init; } = [];
}

public sealed class ImportReceiptListItemViewModel
{
    public int Id { get; init; }
    public string ReceiptNumber { get; init; } = string.Empty;
    public DateTime ReceiptDate { get; init; }
    public string SupplierCode { get; init; } = string.Empty;
    public string SupplierName { get; init; } = string.Empty;
    public string CreatedByName { get; init; } = string.Empty;
    public ReceiptStatus Status { get; init; }
    public decimal TotalAmount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? PostedAt { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class ImportReceiptDetailsViewModel
{
    public int Id { get; init; }
    public string ReceiptNumber { get; init; } = string.Empty;
    public DateTime ReceiptDate { get; init; }
    public string SupplierCode { get; init; } = string.Empty;
    public string SupplierName { get; init; } = string.Empty;
    public string CreatedByName { get; init; } = string.Empty;
    public string? PostedByName { get; init; }
    public ReceiptStatus Status { get; init; }
    public string? Note { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? PostedAt { get; init; }
    public decimal TotalAmount { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<ImportReceiptDetailItemViewModel> Details { get; init; } = [];
}

public sealed class ImportReceiptDetailItemViewModel
{
    public int ProductId { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitCost { get; init; }
    public decimal LineTotal { get; init; }
}
