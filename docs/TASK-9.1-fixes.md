# Task 9.1 — sửa lỗi và kiểm chứng bổ sung 24/09/2026

## A. Trạng thái

**Hoàn thành một phần; chưa đủ bằng chứng nghiệm thu toàn bộ.** Tiếp tục từ implementation hiện có, không viết lại AI, không đổi provider/policy/schema. Giai đoạn hiện tại là kiểm thử bổ sung và báo cáo; API thật còn thiếu cấu hình key/model.

Đã đọc trạng thái, hướng dẫn cấu hình, service/adapter (adapter nằm trong AiAnalysisService), controller AI và Reports, DTO/options, DI, view/JS, entity/DbContext, logic ghi sổ và các test/script. Báo cáo trước chỉ kiểm tra số giới hạn ngoài phạm vi, chưa thử giá trị cấu hình không chuyển được sang số; lượt này đã tái hiện và sửa lỗi đó.

## B. Lỗi và phần bổ sung

| Nội dung | Nguyên nhân | File / cách sửa | Bằng chứng |
|---|---|---|---|
| Cấu hình sai kiểu làm trang AI có thể trả 500 | options.Value bind TimeoutSeconds/MaxOutputTokens trước try/catch; chữ không chuyển được sang int | AiAnalysisService.cs bắt lỗi bind/validate options, trả thông báo cố định, không log exception hoặc giá trị gốc | Test trước sửa FAIL với TimeoutSeconds; sau sửa cả hai trường đạt; 17 HTTP InvalidConfig đạt; UI ADMIN vẫn có số liệu và cảnh báo |
| Thiếu log chẩn đoán AI an toàn theo yêu cầu bổ sung | Các nhánh lỗi chỉ trả chuỗi cho UI | Inject ILogger; event 9100 lỗi cấu hình, 9101 mã nhóm/http status; không log key, prompt, raw response hoặc exception | Kiểm tra log bắt trong bộ nhớ đạt; runtime chỉ ghi http_503 / empty_response |
| Chuỗi key chứa ký tự điều khiển/không phải ASCII chưa bị từ chối ngay | Kiểm tra trước chỉ loại whitespace | Giới hạn ký tự key về ASCII hiển thị không có khoảng trắng trước khi tạo header | Hai test NUL và Unicode đạt, handler không nhận request. Đây là bổ sung kiểm tra cấu hình, không phải bằng chứng key thật hợp lệ |
| Prompt chưa nói rõ giới hạn kết luận nguyên nhân | Quy tắc cũ chỉ cấm bịa số/xu hướng/dự báo | InventoryAnalysisPrompt.cs bổ sung không khẳng định nguyên nhân thiếu bằng chứng, phân biệt dưới/bằng ngưỡng, đề xuất kiểm tra/bổ sung không tự đặt số lượng | Đã rà soát prompt và test payload; chất lượng tuân thủ của AI thật vẫn chưa kiểm chứng |

Không thay đổi controller, view, dữ liệu hoặc thuật toán tính tồn trong lượt sửa. Không migration. Một build trung gian lỗi MSB3026/3027 vì output đang bị tiến trình test giữ; đã dừng tiến trình test và build thư mục riêng. Đây không phải lỗi biên dịch. Build sạch sau đó đạt 0 warning/0 error.

## C. Bảng triển khai và kiểm chứng hiện tại

| Hạng mục | Đã triển khai | Đã kiểm chứng | Còn thiếu | Bằng chứng |
|---|---|---|---|---|
| Service/DI/adapter | Có | HTTP Mock, fake transport và MVC thật | API thật | 82 checks, 22 HTTP Mock, UI test host |
| Key/cấu hình | Environment/User Secrets | Thiếu key, sai kiểu/sai phạm vi/ký tự key; không lộ log/UI | Key/model được cấp quyền | 17 MissingKey + 17 InvalidConfig; 61 service checks |
| Prompt | Có, tiếng Việt | Payload đúng DTO; quy tắc không bịa/không ghi kho | Chất lượng model thật | Rà soát prompt + test captured request |
| Số liệu nhập | Có | 44.125, Posted theo OccurredAt UTC | Không có chênh lệch trên dữ liệu hiện tại | SQL + Reports + DTO |
| Số liệu xuất | Có | 14.750, sổ ghi -14.750 | Như trên | SQL + Reports + DTO |
| Số liệu tồn | Có | 29.375, 20 hàng đều khớp sổ | Cancelled/điều chỉnh/DB rỗng thực tế | SQL StockMismatch=0 |
| Lỗi API | Có | 400/401/403/404/429/500/503/timeout/mất kết nối/JSON/rỗng mô phỏng | Lỗi thực tế từ OpenAI | Fake handler; UI 503 và response rỗng |
| Phân quyền/CSRF | Giữ ViewReports | Anonymous/staff bị chặn; accountant được phép; ADMIN tạo request UI; CSRF 400 | Không chạy lại nhập mật khẩu ADMIN từ đầu | Script + phiên admin có sẵn |
| Giao diện | Có | Mock, ngày, số liệu riêng, thiếu/sai cấu hình, provider lỗi/rỗng | Loading chưa quan sát tin cậy; chưa mọi viewport | AX/screenshot của trình duyệt |
| Chống gọi lặp | JS và 5 POST/phút/user/process | Lần thứ sáu 429; JS độc lập đã kiểm tra | Không phải trần chi phí hoặc chống lặp liên instance | HTTP test và JS của lượt audit trước |
| Build | Có | Thành công 0 warning/0 error | Không | Build test/service và UI host |

Các số ghi dấu chấm trong bảng là thập phân; tương ứng 44,125 / 14,750 / 29,375 trong yêu cầu.

### Các lượt chạy sau sửa

- `Task91Checks`: **61** kiểm tra service/config/log, toàn bộ provider bị chặn trong bộ nhớ.
- `Task91Checks --database`: **82** đạt (61 + 21 đối chiếu DB), không phải 82 kiểm tra API thật.
- `Verify-Task91.ps1 -BaseUrl http://127.0.0.1:7308 -Mode Mock`: **22** đạt.
- `Verify-Task91.ps1 -BaseUrl http://127.0.0.1:7309 -Mode InvalidConfig`: **17** đạt khi TimeoutSeconds là chữ.
- Khởi động lại 7309 với OpenAI thiếu key, `-Mode MissingKey`: **17** đạt.
- Hồi quy `Verify-Task81.ps1` và `Verify-Task82.ps1` trên 7308: **90** và **283** đạt; snapshot sản phẩm trước/sau không đổi.
- Build `bin/Task91FixVerified/` và `bin/Task91UiVerified/`: **0 warning, 0 error**. Bộ kiểm thử là console executable, cần chạy DLL; không coi `dotnet test` không tìm được test là bằng chứng.
- Build xác nhận cuối `bin/Task91FixFinal/`: **0 warning, 0 error**; git diff --check không có lỗi whitespace. Sau kiểm thử đã dừng host lỗi/thiếu key; trình duyệt trở về bản Mock 7308 và ADMIN tạo báo cáo thành công.

### Giao diện lỗi với ứng dụng thật, transport giả

Thêm `tests/Task91UiHost` để chạy entry point thật của WarehouseManagement và thay HttpClientFactory transport bằng handler trong bộ nhớ. Không đổi route/controller/view/provider nghiệp vụ. Host ép Development, tắt seed tài khoản, dùng key giả ghi rõ là fixture và bắt mọi request factory; handler không có đường gọi mạng. Không dùng host này để vận hành ứng dụng.

Đã mở cổng 7310 bằng phiên ADMIN hiện có, bấm tạo: handler trả 503 sau 5 giây; UI báo “Dịch vụ AI tạm thời không khả dụng. Vui lòng thử lại sau.”, không hiện raw body/key fixture và vẫn giữ tổng số liệu. Runtime ghi `INTERCEPTED Responses request in memory` và event 9101 `http_503`. Sau khi khởi động lại với Empty, UI báo “AI trả về nội dung rỗng.”, không có báo cáo diễn giải giả; số liệu vẫn giữ nguyên.

Đã thử quan sát loading trong request chậm, nhưng thao tác click chờ điều hướng và một lần quan sát DOM bị timeout của công cụ; chưa có bằng chứng trực quan đáng tin cậy. Không kết luận đây là lỗi JS chỉ từ giới hạn quan sát. Script JS được phục vụ HTTP 200, console không ghi lỗi; 4 kiểm tra JS độc lập trước đó đạt. Không sửa giao diện để che phần chưa kiểm chứng.

## D. Đối chiếu SQL

Trước/sau: 20 sản phẩm; 9 giao dịch nhập tổng 44.125; 7 giao dịch xuất tổng -14.750; tồn 29.375. 20 hàng hoạt động có tồn **dưới** ngưỡng, 0 hàng bằng ngưỡng, 12 hàng hết; không có sai lệch tồn với SUM(sổ).

Điều kiện lọc giữ nguyên: Import/Export liên kết phiếu Posted, ngày theo OccurredAt UTC từ đầu ngày đến trước ngày kế tiếp; Draft không góp nhập/xuất. Tồn đầu/cuối từ toàn bộ sổ, biến động khác tách riêng, tồn hiện tại từ Products. DTO thực tế trong bốn phạm vi ngày khớp báo cáo 8.1 và request bắt bằng fake handler. Chi tiết đối chiếu phiếu/sổ độc lập ở TASK-9.1-audit.md còn áp dụng; lượt sửa không ghi sổ hay tạo dữ liệu.

## E. Kết luận và file thay đổi

Sửa `Services/Ai/AiAnalysisService.cs`, `Services/Ai/InventoryAnalysisPrompt.cs`, `tests/Task91Checks/Program.cs`, `scripts/Verify-Task91.ps1`; thêm `tests/Task91UiHost/Task91UiHost.csproj`, `Program.cs`; cập nhật tài liệu cấu hình/trạng thái và bản báo cáo này.

Chưa có AI key/model trong User Secrets hoặc biến môi trường process khi kiểm tra, không gọi OpenAI thật. Cần cấu hình riêng trên máy chủ rồi kiểm tra một request nhỏ và chất lượng kết quả; không gửi key qua chat. Còn xác minh loading trực quan, các nghiệp vụ Cancelled/điều chỉnh/DB trống/concurrent trong môi trường phù hợp. ADMIN đã chứng minh quyền của phiên hiện có, chưa thực hiện lại bước nhập mật khẩu. Không mở rộng quyền, không sửa dữ liệu kho, không thêm Task 9.2 hoặc dự báo/đặt hàng.
