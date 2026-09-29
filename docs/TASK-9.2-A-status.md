# Task 9.2-A — điểm tiếp tục 27/09/2026

**Chưa hoàn thành.** Đã triển khai và kiểm thử; chưa có báo cáo Gemini thật với prompt9.2-A được đối chiếu. Không triển khai lại.

## Code hiện có

- Models/AiAnalysis/AiAnalysisModels.cs: thuộc tính tính sẵn NetStockChange, HasPostedMovements, NetPostedChange trên DTO hiện có; không entity/migration.
- Services/Ai/InventoryAnalysisPrompt.cs: mục đầu tóm tắt biến động; dùng số backend, phân biệt kỳ không Posted với biến động khác; tối đa3 mã đáng chú ý từ mẫu10; không so sánh kỳ trước khi không có dữ liệu, không đề xuất nhập hàng.
- Services/Ai/InventoryAnalysisDataService.cs: chỉ cập nhật câu giới hạn dữ liệu; truy vấn và tổng nghiệp vụ giữ nguyên.
- Services/Ai/AiAnalysisService.cs: Mock có tóm tắt được gắn nhãn giả lập.
- Views/AiAnalysis/Index.cshtml: chênh lệch tồn chính thức và nhãn tóm tắt gần báo cáo; giữ báo cáo6mục và số liệu chính thức.
- tests/Task91Checks/MovementChecks.cs (mới), Program.cs: kiểm tra chênh lệch dương/âm/0, kỳ có/không Posted, dữ liệu khác kỳ/toàn lịch sử, một mã có biến động và payload SQL đến Gemini giả lập.
- scripts/Verify-Task91.ps1: thêm2 kiểm tra hiển thị tóm tắt và kỳ không giao dịch; không xóa test cũ.
- scripts/Verify-Task92A-State.ps1 (mới): hash8 bảng nghiệp vụ trước/sau, chỉ đọcSQL, không lưu nội dung các dòng.

Controller/interface/adapterGemini/cấu hìnhkey/quyền/schema không đổi trong9.2-A. Dữ liệu từ InventoryTransactions liên kết phiếu Posted theo kỳUTC; tồn đầu/cuối từ sổ, hiện tại từProducts. Dùng cùng request/report9.1, không thêm requestAI thứ hai.

## Bằng chứng đã thực hiện trong lượt triển khai trước

| Kiểm tra | Kết quả |
|---|---|
| Build nền trước sửa |0warning/0error|
| Build Task92A và Task91Checks |0warning/0error|
| Service/SQL/Gemini giả lập |179PASS|
| HTTP Mock cổng7318 |24PASS|
| HTTP thiếu Gemini key cổng7319 |17PASS|
| HTTP cấu hình sai cổng7320 |17PASS|
| Task82Checks độc lập |35PASS,build0warning/0error|
| Verify-Task82 HTTP/CSV/SQL |283PASS|
| Verify-Task81 |Đã chạy trong lệnh nối tiếp,không thấy lỗi;output tổng bị cắt,chưa ghi nhận chắc số tổng lượt này|
| Hash8 bảng nghiệp vụ |8PASS,khớp trước/sau|
| git diff --check |Đạt,chỉ cảnh báoLF/CRLF|

Tổng có số đếm xác nhận:563checkPASS,0FAIL ghi nhận;không cộng Task81 khi chưa lấy được dòng tổng. Không coi giả lập là bằng chứng chất lượngAI thật.

Hash khớp:Products (bao gồm CurrentQuantity),Categories,Suppliers,ImportReceipts,ImportReceiptDetails,ExportReceipts,ExportReceiptDetails,InventoryTransactions. Không tạo phiếu,không ghi kho,không migration. Bằng chứng hash trước ởbin/Task92AEvidence/business-before.json;không ghi đè baseline này khi tiếp tục đối chiếu cùng lượt.

Trang được kiểm tra HTTP với Accountant;Staff bị chặn theo ViewReports;anonymous chuyển đăng nhập;CSRF/rate limit/giả mạo số liệu vẫn bị chặn. Browser mở7318 tới trang đăng nhập,chưa quan sát trực quan tóm tắt sau đăng nhập. ADMIN chưa kiểm tra lại. Instance kiểm thử trước:7318Mock,7319thiếu key,7320cấu hình sai;cần kiểm tra còn chạy không trước tái sử dụng/dừng.

## Điểm bị dừng và việc tiếp theo

Lệnh cuối định đọc lại tổng Task81/mẫu biến động và kiểm tra sự hiện diện key bị hệ thống duyệt tự động chặn vì hết giới hạn sử dụng. Lệnh không thực hiện;không phải testFAIL hoặc kết luận không an toàn. Không vượt cơ chế duyệt.

1. Khi môi trường cho phép,xác nhận tổng hồi quyTask81 nếu cần và tình trạng instance.
2. Chạy bảnbin/Task92A/WarehouseManagement.dll từ PowerShell người dùng đang giữ cấu hìnhGemini/key,cổng7315,seed tắt. Không gửi key vào chat.
3. Tạo tóm tắt cho kỳ cóPosted/kỳ khôngPosted/kỳ khác/toàn lịch sử;đối chiếu một mã biến động về số và đơn vị. Kiểm tra không đề xuất nhập hàng,không bịa xu hướng,nội dungAI rõ ràng tách với số chính thức. Bằng chứngAPI9.1 cũ không thay thế việc này.
4. Lưu kết quảUI/nội dung/logan toàn9205,sau đó mới đánh giá hoàn thành. Nếu chưa có môi trườngGemini thật,ghi rõ giới hạn,không tuyên bố chất lượng diễn giải đã được xác minh.

Lượt kiểm tra tiếp tục này chỉ đọc lạicode/buildartifact và tạo tài liệu trạng thái;không chạy lạiSQL/API/test,không sửa chức năng.
