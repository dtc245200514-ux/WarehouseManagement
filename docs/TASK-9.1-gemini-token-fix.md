# Task 9.1 — sửa MAX_TOKENS, 25/09/2026

## Điểm tiếp tục

Người dùng đã xác nhận lỗi thực tế MAX_TOKENS trên bản chẩn đoán. Đã sửa code và build bản `bin/GeminiTokenFix/`; chưa gọi Gemini thật sau sửa, chờ người vận hành khởi động bản này từ PowerShell giữ key. Không đánh dấu Task 9.1 hoàn thành.

## Nguyên nhân và thay đổi

MAX_TOKENS xác nhận provider dừng do chạm giới hạn sinh token, không phải lỗi SQL hay safety block. Code trước đặt mặc định 1500 token, không gửi thinkingConfig và yêu cầu tối đa khoảng 600 từ. Theo [tài liệu Google](https://ai.google.dev/gemini-api/docs/generate-content/thinking), Gemini 3 Flash mặc định thinking high, token suy luận dùng chung hạn mức output. Chưa có usageMetadata thật của lần thất bại để định lượng bao nhiêu token dành cho suy luận; không khẳng định toàn bộ 1500 đã bị dùng cho suy luận hoặc môi trường app không ghi đè giá trị mặc định.

| Thành phần | Trước | Sau |
|---|---|---|
| MaxOutputTokens mặc định Options/appsettings | 1500 | 3000 |
| Trần kiểm tra cục bộ | 4000 | 4000, giữ nguyên |
| gemini-3-flash-preview thinking | Không gửi; mặc định provider | minimal, đúng khả năng model đã đối chiếu tài liệu |
| Các model khác | Mặc định provider | Giữ nguyên, không gửi thinkingConfig không chắc hỗ trợ |
| Độ dài báo cáo yêu cầu | Khoảng 600 từ | Khoảng 350 từ, 6 mục, mỗi mục 1–3 câu |
| Timeout | 30 giây mặc định, hợp lệ 1–120 | Giữ nguyên |

Giữ toàn bộ quy tắc số liệu chính thức, Posted/UTC, tồn hiện tại khác tồn lịch sử, khác đơn vị, cảnh báo bằng/dưới ngưỡng/hết hàng, không suy ra giá vốn/đặt hàng/nhân quả khi thiếu dữ liệu, không làm theo lệnh trong tên hàng. Không rút bớt dữ liệu SQL/DTO. Không tự retry hoặc tăng token vô hạn. Chỉ hiển thị STOP với nội dung hợp lệ; MAX_TOKENS vẫn bị từ chối.

## File sửa trong lượt này

- Options/AiOptions.cs và appsettings.json: mặc định maxOutputTokens=3000. Đây là cấu hình chung AI, nên OpenAI cũng dùng mặc định mới nếu không ghi đè môi trường.
- Services/Ai/GeminiAnalysisAdapter.cs: generationConfig có thinkingLevel minimal riêng model gemini-3-flash-preview; event 9204 ghi hạn mức, mức thinking cố định và số byte UTF-8 input/instructions. Event 9203 sẵn có ghi status/finish/token counts nếu API cung cấp. Không log body/key/prompt/thought text.
- Services/Ai/InventoryAnalysisPrompt.cs: chỉ rút gọn yêu cầu đầu ra 600→350 từ; giữ luật nghiệp vụ.
- tests/Task91Checks/Program.cs và GeminiChecks.cs: giữ bài kiểm tra cấu hình tường minh 1500; thêm 3 kiểm tra body Gemini Flash 3000/minimal và không gửi thinkingConfig cho model khác.
- docs/TASK-9.1-status.md, AI-configuration.md, tài liệu này: cập nhật điểm tiếp tục.

## Kiểm chứng

- Build Task91Checks và ứng dụng: 0 warning, 0 error.
- Build Task82Checks/ứng dụng tại bin/GeminiTokenFix82: 0 warning, 0 error; 35 kiểm tra hồi quy độc lập đạt.
- `dotnet bin/GeminiTokenFix/Task91Checks.dll --database`: 149 đạt (146 trước + 3 mới), SQL thật đọc-only, mọi HTTP AI bị chặn trong bộ nhớ. Bao gồm phạm vi không giới hạn và không Posted; số liệu 44.125/14.750/29.375 vẫn khớp.
- Chưa có request Gemini thật sau sửa, chưa có số token thực hoặc báo cáo hoàn chỉnh mới trên UI. Không dùng các số token fixture làm bằng chứng API thật.

## Chạy tiếp

Trong PowerShell đang giữ cấu hình Gemini/key, dừng ứng dụng cũ và chạy:

```powershell
Set-Location 'D:\KHOAI\WarehouseManagement'
$env:AI__MaxOutputTokens='3000'
dotnet bin/GeminiTokenFix/WarehouseManagement.dll
```

Giữ Provider=Gemini, Model=gemini-3-flash-preview, GeminiBaseUrl chính thức và cổng 7315; seed tắt như hướng dẫn AI-configuration.md. Nếu key bị finally xóa khi dừng app, nhập lại bằng Read-Host -AsSecureString tại máy, không đưa vào chat/lệnh chứa giá trị key. Biến môi trường ghi đè appsettings; phải khởi động từ đúng cửa sổ.

Sau khởi động, kiểm tra POST trên /AiAnalysis, xác nhận STOP, nội dung đủ 6 mục và số liệu không bị diễn giải sai; đọc riêng event 9203/9204 để kiểm chứng request/usage thật. Chưa hoàn thành nếu chưa có bằng chứng này.
