# Cấu hình AI — Task 9.1

Cập nhật 25/09/2026: mặc định MaxOutputTokens=3000 (trần vẫn 4000), riêng gemini-3-flash-preview gửi thinkingLevel=minimal. Prompt yêu cầu khoảng 350 từ. Cần restart để áp dụng; AI__MaxOutputTokens trong môi trường ghi đè appsettings. Bản chờ kiểm chứng thật: `bin/GeminiTokenFix/`. Xem [sửa MAX_TOKENS](TASK-9.1-gemini-token-fix.md).

Backend hỗ trợ Gemini Developer API (generateContent), OpenAI Responses API qua HttpClientFactory và Mock chỉ trong Development. Mặc định `AI:Provider=Disabled`; thiếu cấu hình không ảnh hưởng báo cáo kho. Mở `/AiAnalysis` bằng tài khoản có policy `ViewReports` (ADMIN hoặc ACCOUNTANT). Tích hợp Gemini đã được kiểm thử bằng HTTP giả lập, **chưa gọi API Gemini thật**. Xem [bằng chứng Gemini](TASK-9.1-gemini.md).

## Cấu hình trên máy chủ

Các biến môi trường tương ứng:

| Biến | Giá trị |
|---|---|
| `AI__Provider` | `Disabled`, `Mock`, `OpenAI`, hoặc `Gemini` (đúng hoa/thường) |
| `AI__ApiKey` | Secret do người vận hành thiết lập riêng trên máy chủ |
| `AI__Model` | Không có mặc định; ID model hỗ trợ Responses khi chọn OpenAI, hoặc generateContent khi chọn Gemini |
| `AI__BaseUrl` | Chỉ OpenAI: `https://api.openai.com/v1/` |
| `AI__GeminiBaseUrl` | Chỉ Gemini: `https://generativelanguage.googleapis.com/v1beta/` |
| `AI__TimeoutSeconds` | Mặc định 30, hợp lệ 1–120 |
| `AI__MaxOutputTokens` | Mặc định 3000, hợp lệ 256–4000 |

Cũng có thể dùng .NET User Secrets trong Development với tên `AI:ApiKey`, `AI:Provider`, `AI:Model`. Không ghi key vào appsettings, source, tài liệu, chat hoặc lệnh có lịch sử. Dự án không tự đọc `.env`. Không có model mặc định: phải kiểm tra quyền sử dụng model trong tài khoản nhà cung cấp. Chưa xác minh model hoặc API key thật trong phiên kiểm thử này.

### Chạy Gemini sau khi người vận hành cấu hình cục bộ

Gemini dùng `AI__Provider=Gemini`, `AI__ApiKey`, `AI__Model`; endpoint riêng `AI__GeminiBaseUrl`. Không dùng endpoint OpenAI. Model phải là ID không kèm `models/`; chọn ID từ Google AI Studio/Models API mà tài khoản được cấp quyền, không dùng `test-model` của bộ kiểm thử. Không có model mặc định.

`IsReady` kiểm tra đầy đủ cấu hình cục bộ (endpoint HTTPS chính thức, key có cú pháp hợp lệ, model, timeout/token), **không phải xác nhận API thật**. Mỗi lần tạo báo cáo, adapter tự GET metadata model và kiểm tra `supportedGenerationMethods` có `generateContent`, tên model và giới hạn token trước khi POST dữ liệu. Model không tương thích sẽ bị chặn trước POST.

Chỉ chạy đoạn sau tại máy người vận hành khi sẵn sàng kiểm tra thật. Key nhập ẩn, không nằm trong lệnh/lịch sử hoặc file; không gửi key vào chat. Không dùng transcript/debug dump môi trường.

```powershell
Set-Location 'D:\KHOAI\WarehouseManagement'
dotnet build -p:UseAppHost=false -p:OutputPath=bin/GeminiRun/
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ASPNETCORE_URLS='http://127.0.0.1:7315'
$env:AdminSeed__Enabled='false'
$env:TestAccountSeed__Enabled='false'
$env:AI__Provider='Gemini'
$env:AI__GeminiBaseUrl='https://generativelanguage.googleapis.com/v1beta/'
$env:AI__Model=Read-Host 'ID model Gemini được cấp quyền, không kèm models/'
$taskGeminiSecret=Read-Host 'Gemini API key (chỉ nhập cục bộ)' -AsSecureString
try {
    $env:AI__ApiKey=[System.Net.NetworkCredential]::new('', $taskGeminiSecret).Password
    dotnet bin/GeminiRun/WarehouseManagement.dll
} finally {
    Remove-Item Env:AI__ApiKey -ErrorAction SilentlyContinue
    $taskGeminiSecret.Dispose()
}
```

Chạy DLL bỏ qua launch profile bật seed; môi trường chỉ áp dụng cho cửa sổ hiện tại và process con. Mở `http://127.0.0.1:7315/AiAnalysis`, đăng nhập ADMIN/ACCOUNTANT có sẵn. GET không gọi AI. Provider Gemini và không có lỗi cấu hình chỉ chứng minh binding cục bộ. Chọn phạm vi nhỏ có dữ liệu rồi bấm tạo: adapter kiểm tra model bằng GET trước và mới gửi POST generateContent khi hợp lệ. Log thành công event 9200 ghi HTTP status; lỗi event 9201/9202 chỉ ghi mã/nhóm lỗi an toàn. Đối chiếu báo cáo với số liệu chính thức trên trang và SQL trước khi nghiệm thu. Không dùng Task91UiHost để xác minh thật vì host kiểm thử chặn HTTP provider.

Không có request thật nào được thực hiện trong lượt triển khai Gemini. Khởi động app theo hướng dẫn và gọi API thật là bước tiếp theo do người vận hành chủ động thực hiện sau khi cấu hình key. Nếu cần tra model trước, dùng [Models API get/list](https://ai.google.dev/api/models) với header `x-goog-api-key`; không đặt key trong URL và không in toàn bộ lỗi HTTP.

### Vì sao trang báo Chưa cấu hình và cách chạy OpenAI

`WebApplication.CreateBuilder(args)` nạp cấu hình mặc định; `Configure<AiOptions>` bind section **AI**. Provider phân biệt hoa/thường: phải dùng đúng `OpenAI`, `Gemini` hoặc `Mock`. `Disabled`, chuỗi trống hoặc tên khác trả chế độ **Chưa cấu hình** trước khi kiểm tra key/model. `OPENAI_API_KEY`, `GEMINI_API_KEY`, `GOOGLE_API_KEY` không được tự ánh xạ; phải dùng `AI__ApiKey` (hai dấu gạch dưới).

Trạng thái tương ứng là `AiConfiguration.IsReady`, bằng true khi `Error` null. Với OpenAI cần: provider đúng; URL HTTPS chính thức `/v1` không query/fragment/userinfo; key không rỗng và chỉ ASCII hiển thị không khoảng trắng; model không rỗng, tối đa 128 ký tự và hợp lệ theo regex trong service; timeout 1–120 giây; token 256–4000. Có key đúng cú pháp chưa chứng minh xác thực API thành công. Mock Development có thể IsReady=true mà không có key.

Model mặc định trống ở cả appsettings và AiOptions; không có model được chọn sẵn/whitelist. Service đọc `IOptions<AiOptions>.Value.Model` rồi gửi vào trường `model` của Responses request. Người vận hành phải chọn ID model hỗ trợ Responses và được tài khoản cấp quyền; chưa xác minh một ID cụ thể trong môi trường này.

Chạy PowerShell tại máy của người vận hành (nhập secret trực tiếp ở prompt che ký tự, không đưa vào chat/source/lịch sử lệnh):

```powershell
Set-Location 'D:\KHOAI\WarehouseManagement'
dotnet build -p:UseAppHost=false -p:OutputPath=bin/Task91Real/
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ASPNETCORE_URLS='http://127.0.0.1:7315'
$env:AdminSeed__Enabled='false'
$env:TestAccountSeed__Enabled='false'
$env:AI__Provider='OpenAI'
$env:AI__Model=Read-Host 'ID model hỗ trợ Responses mà tài khoản được cấp quyền'
$taskAiSecret=Read-Host 'API key (nhập tại máy này)' -AsSecureString
try {
    $env:AI__ApiKey=[System.Net.NetworkCredential]::new('', $taskAiSecret).Password
    dotnet bin/Task91Real/WarehouseManagement.dll
} finally {
    Remove-Item Env:AI__ApiKey -ErrorAction SilentlyContinue
    $taskAiSecret.Dispose()
}
```

Chạy DLL trực tiếp bỏ qua launch profile có bật seed. Các `$env:` chỉ áp dụng cho PowerShell hiện tại và tiến trình con khởi động sau đó; không cập nhật ứng dụng đang chạy. Sau thay đổi phải khởi động lại từ đúng cửa sổ PowerShell. `setx` không cập nhật môi trường của process hiện tại. Không dùng `Task91UiHost` để kiểm tra API thật vì nó luôn chặn provider bằng fixture.

Đăng nhập ADMIN/ACCOUNTANT và mở `http://127.0.0.1:7315/AiAnalysis`. GET không gọi AI. Với dữ liệu hiện có: chế độ OpenAI, không có cảnh báo cấu hình, nút tạo xuất hiện là bằng chứng ứng dụng nhận cấu hình hợp lệ về cú pháp. Chỉ khi POST tạo báo cáo trả kết quả thực từ provider mới có bằng chứng key/model hoạt động. 401/403 là xác thực/quyền; 400/404 cần kiểm tra model/request; 429 là giới hạn/quota. Không in key để chẩn đoán.

Kiểm chứng ngày 24/09/2026: không có AI__Provider/AI__ApiKey/AI__Model trong Process/User/Machine hoặc User Secrets khi đọc. Chạy app bình thường ở 7313: HTTP 200, cảnh báo chọn provider, không nút tạo. Chạy instance 7314 chỉ đặt AI__Provider=OpenAI: HTTP 200, chế độ OpenAI, cảnh báo thiếu key, không nút tạo. Cả hai vẫn hiển thị tồn 29.375 và không có trường ApiKey trên frontend. Điều này xác minh binding biến môi trường provider hoạt động, không phải bằng chứng cấu hình API thật thành công. Không sửa code ứng dụng, schema, quyền hoặc dữ liệu kho trong lượt kiểm tra này.

Ví dụ chạy Mock trong PowerShell tại thư mục dự án:

```powershell
dotnet build -p:UseAppHost=false -p:OutputPath=bin/Task91Local/
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ASPNETCORE_URLS='http://127.0.0.1:7304'
$env:AdminSeed__Enabled='false'
$env:TestAccountSeed__Enabled='false'
$env:AI__Provider='Mock'
dotnet bin/Task91Local/WarehouseManagement.dll
```

Cần cấu hình SQL Server và tài khoản có sẵn của ứng dụng. Mock dùng số liệu kho thật và luôn ghi rõ không gọi AI thật. Để dùng OpenAI, đổi provider và thiết lập model/key qua cơ chế secret của môi trường, rồi khởi động lại. Không nhập key trên giao diện web.

## Dữ liệu và giới hạn

GET chỉ xem trước; POST có CSRF token lấy lại dữ liệu trước khi tạo phân tích. Ngày theo UTC, ngày cuối được tính trọn ngày. Chỉ giao dịch liên kết phiếu Posted tính nhập/xuất; tồn đầu/cuối lấy từ sổ giao dịch, tồn hiện tại từ hàng hóa. Biến động khác được tách riêng. Dữ liệu lệch sổ hoặc có giao dịch Draft bị chặn. Truy vấn dùng transaction Serializable chỉ đọc, kết thúc trước khi gọi AI; có thể giữ khóa đọc trong thời gian tổng hợp.

Payload gồm số tổng hợp, tối đa 10 hàng có biến động lớn, 10 hàng cảnh báo, 50 nhóm đơn vị và các giới hạn dữ liệu. Có mã, tên và đơn vị hàng hóa; không gửi tài khoản, mật khẩu hay chi tiết khách hàng. Tổng khác đơn vị không có ý nghĩa quy đổi hoặc giá trị tiền. Cảnh báo giữ nghiệp vụ hiện tại: hàng hoạt động có tồn <= mức tối thiểu; tồn bằng 0 là hết hàng.

Prompt coi tên/mã hàng là dữ liệu, không phải chỉ dẫn; yêu cầu không bịa số, xu hướng hoặc dự báo. Kết quả là văn bản được Razor encode, tách khỏi số liệu chính thức. Không có quyền ghi dữ liệu, công cụ nghiệp vụ, lưu lịch sử hay thay đổi schema. Prompt không bảo đảm mọi nhận xét của model đều đúng; cần kiểm tra kết quả thực tế trước nghiệm thu.

Giới hạn: 5 POST/phút/người dùng/mỗi process, không xếp hàng và không tự retry. POST không hợp lệ cũng có thể tiêu tốn lượt. Input tối đa 64 KiB; response 128 KiB; văn bản 16.000 ký tự. Rate limit không phải trần chi phí, không đồng bộ giữa nhiều instance; người vận hành cần cấu hình hạn mức nhà cung cấp. Không công bố giá/model chưa được xác minh.

API key chỉ trong Authorization header. Tắt logger HttpClient của AI; không ghi body lỗi/provider/prompt. Endpoint chỉ OpenAI HTTPS chính thức, không theo redirect. Request đặt `store=false`; điều này không đồng nghĩa cam kết không lưu giữ dữ liệu ở mọi lớp của nhà cung cấp.

Nếu TimeoutSeconds/MaxOutputTokens chứa chữ hoặc cấu hình không bind được, trang hiển thị lỗi cấu hình và không gửi request. Key có ký tự điều khiển/Unicode/khoảng trắng bị chặn trước HTTP. Điều này chỉ kiểm tra cú pháp, không xác nhận key được nhà cung cấp cấp quyền. Log service chỉ có event 9100 (bind cấu hình) hoặc 9101 với mã nhóm như `http_401`, `http_429`, `timeout`, `empty_response`; không kèm giá trị cấu hình, exception, prompt hoặc body provider.

## Kiểm thử

```powershell
dotnet build tests/Task91Checks/Task91Checks.csproj -p:UseAppHost=false -p:OutputPath=D:/KHOAI/WarehouseManagement/bin/Task91Checks/
dotnet bin/Task91Checks/Task91Checks.dll
dotnet bin/Task91Checks/Task91Checks.dll --database
./scripts/Verify-Task91.ps1 -BaseUrl http://127.0.0.1:7304 -Mode Mock
```

`--database` dùng SQL Server kiểm thử hiện tại, chỉ đọc. Các HTTP test dùng tài khoản kiểm thử đã có trong User Secrets. Test MissingKey cần instance riêng `AI__Provider=OpenAI` không có key, chạy `-Mode MissingKey`. Không chạy script Mock vào instance OpenAI thật. Các fixture phản hồi là dữ liệu kiểm thử trong bộ nhớ, không ghi kho và không gọi mạng ngoài.

Thêm `-Mode InvalidConfig` cho instance riêng với `AI__TimeoutSeconds=not-a-number` để xác minh lỗi bind không gây 500. Không đổi cấu hình instance đang vận hành chỉ để chạy test.

Host kiểm thử giao diện lỗi chạy entry point MVC thật nhưng chặn toàn bộ HTTP provider trong bộ nhớ:

```powershell
dotnet build tests/Task91UiHost/Task91UiHost.csproj -p:UseAppHost=false -p:OutputPath=D:/KHOAI/WarehouseManagement/bin/Task91UiLocal/
$env:ASPNETCORE_URLS='http://127.0.0.1:7310'
$env:TASK91_UI_SCENARIO='ServerError' # hoặc Empty
dotnet bin/Task91UiLocal/Task91UiHost.dll
```

Đăng nhập bằng tài khoản có quyền, mở `/AiAnalysis` và bấm tạo để kiểm tra lỗi. Host dùng key fixture không có giá trị thật và delay 5 giây; giao diện vẫn ghi OpenAI vì đang kiểm thử adapter đó, **không phải gọi API thật**. Không triển khai host test vào production; dừng bằng Ctrl+C sau kiểm thử. Không có migration hoặc dữ liệu kho giả.

Tài liệu API đã tham khảo: [Text generation](https://developers.openai.com/api/docs/guides/text), [API overview](https://developers.openai.com/api/reference/overview), [Responses create](https://developers.openai.com/api/reference/cli/resources/responses/methods/create).
