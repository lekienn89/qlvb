using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Qlvb.App.Infrastructure;
using Qlvb.Domain;

namespace Qlvb.App.Pages;

/// <summary>Quản lý danh mục dùng chung, độ mật, quyển sổ đăng ký.</summary>
public sealed class DanhMucPage : UserControl, IPage
{
    private readonly TabControl _tabs = new();
    public string Title => "Danh mục";

    public DanhMucPage()
    {
        var dock = new DockPanel();
        var h = new TextBlock { Text = "Danh mục dùng chung", Style = (Style)FindResource("H1") };
        DockPanel.SetDock(h, Dock.Top);
        dock.Children.Add(h);
        _tabs.Items.Add(new TabItem { Header = "Sổ đăng ký", Content = new SoDangKyTab() });
        foreach (var nhom in new[] { NhomDanhMuc.LoaiVanBan, NhomDanhMuc.NguoiKy, NhomDanhMuc.DonVi, NhomDanhMuc.NoiNhan, NhomDanhMuc.CoQuanBanHanh })
            _tabs.Items.Add(new TabItem { Header = NhomDanhMuc.TenNhom[nhom], Content = new DanhMucTab(nhom) });
        _tabs.Items.Add(new TabItem { Header = "Độ mật", Content = new DoMatTab() });
        _tabs.SelectionChanged += (_, e) => { if (e.Source == _tabs) Refresh(); };
        _tabs.SelectedIndex = 0;
        dock.Children.Add(_tabs);
        Content = dock;
    }

    public void OnShow() => Refresh();

    private void Refresh()
    {
        if ((_tabs.SelectedItem as TabItem)?.Content is IRefresh r) Dlg.Try(r.Refresh, "tải danh mục");
    }

    internal interface IRefresh { void Refresh(); }

    internal static DataGridTextColumn Col(string header, string path, double width, bool star = false) =>
        new() { Header = header, Binding = new Binding(path), Width = star ? new DataGridLength(width, DataGridLengthUnitType.Star) : new DataGridLength(width) };

    internal static StackPanel Bar(params (string Text, RoutedEventHandler Click)[] buttons)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        foreach (var (t, c) in buttons)
        {
            var b = new Button { Content = t };
            b.Click += c;
            sp.Children.Add(b);
        }
        return sp;
    }

    // ================================================================ danh mục chung
    private sealed class DanhMucTab : DockPanel, IRefresh
    {
        private readonly string _nhom;
        private readonly DataGrid _grid = new();
        private readonly TextBox _search = new() { Width = 260, HorizontalAlignment = HorizontalAlignment.Left };
        private readonly CheckBox _inactive = new() { Content = "Hiện cả mục ngừng sử dụng", IsChecked = true, Margin = new Thickness(12, 0, 0, 0) };

        private sealed record Row(MucDanhMuc M, string Ten, string PhuDe, int SoLanDung, string TrangThai);

        public DanhMucTab(string nhom)
        {
            _nhom = nhom;
            Margin = new Thickness(8);
            var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            top.Children.Add(new TextBlock { Text = "Tìm: ", VerticalAlignment = VerticalAlignment.Center });
            top.Children.Add(_search);
            top.Children.Add(_inactive);
            _search.TextChanged += (_, _) => Dlg.Try(Refresh);
            _inactive.Click += (_, _) => Dlg.Try(Refresh);
            SetDock(top, Dock.Top);
            Children.Add(top);
            var bar = Bar(("+ Thêm", (_, _) => Add()), ("Sửa", (_, _) => Edit()), ("Ngừng / Bật lại", (_, _) => Toggle()), ("Xóa", (_, _) => Delete()));
            if (nhom == NhomDanhMuc.LoaiVanBan)
            {
                var up = new Button { Content = "▲" , ToolTip = "Đưa lên" };
                up.Click += (_, _) => Move(-1);
                var down = new Button { Content = "▼", ToolTip = "Đưa xuống" };
                down.Click += (_, _) => Move(1);
                bar.Children.Add(up);
                bar.Children.Add(down);
            }
            bar.Children.Add(new TextBlock
            {
                Text = "Mục đã dùng trong văn bản không xóa được, chỉ ngừng sử dụng. Văn bản đã đăng ký giữ nguyên tên tại thời điểm đăng ký.",
                Style = (Style)FindResource("Hint"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0), MaxWidth = 520,
            });
            SetDock(bar, Dock.Bottom);
            Children.Add(bar);
            _grid.Columns.Add(Col("Tên", nameof(Row.Ten), 3, true));
            var phuDe = nhom switch { NhomDanhMuc.LoaiVanBan => "Chữ viết tắt", NhomDanhMuc.NguoiKy => "Chức vụ", _ => null };
            if (phuDe != null) _grid.Columns.Add(Col(phuDe, nameof(Row.PhuDe), 1, true));
            _grid.Columns.Add(Col("Số lần dùng", nameof(Row.SoLanDung), 100));
            _grid.Columns.Add(Col("Trạng thái", nameof(Row.TrangThai), 130));
            _grid.MouseDoubleClick += (_, _) => Edit();
            Children.Add(_grid);
        }

        public void Refresh()
        {
            var list = Ctx.DanhMuc.List(_nhom, _inactive.IsChecked == true, _search.Text);
            _grid.ItemsSource = list.Select(m => new Row(m, m.Ten, m.PhuDe ?? "", m.SoLanDung,
                (m.DangDung ? "Đang dùng" : "Ngừng sử dụng") + (m.LaDuLieuMau ? " (mẫu)" : ""))).ToList();
        }

        private MucDanhMuc? Sel => (_grid.SelectedItem as Row)?.M;
        private Window Owner => Window.GetWindow(this)!;

        private string? AskPhuDe(string ten, string? cur) => _nhom switch
        {
            NhomDanhMuc.LoaiVanBan => InputDialog.AskText(Owner, "Chữ viết tắt", $"Chữ viết tắt của \"{ten}\" (để trống nếu không có):", cur ?? "", 0, 20),
            NhomDanhMuc.NguoiKy => InputDialog.AskText(Owner, "Chức vụ", $"Chức vụ của \"{ten}\" (có thể để trống):", cur ?? "", 0, 200),
            _ => cur,
        };

        private void Add()
        {
            var ten = InputDialog.AskText(Owner, "Thêm danh mục", $"Tên {NhomDanhMuc.TenNhom[_nhom].ToLowerInvariant()} mới:", "", 1, Limits.TenMax, okText: "Thêm");
            if (ten == null) return;
            var phuDe = AskPhuDe(ten, null);
            if (Dlg.Try(() => Ctx.DanhMuc.Them(_nhom, ten, phuDe), "thêm danh mục")) Refresh();
        }

        private void Edit()
        {
            if (Sel is not { } m) { Dlg.Info("Hãy chọn một mục."); return; }
            var ten = InputDialog.AskText(Owner, "Sửa danh mục", "Tên:", m.Ten, 1, Limits.TenMax, okText: "Lưu");
            if (ten == null) return;
            var phuDe = AskPhuDe(ten, m.PhuDe);
            if (Dlg.Try(() => Ctx.DanhMuc.Sua(m, ten, phuDe), "sửa danh mục")) Refresh();
        }

        private void Toggle()
        {
            if (Sel is not { } m) { Dlg.Info("Hãy chọn một mục."); return; }
            if (Dlg.Try(() => Ctx.DanhMuc.DatTrangThai(m, !m.DangDung), "đổi trạng thái danh mục")) Refresh();
        }

        private void Delete()
        {
            if (Sel is not { } m) { Dlg.Info("Hãy chọn một mục."); return; }
            if (!Dlg.Confirm($"Xóa \"{m.Ten}\" khỏi danh mục?", danger: true)) return;
            if (Dlg.Try(() => Ctx.DanhMuc.Xoa(m), "xóa danh mục")) Refresh();
        }

        private void Move(int dir)
        {
            if (Sel is not { } m) return;
            var items = Ctx.DanhMuc.List(_nhom).ToList();
            var i = items.FindIndex(x => x.Id == m.Id);
            var j = i + dir;
            if (i < 0 || j < 0 || j >= items.Count) return;
            Dlg.Try(() =>
            {
                // Đánh lại thứ tự liên tục rồi đổi chỗ hai mục.
                for (var k = 0; k < items.Count; k++)
                {
                    var target = k == i ? j + 1 : k == j ? i + 1 : k + 1;
                    if (items[k].ThuTu != target) Ctx.DanhMuc.DoiThuTu(items[k], target);
                }
            });
            Refresh();
            foreach (var r in _grid.Items) if (r is Row row && row.M.Id == m.Id) { _grid.SelectedItem = r; break; }
        }
    }

    // ================================================================ độ mật
    private sealed class DoMatTab : DockPanel, IRefresh
    {
        private readonly DataGrid _grid = new();

        private sealed record Row(DoMat D, string Ten, string KyHieu, int Muc, string CamTrichYeu, string TrangThai);

        public DoMatTab()
        {
            Margin = new Thickness(8);
            var note = new Border
            {
                Style = (Style)FindResource("WarnBox"),
                Child = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    Text = "Theo Luật Bảo vệ bí mật nhà nước 2025, bí mật nhà nước có 3 độ mật: Tuyệt mật, Tối mật, Mật. Chỉ thay đổi danh mục này khi pháp luật thay đổi. " +
                           "Thuộc tính \"Cấm ghi trích yếu\" (mặc định bật cho TUYỆT MẬT theo Điều 6 Nghị định 63/2026/NĐ-CP) được áp dụng ở mọi màn hình, bản in và tệp xuất.",
                },
            };
            SetDock(note, Dock.Top);
            Children.Add(note);
            var bar = Bar(("+ Thêm", (_, _) => EditItem(null)), ("Sửa", (_, _) => EditItem((_grid.SelectedItem as Row)?.D)), ("Ngừng / Bật lại", (_, _) => Toggle()));
            SetDock(bar, Dock.Bottom);
            Children.Add(bar);
            _grid.Columns.Add(Col("Tên độ mật", nameof(Row.Ten), 2, true));
            _grid.Columns.Add(Col("Ký hiệu", nameof(Row.KyHieu), 90));
            _grid.Columns.Add(Col("Mức (cao → thấp)", nameof(Row.Muc), 130));
            _grid.Columns.Add(Col("Cấm ghi trích yếu", nameof(Row.CamTrichYeu), 150));
            _grid.Columns.Add(Col("Trạng thái", nameof(Row.TrangThai), 130));
            Children.Add(_grid);
        }

        public void Refresh() =>
            _grid.ItemsSource = Ctx.DanhMuc.ListDoMat(true).Select(d => new Row(d, d.Ten, d.KyHieu, d.Muc, d.CamTrichYeu ? "Có" : "", d.DangDung ? "Đang dùng" : "Ngừng sử dụng")).ToList();

        private void EditItem(DoMat? d)
        {
            var owner = Window.GetWindow(this)!;
            var ten = InputDialog.AskText(owner, "Độ mật", "Tên độ mật (chữ in hoa):", d?.Ten ?? "", 1, 50);
            if (ten == null) return;
            var kh = InputDialog.AskText(owner, "Độ mật", "Ký hiệu (A, B, C…; tối đa 3 ký tự):", d?.KyHieu ?? "", 0, 3);
            if (kh == null) return;
            var mucS = InputDialog.AskText(owner, "Độ mật", "Mức (số lớn hơn = mật hơn):", (d?.Muc ?? 1).ToString(), 1, 2);
            if (mucS == null || !int.TryParse(mucS, out var muc)) return;
            var ch = ChoiceDialog.Ask(owner, "Quy tắc trích yếu", $"Độ mật \"{ten}\" có CẤM ghi trích yếu không?\n\n(Theo Điều 6 Nghị định 63/2026/NĐ-CP: tài liệu Tuyệt mật không ghi trích yếu.)",
                "Cấm ghi trích yếu", "Được ghi trích yếu");
            if (ch < 0) return;
            var cam = ch == 0;
            if (d is { CamTrichYeu: true } && !cam &&
                !Dlg.Confirm("Bạn đang BỎ quy tắc cấm ghi trích yếu cho độ mật này. Việc này có thể trái Điều 6 Nghị định 63/2026/NĐ-CP. Tiếp tục?", danger: true))
                return;
            if (Dlg.Try(() => Ctx.DanhMuc.LuuDoMat(new DoMat(d?.Id ?? 0, ten, kh, muc, cam, d?.DangDung ?? true)), "lưu độ mật")) Refresh();
        }

        private void Toggle()
        {
            if ((_grid.SelectedItem as Row)?.D is not { } d) return;
            if (Dlg.Try(() => Ctx.DanhMuc.LuuDoMat(d with { DangDung = !d.DangDung }), "đổi trạng thái độ mật")) Refresh();
        }
    }

    // ================================================================ sổ đăng ký
    private sealed class SoDangKyTab : DockPanel, IRefresh
    {
        private readonly DataGrid _grid = new();

        private sealed record Row(SoDangKy S, string Loai, int Nam, int Quyen, string Khoang, string Ngay, int SoVb, string TrangThai, string BieuMau);

        public SoDangKyTab()
        {
            Margin = new Thickness(8);
            var note = new TextBlock
            {
                Style = (Style)FindResource("Hint"), Margin = new Thickness(0, 0, 0, 6),
                Text = "Mỗi năm phần mềm tự mở quyển 1 khi đăng ký văn bản đầu tiên. Khi hết quyển (sổ giấy) hoặc kết thúc năm: khóa quyển, rồi mở quyển mới; số thứ tự tiếp nối trong năm. " +
                       "Sổ đã khóa không thêm, sửa, hủy văn bản được.",
            };
            SetDock(note, Dock.Top);
            Children.Add(note);
            var bar = Bar(("Mở quyển mới…", (_, _) => Open()), ("Khóa sổ", (_, _) => Lock()), ("Mở khóa sổ…", (_, _) => Unlock()));
            SetDock(bar, Dock.Bottom);
            Children.Add(bar);
            _grid.Columns.Add(Col("Sổ", nameof(Row.Loai), 70));
            _grid.Columns.Add(Col("Năm", nameof(Row.Nam), 70));
            _grid.Columns.Add(Col("Quyển", nameof(Row.Quyen), 70));
            _grid.Columns.Add(Col("Từ số – đến số", nameof(Row.Khoang), 130));
            _grid.Columns.Add(Col("Từ ngày – đến ngày", nameof(Row.Ngay), 190));
            _grid.Columns.Add(Col("Số văn bản", nameof(Row.SoVb), 100));
            _grid.Columns.Add(Col("Trạng thái", nameof(Row.TrangThai), 1, true));
            _grid.Columns.Add(Col("Biểu mẫu", nameof(Row.BieuMau), 1, true));
            Children.Add(_grid);
        }

        public void Refresh() =>
            _grid.ItemsSource = Ctx.So.List().Select(s => new Row(s, s.Loai == LoaiSo.Di ? "Đi" : "Đến", s.Nam, s.QuyenSo,
                s.SoDau is { } a ? $"{TextUtil.So2(a)} – {TextUtil.So2(s.SoCuoi ?? a)}" : "",
                s.NgayDau is { } d ? $"{TextUtil.FormatDate(d)} – {TextUtil.FormatDate(s.NgayCuoi)}" : "",
                s.SoBanGhi, s.DaKhoa ? $"Đã khóa {s.NgayKhoa:dd/MM/yyyy}" : "Đang mở", s.MaBieuMau)).ToList();

        private SoDangKy? Sel => (_grid.SelectedItem as Row)?.S;

        private void Open()
        {
            var owner = Window.GetWindow(this)!;
            var c = ChoiceDialog.Ask(owner, "Mở quyển sổ", "Mở quyển mới cho sổ nào?", "Sổ văn bản đi", "Sổ văn bản đến");
            if (c < 0) return;
            var di = c == 0;
            var loai = di ? LoaiSo.Di : LoaiSo.Den;
            var namS = InputDialog.AskText(owner, "Mở quyển sổ", $"Năm của quyển sổ {(di ? "đi" : "đến")}:", Ctx.Clock.Today.Year.ToString(), 4, 4);
            if (namS == null || !int.TryParse(namS, out var nam)) return;
            var cur = Ctx.Store.CurrentSoDangKy(loai, nam);
            var soBatDau = 1;
            if (cur == null)
            {
                var s = InputDialog.AskText(owner, "Số bắt đầu",
                    "Đây là quyển đầu tiên của năm. Nếu chuyển từ sổ giấy đang dùng dở, nhập số thứ tự TIẾP THEO (ví dụ đã ghi tay đến số 57 thì nhập 58). Nếu không, để 1:",
                    "1", 1, 7);
                if (s == null || !int.TryParse(s, out soBatDau)) return;
            }
            if (Dlg.Try(() =>
                {
                    var so = Ctx.So.MoSo(loai, nam, soBatDau);
                    Dlg.Info($"Đã mở {so.TenHienThi}.");
                }, "mở sổ"))
            {
                Ctx.NotifyChanged();
                Refresh();
            }
        }

        private void Lock()
        {
            if (Sel is not { } s) { Dlg.Info("Hãy chọn quyển sổ."); return; }
            if (s.DaKhoa) return;
            if (!Dlg.Confirm($"Khóa {s.TenHienThi}?\n\nSau khi khóa không thêm/sửa/hủy được văn bản trong quyển này. Đăng ký tiếp cần mở quyển mới.")) return;
            if (Dlg.Try(() => Ctx.So.Khoa(s), "khóa sổ")) { Ctx.NotifyChanged(); Refresh(); }
        }

        private void Unlock()
        {
            if (Sel is not { } s) { Dlg.Info("Hãy chọn quyển sổ."); return; }
            if (!s.DaKhoa) return;
            var owner = Window.GetWindow(this)!;
            var pw = InputDialog.AskPassword(owner, "Xác nhận", "Mở khóa sổ là thao tác quan trọng. Nhập mật khẩu để xác nhận:");
            if (pw == null) return;
            if (!Ctx.Session.Keys.Verify(pw)) { Dlg.Warn("Mật khẩu không đúng."); return; }
            var lyDo = InputDialog.AskText(owner, "Lý do mở khóa", "Lý do mở khóa sổ (ghi vào nhật ký):", "", 3, 500, multiline: true);
            if (lyDo == null) return;
            if (Dlg.Try(() => Ctx.So.MoKhoa(s, lyDo), "mở khóa sổ")) { Ctx.NotifyChanged(); Refresh(); }
        }
    }
}
