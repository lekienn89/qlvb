namespace Qlvb.Application;

public static class ConfigKeys
{
    public const string TenCoQuan = "ten_co_quan";
    public const string CoQuanChuQuan = "co_quan_chu_quan";
    public const string KyHieuCoQuan = "ky_hieu_co_quan";
    public const string MauSoKyHieu = "mau_so_ky_hieu";
    public const string MauSoKyHieuCongVan = "mau_so_ky_hieu_cong_van";
    public const string TuKhoaPhut = "tu_khoa_phut";
    public const string ChoPhepXuatMat = "cho_phep_xuat_mat";
    public const string InKyHieuDoMat = "in_ky_hieu_do_mat";
    public const string SoBanSaoLuuGiuLai = "so_ban_sao_luu_giu_lai";
    public const string TuSaoLuuKhiThoat = "tu_sao_luu_khi_thoat";
    public const string LanSaoLuuCuoi = "lan_sao_luu_cuoi";
    public const string DaCoDuLieuMau = "da_co_du_lieu_mau";

    public const string MacDinhMauSoKyHieu = "{so}/{viet_tat}-{ky_hieu_co_quan}";
    public const string MacDinhMauCongVan = "{so}/{ky_hieu_co_quan}";
    public const int MacDinhTuKhoaPhut = 10;
    public const int MacDinhSoBanSaoLuu = 30;

    /// <summary>Các khóa cấu hình quan trọng: thay đổi phải ghi nhật ký.</summary>
    public static readonly IReadOnlyDictionary<string, string> TenHienThi = new Dictionary<string, string>
    {
        [TenCoQuan] = "Tên cơ quan, tổ chức",
        [CoQuanChuQuan] = "Cơ quan chủ quản cấp trên",
        [KyHieuCoQuan] = "Ký hiệu cơ quan",
        [MauSoKyHieu] = "Mẫu số, ký hiệu",
        [MauSoKyHieuCongVan] = "Mẫu số, ký hiệu công văn",
        [TuKhoaPhut] = "Tự khóa sau (phút)",
        [ChoPhepXuatMat] = "Cho phép xuất dữ liệu có độ mật",
        [InKyHieuDoMat] = "In ký hiệu độ mật A/B/C",
        [SoBanSaoLuuGiuLai] = "Số bản sao lưu tự động giữ lại",
        [TuSaoLuuKhiThoat] = "Tự sao lưu khi thoát",
    };
}
