namespace Qlvb.Domain;

/// <summary>Loại sổ đăng ký theo Phụ lục III Nghị định 63/2026/NĐ-CP.</summary>
public enum LoaiSo
{
    Di = 1,
    Den = 2,
}

public enum TrangThaiBanGhi
{
    HieuLuc = 0,
    DaHuy = 1,
}

/// <summary>Mục trong danh mục độ mật. Quy tắc cấm trích yếu là thuộc tính dữ liệu, không hard-code theo tên.</summary>
public sealed record DoMat(int Id, string Ten, string KyHieu, int Muc, bool CamTrichYeu, bool DangDung)
{
    public string HienThi => string.IsNullOrEmpty(KyHieu) ? Ten : $"{Ten} ({KyHieu})";
}

public static class NhomDanhMuc
{
    public const string CoQuanBanHanh = "co_quan";
    public const string NoiNhan = "noi_nhan";
    public const string NguoiKy = "nguoi_ky";
    public const string DonVi = "don_vi";
    public const string LoaiVanBan = "loai_van_ban";

    public static readonly IReadOnlyDictionary<string, string> TenNhom = new Dictionary<string, string>
    {
        [LoaiVanBan] = "Loại văn bản",
        [CoQuanBanHanh] = "Cơ quan ban hành",
        [NoiNhan] = "Nơi nhận",
        [NguoiKy] = "Người ký",
        [DonVi] = "Đơn vị",
    };
}

public sealed class MucDanhMuc
{
    public long Id { get; set; }
    public string Nhom { get; set; } = "";
    public string Ten { get; set; } = "";
    /// <summary>Chữ viết tắt (loại văn bản) hoặc chức vụ (người ký).</summary>
    public string? PhuDe { get; set; }
    public int ThuTu { get; set; }
    public bool DangDung { get; set; } = true;
    public int SoLanDung { get; set; }
    public bool LaDuLieuMau { get; set; }
}

/// <summary>Một quyển sổ đăng ký (trang bìa theo mẫu).</summary>
public sealed class SoDangKy
{
    public long Id { get; set; }
    public LoaiSo Loai { get; set; }
    public int Nam { get; set; }
    public int QuyenSo { get; set; } = 1;
    public string TenCoQuan { get; set; } = "";
    public string? CoQuanChuQuan { get; set; }
    public string MaBieuMau { get; set; } = "";
    public DateOnly NgayMo { get; set; }
    public bool DaKhoa { get; set; }
    public DateTime? NgayKhoa { get; set; }

    // Thống kê tính từ dữ liệu
    public int? SoDau { get; set; }
    public int? SoCuoi { get; set; }
    public DateOnly? NgayDau { get; set; }
    public DateOnly? NgayCuoi { get; set; }
    public int SoBanGhi { get; set; }

    public string TenHienThi => $"{(Loai == LoaiSo.Di ? "Sổ đi" : "Sổ đến")} năm {Nam} – quyển {QuyenSo}{(DaKhoa ? " (đã khóa)" : "")}";
}

public abstract class VanBanBase
{
    public long Id { get; set; }
    public long SoDangKyId { get; set; }
    public int Nam { get; set; }
    public int SoThuTu { get; set; }
    public string SoKyHieu { get; set; } = "";
    public DateOnly NgayVanBan { get; set; }
    public string TenLoai { get; set; } = "";
    public string? TrichYeu { get; set; }
    public int DoMatId { get; set; }
    /// <summary>Tên độ mật tại thời điểm đăng ký (bản chụp).</summary>
    public string DoMatTen { get; set; } = "";
    public string DoMatKyHieu { get; set; } = "";
    public string? GhiChu { get; set; }
    public TrangThaiBanGhi TrangThai { get; set; }
    public string? LyDoHuy { get; set; }
    public DateTime? ThoiGianHuy { get; set; }
    public string? NguoiHuy { get; set; }
    public DateOnly NgayDangKy { get; set; }
    public DateTime TaoLuc { get; set; }
    public string TaoBoi { get; set; } = "";
    public DateTime CapNhatLuc { get; set; }
    public string CapNhatBoi { get; set; } = "";
    public int PhienBan { get; set; }
    public bool LaDuLieuMau { get; set; }

    public abstract LoaiSo Loai { get; }
    public bool DaHuy => TrangThai == TrangThaiBanGhi.DaHuy;
}

public sealed class NoiNhanKyNhan
{
    public int ThuTu { get; set; }
    public string NoiNhan { get; set; } = "";
    public bool DaKy { get; set; }
    public string? NguoiKyNhan { get; set; }
    public DateOnly? NgayKyNhan { get; set; }

    public NoiNhanKyNhan Clone() => (NoiNhanKyNhan)MemberwiseClone();
}

public sealed class VanBanDi : VanBanBase
{
    public override LoaiSo Loai => LoaiSo.Di;
    public string NguoiKy { get; set; } = "";
    public string DonViLuu { get; set; } = "";
    public int SoLuong { get; set; } = 1;
    public List<NoiNhanKyNhan> NoiNhan { get; set; } = [];
}

public sealed class VanBanDen : VanBanBase
{
    public override LoaiSo Loai => LoaiSo.Den;
    public DateOnly NgayDen { get; set; }
    public int SoDen { get; set; }
    public string CoQuanBanHanh { get; set; } = "";
    public string DonViNhan { get; set; } = "";
    public bool DaKyNhan { get; set; }
    public string? NguoiKyNhan { get; set; }
    public DateOnly? NgayKyNhan { get; set; }
}
