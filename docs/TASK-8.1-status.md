# Task 8.1 — tiến độ và kiểm chứng ngày 23/09/2026

## Cập nhật mới nhất — phiên hoàn thiện kiểm thử còn thiếu

**Chưa đánh dấu Task 8.1 hoàn thành.** Đã đọc lại code và thay đổi từ phiên trước,
không triển khai lại dashboard/báo cáo, không tạo dữ liệu giả hoặc thay đổi policy.

### Đã chạy trong phiên này

- Build trước và sau kiểm thử: **0 warning, 0 error**. Output cuối:
  `bin/Task81FinalBuild/WarehouseManagement.dll`.
- Bộ HTTP regression mở rộng: **90 kiểm tra đạt**, gồm 78 kiểm tra cũ và 12
  kiểm tra bổ sung (mỗi status/assertion được tính một kiểm tra).
- Khách chưa đăng nhập bị chuyển Login trên cả Dashboard và Index.
- Đăng nhập thật WAREHOUSE_STAFF và ACCOUNTANT bằng tài khoản kiểm thử đã có;
  quyền vẫn đúng policy (staff không được mở báo cáo chi tiết).
- Không có hàng hóa phù hợp (`isActive=false`): 5 chỉ số phụ thuộc hàng hóa
  bằng 0, không dựng thanh biểu đồ, hiển thị thông báo không có giao dịch.
  Thẻ tổng danh mục vẫn là tổng danh mục hệ thống, đúng nhãn hiện tại.
- Báo cáo không có kết quả: thông báo rỗng, không có hàng dữ liệu hoặc phân trang;
  thử cả `page=2` trên tập rỗng.
- Giới hạn phân trang: `page=2` và `page=2147483647` trên 20 sản phẩm đều đưa về
  trang 1, giữ đúng 20 mã hàng theo cùng thứ tự, STT bắt đầu 1, không hiện nút
  trước/sau. `page=0` bị validation. **Không coi đây là kiểm thử trang thứ hai
  có dữ liệu**, vì kích thước trang là 20 và database chỉ có 20 sản phẩm.
- Bộ lọc ngày hợp lệ, đảo ngày, sai định dạng, ngày tối đa và giữ ngày khi mở
  lịch sử đều tiếp tục đạt. Đã kiểm tra HTML render qua HTTP; chưa thay thế cho
  kiểm tra trực quan sau đăng nhập trên trình duyệt.
- `git diff --check` không có lỗi khoảng trắng; thông báo LF/CRLF của Git không
  phải warning của build.

### Đối chiếu SQL và nghiệp vụ

Snapshot đầu/cuối không đổi: **20 sản phẩm, 16 giao dịch, nhập 44.125,
xuất 14.750, tồn 29.375; 0 lệch tồn** (dấu chấm là dấu thập phân).

| Nghiệp vụ | Bằng chứng thực tế |
| --- | --- |
| Nhập Draft | 2 phiếu, tổng chi tiết 32.500; không nằm trong nhập chính thức 44.125 |
| Nhập Posted | 5 phiếu, 9 dòng giao dịch, tổng chi tiết/giao dịch 44.125 |
| Xuất Draft | 3 phiếu; 2 phiếu có chi tiết với tổng 9.000; không nằm trong xuất chính thức 14.750 |
| Xuất Posted | 6 phiếu, 7 dòng giao dịch, tổng chi tiết/giao dịch 14.750 |
| Giao dịch gắn Draft | 0 |
| Cancelled/điều chỉnh/tồn đầu/đảo giao dịch | Không có bản ghi thực tế |

Đã đọc các action Create/Post trong ImportReceiptManagementController và
ExportReceiptManagementController, toàn bộ truy vấn báo cáo và phần đối chiếu
InventoryController. Hiện controller chỉ tạo Draft và ghi sổ Posted; không có
action hủy hoặc tạo điều chỉnh. Cancelled, OpeningBalance, AdjustmentIncrease,
AdjustmentDecrease, ImportCancellation, ExportCancellation mới được khai báo
ở enum/schema. Không tạo action, phiếu hoặc bút toán để ép đạt kiểm thử.

Hai cột nhập/xuất và timeline lọc Posted. Tồn đầu/cuối cộng toàn bộ sổ, còn
chênh lệch được hiện ở Biến động khác — thống nhất cách đối chiếu hiện có ở
InventoryController. Cần kiểm chứng riêng khi nghiệp vụ hủy/điều chỉnh được
triển khai; chưa khẳng định đúng cho lịch sử hủy qua nhiều kỳ.

### ADMIN và giao diện — giới hạn còn lại

- SQL xác nhận có 1 tài khoản ADMIN đang hoạt động. Program.cs cho ADMIN cả
  ViewInventory và ViewReports; action sử dụng đúng các policy này.
- User-secrets không có AdminSeed:Password. Không đoán/reset mật khẩu, tạo
  tài khoản, đổi role hoặc giả cookie ADMIN. Đã đề nghị người dùng đăng nhập
  vào bản kiểm thử localhost; **chưa có bằng chứng đăng nhập ADMIN thành công**.
- Đã mở trực tiếp `http://127.0.0.1:7302/Reports/Dashboard` bằng trình duyệt:
  trang chuyển đúng tới Login với ReturnUrl. Chưa có phiên trình duyệt đã
  đăng nhập để kiểm tra trực quan dashboard/báo cáo và responsive.
- Thử mở HTML snapshot cục bộ bị chính sách URL của trình duyệt chặn. Không
  lách chính sách bằng cách phục vụ lại snapshot hoặc dùng trình duyệt khác.
  Snapshot không được dùng làm bằng chứng đã kiểm tra giao diện.
- Đã kiểm thử **database không có dữ liệu phù hợp** qua bộ lọc. Chưa thử
  database hoàn toàn rỗng; không xóa dữ liệu thật để thực hiện tình huống này.

### File thay đổi trong phiên này

- `scripts/Verify-Task81.ps1`: thêm kiểm tra khách trên báo cáo, biên phân trang,
  chỉ số/biểu đồ trên tập rỗng và trang vượt giới hạn trên tập rỗng.
- `docs/TASK-8.1-status.md`: cập nhật bằng chứng và các giới hạn trên.
- **Không sửa thêm code ứng dụng.** Hai thay đổi ReportsController.cs và
  Views/Reports/Index.cshtml trong working tree là sửa lỗi từ phiên trước;
  lần này đã kiểm thử hồi quy đạt.

### Bước tiếp tục cụ thể

1. Người dùng đăng nhập ADMIN ở bản localhost trong trình duyệt đang mở; xác
   minh vào Dashboard/Index và kiểm tra bộ lọc, bảng, biểu đồ trên desktop/mobile.
2. Kiểm thử trang thứ hai khi có hơn 20 hàng hóa thực; không thêm hàng giả.
3. Database hoàn toàn rỗng và nghiệp vụ hủy/điều chỉnh còn chưa chạy; ghi riêng
   với trường hợp lọc rỗng và Draft/Posted đã đạt, không gộp thành “đã hoàn tất”.

Các mục dưới đây giữ lại lịch sử của phiên kiểm thử trước.

## Điểm tiếp tục đã xác định

Phiên trước đã khảo sát database, tạo ReportsController, ReportViewModels,
ba view Reports, menu và CSS biểu đồ. Build thành công nhưng phiên bị ngắt
trong bước kiểm thử runtime. Phiên này tiếp tục phần 13: kiểm tra và đối chiếu.

**Trạng thái:** chức năng cốt lõi đã chạy và đối chiếu với SQL Server thật;
chưa xác nhận toàn bộ Task 8.1 hoàn tất vì còn các trường hợp kiểm chứng bên dưới.

## File đã kiểm tra

- Controllers/ReportsController.cs; Controllers/AccountController.cs;
  Controllers/ImportReceiptManagementController.cs;
  Controllers/ExportReceiptManagementController.cs.
- Models/Reporting/ReportViewModels.cs; Models/InventoryTransaction.cs;
  Models/Enums/ReceiptStatus.cs; Models/Enums/InventoryTransactionType.cs.
- Data/ApplicationDbContext.cs; Data/IdentityDataInitializer.cs.
- Authorization/ApplicationPolicies.cs; Program.cs.
- Views/Reports/Dashboard.cshtml; Views/Reports/Index.cshtml;
  Views/Reports/_Filters.cshtml; Views/Shared/_Layout.cshtml.
- WarehouseManagement.csproj; appsettings.json; appsettings.Development.json.
- Lịch sử công việc trước và schema thực tế của SQL Server THI-DIEU,
  database WarehouseManagementDb.

## Thay đổi trong phiên tiếp tục

- Controllers/ReportsController.cs: không gọi AddDays(1) khi ngày kết thúc
  là 9999-12-31; hiển thị lỗi validation thay cho lỗi runtime.
- Views/Reports/Index.cshtml: sửa định dạng Biến động khác từ custom format
  không hợp lệ `+N3;-N3;N3` sang `N3`; giữ fromDate/toDate khi mở lịch sử.
- scripts/Verify-Task81.ps1: kiểm chứng HTTP, dữ liệu và quyền, không ghi dữ liệu kho.
- docs/TASK-8.1-status.md: ghi điểm tiếp tục, kết quả và giới hạn.

Không thay entity, schema, migration, policy hay nghiệp vụ ghi sổ.

## Chức năng hiện có

- Dashboard /Reports/Dashboard: sáu chỉ số; lọc ngày UTC, hàng hóa, danh mục,
  trạng thái; thống kê ngày/tháng; số phiếu DISTINCT; biểu đồ CSS tối đa 12 mốc
  gần nhất có phát sinh; thông báo khi không có giao dịch.
- Báo cáo /Reports/Index: mã, tên, danh mục, đơn vị; tồn đầu/nhập/xuất/
  biến động khác/tồn cuối/tồn hiện tại/tồn tối thiểu; tìm mã hoặc tên;
  sắp xếp nhập/xuất trong SQL; phân trang 20 hàng; liên kết tồn kho và lịch sử.
- Không có xuất Excel/PDF hoặc tính giá vốn. Biểu đồ tồn theo danh mục và top
  mặt hàng là đề xuất tùy chọn trong yêu cầu, chưa triển khai.

## Nguồn số liệu và công thức

- Products.CurrentQuantity là tồn hiện tại; InventoryTransactions là sổ giao dịch.
- Nhập: SUM(QuantityChange) với TransactionType=Import và ImportReceipt.Status=Posted.
- Xuất: -SUM(QuantityChange) với TransactionType=Export và ExportReceipt.Status=Posted.
- Ngày dựa trên OccurredAt (datetime2, UTC), từ đầu ngày bắt đầu (>=) đến trước
  đầu ngày sau ngày kết thúc (<). Không tự đặt ngày mặc định.
- Tồn đầu = tổng biến động sổ trước ngày bắt đầu (0 khi không đặt từ ngày).
- Tồn cuối = tồn đầu + biến động sổ trong kỳ.
- Biến động khác = biến động sổ trong kỳ - nhập + xuất.
- Với dữ liệu hiện tại chỉ có nhập/xuất Posted, biến động khác bằng 0:
  tồn cuối = tồn đầu + nhập - xuất.
- Draft/Cancelled không nằm trong hai cột nhập/xuất chính thức. Tồn lịch sử
  hiện cộng sổ giao dịch đầy đủ, bao gồm opening/adjustment/reversal nếu có;
  chưa kiểm chứng tình huống hủy phiếu và đảo giao dịch vì database chưa có.
  Cần thống nhất cách trình bày lịch sử khi bổ sung nghiệp vụ hủy; không tự sửa
  nghiệp vụ hoặc lọc bỏ bút toán đảo vì có thể làm sai đối chiếu tồn.
- Chỉ thống kê số lượng, không suy ra giá trị tiền tệ. UI ghi rõ tổng nhiều
  đơn vị tính không phải một đại lượng vật lý đồng nhất.

## Quyền hiện hành

| Vai trò | Dashboard (ViewInventory) | Báo cáo (ViewReports) |
| --- | --- | --- |
| ADMIN | Có, kiểm tra cấu hình | Có, kiểm tra cấu hình |
| WAREHOUSE_STAFF | HTTP 200 đã kiểm chứng | Chuyển AccessDenied đã kiểm chứng |
| ACCOUNTANT | HTTP 200 đã kiểm chứng | HTTP 200 đã kiểm chứng |
| Chưa đăng nhập | Chuyển Login đã kiểm chứng | Policy yêu cầu đăng nhập |

Không mở rộng quyền WAREHOUSE_STAFF vượt policy báo cáo hiện có.

## Truy vấn đã kiểm chứng

Ứng dụng dùng EF Core LINQ async, AsNoTracking; Count/Sum/GroupBy trên SQL Server,
DISTINCT ID phiếu, sắp xếp trước Skip/Take, chỉ tổng hợp giao dịch cho ID sản phẩm
trong trang báo cáo. Danh sách chọn chỉ lấy ID và nhãn, hiện tải tất cả lựa chọn.

Đối chiếu độc lập bằng SELECT:

```sql
SELECT TransactionType, COUNT(*) N, SUM(QuantityChange) Quantity,
       MIN(OccurredAt) FirstAt, MAX(OccurredAt) LastAt
FROM InventoryTransactions GROUP BY TransactionType;

SELECT SUM(d.Quantity) Quantity
FROM ImportReceiptDetails d JOIN ImportReceipts r ON r.Id=d.ImportReceiptId
WHERE r.Status=1;

SELECT SUM(d.Quantity) Quantity
FROM ExportReceiptDetails d JOIN ExportReceipts r ON r.Id=d.ExportReceiptId
WHERE r.Status=1;

SELECT COUNT(*) Mismatches FROM Products p
WHERE CurrentQuantity <> COALESCE((SELECT SUM(QuantityChange)
 FROM InventoryTransactions t WHERE t.ProductId=p.Id),0);
```

Ngoài ra đã đối chiếu từng chi tiết phiếu Posted với giao dịch cùng phiếu/sản phẩm,
kiểm tra giao dịch gắn Draft, tổng hợp từng hàng hóa/danh mục và kiểu cột qua
INFORMATION_SCHEMA.COLUMNS. Tất cả là truy vấn chỉ đọc.

## Kết quả thực tế

- Build output riêng bin/Task81Resume: thành công, 0 warning, 0 error.
- scripts/Verify-Task81.ps1: **78 kiểm tra đạt** (bao gồm status HTTP và assertion
  số liệu; không phải 78 kịch bản độc lập).
- 20 hàng hóa; 16 giao dịch: 9 dòng nhập thuộc 5 phiếu Posted, 7 dòng xuất thuộc
  6 phiếu Posted. Có 2 phiếu nhập Draft và 3 phiếu xuất Draft.
- Nhập 44.125; xuất 14.750; tồn 29.375 (dấu chấm ở đây là dấu thập phân).
- Cả 20 hàng hóa thỏa phương trình tồn và tồn cuối toàn lịch sử bằng tồn hiện tại.
- 0 lệch tồn; 0 lệch chi tiết nhập; 0 lệch chi tiết xuất; 0 giao dịch gắn Draft.
- HH002: nhập 15.000, xuất 13.750, tồn 1.250. HH005 không phát sinh vẫn hiện.
- Giao dịch hiện có đều nằm ngày 22/09/2026 UTC. Lọc trọn ngày đó cho đủ số liệu;
  ngày 23/09 không phát sinh nhưng tồn đầu/cuối được chuyển sang đúng.
- Tồn cuối trước 22/09 bằng 0, phân biệt đúng với tồn hiện tại.
- Lọc hàng/danh mục/tìm mã, sắp xếp nhập/xuất, trạng thái không có kết quả,
  ngày sai/đảo/vượt giới hạn, ID sai, sort sai, page=0 đều đã kiểm chứng.
- Snapshot sau kiểm thử vẫn 20 hàng hóa, 16 giao dịch, tổng tồn 29.375.
- git diff --check không có lỗi khoảng trắng.

Script xác minh dùng snapshot dữ liệu trên; các số kỳ vọng cần cập nhật nếu
người dùng nhập/xuất thêm. Mật khẩu lấy từ user-secrets, không in hoặc lưu vào file.
Đăng nhập có thể cập nhật metadata Identity; các request nghiệp vụ chỉ GET.

## Các kiểm chứng còn lại / bước kế tiếp

1. ADMIN mới xác minh policy; chưa xác minh bằng đăng nhập ADMIN thực tế.
2. Đã thử kết quả lọc rỗng, chưa thử database hoàn toàn rỗng.
3. Có đúng 20 hàng nên chưa thử trang thứ hai với dữ liệu thật.
4. Chưa có Cancelled, OpeningBalance, Adjustment hoặc reversal để kiểm chứng
   cách trình bày số liệu lịch sử qua các tình huống này.
5. Chưa kiểm tra trực quan giao diện ở nhiều kích thước màn hình.

Không tạo dữ liệu giả vào database chính để lấp các trường hợp thiếu. Bước tiếp
theo là kiểm chứng những trường hợp này trong môi trường kiểm thử riêng hoặc với
dữ liệu/tài khoản thực phù hợp; không cần triển khai lại các chức năng đã có.
