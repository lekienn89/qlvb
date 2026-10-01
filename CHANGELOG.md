# Nhật ký thay đổi

## 1.0.0 – 01/10/2026

Bản phát hành đầu tiên.

### Chức năng

- Sổ đăng ký bí mật nhà nước đi và đến theo Phụ lục III Nghị định 63/2026/NĐ-CP (thay thế theo Công văn 2663/VPCP-NC); mẫu sổ mô tả bằng tệp định nghĩa, cập nhật được khi quy định thay đổi.
- Đánh số thứ tự theo năm, nhiều quyển sổ trong năm, khóa sổ và mở khóa có lý do.
- Văn bản Tuyệt mật không lưu và không hiển thị trích yếu (ràng buộc cả trong cơ sở dữ liệu).
- Thêm, sửa (có bảng xác nhận thay đổi), hủy và khôi phục, xóa văn bản nhập sai (cấp lại số nếu là số cuối của năm).
- Danh mục: loại văn bản, người ký, đơn vị, nơi nhận, cơ quan ban hành, độ mật.
- Tìm kiếm không dấu, tra cứu nhiều điều kiện, báo cáo thống kê, xuất Excel/CSV (xuất văn bản mật mặc định tắt).
- In sổ A4 dọc/ngang có trang bìa, lặp tiêu đề cột, đánh số trang; in ra PDF.
- Dữ liệu mẫu giả lập để làm quen, xóa được hoàn toàn.

### Bảo mật

- Cơ sở dữ liệu mã hóa toàn bộ (AES-256); khóa dữ liệu bọc bằng mật khẩu (PBKDF2 600.000 vòng) và mã khôi phục.
- Chống dò mật khẩu, tự khóa màn hình, xóa clipboard khi khóa hoặc thoát, tùy chọn chặn chụp màn hình.
- Nhật ký thao tác chỉ thêm, nối chuỗi băm, kiểm tra toàn vẹn được.
- Sao lưu mã hóa, kiểm tra kỹ tệp trước khi khôi phục; tự sao lưu khi thoát.
- Chặn lưu dữ liệu, bản sao lưu, tệp xuất vào thư mục tạm, Public, thư mục đồng bộ đám mây; phân quyền thư mục dữ liệu chỉ cho tài khoản đang dùng.
- Không kết nối mạng, không telemetry.

### Phát hành

- Bộ cài tiếng Việt (không cần quyền quản trị) và bản portable, kèm mã kiểm tra SHA-256.
- Tài liệu: Hướng dẫn sử dụng, Hướng dẫn quản trị, Tài liệu kỹ thuật (Markdown và HTML).
- Chưa ký số (cơ quan chưa có chứng thư ký mã); quy trình build đã sẵn bước ký số.
