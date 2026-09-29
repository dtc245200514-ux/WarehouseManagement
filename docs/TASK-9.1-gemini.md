# Task 9.1 — tích hợp Gemini, 24/09/2026

**Chưa hoàn thành nghiệm thu. Không gọi API thật trong lượt này.** Không đọc/tạo secret, không truy cập hoặc thay đổi dữ liệu SQL Server, schema, migration hay quyền. Các thay đổi có sẵn từ Task 8.1/8.2/9.1 được giữ nguyên.

## Hiện trạng và thay đổi

Đã đọc code thực tế: .NET 10, EF Core 10.0.7, HttpClientFactory; không cần thêm SDK/package. Trước sửa chỉ có OpenAI Responses và Mock Development. Nhánh OpenAI nằm trong AiAnalysisService, không phải adapter độc lập. Controller, ViewModel/DTO, prompt, SQL aggregation, ViewReports, CSRF và giới hạn 5 POST/phút/người dùng không thay đổi.

| File | Loại trong lượt này | Nội dung |
|---|---|---|
| Services/Ai/GeminiAnalysisAdapter.cs | Mới | Native Gemini HTTP, metadata model, request/response, timeout, lỗi/log an toàn |
| tests/Task91Checks/GeminiChecks.cs | Mới | 60 kiểm tra Gemini dùng HTTP handler trong bộ nhớ |
| docs/TASK-9.1-gemini.md | Mới | Bằng chứng và giới hạn nghiệm thu |
| Options/AiOptions.cs | Sửa | GeminiBaseUrl riêng, giữ BaseUrl của OpenAI |
| Services/Ai/AiAnalysisService.cs | Sửa | Nhận adapter qua DI; chọn Gemini sau kiểm tra dữ liệu chung |
| Program.cs | Sửa | Typed HttpClient Gemini, không redirect, bỏ HTTP logger mặc định |
| appsettings.json | Sửa | Thêm endpoint Gemini, vẫn Disabled và model trống; không secret |
| Views/AiAnalysis/Index.cshtml | Sửa | Thông báo provider trung lập, phân biệt cấu hình với API đã xác minh |
| tests/Task91Checks/Program.cs | Sửa | Constructor mới và gọi bộ kiểm tra Gemini; giữ bộ OpenAI/Mock |
| docs/AI-configuration.md | Sửa | Biến môi trường, lệnh PowerShell và giới hạn readiness |
| docs/TASK-9.1-status.md | Sửa | Đặt bằng chứng mới nhất ở đầu, giữ lịch sử |

Đây là danh sách thay đổi của lượt Gemini; git working tree còn nhiều file từ các lượt trước.

## Cách adapter hoạt động

1. Service giữ nguyên kiểm tra DTO và xây prompt từ dữ liệu kho. Cấu hình section `AI`, provider đúng `Gemini`; model không có mặc định.
2. Kiểm tra key/model/endpoint/timeout/token cục bộ. `IsReady` chỉ chứng minh cấu hình hợp lệ về cú pháp, không chứng minh key có quyền, quota còn hay model hoạt động.
3. `GET /v1beta/models/{AI:Model}` trên host chính thức; header `x-goog-api-key`. Metadata phải có name đúng, `supportedGenerationMethods` chứa `generateContent` và outputTokenLimit đáp ứng cấu hình. Không gửi prompt kho trong GET.
4. Chỉ sau khi metadata hợp lệ, `POST /v1beta/models/{id}:generateContent`. Body gồm `systemInstruction`, `contents` role user, `generationConfig` với candidateCount=1 và maxOutputTokens. Giữ nguyên prompt tiếng Việt và JSON tổng hợp hiện tại. Không dùng body Responses, Bearer hay query chứa key.
5. Đọc `candidates[0].content.parts[].text`, bỏ thought parts, chỉ chấp nhận finishReason STOP; chặn rỗng, sai JSON, bị chặn, chưa hoàn chỉnh, quá dài hoặc nội dung chứa key. Kết quả về luồng Controller/View hiện có, Razor encode văn bản và tách số liệu chính thức.
6. Timeout dùng chung cho GET và POST; cancellation truyền lên; không tự retry. Giới hạn input 64 KiB, mỗi response 128 KiB, báo cáo 16.000 ký tự. Chỉ log status/phase/category, không log key, exception thô, prompt hoặc response.

Phân loại: cấu hình endpoint; model/method/token-limit; key (400 có reason nhận diện), key/quyền (401/403); request (400 khác); model không tồn tại/không khả dụng (404); quota/rate limit (429); provider (5xx); redirect/endpoint; network; timeout; response. Không khẳng định 403 chỉ do key sai hoặc 404 chỉ do model không tồn tại khi provider chưa đủ bằng chứng.

Tài liệu đối chiếu: [Gemini API và xác thực](https://ai.google.dev/api), [Models get/list](https://ai.google.dev/api/models), [generateContent](https://ai.google.dev/api/generate-content). Không chọn một model sản phẩm chưa xác minh; `test-model` trong test chỉ là fixture, không phải cấu hình để gọi thật.

## Bằng chứng chạy trong lượt này

- Build Task91Checks và ứng dụng phụ thuộc: **0 warning, 0 error**.
- `dotnet bin/GeminiChecks/Task91Checks.dll`: **121/121 đạt**, gồm 61 kiểm tra OpenAI/Mock/chung đã có và 60 Gemini mới. Toàn bộ HTTP AI bị chặn trong bộ nhớ; không đọc DB.
- Build Task82Checks và ứng dụng phụ thuộc: **0 warning, 0 error**.
- `dotnet bin/GeminiRegression/Task82Checks.dll`: **35/35 đạt**, kiểm tra tồn tối thiểu/CSV/policy độc lập, không DB.
- Build Task91UiHost và ứng dụng phụ thuộc tại `bin/GeminiUiBuild/`: **0 warning, 0 error**; chỉ build, không chạy host/browser.
- `git diff --check`: đạt, chỉ có thông báo quy đổi LF/CRLF của Git.
- Các kiểm tra Gemini bao gồm lựa chọn adapter, metadata trước POST, header/body, prompt DTO chính xác, lỗi hai giai đoạn HTTP, key/model/endpoint, quota, response rỗng/sai/bị chặn/quá giới hạn, timeout, cancellation, network và log không lộ dữ liệu.

## Chưa kiểm chứng

- Chưa gọi Gemini thật; chưa có model cụ thể được xác thực bằng key người dùng, HTTP status hoặc response thật.
- Chưa chạy lại giao diện Gemini sau đăng nhập, loading trực quan hoặc lỗi thực tế trên browser. Razor view đã qua build.
- Chưa đối chiếu lại SQL trong lượt này. Số nhập 44.125 / xuất 14.750 / tồn 29.375 là bằng chứng lịch sử, không phải kết quả SQL mới. Test mới xác nhận prompt từ DTO đến Gemini handler, không chứng minh SQL thật qua Gemini.
- Chưa chạy lại Verify-Task91 HTTP Mock/thiếu key, Verify-Task81/82 HTTP/SQL. Các kết quả 22/17/90/283 trước đây là lịch sử.
- Không tạo dữ liệu bổ sung để kiểm thử Cancelled/điều chỉnh hoặc các trường hợp nghiệp vụ chưa có.

Sau khi người dùng cấu hình cục bộ theo AI-configuration.md: khởi động đúng môi trường; đăng nhập tài khoản hợp lệ; tạo một báo cáo nhỏ; ghi nhận status thành công, văn bản có nội dung, model được metadata xác nhận, số liệu SQL và hiển thị tại /AiAnalysis. Khi đó mới tiếp tục đánh giá nghiệm thu; cấu hình hợp lệ hoặc test giả lập không đủ đánh dấu hoàn thành.
