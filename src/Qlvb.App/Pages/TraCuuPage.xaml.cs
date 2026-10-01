using System.Windows;
using System.Windows.Controls;
using Qlvb.App.Infrastructure;
using Qlvb.App.Printing;
using Qlvb.Application;
using Qlvb.Domain;

namespace Qlvb.App.Pages;

public partial class TraCuuPage : UserControl, IPage
{
    private const int MaxResults = 2000;
    private bool _init;
    private BieuMau? _form;
    private IReadOnlyList<VanBanBase> _items = [];

    public string Title => "Tra cứu";

    public TraCuuPage()
    {
        InitializeComponent();
    }

    private LoaiSo Loai => CbLoai.SelectedIndex == 1 ? LoaiSo.Den : LoaiSo.Di;

    public void OnShow()
    {
        if (_init) return;
        _init = true;
        CbTrangThai.SelectedIndex = 0;
        CbLoai.SelectedIndex = 0;
        var dm = new List<KeyValuePair<int?, string>> { new(null, "Tất cả") };
        dm.AddRange(Ctx.DanhMuc.ListDoMat(true).Select(d => new KeyValuePair<int?, string>(d.Id, d.HienThi)));
        CbDoMat.ItemsSource = dm;
        CbDoMat.SelectedIndex = 0;
        LoadSuggestions();
    }

    public void Reload()
    {
        LoadSuggestions();
        if (_items.Count > 0) Search();
    }

    private void LoadSuggestions()
    {
        var nam = new List<KeyValuePair<int?, string>> { new(null, "Mọi năm") };
        nam.AddRange(Ctx.TraCuu.CacNam(Loai).Select(n => new KeyValuePair<int?, string>(n, n.ToString())));
        CbNam.ItemsSource = nam;
        CbNam.SelectedIndex = 0;
        CbTenLoai.ItemsSource = Ctx.DanhMuc.GoiY(NhomDanhMuc.LoaiVanBan);
        CbNguoiKy.ItemsSource = Ctx.DanhMuc.GoiY(NhomDanhMuc.NguoiKy);
        CbNoiNhan.ItemsSource = Ctx.DanhMuc.GoiY(NhomDanhMuc.NoiNhan);
        CbDonViLuu.ItemsSource = Ctx.DanhMuc.GoiY(NhomDanhMuc.DonVi);
        CbDonViNhan.ItemsSource = Ctx.DanhMuc.GoiY(NhomDanhMuc.DonVi);
        CbCoQuan.ItemsSource = Ctx.DanhMuc.GoiY(NhomDanhMuc.CoQuanBanHanh);
    }

    private void CbLoai_Changed(object sender, SelectionChangedEventArgs e)
    {
        var di = Loai == LoaiSo.Di;
        foreach (var p in new UIElement[] { PnlNguoiKy, PnlNoiNhan, PnlDonViLuu }) p.Visibility = di ? Visibility.Visible : Visibility.Collapsed;
        foreach (var p in new UIElement[] { PnlCoQuan, PnlDonViNhan, PnlNgayVb, PnlSoDen }) p.Visibility = di ? Visibility.Collapsed : Visibility.Visible;
        LblNgay.Text = di ? "Ngày văn bản từ" : "Ngày đến từ";
        Grid.ItemsSource = null;
        _items = [];
        TxtTotal.Text = "";
        if (_init) Dlg.Try(LoadSuggestions);
    }

    private static int? Int(TextBox tb) => int.TryParse(tb.Text.Trim(), out var v) ? v : null;

    private void BtnSearch_Click(object sender, RoutedEventArgs e) => Search();

    private void Search() => Dlg.Try(() =>
    {
        var di = Loai == LoaiSo.Di;
        var c = new SearchCriteria
        {
            Loai = Loai,
            Nam = (CbNam.SelectedItem as KeyValuePair<int?, string>?)?.Key,
            TuKhoa = TxtTuKhoa.Text,
            SoThuTuTu = Int(TxtSttTu),
            SoThuTuDen = Int(TxtSttDen),
            SoDen = di ? null : Int(TxtSoDen),
            SoKyHieu = TxtSoKyHieu.Text,
            TenLoai = CbTenLoai.Text,
            TrichYeu = TxtTrichYeu.Text,
            DoMatId = (CbDoMat.SelectedItem as KeyValuePair<int?, string>?)?.Key,
            TuNgay = FormKit.ToDate(DpTu),
            DenNgay = FormKit.ToDate(DpDen),
            NguoiKy = di ? CbNguoiKy.Text : null,
            NoiNhan = di ? CbNoiNhan.Text : null,
            DonViLuu = di ? CbDonViLuu.Text : null,
            CoQuanBanHanh = di ? null : CbCoQuan.Text,
            DonViNhan = di ? null : CbDonViNhan.Text,
            NgayVanBanTu = di ? null : FormKit.ToDate(DpVbTu),
            NgayVanBanDen = di ? null : FormKit.ToDate(DpVbDen),
            TrangThai = CbTrangThai.SelectedIndex switch { 1 => LocTrangThai.HieuLuc, 2 => LocTrangThai.DaHuy, _ => LocTrangThai.TatCa },
            SapXep = "so_thu_tu",
            Giam = true,
            Limit = MaxResults,
        };
        var r = Ctx.TraCuu.Tim(c);
        _items = r.Items;
        _form = DocActions.FormFor(Loai, c.Nam);
        DocGrid.Build(Grid, _form);
        var kh = Ctx.InKyHieuDoMat;
        Grid.ItemsSource = r.Items.Select(v => DocRow.From(v, _form, kh)).ToList();
        TxtTotal.Text = r.Total > MaxResults
            ? $"Tìm thấy {r.Total} văn bản, hiển thị {MaxResults} văn bản mới nhất. Hãy thu hẹp điều kiện."
            : $"Tìm thấy {r.Total} văn bản.";
    }, "tra cứu");

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        foreach (var tb in new[] { TxtTuKhoa, TxtSttTu, TxtSttDen, TxtSoDen, TxtSoKyHieu, TxtTrichYeu }) tb.Text = "";
        foreach (var cb in new[] { CbTenLoai, CbNguoiKy, CbNoiNhan, CbDonViLuu, CbCoQuan, CbDonViNhan }) cb.Text = "";
        foreach (var dp in new[] { DpTu, DpDen, DpVbTu, DpVbDen }) dp.SelectedDate = null;
        CbNam.SelectedIndex = 0;
        CbDoMat.SelectedIndex = 0;
        CbTrangThai.SelectedIndex = 0;
    }

    private void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is DocRow r && DocActions.Edit(Window.GetWindow(this)!, r.V)) Search();
    }

    private void Grid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement { DataContext: DocRow }) BtnEdit_Click(sender, e);
    }

    public void FocusSearch() => TxtTuKhoa.Focus();

    private void BtnPrint_Click(object sender, RoutedEventArgs e) => Print();

    public void Print() => Dlg.Try(() =>
    {
        if (_form == null || _items.Count == 0) { Dlg.Info("Chưa có kết quả tra cứu để in."); return; }
        var kh = Ctx.InKyHieuDoMat;
        var items = _items.OrderBy(v => v.Nam).ThenBy(v => v.SoThuTu).ToList();
        var spec = new TableSpec
        {
            RunningTitle = $"Kết quả tra cứu {Ctx.TenLoaiSo(Loai)} – {Ctx.CauHinh.Get(ConfigKeys.TenCoQuan)}",
            FirstPageTitle = [$"KẾT QUẢ TRA CỨU {Ctx.TenLoaiSo(Loai).ToUpperInvariant()}", $"{items.Count} văn bản"],
            Columns = _form.Cot.Select(c => new PrintColumn(c.TieuDe, c.DoRong, c.CanGiua, $"({c.So})")).ToList(),
            Rows = items.Select(v => _form.Cot.Select(c => TruongBieuMau.GiaTri(v, c.Truong, kh)).ToArray()).ToList(),
            Italic = i => items[i].DaHuy,
        };
        var doc = TableDocument.Build(spec, Ctx.Clock.Now);
        PrintPreviewWindow.Show(Window.GetWindow(this)!, doc, "Kết quả tra cứu", $"In kết quả tra cứu {Ctx.TenLoaiSo(Loai)}, {items.Count} văn bản", true);
    }, "in kết quả");
}
