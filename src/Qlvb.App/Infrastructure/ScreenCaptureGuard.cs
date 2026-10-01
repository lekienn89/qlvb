using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Qlvb.App.Infrastructure;

/// <summary>
/// Tùy chọn chặn chụp màn hình: cửa sổ của phần mềm hiện màu đen trong ảnh chụp, phần mềm quay màn hình,
/// chia sẻ màn hình (Teams, Zalo…) và điều khiển từ xa. Dùng SetWindowDisplayAffinity của Windows; không chặn được chụp bằng máy ảnh.
/// </summary>
public static class ScreenCaptureGuard
{
    private const uint WdaNone = 0;
    private const uint WdaMonitor = 1;
    private const uint WdaExcludeFromCapture = 0x11; // Windows 10 bản 2004 trở lên

    public static bool Enabled { get; private set; }

    public static void Init() =>
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, _) =>
        {
            if (s is Window w) Apply(w);
        }));

    public static void Set(bool enabled)
    {
        Enabled = enabled;
        foreach (var w in System.Windows.Application.Current.Windows.OfType<Window>()) Apply(w);
    }

    private static void Apply(Window w)
    {
        var hwnd = new WindowInteropHelper(w).Handle;
        if (hwnd == IntPtr.Zero) return;
        if (!Enabled) { SetWindowDisplayAffinity(hwnd, WdaNone); return; }
        if (!SetWindowDisplayAffinity(hwnd, WdaExcludeFromCapture)) SetWindowDisplayAffinity(hwnd, WdaMonitor);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);
}
