using System.Windows;
using Qlvb.Application;
using Qlvb.Infrastructure.Services;

namespace Qlvb.App.Infrastructure;

/// <summary>Hộp thoại tiếng Việt dùng chung.</summary>
public static class Dlg
{
    public const string Title = "Quản lý văn bản đi – đến";

    private static Window? Owner => System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                                    ?? System.Windows.Application.Current?.MainWindow;

    private static MessageBoxResult Show(string msg, MessageBoxButton b, MessageBoxImage i, MessageBoxResult def = MessageBoxResult.None)
    {
        var o = Owner;
        return o is { IsVisible: true }
            ? MessageBox.Show(o, msg, Title, b, i, def)
            : MessageBox.Show(msg, Title, b, i, def);
    }

    public static void Info(string msg) => Show(msg, MessageBoxButton.OK, MessageBoxImage.Information);
    public static void Warn(string msg) => Show(msg, MessageBoxButton.OK, MessageBoxImage.Warning);
    public static void Error(string msg) => Show(msg, MessageBoxButton.OK, MessageBoxImage.Error);

    /// <summary>Hỏi Có/Không. Mặc định chọn "Không" cho thao tác nguy hiểm.</summary>
    public static bool Confirm(string msg, bool danger = false)
    {
        var w = new Window
        {
            Title = Title, Width = 520, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false,
        };
        var o = Owner;
        if (o is { IsVisible: true } && o != w) w.Owner = o; else w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var sp = new System.Windows.Controls.StackPanel { Margin = new Thickness(20) };
        var head = new System.Windows.Controls.DockPanel();
        var icon = new System.Windows.Controls.TextBlock
        {
            Text = danger ? "⚠" : "?", FontSize = 28, Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Top,
            Foreground = danger ? System.Windows.Media.Brushes.DarkOrange : System.Windows.Media.Brushes.SteelBlue,
        };
        System.Windows.Controls.DockPanel.SetDock(icon, System.Windows.Controls.Dock.Left);
        head.Children.Add(icon);
        head.Children.Add(new System.Windows.Controls.TextBlock { Text = msg, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(head);
        var bar = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var yes = new System.Windows.Controls.Button { Content = "Đồng ý", IsDefault = !danger };
        if (!danger) yes.Style = (Style)System.Windows.Application.Current.FindResource("PrimaryButton");
        yes.Click += (_, _) => w.DialogResult = true;
        var no = new System.Windows.Controls.Button { Content = "Không", IsCancel = true, IsDefault = danger };
        bar.Children.Add(yes);
        bar.Children.Add(no);
        sp.Children.Add(bar);
        w.Content = sp;
        w.Loaded += (_, _) => (danger ? no : yes).Focus();
        return w.ShowDialog() == true;
    }

    /// <summary>
    /// Xử lý lỗi tập trung: lỗi nghiệp vụ hiển thị nguyên văn (đã là tiếng Việt, không chứa dữ liệu mật);
    /// lỗi kỹ thuật chỉ hiển thị thông điệp chung và ghi nhật ký kỹ thuật đã che dữ liệu.
    /// </summary>
    public static void Handle(Exception ex, string? context = null)
    {
        // Phiên đã đăng xuất (ví dụ tự khóa rồi đăng xuất khi đang mở hộp thoại): bỏ qua.
        if (ex is InvalidOperationException && Ctx.Session is { LoggedIn: false }) return;
        switch (ex)
        {
            case BusinessException b:
                Warn(b.Message);
                return;
            case Qlvb.Infrastructure.Security.AuthException a:
                Warn(a.Message);
                return;
            case Qlvb.Infrastructure.Data.DatabaseOpenException d:
                Ctx.Log.Error("Lỗi CSDL {Ctx}: {Err}", context ?? "", Logging.Describe(d));
                Error(d.Message);
                return;
            case InvalidDataException i:
                Error(i.Message);
                return;
            case UnauthorizedAccessException:
                Error("Không có quyền truy cập tệp hoặc thư mục đã chọn. Hãy chọn vị trí khác.");
                return;
            case IOException io:
                Ctx.Log.Warning("Lỗi tệp {Ctx}: {Err}", context ?? "", Logging.Describe(io));
                Error("Không đọc/ghi được tệp. Tệp có thể đang được chương trình khác mở, ổ đĩa đầy hoặc bị tháo ra.");
                return;
            default:
                var code = DateTime.Now.ToString("yyyyMMddHHmmss");
                Ctx.Log.Error("Lỗi không mong muốn [{Code}] {Ctx}: {Err}", code, context ?? "", Logging.Describe(ex));
                Error($"Đã xảy ra lỗi không mong muốn (mã {code}). Thao tác chưa được thực hiện; dữ liệu đã lưu trước đó không bị ảnh hưởng.\n\nNếu lỗi lặp lại, hãy gửi tệp nhật ký kỹ thuật trong thư mục Logs cho người quản trị.");
                return;
        }
    }

    /// <summary>Chạy một thao tác, bắt và hiển thị lỗi. Trả về true nếu thành công.</summary>
    public static bool Try(Action action, string? context = null)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex)
        {
            Handle(ex, context);
            return false;
        }
    }
}
