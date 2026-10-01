using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Qlvb.App.Infrastructure;
using Qlvb.App.Views;
using Qlvb.Application;
using Qlvb.Infrastructure;
using Qlvb.Infrastructure.Services;

namespace Qlvb.App;

public partial class App : System.Windows.Application
{
    private Mutex? _single;
    private IdleLock? _idle;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var vi = CultureInfo.GetCultureInfo("vi-VN");
        CultureInfo.DefaultThreadCurrentCulture = vi;
        CultureInfo.DefaultThreadCurrentUICulture = vi;
        Thread.CurrentThread.CurrentCulture = vi;
        Thread.CurrentThread.CurrentUICulture = vi;
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(vi.IetfLanguageTag)));

        // Chữ gợi ý mặc định của DatePicker là tiếng Anh ("Select a date"): thay bằng tiếng Việt.
        EventManager.RegisterClassHandler(typeof(DatePicker), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, _) =>
        {
            if (s is DatePicker dp && dp.Template?.FindName("PART_TextBox", dp) is DatePickerTextBox tb)
            {
                tb.ApplyTemplate();
                if (tb.Template?.FindName("PART_Watermark", tb) is ContentControl wm) wm.Content = "dd/mm/yyyy";
            }
        }));

        DispatcherUnhandledException += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += (_, a) =>
            Ctx.Log.Fatal("Lỗi nghiêm trọng: {Err}", a.ExceptionObject is Exception x ? Logging.Describe(x) : "không rõ");
        TaskScheduler.UnobservedTaskException += (_, a) =>
        {
            Ctx.Log.Error("Lỗi tác vụ nền: {Err}", Logging.Describe(a.Exception));
            a.SetObserved();
        };

        try
        {
            Ctx.Paths = AppPaths.Detect(AppContext.BaseDirectory);
            if (Ctx.Paths.IsInTempFolder())
            {
                Dlg.Error("Phần mềm đang chạy từ thư mục tạm (có thể đang mở trực tiếp trong tệp nén). Hãy giải nén bản portable ra một thư mục cố định rồi chạy lại.");
                Shutdown(2);
                return;
            }
            var name = "Local\\QLVB-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(Ctx.Paths.Root.ToUpperInvariant())))[..16];
            _single = new Mutex(true, name, out var created);
            if (!created)
            {
                Dlg.Info("Phần mềm đang được mở. Hãy dùng cửa sổ đang mở trên thanh tác vụ.");
                Shutdown(0);
                return;
            }
            Ctx.Paths.EnsureCreated();
            Ctx.Log = Logging.Create(Ctx.Paths);
            Ctx.Log.Information("Khởi động phiên bản {Ver}, chế độ {Mode}", typeof(App).Assembly.GetName().Version, Ctx.Paths.Portable ? "portable" : "cài đặt");
            Ctx.Session = new AppSession(Ctx.Paths);
        }
        catch (Exception ex)
        {
            Dlg.Error("Không khởi tạo được thư mục dữ liệu: " + Logging.Redact(ex.Message));
            Shutdown(3);
            return;
        }
        ShowLogin();
    }

    /// <summary>Hiển thị màn hình đăng nhập; đăng nhập thành công thì mở cửa sổ chính, ngược lại thoát.</summary>
    public void ShowLogin()
    {
        var login = new LoginWindow();
        if (login.ShowDialog() != true)
        {
            Shutdown(0);
            return;
        }
        var main = new MainWindow();
        MainWindow = main;
        _idle?.Dispose();
        _idle = new IdleLock(main);
        main.Show();
    }

    /// <summary>Đăng xuất: đóng mọi cửa sổ, xóa khóa khỏi bộ nhớ, quay về màn hình đăng nhập.</summary>
    public bool LoggingOut { get; private set; }

    public void Logout()
    {
        if (LoggingOut) return;
        LoggingOut = true;
        _idle?.Dispose();
        _idle = null;
        foreach (var w in Windows.OfType<Window>().ToList()) w.Close();
        Ctx.Session.Logout();
        LoggingOut = false;
        ShowLogin();
    }

    public IdleLock? Idle => _idle;

    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        Dlg.Handle(e.Exception, "giao diện");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _idle?.Dispose();
            Ctx.Session?.Dispose();
            Ctx.Log.Information("Thoát");
            (Ctx.Log as IDisposable)?.Dispose();
        }
        catch (Exception) { /* thoát: không được ném lỗi */ }
        _single?.Dispose();
        base.OnExit(e);
    }

    public static new App Current => (App)System.Windows.Application.Current;
}
