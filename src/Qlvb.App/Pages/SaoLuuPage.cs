using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Qlvb.App.Infrastructure;
using Qlvb.Infrastructure.Services;

namespace Qlvb.App.Pages;

public sealed class SaoLuuPage : UserControl, IPage
{
    private readonly DataGrid _grid = new();
    public string Title => "Sao lưu / Khôi phục";

    private sealed record Row(BackupInfo B, string ThoiGian, string Loai, string KichThuoc, string Tep);

    public SaoLuuPage()
    {
        var dock = new DockPanel();
        var h = new TextBlock { Text = "Sao lưu và khôi phục dữ liệu", Style = (Style)FindResource("H1") };
        DockPanel.SetDock(h, Dock.Top);
        dock.Children.Add(h);
        var info = new Border
        {
            Style = (Style)FindResource("WarnBox"),
            Child = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text = "Bản sao lưu (.qlvbak) được mã hóa bằng khóa dữ liệu và chỉ mở được bằng mật khẩu đang dùng TẠI THỜI ĐIỂM SAO LƯU (hoặc mã khôi phục lúc đó). " +
                       "Bản sao lưu vẫn là tài liệu chứa bí mật nhà nước: chỉ lưu trên thiết bị được quản lý, không lưu lên mạng, đám mây, thư mục dùng chung.",
            },
        };
        DockPanel.SetDock(info, Dock.Top);
        dock.Children.Add(info);
        var bar = new WrapPanel { Margin = new Thickness(0, 8, 0, 8) };
        void B(string t, RoutedEventHandler c, bool primary = false)
        {
            var b = new Button { Content = t };
            if (primary) b.Style = (Style)FindResource("PrimaryButton");
            b.Click += c;
            bar.Children.Add(b);
        }
        B("Sao lưu nhanh", (_, _) => Quick(), true);
        B("Sao lưu ra vị trí khác…", (_, _) => SaveAs());
        B("Khôi phục từ tệp…", (_, _) => { if (RestoreHelper.RunFromFile(Window.GetWindow(this)!)) OnShow(); });
        B("Khôi phục bản đã chọn", (_, _) => RestoreSelected());
        B("Mở thư mục sao lưu", (_, _) => OpenFolder());
        DockPanel.SetDock(bar, Dock.Top);
        dock.Children.Add(bar);
        var sub = new TextBlock { Style = (Style)FindResource("Hint"), Margin = new Thickness(0, 0, 0, 4), Text = "Các bản sao lưu trong thư mục mặc định:" };
        DockPanel.SetDock(sub, Dock.Top);
        dock.Children.Add(sub);
        _grid.Columns.Add(DanhMucPage.Col("Thời gian", nameof(Row.ThoiGian), 150));
        _grid.Columns.Add(DanhMucPage.Col("Loại", nameof(Row.Loai), 260));
        _grid.Columns.Add(DanhMucPage.Col("Dung lượng", nameof(Row.KichThuoc), 110));
        _grid.Columns.Add(DanhMucPage.Col("Tệp", nameof(Row.Tep), 1, true));
        dock.Children.Add(_grid);
        Content = dock;
    }

    public void OnShow() => Dlg.Try(() =>
        _grid.ItemsSource = Ctx.Backup.List().Select(b => new Row(b, b.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss"),
            b.Kind is { } k ? BackupService.KindName(k) : "(không đọc được)", $"{b.Size / 1024.0:#,0} KB", System.IO.Path.GetFileName(b.Path))).ToList(), "danh sách sao lưu");

    private void Quick()
    {
        if (Dlg.Try(() =>
            {
                string p;
                using (new WaitCursor()) p = Ctx.Backup.Create(BackupKind.Nhanh);
                Dlg.Info("Đã sao lưu: " + p);
            }, "sao lưu"))
        {
            Ctx.NotifyChanged();
            OnShow();
        }
    }

    private void SaveAs()
    {
        var sfd = new SaveFileDialog
        {
            Title = "Chọn nơi lưu bản sao lưu",
            Filter = "Tệp sao lưu QLVB (*.qlvbak)|*.qlvbak",
            FileName = Ctx.Backup.DefaultFileName(BackupKind.ThuCong),
        };
        if (sfd.ShowDialog(Window.GetWindow(this)) != true) return;
        if (!SafeLocation.Check(sfd.FileName, "bản sao lưu")) return;
        if (Dlg.Try(() =>
            {
                using (new WaitCursor()) Ctx.Backup.Create(BackupKind.ThuCong, sfd.FileName);
                Dlg.Info("Đã sao lưu: " + sfd.FileName);
            }, "sao lưu"))
            Ctx.NotifyChanged();
    }

    private void RestoreSelected()
    {
        if (_grid.SelectedItem is not Row r) { Dlg.Info("Hãy chọn một bản sao lưu trong danh sách."); return; }
        if (RestoreHelper.RunFromFile(Window.GetWindow(this)!, r.B.Path)) OnShow();
    }

    private static void OpenFolder() => Dlg.Try(() =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Ctx.Paths.Backup}\"") { UseShellExecute = true }));
}
