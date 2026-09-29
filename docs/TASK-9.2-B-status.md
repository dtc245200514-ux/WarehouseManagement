# Task 9.2-B — trạng thái ngày 29/09/2026

**Task 9.2-B: CHƯA ĐẠT nghiệm thu cuối.** Chức năng và kiểm thử nội bộ đã đạt; còn kiểm chứng báo cáo đề xuất Gemini thật. Không triển khai lại.

## Đã xác nhận

- Field thật là `MinimumStockLevel`; query chỉ hàng IsActive với `CurrentQuantity < MinimumStockLevel`. Cảnh báo hiện có vẫn dùng `<=`.
- Backend tính MinimumShortfall = max(0, MinimumStockLevel − CurrentQuantity), không cập nhật Product hoặc tạo phiếu. Đây là chênh lệch tham khảo, không phải lượng phải nhập.
- UI đầy đủ 20 mã phù hợp và 12 mã hết trong tập đề xuất; đối chiếu cả mã/tên/đơn vị/tồn/ngưỡng/chênh lệch với SQL đạt 21 checks. AI nhận tối đa 50 mã, không làm mất danh sách đầy đủ trên UI. Fixture 60 mã xác nhận giới hạn mẫu.
- Các cạnh dưới/bằng/trên ngưỡng, 0/ngưỡng dương, 0/0 và inactive đạt; không chèn dữ liệu giả vào SQL.
- Test đọc count từ `<strong data-metric="below-minimum">` đã được sửa từ lượt trước. Lượt này chạy lại Verify-Task91 Mock: **26 PASS**, gồm count 20 và đủ 20 dòng. Không thay UI để chiều selector cũ, không bỏ test.
- Prompt và service 9.2-B giữ nguyên trong lượt 9.2-C. Mục 7 chỉ đề xuất xem xét, không quyết định lượng nhập, không tự đặt hàng. Nội dung AI vẫn là tham khảo và cần đối chiếu thực tế.
- Build 0 warning/0 error; tổng bộ kiểm thử kết hợp B/C và hồi quy: **812 PASS, 0 FAIL**. Chi tiết và đường dẫn bằng chứng trong TASK-9.2-C-status.md.
- Hash 8 bảng nghiệp vụ khớp trước/sau toàn bộ kiểm thử: không sửa tồn/ngưỡng, không tạo phiếu, không ghi sổ. Không migration hoặc sửa quyền.

## Phần còn thiếu

Môi trường kiểm thử không có Gemini key trong process hoặc User Secrets. Các request Gemini trong bộ kiểm thử đều bị intercept trong bộ nhớ. Chưa có báo cáo Gemini thật với prompt đề xuất hiện tại được đối chiếu từng mã, đơn vị, tồn, ngưỡng và chênh lệch. Bằng chứng API 9.1 trước đây và xác nhận 9.2-A của người dùng không thay thế bằng chứng này.

Không có kiểm thử nội bộ đang FAIL. Giới hạn bổ sung: chưa quan sát trực quan bố cục sau đăng nhập ADMIN; HTTP dùng Accountant có quyền ViewReports, không đổi quyền để kiểm thử.

## Cách tiếp tục với key đã cấu hình trên máy người dùng

Dùng PowerShell đang giữ key; không gửi key vào chat hoặc log. Nếu instance cũ đang chiếm 7315, dừng instance đó trong cửa sổ của nó trước. Bản Task92C bao gồm cả 9.2-B và xử lý lỗi mới:

```powershell
Set-Location D:\KHOAI\WarehouseManagement
$env:ASPNETCORE_URLS='http://127.0.0.1:7315'
$env:AI__Provider='Gemini'
$env:AI__Model='gemini-3-flash-preview'
$env:AI__GeminiBaseUrl='https://generativelanguage.googleapis.com/v1beta/'
$env:AdminSeed__Enabled='false'
$env:TestAccountSeed__Enabled='false'
dotnet bin/Task92C/WarehouseManagement.dll
```

Giữ `AI__ApiKey` đã cấu hình ở server; lệnh trên không nhập/in key. Tạo một báo cáo trên /AiAnalysis, đối chiếu mục 7 với bảng chính thức: chỉ mã dưới ngưỡng, không suy diễn đã nhập/đặt hàng, không gọi chênh lệch là lượng phải nhập, nêu đúng giới hạn mẫu. Ghi nhận audit 9205 HTTP 200/STOP/success True và nội dung đã đối chiếu; nếu bị lỗi, dùng mã/status an toàn, không thu thập body hoặc header nhạy cảm. Sau đó mới xét hoàn thành 9.2-B.
