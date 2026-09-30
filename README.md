# WarehouseManagement – Hệ thống quản lý kho

Ứng dụng web quản lý kho xây dựng bằng **ASP.NET Core MVC**, cung cấp các nghiệp vụ quản lý hàng hóa, nhà cung cấp, nhập – xuất – tồn, lịch sử giao dịch, cảnh báo tồn thấp và báo cáo. Hệ thống có phân quyền theo vai trò và chức năng phân tích, gợi ý bằng AI ở mức tham khảo.

> **Phạm vi:** Dự án hiện quản lý một lượng tồn hiện tại cho mỗi sản phẩm; chưa có mô hình quản lý nhiều kho. Nội dung README dựa trên báo cáo khảo sát mã nguồn và tài liệu ngày **30/09/2026**. Các chức năng được liệt kê là **đã có trong mã nguồn**, không đồng nghĩa toàn bộ đã được chạy thử hoặc nghiệm thu ở môi trường mới.

## 1. Mục tiêu

- Quản lý danh mục hàng hóa và thông tin nhà cung cấp.
- Lập phiếu nhập, phiếu xuất và ghi nhận biến động tồn kho khi ghi sổ.
- Theo dõi số lượng tồn, lịch sử giao dịch và các mặt hàng chạm hoặc dưới ngưỡng tồn tối thiểu.
- Tổng hợp dashboard, báo cáo nhập – xuất – tồn và cung cấp nhận xét AI mang tính tham khảo.
- Phân quyền truy cập theo nhiệm vụ của người sử dụng.

## 2. Công nghệ

| Thành phần | Công nghệ |
|---|---|
| Ngôn ngữ | C# |
| Nền tảng | .NET 10, ASP.NET Core MVC |
| Giao diện | Razor Views, HTML, CSS, JavaScript, Bootstrap 5.3.3, jQuery 3.7.1 |
| Cơ sở dữ liệu | Microsoft SQL Server |
| Truy cập dữ liệu | Entity Framework Core / SQL Server provider 10.0.7 |
| Xác thực và phân quyền | ASP.NET Core Identity, cookie, role và authorization policy |
| Tích hợp AI tùy chọn | OpenAI hoặc Gemini qua HTTP; có chế độ Mock trong Development |

Dự án là một ứng dụng web MVC; các controller nghiệp vụ chủ yếu sử dụng `ApplicationDbContext`. Các dịch vụ phân tích AI và truy vấn báo cáo được tách thành service.

## 3. Chức năng

| Nhóm chức năng | Mô tả |
|---|---|
| Đăng nhập | Đăng nhập bằng tên tài khoản hoặc email; đăng xuất và ghi nhớ đăng nhập |
| Quản lý tài khoản | Xem, tạo, sửa, gán vai trò và khóa/mở khóa tài khoản |
| Danh mục | Xem, tạo, sửa và thay đổi trạng thái danh mục |
| Hàng hóa | Quản lý mã, tên, đơn vị, danh mục, mô tả, ngưỡng tồn và trạng thái |
| Nhà cung cấp | Quản lý mã, tên, liên hệ và trạng thái |
| Nhập kho | Tạo phiếu nháp nhiều dòng; ghi sổ để tăng tồn và tạo giao dịch |
| Xuất kho | Tạo phiếu nháp; kiểm tra tồn khi ghi sổ, giảm tồn và tạo giao dịch |
| Tồn kho và lịch sử | Tra cứu tồn, lọc, phân trang, xem giao dịch và đối chiếu biến động |
| Cảnh báo tồn thấp | Xem mặt hàng hết hàng hoặc chạm/dưới ngưỡng; xuất danh sách cảnh báo ra CSV |
| Dashboard và báo cáo | Chỉ số tổng hợp, biểu đồ nhập/xuất và báo cáo nhập – xuất – tồn chi tiết |
| Phân tích AI | Diễn giải số liệu kho và gợi ý xem xét nhập hàng, không tự thực hiện nghiệp vụ |

**Quy tắc nghiệp vụ:** Phiếu nháp không làm thay đổi hoặc giữ chỗ tồn kho. Chỉ khi ghi sổ, hệ thống mới cập nhật số lượng và ghi giao dịch. Phiếu xuất được kiểm tra lại tồn tại thời điểm ghi sổ. Hệ thống có xử lý xung đột đồng thời và ngăn ghi sổ trùng trong mã nguồn.

### Phân quyền

| Chức năng | Quản trị viên | Nhân viên kho | Kế toán |
|---|:---:|:---:|:---:|
| Quản lý tài khoản | ✓ | — | — |
| Xem danh mục | ✓ | ✓ | ✓ |
| Thay đổi danh mục | ✓ | — | — |
| Quản lý hàng hóa, nhà cung cấp | ✓ | ✓ | — |
| Quản lý phiếu nhập, phiếu xuất | ✓ | ✓ | — |
| Xem tồn kho, lịch sử, cảnh báo và dashboard | ✓ | ✓ | ✓ |
| Báo cáo chi tiết, xuất CSV cảnh báo | ✓ | — | ✓ |
| Phân tích và gợi ý AI | ✓ | — | ✓ |

## 4. Cơ sở dữ liệu

Hệ thống sử dụng **SQL Server** với tên cơ sở dữ liệu trong cấu hình và script là `WarehouseManagementDb`. Chuỗi kết nối được đọc từ `ConnectionStrings:DefaultConnection`.

Các bảng nghiệp vụ chính:

| Bảng | Chức năng |
|---|---|
| `Categories` | Danh mục hàng hóa |
| `Products` | Hàng hóa, tồn hiện tại, ngưỡng tồn và giá vốn bình quân |
| `Suppliers` | Nhà cung cấp |
| `ImportReceipts`, `ImportReceiptDetails` | Phiếu và chi tiết nhập |
| `ExportReceipts`, `ExportReceiptDetails` | Phiếu và chi tiết xuất |
| `InventoryTransactions` | Sổ giao dịch biến động tồn kho |

Ngoài ra còn có các bảng ASP.NET Core Identity và `__EFMigrationsHistory`. Các quan hệ chính: danh mục – hàng hóa; nhà cung cấp – phiếu nhập; phiếu – chi tiết phiếu; hàng hóa – chi tiết phiếu và giao dịch tồn kho.

Repository có hai **phương án khởi tạo CSDL thay thế nhau**:

- **EF Core migrations:** tạo schema mới; không tự nhập bộ dữ liệu mẫu trong snapshot SQL.
- **Script [`Database/WarehouseManagementDb.sql`](Database/WarehouseManagementDb.sql):** snapshot có schema và dữ liệu; chứa đường dẫn tệp SQL Server theo máy xuất nên phải kiểm tra, điều chỉnh trước khi sử dụng trên máy khác.

**Không chạy toàn bộ script SQL lên CSDL đã tạo schema bằng migrations.** Script có dữ liệu tài khoản Identity; không công bố dữ liệu tài khoản, password hash hoặc thông tin xác thực từ script.

## 5. Cài đặt và chạy ứng dụng

### 5.1. Yêu cầu

- **.NET SDK 10**.
- Một instance **SQL Server** mà máy chạy ứng dụng có quyền truy cập. Snapshot SQL hiện có hướng tới SQL Server 2022; phiên bản máy chủ đang sử dụng thực tế chưa được xác minh.
- Công cụ quản trị SQL Server như **SQL Server Management Studio (SSMS)** nếu cần chạy hoặc điều chỉnh script SQL.
- `dotnet-ef` tương thích với EF Core 10.0.7 **nếu** chọn tạo CSDL bằng migrations.
- Trình duyệt web. Cần kết nối mạng để tải package chưa được cache hoặc sử dụng dịch vụ AI thật.

### 5.2. Lấy mã nguồn, restore và build

```powershell
git clone https://github.com/dtc245200514-ux/WarehouseManagement.git
cd WarehouseManagement
dotnet restore WarehouseManagement.csproj
dotnet build WarehouseManagement.csproj --no-restore
```

### 5.3. Cấu hình kết nối SQL Server

Thiết lập `ConnectionStrings:DefaultConnection` theo instance SQL Server của bạn trong cấu hình môi trường phát triển, biến môi trường hoặc .NET User Secrets. Ví dụ **chỉ dành cho môi trường phát triển dùng Windows Authentication**:

```text
Server=<TEN_SQL_SERVER>;Database=WarehouseManagementDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True
```

Thay `<TEN_SQL_SERVER>` bằng instance thực tế. Tài khoản Windows chạy ứng dụng cần có quyền truy cập CSDL. Không commit mật khẩu hoặc chuỗi kết nối chứa bí mật vào Git.

### 5.4. Khởi tạo cơ sở dữ liệu

**Phương án A – CSDL mới bằng migrations** (sau khi cấu hình kết nối và cài `dotnet-ef` phù hợp):

```powershell
dotnet ef database update --project WarehouseManagement.csproj
```

Phương án này tạo/cập nhật schema nhưng **không** nhập dữ liệu mẫu từ snapshot SQL.

**Phương án B – sử dụng snapshot SQL:** kiểm tra file [`Database/WarehouseManagementDb.sql`](Database/WarehouseManagementDb.sql), đặc biệt các đường dẫn vật lý `.mdf`/`.ldf` và điều kiện tạo database; chỉ chạy trên môi trường phù hợp, với CSDL riêng. Không chạy lặp tùy ý và không kết hợp với phương án A trên cùng một schema đã được tạo.

### 5.5. Cấu hình tài khoản ban đầu

Ứng dụng tạo role khi khởi động. Việc tạo tài khoản ADMIN chỉ diễn ra khi bật `AdminSeed:Enabled` và cung cấp đầy đủ cấu hình `AdminSeed:UserName`, `AdminSeed:Email`, `AdminSeed:FullName`, `AdminSeed:Password` qua cơ chế cấu hình an toàn.

**Chú ý:** các launch profile hiện bật `TestAccountSeed__Enabled=true` nhưng không kèm mật khẩu kiểm thử. Nếu không cấu hình đầy đủ tài khoản kiểm thử, hãy tắt seed kiểm thử khi chạy. CSDL và schema phải được chuẩn bị trước; ứng dụng không tự chạy migrations lúc khởi động.

### 5.6. Chạy ứng dụng

Sau khi đã chuẩn bị CSDL và tài khoản đăng nhập phù hợp:

```powershell
dotnet run --project WarehouseManagement.csproj --launch-profile https -- --AdminSeed:Enabled=false --TestAccountSeed:Enabled=false
```

Các địa chỉ được khai báo trong launch profile:

- HTTPS: `https://localhost:7278`
- HTTP: `http://localhost:5138`

Lệnh trên **tắt tạo tài khoản mới**, không tự tạo tài khoản để đăng nhập. Nếu dùng CSDL mới và chưa có tài khoản, cần cấu hình seed ADMIN an toàn trước lần chạy đầu. Chạy HTTPS yêu cầu development certificate phù hợp.

### 5.7. AI (tùy chọn)

Nghiệp vụ kho cơ bản không yêu cầu AI. Có thể cấu hình `AI:Provider` là `Disabled`, `Mock` (chỉ Development), `OpenAI` hoặc `Gemini`. Với dịch vụ thật, cần cung cấp `AI:Model` và `AI:ApiKey` ở phía server qua User Secrets hoặc biến môi trường như `AI__Provider`, `AI__Model`, `AI__ApiKey`. Không commit API key vào repository. Xem thêm [`docs/AI-configuration.md`](docs/AI-configuration.md).

## 6. Cấu trúc dự án

```text
WarehouseManagement/
├── Authorization/       # Role và authorization policy
├── Controllers/         # Xử lý request và nghiệp vụ MVC
├── Data/                # DbContext, khởi tạo Identity, Migrations
├── Database/            # Script SQL snapshot
├── Models/              # Entity, enum, DTO, view model
├── Options/             # Lớp cấu hình
├── Services/
│   ├── Ai/              # Phân tích và tích hợp AI
│   └── Reporting/       # Truy vấn báo cáo
├── Views/               # Razor Views
├── wwwroot/             # CSS, JavaScript, hình ảnh, thư viện giao diện
├── Properties/          # Launch profiles
├── docs/                # Tài liệu và ghi nhận tiến độ/kiểm chứng
├── scripts/             # Script kiểm chứng
├── tests/               # Chương trình/host kiểm thử riêng
├── Program.cs
├── appsettings.json
├── appsettings.Development.json
├── WarehouseManagement.csproj
└── WarehouseManagement.slnx
```

## 7. Tình trạng và giới hạn hiện tại

Các nhóm chức năng ở mục 3 đã có đường xử lý trong mã nguồn, nhưng **chưa được xác nhận chạy và nghiệm thu toàn bộ** trong lần khảo sát README. Một số phần AI có kiểm thử hoặc xác nhận trong tài liệu ở các thời điểm khác nhau; không nên hiểu đó là xác nhận tất cả provider và tính năng AI hiện tại đều hoạt động trên mọi môi trường.

Những chức năng **chưa thấy luồng thao tác hoàn chỉnh** trong mã nguồn được khảo sát gồm: hủy phiếu nhập/xuất, đảo giao dịch hủy, điều chỉnh tồn bằng nghiệp vụ riêng, khởi tạo tồn đầu qua giao diện riêng, sửa/xóa phiếu nháp đã lưu và xuất báo cáo Excel/PDF. Dự án hiện không có bằng chứng hỗ trợ đa kho. AI chỉ đưa ra nhận xét và khuyến nghị, không tự tạo phiếu hoặc cập nhật tồn.

Các lệnh cài đặt trong README được đề xuất dựa trên cấu trúc mã nguồn; **chưa được kiểm chứng bằng một lần cài mới từ đầu** trong đợt khảo sát ngày 30/09/2026.

## 8. Nhóm phát triển

| STT | Họ và tên |
|---|---|
| 1 | Đặng Thị Diệu |
| 2 | Trần Thị Hương |
---

*Tài liệu được biên soạn dựa trên báo cáo khảo sát mã nguồn và thư mục `docs` ngày 30/09/2026.*

