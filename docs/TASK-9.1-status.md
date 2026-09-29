# Task 9.1 — trạng thái 2026-09-24

## Mới nhất — đã sửa prompt, chờ báo cáo thật sau sửa

Đã thêm nhãn ngày bao gồm/snapshot từ backend, làm rõ Posted,tồn theo hệ thống,nhiều đơn vị,cảnh báo/hết hàng và mẫu10. Build sạch;158 service/SQL+35 hồi quy đạt. Bản bin/GeminiWordingFix cần khởi động từ PowerShell giữ key để kiểm chứng nội dung mới. **Chưa hoàn thành**. Xem [TASK-9.1-wording-fix.md](TASK-9.1-wording-fix.md).

## Mới nhất — đã nhận toàn văn, chưa đạt nội dung

Đã đối chiếu báo cáo người dùng cung cấp với SQL đúng kỳ23–25/09. Số liệu khớp nhưng tiêu đề dùng sai cận ngày26/09, câu giới hạn toàn bộ dữ liệu ở Posted quá rộng và “tồn thực tế” chưa có kiểm kê chứng minh. **Task9.1 chưa hoàn thành.** Build mới0 warning/0 error; không sửa code/SQL. Chi tiết:[TASK-9.1-content-review.md](TASK-9.1-content-review.md). Các đánh giá thiếu toàn văn bên dưới là lịch sử.

## Đánh giá mới nhất — đã có bằng chứng API từ người dùng

Người dùng cung cấp event9205:Gemini/gemini-3-flash-preview,HTTP200,STOP,totalTokens3154,successTrue. Ghi nhận API thành công theo bằng chứng được cung cấp. Build mới sạch;153 service/SQL và35 hồi quy đạt. **Chưa đóng nghiệm thu nội dung** vì chưa có toàn văn báo cáo/cùng kỳ để kiểm tra đơn vị,29.375,100% cảnh báo và khoảng ngày. Xem [TASK-9.1-final-assessment.md](TASK-9.1-final-assessment.md). Các phần dưới là lịch sử.

## Mới nhất — bổ sung bằng chứng mỗi lần gọi

Không tìm thấy file log để chứng minh lần gọi người dùng vừa tạo. Đã bổ sung GeminiCallAudit event9205 (provider/model/status/finish/totalTokens/success), build sạch, 132 kiểm tra độc lập đạt. Bản bin/GeminiAuditLog cần được khởi động để thu bằng chứng thật; **chưa hoàn thành**. Xem [TASK-9.1-call-evidence.md](TASK-9.1-call-evidence.md).

## Mới nhất — bản sửa token 25/09/2026

MAX_TOKENS đã được người dùng xác nhận. Đã sửa mặc định 3000 token (trần 4000), thinking minimal riêng gemini-3-flash-preview, báo cáo 350 từ; build sạch và 149 kiểm tra service/SQL đạt. Chờ khởi động `bin/GeminiTokenFix/` từ PowerShell giữ key để gọi Gemini thật sau sửa. **Chưa hoàn thành.** Xem [TASK-9.1-gemini-token-fix.md](TASK-9.1-gemini-token-fix.md); các phần dưới là lịch sử.

## 25/09/2026 — đang chẩn đoán Gemini chưa hoàn chỉnh

Đã bổ sung phân loại finishReason và token diagnostics an toàn, 146 kiểm tra service/SQL/HTTP giả lập + 35 hồi quy độc lập đạt, build 0 warning/0 error. Browser cổng 7315 xác nhận toàn bộ lịch sử và kỳ không Posted hiển thị đúng. Chờ bản chẩn đoán khởi động từ môi trường giữ key để kiểm tra phản hồi Gemini thật; **chưa hoàn thành**. Chi tiết: [TASK-9.1-gemini-diagnostics.md](TASK-9.1-gemini-diagnostics.md).

## Cập nhật mới nhất: Gemini

Đã bổ sung Gemini adapter native, giữ OpenAI/Mock và nghiệp vụ hiện tại. **121 kiểm tra service/HTTP giả lập đạt (60 Gemini mới), 35 kiểm tra hồi quy Task82 độc lập đạt; build 0 warning/0 error.** Không gọi Gemini thật, không đọc secret, không truy cập/thay đổi DB trong lượt này. Chưa có model/API/UI Gemini thật được xác minh; **Task 9.1 chưa hoàn thành**. File, cơ chế, bằng chứng và nội dung còn thiếu: [TASK-9.1-gemini.md](TASK-9.1-gemini.md). Lệnh cấu hình an toàn: [AI-configuration.md](AI-configuration.md).

## Lịch sử trước khi tích hợp Gemini

**Chưa đánh dấu hoàn thành nghiệm thu.** Đã triển khai backend/UI, kiểm thử Mock và lỗi bằng HTTP handler thay thế; chưa gọi OpenAI thật. Khi tiếp tục đã kiểm tra code hiện có, xác định phần còn dở là kiểm thử và tài liệu, không triển khai lại Task 8.1/8.2.

**Kiểm tra lại theo yêu cầu gọi API thật:** AI__Provider/AI__ApiKey/AI__Model đều chưa có trong Process/User/Machine và User Secrets của dự án. Appsettings vẫn Provider=Disabled, Model trống, không có key; appsettings.Development không ghi đè AI. Vì thiếu điều kiện nên **không gửi request OpenAI**, không có HTTP status/response/model thật để báo cáo. Chạy lại `dotnet bin/Task91FixVerified/Task91Checks.dll --database`: 82 kiểm tra đạt, DTO/prompt khớp SQL 44.125 / 14.750 / 29.375 ở bốn phạm vi ngày, toàn bộ HTTP provider bị bắt trong bộ nhớ. Browser hiện không có tab mở khi kiểm tra; chưa có báo cáo AI thật trên trình duyệt. Loading trực quan và chất lượng phản hồi thật vẫn chưa xác minh. Không sửa code, schema, quyền hoặc dữ liệu kho. Điều kiện tiếp tục: cấu hình key/model riêng trên máy chủ theo AI-configuration.md, khởi động ứng dụng từ môi trường đó; không gửi key qua chat. Các bằng chứng UI Mock/lỗi mô phỏng bên dưới là của lượt trước.

**Cập nhật mới nhất — bản sửa bổ sung:** đã tái hiện và sửa lỗi cấu hình số chứa chữ làm options binding ném exception; thêm log mã lỗi an toàn và làm rõ prompt. Kết quả sau sửa: **82 service/SQL, 22 HTTP Mock, 17 HTTP thiếu key, 17 HTTP cấu hình sai, 90/283 hồi quy đều đạt**; build sạch 0 warning/0 error. Đã kiểm tra UI ADMIN với lỗi provider 503 và response rỗng qua transport giả. Loading vẫn chưa có quan sát trực quan tin cậy; API thật vẫn chưa kiểm chứng. Chi tiết lỗi, file và bảng nghiệm thu hiện tại tại [TASK-9.1-fixes.md](TASK-9.1-fixes.md). Các số liệu/bảng dưới là lịch sử trước bản sửa, không thay thế kết quả mới này.

Rà soát bổ sung theo yêu cầu không triển khai lại: xem [TASK-9.1-audit.md](TASK-9.1-audit.md) với bảng triển khai/kiểm chứng/thiếu/bằng chứng. Đã chạy lại 76 service/SQL + 22 HTTP Mock + 17 HTTP thiếu key trên build hiện tại; thêm 4 kiểm tra JS độc lập. Build `bin/Task91Audit/` thành công 0 warning, 0 error. Đã kiểm tra UI bằng phiên ADMIN hiện có và đối chiếu chi tiết phiếu/sổ bằng SQL; không sửa code ứng dụng.

## Phần đã có

- IAiAnalysisService, provider OpenAI Responses, HttpClientFactory, cấu hình máy chủ và Mock Development có nhãn rõ ràng.
- DTO tổng hợp SQL thật, dùng chung predicate Posted với báo cáo; kiểm tra lệch tồn; prompt tiếng Việt có giới hạn dữ liệu.
- Trang `/AiAnalysis`: ngày, xem trước số chính thức, POST tạo lại phân tích, trạng thái chờ, lỗi, kết quả encode dạng văn bản.
- Giữ policy ViewReports, CSRF, no-store, giới hạn request; không thêm quyền/schema hoặc thao tác kho.
- Hướng dẫn tại `docs/AI-configuration.md`.

## Bằng chứng đã chạy

| Bộ kiểm thử | Kết quả |
|---|---|
| Task91Checks, fake HTTP handler | 55 kiểm tra đạt |
| Task91Checks --database, thêm đối chiếu SQL/Reports/DTO | Tổng 76 kiểm tra đạt |
| Verify-Task91, Mock trên cổng 7304 | 22 kiểm tra HTTP đạt |
| Verify-Task91, OpenAI thiếu key trên cổng 7305 | 17 kiểm tra HTTP đạt |
| Hồi quy Verify-Task81 trên bản AI, cổng 7304 | 90 kiểm tra đạt |
| Hồi quy Verify-Task82 trên bản AI, cổng 7304 | 283 kiểm tra HTTP/CSV/SQL đạt |

Service test bao gồm ngày sai/biên, cấu hình thiếu/sai, Mock ngoài Development, dữ liệu rỗng/âm/lệch/phạm vi sai/quá lớn, payload không có key/tools, parse output sau reasoning, lỗi 400/401/403/404/429/500/503/redirect, mất kết nối, timeout, cancellation, JSON sai, nội dung rỗng/refusal/incomplete/quá dài và phản hồi chứa key kiểm thử. Đây là lỗi mô phỏng, không phải kiểm thử lỗi trên OpenAI thật.

HTTP kiểm tra anonymous bị yêu cầu đăng nhập; WAREHOUSE_STAFF bị từ chối GET/POST; ACCOUNTANT dùng được; thiếu CSRF bị 400; quá lượt bị 429; ngày sai bị chặn; khoảng không giao dịch giữ tồn đầu kỳ; dữ liệu số/provider/key giả trong form bị bỏ qua; thiếu key không làm hỏng dashboard.

SQL thực tế: 20 hàng, 16 giao dịch, nhập **44.125**, xuất **14.750**, tồn **29.375** (tương ứng cách hiển thị tiếng Việt 44,125; 14,750; 29,375). Có 5 phiếu nhập Posted và 6 phiếu xuất Posted; 20 cảnh báo gồm 12 hết hàng. Đối chiếu bốn phạm vi: không giới hạn, ngày 22/09/2026, ngày 23/09/2026 không giao dịch, đến 21/09/2026. DTO gửi handler khớp dữ liệu báo cáo. Bằng chứng JSON cục bộ: `bin/Task91Evidence/analysis-input.json` (không chứa API key).

Snapshot sản phẩm trước/sau hồi quy không đổi. Không tạo hàng/phiếu giả; không sửa dữ liệu nghiệp vụ. Các phiếu Draft đang có không tính vào nhập/xuất; chưa có Cancelled/điều chỉnh trong dữ liệu thật để kiểm tra thực nghiệm. Bộ dữ liệu hiện có chỉ một trang, chưa xác minh nhiều trang thực tế.

## File Task 9.1

Tạo: `Options/AiOptions.cs`; `Models/AiAnalysis/AiAnalysisModels.cs`; `Services/Ai/IAiAnalysisService.cs`, `AiAnalysisService.cs`, `InventoryAnalysisDataService.cs`, `InventoryAnalysisPrompt.cs`; `Services/Reporting/StockReportQueries.cs`; `Controllers/AiAnalysisController.cs`; `Views/AiAnalysis/Index.cshtml`; `wwwroot/js/ai-analysis.js`; `tests/Task91Checks/Task91Checks.csproj`, `Program.cs`; `scripts/Verify-Task91.ps1`; hai tài liệu AI này.

Sửa tích hợp: `Program.cs`, `appsettings.json`, `Controllers/ReportsController.cs`, `Views/Shared/_Layout.cshtml`, `wwwroot/css/site.css`. Các thay đổi Task 8.1/8.2 có sẵn được giữ nguyên; cấu hình loại tests khỏi project chính đã có. Không phát hiện lỗi cần sửa thêm qua các lượt kiểm thử cuối; phần tiếp tục bổ sung kiểm thử và tài liệu.

Build ứng dụng `bin/Task91Verify/`, project kiểm thử `bin/Task91Checks/` và build xác nhận cuối `bin/Task91Final/`: thành công, **0 warning, 0 error**. `git diff --check` không có lỗi whitespace (Git chỉ thông báo chuyển LF sang CRLF).

## Chưa kiểm chứng / cần để nghiệm thu

- API thật: chưa có AI key/model được cấu hình; chưa xác minh quyền model, chất lượng tiếng Việt, độ chính xác nhận xét, độ trễ và chi phí. Không yêu cầu gửi key trong chat.
- ADMIN: đã xác minh GET/POST tạo Mock trong phiên trình duyệt `admin` hiện có, menu ADMIN hiển thị đúng. Chưa chạy lại thao tác nhập mật khẩu từ trạng thái đăng xuất; không có mật khẩu ADMIN trong cấu hình kiểm thử.
- Trình duyệt sau đăng nhập: đã kiểm tra số liệu/diễn giải riêng, tạo Mock, ngày không giao dịch, ngày đảo ngược, xóa lọc, thiếu/sai cấu hình, lỗi provider 503 và response rỗng mô phỏng. Chưa quan sát tin cậy trạng thái loading với request chậm; chưa kiểm tra mọi kích thước màn hình. JS busy/chặn lặp/pageshow/trang không form đã đạt kiểm tra độc lập, không thay thế kiểm thử thời gian trên trình duyệt.
- Database hoàn toàn rỗng, nhiều trang thật, Cancelled/điều chỉnh thật và cập nhật kho đồng thời: chưa có dữ liệu/môi trường phù hợp; không tạo dữ liệu nghiệp vụ để đạt kiểm thử. Dữ liệu rỗng ở service và khoảng không giao dịch đã được kiểm tra như mô tả trên.

Không gộp các mục chưa chạy vào số kiểm tra đã đạt và không coi Mock là bằng chứng tích hợp API thật.
