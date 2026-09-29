# Task 8.2 — Cảnh báo tồn kho và xuất CSV

Ngày kiểm chứng: 23/09/2026. **Đã triển khai và kiểm thử dữ liệu; chưa đánh dấu
hoàn thành toàn bộ vì thiếu kiểm thử đăng nhập ADMIN và giao diện trực quan.**

## Khảo sát trước khi triển khai

- ASP.NET Core MVC .NET 10, EF Core SQL Server 10.0.7.
- Kiến trúc hiện tại truy vấn DbContext trực tiếp trong controller; không có
  tầng Service/Repository cho tồn kho. Giữ kiến trúc đó, tách phần cảnh báo
  thành một file partial của chính InventoryController.
- Đã đọc InventoryController, ReportsController, InventoryViewModels,
  Product, ApplicationDbContext, enum, các view tồn kho/báo cáo, menu,
  Program.cs, ApplicationRoles/Policies, csproj và trạng thái Task 8.1.
- Database THI-DIEU/WarehouseManagementDb: Products lưu CurrentQuantity và
  MinimumStockLevel kiểu decimal(18,3), Code, Name, Unit, CategoryId, IsActive.
  CategoryId liên kết Categories. Không có bảng tồn riêng cần tạo.
- Ràng buộc SQL: CurrentQuantity >= 0 và MinimumStockLevel >= 0.
- Chưa có thư viện/cơ chế xuất báo cáo. Chọn CSV UTF-8 BOM, không thêm package.

## Quy tắc đã chọn và lý do

Code tồn kho cũ, chi tiết tồn kho và Dashboard đều dùng **<= ngưỡng**, không
phải <. Task 8.2 yêu cầu tôn trọng quy tắc nghiệp vụ hiện có nếu khác ví dụ.
Vì vậy giữ nguyên hành vi và đưa về StockLevelRules dùng chung:

| Điều kiện | Kết quả |
| --- | --- |
| Tồn = 0, kể cả ngưỡng = 0 | Hết hàng |
| Tồn > 0 và tồn <= ngưỡng | Chạm hoặc dưới mức tối thiểu |
| Tồn > ngưỡng | Bình thường, không vào danh sách cảnh báo |
| IsActive = false | Không vào danh sách cảnh báo |

Ví dụ 10/10 **có cảnh báo** theo nghiệp vụ cũ; 15/10 không cảnh báo.
Giao diện giải thích rõ bằng ngưỡng vẫn cảnh báo. Không tự đổi quy tắc cũ sang <.
Chênh lệch = CurrentQuantity - MinimumStockLevel; số âm của chênh lệch thể hiện
mức thiếu, không phải tồn kho âm. Không tính lại tồn từ phiếu hay cộng Draft.

## Chức năng đã triển khai

- GET /Inventory/LowStock: danh sách hàng hoạt động cảnh báo, hết hàng trước,
  sau đó mã/ID ổn định; phân trang 20 hàng.
- Hiển thị mã, tên, danh mục, đơn vị, tồn hiện tại, ngưỡng, chênh lệch, nhãn
  trạng thái và link tới /Inventory/Details/{id}. Trang chi tiết đã có lịch sử.
- Lọc theo mã/tên, danh mục có thật, tất cả cảnh báo/hết hàng/chạm hoặc dưới
  ngưỡng. Có nút xóa lọc và giữ điều kiện khi phân trang/xuất.
- Ba thẻ tổng hợp đúng tập sau lọc: tổng cảnh báo, hết hàng, chạm/dưới ngưỡng.
- Validation từ khóa tối đa 200 ký tự, danh mục và trạng thái hợp lệ, trang
  hợp lệ. Lỗi lọc không trả dữ liệu đầy đủ hoặc cho tải CSV không lọc.
- GET /Inventory/ExportLowStock: CSV toàn bộ tập sau lọc, không chỉ trang
  hiện tại; đủ 8 cột, UTF-8 BOM, số thập phân invariant 3 chữ số, tên file có
  ngày giờ UTC. Tập rỗng xuất file chỉ chứa tiêu đề. Giới hạn 10.000 dòng;
  nếu vượt giới hạn trả lỗi yêu cầu thu hẹp lọc, không cắt âm thầm.
- Văn bản CSV được quote/escape và chặn chuỗi có thể bị hiểu thành công thức;
  số chênh lệch âm vẫn là số. Header Cache-Control: no-store.
- Lỗi truy vấn SQL trả thông báo chung (503), không đưa chi tiết kết nối ra
  giao diện; log lỗi ở server. CSV lọc sai trả 400, không tạo file.
- Thêm menu và link từ màn hình tồn kho. Không đổi toàn bộ Dashboard.

## Quyền

| Vai trò | Xem cảnh báo/chi tiết | Xuất CSV |
| --- | --- | --- |
| ADMIN | ViewInventory: có theo policy | ViewInventory + ViewReports: có theo policy |
| WAREHOUSE_STAFF | HTTP 200 đã kiểm thử | Chuyển AccessDenied, không hiện nút xuất |
| ACCOUNTANT | HTTP 200 đã kiểm thử | HTTP 200, file CSV đã đọc và đối chiếu |
| Khách | Chuyển Login | Chuyển Login |

Không thay Program.cs, role, policy hay tài khoản. Xuất được coi là báo cáo
nên giữ giới hạn ViewReports hiện có; không tự mở quyền xuất cho staff.
ADMIN chưa đăng nhập thực tế do thiếu mật khẩu cấu hình; không giả cookie,
đổi/reset mật khẩu hoặc cấp role để vượt bước này.

## Kết quả kiểm thử

- Build ứng dụng và project kiểm tra: thành công **0 warning, 0 error** với
  output riêng. Lần chạy test đầu vào output mặc định bị khóa apphost vì ứng
  dụng của người dùng đang chạy; đã xử lý bằng output riêng, không dừng app đó.
- **35 kiểm tra độc lập đạt**: 0/10, 5/10, 10/10, 15/10, 0/0, 0.001/0,
  10.007/10.008; phân loại/predicate/bộ lọc thống nhất; CSV dấu phẩy, quote,
  newline, formula-like text, tiếng Việt, BOM, số theo culture vi-VN, tập rỗng;
  thuộc tính policy trên controller/action xuất. Những giá trị minh họa này
  chỉ tồn tại trong bộ nhớ của test, không được ghi vào database.
- **283 kiểm tra HTTP/CSV/SQL đạt**: mã, tên tiếng Việt, danh mục, từng trạng
  thái, kết hợp, kết quả rỗng, xóa lọc, page=2/int.MaxValue, ID/trạng thái/từ
  khóa/trang sai; ba thẻ tổng hợp; từng trường của từng dòng HTML và CSV so
  với snapshot SQL; quyền khách/staff/accountant; chi tiết và bộ lọc tồn kho cũ.
- **90 kiểm tra hồi quy Task 8.1 đạt** trên bản mới: dashboard, báo cáo,
  thời gian, quyền đã có tài khoản, phương trình tồn, validation, tập rỗng.
- Các con số trên là số assertion/status check, không phải số kịch bản độc lập.
- Snapshot Products trước/sau được so sánh từng trường trong script: không đổi.
  Kiểm thử chỉ GET dữ liệu kho; đăng nhập có thể cập nhật metadata Identity.

### Đối chiếu SQL Server

```sql
SELECT COUNT(*) Alerts,
 SUM(CASE WHEN CurrentQuantity=0 THEN 1 ELSE 0 END) OutOfStock,
 SUM(CASE WHEN CurrentQuantity>0 THEN 1 ELSE 0 END) LowStock
FROM Products WHERE IsActive=1 AND CurrentQuantity<=MinimumStockLevel;

SELECT p.Id,p.Code,p.Name,c.Name CategoryName,p.Unit,p.IsActive,
       p.CurrentQuantity,p.MinimumStockLevel,p.CategoryId
FROM Products p JOIN Categories c ON c.Id=p.CategoryId ORDER BY p.Id;

SELECT COUNT(*) Mismatches FROM Products p
WHERE CurrentQuantity<>COALESCE((SELECT SUM(QuantityChange)
 FROM InventoryTransactions t WHERE t.ProductId=p.Id),0);
```

| Số liệu | Kết quả |
| --- | ---: |
| Cảnh báo | 20 |
| Hết hàng | 12 |
| Tồn dương chạm/dưới ngưỡng | 8 |
| Tổng hàng hóa | 20 |
| Giao dịch | 16 |
| Tồn hiện tại/tổng biến động | 29.375 |
| Hàng hóa lệch tồn | 0 |

Nhập 44.125 và xuất 14.750 vẫn khớp Task 8.1; dấu chấm ở đây là thập phân.
Không có hàng bằng hoặc trên ngưỡng, ngừng hoạt động trong dữ liệu hiện tại.

## File mới/chỉnh sửa

Mới:

- Controllers/InventoryController.LowStock.cs
- Models/InventoryManagement/StockLevelRules.cs
- Models/InventoryManagement/LowStockViewModel.cs
- Models/InventoryManagement/LowStockCsv.cs
- Views/Inventory/LowStock.cshtml
- scripts/Verify-Task82.ps1
- tests/Task82Checks/Task82Checks.csproj và Program.cs
- docs/TASK-8.2-status.md

Sửa:

- Controllers/InventoryController.cs: partial, logger, tái sử dụng quy tắc cũ.
- Controllers/ReportsController.cs: thẻ cảnh báo dùng chung predicate, không
  đổi cách tính hoặc kết quả. Sửa ngày cực đại đã có từ Task 8.1 được giữ lại.
- Views/Inventory/Index.cshtml; Views/Shared/_Layout.cshtml: link cảnh báo.
- WarehouseManagement.csproj: loại mã/nội dung test khỏi build/publish web.

Không thay entity, DbContext, migration, database schema, luồng nhập/xuất,
phân quyền hoặc thư viện. Views/Reports/Index.cshtml và script/docs Task 8.1
là thay đổi có sẵn từ phiên trước, không triển khai lại.

## Chạy lại

Từ thư mục dự án, build output riêng để không đụng ứng dụng đang chạy:

```powershell
dotnet build WarehouseManagement.csproj --no-restore -p:UseAppHost=false -p:OutputPath=bin/Task82Final/
dotnet build tests/Task82Checks/Task82Checks.csproj --no-restore -p:UseAppHost=false -p:OutputPath=D:/KHOAI/WarehouseManagement/bin/Task82Checks/
dotnet bin/Task82Checks/Task82Checks.dll
./scripts/Verify-Task82.ps1 -BaseUrl http://127.0.0.1:7303
./scripts/Verify-Task81.ps1 -BaseUrl http://127.0.0.1:7303
```

Phải chạy bản web tương ứng trước HTTP test, tắt AdminSeed/TestAccountSeed.
Verify-Task82 lấy snapshot SQL thật và mật khẩu từ user-secrets, không in mật
khẩu. Một số case chọn mã/tên/danh mục của bộ dữ liệu hiện tại nên cần cập nhật
case nếu dữ liệu thay đổi. CSV và log kiểm thử lưu dưới bin/Task82Evidence,
không đưa dữ liệu sinh ra vào mã nguồn.

## Chưa kiểm chứng / điểm tiếp tục

1. ADMIN: đã xác minh policy, chưa kiểm thử đăng nhập thực tế.
2. Giao diện trực quan desktop/mobile và thao tác bằng trình duyệt: mới xác
   minh chuyển tới Login ở /Inventory/LowStock trên cổng 7303. HTML/table/form
   được kiểm tra qua HTTP nhưng không thay thế cho kiểm tra trực quan. Đã mở
   trang chờ người dùng đăng nhập, không bỏ qua authentication.
3. Trang thứ hai có dữ liệu: chỉ có đúng 20 cảnh báo; đã thử trang vượt giới
   hạn, chưa có dữ liệu để kiểm tra chuyển trang thật. Không tạo hàng giả.
4. Inactive, bằng/trên ngưỡng: code loại/lọc đã đọc; các biên số học được test
   trong bộ nhớ, chưa có ví dụ thực tế tương ứng để đối chiếu SQL end-to-end.
5. Giới hạn 10.000 CSV và lỗi database 503 chưa được kích hoạt bằng kiểm thử
   lỗi thực tế. Không làm gián đoạn SQL Server đang sử dụng để thử.

Không phát hiện sai lệch số liệu trong phạm vi đã chạy. Không tuyên bố toàn bộ
Task 8.2 hoàn thành trước khi xác minh những phần quan trọng còn thiếu.
