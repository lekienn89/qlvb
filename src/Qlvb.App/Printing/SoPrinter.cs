using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using Qlvb.Application;
using Qlvb.Domain;

namespace Qlvb.App.Printing;

public sealed class SoPrintOptions
{
    public bool Landscape { get; init; } = true;
    public bool Cover { get; init; } = true;
    public bool KyHieuDoMat { get; init; }
    public bool BaoGomDaHuy { get; init; } = true;
}

/// <summary>In sổ đăng ký theo đúng biểu mẫu (cột, tiêu đề, trang bìa) lưu trong cơ sở dữ liệu.</summary>
public static class SoPrinter
{
    public static FixedDocument Build(BieuMau bm, SoDangKy? so, int nam, IReadOnlyList<VanBanBase> items, SoPrintOptions o, string tenCoQuan, string? chuQuan, DateTime now)
    {
        var rows = items.Select(v => bm.Cot.Select(c => TruongBieuMau.GiaTri(v, c.Truong, o.KyHieuDoMat)).ToArray()).ToList();
        var huy = items.Select(v => v.DaHuy).ToArray();
        var quyen = so != null ? $" – Quyển số {so.QuyenSo}" : "";
        var spec = new TableSpec
        {
            Landscape = o.Landscape,
            RunningTitle = $"{bm.TieuDe} – Năm {nam}{quyen}{(string.IsNullOrWhiteSpace(tenCoQuan) ? "" : " – " + tenCoQuan)}",
            FirstPageTitle = [bm.TieuDe, $"Năm {nam}{quyen}"],
            Columns = bm.Cot.Select(c => new PrintColumn(c.TieuDe, c.DoRong, c.CanGiua, $"({c.So})")).ToList(),
            Rows = rows,
            Italic = i => huy[i],
            FooterRight = "Mẫu: " + bm.Ma,
            Cover = o.Cover ? size => CoverPage(size, bm, so, nam, items, tenCoQuan, chuQuan) : null,
        };
        return TableDocument.Build(spec, now);
    }

    /// <summary>Trang bìa theo hướng dẫn (1)–(6) của biểu mẫu.</summary>
    private static FixedPage CoverPage(Size size, BieuMau bm, SoDangKy? so, int nam, IReadOnlyList<VanBanBase> items, string tenCoQuan, string? chuQuan)
    {
        var page = new FixedPage { Width = size.Width, Height = size.Height, Background = Brushes.White };
        var m = TableDocument.Mm(25);
        var frame = new Rectangle { Width = size.Width - 2 * m, Height = size.Height - 2 * m, Stroke = Brushes.Black, StrokeThickness = 2 };
        FixedPage.SetLeft(frame, m);
        FixedPage.SetTop(frame, m);
        page.Children.Add(frame);
        var w = size.Width - 2 * m - 40;
        var x = m + 20;
        var y = m + 30;
        var dongSo = items.Select(v => v.SoThuTu).DefaultIfEmpty().ToList();
        var ngay = items.Select(v => v is VanBanDen d ? d.NgayDen : v.NgayDangKy).DefaultIfEmpty().ToList();
        string D(DateOnly? d) => d is { } x1 && x1 != default ? TextUtil.FormatDate(x1) : "……/……/………";
        string S(int? s) => s is > 0 ? TextUtil.So2(s.Value) : "………";

        TableDocument.Text(page, (chuQuan ?? "").ToUpperInvariant(), x, y, w / 2, 15, false, TextAlignment.Center);
        y += 24;
        TableDocument.Text(page, string.IsNullOrWhiteSpace(tenCoQuan) ? "……………………………" : tenCoQuan.ToUpperInvariant(), x, y, w / 2, 15, true, TextAlignment.Center);
        y += size.Height / 5;
        TableDocument.Text(page, bm.TieuDe, x, y, w, 26, true, TextAlignment.Center);
        y += 56;
        TableDocument.Text(page, $"Năm: {nam}", x, y, w, 18, true, TextAlignment.Center);
        y += 44;
        var lines = new[]
        {
            $"Từ ngày {D(items.Count > 0 ? ngay.Min() : null)} đến ngày {D(items.Count > 0 && so?.DaKhoa == true ? ngay.Max() : null)}",
            $"Từ số {S(items.Count > 0 ? dongSo.Min() : null)} đến số {S(items.Count > 0 && so?.DaKhoa == true ? dongSo.Max() : null)}",
            $"Quyển số: {(so != null ? so.QuyenSo.ToString() : "………")}",
        };
        foreach (var l in lines)
        {
            TableDocument.Text(page, l, x, y, w, 16, false, TextAlignment.Center);
            y += 30;
        }
        TableDocument.Text(page, "Căn cứ: " + bm.CanCu, x, size.Height - m - 60, w, 11, false, TextAlignment.Center, FontStyles.Italic);
        return page;
    }

    /// <summary>Lấy dữ liệu cần in (sắp tăng dần theo số thứ tự).</summary>
    public static IReadOnlyList<VanBanBase> Data(IDataStore store, LoaiSo loai, int nam, long? soId, bool baoGomDaHuy, DateOnly? tu, DateOnly? den) =>
        store.Search(new SearchCriteria
        {
            Loai = loai, Nam = nam, SoDangKyId = soId, Limit = 0, SapXep = "so_thu_tu", Giam = false,
            TrangThai = baoGomDaHuy ? LocTrangThai.TatCa : LocTrangThai.HieuLuc,
            TuNgay = loai == LoaiSo.Den ? tu : null, DenNgay = loai == LoaiSo.Den ? den : null,
        }).Items.Where(v => loai == LoaiSo.Den || ((tu == null || v.NgayDangKy >= tu) && (den == null || v.NgayDangKy <= den))).ToList();
}
