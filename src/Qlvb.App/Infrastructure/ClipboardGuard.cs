using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace Qlvb.App.Infrastructure;

/// <summary>
/// Nội dung người dùng sao chép (Ctrl+C) từ phần mềm có thể là thông tin mật. Khi khóa màn hình, đăng xuất hoặc thoát,
/// nếu bộ nhớ tạm (clipboard) vẫn còn đúng nội dung đã chép từ phần mềm thì xóa đi; nội dung chép từ chương trình khác giữ nguyên.
/// Chỉ giữ mã băm của nội dung, không giữ bản thân nội dung.
/// </summary>
public static class ClipboardGuard
{
    private static byte[]? _hash;

    public static void Init() =>
        EventManager.RegisterClassHandler(typeof(UIElement), CommandManager.ExecutedEvent, new ExecutedRoutedEventHandler((_, e) =>
        {
            if (e.Command == ApplicationCommands.Copy || e.Command == ApplicationCommands.Cut)
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(Remember);
        }), handledEventsToo: true);

    private static void Remember()
    {
        try { _hash = Clipboard.ContainsText() ? Hash(Clipboard.GetText()) : null; }
        catch (ExternalException) { /* bộ nhớ tạm đang bị chương trình khác giữ */ }
    }

    /// <summary>Xóa bộ nhớ tạm nếu nội dung hiện có là nội dung đã chép từ phần mềm.</summary>
    public static void ClearIfOurs()
    {
        if (_hash == null) return;
        try
        {
            if (Clipboard.ContainsText() && CryptographicOperations.FixedTimeEquals(Hash(Clipboard.GetText()), _hash))
                Clipboard.Clear();
        }
        catch (ExternalException) { }
        _hash = null;
    }

    private static byte[] Hash(string s) => SHA256.HashData(Encoding.UTF8.GetBytes(s));
}
