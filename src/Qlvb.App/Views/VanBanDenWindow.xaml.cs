using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Qlvb.App.Infrastructure;
using Qlvb.Application;
using Qlvb.Domain;

namespace Qlvb.App.Views;

public partial class VanBanDenWindow : Window
{
    private VanBanDen _v;
    private readonly bool _isNew;
    private readonly Dictionary<string, Control> _fields;
    private int? _lastDoMatId;
    private bool _loading;
    public bool Saved { get; private set; }

    public VanBanDenWindow(VanBanDen? v = null, VanBanDen? copyFrom = null)
    {
        InitializeComponent();
        _isNew = v == null;
        _v = v ?? Ctx.VanBan.TaoMoiDen();
        _fields = new()
        {
            [nameof(VanBanDen.NgayDen)] = DpNgayDen, [nameof(VanBanDen.SoDen)] = TxtSoDen, [nameof(VanBanDen.CoQuanBanHanh)] = CbCoQuan,
            [nameof(VanBanDen.SoKyHieu)] = TxtSoKyHieu, [nameof(VanBanDen.NgayVanBan)] = DpNgayVanBan, [nameof(VanBanDen.TenLoai)] = CbTenLoai,
            [nameof(VanBanDen.DoMatId)] = CbDoMat, [nameof(VanBanDen.TrichYeu)] = TxtTrichYeu, [nameof(VanBanDen.DonViNhan)] = CbDonViNhan,
            [nameof(VanBanDen.NguoiKyNhan)] = TxtNguoiKyNhan, [nameof(VanBanDen.NgayKyNhan)] = DpNgayKyNhan, [nameof(VanBanDen.GhiChu)] = TxtGhiChu,
            [nameof(VanBanDen.SoThuTu)] = TxtSoDen,
        };
        LoadLists();
        Bind(_v);
        if (!_isNew)
        {
            Title = "Sửa văn bản đến";
            TxtHeader.Text = $"Sửa văn bản đến số {TextUtil.So2(_v.SoThuTu)}/{_v.Nam}";
            TxtSoDen.IsReadOnly = true;
            TxtSoDen.Background = TxtSoThuTu.Background;
            BtnSaveNew.Visibility = Visibility.Collapsed;
            BtnCopyPrev.Visibility = Visibility.Collapsed;
            BtnSave.Content = "Lưu thay đổi (Ctrl+S)";
            TxtMeta.Text = $"Tạo lúc {_v.TaoLuc:dd/MM/yyyy HH:mm} bởi {_v.TaoBoi}; sửa lần cuối {_v.CapNhatLuc:dd/MM/yyyy HH:mm} bởi {_v.CapNhatBoi}; phiên bản {_v.PhienBan}.";
        }
        else
        {
            if (FormKit.Remember.TryGetValue("den.domat", out var dm) && int.TryParse(dm, out var dmId)) FormKit.SelectDoMat(CbDoMat, dmId);
            if (FormKit.Remember.TryGetValue("den.donvi", out var dv)) CbDonViNhan.Text = dv;
        }
        if (_isNew && copyFrom != null) CopyFrom(copyFrom);
        _banDau = DauVet();
        Loaded += (_, _) => (_isNew ? (Control)CbCoQuan : TxtSoKyHieu).Focus();
    }

    private void LoadLists()
    {
        CbDoMat.ItemsSource = Ctx.DanhMuc.ListDoMat(includeInactive: !_isNew).Where(d => d.DangDung || d.Id == _v.DoMatId).ToList();
        FormKit.FillSuggestions(CbTenLoai, NhomDanhMuc.LoaiVanBan);
        FormKit.FillSuggestions(CbCoQuan, NhomDanhMuc.CoQuanBanHanh);
        FormKit.FillSuggestions(CbDonViNhan, NhomDanhMuc.DonVi);
    }

    private void Bind(VanBanDen v)
    {
        _loading = true;
        DpNgayDen.SelectedDate = FormKit.ToDateTime(v.NgayDen);
        UpdateNumbers(v);
        CbCoQuan.Text = v.CoQuanBanHanh;
        TxtSoKyHieu.Text = v.SoKyHieu;
        DpNgayVanBan.SelectedDate = FormKit.ToDateTime(v.NgayVanBan);
        CbTenLoai.Text = v.TenLoai;
        if (v.DoMatId != 0) FormKit.SelectDoMat(CbDoMat, v.DoMatId);
        _lastDoMatId = (CbDoMat.SelectedItem as DoMat)?.Id;
        TxtTrichYeu.Text = v.TrichYeu ?? "";
        CbDonViNhan.Text = v.DonViNhan;
        ChkDaKyNhan.IsChecked = v.DaKyNhan;
        TxtNguoiKyNhan.Text = v.NguoiKyNhan ?? "";
        DpNgayKyNhan.SelectedDate = FormKit.ToDateTime(v.NgayKyNhan);
        TxtGhiChu.Text = v.GhiChu ?? "";
        ApplyDoMatRule(false);
        _loading = false;
    }

    private void UpdateNumbers(VanBanDen v)
    {
        if (_isNew)
        {
            var nam = FormKit.ToDate(DpNgayDen)?.Year ?? Ctx.Clock.Today.Year;
            TxtSoThuTu.Text = $"{TextUtil.So2(Ctx.VanBan.SoDuKien(LoaiSo.Den, nam, BoDem.SoThuTu))} (dự kiến, cấp khi lưu)";
            TxtSoDen.Text = Ctx.VanBan.SoDuKien(LoaiSo.Den, nam, BoDem.SoDen).ToString();
            TxtSubHeader.Text = $"Sổ đăng ký bí mật nhà nước đến năm {nam}. Số thứ tự cấp tự động; số đến gợi ý tiếp theo, có thể sửa nếu đơn vị đánh số đến riêng.";
        }
        else
        {
            TxtSoThuTu.Text = TextUtil.So2(v.SoThuTu);
            TxtSoDen.Text = v.SoDen.ToString();
            TxtSubHeader.Text = $"Sổ năm {v.Nam}. Số thứ tự, số đến và năm không thay đổi khi sửa.";
        }
    }

    private void DpNgayDen_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (!_loading && _isNew) Dlg.Try(() => UpdateNumbers(_v));
    }

    private void CbDoMat_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        var dm = CbDoMat.SelectedItem as DoMat;
        if (dm is { CamTrichYeu: true } && TxtTrichYeu.Text.Trim().Length > 0
            && !Dlg.Confirm($"Tài liệu độ mật {dm.Ten} không được ghi trích yếu (Điều 6 Nghị định 63/2026/NĐ-CP).\n\nNội dung trích yếu đã nhập sẽ bị XÓA. Tiếp tục?", danger: true))
        {
            _loading = true;
            if (_lastDoMatId is { } old) FormKit.SelectDoMat(CbDoMat, old); else CbDoMat.SelectedItem = null;
            _loading = false;
            return;
        }
        _lastDoMatId = dm?.Id;
        ApplyDoMatRule(true);
    }

    private void ApplyDoMatRule(bool userAction)
    {
        var cam = CbDoMat.SelectedItem is DoMat { CamTrichYeu: true };
        TxtTrichYeu.IsEnabled = !cam;
        TmNotice.Visibility = cam ? Visibility.Visible : Visibility.Collapsed;
        if (cam)
        {
            TxtTrichYeu.Text = "";
            if (userAction) Dlg.Info("Đã chọn độ mật TUYỆT MẬT: phần mềm khóa ô trích yếu. Cột (7) của sổ chỉ ghi tên loại tài liệu.");
        }
    }

    private void QuickAddLoai_Click(object sender, RoutedEventArgs e) => FormKit.QuickAdd(this, CbTenLoai, NhomDanhMuc.LoaiVanBan);
    private void Digits_PreviewTextInput(object sender, TextCompositionEventArgs e) => e.Handled = !e.Text.All(char.IsDigit);

    private void BtnCopyPrev_Click(object sender, RoutedEventArgs e) => Dlg.Try(() =>
    {
        var prev = Ctx.Store.Search(new SearchCriteria { Loai = LoaiSo.Den, Limit = 1, SapXep = "so_thu_tu", Giam = true, TrangThai = LocTrangThai.HieuLuc })
            .Items.OfType<VanBanDen>().FirstOrDefault();
        if (prev == null) { Dlg.Info("Chưa có văn bản đến nào để sao chép."); return; }
        CopyFrom(prev);
    });

    public void CopyFrom(VanBanDen prev)
    {
        var copy = Ctx.VanBan.SaoChepDen(prev);
        copy.NgayDen = FormKit.ToDate(DpNgayDen) ?? copy.NgayDen;
        Bind(copy);
        _v = copy;
        CopyBanner.Visibility = Visibility.Visible;
        TxtCopyBanner.Text = $"Đã sao chép cơ quan ban hành, tên loại, độ mật, đơn vị nhận từ văn bản đến số {TextUtil.So2(prev.SoThuTu)}/{prev.Nam}. " +
                             "Không sao chép số, ký hiệu, ngày, trích yếu, ký nhận. Hãy kiểm tra lại trước khi lưu.";
        TxtSoKyHieu.Focus();
    }

    private VanBanDen Collect()
    {
        var v = _isNew ? new VanBanDen() : Ctx.Store.GetDen(_v.Id) ?? throw new BusinessException("Văn bản không còn tồn tại.");
        if (!_isNew) v.PhienBan = _v.PhienBan;
        v.NgayDen = FormKit.ToDate(DpNgayDen) ?? default;
        v.NgayDangKy = v.NgayDen;
        v.Nam = _isNew ? v.NgayDen.Year : _v.Nam;
        if (_isNew)
        {
            v.SoThuTu = Ctx.VanBan.SoDuKien(LoaiSo.Den, v.Nam, BoDem.SoThuTu);
            v.SoDen = FormKit.TryInt(TxtSoDen, out var sd) ? sd : 0;
        }
        v.CoQuanBanHanh = CbCoQuan.Text;
        v.SoKyHieu = TxtSoKyHieu.Text;
        v.NgayVanBan = FormKit.ToDate(DpNgayVanBan) ?? default;
        v.TenLoai = CbTenLoai.Text;
        v.DoMatId = (CbDoMat.SelectedItem as DoMat)?.Id ?? 0;
        v.TrichYeu = TxtTrichYeu.IsEnabled ? TxtTrichYeu.Text : null;
        v.DonViNhan = CbDonViNhan.Text;
        v.NgayKyNhan = FormKit.ToDate(DpNgayKyNhan);
        v.DaKyNhan = ChkDaKyNhan.IsChecked == true || v.NgayKyNhan != null;
        v.NguoiKyNhan = TxtNguoiKyNhan.Text;
        v.GhiChu = TxtGhiChu.Text;
        return v;
    }

    private bool Save()
    {
        try
        {
            var v = Collect();
            var kt = Ctx.VanBan.KiemTra(v);
            if (!kt.KetQua.IsValid)
            {
                FormKit.ShowErrors(kt.KetQua, _fields);
                return false;
            }
            if (!FormKit.ConfirmWarnings(kt)) return false;
            if (_isNew)
            {
                Ctx.VanBan.ThemMoi(v);
                FormKit.Remember["den.domat"] = v.DoMatId.ToString();
                FormKit.Remember["den.donvi"] = v.DonViNhan;
                Dlg.Info($"Đã đăng ký văn bản đến số {TextUtil.So2(v.SoThuTu)}/{v.Nam}, số đến {v.SoDen}.");
            }
            else
            {
                var diff = Ctx.VanBan.SoSanh(v);
                if (diff.Count == 0)
                {
                    Dlg.Info("Không có thay đổi nào để lưu.");
                    return false;
                }
                if (!ConfirmChangesWindow.Ask(this, diff)) return false;
                Ctx.VanBan.CapNhat(v);
                _v = v;
            }
            Saved = true;
            Ctx.NotifyChanged();
            return true;
        }
        catch (ValidationException vex)
        {
            FormKit.ShowErrors(vex.Result, _fields);
            return false;
        }
        catch (Exception ex)
        {
            Dlg.Handle(ex, "lưu văn bản đến");
            return false;
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (Save()) DialogResult = true;
    }

    private void BtnSaveNew_Click(object sender, RoutedEventArgs e)
    {
        if (!Save()) return;
        _v = Ctx.VanBan.TaoMoiDen();
        LoadLists();
        Bind(_v);
        if (FormKit.Remember.TryGetValue("den.domat", out var dm) && int.TryParse(dm, out var id)) FormKit.SelectDoMat(CbDoMat, id);
        if (FormKit.Remember.TryGetValue("den.donvi", out var dv)) CbDonViNhan.Text = dv;
        CopyBanner.Visibility = Visibility.Collapsed;
        CbCoQuan.Focus();
    }

    private void CmdSave(object sender, ExecutedRoutedEventArgs e) => BtnSave_Click(sender, e);
    private void CmdClose(object sender, ExecutedRoutedEventArgs e) => Close();
    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        if (DialogResult == true || App.Current.LoggingOut || !Ctx.Session.LoggedIn) return;
        if (IsDirty() && !Dlg.Confirm("Thông tin đã nhập chưa được lưu. Đóng và bỏ qua?", danger: true)) e.Cancel = true;
    }

    private string _banDau = "";

    /// <summary>Dấu vết nội dung đang nhập, so với lúc mở form để biết người dùng đã nhập gì chưa.</summary>
    private string DauVet() => string.Join("\u001f", TxtSoKyHieu.Text, CbTenLoai.Text, (CbDoMat.SelectedItem as DoMat)?.Id, TxtTrichYeu.Text, CbCoQuan.Text, CbDonViNhan.Text, TxtSoDen.Text, TxtGhiChu.Text, TxtNguoiKyNhan.Text, ChkDaKyNhan.IsChecked, DpNgayDen.SelectedDate, DpNgayVanBan.SelectedDate, DpNgayKyNhan.SelectedDate);

    private bool IsDirty()
    {
        if (_isNew) return DauVet() != _banDau;
        try { return Ctx.VanBan.SoSanh(Collect()).Count > 0; }
        catch (Exception) { return true; }
    }
}
