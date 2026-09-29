# Rà soát phần còn thiếu Task 9.1 — 24/09/2026

> Đây là bằng chứng của lượt audit trước bản sửa bổ sung. Kết quả mới nhất, gồm lỗi bind cấu hình đã tái hiện/sửa và kiểm thử UI lỗi provider, nằm ở [TASK-9.1-fixes.md](TASK-9.1-fixes.md).

**Kết luận: chưa nghiệm thu hoàn thành. Không cần triển khai lại hoặc sửa code ứng dụng trong lượt rà soát này.** Đã đọc hai tài liệu AI, toàn bộ service/adapter, DTO/options, Program, controller AI/báo cáo, view/JS, project test và script Verify-Task91; kiểm tra thêm nghiệp vụ ghi sổ phiếu nhập/xuất. Kết luận dưới đây dựa trên chạy lại code hiện tại và quan sát trình duyệt, không chỉ dựa vào tên file hoặc báo cáo cũ.

## Bảng đối chiếu

| Hạng mục | Đã triển khai | Đã kiểm chứng | Còn thiếu | Bằng chứng |
|---|---|---|---|---|
| Luồng UI → controller → tổng hợp → prompt → provider → kết quả | Có, DI nối đầy đủ | GET/POST Mock qua HTTP và trình duyệt ADMIN | Nhánh provider thật | Program.cs; AiAnalysisController; 22 HTTP checks; thao tác trình duyệt cổng 7306 |
| Adapter OpenAI Responses | Nằm trong AiAnalysisService, không phải file adapter riêng | POST /v1/responses, Bearer, payload, parse output_text qua fake handler | Xác thực và response của OpenAI thật | Task91Checks; đối chiếu tài liệu OpenAI chính thức |
| Cấu hình key/model | Options + cấu hình .NET, mặc định Disabled | User Secrets và biến môi trường process không có AI key/model; thiếu key không gửi request | Key/model được cấp quyền trên máy chủ | Chỉ kiểm tra cờ có/không, không in secret; 17 HTTP checks MissingKey |
| SQL/điều kiện nghiệp vụ | PostedInPeriod dùng chung dashboard; báo cáo chi tiết dùng điều kiện tương đương | Bốn phạm vi ngày, DTO thực gửi fake handler, đối chiếu từng chi tiết phiếu/sổ bằng SQL độc lập | Cancelled, điều chỉnh và DB rỗng thực tế chưa có | 76 checks gồm 21 đối chiếu DB; SQL bên dưới |
| CSRF/phân quyền | ValidateAntiForgeryToken, ViewReports | Anonymous yêu cầu đăng nhập; staff bị từ chối; accountant GET/POST thành công; phiên ADMIN GET/POST Mock thành công | Chưa thực hiện lại thao tác nhập mật khẩu ADMIN từ trạng thái đăng xuất | 22/17 HTTP checks; menu ADMIN và tài khoản admin trên trình duyệt |
| Thiếu key/timeout/lỗi/response xấu | Có thông báo an toàn, timeout/cancellation và giới hạn response | Thiếu key qua HTTP và UI; lỗi 400/401/403/404/429/500/503, timeout/rỗng/sai JSON qua fake handler | Lỗi từ API thật và UI sau lỗi provider chưa được quan sát | Task91Checks; cổng 7307 |
| Số liệu riêng với diễn giải | Hai phần riêng, Razor encode văn bản | Quan sát số liệu và báo cáo Mock trên trình duyệt | Độ chính xác diễn giải của model thật | Hình ảnh và AX state trang Generate; không có Html.Raw |
| Bộ lọc/ngày và nút tạo | Có GET preview, POST lấy lại dữ liệu | Tạo Mock, ngày 23/09 không giao dịch, ngày đảo ngược, xóa lọc | Chưa kiểm tra mọi kích thước màn hình | Tương tác trực tiếp trình duyệt, viewport mặc định hẹp |
| Chờ/chống gửi lặp | JS disabled + aria-busy; 5 POST/phút/user/process | HTTP lần thứ sáu trả 429; 4 kiểm tra JS độc lập đạt | Chưa quan sát trạng thái chờ trong request chậm trên trình duyệt | Script ai-analysis.js chạy với DOM tối thiểu; Mock trả quá nhanh |
| Không ghi kho/build | Luồng AI chỉ đọc; không schema mới | SQL không có sai lệch; build hiện tại 0 warning/0 error | Kiểm thử kho thay đổi đồng thời chưa chạy | Build bin/Task91Audit; đọc code và SQL |

## Các lượt chạy mới

- `dotnet build tests/Task91Checks/Task91Checks.csproj -p:NuGetAudit=false -p:UseAppHost=false -p:OutputPath=D:/KHOAI/WarehouseManagement/bin/Task91Audit/`: thành công, 0 warning, 0 error; build cả ứng dụng và project test.
- `dotnet bin/Task91Audit/Task91Checks.dll --database`: **76 đạt**, gồm 55 kiểm tra service và 21 kiểm tra bổ sung với SQL. Toàn bộ request provider bị fake handler bắt trong bộ nhớ.
- `Verify-Task91.ps1 -BaseUrl http://127.0.0.1:7306 -Mode Mock`: **22 đạt**.
- `Verify-Task91.ps1 -BaseUrl http://127.0.0.1:7307 -Mode MissingKey`: **17 đạt**.
- Chạy nguyên JS với Node VM/DOM tối thiểu: **4 kiểm tra đạt** — lần submit đầu bật busy/disable; lần hai bị chặn; pageshow phục hồi; trang không có form không lỗi. Đây không phải kiểm thử thời gian xử lý thực tế trong trình duyệt.
- 90/283 kiểm tra hồi quy 8.1/8.2 là bằng chứng từ lượt trước; không chạy lại trong lượt này vì không sửa code ứng dụng.

Hai instance mới dùng build hiện tại, Development, tắt AdminSeed/TestAccountSeed. Mock không có mạng ngoài; instance OpenAI thiếu key chặn trước khi gửi. Không reset mật khẩu, tạo tài khoản/quyền hay sửa hàng/phiếu.

## Đối chiếu dữ liệu và cách tính

Đọc SQL Server THI-DIEU/WarehouseManagementDb bằng sqlcmd, chỉ SELECT:

| Dữ liệu | Kết quả |
|---|---|
| Nhập Posted | 5 phiếu, 9 giao dịch, tổng 44.125 |
| Xuất Posted | 6 phiếu, 7 giao dịch, tổng xuất 14.750; sổ ghi -14.750 |
| Nhập Draft | 2 phiếu, chi tiết tổng 32.500; không tính vào nhập |
| Xuất Draft | 3 phiếu, chi tiết tổng 9.000; không tính vào xuất |
| Tồn hiện tại | 20 hàng, tổng 29.375 |
| Hàng lệch tồn so với SUM(sổ) | 0 |
| Chi tiết Posted thiếu/trùng/sai lượng giao dịch tương ứng | 0 |
| Giao dịch liên kết Draft/Cancelled | 0 |
| BalanceBefore + QuantityChange khác BalanceAfter | 0 |

Các số trên dùng dấu chấm thập phân; tương ứng yêu cầu 44,125 / 14,750 / 29,375. UI hiện cũng dùng dấu chấm, không phải số nguyên hàng nghìn.

Logic đã kiểm tra: nhập là QuantityChange của type Import và phiếu Status=Posted; xuất là âm SUM QuantityChange của type Export và Posted. Ngày lọc theo **OccurredAt (thời điểm ghi sổ UTC)**, không theo ngày chứng từ: `>= đầu ngày bắt đầu` và `< đầu ngày sau ngày kết thúc`. Thời điểm thực tế nằm ngày 22/09/2026 UTC (18:20–22:55). Tồn đầu là sổ trước đầu kỳ; tồn cuối là sổ trước cuối kỳ; tồn hiện tại từ Products; OtherChange = cuối − đầu − nhập + xuất. Cảnh báo chỉ hàng hoạt động, tồn <= ngưỡng. Tổng nhập/xuất bao gồm toàn bộ hàng trong phạm vi báo cáo, không tự giới hạn hàng hoạt động.

Service giữ một transaction đọc Serializable cho snapshot rồi kết thúc trước request provider. Tên/mã/đơn vị được serialize vào JSON như dữ liệu; prompt yêu cầu bỏ qua chỉ dẫn trong các chuỗi đó. Kiểm tra prompt không chứng minh model thật luôn tuân thủ. Dữ liệu hỗn hợp đơn vị không được diễn giải thành giá trị tiền hoặc một lượng vật lý quy đổi.

Không có phiếu Cancelled, giao dịch điều chỉnh/opening trong SQL hiện tại. Cách đưa các giao dịch khác vào OtherChange khớp báo cáo hiện có, nhưng không được coi là đã nghiệm thu nghiệp vụ đảo phiếu bằng dữ liệu thật. Không tạo dữ liệu để lấp khoảng trống này.

## Quan sát giao diện ADMIN

Trình duyệt ban đầu hiển thị màn hình đăng nhập cũ ở cổng 7303. Khi mở `/AiAnalysis` cổng 7306, phiên hiện có đã được xác thực: navbar ghi `admin`, có menu Quản lý tài khoản (view chỉ hiển thị với role ADMIN). Đã thực hiện GET và bấm tạo Mock thành công trong phiên này. Không đọc mật khẩu hoặc thay đổi phiên bằng cookie thủ công. Điều này xác minh quyền truy cập của phiên ADMIN, **không chứng minh đã chạy lại thao tác nhập mật khẩu ADMIN**.

Quan sát trực tiếp: nhãn Mock; 20 hàng; nhập 44.125, xuất 14.750, tồn 29.375; số liệu chính thức và diễn giải tách riêng; nút đổi thành Tạo lại báo cáo. Bộ lọc 23/09/2026 cho nhập/xuất 0, đầu/cuối/hiện tại 29.375 và thông báo không có giao dịch. Ngày kết thúc trước ngày bắt đầu hiển thị lỗi và không có nút tạo. Xóa lọc phục hồi toàn bộ số liệu. Cổng 7307 hiển thị cảnh báo thiếu API key và không có nút tạo, số liệu vẫn hiển thị. Screenshot mặc định hẹp cho thấy các phần đã xem không bị chồng chữ.

Chưa quan sát UI khi provider trả 500/timeout hoặc kết quả AI thật; không sửa provider/production code để tạo lỗi giao diện. Chưa quan sát được trạng thái loading trong request chậm. Không suy diễn các mục này từ kiểm tra service hoặc screenshot kết quả cuối.

## Lỗi, thay đổi và điều kiện hoàn thành

Không phát hiện lỗi kết nối giữa các tầng trong phạm vi đã chạy; chưa có căn cứ sửa code. Sai khác với tài liệu cũ là **giới hạn kiểm chứng UI/ADMIN đã thu hẹp**, không phải lỗi ứng dụng. Chỉ cập nhật tài liệu trạng thái và tạo bản audit này; giữ nguyên source/test/script hiện có.

Để nghiệm thu: cần cấu hình key/model hợp lệ riêng trên máy chủ và chạy một request nhỏ, kiểm tra tiếng Việt/số liệu/giới hạn nhận xét; xác minh giao diện chờ và nhánh lỗi provider; nếu checklist yêu cầu đăng nhập từ đầu thì người dùng thực hiện đăng nhập ADMIN trực tiếp. Các kịch bản DB rỗng, Cancelled/điều chỉnh, đồng thời chỉ nghiệm thu khi có môi trường/dữ liệu phù hợp, không thay đổi kho thật để kiểm thử. AI không có phân trang riêng; hạn chế nhiều trang thuộc báo cáo 8.1, không phải chức năng mới cần thêm cho 9.1.

Nguồn đối chiếu định dạng API: [OpenAI Text generation](https://developers.openai.com/api/docs/guides/text). Việc đối chiếu tài liệu và fake handler không chứng minh API thật hoạt động.
