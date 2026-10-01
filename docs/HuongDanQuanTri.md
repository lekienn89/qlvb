# Hướng dẫn quản trị

**Phần mềm Quản lý văn bản đi – đến**, phiên bản 1.0.0

Tài liệu dành cho người cài đặt, quản lý máy tính và người phụ trách bảo vệ bí mật nhà nước của cơ quan.

---

## 1. Yêu cầu máy tính

- Windows 10 hoặc Windows 11, bản 64 bit.
- Không cần cài .NET hay phần mềm nào khác: mọi thứ cần thiết đã có sẵn trong bộ cài.
- Không cần quyền quản trị (Administrator) để cài đặt.
- Phần mềm **không kết nối mạng**, không gửi dữ liệu đi đâu.
- Máy dùng để soạn thảo, lưu trữ bí mật nhà nước phải tuân thủ quy định của cơ quan: không kết nối Internet, có phần mềm diệt mã độc, quản lý chặt thiết bị lưu trữ ngoài.

## 2. Cài đặt

### 2.1. Kiểm tra tệp tải về

Mỗi bản phát hành có tệp `SHA256SUMS.txt`, ghi mã kiểm tra của bộ cài và bản portable. Trên máy đã tải, mở PowerShell trong thư mục chứa tệp và chạy:

```
Get-FileHash .\QLVB-Setup-1.0.0-win-x64.exe -Algorithm SHA256
```

Mã in ra (không phân biệt chữ hoa, chữ thường) phải **trùng khớp** với dòng tương ứng trong `SHA256SUMS.txt`. Không khớp thì không cài, và tải lại.

### 2.2. Bản cài đặt

1. Chạy `QLVB-Setup-1.0.0-win-x64.exe`.
2. Vì bộ cài chưa có chữ ký số, Windows có thể hiện **"Windows protected your PC"**. Bấm **More info → Run anyway** (Thông tin thêm → Vẫn chạy). Chỉ làm vậy khi mã SHA-256 đã khớp.
3. Làm theo các bước của bộ cài. Mặc định phần mềm được cài vào `%LOCALAPPDATA%\Programs\QLVB` và chỉ cho tài khoản Windows đang dùng.

Dữ liệu được lưu riêng tại **`%LOCALAPPDATA%\QLVB`** (thường là `C:\Users\<tên tài khoản>\AppData\Local\QLVB`).

### 2.3. Bản portable (không cần cài)

1. Giải nén `QLVB-portable-win-x64.zip` vào một **thư mục cố định của máy**, ví dụ `D:\QLVB`.
2. Chạy `QLVB.exe`.

Dữ liệu nằm trong thư mục `Data` ngay cạnh `QLVB.exe`. Phần mềm nhận biết bản portable nhờ tệp `portable.flag`; **không xóa tệp này**, nếu không phần mềm sẽ tìm dữ liệu ở `%LOCALAPPDATA%\QLVB`.

Bản portable **từ chối chạy** nếu đặt trong thư mục tạm, thư mục dùng chung (Public) hoặc thư mục đồng bộ đám mây (OneDrive, Dropbox, Google Drive, iCloud). Giải nén thẳng vào Desktop hay Documents cũng có thể bị chặn nếu các thư mục này được OneDrive đồng bộ.

### 2.4. Nên chọn bản nào?

| | Bản cài đặt | Bản portable |
|---|---|---|
| Có lối tắt trong menu Start, gỡ cài đặt được | Có | Không |
| Dữ liệu nằm ở | `%LOCALAPPDATA%\QLVB` | Thư mục `Data` cạnh `QLVB.exe` |
| Dùng cho | Máy tính dùng hằng ngày | Trường hợp không được phép cài phần mềm |

Không chạy cả hai bản trên cùng một dữ liệu. Chuyển từ bản này sang bản kia thì dùng **sao lưu và khôi phục** (mục 5).

## 3. Thư mục dữ liệu

| Thư mục | Nội dung |
|---|---|
| `Database\qlvb.db` | Cơ sở dữ liệu, **mã hóa toàn bộ** (AES-256) |
| `Database\qlvb.key` | Tệp khóa: khóa dữ liệu được bọc bằng mật khẩu và mã khôi phục |
| `Backup\` | Bản sao lưu `.qlvbak` (được mã hóa) |
| `Logs\` | Nhật ký kỹ thuật để xử lý sự cố |
| `Export\` | Nơi lưu mặc định cho tệp xuất Excel/CSV |
| `Config\` | Dự phòng cho cấu hình |

Lưu ý quan trọng:

- **Hai tệp `qlvb.db` và `qlvb.key` phải đi cùng nhau.** Mất tệp khóa thì không mở được dữ liệu, kể cả có mật khẩu. Không tự chép tay hai tệp này; hãy dùng chức năng sao lưu.
- **Phân quyền tự động:** mỗi lần khởi động, nếu thư mục dữ liệu nằm trên ổ cố định của máy, phần mềm đặt quyền chỉ cho **tài khoản Windows đang dùng**, cùng SYSTEM và nhóm Administrators. Các tài khoản Windows khác trên máy không mở được thư mục. Ổ USB, ổ rời giữ nguyên quyền để vẫn mở được khi cắm sang máy khác.
- Người có quyền Administrator của máy vẫn đọc được các tệp (dù đã mã hóa). Chỉ giao quyền quản trị máy cho người có thẩm quyền.

## 4. Đăng nhập và mật khẩu

- Mỗi bộ dữ liệu có **một mật khẩu** và **một mã khôi phục**. Phần mềm không có mật khẩu mặc định, không có "cửa sau".
- Mật khẩu: 8 đến 128 ký tự, gồm chữ cái và chữ số hoặc ký tự đặc biệt.
- **Nhập sai 5 lần liên tiếp** thì phải chờ trước khi thử tiếp; thời gian chờ tăng dần, tối đa 15 phút. Đếm số lần sai được lưu cả khi tắt và mở lại phần mềm.
- **Tự khóa màn hình** sau 10 phút không thao tác (đổi trong **Cấu hình → Bảo mật**, đặt 0 để tắt — không khuyến nghị).
- **Quên mật khẩu**: dùng mã khôi phục ở màn hình đăng nhập. Mật khẩu và mã khôi phục được lưu dưới dạng không thể đảo ngược (PBKDF2, 600.000 vòng); không ai, kể cả người viết phần mềm, lấy lại được mật khẩu.
- **Mã khôi phục** nên được ghi ra giấy, niêm phong và cất giữ như tài liệu mật, tách khỏi máy tính. Khi có người phụ trách thay đổi, hãy đổi mật khẩu và **tạo mã khôi phục mới** (Cấu hình → Tạo mã khôi phục mới…).

## 5. Sao lưu và khôi phục

### 5.1. Sao lưu

- **Tự động khi thoát**: bật mặc định. Bản sao lưu lưu vào thư mục `Backup`, giữ lại **30 bản gần nhất** (đổi trong Cấu hình).
- **Sao lưu nhanh** và **sao lưu ra nơi khác**: ở trang **Sao lưu / Khôi phục**.
- Tệp `.qlvbak` chứa dữ liệu **đã mã hóa** cùng tệp khóa và mã băm SHA-256 để kiểm tra toàn vẹn. Tệp chỉ mở được bằng mật khẩu (hoặc mã khôi phục) **đang dùng lúc sao lưu**.
- Phần mềm **không cho lưu** bản sao lưu vào thư mục tạm, thư mục dùng chung (Public) hay thư mục đồng bộ đám mây. Lưu vào thư mục mạng thì phải xác nhận.

Khuyến nghị:

1. Sao lưu ít nhất mỗi ngày làm việc (tự động khi thoát là đủ nếu tắt phần mềm cuối ngày).
2. Định kỳ hằng tuần hoặc hằng tháng chép một bản ra **thiết bị lưu trữ chuyên dùng** của cơ quan, quản lý như tài liệu mật.
3. Sau khi đổi mật khẩu, sao lưu lại ngay và ghi nhớ bản sao lưu nào đi với mật khẩu nào.
4. Thỉnh thoảng thử khôi phục trên một máy khác (dùng bản portable) để chắc chắn bản sao lưu dùng được.

### 5.2. Khôi phục

1. Vào **Sao lưu / Khôi phục → Khôi phục từ bản sao lưu…**, chọn tệp `.qlvbak`.
2. Nhập mật khẩu của bản sao lưu.
3. Phần mềm kiểm tra tệp trước khi thay thế: đúng cấu trúc, đúng kích thước, đúng mã băm, đúng mật khẩu, ổ đĩa đủ chỗ trống. Tệp bị sửa đổi hoặc giả mạo sẽ bị từ chối.
4. Dữ liệu hiện tại được tự sao lưu an toàn trước, rồi mới bị thay thế.

Sau khi khôi phục, mật khẩu đăng nhập là **mật khẩu của bản sao lưu**.

### 5.3. Chuyển sang máy mới

1. Trên máy cũ: sao lưu ra thiết bị lưu trữ chuyên dùng.
2. Trên máy mới: cài phần mềm, thiết lập lần đầu với mật khẩu tạm bất kỳ, rồi **khôi phục** từ bản sao lưu.
3. Xử lý dữ liệu trên máy cũ theo quy định về tiêu hủy tài liệu mật (mục 9).

## 6. Cấu hình bảo mật

Trong **Cấu hình → Bảo mật**:

| Tùy chọn | Mặc định | Ghi chú |
|---|---|---|
| Tự khóa sau (phút) | 10 | |
| Chặn chụp, quay, chia sẻ màn hình | Tắt | Khi bật, cửa sổ phần mềm hiện màu đen trong ảnh chụp, phần mềm quay màn hình, chia sẻ màn hình (Teams, Zalo…) và điều khiển từ xa. Tắt lại cần mật khẩu. |
| Cho phép xuất danh sách văn bản mật ra Excel/CSV | Tắt | Bật cần mật khẩu. Tệp xuất ra **không được mã hóa**: chỉ bật khi người có thẩm quyền yêu cầu, tắt lại ngay sau khi dùng, và quản lý tệp xuất như tài liệu mật. |

Mọi thay đổi cấu hình quan trọng đều được ghi vào **Nhật ký**.

## 7. Nhật ký

Phần mềm có hai loại nhật ký khác nhau:

- **Nhật ký thao tác** (trang **Nhật ký** trong phần mềm): ghi mọi lần đăng nhập, thêm, sửa, hủy, xóa, khóa sổ, sao lưu, khôi phục, đổi cấu hình. Nhật ký nằm trong cơ sở dữ liệu mã hóa, **không sửa, không xóa được**, và được nối chuỗi bằng mã băm: nút **Kiểm tra toàn vẹn** phát hiện nếu có dòng nào bị can thiệp. Nhật ký **không ghi trích yếu**.
- **Nhật ký kỹ thuật** (thư mục `Logs`, tệp `qlvb-YYYYMMDD.log`): ghi lỗi để xử lý sự cố. Mỗi ngày một tệp, mỗi tệp tối đa 5 MB, giữ 60 tệp gần nhất. Nhật ký kỹ thuật **không chứa** mật khẩu, khóa, hay nội dung văn bản; các giá trị trong thông báo lỗi được che. Có thể gửi tệp này cho người hỗ trợ kỹ thuật sau khi đã tự đọc lại.

## 8. Xử lý sự cố

| Hiện tượng | Nguyên nhân và cách xử lý |
|---|---|
| "Windows protected your PC" khi cài | Bộ cài chưa có chữ ký số. Kiểm tra mã SHA-256 (mục 2.1) rồi chọn **More info → Run anyway**. |
| "Không thể đặt dữ liệu tại …" | Bản portable đang nằm trong thư mục tạm, Public hoặc OneDrive. Chép ra thư mục cố định như `D:\QLVB`. |
| "Phần mềm đang được mở" | Chỉ chạy được một cửa sổ. Tìm cửa sổ trên thanh tác vụ, hoặc đợi vài giây sau khi đóng. |
| "Mật khẩu không đúng" và phải chờ | Đã nhập sai nhiều lần. Đợi hết thời gian, hoặc dùng **Quên mật khẩu?** với mã khôi phục. |
| "Không tìm thấy tệp cơ sở dữ liệu" | Tệp `qlvb.db` bị xóa hoặc di chuyển. Khôi phục từ bản sao lưu. |
| "Không mở được cơ sở dữ liệu: khóa không khớp…" | `qlvb.db` và `qlvb.key` không đi cùng nhau (ví dụ chép tay từ hai nơi). Khôi phục từ bản sao lưu. |
| "Cơ sở dữ liệu bị hỏng" | Tệp bị lỗi do mất điện, ổ đĩa hỏng… Khôi phục từ bản sao lưu gần nhất; kiểm tra ổ đĩa. |
| "Dữ liệu được tạo bởi phiên bản phần mềm mới hơn" | Đang dùng phần mềm cũ hơn dữ liệu. Cài phiên bản mới nhất. |
| "Nâng cấp dữ liệu … thất bại" | Dữ liệu được giữ nguyên như trước. Gửi tệp nhật ký kỹ thuật cho người hỗ trợ; tạm thời dùng lại phiên bản cũ. |
| Cảnh báo ngày giờ máy tính | Đồng hồ máy sớm hơn thao tác cuối cùng đã ghi. Sửa ngày giờ Windows trước khi đăng ký văn bản. |
| "Không phải tệp sao lưu của phần mềm" hoặc "Tệp sao lưu bị hỏng hoặc bị sửa đổi" | Tệp bị hỏng, bị sửa, hoặc không phải bản sao lưu của phần mềm. Dùng bản sao lưu khác. |
| "Đã xảy ra lỗi không mong muốn (mã …)" | Thao tác chưa được thực hiện, dữ liệu cũ không bị ảnh hưởng. Mã lỗi là ngày giờ xảy ra lỗi (`năm tháng ngày giờ phút giây`). Tìm mã này trong tệp nhật ký kỹ thuật của ngày đó (thư mục `Logs`) để xem chi tiết. |
| "Không đọc/ghi được tệp…" | Tệp đang được chương trình khác mở, ổ đĩa đầy hoặc USB bị rút ra. |

## 9. Nâng cấp, gỡ cài đặt, tiêu hủy dữ liệu

- **Nâng cấp**: sao lưu trước, rồi chạy bộ cài phiên bản mới (bản portable: thay `QLVB.exe` và các tệp `.dll`, giữ nguyên thư mục `Data`). Lần mở đầu tiên, phần mềm tự nâng cấp cấu trúc dữ liệu; nếu thất bại, dữ liệu giữ nguyên như trước.
- **Gỡ cài đặt** (Settings → Apps) **không xóa dữ liệu** trong `%LOCALAPPDATA%\QLVB`, vì dữ liệu mật phải được xử lý theo quy định chứ không được tự động xóa.
- **Tiêu hủy dữ liệu** khi thanh lý máy hoặc không dùng nữa: thực hiện theo quy định về tiêu hủy tài liệu, vật chứa bí mật nhà nước của cơ quan (lập hội đồng, biên bản…). Xóa tệp thông thường chưa phải là tiêu hủy; ổ cứng chứa dữ liệu cần được xử lý theo hướng dẫn của cơ quan chuyên môn.

## 10. Ký số bộ cài (khi cơ quan có chứng thư)

Hiện bộ cài **chưa ký số**, nên Windows cảnh báo "Unknown publisher" khi cài lần đầu. Khi cơ quan có **chứng thư ký mã (code signing)** dạng tệp `.pfx`:

1. Trên GitHub, mở kho `qlvb` → **Settings → Secrets and variables → Actions**.
2. Tab **Secrets**, thêm:
   - `SIGN_PFX_BASE64`: nội dung tệp `.pfx` mã hóa base64. Trên PowerShell: `[Convert]::ToBase64String([IO.File]::ReadAllBytes("chungthu.pfx")) | Set-Clipboard`, rồi dán vào ô giá trị;
   - `SIGN_PFX_PASSWORD`: mật khẩu tệp `.pfx`.
3. (Tùy chọn) Tab **Variables**, thêm `SIGN_TIMESTAMP_URL` là địa chỉ dịch vụ cấp dấu thời gian của nhà cung cấp chứng thư.
4. Chạy lại quy trình build. `QLVB.exe` và bộ cài sẽ được ký và kiểm tra chữ ký tự động.

Lưu ý: chứng thư do **CA chuyên dùng Chính phủ** cấp có thể chưa được Windows tin cậy mặc định. Khi đó, trên mỗi máy cần cài chứng thư gốc của CA đó vào kho **Trusted Root Certification Authorities** của máy (theo hướng dẫn của Ban Cơ yếu Chính phủ hoặc đơn vị cấp chứng thư), thì chữ ký mới được công nhận.

## 11. Cập nhật mẫu sổ khi quy định thay đổi

Mẫu sổ (tiêu đề, cột, hướng dẫn ghi) không viết cứng trong mã, mà được mô tả bằng tệp định nghĩa biểu mẫu kèm phiên bản và căn cứ pháp lý. Khi Nhà nước ban hành mẫu mới, người phát triển chỉ cần thêm tệp định nghĩa mới và phát hành bản cập nhật; dữ liệu và sổ các năm cũ vẫn giữ mẫu cũ. Xem chi tiết trong **Tài liệu kỹ thuật**.
