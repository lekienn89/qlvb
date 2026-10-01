# Hướng dẫn sử dụng

**Phần mềm Quản lý văn bản đi – đến**, phiên bản 1.0.0

Phần mềm dùng để lập và quản lý *Sổ đăng ký bí mật nhà nước đi* và *Sổ đăng ký bí mật nhà nước đến* trên **một máy tính Windows 10/11 không kết nối mạng**. Toàn bộ dữ liệu được mã hóa và chỉ mở được bằng mật khẩu của người dùng.

---

## 1. Lần đầu sử dụng

### 1.1. Đặt mật khẩu

Lần đầu mở phần mềm, màn hình **Thiết lập lần đầu** hiện ra:

1. Nhập **tên cơ quan, tổ chức**. Tên này được in trên trang bìa sổ và có thể sửa sau.
2. Nhập **mật khẩu mới** hai lần. Mật khẩu phải có ít nhất 8 ký tự, gồm chữ cái và chữ số (hoặc ký tự đặc biệt). Phần mềm không có mật khẩu mặc định.
3. Bấm **Thiết lập**.

### 1.2. Ghi lại mã khôi phục

Sau khi thiết lập, phần mềm hiện **mã khôi phục** gồm 20 ký tự, dạng `XXXXX-XXXXX-XXXXX-XXXXX`.

> **Quan trọng:** chép mã này ra giấy và cất giữ như tài liệu mật. Quên mật khẩu thì vẫn đặt lại được bằng mã khôi phục. **Mất cả mật khẩu lẫn mã khôi phục thì không ai mở được dữ liệu**, kể cả người viết phần mềm.

Bấm **Tôi đã ghi lại mã** để vào phần mềm.

### 1.3. Khai báo thông tin cơ quan

Vào **Cấu hình**, mục *Thông tin cơ quan và số, ký hiệu*:

- tên cơ quan, tổ chức (2) và cơ quan chủ quản cấp trên trực tiếp (1), nếu có;
- ký hiệu (chữ viết tắt) của cơ quan;
- mẫu số, ký hiệu: phần mềm dùng mẫu này để gợi ý số, ký hiệu khi nhập văn bản đi. Ví dụ mẫu `{so}/{viet_tat}-{ky_hieu_co_quan}` cho ra `15/BC-ABC`.

Bấm **Lưu thông tin**.

Tiếp theo, vào **Danh mục** để nhập trước người ký, đơn vị, nơi nhận và cơ quan ban hành thường dùng. Nếu chưa nhập trước cũng không sao: khi nhập văn bản, giá trị mới được tự ghi nhớ.

### 1.4. Dùng thử bằng dữ liệu mẫu

Ở **Trang chủ**, bấm **Nạp dữ liệu mẫu** để có sẵn vài chục văn bản giả lập (đánh dấu `[MẪU]`) để làm quen.

Trước khi nhập văn bản thật, bấm **Xóa dữ liệu mẫu**. Nút này chỉ xóa dữ liệu mẫu, không ảnh hưởng văn bản thật, và số thứ tự sẽ bắt đầu lại từ 01.

---

## 2. Màn hình chính

Thanh bên trái gồm các mục:

| Mục | Dùng để |
|---|---|
| Trang chủ | Xem số liệu nhanh, văn bản gần đây, nút thêm văn bản đi, đến |
| Văn bản đi / Văn bản đến | Xem danh sách theo sổ; thêm, sửa, hủy, xóa, in sổ |
| Tra cứu | Tìm kết hợp nhiều điều kiện trên cả hai sổ |
| Báo cáo – Thống kê | Thống kê theo năm, tháng, độ mật, loại văn bản, cơ quan, người ký, đơn vị |
| Danh mục | Sổ đăng ký, loại văn bản, người ký, đơn vị, nơi nhận, cơ quan ban hành, độ mật |
| Sao lưu / Khôi phục | Sao lưu và khôi phục dữ liệu |
| Nhật ký | Xem lịch sử thao tác, kiểm tra toàn vẹn nhật ký |
| Cấu hình | Thông tin cơ quan, bảo mật, hiển thị và in, sao lưu tự động |
| Trợ giúp | Hướng dẫn nhanh |

Phía dưới thanh bên có nút **Khóa màn hình (Ctrl+L)** và **Đăng xuất**.

---

## 3. Đăng ký văn bản đi

Văn bản đi phải được **đăng ký trước khi phát hành**.

1. Vào **Văn bản đi**, bấm **+ Thêm mới (Ctrl+N)**.
2. Nhập các ô theo mẫu sổ:
   - **Số thứ tự**: phần mềm tự cấp theo năm, không sửa được.
   - **Số, ký hiệu**: có nút **Gợi ý** theo mẫu đã khai báo trong Cấu hình.
   - **Ngày văn bản**, **Loại văn bản**, **Trích yếu**.
   - **Độ mật**: Tuyệt mật, Tối mật hoặc Mật.
   - **Người ký**, **Đơn vị lưu**, **Số lượng** bản phát hành, **Ghi chú**.
   - **Nơi nhận**: gõ tên rồi bấm **Thêm nơi nhận**. Có thể thêm nhiều nơi nhận; mỗi nơi nhận có thể ghi người ký nhận và ngày ký nhận.
3. Bấm **Lưu (Ctrl+S)**, hoặc **Lưu và nhập tiếp** để nhập văn bản kế tiếp ngay.

Một số tiện ích khi nhập:

- Nút **+** cạnh ô danh mục để thêm nhanh một mục mới.
- Nút **Sao chép từ văn bản trước** chỉ chép các thông tin thường lặp lại (loại, người ký, đơn vị, nơi nhận). Luôn kiểm tra lại trước khi lưu.
- Phần mềm **cảnh báo** khi nghi trùng số, ký hiệu, hoặc khi ngày nhập sớm hơn văn bản đã đăng ký trước đó. Người dùng quyết định có lưu hay không.

### Văn bản TUYỆT MẬT

Khi chọn độ mật **Tuyệt mật**, phần mềm hỏi xác nhận, **xóa và khóa ô trích yếu**, vì theo quy định không được ghi trích yếu cho tài liệu Tuyệt mật. Trên sổ, cột "Tên loại và trích yếu" chỉ ghi tên loại. Không có cách nào lưu trích yếu cho văn bản Tuyệt mật.

## 4. Đăng ký văn bản đến

Văn bản đến được **đăng ký sau khi tiếp nhận**.

1. Vào **Văn bản đến**, bấm **+ Thêm mới (Ctrl+N)**.
2. Nhập các ô sau:
   - **Ngày đến**;
   - **Số đến**: phần mềm gợi ý số tiếp theo; có thể sửa nếu đơn vị đánh số đến riêng;
   - **Cơ quan ban hành**, **Số, ký hiệu**, **Ngày văn bản**;
   - **Loại**, **Trích yếu**, **Độ mật**, **Đơn vị/người nhận**, **Ghi chú**.
3. Bấm **Lưu (Ctrl+S)**.

---

## 5. Xem, tìm và sắp xếp

- Trên danh sách, chọn **năm** và **quyển sổ** cần xem.
- Ô **Tìm kiếm** tìm gần đúng, có dấu hay không dấu đều được (ví dụ gõ `bao cao` vẫn tìm ra "Báo cáo").
- Bấm vào tiêu đề cột để sắp xếp; chọn số dòng mỗi trang: 20, 50, 100, 200 hoặc Tất cả.
- Nhấn đúp, hoặc nhấn **Enter**, để mở văn bản.
- **Tra cứu** cho phép kết hợp nhiều điều kiện: loại sổ, năm, khoảng ngày, số thứ tự, số ký hiệu, cơ quan, người ký, độ mật, trích yếu, trạng thái. Kết quả có thể in bằng nút **In kết quả**.

---

## 6. Sửa, hủy và xóa văn bản

| Tình huống | Nên làm | Kết quả |
|---|---|---|
| Nhập sai một vài thông tin (ngày, số ký hiệu, trích yếu, người ký…) | **Sửa** | Phần mềm hiện bảng giá trị cũ → mới để xác nhận và ghi nhật ký. Số thứ tự không đổi. |
| Văn bản không phát hành, bị thu hồi… và cần giữ dấu vết trên sổ | **Hủy văn bản** (bắt buộc ghi lý do) | Văn bản vẫn nằm trên sổ với ghi chú "ĐÃ HỦY", vẫn giữ số. Có thể **Khôi phục**. |
| Nhập nhầm hẳn một văn bản (nhập trùng, nhập nhầm sổ…) | **Xóa văn bản nhập sai…** | Văn bản bị xóa khỏi sổ, không khôi phục được (trừ khi khôi phục cả bản sao lưu). |

### Cách xóa văn bản nhập sai

1. Chọn văn bản trên danh sách, bấm nút **Xóa văn bản nhập sai…**, hoặc nhấn chuột phải và chọn lệnh cùng tên.
2. Đọc cảnh báo. Cảnh báo cho biết số thứ tự sẽ được cấp lại hay để trống.
3. Nhập **mật khẩu**, rồi nhập **lý do xóa**.

Quy tắc số thứ tự khi xóa:

- Nếu văn bản bị xóa mang **số lớn nhất đã cấp trong năm**, số đó được **cấp lại** cho văn bản nhập tiếp theo. Ví dụ: vừa nhập số 15 thì phát hiện sai; xóa đi, nhập lại thì văn bản mới vẫn là số 15.
- Nếu văn bản bị xóa nằm ở giữa, số đó **để trống** trên sổ, để không làm thay đổi số của các văn bản khác.
- Với sổ văn bản đến, số đến cũng theo quy tắc này.

Mọi lần sửa, hủy, khôi phục, xóa đều được ghi vào **Nhật ký** kèm lý do.

### Sổ đã khóa

Khi một quyển sổ đã dùng xong, có thể **khóa sổ** trong **Danh mục → Sổ đăng ký**. Sổ đã khóa thì không thêm, sửa, hủy hay xóa văn bản được. Mở khóa cần nhập mật khẩu và lý do.

---

## 7. In sổ

1. Ở trang Văn bản đi hoặc Văn bản đến, bấm **In sổ (Ctrl+P)**.
2. Chọn năm, quyển, có in **trang bìa** hay không, khổ **A4 dọc** hoặc **A4 ngang**, và có gồm văn bản đã hủy hay không.
3. Bấm **Xem trước** để kiểm tra, rồi bấm **In… (Ctrl+P)** để chọn máy in, trang cần in và số bản.

Mỗi trang in đều lặp lại tiêu đề cột, có "Trang x/y" và ngày in. Muốn lưu thành tệp PDF thì chọn máy in **Microsoft Print to PDF**.

> Bản in và tệp PDF là tài liệu mật: phải quản lý theo quy định của cơ quan.

## 8. Báo cáo, thống kê và xuất tệp

- **Báo cáo – Thống kê**: chọn tiêu chí (năm, tháng, độ mật, loại văn bản, cơ quan ban hành, người ký, đơn vị), loại sổ, năm, khoảng ngày, rồi bấm **Xem thống kê**. Kết quả in được, hoặc xuất ra Excel/CSV bằng nút **Xuất Excel/CSV…**.
- **Xuất danh sách văn bản** ra Excel/CSV mặc định **bị tắt**, vì tệp xuất ra không được mã hóa. Chỉ bật khi người có thẩm quyền yêu cầu (xem Hướng dẫn quản trị) và tắt lại ngay sau khi dùng.
- Phần mềm không cho lưu tệp xuất vào thư mục tạm, thư mục dùng chung (Public) hoặc thư mục đồng bộ đám mây như OneDrive.

---

## 9. Sao lưu và khôi phục

- **Sao lưu nhanh**: vào **Sao lưu / Khôi phục**, bấm **Sao lưu nhanh**. Bản sao lưu được lưu vào thư mục sao lưu mặc định.
- **Sao lưu ra nơi khác**: dùng nút sao lưu thủ công để chọn nơi lưu, ví dụ thiết bị lưu trữ được cơ quan quản lý.
- **Tự động sao lưu khi thoát**: bật sẵn; có thể tắt trong Cấu hình.
- Tệp sao lưu (đuôi `.qlvbak`) **được mã hóa**. Tệp chỉ mở được bằng **mật khẩu đang dùng tại thời điểm sao lưu**, hoặc mã khôi phục lúc đó. Vì vậy, sau khi đổi mật khẩu nên sao lưu lại ngay.
- **Khôi phục**: bấm **Khôi phục từ bản sao lưu…**, chọn tệp, nhập mật khẩu của bản sao lưu. Phần mềm kiểm tra tệp, tự sao lưu an toàn dữ liệu hiện tại, rồi mới thay thế.

> Nên sao lưu ít nhất mỗi ngày làm việc, và định kỳ chép một bản ra thiết bị lưu trữ riêng, cất giữ như tài liệu mật.

## 10. Mật khẩu và khóa màn hình

- **Đổi mật khẩu**: vào **Cấu hình → Đổi mật khẩu…**.
- **Quên mật khẩu**: ở màn hình đăng nhập, bấm **Quên mật khẩu?**, nhập mã khôi phục và mật khẩu mới. Sau đó phần mềm cấp **mã khôi phục mới**; mã cũ hết hiệu lực, cần ghi lại mã mới.
- **Nhập sai mật khẩu 5 lần** thì phải chờ, thời gian chờ tăng dần đến tối đa 15 phút.
- **Khóa màn hình**: bấm **Ctrl+L** khi rời khỏi máy. Phần mềm cũng tự khóa sau 10 phút không thao tác (có thể đổi trong Cấu hình).

## 11. Phím tắt

| Phím | Chức năng |
|---|---|
| Ctrl+N | Thêm văn bản mới |
| Ctrl+S | Lưu |
| Ctrl+F | Tìm |
| Ctrl+P | In |
| F5 | Làm mới danh sách |
| Enter | Mở văn bản đang chọn |
| Esc | Đóng cửa sổ |
| Ctrl+L | Khóa màn hình |

## 12. Câu hỏi thường gặp

**Số thứ tự có tự bắt đầu lại mỗi năm không?** Có. Ngày 01/01 hằng năm, số thứ tự bắt đầu lại từ 01; dữ liệu năm cũ giữ nguyên.

**Một năm dùng nhiều quyển sổ thì sao?** Khi quyển đầy, khóa quyển cũ và mở quyển mới trong Danh mục → Sổ đăng ký. Số thứ tự tiếp nối giữa các quyển trong năm.

**Khi mở phần mềm, có cảnh báo về ngày giờ máy tính.** Đồng hồ máy đang sớm hơn thao tác cuối cùng đã ghi, có thể do máy hết pin đồng hồ hoặc bị chỉnh lùi. Hãy sửa ngày giờ của Windows trước khi đăng ký văn bản, vì ngày đăng ký lấy theo đồng hồ máy.

**Phần mềm báo đã đang mở.** Mỗi lúc chỉ chạy được một cửa sổ phần mềm. Hãy tìm cửa sổ đang mở trên thanh tác vụ.

**Gặp thông báo "lỗi không mong muốn (mã …)".** Thao tác đó chưa được thực hiện và dữ liệu đã lưu không bị ảnh hưởng. Nếu lỗi lặp lại, báo cho người quản trị kèm mã lỗi (xem Hướng dẫn quản trị, mục Xử lý sự cố).
