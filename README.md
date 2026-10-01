# Quản lý văn bản đi – đến (QLVB)

Phần mềm desktop chạy **ngoại tuyến trên một máy Windows 10/11** để quản lý sổ đăng ký bí mật nhà nước đi, đến
theo Phụ lục III Nghị định 63/2026/NĐ-CP (thay thế theo Công văn 2663/VPCP-NC).

- C# / .NET 10 (LTS), WPF, SQLite mã hóa toàn bộ tệp (SQLite3 Multiple Ciphers, AES-256).
- Không kết nối mạng, không telemetry, không cloud.
- Kho mã này **chỉ chứa mã nguồn và dữ liệu giả**. Không đưa dữ liệu thật, bản sao lưu, tệp khóa lên đây.

## Cấu trúc

| Thư mục | Nội dung |
|---|---|
| `src/Qlvb.Domain` | Thực thể, quy tắc kiểm tra, định nghĩa biểu mẫu sổ |
| `src/Qlvb.Application` | Dịch vụ nghiệp vụ (đăng ký, sửa, hủy, danh mục, sổ, cấu hình, dữ liệu mẫu) |
| `src/Qlvb.Infrastructure` | CSDL mã hóa, di trú lược đồ, khóa dữ liệu, nhật ký, sao lưu, xuất tệp |
| `src/Qlvb.App` | Giao diện WPF tiếng Việt, in sổ |
| `tests/Qlvb.Tests` | Kiểm thử tự động phần lõi (chạy trên mọi hệ điều hành) |
| `tests/Qlvb.UiTests` | Kiểm thử giao diện bằng FlaUI (chỉ Windows) |
| `installer` | Kịch bản bộ cài Inno Setup |

## Build

```
dotnet test tests/Qlvb.Tests
dotnet publish src/Qlvb.App -c Release -o artifacts/publish
```

GitHub Actions (`.github/workflows/build.yml`) build trên Windows, chạy kiểm thử, tạo bộ cài và bản portable.

Tài liệu đầy đủ (hướng dẫn sử dụng, quản trị, kỹ thuật) sẽ có trong thư mục `docs/` ở giai đoạn phát hành.
