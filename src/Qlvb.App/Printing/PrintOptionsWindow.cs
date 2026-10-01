using System.Windows;
using System.Windows.Controls;
using Qlvb.App.Infrastructure;
using Qlvb.Domain;

namespace Qlvb.App.Printing;

/// <summary>Chọn phạm vi và tùy chọn in sổ.</summary>
public sealed class PrintOptionsWindow : Window
{
    private readonly LoaiSo _loai;
    private readonly ComboBox _nam = new() { Width = 100, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly ComboBox _so = new() { DisplayMemberPath = "Value" };
    private readonly DatePicker _tu = new();
    private readonly DatePicker _den = new();
    private readonly CheckBox _cover = new() { Content = "In trang bìa sổ", IsChecked = true };
    private readonly CheckBox _huy = new() { Content = "Gồm văn bản đã hủy (ghi chú \"ĐÃ HỦY\")", IsChecked = true };
    private readonly CheckBox _kyHieu = new() { Content = "Độ mật ghi bằng ký hiệu A, B, C" };
    private readonly RadioButton _ngang = new() { Content = "Khổ A4 ngang", IsChecked = true, GroupName = "kho" };
    private readonly RadioButton _doc = new() { Content = "Khổ A4 dọc", GroupName = "kho", Margin = new Thickness(16, 4, 0, 4) };

    private PrintOptionsWindow(LoaiSo loai, int nam, long? soId)
    {
        _loai = loai;
        Title = "In sổ " + Ctx.TenLoaiSo(loai);
        Width = 480;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        var sp = new StackPanel { Margin = new Thickness(20) };
        sp.Children.Add(new TextBlock { Text = "Năm", Style = (Style)FindResource("Label") });
        sp.Children.Add(_nam);
        sp.Children.Add(new TextBlock { Text = "Quyển sổ", Style = (Style)FindResource("Label") });
        sp.Children.Add(_so);
        var dates = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        dates.ColumnDefinitions.Add(new ColumnDefinition());
        dates.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        dates.ColumnDefinitions.Add(new ColumnDefinition());
        var l1 = new StackPanel();
        l1.Children.Add(new TextBlock { Text = loai == LoaiSo.Di ? "Từ ngày đăng ký (tùy chọn)" : "Từ ngày đến (tùy chọn)", Style = (Style)FindResource("Hint") });
        l1.Children.Add(_tu);
        var l2 = new StackPanel();
        l2.Children.Add(new TextBlock { Text = "Đến ngày (tùy chọn)", Style = (Style)FindResource("Hint") });
        l2.Children.Add(_den);
        Grid.SetColumn(l2, 2);
        dates.Children.Add(l1);
        dates.Children.Add(l2);
        sp.Children.Add(dates);
        sp.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 6) });
        sp.Children.Add(_cover);
        sp.Children.Add(_huy);
        sp.Children.Add(_kyHieu);
        var kho = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        kho.Children.Add(_ngang);
        kho.Children.Add(_doc);
        sp.Children.Add(kho);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var ok = new Button { Content = "Xem trước", IsDefault = true, Style = (Style)FindResource("PrimaryButton") };
        ok.Click += (_, _) => Preview();
        buttons.Children.Add(ok);
        buttons.Children.Add(new Button { Content = "Đóng", IsCancel = true });
        sp.Children.Add(buttons);
        Content = sp;

        _kyHieu.IsChecked = Ctx.InKyHieuDoMat;
        _nam.ItemsSource = Ctx.TraCuu.CacNam(loai);
        _nam.SelectionChanged += (_, _) => LoadBooks(null);
        _nam.SelectedItem = nam;
        LoadBooks(soId);
    }

    private void LoadBooks(long? select)
    {
        var nam = _nam.SelectedItem as int?;
        var items = new List<KeyValuePair<long?, string>> { new(null, "Cả năm (mọi quyển)") };
        if (nam != null)
            items.AddRange(Ctx.So.List(_loai).Where(s => s.Nam == nam).OrderBy(s => s.QuyenSo)
                .Select(s => new KeyValuePair<long?, string>(s.Id, $"Quyển {s.QuyenSo} ({s.SoBanGhi} văn bản){(s.DaKhoa ? " – đã khóa" : "")}")));
        _so.ItemsSource = items;
        _so.SelectedIndex = Math.Max(0, items.FindIndex(i => i.Key == select));
    }

    private void Preview()
    {
        if (_nam.SelectedItem is not int nam) return;
        Dlg.Try(() =>
        {
            var soId = (_so.SelectedItem as KeyValuePair<long?, string>?)?.Key;
            var so = soId is { } id ? Ctx.Store.GetSoDangKy(id) : null;
            var bm = so != null ? Ctx.Store.GetBieuMau(so.MaBieuMau)! : DocActions.FormFor(_loai, nam);
            var data = SoPrinter.Data(Ctx.Store, _loai, nam, soId, _huy.IsChecked == true, FormKit.ToDate(_tu), FormKit.ToDate(_den));
            var opts = new SoPrintOptions
            {
                Landscape = _ngang.IsChecked == true, Cover = _cover.IsChecked == true,
                KyHieuDoMat = _kyHieu.IsChecked == true, BaoGomDaHuy = _huy.IsChecked == true,
            };
            System.Windows.Documents.FixedDocument doc;
            using (new WaitCursor())
                doc = SoPrinter.Build(bm, so, nam, data, opts, so?.TenCoQuan is { Length: > 0 } t ? t : Ctx.CauHinh.Get(Qlvb.Application.ConfigKeys.TenCoQuan),
                    so?.CoQuanChuQuan ?? Ctx.CauHinh.Get(Qlvb.Application.ConfigKeys.CoQuanChuQuan), Ctx.Clock.Now);
            var desc = $"In sổ {Ctx.TenLoaiSo(_loai)} năm {nam}{(so != null ? ", quyển " + so.QuyenSo : "")}, {data.Count} văn bản";
            PrintPreviewWindow.Show(this, doc, $"Sổ {Ctx.TenLoaiSo(_loai)} năm {nam}", desc, opts.Landscape);
        }, "in sổ");
    }

    public static void Show(Window owner, LoaiSo loai, int nam, long? soId) =>
        new PrintOptionsWindow(loai, nam, soId) { Owner = owner }.ShowDialog();
}
