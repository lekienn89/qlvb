using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Qlvb.App.Infrastructure;
using Qlvb.Application;
using Qlvb.Domain;
using Qlvb.Infrastructure.Services;

namespace Qlvb.App.Pages;

public partial class HomePage : UserControl, IPage
{
    private readonly MainWindow _main;
    public string Title => "Trang chủ";

    private sealed record RecentRow(VanBanBase V, string Loai, string So, string SoKyHieu, string NoiDung, string DoMat, string Luc);
    private sealed record BarRow(string Nhom, int SoLuong, double Width);

    public HomePage(MainWindow main)
    {
        InitializeComponent();
        _main = main;
        Ctx.DataChanged += () => { if (IsVisible) Dispatcher.BeginInvoke(OnShow); };
    }

    public void OnShow() => Dlg.Try(() =>
    {
        var nam = Ctx.Clock.Today.Year;
        TxtTitle.Text = $"Trang chủ – năm {nam}";
        var f = new ThongKeFilter { Nam = nam };
        int Count(LoaiSo l) => Ctx.TraCuu.ThongKe(TieuChiThongKe.Nam, new ThongKeFilter { Nam = nam, Loai = l }).Sum(x => x.SoLuong);
        int Huy(LoaiSo l) => Ctx.Store.Search(new SearchCriteria { Loai = l, Nam = nam, TrangThai = LocTrangThai.DaHuy, Limit = 1 }).Total;
        TxtDiLabel.Text = $"Văn bản đi năm {nam}";
        TxtDenLabel.Text = $"Văn bản đến năm {nam}";
        TxtDi.Text = Count(LoaiSo.Di).ToString();
        TxtDen.Text = Count(LoaiSo.Den).ToString();
        TxtDiDetail.Text = $"Đã hủy: {Huy(LoaiSo.Di)} • Số tiếp theo: {TextUtil.So2(Ctx.VanBan.SoDuKien(LoaiSo.Di, nam, BoDem.SoThuTu))}";
        TxtDenDetail.Text = $"Đã hủy: {Huy(LoaiSo.Den)} • Số đến tiếp theo: {Ctx.VanBan.SoDuKien(LoaiSo.Den, nam, BoDem.SoDen)}";

        var dm = Ctx.TraCuu.ThongKe(TieuChiThongKe.DoMat, f);
        var max = Math.Max(1, dm.Select(x => x.SoLuong).DefaultIfEmpty(0).Max());
        ListDoMat.ItemsSource = dm.Select(x => new BarRow(x.Nhom, x.SoLuong, 4 + 400.0 * x.SoLuong / max)).ToList();

        var kh = Ctx.InKyHieuDoMat;
        GridRecent.ItemsSource = Ctx.TraCuu.GanDay(15).Select(v => new RecentRow(v, v.Loai == LoaiSo.Di ? "Đi" : "Đến",
            $"{TextUtil.So2(v.SoThuTu)}/{v.Nam}", v.SoKyHieu, TruongBieuMau.TenLoaiVaTrichYeu(v),
            TruongBieuMau.GiaTri(v, TruongBieuMau.DoMat, kh), v.TaoLuc.ToString("dd/MM/yyyy HH:mm"))).ToList();

        SampleCard.Visibility = Ctx.DuLieuMau.CoTheNap() ? Visibility.Visible : Visibility.Collapsed;
        var last = Ctx.CauHinh.Get(ConfigKeys.LanSaoLuuCuoi);
        var hasDocs = TxtDi.Text != "0" || TxtDen.Text != "0";
        if (hasDocs && (!DateTime.TryParse(last, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) || (Ctx.Clock.Now - t).TotalDays > 7))
        {
            BackupWarn.Visibility = Visibility.Visible;
            TxtBackupWarn.Text = string.IsNullOrEmpty(last) ? "Dữ liệu chưa được sao lưu lần nào." : $"Lần sao lưu gần nhất: {t:dd/MM/yyyy}. Nên sao lưu thường xuyên ra thiết bị lưu trữ được quản lý.";
        }
        else BackupWarn.Visibility = Visibility.Collapsed;
    }, "trang chủ");

    private void BtnNewDi_Click(object sender, RoutedEventArgs e) => _main.NewDocument(LoaiSo.Di);
    private void BtnNewDen_Click(object sender, RoutedEventArgs e) => _main.NewDocument(LoaiSo.Den);
    private void BtnSearch_Click(object sender, RoutedEventArgs e) => _main.Navigate("tracuu");

    private void BtnBackup_Click(object sender, RoutedEventArgs e)
    {
        if (Dlg.Try(() =>
            {
                string path;
                using (new WaitCursor()) path = Ctx.Backup.Create(BackupKind.Nhanh);
                Dlg.Info("Đã sao lưu: " + path + "\n\nNên chép thêm bản sao lưu ra thiết bị lưu trữ được quản lý (không dùng thiết bị có kết nối mạng).");
            }, "sao lưu nhanh"))
            Ctx.NotifyChanged();
    }

    private void BtnSample_Click(object sender, RoutedEventArgs e)
    {
        if (!Dlg.Confirm("Nạp khoảng 60 văn bản MẪU (dữ liệu giả, có chữ [MẪU]) để làm quen?\n\nKhi dùng thật, bấm \"Xóa dữ liệu mẫu\" trên thanh cảnh báo; số thứ tự sẽ bắt đầu lại từ 01.")) return;
        if (Dlg.Try(() => { using (new WaitCursor()) Ctx.DuLieuMau.Nap(); }, "nạp dữ liệu mẫu"))
        {
            Ctx.NotifyChanged();
            OnShow();
        }
    }

    private void GridRecent_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (GridRecent.SelectedItem is RecentRow r) DocActions.Edit(Window.GetWindow(this)!, r.V);
    }

    public void NewItem() => _main.NewDocument(LoaiSo.Di);
}
