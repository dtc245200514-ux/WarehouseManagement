# Task 9.2-B — kiểm chứng ngày 30/09/2026

Trạng thái: triển khai và kiểm thử nội bộ đạt; CHƯA nghiệm thu Gemini thật.

## Kiến trúc đã kiểm tra và tái sử dụng

- Task 9.1/9.2-A/B dùng `AiAnalysisController`, `AiAnalysisViewModel`, `InventoryAnalysisData`, `InventoryAnalysisDataService`, `InventoryAnalysisPrompt`, `AiAnalysisService`, `Views/AiAnalysis/Index.cshtml`.
- Gemini tiếp tục đi qua `GeminiAnalysisAdapter` và `GeminiResponseValidator`; không đổi adapter, model, cấu hình, DI hoặc controller.
- SQL Server qua EF Core `ApplicationDbContext`, `AsNoTracking`, transaction Serializable chỉ đọc. Quy tắc Posted tái sử dụng `StockReportQueries.PostedInPeriod`.
- Tên trường thực tế: Code = ProductCode, Name = ProductName, MinimumStockLevel = MinimumQuantity. Không đổi schema.

## Bổ sung

- Ứng viên vẫn chỉ gồm hàng hoạt động có CurrentQuantity < MinimumStockLevel; không đổi cảnh báo <= của Task 9.1.
- Backend tính RecentExportedQuantity bằng tổng xuất Posted trong [SnapshotAtUtc − 30 ngày, SnapshotAtUtc), không phụ thuộc bộ lọc ngày báo cáo. Draft/Cancelled, giao dịch cũ và giao dịch ở cận cuối bị loại.
- Prompt có nguyên văn chỉ dẫn hệ thống được yêu cầu, dữ liệu tồn/ngưỡng/đơn vị/xuất gần đây và yêu cầu giải thích ngắn gọn. Không đặt lượng nhập; MinimumShortfall vẫn chỉ là chênh lệch tham khảo.
- Khu vực “AI gợi ý nhập hàng” có bảng đầy đủ mã/tên/đơn vị/tồn/ngưỡng/chênh lệch/xuất 30 ngày và trạng thái hết hàng hoặc còn tồn dưới ngưỡng. Nhận xét AI mở ngay trong khu vực này; báo cáo 7 mục vẫn được giữ nguyên.
- Nhận xét vẫn là văn bản của báo cáo hiện tại, tối đa 3 mã tiêu biểu trên mẫu tối đa 50; không cam kết mỗi dòng đều có nhận xét AI riêng. UI vẫn hiển thị toàn bộ ứng viên.
- Không thêm công cụ truy vấn SQL cho Gemini, không thêm thao tác ghi kho/tạo phiếu, không thêm API key vào code hoặc ViewModel.
- Giữ các xử lý lỗi sẵn có; tests xác minh thiếu key, timeout, HTTP/rate limit, nội dung rỗng/không hoàn chỉnh và lỗi định dạng.

## File sửa trong lượt này

1. Models/AiAnalysis/AiAnalysisModels.cs
2. Services/Ai/InventoryAnalysisDataService.cs
3. Services/Ai/InventoryAnalysisPrompt.cs
4. Services/Ai/AiAnalysisService.cs
5. Views/AiAnalysis/Index.cshtml (giữ các thay đổi giao diện đã có trước lượt này)
6. tests/Task91Checks/ReplenishmentChecks.cs
7. tests/Task91Checks/Program.cs
8. scripts/Verify-Task92B-Ui.ps1
9. docs/TASK-9.2-B-status.md (liên kết kết quả mới)
10. Tài liệu này.

Các thay đổi khác đang có trong working tree thuộc trước lượt này, không phải phần triển khai này.

## Bằng chứng cuối

| Bộ kiểm tra | Pass | Fail |
|---|---:|---:|
| Task91Checks --database: AI/Gemini mock, 9.2-A/B, bảo mật, SQL và báo cáo | 391 | 0 |
| Task82Checks: quy tắc tồn kho/CSV/quyền | 35 | 0 |
| Verify-Task92B-Ui: SQL/UI, POST tạo Mock, trang báo cáo/tồn/hàng hóa/nhập/xuất | 29 | 0 |
| Hash 8 bảng nghiệp vụ trước/sau | 8 | 0 |
| Tổng lượt cuối (không cộng lặp 260 checks trong bộ nhớ) | 463 | 0 |

- Build toàn bộ WarehouseManagement.csproj sang bin/Task92BCurrentApp: 0 warning, 0 error. Build hai test project cũng thành công.
- Build mặc định ban đầu không copy được executable do instance cũ đang giữ file; dùng thư mục output riêng, không dừng instance của người dùng.
- Một assertion Mock ở lượt đầu không đạt; full Rebuild rồi chạy lại đạt. Assertion SQL cố định dữ liệu ngày trước cũng không đạt; đã thay bằng truy vấn độc lập dữ liệu hiện tại, vẫn giữ đối chiếu dashboard/báo cáo và dữ liệu xuất từng mã.
- 19 mã dưới ngưỡng tại lần kiểm thử; UI khớp toàn bộ tập SQL. Không dùng lại con số 20 mã trong bằng chứng cũ.
- Hash SHA-256 khớp Products, ImportReceipts, ImportReceiptDetails, ExportReceipts, ExportReceiptDetails, InventoryTransactions, Categories, Suppliers. Chứng minh không đổi dữ liệu tồn, không tạo phiếu nhập, không ghi sổ trong phạm vi các lần kiểm thử này.
- Bằng chứng lưu ở bin/Task92BCurrentEvidence/: build.log, check-build.log, sql-checks.log, task82.log, ui.log, business-before.json, state-after.log. Hash chỉ lưu băm, không lưu nội dung bảng.
- Hồi quy HTTP xác minh trang danh sách/form hàng hóa, nhập/xuất và trang tồn/báo cáo hoạt động; không ghi sổ phiếu thử vào CSDL thật. Chưa kiểm tra bố cục trực quan qua trình duyệt hoặc luồng ghi sổ end-to-end mới.

## Gemini thật và điều kiện nghiệm thu

Không có API key trong process, User Secrets hoặc hai appsettings hiện tại. Không gọi Gemini thật; mọi Gemini request của tests đều là transport mock. Không thay AI__Model, không yêu cầu gửi secret vào chat.

Còn cần một lần gọi Gemini với cấu hình máy chủ thật và đối chiếu nội dung mục 7 với dữ liệu backend (mã, đơn vị, tồn, ngưỡng, xuất 30 ngày, không bịa lượng đặt hàng/khẳng định đã đặt hàng). Chỉ sau khi có bằng chứng này mới xét đánh dấu hoàn thành nghiệm thu. Không dùng kết quả Gemini Task 9.1 cũ thay thế.
