using System.Windows;
using System.Windows.Controls;
using Qlvb.App.Infrastructure;
using Qlvb.App.Views;
using Qlvb.Application;
using Qlvb.Domain;
using Qlvb.Infrastructure.Data;

namespace Qlvb.App.Pages;

public sealed class CauHinhPage : UserControl, IPage
{
    private readonly TextBox _tenCq = new() { MaxLength = 300 };
    private readonly TextBox _chuQuan = new() { MaxLength = 300 };
    private readonly TextBox _kyHieu = new() { MaxLength = 30, Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBox _mau = new() { MaxLength = 100 };
    private readonly TextBox _mauCv = new() { MaxLength = 100 };
    private readonly TextBlock _preview = new() { Margin = new Thickness(0, 4, 0, 0) };
    private readonly TextBox _tuKhoa = new() { Width = 80, HorizontalAlignment = HorizontalAlignment.Left, MaxLength = 3 };
    private readonly TextBox _giuLai = new() { Width = 80, HorizontalAlignment = HorizontalAlignment.Left, MaxLength = 3 };
    private readonly CheckBox _exitBackup = new() { Content = "Tự động sao lưu khi thoát phần mềm" };
    private readonly CheckBox _kyHieuDoMat = new() { Content = "Ghi độ mật bằng ký hiệu A, B, C trên danh sách và bản in" };
    private readonly CheckBox _chanChup = new() { Content = "Chặn chụp màn hình, quay màn hình và chia sẻ màn hình cửa sổ phần mềm" };
    private readonly CheckBox _xuatMat = new() { Content = "Cho phép xuất danh sách văn bản mật ra Excel/CSV (không khuyến nghị)" };
    private readonly TextBlock _info = new() { TextWrapping = TextWrapping.Wrap };
    private bool _loading;

    public string Title => "Cấu hình";

    public CauHinhPage()
    {
        var root = new StackPanel { MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left };
        root.Children.Add(new TextBlock { Text = "Cấu hình", Style = (Style)FindResource("H1") });

        var org = Section(root, "Thông tin cơ quan và số, ký hiệu");
        Field(org, "Tên cơ quan, tổ chức (2)", _tenCq);
        Field(org, "Cơ quan chủ quản cấp trên trực tiếp (1) – nếu có", _chuQuan);
        Field(org, "Ký hiệu (chữ viết tắt) của cơ quan", _kyHieu);
        Field(org, "Mẫu số, ký hiệu văn bản có tên loại", _mau);
        Field(org, "Mẫu số, ký hiệu công văn", _mauCv);
        org.Children.Add(new TextBlock
        {
            Style = (Style)FindResource("Hint"),
            Text = "Dùng các ô: {so} số thứ tự, {viet_tat} chữ viết tắt tên loại, {ky_hieu_co_quan} ký hiệu cơ quan, {nam} năm. Ví dụ \"{so}/{viet_tat}-{ky_hieu_co_quan}\" → \"15/BC-ABC\". Đây chỉ là gợi ý, luôn sửa được trên form.",
        });
        org.Children.Add(_preview);
        foreach (var t in new[] { _kyHieu, _mau, _mauCv }) t.TextChanged += (_, _) => Preview();
        var save = new Button { Content = "Lưu thông tin", Style = (Style)FindResource("PrimaryButton"), HorizontalAlignment = HorizontalAlignment.Left };
        save.Click += (_, _) => SaveOrg();
        org.Children.Add(save);

        var sec = Section(root, "Bảo mật");
        Field(sec, "Tự khóa màn hình sau (phút, 0 = không tự khóa)", _tuKhoa);
        var pw = new WrapPanel();
        var b1 = new Button { Content = "Đổi mật khẩu…" };
        b1.Click += (_, _) => ChangePassword();
        var b2 = new Button { Content = "Tạo mã khôi phục mới…" };
        b2.Click += (_, _) => NewRecovery();
        var b3 = new Button { Content = "Lưu thời gian tự khóa" };
        b3.Click += (_, _) => SaveInt(ConfigKeys.TuKhoaPhut, _tuKhoa, 0, 240);
        pw.Children.Add(b3);
        pw.Children.Add(b1);
        pw.Children.Add(b2);
        sec.Children.Add(pw);
        sec.Children.Add(_chanChup);
        sec.Children.Add(_xuatMat);
        _chanChup.Click += (_, _) => ToggleCapture();
        _xuatMat.Click += (_, _) => ToggleExport();

        var prn = Section(root, "Hiển thị và in");
        prn.Children.Add(_kyHieuDoMat);
        _kyHieuDoMat.Click += (_, _) => { if (!_loading) Dlg.Try(() => Ctx.CauHinh.SetBool(ConfigKeys.InKyHieuDoMat, _kyHieuDoMat.IsChecked == true)); Ctx.NotifyChanged(); };

        var bk = Section(root, "Sao lưu tự động");
        bk.Children.Add(_exitBackup);
        _exitBackup.Click += (_, _) => { if (!_loading) Dlg.Try(() => Ctx.CauHinh.SetBool(ConfigKeys.TuSaoLuuKhiThoat, _exitBackup.IsChecked == true)); };
        Field(bk, "Số bản sao lưu nhanh/tự động giữ lại trong thư mục mặc định", _giuLai);
        var b4 = new Button { Content = "Lưu", HorizontalAlignment = HorizontalAlignment.Left };
        b4.Click += (_, _) => SaveInt(ConfigKeys.SoBanSaoLuuGiuLai, _giuLai, 1, 999);
        bk.Children.Add(b4);

        var about = Section(root, "Thông tin hệ thống");
        about.Children.Add(_info);

        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private StackPanel Section(StackPanel root, string title)
    {
        var sp = new StackPanel();
        root.Children.Add(new Border { Style = (Style)FindResource("Card"), Child = sp });
        sp.Children.Add(new TextBlock { Text = title, Style = (Style)FindResource("H2"), Margin = new Thickness(0, 0, 0, 6) });
        return sp;
    }

    private void Field(StackPanel sp, string label, Control c)
    {
        sp.Children.Add(new TextBlock { Text = label, Style = (Style)FindResource("Label") });
        sp.Children.Add(c);
    }

    public void OnShow() => Dlg.Try(() =>
    {
        _loading = true;
        var ch = Ctx.CauHinh;
        _tenCq.Text = ch.Get(ConfigKeys.TenCoQuan);
        _chuQuan.Text = ch.Get(ConfigKeys.CoQuanChuQuan);
        _kyHieu.Text = ch.Get(ConfigKeys.KyHieuCoQuan);
        _mau.Text = ch.Get(ConfigKeys.MauSoKyHieu, ConfigKeys.MacDinhMauSoKyHieu);
        _mauCv.Text = ch.Get(ConfigKeys.MauSoKyHieuCongVan, ConfigKeys.MacDinhMauCongVan);
        _tuKhoa.Text = ch.GetInt(ConfigKeys.TuKhoaPhut, ConfigKeys.MacDinhTuKhoaPhut).ToString();
        _giuLai.Text = ch.GetInt(ConfigKeys.SoBanSaoLuuGiuLai, ConfigKeys.MacDinhSoBanSaoLuu).ToString();
        _exitBackup.IsChecked = ch.GetBool(ConfigKeys.TuSaoLuuKhiThoat, true);
        _kyHieuDoMat.IsChecked = ch.GetBool(ConfigKeys.InKyHieuDoMat);
        _xuatMat.IsChecked = ch.ChoPhepXuatMat;
        _chanChup.IsChecked = ch.ChanChupManHinh;
        Preview();
        var forms = string.Join("\n", Ctx.Store.ListBieuMau().Select(b => $"  • {b.Ma} (hiệu lực từ {TextUtil.FormatDate(b.HieuLucTu)}): {b.TieuDe} – {b.CanCu}"));
        _info.Text = $"Phiên bản phần mềm: {typeof(App).Assembly.GetName().Version}\n" +
                     $"Chế độ: {(Ctx.Paths.Portable ? "portable (dữ liệu cạnh tệp chạy)" : "cài đặt")}\n" +
                     $"Thư mục dữ liệu: {Ctx.Paths.Root}\n" +
                     $"Phiên bản lược đồ dữ liệu: {Migrator.CurrentVersion(Ctx.Session.Db!)}\n" +
                     $"Mã hóa dữ liệu: SQLite3 Multiple Ciphers (SQLCipher 4, AES-256); khóa bọc bằng PBKDF2-HMAC-SHA256 + AES-256-GCM\n" +
                     $"Biểu mẫu sổ:\n{forms}";
        _loading = false;
    }, "cấu hình");

    private void Preview()
    {
        try
        {
            var a = SoKyHieu.GoiY(_mau.Text, _mauCv.Text, 15, "BC", _kyHieu.Text, Ctx.Clock.Today.Year);
            var b = SoKyHieu.GoiY(_mau.Text, _mauCv.Text, 16, null, _kyHieu.Text, Ctx.Clock.Today.Year);
            _preview.Text = $"Ví dụ: Báo cáo số 15 → {a};  Công văn số 16 → {b}";
        }
        catch (Exception) { _preview.Text = ""; }
    }

    private void SaveOrg()
    {
        if (Dlg.Try(() =>
            {
                var ch = Ctx.CauHinh;
                ch.Set(ConfigKeys.TenCoQuan, _tenCq.Text);
                ch.Set(ConfigKeys.CoQuanChuQuan, _chuQuan.Text);
                ch.Set(ConfigKeys.KyHieuCoQuan, _kyHieu.Text);
                ch.Set(ConfigKeys.MauSoKyHieu, _mau.Text);
                ch.Set(ConfigKeys.MauSoKyHieuCongVan, _mauCv.Text);
            }, "lưu cấu hình"))
        {
            Ctx.NotifyChanged();
            Dlg.Info("Đã lưu. Tên cơ quan mới áp dụng cho quyển sổ mở sau; quyển đang mở giữ thông tin trang bìa lúc mở.");
        }
    }

    private void SaveInt(string key, TextBox tb, int min, int max)
    {
        if (!int.TryParse(tb.Text.Trim(), out var v) || v < min || v > max)
        {
            Dlg.Warn($"Vui lòng nhập số từ {min} đến {max}.");
            FormKit.FocusField(tb);
            return;
        }
        if (Dlg.Try(() => Ctx.CauHinh.SetInt(key, v))) Dlg.Info("Đã lưu.");
    }

    private bool ConfirmPassword(string why)
    {
        var pw = InputDialog.AskPassword(Window.GetWindow(this), "Xác nhận mật khẩu", why + "\nNhập mật khẩu hiện tại:");
        if (pw == null) return false;
        if (Ctx.Session.Keys.Verify(pw)) return true;
        Dlg.Warn("Mật khẩu không đúng.");
        return false;
    }

    private void ToggleCapture()
    {
        var on = _chanChup.IsChecked == true;
        if (!on && !ConfirmPassword("Tắt chặn chụp màn hình."))
        {
            _chanChup.IsChecked = true;
            return;
        }
        if (Dlg.Try(() => Ctx.CauHinh.SetBool(ConfigKeys.ChanChupManHinh, on))) ScreenCaptureGuard.Set(on);
        else _chanChup.IsChecked = !on;
    }

    private void ToggleExport()
    {
        if (_loading) return;
        var on = _xuatMat.IsChecked == true;
        if (on && (!Dlg.Confirm("Bật cho phép xuất danh sách văn bản mật ra tệp Excel/CSV?\n\n" +
                                "• Tệp xuất ra KHÔNG được mã hóa và rất dễ bị sao chép, phát tán.\n" +
                                "• Chỉ bật khi có yêu cầu của người có thẩm quyền; tắt lại ngay sau khi dùng.\n" +
                                "• Mọi lần xuất đều ghi nhật ký.", danger: true) || !ConfirmPassword("Thay đổi quy tắc bảo mật.")))
        {
            _xuatMat.IsChecked = false;
            return;
        }
        Dlg.Try(() => Ctx.CauHinh.SetBool(ConfigKeys.ChoPhepXuatMat, on));
    }

    private void ChangePassword()
    {
        var w = Window.GetWindow(this);
        var old = InputDialog.AskPassword(w, "Đổi mật khẩu", "Mật khẩu hiện tại:");
        if (old == null) return;
        var n1 = InputDialog.AskPassword(w, "Đổi mật khẩu", "Mật khẩu mới (ít nhất 8 ký tự, có chữ và số/ký tự đặc biệt):");
        if (n1 == null) return;
        var n2 = InputDialog.AskPassword(w, "Đổi mật khẩu", "Nhập lại mật khẩu mới:");
        if (n2 == null) return;
        if (n1 != n2) { Dlg.Warn("Hai lần nhập mật khẩu mới không khớp."); return; }
        if (Dlg.Try(() => { using (new WaitCursor()) Ctx.Session.ChangePassword(old, n1); }, "đổi mật khẩu"))
            Dlg.Info("Đã đổi mật khẩu.\n\nLưu ý: các bản sao lưu cũ vẫn mở bằng mật khẩu CŨ. Nên sao lưu lại ngay để có bản sao lưu dùng mật khẩu mới.");
    }

    private void NewRecovery()
    {
        var w = Window.GetWindow(this)!;
        var pw = InputDialog.AskPassword(w, "Mã khôi phục mới", "Mã khôi phục cũ sẽ hết hiệu lực. Nhập mật khẩu hiện tại:");
        if (pw == null) return;
        try
        {
            string code;
            using (new WaitCursor()) code = Ctx.Session.RegenerateRecoveryCode(pw);
            new RecoveryCodeWindow(code) { Owner = w }.ShowDialog();
        }
        catch (Exception ex) { Dlg.Handle(ex, "tạo mã khôi phục"); }
    }
}
