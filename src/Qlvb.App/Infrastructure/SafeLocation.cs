using Qlvb.Infrastructure.Security;

namespace Qlvb.App.Infrastructure;

/// <summary>Kiểm tra nơi lưu tệp người dùng chọn (sao lưu, xuất tệp) theo <see cref="LocationPolicy"/>.</summary>
public static class SafeLocation
{
    /// <summary>true nếu được lưu. Thư mục tạm, dùng chung, đồng bộ đám mây: chặn. Ổ mạng: hỏi xác nhận.</summary>
    public static bool Check(string path, string loaiTep)
    {
        if (LocationPolicy.Reason(path) is { } why)
        {
            Dlg.Warn($"Không lưu {loaiTep} vào vị trí này.\n\n{why}\n\nHãy chọn thư mục riêng trên máy hoặc thiết bị lưu trữ được quản lý theo quy định.");
            return false;
        }
        if (LocationPolicy.Warning(path) is { } warn)
            return Dlg.Confirm($"{warn}\n\nVẫn lưu {loaiTep} vào đây?", danger: true);
        return true;
    }
}
