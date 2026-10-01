using System.Text.RegularExpressions;
using Serilog;
using Serilog.Core;

namespace Qlvb.Infrastructure.Services;

/// <summary>
/// Nhật ký kỹ thuật (tệp văn bản trong thư mục Logs, xoay vòng theo ngày, giới hạn dung lượng).
/// KHÔNG ghi mật khẩu, khóa, nội dung văn bản: chỉ ghi loại lỗi, vị trí lỗi, và thông điệp đã che các giá trị trong ngoặc kép.
/// </summary>
public static partial class Logging
{
    public static Logger Create(AppPaths paths) => new LoggerConfiguration()
        .MinimumLevel.Information()
        .WriteTo.File(Path.Combine(paths.Logs, "qlvb-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 60,
            fileSizeLimitBytes: 5 * 1024 * 1024,
            rollOnFileSizeLimit: true,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}")
        .CreateLogger();

    /// <summary>Che các giá trị có thể là dữ liệu người dùng trong thông điệp lỗi.</summary>
    public static string Redact(string? message)
    {
        if (string.IsNullOrEmpty(message)) return "";
        var s = Quoted().Replace(message, "\"…\"");
        s = SingleQuoted().Replace(s, "'…'");
        s = HexKey().Replace(s, "[khóa]");
        return s.Length > 500 ? s[..500] + "…" : s;
    }

    /// <summary>Mô tả lỗi an toàn để ghi nhật ký: loại lỗi, thông điệp đã che, ngăn xếp gọi (không có dữ liệu).</summary>
    public static string Describe(Exception ex)
    {
        var parts = new List<string>();
        for (var e = ex; e != null; e = e.InnerException)
            parts.Add($"{e.GetType().FullName}: {Redact(e.Message)}");
        return string.Join(" ---> ", parts) + Environment.NewLine + ex.StackTrace;
    }

    [GeneratedRegex("\"[^\"]*\"")] private static partial Regex Quoted();
    [GeneratedRegex("'[^']*'")] private static partial Regex SingleQuoted();
    [GeneratedRegex("[0-9A-Fa-f]{32,}")] private static partial Regex HexKey();
}
