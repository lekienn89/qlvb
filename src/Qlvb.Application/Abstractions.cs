using Qlvb.Domain;

namespace Qlvb.Application;

public interface IClock
{
    DateTime Now { get; }
    DateOnly Today => DateOnly.FromDateTime(Now);
}

public sealed class SystemClock : IClock
{
    public DateTime Now => DateTime.Now;
}

/// <summary>Người thực hiện thao tác (ghi vào nhật ký).</summary>
public interface ICurrentUser
{
    string Name { get; }
}

/// <summary>Lỗi nghiệp vụ: thông điệp tiếng Việt hiển thị trực tiếp cho người dùng, không chứa nội dung bí mật.</summary>
public class BusinessException(string message, string? field = null) : Exception(message)
{
    public string? Field { get; } = field;
}

public sealed class ValidationException(ValidationResult result)
    : BusinessException(string.Join("\n", result.Errors.Select(e => e.Message)), result.Errors.FirstOrDefault()?.Field)
{
    public ValidationResult Result { get; } = result;
}

/// <summary>Bản ghi đã bị thay đổi ở nơi khác (khóa lạc quan).</summary>
public sealed class ConcurrencyException()
    : BusinessException("Bản ghi đã được thay đổi sau khi mở. Vui lòng đóng và mở lại để xem dữ liệu mới nhất.");

public sealed record AuditEntry(string HanhDong, string DoiTuong, long? BanGhiId, string MoTa, string? ThayDoiJson = null);

public sealed class AuditRecord
{
    public long Id { get; set; }
    public DateTime ThoiGian { get; set; }
    public string HanhDong { get; set; } = "";
    public string DoiTuong { get; set; } = "";
    public long? BanGhiId { get; set; }
    public string NguoiThucHien { get; set; } = "";
    public string MoTa { get; set; } = "";
    public string? ThayDoi { get; set; }
}

public sealed class AuditFilter
{
    public DateTime? Tu { get; set; }
    public DateTime? Den { get; set; }
    public string? HanhDong { get; set; }
    public string? DoiTuong { get; set; }
    public int Limit { get; set; } = 5000;
}

public enum LocTrangThai { HieuLuc, DaHuy, TatCa }

public sealed class SearchCriteria
{
    public LoaiSo Loai { get; set; }
    public int? Nam { get; set; }
    public long? SoDangKyId { get; set; }
    public int? SoThuTuTu { get; set; }
    public int? SoThuTuDen { get; set; }
    public int? SoDen { get; set; }
    public string? SoKyHieu { get; set; }
    /// <summary>Khoảng ngày chính: ngày văn bản (sổ đi) hoặc ngày đến (sổ đến).</summary>
    public DateOnly? TuNgay { get; set; }
    public DateOnly? DenNgay { get; set; }
    /// <summary>Khoảng ngày văn bản (dùng cho sổ đến).</summary>
    public DateOnly? NgayVanBanTu { get; set; }
    public DateOnly? NgayVanBanDen { get; set; }
    public string? TenLoai { get; set; }
    public string? TrichYeu { get; set; }
    public int? DoMatId { get; set; }
    public string? NguoiKy { get; set; }
    public string? NoiNhan { get; set; }
    public string? DonViLuu { get; set; }
    public string? CoQuanBanHanh { get; set; }
    public string? DonViNhan { get; set; }
    /// <summary>Tìm nhanh: mọi từ (không dấu) phải xuất hiện trong bản ghi.</summary>
    public string? TuKhoa { get; set; }
    public LocTrangThai TrangThai { get; set; } = LocTrangThai.TatCa;
    public string SapXep { get; set; } = "so_thu_tu";
    public bool Giam { get; set; } = true;
    public int Offset { get; set; }
    /// <summary>0 = tất cả.</summary>
    public int Limit { get; set; } = 50;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total);

public sealed record DemTheoNhom(string Nhom, int SoLuong);

public sealed class ThongKeFilter
{
    public LoaiSo? Loai { get; set; }
    public int? Nam { get; set; }
    public DateOnly? TuNgay { get; set; }
    public DateOnly? DenNgay { get; set; }
    public int? DoMatId { get; set; }
    public bool BaoGomDaHuy { get; set; }
}

public enum TieuChiThongKe { Nam, Thang, DoMat, CoQuanBanHanh, NguoiKy, DonVi, LoaiVanBan }

/// <summary>Kho dữ liệu. Mỗi phương thức ghi chạy trong một giao dịch và ghi nhật ký trong cùng giao dịch đó.</summary>
public interface IDataStore
{
    // Cấu hình
    string? GetConfig(string key);
    void SetConfig(string key, string? value, AuditEntry? audit = null);

    // Độ mật
    IReadOnlyList<DoMat> ListDoMat(bool includeInactive = false);
    DoMat? GetDoMat(int id);
    int SaveDoMat(DoMat d, AuditEntry audit);
    int CountDocumentsWithTrichYeu(int doMatId);

    // Danh mục
    IReadOnlyList<MucDanhMuc> ListDanhMuc(string nhom, bool includeInactive = false, string? tuKhoa = null);
    MucDanhMuc? FindDanhMuc(string nhom, string ten);
    long AddDanhMuc(MucDanhMuc m, AuditEntry audit);
    void UpdateDanhMuc(MucDanhMuc m, AuditEntry audit);
    bool IsDanhMucUsed(long id);
    void DeleteDanhMuc(long id, AuditEntry audit);
    /// <summary>Tăng số lần dùng; nếu chưa có thì thêm mới (ghi nhật ký "Thêm danh mục (tự động)").</summary>
    void TouchDanhMuc(string nhom, string ten, bool laDuLieuMau);
    IReadOnlyList<string> Suggestions(string nhom, int limit = 200);

    // Biểu mẫu
    BieuMau? GetBieuMau(string ma);
    IReadOnlyList<BieuMau> ListBieuMau();

    // Sổ đăng ký
    IReadOnlyList<SoDangKy> ListSoDangKy(LoaiSo? loai = null);
    SoDangKy? GetSoDangKy(long id);
    SoDangKy? CurrentSoDangKy(LoaiSo loai, int nam);
    long AddSoDangKy(SoDangKy s, int soBatDau, AuditEntry audit);
    void SetSoDangKyLocked(long id, bool locked, AuditEntry audit);
    int PeekNextNumber(LoaiSo loai, int nam, string boDem);

    // Văn bản
    VanBanDi? GetDi(long id);
    VanBanDen? GetDen(long id);
    long InsertDi(VanBanDi v, Func<VanBanDi, AuditEntry> audit);
    long InsertDen(VanBanDen v, Func<VanBanDen, AuditEntry> audit);
    void UpdateDi(VanBanDi v, AuditEntry audit);
    void UpdateDen(VanBanDen v, AuditEntry audit);
    void SetTrangThai(LoaiSo loai, long id, TrangThaiBanGhi trangThai, string? lyDo, string nguoi, DateTime luc, int phienBan, AuditEntry audit);
    /// <summary>Xóa hẳn bản ghi. Nếu đó là số cuối cùng đã cấp trong năm thì lùi bộ đếm để số này được cấp lại.
    /// Trả về true khi số được thu hồi; <paramref name="audit"/> nhận kết quả đó để ghi nhật ký.</summary>
    bool HardDelete(LoaiSo loai, long id, Func<bool, AuditEntry> audit);
    PagedResult<VanBanBase> Search(SearchCriteria c);
    IReadOnlyList<VanBanBase> FindDuplicates(VanBanBase v);
    IReadOnlyList<VanBanBase> Recent(int limit);
    IReadOnlyList<int> Years(LoaiSo? loai = null);

    // Thống kê
    IReadOnlyList<DemTheoNhom> ThongKe(TieuChiThongKe tieuChi, ThongKeFilter f);

    // Dữ liệu mẫu
    int DeleteSampleData(AuditEntry audit);

    // Nhật ký
    void AppendAudit(AuditEntry e);
    IReadOnlyList<AuditRecord> ListAudit(AuditFilter f);
    /// <summary>Kiểm tra chuỗi băm nhật ký. Trả về null nếu toàn vẹn, ngược lại mô tả vị trí lỗi.</summary>
    string? VerifyAuditChain();
}
