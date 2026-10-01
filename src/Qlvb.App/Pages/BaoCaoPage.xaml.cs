using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Qlvb.App.Infrastructure;
using Qlvb.App.Printing;
using Qlvb.Application;
using Qlvb.Domain;
using Qlvb.Infrastructure.Services;

namespace Qlvb.App.Pages;

public partial class BaoCaoPage : UserControl, IPage
{
    private bool _init;
    private List<Row> _rows = [];
    private string _title = "";
    private string _subtitle = "";

    public string Title => "Báo cáo – Thống kê";

    private sealed record Row(int Stt, string Nhom, int SoLuong, string TyLe, double Bar);

    private static readonly KeyValuePair<TieuChiThongKe, string>[] TieuChi =
    [
        new(TieuChiThongKe.Nam, "Năm"), new(TieuChiThongKe.Thang, "Tháng"), new(TieuChiThongKe.DoMat, "Độ mật"),
        new(TieuChiThongKe.LoaiVanBan, "Loại văn bản"), new(TieuChiThongKe.CoQuanBanHanh, "Cơ quan ban hành (sổ đến)"),
        new(TieuChiThongKe.NguoiKy, "Người ký (sổ đi)"), new(TieuChiThongKe.DonVi, "Đơn vị (lưu/nhận)"),
    ];

    public BaoCaoPage() => InitializeComponent();

    public void OnShow()
    {
        if (_init) return;
        _init = true;
        CbTieuChi.ItemsSource = TieuChi;
        CbTieuChi.SelectedIndex = 1;
        CbLoai.SelectedIndex = 0;
        var nam = new List<KeyValuePair<int?, string>> { new(null, "Mọi năm") };
        nam.AddRange(Ctx.TraCuu.CacNam().Select(n => new KeyValuePair<int?, string>(n, n.ToString())));
        CbNam.ItemsSource = nam;
        CbNam.SelectedIndex = nam.FindIndex(n => n.Key == Ctx.Clock.Today.Year) is var i and >= 0 ? i : 0;
        var dm = new List<KeyValuePair<int?, string>> { new(null, "Tất cả") };
        dm.AddRange(Ctx.DanhMuc.ListDoMat(true).Select(d => new KeyValuePair<int?, string>(d.Id, d.HienThi)));
        CbDoMat.ItemsSource = dm;
        CbDoMat.SelectedIndex = 0;
        Run();
    }

    public void Reload() => Run();

    private void BtnRun_Click(object sender, RoutedEventArgs e) => Run();

    private void Run() => Dlg.Try(() =>
    {
        var tc = ((KeyValuePair<TieuChiThongKe, string>)CbTieuChi.SelectedItem).Key;
        var loai = CbLoai.SelectedIndex switch { 1 => LoaiSo.Di, 2 => LoaiSo.Den, _ => (LoaiSo?)null };
        var f = new ThongKeFilter
        {
            Loai = loai,
            Nam = (CbNam.SelectedItem as KeyValuePair<int?, string>?)?.Key,
            TuNgay = FormKit.ToDate(DpTu),
            DenNgay = FormKit.ToDate(DpDen),
            DoMatId = (CbDoMat.SelectedItem as KeyValuePair<int?, string>?)?.Key,
            BaoGomDaHuy = ChkHuy.IsChecked == true,
        };
        var data = Ctx.TraCuu.ThongKe(tc, f);
        var total = data.Sum(x => x.SoLuong);
        var max = Math.Max(1, data.Select(x => x.SoLuong).DefaultIfEmpty(0).Max());
        _rows = data.Select((x, i) => new Row(i + 1, x.Nhom, x.SoLuong, total == 0 ? "" : $"{100.0 * x.SoLuong / total:0.0}%", 2 + 300.0 * x.SoLuong / max)).ToList();
        Grid.ItemsSource = _rows;
        ColNhom.Header = ((KeyValuePair<TieuChiThongKe, string>)CbTieuChi.SelectedItem).Value;
        TxtTotal.Text = $"Tổng cộng: {total} văn bản";
        _title = $"THỐNG KÊ VĂN BẢN {(loai == null ? "ĐI VÀ ĐẾN" : loai == LoaiSo.Di ? "ĐI" : "ĐẾN")} THEO {((KeyValuePair<TieuChiThongKe, string>)CbTieuChi.SelectedItem).Value.ToUpperInvariant()}";
        var parts = new List<string>();
        if (f.Nam is { } n) parts.Add($"năm {n}");
        if (f.TuNgay is { } a) parts.Add($"từ {TextUtil.FormatDate(a)}");
        if (f.DenNgay is { } b) parts.Add($"đến {TextUtil.FormatDate(b)}");
        if (f.DoMatId != null) parts.Add($"độ mật {((KeyValuePair<int?, string>)CbDoMat.SelectedItem).Value}");
        parts.Add(f.BaoGomDaHuy ? "gồm văn bản đã hủy" : "không tính văn bản đã hủy");
        _subtitle = string.Join(", ", parts);
    }, "thống kê");

    private void BtnPrint_Click(object sender, RoutedEventArgs e) => Print();

    public void Print() => Dlg.Try(() =>
    {
        if (_rows.Count == 0) { Dlg.Info("Chưa có số liệu để in."); return; }
        var spec = new TableSpec
        {
            Landscape = false,
            RunningTitle = Ctx.CauHinh.Get(ConfigKeys.TenCoQuan),
            FirstPageTitle = [_title, "(" + _subtitle + ")"],
            Columns = [new("STT", 0.6, true), new((string)ColNhom.Header, 4, false), new("Số lượng", 1.2, true), new("Tỷ lệ", 1, true)],
            Rows = [.. _rows.Select(r => new[] { r.Stt.ToString(), r.Nhom, r.SoLuong.ToString(), r.TyLe }),
                    new[] { "", "Tổng cộng", _rows.Sum(r => r.SoLuong).ToString(), "100%" }],
            NumberRow = false,
        };
        PrintPreviewWindow.Show(Window.GetWindow(this)!, TableDocument.Build(spec, Ctx.Clock.Now), "Báo cáo thống kê", "In báo cáo: " + _title, false);
    }, "in báo cáo");

    private void BtnExport_Click(object sender, RoutedEventArgs e) => Dlg.Try(() =>
    {
        if (_rows.Count == 0) { Dlg.Info("Chưa có số liệu để xuất."); return; }
        var sfd = new SaveFileDialog
        {
            Title = "Xuất báo cáo thống kê",
            Filter = "Excel (*.xlsx)|*.xlsx|CSV UTF-8 (*.csv)|*.csv",
            FileName = $"ThongKe_{Ctx.Clock.Now:yyyyMMdd_HHmm}",
            InitialDirectory = Ctx.Paths.Export,
        };
        if (sfd.ShowDialog(Window.GetWindow(this)) != true) return;
        var header = new[] { "STT", (string)ColNhom.Header, "Số lượng", "Tỷ lệ" };
        var rows = _rows.Select(r => (IReadOnlyList<string>)[r.Stt.ToString(), r.Nhom, r.SoLuong.ToString(), r.TyLe]).ToList();
        Ctx.Export.ExportStatistics($"{_title} ({_subtitle})", header, rows, sfd.FilterIndex == 2 ? ExportFormat.Csv : ExportFormat.Xlsx, sfd.FileName);
        Dlg.Info("Đã xuất tệp: " + sfd.FileName + "\n\nLưu ý: tên cơ quan, người ký trong thống kê có thể là thông tin cần bảo vệ; quản lý tệp theo quy định.");
    }, "xuất thống kê");
}
