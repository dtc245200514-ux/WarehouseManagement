# Đánh giá nghiệm thu Task 9.1 — 25/09/2026

**Chưa đóng nghiệm thu nội dung.** Không sửa code ứng dụng hoặc database trong lượt này.

## Bằng chứng API thật do người dùng cung cấp

Người dùng chép thông tin từ ảnh PowerShell: event GeminiCallAudit 9205, Provider Gemini, Model gemini-3-flash-preview, HTTP 200, finish STOP, totalTokens 3154, success True. Ghi nhận bằng chứng này xác nhận gọi thật thành công theo thông tin người dùng; chưa trực tiếp đọc ảnh/log gốc, không coi đây là output của kiểm thử giả lập. Theo code, success chỉ true sau khi nhận nội dung hoàn chỉnh, không rỗng và qua kiểm tra giới hạn.

3154 là tổng token đầu vào/đầu ra/suy luận theo API, không phải riêng output; không thể kết luận vượt maxOutputTokens=3000 chỉ từ con số này. Chưa có phân rã usage của lần đó.

## Đối chiếu dữ liệu và diễn đạt

| Nội dung | Bằng chứng hệ thống | Điều kiện diễn đạt chính xác |
|---|---|---|
| Tổng tồn 29.375 | SQL DTO khớp dashboard/report qua kiểm thử đọc SQL mới | Tổng số học nhiều đơn vị, không phải 29.375 sản phẩm đồng nhất; không suy ra giá trị tiền |
| Nhóm đơn vị | Payload CurrentStockByUnit: Hộp10.000, Thùng10.000, cái5.000, Chai1.250, kg3.125, cuộn0 | Không đổi đơn vị hoặc đọc10.000 thành mười nghìn; nhóm đơn vị độc lập với mẫu10 hàng |
| 100% cảnh báo | ProductCount20, AlertCount20:20/20=100% | Tỷ lệ mã hàng trong phạm vi tổng hợp, không phải tỷ lệ số lượng; cảnh báo gồm tồn bằng ngưỡng, dưới ngưỡng và hết |
| Hết hàng | OutOfStockCount12 | Không nói toàn bộ20 hàng hết; nếu tính12/20=60% phải nêu rõ mẫu số |
| Chi tiết cảnh báo | LowStock tối đa10, AlertCount tổng20 | Không khẳng định đã phân tích chi tiết cả20 mã từ danh sách mẫu |
| Khoảng ngày | DTO FromUtc/ToExclusiveUtc, ngày kết thúc được đổi thành đầu ngày kế tiếp | Không lấy ngày snapshot làm ngày kết thúc; cần đối chiếu bộ lọc của chính lần gọi |

Prompt hiện có ràng buộc chống bịa số, khác đơn vị, mẫu10, không suy diễn tiền/đặt hàng/nhân quả thiếu dữ liệu. Adapter không tự xác minh ngữ nghĩa từng số trong văn bản. STOP/success chỉ chứng minh hoàn tất giao thức và kiểm tra định dạng, không bảo đảm mọi nhận xét đúng.

## Kiểm thử chạy lại

- Build Task91Checks và ứng dụng tại bin/GeminiFinalAudit:0 warning,0 error.
- Task91Checks --database:153 kiểm tra đạt (132 độc lập +21 SQL), đọc SQL thật, HTTP provider giả lập. Bốn phạm vi gồm toàn bộ lịch sử và ngày không Posted. Toàn bộ lịch sử44.125/14.750/29.375 khớp; snapshot được bộ kiểm thử ghi vào bin/Task91Evidence/analysis-input.json. Đây không phải bản lưu payload của lần Gemini thật người dùng vừa gọi.
- Build Task82Checks tại bin/GeminiFinalAudit82:0 warning,0 error;35 hồi quy độc lập đạt.

## Còn thiếu để đóng nghiệm thu

Trình duyệt Codex hiện không có tab mở; chưa nhận toàn văn báo cáo của lần event9205 và khoảng ngày tương ứng. Đã hỏi người dùng cung cấp văn bản không chứa secret. Chưa thể đánh dấu cách diễn đạt29.375,100% cảnh báo và ngày23/09–26/09 đúng/sai trong chính báo cáo đó. Cần đối chiếu báo cáo với payload cùng kỳ; nếu phát hiện lỗi thực mới sửa tối thiểu và kiểm chứng lại. Không cần gọi lại chỉ để thay thế bằng chứng lần cũ.
