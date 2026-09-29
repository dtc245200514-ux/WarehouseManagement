# Sửa diễn đạt báo cáo — 25/09/2026

Trạng thái: đã sửa prompt, build/test đạt; chờ chạy bản GeminiWordingFix từ PowerShell giữ key để tạo báo cáo thật mới. Task9.1 chưa hoàn thành. Bằng chứng API thành công trước đây không chứng minh prompt mới đã trả nội dung đúng.

## Thay đổi

- Services/Ai/InventoryAnalysisPrompt.cs: backend thêm nhãn kỳ hiển thị với ngày kết thúc bao gồm (ToExclusiveUtc trừ một tick);23/09 đến trước26/09 trở thành23/09–25/09. Có nhãn rõ cho bỏ trống cả hai ngày hoặc chỉ một đầu. Thêm thời điểm snapshot UTC và giới hạn không bao quát giao dịch sau snapshot. JSON DTO phía sau giữ nguyên, không sửa SQL hoặc giá trị số.
- Prompt bắt buộc nhập/xuất/số phiếu là Posted; không suy không có Draft. Tồn đầu/cuối theo sổ, hiện tại theo hệ thống, không khẳng định kiểm kê thực tế. Không nói toàn bộ dữ liệu chỉ Posted. Không suy không biến động giữa kỳ từ hai số đầu/cuối bằng nhau.
- Prompt phân biệt tổng số học nhiều đơn vị với số sản phẩm; dùng CurrentStockByUnit, cảnh báo AlertCount khác hết hàng OutOfStockCount, tỷ lệ mã hàng khác tỷ lệ số lượng. Chi tiết tối đa10 mã, không đại diện toàn bộ20. Không tự gọi ProductCount là số mã active. Giữ giới hạn350 từ,6 mục và các ràng buộc không bịa/không tính giá vốn/không suy lượng mua.
- tests/Task91Checks/Program.cs: thêm5 kiểm tra nhãn ngày bao gồm, snapshot/JSON nguyên vẹn, toàn lịch sử và hai trường hợp chỉ có một đầu ngày.
- docs/TASK-9.1-status.md và tài liệu này ghi điểm tiếp tục.

Adapter/Controller/View/DTO không sửa. Cơ chế chống key leak, log9205, STOP, hạn mức/token/timeout giữ nguyên. Không thay đổi database hoặc dữ liệu nghiệp vụ.

## Kiểm chứng

- Build ứng dụng và Task91Checks tại bin/GeminiWordingFix:0 warning,0 error.
- Task91Checks --database:158 kiểm tra đạt (153 cũ+5 mới), SQL đọc thật, HTTP AI giả lập.
- Build Task82Checks tại bin/GeminiWordingFix82:0 warning,0 error;35 hồi quy đạt.
- Test xác nhận đầu vào kỳ23–25/09 chứa nhãn `Kỳ báo cáo: 23/09/2026 – 25/09/2026 (bao gồm cả hai ngày, UTC)` và thời điểm snapshot cố định đúng; đây là đầu vào backend, không phải trích đoạn response Gemini mới.

## Phần chưa kiểm chứng

Chưa có báo cáo Gemini thật với prompt mới. Prompt ràng buộc không bảo đảm tuyệt đối ngữ nghĩa đầu ra; phải đọc report mới và đối chiếu số liệu chính thức. Không sửa văn bản lỗi thành “response thật” hoặc nhận báo cáo cũ làm bằng chứng mới.

Khởi động từ PowerShell giữ key/model/cổng7315: `dotnet bin/GeminiWordingFix/WarehouseManagement.dll`; nếu key bị finally xóa, nhập lại cục bộ, không gửi chat. Sau đó tạo báo cáo23–25/09, đối chiếu kỳ, snapshot, Posted, tồn29.375 nhiều đơn vị,20 cảnh báo/12 hết,giới hạn mẫu10 và log9205 của lần mới. Chỉ khi đầy đủ mới cập nhật hoàn thành.
