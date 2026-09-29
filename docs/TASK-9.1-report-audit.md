# Đối chiếu báo cáo GeminiTokenFix — 25/09/2026

Không sửa code ứng dụng, không ghi SQL hoặc thay đổi nghiệp vụ. Chưa đánh dấu hoàn thành; thiếu bản báo cáo/nhật ký thực tế của lần người dùng vừa tạo.

| Hạng mục | Đã kiểm chứng | Bằng chứng | Vấn đề | Đề xuất |
|---|---|---|---|---|
| Tiến trình cổng 7315 | Có | Process 24476 chạy GeminiTokenFix/WarehouseManagement.dll, không Task91UiHost; chỉ đọc cờ, không xuất command line | Chưa đọc cấu hình model của process | Đối chiếu cấu hình cục bộ không chứa key |
| Chọn provider | Có ở code và UI | UI Gemini; AiAnalysisService chọn GeminiAnalysisAdapter, Mock chỉ khi Provider=Mock; không fallback | Chưa có log của request cụ thể | Cần event 9200/9203 của đúng lần gọi |
| HTTP thật | Chưa xác nhận độc lập cho lần báo cáo đó | DI dùng HttpClientHandler thật, không redirect; adapter GET metadata rồi POST generateContent | Source/process không thay thế bằng chứng HTTP runtime | Đọc log thành công đã có, không dựng lại bằng mock |
| Tồn theo đơn vị | Có, SQL đọc trực tiếp | Hộp 10.000; Thùng 10.000; cái 5.000; Chai 1.250; kg 3.125; cuộn 0.000 | Không phải tất cả đều hiển thị trong bảng 10 hàng mẫu | CurrentStockByUnit được serialize nguyên vào input; các nhóm này có căn cứ |
| Tổng cảnh báo | Có, SQL trực tiếp | 20 hàng, tồn 29.375, 20 cảnh báo, 12 hết; AlertCount/OutOfStockCount riêng với LowStock.Take(10) | Không được suy ra rằng cả 20 hàng đã được phân tích chi tiết | Chỉ khẳng định tổng 20; mô tả chi tiết giới hạn mẫu 10 |
| Kỳ báo cáo | Có cho tab đang mở, chưa cho báo cáo người dùng nói tới | Tab GET fromDate=2026-09-23&toDate=2026-09-23, 0/0 nhập xuất, không có report | Tab hiện tại không có văn bản AI; chưa đối chiếu câu 23/09–26/09 | Với bộ lọc 23/09–23/09, kỳ đúng là [23/09 00:00,24/09 00:00) UTC. Nếu thực sự lọc đến26/09, upper bound phải là27/09 |
| Không bịa số | Ràng buộc prompt có, không có bảo đảm tự động | Prompt cấm bịa; adapter kiểm tra trạng thái/định dạng, không kiểm tra ngữ nghĩa từng số/ngày | STOP không chứng minh mọi nhận xét đúng | Đọc báo cáo thực trước khi đánh giá; không tuyên bố mọi số đúng chỉ vì HTTP thành công |
| Log | Cấu trúc an toàn đã kiểm tra | 9200 HTTP thành công;9203 status/finish/counts;9204 giới hạn/thinking/byte length; không body/key/auth token | Không tìm thấy file .log trong dự án; console process của người dùng chưa được cung cấp. Counts là số lượng token, không phải token xác thực | Chỉ cung cấp các dòng an toàn liên quan, không toàn bộ log hoặc header |

`10,000` nếu dùng dấu phẩy thập phân tiếng Việt là mười đơn vị; SQL lưu 10.000, không phải mười nghìn. Chưa có toàn văn báo cáo để kết luận AI đã dùng nghĩa nào.

Không tự gọi lại Gemini: yêu cầu hiện tại là kiểm tra báo cáo vừa tạo, tạo mới không chứng minh nội dung/lần gọi cũ. Đã hỏi người dùng vị trí báo cáo và các dòng log an toàn. Các kết quả build 149 service/SQL + 35 hồi quy là của lượt trước, không chạy lại vì không sửa code.
