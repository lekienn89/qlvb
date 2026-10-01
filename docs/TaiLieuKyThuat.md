# Tài liệu kỹ thuật

**Phần mềm Quản lý văn bản đi – đến**, phiên bản 1.0.0

Dành cho người bảo trì, phát triển tiếp hoặc thẩm định an toàn phần mềm.

---

## 1. Tổng quan

- Ứng dụng desktop **WPF trên .NET 10 (LTS)**, chạy ngoại tuyến trên một máy Windows 10/11 x64.
- Đóng gói self-contained: không cần cài .NET. Thư viện gốc (DLL) nằm cạnh `QLVB.exe`, không giải nén ra thư mục tạm khi chạy.
- Một người dùng, một bộ dữ liệu, một tiến trình (mutex chống mở hai lần).
- Không có mã kết nối mạng, không telemetry, không tự cập nhật.

### 1.1. Căn cứ pháp lý được mô hình hóa

| Thứ tự ưu tiên | Văn bản |
|---|---|
| 1 | Luật Bảo vệ bí mật nhà nước số 117/2025/QH15 |
| 2 | Nghị định 63/2026/NĐ-CP |
| 3 | Phụ lục III (mẫu sổ đăng ký BMNN đi, đến), thay thế theo Công văn 2663/VPCP-NC |
| Tham khảo | Thông tư 24/2020/TT-BCA (mẫu 14, 15): chỉ để đối chiếu lịch sử |

Quy tắc nghiệp vụ được cài đặt từ các căn cứ trên: đánh số theo năm, đăng ký văn bản đi trước khi phát hành, không ghi trích yếu cho tài liệu Tuyệt mật.

## 2. Cấu trúc mã nguồn

| Dự án | Vai trò | Phụ thuộc |
|---|---|---|
| `Qlvb.Domain` | Thực thể (`VanBanDi`, `VanBanDen`, `SoDangKy`, `BieuMau`…), kiểm tra hợp lệ, danh sách trường biểu mẫu | — |
| `Qlvb.Application` | Dịch vụ nghiệp vụ: đăng ký, sửa, hủy, xóa, danh mục, sổ, cấu hình, thống kê, dữ liệu mẫu; giao diện `IDataStore` | Domain |
| `Qlvb.Infrastructure` | CSDL mã hóa, di trú lược đồ, `KeyStore`, `AppSession`, sao lưu, xuất tệp, nhật ký kỹ thuật, chính sách nơi lưu | Application |
| `Qlvb.App` | Giao diện WPF tiếng Việt, in sổ (FlowDocument), bảo vệ clipboard, chặn chụp màn hình, tự khóa | Infrastructure |
| `tests/Qlvb.Tests` | Kiểm thử đơn vị và tích hợp (xUnit, chạy được trên Linux) | |
| `tests/Qlvb.UiTests` | Kiểm thử giao diện (FlaUI, chỉ Windows) | |

Phụ thuộc chỉ đi một chiều: App → Infrastructure → Application → Domain. Lớp nghiệp vụ không biết gì về SQLite hay WPF.

## 3. Bảo mật dữ liệu

### 3.1. Khóa và mật khẩu

- **Khóa dữ liệu (DEK)**: 256 bit, sinh bằng `RandomNumberGenerator`.
- DEK **không bao giờ ghi ra đĩa ở dạng rõ**. Tệp `qlvb.key` (JSON) chứa DEK được bọc hai lần, độc lập:
  - bằng khóa dẫn xuất từ **mật khẩu**: PBKDF2-HMAC-SHA256, 600.000 vòng, salt ngẫu nhiên 16 byte, bọc bằng AES-256-GCM;
  - bằng khóa dẫn xuất từ **mã khôi phục** (20 ký tự trên bảng chữ 31 ký tự, ~99 bit), cùng cơ chế.
- Đổi mật khẩu chỉ bọc lại DEK; dữ liệu không phải mã hóa lại.
- Chống dò mật khẩu: 5 lần sai miễn phí, sau đó trễ tăng theo cấp số nhân, tối đa 15 phút; bộ đếm lưu trong tệp khóa.
- Chỉ dùng thuật toán chuẩn của `System.Security.Cryptography`; **không có mã hóa tự chế**.

### 3.2. Cơ sở dữ liệu mã hóa

- **SQLite3 Multiple Ciphers** (gói `SQLite3MC.PCLRaw.bundle`), chế độ tương thích SQLCipher 4 (AES-256-CBC, HMAC-SHA512 theo trang).
- Khóa được truyền dạng byte qua `sqlite3_key` (không tạo chuỗi hex trong bộ nhớ). Mảng khóa được ghim (pinned) và ghi đè bằng 0 khi đăng xuất, thoát hoặc lỗi.
- PRAGMA: `foreign_keys=ON`, `journal_mode=WAL`, `synchronous=FULL`, `secure_delete=ON`.
- Mọi truy vấn dùng tham số; tên bảng và cột sắp xếp chỉ lấy từ danh sách cố định trong mã.
- Một kết nối duy nhất cho cả phiên, truy cập qua `lock(db.Gate)`.

### 3.3. Ràng buộc trong CSDL (trigger)

Các quy tắc quan trọng được bảo vệ ngay trong CSDL, không chỉ ở giao diện:

| Trigger | Bảo vệ |
|---|---|
| `tg_nhat_ky_no_update`, `tg_nhat_ky_no_delete` | Nhật ký thao tác chỉ được thêm |
| `tg_di_tm_*`, `tg_den_tm_*` | Văn bản Tuyệt mật không được có trích yếu (`CAM_TRICH_YEU`) |
| `tg_di_so_bat_bien`, `tg_den_so_bat_bien` | Không sửa số thứ tự, số đến, năm, quyển sổ sau khi đăng ký |
| `tg_di_khoa_*`, `tg_den_khoa_*` | Không thêm, sửa, xóa văn bản trong sổ đã khóa (`SO_DA_KHOA`) |

### 3.4. Nhật ký thao tác

Bảng `nhat_ky`, mỗi dòng có `hash = SHA-256(prev_hash | thời gian | hành động | đối tượng | id | người | mô tả | thay đổi)`. Hàm kiểm tra toàn vẹn duyệt lại toàn chuỗi và báo dòng đầu tiên bị sai. Nhật ký không ghi trích yếu.

### 3.5. Các biện pháp khác

| Biện pháp | Vị trí mã |
|---|---|
| Chặn đặt dữ liệu, sao lưu, tệp xuất trong thư mục tạm, Public, OneDrive/Dropbox/Google Drive/iCloud | `Infrastructure/Security/LocationPolicy.cs`, `App/Infrastructure/SafeLocation.cs` |
| Phân quyền thư mục dữ liệu (DACL chỉ người dùng hiện tại, SYSTEM, Administrators; chỉ ổ cố định) | `LocationPolicy.RestrictToCurrentUser` |
| Xóa clipboard do phần mềm chép khi khóa, đăng xuất, thoát | `App/Infrastructure/ClipboardGuard.cs` |
| Chặn chụp màn hình (`SetWindowDisplayAffinity`, `WDA_EXCLUDEFROMCAPTURE`), mặc định tắt | `App/Infrastructure/ScreenCaptureGuard.cs` |
| Tự khóa khi không thao tác | `App/Infrastructure/IdleLock.cs` |
| Nhật ký kỹ thuật che giá trị trong thông điệp lỗi | `Infrastructure/Services/Logging.cs` |
| Nạp DLL hệ thống chỉ từ System32 | `[DefaultDllImportSearchPaths(System32)]` trong `ScreenCaptureGuard` |
| Phát hiện đồng hồ máy bị lùi | `AppSession.KiemTraDongHo`: so với thao tác cuối trong nhật ký (lệch quá 5 phút) |

## 4. Lược đồ dữ liệu

| Bảng | Nội dung |
|---|---|
| `cau_hinh` | Cấu hình dạng khóa – giá trị (`ConfigKeys`) |
| `bieu_mau` | Định nghĩa mẫu sổ (JSON) theo mã và phiên bản |
| `do_mat` | Tuyệt mật / Tối mật / Mật, ký hiệu A/B/C |
| `danh_muc` | Loại văn bản, người ký, đơn vị, nơi nhận, cơ quan ban hành |
| `so_dang_ky` | Quyển sổ theo loại, năm, số quyển; trạng thái khóa |
| `bo_dem` | Bộ đếm số thứ tự (và số đến) theo loại sổ, năm |
| `van_ban_di`, `van_ban_di_noi_nhan` | Văn bản đi và các nơi nhận, ký nhận |
| `van_ban_den` | Văn bản đến |
| `nhat_ky` | Nhật ký thao tác (chuỗi băm) |

Di trú: các tệp `Migrations/Vnnn__ten.sql` nhúng trong assembly, áp dụng tuần tự trong một giao dịch; phiên bản đã áp dụng ghi ở bảng `schema_version`. Thất bại thì rollback, dữ liệu giữ nguyên. Dữ liệu có phiên bản lược đồ cao hơn phần mềm thì từ chối mở.

- `V001__khoi_tao.sql`: lược đồ ban đầu.
- `V002__chi_muc_kiem_tra_trung.sql`: cột và chỉ mục chuẩn hóa số ký hiệu để kiểm tra trùng nhanh.

### 4.1. Đánh số

- Số thứ tự cấp theo `bo_dem(loai, nam, ten)` trong cùng giao dịch ghi văn bản; bắt đầu lại từ 1 mỗi năm, tiếp nối giữa các quyển trong năm.
- Hủy văn bản: giữ số. Xóa văn bản nhập sai: nếu là số lớn nhất của năm thì lùi bộ đếm (`UPDATE … WHERE gia_tri = so`), số được cấp lại; nếu ở giữa thì để trống.

## 5. Biểu mẫu sổ cập nhật được

Mẫu sổ không viết cứng trong mã in. Mỗi mẫu là một tệp JSON trong `src/Qlvb.Infrastructure/Forms/`:

```json
{
  "Ma": "SO_DI_ND63_2026", "Loai": 1, "PhienBan": 1, "HieuLucTu": "2026-03-01",
  "TieuDe": "SỔ ĐĂNG KÝ BÍ MẬT NHÀ NƯỚC ĐI",
  "CanCu": "Phụ lục III … Nghị định số 63/2026/NĐ-CP …",
  "HuongDanTrangBia": ["(1) …", "(2) …"],
  "Cot": [ { "So": 1, "TieuDe": "Số thứ tự", "Truong": "so_thu_tu", "DoRong": 0.6, "CanGiua": true, "HuongDan": "…" } ]
}
```

- `Truong` phải là một trường phần mềm hiểu (`TruongBieuMau.TatCa`); `BieuMauValidator` từ chối mẫu tham chiếu trường lạ.
- Khi khởi động, các mẫu nhúng được nạp vào bảng `bieu_mau` (`INSERT OR IGNORE`). Sổ in theo mẫu có hiệu lực tại năm của sổ.
- **Khi quy định thay đổi**: thêm tệp JSON mới (mã mới hoặc `PhienBan` mới, `HieuLucTu` mới), chạy kiểm thử, phát hành bản cập nhật. Chỉ khi mẫu mới cần **trường dữ liệu mới** mới phải thêm cột qua một tệp di trú và thêm tên trường vào `TruongBieuMau`.

## 6. Sao lưu

Tệp `.qlvbak` là gói ZIP gồm đúng 3 thành phần:

| Thành phần | Nội dung |
|---|---|
| `manifest.json` | Ứng dụng, định dạng, phiên bản phần mềm và lược đồ, thời điểm, kích thước CSDL, SHA-256 của CSDL và tệp khóa |
| `qlvb.db` | Bản chụp CSDL **vẫn mã hóa** bằng DEK (tạo bằng SQLite Online Backup API, sau checkpoint WAL) |
| `qlvb.key` | Tệp khóa tại thời điểm sao lưu |

Khi khôi phục, phần mềm kiểm tra: đúng 3 thành phần; manifest và tệp khóa ≤ 64 KB; CSDL ≤ 4 GB và đúng kích thước khai báo; tỷ lệ nén ≤ 200; SHA-256 khớp; SHA-256 của tệp khóa khớp; mở được bằng mật khẩu nhập vào; `integrity_check` đạt; ổ đĩa còn trống ≥ 2 × CSDL + CSDL hiện tại + 50 MB. Dữ liệu hiện tại được tự sao lưu trước khi thay thế.

## 7. Build, kiểm thử, phát hành

### 7.1. Trên máy phát triển

```
dotnet test tests/Qlvb.Tests
dotnet publish src/Qlvb.App -c Release -o artifacts/publish
```

Biến `QLVB_PERF=20000` bật kiểm thử hiệu năng với 20.000 văn bản giả lập.

### 7.2. GitHub Actions (`.github/workflows/build.yml`, máy `windows-latest`)

1. Restore, build (cảnh báo bộ phân tích mức Recommended được coi là lỗi).
2. Kiểm thử đơn vị; kiểm thử hiệu năng 20.000 văn bản.
3. Publish self-contained win-x64; ký số `QLVB.exe` nếu có secret.
4. Kiểm thử giao diện FlaUI trên bản publish: thiết lập lần đầu, nhập liệu, in, xóa văn bản nhập sai, khóa màn hình, sao lưu…; ảnh chụp được đẩy lên nhánh `ci-screenshots`.
5. Tạo tài liệu HTML từ `docs/*.md` (`tools/build-docs.py`).
6. Đóng gói portable (ZIP) và bộ cài Inno Setup; ký số bộ cài nếu có secret.
7. Tính `SHA256SUMS.txt`; tải lên artifact `qlvb-windows`.
8. Khi đẩy tag `v*`: tạo **GitHub Release** kèm bộ cài, bản portable và `SHA256SUMS.txt`.

### 7.3. Ký số

`installer/sign.ps1` đọc `SIGN_PFX_BASE64`, `SIGN_PFX_PASSWORD` (secret) và `SIGN_TIMESTAMP_URL` (biến), gọi `signtool sign /fd SHA256` rồi `signtool verify /pa`, sau đó xóa tệp `.pfx` tạm. Không có secret thì bỏ qua.

### 7.4. Đánh số phiên bản

Phiên bản đặt ở `Directory.Build.props` (`<Version>`) và `installer/qlvb.iss` (`AppVersion`). Khi phát hành: sửa cả hai, cập nhật `CHANGELOG.md`, gắn tag `vX.Y.Z`.

## 8. Thư viện bên thứ ba

Xem `NOTICE.md`. Tất cả có giấy phép MIT hoặc Apache-2.0 (SQLite: public domain). Không có thư viện mạng, phân tích hay telemetry.

## 9. Hạn chế đã biết

- Mật khẩu người dùng nhập đi qua `PasswordBox` của WPF nên tồn tại ngắn hạn trong bộ nhớ dạng chuỗi.
- Không chống được người có quyền quản trị Windows hoặc mã độc trên chính máy.
- Bản in, tệp PDF, tệp Excel/CSV xuất ra không được phần mềm bảo vệ.
- Bộ cài chưa ký số cho tới khi cơ quan có chứng thư ký mã.
