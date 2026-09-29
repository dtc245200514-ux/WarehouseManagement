# Task 9.2-D — kiểm tra định dạng phản hồi Gemini

Ngày thực hiện: 29/09/2026. Chỉ phạm vi 9.2-D, không chuyển sang 9.3.

## Hợp đồng dữ liệu thực tế

Prompt hiện có yêu cầu báo cáo **văn bản thuần**, không yêu cầu Gemini viết JSON nghiệp vụ kiểu `summary/quantity/items`. Response HTTP từ generateContent phải là JSON envelope; văn bản hợp lệ nằm tại `candidates[0].content.parts[].text`. Do đó 9.2-D kiểm tra schema envelope thực tế, không tự đặt schema báo cáo mới hoặc đổi cấu hình Gemini. Một đoạn văn bản thuần đứng ngoài envelope sẽ bị từ chối. Văn bản trong trường `text` đúng kiểu vẫn là báo cáo diễn giải, không được deserialize thành Product/phiếu/giao dịch.

Parser: `System.Text.Json.JsonDocument.Parse` trong `GeminiAnalysisAdapter.ReadJson`, giới hạn response 128 KiB. Không cắt markdown fence, không trích JSON từ văn bản, không sửa JSON bị cắt hoặc ép kiểu số từ string.

Validator: `GeminiResponseValidator.ValidRoot` và `ValidCompletedCandidate`, phối hợp với kiểm tra finishReason/candidates có sẵn trong adapter. Chỉ trả Success khi toàn bộ điều kiện hợp lệ và báo cáo không rỗng, không chứa key, không vượt 16.000 ký tự. Kết quả qua `AiAnalysisResult`; output sai có Text=null và FailureKind=InvalidResponse. Controller chỉ đặt Report khi Success=true.

## Lỗi cụ thể và xử lý

- Trước sửa, usageMetadata có số token dạng string bị bỏ qua; part có cả text và functionCall vẫn có thể được coi là báo cáo văn bản. Bộ parser cũng chưa chủ động chặn tên property JSON trùng nhau. Đã bổ sung kiểm tra, không thực thi hoặc sửa nội dung sai.
- JSON sai/trống/whitespace/truncated: JsonException được bắt, trả thông báo cố định, không raw exception/body.
- Root null/array/string hoặc property trùng: ValidRoot thất bại.
- Thiếu candidates, finishReason, content, role, parts hoặc text: bị từ chối trước khi trả báo cáo.
- Candidates/parts phải là array; candidate/content/part phải là object; role phải là string `model`; text phải là string; thought nếu có phải boolean; token count đã biết nếu có phải số nguyên không âm trong Int32, không ép từ string.
- Không nhận part trộn functionCall/functionResponse/code/image/file cùng text. Thought text không hiển thị. Chỉ thought hoặc toàn whitespace không tạo báo cáo hợp lệ.
- MAX_TOKENS hoặc finishReason khác STOP vẫn bị chặn theo 9.2-C; không đoán phần thiếu. Audit success=false, không hiển thị nội dung cắt.
- Lỗi schema mới hiển thị: “Kết quả phân tích AI không đúng định dạng mong đợi. Vui lòng thử lại.” Các thông báo an toàn hiện có cho JSON lỗi/rỗng/MAX_TOKENS giữ nguyên để không phá hành vi 9.2-C.
- Số liệu chính thức vẫn từ SQL và hiển thị độc lập. Không đổi query nhập/xuất/tồn, prompt 9.2-B, provider/model/key/timeout/token limit, schema/quyền hoặc logic đề xuất.

## File thay đổi trong lượt 9.2-D

| File | Thay đổi |
|---|---|
| Services/Ai/GeminiResponseValidator.cs | Mới: kiểm tra cấu trúc/kiểu/property trùng và part không được phép |
| Services/Ai/GeminiAnalysisAdapter.cs | Gọi validator trước khi sử dụng nội dung |
| Controllers/AiAnalysisController.cs | Chỉ hiển thị Text khi result.Success |
| tests/Fixtures/GeminiFormatFixtures.cs | Mới: 20 trường hợp định dạng dùng chung cho unit và HTTP |
| tests/Task91Checks/GeminiFormatChecks.cs | Mới: 40 assertions đi qua adapter thật, chỉ mô phỏng transport |
| tests/Task91Checks/Program.cs | Gọi bộ kiểm tra mới |
| tests/Task91Checks/Task91Checks.csproj | Link fixture dùng chung |
| tests/Task91UiHost/Task91UiHost.csproj | Link fixture dùng chung |
| tests/Task91UiHost/Program.cs | Chế độ GeminiFormat; transport chặn network, dùng entry point/MVC thật |
| scripts/Verify-Task92C.ps1 | Thêm -Format, giữ mặc định 9.2-C; kiểm tra SQL hash sau từng ca định dạng |
| docs/TASK-9.2-D-status.md | Bằng chứng và giới hạn nghiệm thu |

Các thay đổi sẵn có từ Task 8/9 trong working tree được giữ nguyên; không phải toàn bộ git status là thay đổi mới của lượt này.

## Ma trận kiểm tra định dạng

20 ca: valid JSON; invalid JSON; missing required field; parts sai kiểu; candidates sai kiểu; text=null; root=null; body rỗng; whitespace; plain text; markdown bọc JSON; JSON bị cắt; MAX_TOKENS; số token dạng string; property trùng; text lẫn tool call; thought sai kiểu; valid text chia nhiều parts; object rỗng; text dạng number.

Mỗi ca chạy qua adapter thật ở kiểm thử service; qua controller/service/adapter/Razor với SQL thật ở HTTP. Transport provider là fixture, không phải kết quả PASS giả lập: các assertion kiểm tra kết quả và HTML thực tế. HTTP xác nhận số liệu chính thức, đủ 20 dòng đề xuất, không lộ key/body/stack trace, chỉ hai ca hợp lệ hiển thị báo cáo. Sau từng POST đối chiếu hash cả 8 bảng với baseline trước kiểm thử. Rate limit 5 lần/phút giữ nguyên và chờ giữa các nhóm.

## Kết quả

**Task 9.2-D: ĐẠT trong phạm vi hợp đồng response hiện có.** Tất cả bộ kiểm thử kết thúc exit code 0; không có test FAIL.

| Bộ kiểm thử | PASS | FAIL | Bằng chứng trong bin/Task92DEvidence |
|---|---:|---:|---|
| Service/SQL/AI, gồm 40 assertions mới 9.2-D | 265 | 0 | service-sql.log |
| HTTP 9.2-D: 20 ca, gồm 160 kiểm tra hash sau từng ca | 262 | 0 | format-http.log |
| Hồi quy 9.2-C: 17 ca lỗi/success | 87 | 0 | error-http.log |
| AI Mock, quyền, CSRF, rate limit, 9.2-B | 26 | 0 | mock-http.log |
| Đối chiếu UI đề xuất 9.2-B với SQL | 21 | 0 | replenishment-ui-sql.log |
| Hồi quy 8.1 | 90 | 0 | task81.log |
| Hồi quy 8.2 HTTP/CSV/SQL | 283 | 0 | task82-http.log |
| Hồi quy 8.2 độc lập | 35 | 0 | task82-unit.log |
| Log/audit an toàn của hai host | 4 | 0 | log-safety.log |
| Hash cuối sau toàn bộ kiểm thử | 8 | 0 | business-after-check.log |
| **Tổng** | **1081** | **0** | Không cộng lại 40 hoặc 160 assertions đã nằm trong các dòng trên |

Build ứng dụng cùng Task91Checks, Task91UiHost và Task82Checks: **0 warning, 0 error**. Output riêng: bin/Task92D, bin/Task92DChecks, bin/Task92D82Checks. Các build đều biên dịch ứng dụng qua ProjectReference; không ghi đè instance người dùng đang chạy. git diff --check không có lỗi whitespace (chỉ cảnh báo chuẩn hóa LF/CRLF trên file cũ).

Format host có 20 audit: 2 success hợp lệ, 18 failure; error host 9.2-C có 17 audit: 1 success, 16 failure. Không log marker key, header nhạy cảm, body lỗi hoặc nội dung báo cáo fixture. Đây là kiểm tra chống rò rỉ bằng fixture có chủ đích, không khẳng định bao phủ mọi chuỗi tùy ý.

Hash sau từng ca và cuối cùng khớp baseline: Products (gồm CurrentQuantity/MinimumStockLevel), Categories, Suppliers, ImportReceipts, ImportReceiptDetails, ExportReceipts, ExportReceiptDetails, InventoryTransactions. Không INSERT/UPDATE/DELETE nghiệp vụ, không tự tạo phiếu, không thay đổi tồn kho hoặc schema. AI vẫn chỉ trả diễn giải; không có đường SaveChanges hoặc công cụ ghi kho từ output.

HTTP giữ tổng toàn lịch sử nhập 44.125/xuất 14.750/tồn hiện tại 29.375; kỳ 23–25/09/2026 nhập/xuất 0.000, tồn hiện tại 29.375. Danh sách 9.2-B vẫn đủ 20 mã và từng ô khớp SQL. Đây là tổng số học nhiều đơn vị tính. Không thay điều kiện `<` của đề xuất hoặc `<=` của cảnh báo.

Lệnh tái kiểm thử định dạng (host cần khởi động mới để reset thứ tự fixture):

```powershell
# Trong thư mục dự án; host kiểm thử tự tắt seed và chặn mọi provider HTTP.
$env:TASK91_UI_SCENARIO='GeminiFormat'
$env:ASPNETCORE_URLS='http://127.0.0.1:7327'
dotnet bin/Task92D/Task91UiHost.dll
# Cửa sổ khác, trước POST:
pwsh -NoProfile -File scripts/Verify-Task92A-State.ps1 -Phase Before -EvidenceDirectory ../bin/Task92DRepeatEvidence
pwsh -NoProfile -File scripts/Verify-Task92C.ps1 -BaseUrl http://127.0.0.1:7327 -Format -EvidenceDirectory ../bin/Task92DRepeatEvidence
```

Không chạy script fixture này vào instance Gemini thật. Model/key fixture chỉ tồn tại trong test host; không sửa cấu hình production. Các host kiểm thử riêng được dừng sau nghiệm thu.

## Giới hạn

- Đây là kiểm chứng xử lý định dạng, không chứng minh mọi nhận xét hoặc số trong văn bản AI đều đúng. Không đổi prompt hay xây schema JSON nghiệp vụ mới. Nếu cần báo cáo có trường summary/quantity/items bắt buộc, đó là thay đổi hợp đồng output riêng.
- Không gọi Gemini thật trong lượt này. Success trong fixture chỉ xác nhận parser và luồng hiển thị, không thay thế bằng chứng API thật hoặc chất lượng nội dung 9.2-B.
- HTTP dùng Accountant có sẵn; không đổi mật khẩu hoặc quyền để đăng nhập ADMIN. Không sửa database để tạo ca kiểm thử.
