using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Qlvb.App.Infrastructure;
using Qlvb.Application;
using Qlvb.Domain;

namespace Qlvb.App.Views;

public sealed class NoiNhanRow
{
    public int ThuTu { get; set; }
    public string NoiNhan { get; set; } = "";
    public bool DaKy { get; set; }
    public string? NguoiKyNhan { get; set; }
    public DateTime? NgayKyNhan { get; set; }
}

public partial class VanBanDiWindow : Window
{
    private VanBanDi _v;
    private readonly bool _isNew;
    private readonly ObservableCollection<NoiNhanRow> _noiNhan = [];
    private readonly Dictionary<string, Control> _fields;
    private int? _lastDoMatId;
    private bool _loading;
    public bool Saved { get; private set; }

    /// <param name="v">null: tạo mới.</param>
    public VanBanDiWindow(VanBanDi? v = null, VanBanDi? copyFrom = null)
    {
        InitializeComponent();
        _isNew = v == null;
        _v = v ?? Ctx.VanBan.TaoMoiDi();
        _fields = new()
        {
            [nameof(VanBanDi.SoKyHieu)] = TxtSoKyHieu, [nameof(VanBanDi.NgayVanBan)] = DpNgayVanBan, [nameof(VanBanDi.NgayDangKy)] = DpNgayDangKy,
            [nameof(VanBanDi.TenLoai)] = CbTenLoai, [nameof(VanBanDi.TrichYeu)] = TxtTrichYeu, [nameof(VanBanDi.DoMatId)] = CbDoMat,
            [nameof(VanBanDi.NguoiKy)] = CbNguoiKy, [nameof(VanBanDi.DonViLuu)] = CbDonViLuu, [nameof(VanBanDi.SoLuong)] = TxtSoLuong,
            [nameof(VanBanDi.NoiNhan)] = CbNoiNhan, [nameof(VanBanDi.GhiChu)] = TxtGhiChu,
        };
        GridNoiNhan.ItemsSource = _noiNhan;
        LoadLists();
        Bind(_v);
        if (!_isNew)
        {
            Title = "Sửa văn bản đi";
            TxtHeader.Text = $"Sửa văn bản đi số {TextUtil.So2(_v.SoThuTu)}/{_v.Nam}";
            BtnSaveNew.Visibility = Visibility.Collapsed;
            BtnCopyPrev.Visibility = Visibility.Collapsed;
            BtnSave.Content = "Lưu thay đổi (Ctrl+S)";
            TxtMeta.Text = $"Tạo lúc {_v.TaoLuc:dd/MM/yyyy HH:mm} bởi {_v.TaoBoi}; sửa lần cuối {_v.CapNhatLuc:dd/MM/yyyy HH:mm} bởi {_v.CapNhatBoi}; phiên bản {_v.PhienBan}.";
        }
        else
        {
            if (FormKit.Remember.TryGetValue("di.domat", out var dm) && int.TryParse(dm, out var dmId)) FormKit.SelectDoMat(CbDoMat, dmId);
            if (FormKit.Remember.TryGetValue("di.nguoiky", out var nk)) CbNguoiKy.Text = nk;
            if (FormKit.Remember.TryGetValue("di.donvi", out var dv)) CbDonViLuu.Text = dv;
        }
        if (_isNew && copyFrom != null) CopyFrom(copyFrom);
        _banDau = DauVet();
        Loaded += (_, _) => (_isNew ? (Control)CbTenLoai : TxtSoKyHieu).Focus();
    }

    private void LoadLists()
    {
        CbDoMat.ItemsSource = Ctx.DanhMuc.ListDoMat(includeInactive: !_isNew).Where(d => d.DangDung || d.Id == _v.DoMatId).ToList();
        FormKit.FillSuggestions(CbTenLoai, NhomDanhMuc.LoaiVanBan);
        FormKit.FillSuggestions(CbNguoiKy, NhomDanhMuc.NguoiKy);
        FormKit.FillSuggestions(CbDonViLuu, NhomDanhMuc.DonVi);
        FormKit.FillSuggestions(CbNoiNhan, NhomDanhMuc.NoiNhan);
    }

    private void Bind(VanBanDi v)
    {
        _loading = true;
        UpdateSoThuTu();
        DpNgayDangKy.SelectedDate = FormKit.ToDateTime(v.NgayDangKy);
        DpNgayVanBan.SelectedDate = FormKit.ToDateTime(v.NgayVanBan);
        CbTenLoai.Text = v.TenLoai;
        TxtSoKyHieu.Text = v.SoKyHieu;
        if (v.DoMatId != 0) FormKit.SelectDoMat(CbDoMat, v.DoMatId);
        _lastDoMatId = (CbDoMat.SelectedItem as DoMat)?.Id;
        TxtTrichYeu.Text = v.TrichYeu ?? "";
        CbNguoiKy.Text = v.NguoiKy;
        CbDonViLuu.Text = v.DonViLuu;
        TxtSoLuong.Text = v.SoLuong.ToString();
        TxtGhiChu.Text = v.GhiChu ?? "";
        _noiNhan.Clear();
        foreach (var n in v.NoiNhan.OrderBy(n => n.ThuTu))
            _noiNhan.Add(new NoiNhanRow { ThuTu = n.ThuTu, NoiNhan = n.NoiNhan, DaKy = n.DaKy, NguoiKyNhan = n.NguoiKyNhan, NgayKyNhan = FormKit.ToDateTime(n.NgayKyNhan) });
        ApplyDoMatRule(false);
        _loading = false;
    }

    private void UpdateSoThuTu()
    {
        if (_isNew)
        {
            var nam = FormKit.ToDate(DpNgayDangKy)?.Year ?? Ctx.Clock.Today.Year;
            var so = Ctx.VanBan.SoDuKien(LoaiSo.Di, nam, BoDem.SoThuTu);
            TxtSoThuTu.Text = $"{TextUtil.So2(so)} (dự kiến, cấp khi lưu)";
            TxtSubHeader.Text = $"Sổ đăng ký bí mật nhà nước đi năm {nam}. Số thứ tự do phần mềm cấp tự động, không trùng.";
        }
        else
        {
            TxtSoThuTu.Text = TextUtil.So2(_v.SoThuTu);
            TxtSubHeader.Text = $"Sổ năm {_v.Nam}. Số thứ tự và năm không thay đổi khi sửa.";
        }
    }

    private void DpNgayDangKy_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (!_loading && _isNew) Dlg.Try(UpdateSoThuTu);
    }

    // ------------------------------------------------------------- quy tắc độ mật
    private void CbDoMat_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        var dm = CbDoMat.SelectedItem as DoMat;
        if (dm is { CamTrichYeu: true } && TxtTrichYeu.Text.Trim().Length > 0)
        {
            if (!Dlg.Confirm($"Tài liệu độ mật {dm.Ten} không được ghi trích yếu (Điều 6 Nghị định 63/2026/NĐ-CP).\n\nNội dung trích yếu đã nhập sẽ bị XÓA. Tiếp tục?", danger: true))
            {
                _loading = true;
                if (_lastDoMatId is { } old) FormKit.SelectDoMat(CbDoMat, old); else CbDoMat.SelectedItem = null;
                _loading = false;
                return;
            }
        }
        _lastDoMatId = dm?.Id;
        ApplyDoMatRule(true);
    }

    private void ApplyDoMatRule(bool userAction)
    {
        var cam = CbDoMat.SelectedItem is DoMat { CamTrichYeu: true };
        if (cam)
        {
            TxtTrichYeu.Text = "";
            TxtTrichYeu.IsEnabled = false;
            TmNotice.Visibility = Visibility.Visible;
            if (userAction) Dlg.Info("Đã chọn độ mật TUYỆT MẬT: phần mềm khóa ô trích yếu. Cột (4) của sổ chỉ ghi tên loại tài liệu.");
        }
        else
        {
            TxtTrichYeu.IsEnabled = true;
            TmNotice.Visibility = Visibility.Collapsed;
        }
    }

    // ------------------------------------------------------------- nơi nhận
    private void AddNoiNhan()
    {
        var ten = TextUtil.Clean(CbNoiNhan.Text);
        if (ten == null) return;
        if (_noiNhan.Any(n => TextUtil.SearchKey(n.NoiNhan) == TextUtil.SearchKey(ten)))
        {
            Dlg.Warn($"\"{ten}\" đã có trong danh sách nơi nhận.");
            return;
        }
        _noiNhan.Add(new NoiNhanRow { ThuTu = _noiNhan.Count + 1, NoiNhan = ten });
        CbNoiNhan.Text = "";
        CbNoiNhan.Focus();
    }

    private void BtnAddNoiNhan_Click(object sender, RoutedEventArgs e) => AddNoiNhan();

    private void CbNoiNhan_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AddNoiNhan();
            e.Handled = true;
        }
    }

    private void BtnRemoveNoiNhan_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not NoiNhanRow r) return;
        GridNoiNhan.CommitEdit(DataGridEditingUnit.Row, true);
        _noiNhan.Remove(r);
        for (var i = 0; i < _noiNhan.Count; i++) _noiNhan[i].ThuTu = i + 1;
        GridNoiNhan.Items.Refresh();
    }

    // ------------------------------------------------------------- tiện ích
    private void QuickAddLoai_Click(object sender, RoutedEventArgs e) => FormKit.QuickAdd(this, CbTenLoai, NhomDanhMuc.LoaiVanBan);
    private void QuickAddNguoiKy_Click(object sender, RoutedEventArgs e) => FormKit.QuickAdd(this, CbNguoiKy, NhomDanhMuc.NguoiKy);
    private void QuickAddDonVi_Click(object sender, RoutedEventArgs e) => FormKit.QuickAdd(this, CbDonViLuu, NhomDanhMuc.DonVi);

    private void BtnGoiY_Click(object sender, RoutedEventArgs e) => Dlg.Try(() =>
    {
        var nam = _isNew ? FormKit.ToDate(DpNgayDangKy)?.Year ?? Ctx.Clock.Today.Year : _v.Nam;
        var so = _isNew ? Ctx.VanBan.SoDuKien(LoaiSo.Di, nam, BoDem.SoThuTu) : _v.SoThuTu;
        TxtSoKyHieu.Text = Ctx.VanBan.GoiYSoKyHieu(so, CbTenLoai.Text, nam);
        if (_isNew) TxtSoKyHieu.ToolTip = "Số trong ký hiệu là số dự kiến; nếu có người khác lưu trước, hãy kiểm tra lại.";
    });

    private void CbTenLoai_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isNew && TxtSoKyHieu.Text.Trim().Length == 0 && CbTenLoai.Text.Trim().Length > 0) BtnGoiY_Click(sender, e);
    }

    private void Digits_PreviewTextInput(object sender, TextCompositionEventArgs e) => e.Handled = !e.Text.All(char.IsDigit);

    private void BtnCopyPrev_Click(object sender, RoutedEventArgs e) => Dlg.Try(() =>
    {
        var prev = Ctx.Store.Search(new SearchCriteria { Loai = LoaiSo.Di, Limit = 1, SapXep = "so_thu_tu", Giam = true, TrangThai = LocTrangThai.HieuLuc })
            .Items.OfType<VanBanDi>().FirstOrDefault();
        if (prev == null) { Dlg.Info("Chưa có văn bản đi nào để sao chép."); return; }
        CopyFrom(Ctx.Store.GetDi(prev.Id)!);
    });

    public void CopyFrom(VanBanDi prev)
    {
        var copy = Ctx.VanBan.SaoChepDi(prev);
        copy.NgayDangKy = FormKit.ToDate(DpNgayDangKy) ?? copy.NgayDangKy;
        Bind(copy);
        _v = copy;
        CopyBanner.Visibility = Visibility.Visible;
        TxtCopyBanner.Text = $"Đã sao chép tên loại, độ mật, người ký, đơn vị lưu, số lượng, nơi nhận từ văn bản số {TextUtil.So2(prev.SoThuTu)}/{prev.Nam}. " +
                             "Không sao chép số, ký hiệu, ngày, trích yếu, ký nhận. Hãy kiểm tra lại toàn bộ trước khi lưu.";
        TxtSoKyHieu.Focus();
    }

    // ------------------------------------------------------------- lưu
    private VanBanDi Collect()
    {
        GridNoiNhan.CommitEdit(DataGridEditingUnit.Row, true);
        var v = _isNew ? new VanBanDi() : Ctx.Store.GetDi(_v.Id) ?? throw new BusinessException("Văn bản không còn tồn tại.");
        if (!_isNew) v.PhienBan = _v.PhienBan;
        v.NgayDangKy = FormKit.ToDate(DpNgayDangKy) ?? default;
        v.NgayVanBan = FormKit.ToDate(DpNgayVanBan) ?? default;
        v.Nam = _isNew ? v.NgayDangKy.Year : _v.Nam;
        if (_isNew) v.SoThuTu = Ctx.VanBan.SoDuKien(LoaiSo.Di, v.Nam, BoDem.SoThuTu);
        v.TenLoai = CbTenLoai.Text;
        v.SoKyHieu = TxtSoKyHieu.Text;
        v.DoMatId = (CbDoMat.SelectedItem as DoMat)?.Id ?? 0;
        v.TrichYeu = TxtTrichYeu.IsEnabled ? TxtTrichYeu.Text : null;
        v.NguoiKy = CbNguoiKy.Text;
        v.DonViLuu = CbDonViLuu.Text;
        v.SoLuong = FormKit.TryInt(TxtSoLuong, out var sl) ? sl : 0;
        v.GhiChu = TxtGhiChu.Text;
        v.NoiNhan = _noiNhan.Select(n => new NoiNhanKyNhan
        {
            ThuTu = n.ThuTu, NoiNhan = n.NoiNhan, DaKy = n.DaKy || n.NgayKyNhan != null, NguoiKyNhan = n.NguoiKyNhan,
            NgayKyNhan = n.NgayKyNhan is { } d ? DateOnly.FromDateTime(d) : null,
        }).ToList();
        // Nơi nhận đang gõ dở mà chưa bấm "Thêm"
        if (TextUtil.Clean(CbNoiNhan.Text) is { } pending && v.NoiNhan.All(n => TextUtil.SearchKey(n.NoiNhan) != TextUtil.SearchKey(pending)))
        {
            if (Dlg.Confirm($"Thêm \"{pending}\" vào danh sách nơi nhận?"))
            {
                AddNoiNhan();
                v.NoiNhan.Add(new NoiNhanKyNhan { ThuTu = v.NoiNhan.Count + 1, NoiNhan = pending });
            }
        }
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
                FormKit.Remember["di.domat"] = v.DoMatId.ToString();
                FormKit.Remember["di.nguoiky"] = v.NguoiKy;
                FormKit.Remember["di.donvi"] = v.DonViLuu;
                Dlg.Info($"Đã đăng ký văn bản đi số {TextUtil.So2(v.SoThuTu)}/{v.Nam}.");
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
            Dlg.Handle(ex, "lưu văn bản đi");
            return false;
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (!IsVisible) return; // lệnh bấm đến sau khi form đã lưu và đóng: không lưu lần hai
        if (Save()) DialogResult = true;
    }

    private void BtnSaveNew_Click(object sender, RoutedEventArgs e)
    {
        if (!Save()) return;
        var next = Ctx.VanBan.TaoMoiDi();
        _v = next;
        Bind(next);
        if (FormKit.Remember.TryGetValue("di.domat", out var dm) && int.TryParse(dm, out var id)) FormKit.SelectDoMat(CbDoMat, id);
        if (FormKit.Remember.TryGetValue("di.nguoiky", out var nk)) CbNguoiKy.Text = nk;
        if (FormKit.Remember.TryGetValue("di.donvi", out var dv)) CbDonViLuu.Text = dv;
        CopyBanner.Visibility = Visibility.Collapsed;
        LoadLists();
        CbTenLoai.Focus();
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
    private string DauVet() => string.Join("\u001f", TxtSoKyHieu.Text, CbTenLoai.Text, (CbDoMat.SelectedItem as DoMat)?.Id, TxtTrichYeu.Text, CbNguoiKy.Text, CbDonViLuu.Text, TxtSoLuong.Text, TxtGhiChu.Text, DpNgayVanBan.SelectedDate, DpNgayDangKy.SelectedDate, _noiNhan.Count);

    private bool IsDirty()
    {
        if (_isNew) return DauVet() != _banDau;
        try { return Ctx.VanBan.SoSanh(Collect()).Count > 0; }
        catch (Exception) { return true; }
    }
}
