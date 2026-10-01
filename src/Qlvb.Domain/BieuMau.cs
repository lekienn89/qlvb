using System.Globalization;

namespace Qlvb.Domain;

/// <summary>
/// Định nghĩa một mẫu sổ (cột, tiêu đề, hướng dẫn ghi) theo một căn cứ pháp lý cụ thể.
/// Lưu trong cơ sở dữ liệu dạng JSON để khi pháp luật thay đổi chỉ cần thêm phiên bản mới.
/// </summary>
public sealed class BieuMau
{
    public string Ma { get; set; } = "";
    public LoaiSo Loai { get; set; }
    public string TieuDe { get; set; } = "";
    public string CanCu { get; set; } = "";
    public DateOnly HieuLucTu { get; set; }
    public int PhienBan { get; set; } = 1;
    public List<CotBieuMau> Cot { get; set; } = [];
    public List<string> HuongDanTrangBia { get; set; } = [];
}

public sealed class CotBieuMau
{
    public int So { get; set; }
    public string TieuDe { get; set; } = "";
    /// <summary>Khóa trường dữ liệu, xem <see cref="TruongBieuMau"/>.</summary>
    public string Truong { get; set; } = "";
    /// <summary>Độ rộng tương đối.</summary>
    public double DoRong { get; set; } = 1;
    public bool CanGiua { get; set; }
    public string HuongDan { get; set; } = "";
}

/// <summary>Khóa trường mà một cột biểu mẫu có thể hiển thị, và cách lấy giá trị từ bản ghi.</summary>
public static class TruongBieuMau
{
    public const string SoThuTu = "so_thu_tu";
    public const string SoKyHieu = "so_ky_hieu";
    public const string NgayVanBan = "ngay_van_ban";
    public const string TenLoaiTrichYeu = "ten_loai_trich_yeu";
    public const string DoMat = "do_mat";
    public const string NguoiKy = "nguoi_ky";
    public const string NoiNhanKyNhan = "noi_nhan_ky_nhan";
    public const string DonViLuu = "don_vi_luu";
    public const string SoLuong = "so_luong";
    public const string GhiChu = "ghi_chu";
    public const string NgayDen = "ngay_den";
    public const string SoDen = "so_den";
    public const string CoQuanBanHanh = "co_quan_ban_hanh";
    public const string DonViKyNhan = "don_vi_ky_nhan";

    public static readonly IReadOnlySet<string> TatCa = new HashSet<string>
    {
        SoThuTu, SoKyHieu, NgayVanBan, TenLoaiTrichYeu, DoMat, NguoiKy, NoiNhanKyNhan, DonViLuu,
        SoLuong, GhiChu, NgayDen, SoDen, CoQuanBanHanh, DonViKyNhan,
    };

    /// <summary>
    /// Giá trị hiển thị của một trường. Đây là nơi DUY NHẤT dựng nội dung ô cho in, xuất, danh sách,
    /// nên quy tắc không hiển thị trích yếu của tài liệu cấm trích yếu được áp dụng nhất quán.
    /// </summary>
    public static string GiaTri(VanBanBase v, string truong, bool dungKyHieuDoMat = false)
    {
        var inv = CultureInfo.InvariantCulture;
        switch (truong)
        {
            case SoThuTu: return TextUtil.So2(v.SoThuTu);
            case SoKyHieu: return v.SoKyHieu;
            case NgayVanBan: return TextUtil.FormatDate(v.NgayVanBan);
            case TenLoaiTrichYeu: return TenLoaiVaTrichYeu(v);
            case DoMat: return dungKyHieuDoMat && v.DoMatKyHieu.Length > 0 ? v.DoMatKyHieu : v.DoMatTen;
            case GhiChu:
                {
                    var parts = new List<string>();
                    if (v.DaHuy) parts.Add($"ĐÃ HỦY{(string.IsNullOrWhiteSpace(v.LyDoHuy) ? "" : ": " + v.LyDoHuy)}");
                    if (!string.IsNullOrWhiteSpace(v.GhiChu)) parts.Add(v.GhiChu!);
                    return string.Join("; ", parts);
                }
        }
        if (v is VanBanDi di)
        {
            switch (truong)
            {
                case NguoiKy: return di.NguoiKy;
                case DonViLuu: return di.DonViLuu;
                case SoLuong: return TextUtil.So2(di.SoLuong);
                case NoiNhanKyNhan:
                    return string.Join("\n", di.NoiNhan.OrderBy(n => n.ThuTu).Select(n =>
                        n.NoiNhan + (n.DaKy || n.NgayKyNhan != null
                            ? $" – đã ký nhận{(string.IsNullOrWhiteSpace(n.NguoiKyNhan) ? "" : " (" + n.NguoiKyNhan + ")")}{(n.NgayKyNhan is { } d ? " ngày " + TextUtil.FormatDate(d) : "")}"
                            : "")));
            }
        }
        if (v is VanBanDen den)
        {
            switch (truong)
            {
                case NgayDen: return TextUtil.FormatDate(den.NgayDen);
                case SoDen: return den.SoDen.ToString(inv);
                case CoQuanBanHanh: return den.CoQuanBanHanh;
                case DonViKyNhan:
                    return den.DonViNhan + (den.DaKyNhan || den.NgayKyNhan != null
                        ? $"\n– đã ký nhận{(string.IsNullOrWhiteSpace(den.NguoiKyNhan) ? "" : " (" + den.NguoiKyNhan + ")")}{(den.NgayKyNhan is { } d ? " ngày " + TextUtil.FormatDate(d) : "")}"
                        : "");
            }
        }
        return "";
    }

    public static string TenLoaiVaTrichYeu(VanBanBase v)
    {
        var ty = v.TrichYeu;
        if (string.IsNullOrWhiteSpace(ty)) return v.TenLoai;
        return $"{v.TenLoai}: {ty}";
    }
}
