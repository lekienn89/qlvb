using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using Qlvb.App.Infrastructure;
using Qlvb.App.Printing;
using Qlvb.App.Views;
using Qlvb.Application;
using Qlvb.Domain;
using Qlvb.Infrastructure.Services;

namespace Qlvb.App.Pages;

public partial class DanhSachPage : UserControl, IPage
{
    private readonly LoaiSo _loai;
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private bool _init;
    private int _page;
    private int _total;
    private string _sort = "so_thu_tu";
    private bool _desc = true;
    private BieuMau? _form;

    public string Title => _loai == LoaiSo.Di ? "Văn bản đi" : "Văn bản đến";

    public DanhSachPage(LoaiSo loai)
    {
        InitializeComponent();
        _loai = loai;
        TxtTitle.Text = loai == LoaiSo.Di ? "Sổ đăng ký văn bản mật đi" : "Sổ đăng ký văn bản mật đến";
        TxtDateLabel.Text = loai == LoaiSo.Di ? "Ngày VB từ" : "Ngày đến từ";
        _debounce.Tick += (_, _) => { _debounce.Stop(); _page = 0; Load(); };
        Ctx.DataChanged += () => { if (IsVisible) Dispatcher.BeginInvoke(Load); };
    }

    public void OnShow()
    {
        if (!_init)
        {
            _init = true;
            var saved = SafeGet("page_size");
            CbPageSize.SelectedIndex = saved is >= 0 and <= 4 ? saved : 1;
            CbTrangThai.SelectedIndex = 0;
            LoadYears();
            LoadDoMat();
        }
        Load();
    }

    private static int SafeGet(string key) => FormKit.Remember.TryGetValue("ds." + key, out var s) && int.TryParse(s, out var i) ? i : -1;

    private void LoadYears()
    {
        var nam = CbNam.SelectedItem as int?;
        _init = false;
        CbNam.ItemsSource = Ctx.TraCuu.CacNam(_loai);
        CbNam.SelectedItem = nam ?? Ctx.Clock.Today.Year;
        _init = true;
        LoadBooks();
    }

    private void LoadBooks()
    {
        var nam = CbNam.SelectedItem as int?;
        var items = new List<KeyValuePair<long?, string>> { new(null, "Tất cả quyển") };
        if (nam != null)
            items.AddRange(Ctx.So.List(_loai).Where(s => s.Nam == nam).OrderBy(s => s.QuyenSo)
                .Select(s => new KeyValuePair<long?, string>(s.Id, $"Quyển {s.QuyenSo}{(s.DaKhoa ? " (đã khóa)" : "")}")));
        var old = _init;
        _init = false;
        CbSo.ItemsSource = items;
        CbSo.SelectedIndex = 0;
        _init = old;
    }

    private void LoadDoMat()
    {
        var items = new List<KeyValuePair<int?, string>> { new(null, "Tất cả") };
        items.AddRange(Ctx.DanhMuc.ListDoMat(true).Select(d => new KeyValuePair<int?, string>(d.Id, d.HienThi)));
        var old = _init;
        _init = false;
        CbDoMat.ItemsSource = items;
        CbDoMat.SelectedIndex = 0;
        _init = old;
    }

    private int PageSize => CbPageSize.SelectedIndex switch { 0 => 20, 1 => 50, 2 => 100, 3 => 200, _ => 0 };

    private SearchCriteria Criteria(bool paged) => new()
    {
        Loai = _loai,
        Nam = CbNam.SelectedItem as int?,
        SoDangKyId = (CbSo.SelectedItem as KeyValuePair<long?, string>?)?.Key,
        DoMatId = (CbDoMat.SelectedItem as KeyValuePair<int?, string>?)?.Key,
        TrangThai = CbTrangThai.SelectedIndex switch { 1 => LocTrangThai.HieuLuc, 2 => LocTrangThai.DaHuy, _ => LocTrangThai.TatCa },
        TuNgay = FormKit.ToDate(DpTu),
        DenNgay = FormKit.ToDate(DpDen),
        TuKhoa = TxtSearch.Text,
        SapXep = _sort,
        Giam = _desc,
        Limit = paged ? PageSize : 0,
        Offset = paged ? _page * PageSize : 0,
    };

    private void Load()
    {
        if (!_init || !Ctx.Session.LoggedIn) return;
        Dlg.Try(() =>
        {
            var nam = CbNam.SelectedItem as int?;
            var form = DocActions.FormFor(_loai, nam);
            if (_form?.Ma != form.Ma)
            {
                _form = form;
                DocGrid.Build(Grid, form);
            }
            TxtForm.Text = $"Theo {form.CanCu}";
            var r = Ctx.TraCuu.Tim(Criteria(true));
            _total = r.Total;
            var pages = PageSize == 0 ? 1 : Math.Max(1, (int)Math.Ceiling(_total / (double)PageSize));
            if (_page >= pages)
            {
                _page = pages - 1;
                r = Ctx.TraCuu.Tim(Criteria(true));
            }
            var kh = Ctx.InKyHieuDoMat;
            Grid.ItemsSource = r.Items.Select(v => DocRow.From(v, form, kh)).ToList();
            TxtPage.Text = $"Trang {_page + 1}/{pages}";
            TxtTotal.Text = $"Tổng: {_total} văn bản";
        }, "tải danh sách");
    }

    private void Filter_Changed(object sender, EventArgs e)
    {
        if (!_init) return;
        _debounce.Stop();
        _debounce.Start();
    }

    private void CbNam_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_init) return;
        LoadBooks();
        _page = 0;
        Load();
    }

    private void CbPageSize_Changed(object sender, SelectionChangedEventArgs e)
    {
        FormKit.Remember["ds.page_size"] = CbPageSize.SelectedIndex.ToString();
        _page = 0;
        Load();
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        _init = false;
        TxtSearch.Text = "";
        CbTrangThai.SelectedIndex = 0;
        CbDoMat.SelectedIndex = 0;
        CbSo.SelectedIndex = 0;
        DpTu.SelectedDate = null;
        DpDen.SelectedDate = null;
        _init = true;
        _page = 0;
        Load();
    }

    private void BtnFirst_Click(object sender, RoutedEventArgs e) { _page = 0; Load(); }
    private void BtnPrev_Click(object sender, RoutedEventArgs e) { if (_page > 0) { _page--; Load(); } }
    private void BtnNext_Click(object sender, RoutedEventArgs e) { _page++; Load(); }
    private void BtnLast_Click(object sender, RoutedEventArgs e) { _page = int.MaxValue / 2; Load(); }

    private void Grid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        if (string.IsNullOrEmpty(e.Column.SortMemberPath)) return;
        if (_sort == e.Column.SortMemberPath) _desc = !_desc;
        else { _sort = e.Column.SortMemberPath; _desc = false; }
        foreach (var c in Grid.Columns) c.SortDirection = null;
        e.Column.SortDirection = _desc ? System.ComponentModel.ListSortDirection.Descending : System.ComponentModel.ListSortDirection.Ascending;
        Load();
    }

    private VanBanBase? Selected => (Grid.SelectedItem as DocRow)?.V;
    private Window Owner => Window.GetWindow(this)!;

    private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement { DataContext: DocRow }) BtnEdit_Click(sender, e);
    }

    private void Grid_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Selected != null) { BtnEdit_Click(sender, e); e.Handled = true; }
    }

    private VanBanBase? RequireSelected()
    {
        var v = Selected;
        if (v == null) Dlg.Info("Hãy chọn một văn bản trong danh sách.");
        return v;
    }

    private void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (RequireSelected() is { } v && DocActions.Edit(Owner, v)) Load();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        if (RequireSelected() is { } v) DocActions.Cancel(Owner, v);
    }

    private void BtnRestore_Click(object sender, RoutedEventArgs e)
    {
        if (RequireSelected() is { } v) DocActions.Restore(v);
    }

    private void MnuHardDelete_Click(object sender, RoutedEventArgs e)
    {
        if (RequireSelected() is { } v) DocActions.Delete(Owner, v);
    }

    private void MnuCopy_Click(object sender, RoutedEventArgs e)
    {
        if (RequireSelected() is not { } v) return;
        Dlg.Try(() =>
        {
            Window w = v is VanBanDi di ? new VanBanDiWindow(copyFrom: Ctx.Store.GetDi(di.Id)) : new VanBanDenWindow(copyFrom: (VanBanDen)v);
            w.Owner = Owner;
            w.ShowDialog();
        });
    }

    private void BtnNew_Click(object sender, RoutedEventArgs e) => NewItem();

    public void NewItem()
    {
        if (Dlg.Try(() => DocActions.New(Owner, _loai))) { LoadYears(); Load(); }
    }

    public void FocusSearch()
    {
        TxtSearch.Focus();
        TxtSearch.SelectAll();
    }

    public void Reload()
    {
        LoadYears();
        LoadDoMat();
        Load();
    }

    private void BtnPrint_Click(object sender, RoutedEventArgs e) => Print();

    public void Print() => Dlg.Try(() =>
    {
        var nam = CbNam.SelectedItem as int? ?? Ctx.Clock.Today.Year;
        var soId = (CbSo.SelectedItem as KeyValuePair<long?, string>?)?.Key;
        PrintOptionsWindow.Show(Owner, _loai, nam, soId);
    }, "in sổ");

    private void BtnExport_Click(object sender, RoutedEventArgs e) => Dlg.Try(() =>
    {
        if (!Ctx.Export.DocumentExportAllowed)
        {
            Dlg.Warn("Xuất danh sách văn bản mật ra tệp Excel/CSV đang bị TẮT theo cấu hình bảo mật mặc định.\n\n" +
                     "Lý do: tệp xuất ra không được mã hóa, dễ bị sao chép ra ngoài. Hãy dùng chức năng In sổ.\n" +
                     "Người có thẩm quyền có thể bật lại trong Cấu hình → Bảo mật.");
            return;
        }
        var criteria = Criteria(false);
        var items = Ctx.TraCuu.Tim(criteria).Items;
        if (items.Count == 0) { Dlg.Info("Không có văn bản nào để xuất."); return; }
        if (!Dlg.Confirm($"Xuất {items.Count} văn bản mật ra tệp KHÔNG MÃ HÓA.\n\n" +
                         "• Tệp xuất ra phải được quản lý như tài liệu bí mật nhà nước.\n" +
                         "• Không lưu vào thư mục dùng chung, USB không được quản lý, hoặc máy có kết nối mạng.\n" +
                         "• Thao tác được ghi vào nhật ký.\n\nTiếp tục?", danger: true))
            return;
        var sfd = new SaveFileDialog
        {
            Title = "Xuất danh sách văn bản",
            Filter = "Excel (*.xlsx)|*.xlsx|CSV UTF-8 (*.csv)|*.csv",
            FileName = $"{(_loai == LoaiSo.Di ? "VanBanDi" : "VanBanDen")}_{criteria.Nam}_{Ctx.Clock.Now:yyyyMMdd_HHmm}",
            InitialDirectory = Ctx.Paths.Export,
        };
        if (sfd.ShowDialog(Owner) != true) return;
        if (!SafeLocation.Check(sfd.FileName, "tệp xuất")) return;
        var fmt = sfd.FilterIndex == 2 ? ExportFormat.Csv : ExportFormat.Xlsx;
        using (new WaitCursor())
            Ctx.Export.ExportDocuments(items, DocActions.FormFor(_loai, criteria.Nam), fmt, sfd.FileName, Ctx.InKyHieuDoMat);
        Dlg.Info("Đã xuất tệp: " + sfd.FileName);
    }, "xuất tệp");
}
