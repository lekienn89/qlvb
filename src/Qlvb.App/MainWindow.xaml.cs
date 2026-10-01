using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Qlvb.App.Infrastructure;
using Qlvb.App.Pages;
using Qlvb.Application;
using Qlvb.Domain;
using Qlvb.Infrastructure.Services;

namespace Qlvb.App;

public partial class MainWindow : Window
{
    private readonly Dictionary<string, Func<IPage>> _factories;
    private readonly Dictionary<string, IPage> _pages = [];
    private IPage? _current;

    public MainWindow()
    {
        InitializeComponent();
        _factories = new()
        {
            ["home"] = () => new HomePage(this),
            ["di"] = () => new DanhSachPage(LoaiSo.Di),
            ["den"] = () => new DanhSachPage(LoaiSo.Den),
            ["tracuu"] = () => new TraCuuPage(),
            ["baocao"] = () => new BaoCaoPage(),
            ["danhmuc"] = () => new DanhMucPage(),
            ["saoluu"] = () => new SaoLuuPage(),
            ["nhatky"] = () => new NhatKyPage(),
            ["cauhinh"] = () => new CauHinhPage(),
            ["trogiup"] = () => new TroGiupPage(),
        };
        Loaded += (_, _) =>
        {
            ((RadioButton)Nav.Children[0]).IsChecked = true;
            UpdateStatus();
        };
        Ctx.DataChanged += OnDataChanged;
        Closing += OnClosing;
        Closed += (_, _) => Ctx.DataChanged -= OnDataChanged;
    }

    private void OnDataChanged() => Dispatcher.BeginInvoke(UpdateStatus);

    public void Navigate(string key)
    {
        foreach (var rb in Nav.Children.OfType<RadioButton>())
            if ((string)rb.Tag == key) { rb.IsChecked = true; return; }
    }

    /// <summary>Mở trang danh sách và tạo văn bản mới.</summary>
    public void NewDocument(LoaiSo loai)
    {
        Navigate(loai == LoaiSo.Di ? "di" : "den");
        _current?.NewItem();
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        var key = (string)((RadioButton)sender).Tag;
        try
        {
            if (!_pages.TryGetValue(key, out var page))
            {
                page = _factories[key]();
                _pages[key] = page;
            }
            _current = page;
            Host.Content = page;
            Title = $"{page.Title} – Quản lý văn bản đi – đến";
            page.OnShow();
        }
        catch (Exception ex)
        {
            Dlg.Handle(ex, "mở trang " + key);
        }
    }

    public void UpdateStatus()
    {
        if (!Ctx.Session.LoggedIn) return;
        try
        {
            var tenCq = Ctx.CauHinh.Get(ConfigKeys.TenCoQuan);
            var last = Ctx.CauHinh.Get(ConfigKeys.LanSaoLuuCuoi);
            var lastText = DateTime.TryParse(last, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var t)
                ? t.ToString("dd/MM/yyyy HH:mm") : "chưa sao lưu";
            TxtStatus.Text = $"{(string.IsNullOrEmpty(tenCq) ? "(chưa cấu hình tên cơ quan)" : tenCq)}   •   Năm làm việc: {Ctx.Clock.Today.Year}   •   Sao lưu gần nhất: {lastText}";
            TxtStatusRight.Text = $"{(Ctx.Paths.Portable ? "Bản portable" : "Bản cài đặt")} • Dữ liệu: {Ctx.Paths.Root}";
            SampleBanner.Visibility = Ctx.DuLieuMau.DangCoDuLieuMau ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            Ctx.Log.Warning("Cập nhật thanh trạng thái lỗi: {Err}", Logging.Describe(ex));
        }
    }

    private void CmdNew(object sender, ExecutedRoutedEventArgs e) => _current?.NewItem();
    private void CmdFind(object sender, ExecutedRoutedEventArgs e) => _current?.FocusSearch();
    private void CmdPrint(object sender, ExecutedRoutedEventArgs e) => _current?.Print();
    private void CmdRefresh(object sender, ExecutedRoutedEventArgs e) => Dlg.Try(() => _current?.Reload(), "làm mới");
    private void CmdLock(object sender, ExecutedRoutedEventArgs e) => App.Current.Idle?.Lock();
    private void BtnLock_Click(object sender, RoutedEventArgs e) => App.Current.Idle?.Lock();

    private void BtnLogout_Click(object sender, RoutedEventArgs e)
    {
        if (Dlg.Confirm("Đăng xuất khỏi phần mềm?")) App.Current.Logout();
    }

    private void BtnDeleteSample_Click(object sender, RoutedEventArgs e)
    {
        if (!Dlg.Confirm("Xóa toàn bộ dữ liệu mẫu? Văn bản thật (nếu có) không bị ảnh hưởng.", danger: true)) return;
        if (Dlg.Try(() =>
            {
                var n = Ctx.DuLieuMau.Xoa();
                Dlg.Info($"Đã xóa {n} văn bản mẫu. Số thứ tự của năm sẽ bắt đầu lại từ 01 nếu năm đó chưa có văn bản thật.");
            }, "xóa dữ liệu mẫu"))
        {
            Ctx.NotifyChanged();
            _current?.Reload();
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (App.Current.LoggingOut || !Ctx.Session.LoggedIn) return;
        if (!Dlg.Confirm("Thoát phần mềm?"))
        {
            e.Cancel = true;
            return;
        }
        try
        {
            if (Ctx.CauHinh.GetBool(ConfigKeys.TuSaoLuuKhiThoat, true))
                using (new WaitCursor()) Ctx.Backup.Create(BackupKind.TuDongKhiThoat);
        }
        catch (Exception ex)
        {
            Ctx.Log.Error("Sao lưu khi thoát lỗi: {Err}", Logging.Describe(ex));
            if (!Dlg.Confirm("Không tự sao lưu được khi thoát. Vẫn thoát phần mềm?", danger: true))
            {
                e.Cancel = true;
                return;
            }
        }
        Ctx.Session.Logout();
        System.Windows.Application.Current.Shutdown(0);
    }
}
