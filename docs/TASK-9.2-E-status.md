# Task 9.2-E — bảo vệ dữ liệu nhạy cảm và kiểm thử cuối

Ngày: 29/09/2026. Chỉ phạm vi 9.2-E; không chuyển sang 9.3.

## Kết quả rà soát và thay đổi

Không tìm thấy key thật hoặc các secret đã cấu hình trong source/index/history theo bộ quét: so khớp giá trị trong môi trường/User Secrets chỉ ở bộ nhớ, nhận diện định dạng key Gemini/OpenAI và private key; không in các giá trị. Lịch sử Git có 3 commit, 161 blob duy nhất đã được đọc; không rewrite history. Các fixture chỉ dùng chuỗi giả lập, không phải credential hoạt động.

Phát hiện và sửa ba điểm bảo mật:

1. Model vô tình bằng/chứa key trước đây chỉ được redact ở audit nhưng vẫn có thể đi vào URL metadata. Adapter nay chặn trước mọi HTTP; audit vẫn redact model.
2. Tên/mã/đơn vị là chuỗi tự do. Dù DTO không chứa trường tài khoản, dữ liệu vô tình chứa credential có thể đi vào prompt; response cũng chỉ kiểm tra đúng key hiện tại. Thêm AiSensitiveDataGuard trước gửi và trước trả text: từ chối key hiện tại (kể cả dạng JSON escaped), định dạng provider key, email và các nhãn credential/account như password/hash, username, phone, cookie, session, access/refresh token, authorization hoặc secret có giá trị. Không sửa tên hàng hoặc nội dung AI để đoán; chặn toàn yêu cầu/phản hồi bằng lỗi an toàn.
3. Layout chung hiển thị User.Identity.Name trên trang AI. Nay ẩn riêng tên đăng nhập khi controller=AiAnalysis, giữ đăng xuất/quyền/nghiệp vụ và các trang khác.

Thêm ignore cho .env/.env.*, secrets.json, UserSecrets, appsettings.Local/*.Local.json và *.secrets.json. Không đưa secret vào source hoặc thay key đang dùng. .env.example được phép theo dõi nhưng vẫn cần nội dung mẫu không bí mật.

## File sửa/tạo trong lượt này

| File | Thay đổi |
|---|---|
| .gitignore | Loại trừ file secret local |
| Services/Ai/AiSensitiveDataGuard.cs | Mới: phát hiện dấu hiệu nhạy cảm, không log/echo giá trị |
| Services/Ai/GeminiAnalysisAdapter.cs | Chặn key trong model, input nhạy cảm và response nhạy cảm |
| Services/Ai/AiAnalysisService.cs | Bảo vệ input/output của nhánh adapter OpenAI hiện có tương tự; không đổi provider |
| Views/Shared/_Layout.cshtml | Không hiện username tại trang AI |
| scripts/Verify-Task92E-Secrets.ps1 | Mới: quét source/index/history/log/config và kiểm tra ignore; output an toàn |
| tests/Task91Checks/SecurityChecks.cs | Mới: spy request, dữ liệu tài khoản, key URL/body/model, log/ViewModel |
| tests/Task91Checks/Program.cs | Chạy 39 assertions bảo mật mới |
| tests/Fixtures/GeminiFormatFixtures.cs | Thêm 5 response nhạy cảm giả lập, giữ 20 ca D |
| tests/Task91UiHost/Program.cs | GeminiSecurity, không gọi network provider |
| scripts/Verify-Task92C.ps1 | Chế độ -Security; kiểm tra credential thật trong HTML mà không in; giữ C/D mặc định |
| docs/TASK-9.2-E-status.md | Bằng chứng và giới hạn |

Đã đọc nhưng giữ nguyên: AiOptions, appsettings, Program.cs/DI, InventoryAnalysisDataService, InventoryAnalysisPrompt, DTO/ViewModel, controller, GeminiResponseValidator, các test Gemini/Movement/Replenishment và chính sách quyền. Prompt nghiệp vụ, model, provider, key, timeout/token, strict `<`, cảnh báo `<=`, xử lý C/D và schema không đổi. Guard bảo mật là lớp kiểm tra bổ sung; response vẫn qua validator 9.2-D.

## Dữ liệu gửi provider

Spy transport xác nhận payload chính xác bằng InventoryAnalysisPrompt.Input(InventoryAnalysisData). DTO được chiếu từ Products/InventoryTransactions và phiếu Posted chỉ đọc; không serialize entity, navigation, IdentityUser hoặc HttpContext. Input gồm kỳ/snapshot, tổng nhập/xuất/tồn, số phiếu/cảnh báo, mẫu biến động/cảnh báo <=10, nhóm đơn vị <=50, mẫu đề xuất <=50 và các giới hạn. Không có password/hash, username/email/phone tài khoản, session/cookie/token hoặc cấu hình secret. Request body không có key; Gemini dùng header xác thực hiện có, không URL query/Bearer. HttpClient loggers tiếp tục bị tắt.

Các thông tin credential đưa vào tên hàng ở fixture bị chặn trước metadata GET, không sửa SQL để tạo tình huống. Payload SQL thật được đối chiếu bằng MovementChecks ở nhiều kỳ với spy Gemini; không sao chép payload nhạy cảm vào tài liệu.

Audit chỉ ghi category cố định, model đã kiểm tra/redact, HTTP status, finish reason allowlist và token count dạng số. Không log prompt/body/response/exception. Response chứa key/tài khoản/email/cookie bị từ chối trước HTML; response quá lớn, lỗi, MAX_TOKENS vẫn đi qua kiểm tra có sẵn và hồi quy D/C.

## Trạng thái kiểm thử

**TASK 9.2-E = ĐẠT trong phạm vi kiểm thử và giới hạn bên dưới.** Không còn kiểm thử FAIL. Không dùng mô phỏng làm bằng chứng Gemini thật.

| Yêu cầu bảo mật | Kết quả | Bằng chứng |
|---|---|---|
| API key trong source | PASS | Quét file code/config/docs/scripts với mẫu key và secret cấu hình, không in giá trị |
| API key Git | PASS | Index và 3 commit/161 blob; không secret local tracked; 7 đường dẫn ignore đạt |
| API key log/audit | PASS | Quét log local; log capture unit và 3 host không marker nhạy cảm |
| API key/UI | PASS | 5 ca phản hồi nhạy cảm bị chặn; HTML không key, username/password thực của tài khoản kiểm thử |
| API key URL/body | PASS | Spy xác nhận header-only; model chứa key bị chặn trước HTTP |
| Password/hash trong prompt | PASS | Không có field trong DTO/payload spy; tên hàng fixture chứa password/hash bị chặn |
| Account/user data trong prompt | PASS | Không IdentityUser/username/email/phone, không cookie/session/access token; đầu vào nhiễm bị chặn |
| Sensitive response trong log | PASS | Marker key/account/password/cookie/header không xuất hiện trong log host |
| Exception leakage | PASS | Hồi quy network/socket/timeout/unexpected; không exception object vào logger hoặc stack trace vào HTML |
| Data minimization | PASS | SQL projection -> DTO kho -> exact spy payload, không entity/navigation/request context |
| Validator 9.2-D | PASS | 40 unit assertions + 262 HTTP/state checks, không bỏ qua validator |
| Database unchanged | PASS | Hash 8 bảng sau từng ca D/E và cuối toàn bộ kiểm thử khớp baseline |

### Các bộ kiểm thử thực chạy

Tất cả log bằng chứng dưới `bin/Task92EEvidence` (ignored, không chứa secret thật):

| Bộ | PASS | FAIL | File |
|---|---:|---:|---|
| Service/SQL/AI | 304 | 0 | service-sql.log |
| HTTP/state 9.2-D, 20 ca | 262 | 0 | format-http.log |
| HTTP 9.2-C, 17 ca | 87 | 0 | error-http.log |
| HTTP/state bảo mật, 5 ca | 72 | 0 | security-http.log |
| AI Mock | 26 | 0 | mock-http.log |
| UI–SQL 9.2-B | 21 | 0 | replenishment-ui-sql.log |
| Thiếu key/cấu hình sai | 34 | 0 | missing-key-http.log, invalid-config-http.log |
| 8.1 | 90 | 0 | task81.log |
| 8.2 HTTP/CSV/SQL | 283 | 0 | task82-http.log |
| 8.2 độc lập | 35 | 0 | task82-unit.log |
| Source/Git/config/log scan | 14 | 0 | repository-security.log |
| Log/audit host cuối | 6 | 0 | log-safety.log |
| Hash cuối | 8 | 0 | business-after-check.log |
| **Tổng** | **1242** | **0** | Không cộng trùng các nhóm bên dưới |

Phân nhóm theo nhiệm vụ để báo cáo (mỗi assertion chỉ đếm một lần ở bảng này; một số test bao phủ nhiều nhiệm vụ):

| Nhiệm vụ | PASS / FAIL | Phân bổ |
|---|---|---|
| 9.1 regression | 242 / 0 | 182 service/SQL/Gemini dùng chung + 60 Mock/config HTTP; gồm model, prompt, báo cáo, token/timeout/audit, configuration |
| 9.2-A regression | 21 / 0 | Movement unit/SQL |
| 9.2-B regression | 43 / 0 | 22 service/SQL threshold/sample + 21 UI–SQL; count/full list cũng được kiểm tra trong HTTP chung |
| 9.2-C regression | 87 / 0 | HTTP lỗi; unit lỗi nằm nhóm 9.1 dùng chung |
| 9.2-D regression | 302 / 0 | 40 format unit + 262 HTTP/state |
| 9.2-E | 139 / 0 | 39 spy/security unit + 72 HTTP/state + 14 scan + 6 log + 8 hash cuối |
| Task 8 regression | 408 / 0 | 90 + 283 + 35 |
| **Tổng** | **1242 / 0** | |

Build ứng dụng bằng `dotnet build -p:UseAppHost=false -p:NuGetAudit=false -p:OutputPath=D:/KHOAI/WarehouseManagement/bin/Task92EFinal/`: **0 warning, 0 error**. Đây là build code, không phải kiểm toán lỗ hổng dependency (NuGetAudit tắt như các lượt trước). Build các project kiểm thử cũng 0 warning/0 error. Bản MVC đã kiểm thử ở bin/Task92E dùng cùng source. Không sửa bản đang chạy của người dùng.

Quét log lần đầu lỗi do file host đang mở; đã sửa dùng FileShare.ReadWrite và chạy lại toàn bộ scan đạt. Không bỏ qua file hoặc đổi expected để đạt PASS. Không tìm thấy secret thật để phải thu hồi key hoặc sửa lịch sử Git.

Hash khớp Products (CurrentQuantity/MinimumStockLevel), Categories, Suppliers, ImportReceipts, ImportReceiptDetails, ExportReceipts, ExportReceiptDetails, InventoryTransactions. Không ghi warehouse entity, tạo phiếu hoặc thay đổi kho. Audit format: 20 lần (2 thành công mô phỏng/18 thất bại); error: 17 (1/16); security: 5 (0/5). Chỉ SQL thật; provider HTTP luôn bị intercept trong test host.

Các host kiểm thử riêng cổng 7330–7335 được dừng sau kiểm thử. Giữ nguyên instance người dùng ở cổng khác. Không thay đổi trạng thái hoàn thành các task trước dựa trên bằng chứng mới không liên quan.

## Giới hạn của kết luận

### Xác nhận lượt tiếp tục

Đã đọc lại yêu cầu 9.2-E và kiểm tra phần bị ngắt ở cuối lượt trước: lần quét secret cuối đã hoàn tất với 14 PASS. Đã đếm trực tiếp các dòng PASS trong 14 file log bằng chứng, tổng đúng 1.242, không có dòng FAIL; hash cuối có đủ 8 bảng khớp. Không còn listener kiểm thử tại 7330–7335. Bản bin/Task92EFinal/WarehouseManagement.dll tồn tại; git diff --check không báo lỗi whitespace (chỉ cảnh báo LF/CRLF). Lượt tiếp tục chỉ xác nhận bằng chứng và cập nhật tài liệu, không sửa code hoặc chạy lại nghiệp vụ/SQL/API; kết quả build và hồi quy là các lần chạy đã ghi nhận bên trên.

- Quét secret dựa trên các giá trị cấu hình đã biết và mẫu định dạng, không chứng minh tuyệt đối không có secret tùy ý/đã mã hóa/không nhận diện. Quét history chỉ các commit reachable hiện có; không kiểm tra remote không fetch hoặc object đã mất tham chiếu. Không phát hiện secret đã commit trong phạm vi quét.
- Guard là phòng vệ bổ sung, không phải bộ nhận diện mọi dữ liệu cá nhân hoặc mọi cách diễn đạt tài khoản; có thể chặn nhầm tên hàng có dạng email/credential. Boundary chính vẫn là DTO chỉ lấy dữ liệu kho cần thiết. Không đưa thêm tài khoản/mật khẩu vào service để cố so khớp response.
- HTML vẫn có antiforgery token cần thiết cho CSRF; token này không được đưa vào prompt hoặc log. Cookie đăng nhập là cơ chế xác thực trình duyệt, không truyền cho provider. Không loại bỏ bảo vệ CSRF để loại mọi chuỗi được gọi là token.
- Kiểm thử dùng transport mô phỏng và SQL thật chỉ đọc, không gọi Gemini thật. Kết luận E không thay bằng chứng chất lượng diễn giải hoặc nghiệm thu Gemini thật của B. Không tự sửa trạng thái nghiệm thu B/A cũ.
- Không thay password/quyền; HTTP dùng Accountant hiện có. ADMIN không được kiểm tra lại. Không tự ý thay dữ liệu kho để làm test.
