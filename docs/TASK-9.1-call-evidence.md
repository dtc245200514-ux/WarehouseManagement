# Bằng chứng lần gọi Gemini — 25/09/2026

Chưa tìm thấy file .log trong dự án. Log console của tiến trình người dùng chưa được cung cấp; không khẳng định rằng tiến trình chưa ghi log. Code cũ có event 9200 thành công và 9203 finish/usage nhưng thiếu model và kết quả tổng hợp gắn với lần gọi.

Sửa Services/Ai/GeminiAnalysisAdapter.cs: thêm event 9205 GeminiCallAudit, một dòng trong finally cho mỗi lần đi vào gọi provider, gồm CallId ngẫu nhiên, Provider=Gemini, Model lấy từ đúng cấu hình tạo URL request, phase model/generate, HTTP status nếu có, finishReason theo whitelist, totalTokenCount nếu có và success. Chỉ success=true sau tất cả kiểm tra STOP/nội dung/giới hạn; HTTP 200 không tự được coi là thành công. Timeout/network/cancellation có success=false; status có thể trống nếu chưa nhận phản hồi. Metadata HTTP 200 có phase=model, không được nhầm là generate thành công.

Không log key, header xác thực, request/response body hoặc exception. Model được kiểm tra cú pháp trước; nếu chứa key thì thay bằng REDACTED. TotalTokens là số lượng token, không phải token xác thực. Lỗi cấu hình/dữ liệu trước bước gọi vẫn dùng event lỗi sẵn có, không tạo bằng chứng gọi API.

tests/Task91Checks/GeminiChecks.cs thêm kiểm tra model/status/success, MAX_TOKENS thất bại, tổng token dạng số và che key bị đặt nhầm trong model. HTTP kiểm thử giả lập không phải bằng chứng Gemini thật. Không thay đổi database, prompt, nghiệp vụ hoặc quyền.

Model runtime của lần gọi cũ chưa xác minh độc lập; gemini-3-flash-preview là model người dùng dự kiến. Trạng thái Task 9.1 vẫn chưa hoàn thành.

Build ứng dụng/Task91Checks tại bin/GeminiAuditLog: 0 warning, 0 error. 132 kiểm tra độc lập đạt (128 cũ + 4 audit mới), mọi HTTP provider giả lập. Không chạy SQL hoặc API thật trong lượt bổ sung log.

## Thu bằng chứng tiếp theo

Sau khi dừng app, chạy bản mới từ PowerShell giữ key và cấu hình cổng7315:

```powershell
Set-Location 'D:\KHOAI\WarehouseManagement'
dotnet bin/GeminiAuditLog/WarehouseManagement.dll
```

Nếu finally của lệnh trước xóa key, nhập lại cục bộ bằng Read-Host -AsSecureString theo AI-configuration.md, không đưa key vào lịch sử/chat. Tạo một báo cáo và đối chiếu dòng GeminiCallAudit [9205] với báo cáo hiển thị. Chỉ cung cấp dòng audit, không toàn bộ log/headers. Không tự thêm cơ chế lưu toàn bộ log ra file.
