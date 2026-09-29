# Task 9.2-C — nghiệm thu ngày 29/09/2026

**Task 9.2-C: ĐẠT trong phạm vi kiểm thử lỗi bằng transport mô phỏng được yêu cầu.**
**Task 9.2-B: CHƯA ĐẠT nghiệm thu cuối; kiểm thử nội bộ đạt, chưa đối chiếu phần đề xuất của báo cáo Gemini thật với prompt 9.2-B.** Xem TASK-9.2-B-status.md. Không dùng thành công Gemini 9.1 trước đây làm bằng chứng cho nội dung mới.

## Hiện trạng và thay đổi tối thiểu

9.2-B đã có truy vấn hàng hoạt động với `CurrentQuantity < MinimumStockLevel`, chênh lệch backend, danh sách UI đầy đủ và mẫu gửi AI tối đa 50. Không triển khai lại. Selector trong Verify-Task91.ps1 đã là `<strong>` khi bắt đầu lượt này; giữ nguyên và chạy lại, đạt.

Gemini adapter đã có timeout và một số catch, nhưng chưa có kết quả phân loại, thiếu SocketException và fallback exception ngoài dự kiến; thông báo timeout/HTTP chưa đúng yêu cầu 9.2-C. Đã bổ sung theo kiến trúc hiện có, không đổi prompt, endpoint, model, token limit, retry hoặc nghiệp vụ.

| File sửa/tạo trong lượt này | Thay đổi |
|---|---|
| Services/Ai/IAiAnalysisService.cs | Thêm AiFailureKind, tham số tùy chọn FailureKind cho result; giữ Success và cách gọi hiện tại |
| Services/Ai/GeminiAnalysisAdapter.cs | Phân loại lỗi, thông báo thân thiện, bắt socket và unexpected; không log exception/message/body; giữ caller cancellation |
| Views/AiAnalysis/Index.cshtml | Giải thích AI chưa tạo được phần phân tích, số liệu hệ thống hiển thị riêng |
| tests/Task91Checks/GeminiChecks.cs | HTTP 502, kiểm tra kind/message cho hai pha, timeout GET/POST, socket/unexpected, không lộ header trong log |
| tests/Task91Checks/ReplenishmentChecks.cs | Thêm trường hợp inactive trong bộ lọc active + BelowMinimum |
| tests/Task91UiHost/Program.cs | Thêm GeminiMatrix; chặn toàn bộ provider HTTP bằng fixture, chạy entry point/MVC thật |
| scripts/Verify-Task92C.ps1 | 17 ca HTTP qua đăng nhập/CSRF/rate limit thật; kiểm tra lỗi, số liệu, đủ dòng, HTML không rò rỉ |
| scripts/Verify-Task92B-Ui.ps1 | Đối chiếu từng ô trong danh sách đề xuất với SQL chỉ đọc |
| docs/TASK-9.2-B-status.md, docs/TASK-9.2-C-status.md | Trạng thái, bằng chứng và giới hạn |

Đã đọc, giữ nguyên trong lượt này: Program.cs; appsettings.json/Development; AiOptions; AiAnalysisService; InventoryAnalysisDataService; InventoryAnalysisPrompt; AiAnalysisController; DTO/ViewModel; Product; StockLevelRules; Verify-Task91.ps1; Verify-Task92A-State.ps1. Các thay đổi 8.x/9.x đã có trong working tree được giữ lại, không coi là sửa mới của 9.2-C.

## Xử lý lỗi và cấu hình

| Tình huống | FailureKind | Thông báo UI |
|---|---|---|
| Phản hồi STOP hợp lệ | None, Success=true | Hiển thị báo cáo tách số liệu chính thức |
| Hết thời gian | Timeout | Không thể kết nối dịch vụ AI. Vui lòng thử lại sau. |
| 401/403; 400 với API_KEY_INVALID/EXPIRED/SERVICE_BLOCKED | Authentication | Không thể kết nối dịch vụ AI. Vui lòng thử lại sau. |
| 404 | ModelUnavailable | Mô hình AI hiện không khả dụng. Vui lòng thử lại sau. |
| 429 | HttpError | Dịch vụ AI đang bị giới hạn yêu cầu. Vui lòng thử lại sau. |
| 400 thông thường, 500/502/503 | HttpError | Dịch vụ AI hiện không khả dụng. Vui lòng thử lại sau. |
| HttpRequestException/IOException/SocketException | Network | Không thể kết nối dịch vụ AI. Vui lòng thử lại sau. |
| Exception ngoài dự kiến | Unexpected | Không thể kết nối dịch vụ AI. Vui lòng thử lại sau. |
| JSON/response sai, rỗng, bị chặn hoặc MAX_TOKENS | InvalidResponse | Thông báo cố định an toàn; không hiển thị báo cáo cắt/rỗng |

`AI__TimeoutSeconds` vẫn bind section AI. Mặc định 30 giây; phạm vi hợp lệ 1–120. Linked cancellation bao phủ cả GET metadata, POST generation và đọc response; fixture dùng 1 giây để kiểm tra, không tăng timeout production. Caller chủ động hủy vẫn được truyền cancellation, không bị nuốt bởi generic catch. Không retry, không đổi provider/model. MaxOutputTokens mặc định 3000, giới hạn 4000 giữ nguyên.

Key vẫn từ môi trường/User Secrets, chỉ dùng phía server; HttpClient loggers vẫn tắt. Audit 9205 giữ provider/model/phase/status/finish/token/success; model vô tình chứa key được redact. Lỗi 9201/9202 chỉ có status/nhóm cố định, không truyền đối tượng exception vào logger. ViewModel không có secret; không thêm ViewData/ViewBag chứa cấu hình.

## Bằng chứng kiểm thử

Build ứng dụng qua ProjectReference và hai test host ở output riêng: **0 warning, 0 error**. Bản chạy ứng dụng: `bin/Task92C/WarehouseManagement.dll`. Build kiểm tra service: `bin/Task92CChecks/Task91Checks.dll`; hồi quy độc lập: `bin/Task92C82Checks/Task82Checks.dll`. Không ghi đè instance người dùng tại 7315.

| Bộ kiểm thử | PASS | FAIL | Bằng chứng |
|---|---:|---:|---|
| Task91Checks --database: AI/Gemini/9.2-A/B/C và SQL | 225 | 0 | Lệnh chạy bản Task92CChecks, exit 0, dòng tổng 225 được ghi nhận trong phiên |
| Verify-Task91 Mock, gồm selector strong/đủ 20 dòng | 26 | 0 | bin/Task92CEvidence/http-mock.log |
| Đối chiếu từng dòng UI 9.2-B với SQL | 21 | 0 | replenishment-ui-sql.log |
| GeminiMatrix HTTP/UI: 17 tình huống | 87 | 0 | http-matrix.log |
| Verify-Task91 MissingKey | 17 | 0 | http-missing-key.log |
| Verify-Task91 InvalidConfig | 17 | 0 | http-invalid-config.log |
| Task 8.1 | 90 | 0 | task81.log |
| Task 8.2 HTTP/CSV/SQL | 283 | 0 | task82-http.log |
| Task82Checks độc lập | 35 | 0 | task82-unit.log |
| Kiểm tra log host an toàn | 3 | 0 | log-safety.log |
| Hash 8 bảng nghiệp vụ trước/sau | 8 | 0 | business-before.json, business-after-check.log |
| **Tổng** | **812** | **0** | Các file không ghi đường dẫn đầy đủ trong bảng đều thuộc bin/Task92CEvidence |

GeminiMatrix gồm success, timeout, 400/401/403/404/429/500/502/503, HttpRequestException, IOException, SocketException, invalid key, unexpected exception, response rỗng và MAX_TOKENS. Tất cả đi qua controller, service, adapter, Razor thật với SQL thật; chỉ provider transport bị thay thế. Ca xen kẽ toàn lịch sử và 23–25/09/2026. Bộ kiểm thử giữ rate limit 5 POST/phút và chờ giữa nhóm, không nới policy.

Host log có **17 GeminiCallAudit: 1 success mô phỏng, 16 failure**. Provider Gemini, model `test-model` là fixture. Không chứa marker key fixture, tên header nhạy cảm, body lỗi hoặc nội dung báo cáo fixture. HTML không chứa key fixture, raw body, exception/stack trace. Các kiểm tra này là bằng chứng trên fixture có chủ đích, không tuyên bố bảo đảm tuyệt đối với mọi chuỗi đầu vào.

## Nghiệp vụ và dữ liệu không đổi

Các cạnh `<`, `=`, `>`, 0/ngưỡng dương, 0/0 và inactive đã được kiểm tra bằng fixture bộ nhớ; không chèn fixture vào SQL. Query thật đối chiếu 20 mã đang hoạt động dưới ngưỡng, 12 mã hết trong tập này. Điều kiện cảnh báo cũ `<=` giữ nguyên. MinimumShortfall là chênh lệch, không phải quyết định lượng đặt hàng. UI đủ 20 dòng; fixture 60 dòng chứng minh DTO giữ đủ danh sách UI và chỉ serialize mẫu 50.

Tổng toàn lịch sử: nhập 44.125, xuất 14.750, tồn hiện tại 29.375. Kỳ không Posted: nhập/xuất 0.000, tồn hiện tại 29.375. Đây là tổng số học nhiều đơn vị, không phải một lượng vật lý đồng nhất.

Hash khớp cả 8 bảng: Products (bao gồm CurrentQuantity, MinimumStockLevel), Categories, Suppliers, ImportReceipts, ImportReceiptDetails, ExportReceipts, ExportReceiptDetails, InventoryTransactions. Luồng AI chỉ đọc; không SaveChanges, tạo phiếu hoặc ghi sổ. Không migration/schema/quyền mới. Đăng nhập sử dụng tài khoản đã có, các seed tắt. Lần đọc SQL đầu trong sandbox lỗi SSPI; chạy lại bằng Windows authentication ngoài sandbox đã thành công. Đây là lỗi môi trường đã giải quyết, không phải test nghiệp vụ còn FAIL.

## Giới hạn và bước nghiệm thu còn lại

- Không có key trong process/User Secrets ở lượt này (chỉ kiểm tra sự hiện diện, không in giá trị); **chưa gọi Gemini thật với bản 9.2-B/C**. Chất lượng diễn giải phần đề xuất chưa được nghiệm thu. Không lấy fixture success thay bằng bằng chứng Gemini thật.
- Giao diện được kiểm tra qua HTTP/Razor sau đăng nhập Accountant; quyền Staff/anonymous/CSRF/rate limit được hồi quy. Không kiểm tra lại đăng nhập ADMIN hoặc bố cục bằng trình duyệt trực quan trong lượt này; không đổi mật khẩu/quyền.
- Các case inactive, bằng/vượt ngưỡng, danh sách >50 và rỗng dùng fixture bộ nhớ vì dữ liệu thật không có đủ các trạng thái đó; không sửa kho để tạo tình huống.
- Các host kiểm thử riêng 7323–7326 đã được dừng sau kiểm thử. Instance 7315 của người dùng không bị can thiệp.
- Task 9.2-C cho phép mô phỏng lỗi nên đạt phạm vi đó. Task 9.2-B giữ CHƯA ĐẠT nghiệm thu cuối để chờ đối chiếu báo cáo Gemini thật; không có test nội bộ còn FAIL.
