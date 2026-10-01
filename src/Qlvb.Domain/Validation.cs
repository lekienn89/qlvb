namespace Qlvb.Domain;

/// <summary>Một lỗi hoặc cảnh báo gắn với một trường (tên thuộc tính) để giao diện đặt con trỏ vào đúng ô.</summary>
public sealed record ValidationIssue(string Field, string Message);

public sealed class ValidationResult
{
    public List<ValidationIssue> Errors { get; } = [];
    public List<ValidationIssue> Warnings { get; } = [];
    public bool IsValid => Errors.Count == 0;

    public void Error(string field, string message) => Errors.Add(new(field, message));
    public void Warn(string field, string message) => Warnings.Add(new(field, message));
}

public static class Limits
{
    public const int SoKyHieuMax = 100;
    public const int TenMax = 300;
    public const int TrichYeuMax = 2000;
    public const int GhiChuMax = 1000;
    public const int SoLuongMax = 9999;
    public const int NamMin = 1945;
    public const int NamMax = 2200;
}

public static class VanBanValidator
{
    public static ValidationResult Validate(VanBanDi v, DoMat? doMat, DateOnly today)
    {
        var r = new ValidationResult();
        Common(v, doMat, r);
        Required(r, nameof(v.NguoiKy), "Người ký", v.NguoiKy, Limits.TenMax);
        Required(r, nameof(v.DonViLuu), "Đơn vị lưu", v.DonViLuu, Limits.TenMax);
        if (v.SoLuong < 1 || v.SoLuong > Limits.SoLuongMax)
            r.Error(nameof(v.SoLuong), $"Số lượng phải là số nguyên dương từ 1 đến {Limits.SoLuongMax}.");
        var noiNhan = v.NoiNhan.Where(n => !string.IsNullOrWhiteSpace(n.NoiNhan)).ToList();
        if (noiNhan.Count == 0)
            r.Error(nameof(v.NoiNhan), "Chưa nhập nơi nhận (cột 7). Cần ít nhất một nơi nhận.");
        var dup = noiNhan.GroupBy(n => TextUtil.SearchKey(n.NoiNhan)).FirstOrDefault(g => g.Count() > 1);
        if (dup != null)
            r.Error(nameof(v.NoiNhan), $"Nơi nhận \"{dup.First().NoiNhan.Trim()}\" bị lặp.");
        foreach (var n in noiNhan)
        {
            if (n.NoiNhan.Trim().Length > Limits.TenMax)
                r.Error(nameof(v.NoiNhan), "Tên nơi nhận quá dài.");
            if (n.NgayKyNhan is { } nk && nk < v.NgayVanBan)
                r.Error(nameof(v.NoiNhan), $"Ngày ký nhận của \"{n.NoiNhan.Trim()}\" trước ngày văn bản.");
            if (n.NgayKyNhan is { } nk2 && nk2 > today)
                r.Error(nameof(v.NoiNhan), $"Ngày ký nhận của \"{n.NoiNhan.Trim()}\" sau ngày hôm nay.");
        }
        if (v.NgayDangKy > today)
            r.Error(nameof(v.NgayDangKy), "Ngày đăng ký không được sau ngày hôm nay.");
        if (v.NgayDangKy.Year < Limits.NamMin)
            r.Error(nameof(v.NgayDangKy), "Ngày đăng ký không hợp lệ.");
        if (v.NgayVanBan > v.NgayDangKy)
            r.Warn(nameof(v.NgayVanBan), "Ngày văn bản sau ngày đăng ký vào sổ.");
        return r;
    }

    public static ValidationResult Validate(VanBanDen v, DoMat? doMat, DateOnly today)
    {
        var r = new ValidationResult();
        if (v.NgayDen > today)
            r.Error(nameof(v.NgayDen), "Ngày đến không được sau ngày hôm nay.");
        CheckDate(r, nameof(v.NgayDen), "Ngày đến", v.NgayDen);
        if (v.SoDen < 1)
            r.Error(nameof(v.SoDen), "Số đến phải là số nguyên dương.");
        Required(r, nameof(v.CoQuanBanHanh), "Cơ quan, tổ chức ban hành", v.CoQuanBanHanh, Limits.TenMax);
        Common(v, doMat, r);
        Required(r, nameof(v.DonViNhan), "Đơn vị nhận", v.DonViNhan, Limits.TenMax);
        if (v.NgayVanBan > v.NgayDen)
            r.Warn(nameof(v.NgayVanBan), "Ngày văn bản sau ngày đến. Vui lòng kiểm tra lại.");
        if (v.NgayKyNhan is { } nk)
        {
            if (nk < v.NgayDen)
                r.Error(nameof(v.NgayKyNhan), "Ngày ký nhận không được trước ngày đến.");
            if (nk > today)
                r.Error(nameof(v.NgayKyNhan), "Ngày ký nhận không được sau ngày hôm nay.");
        }
        if (v.DaKyNhan && string.IsNullOrWhiteSpace(v.NguoiKyNhan))
            r.Warn(nameof(v.NguoiKyNhan), "Đã đánh dấu ký nhận nhưng chưa ghi người ký nhận.");
        return r;
    }

    private static void Common(VanBanBase v, DoMat? doMat, ValidationResult r)
    {
        if (v.SoThuTu < 1)
            r.Error(nameof(v.SoThuTu), "Số thứ tự phải là số nguyên dương.");
        Required(r, nameof(v.SoKyHieu), "Số, ký hiệu", v.SoKyHieu, Limits.SoKyHieuMax);
        CheckDate(r, nameof(v.NgayVanBan), "Ngày tháng văn bản", v.NgayVanBan);
        Required(r, nameof(v.TenLoai), "Tên loại", v.TenLoai, Limits.TenMax);
        if (doMat == null)
            r.Error(nameof(v.DoMatId), "Chưa chọn độ mật.");
        if (doMat is { CamTrichYeu: true })
        {
            if (!string.IsNullOrWhiteSpace(v.TrichYeu))
                r.Error(nameof(v.TrichYeu),
                    $"Tài liệu độ mật \"{doMat.Ten}\" không được ghi trích yếu (Điều 6 Nghị định 63/2026/NĐ-CP).");
        }
        else
        {
            Required(r, nameof(v.TrichYeu), "Trích yếu nội dung", v.TrichYeu, Limits.TrichYeuMax);
        }
        if (v.GhiChu is { Length: > Limits.GhiChuMax })
            r.Error(nameof(v.GhiChu), $"Ghi chú quá dài (tối đa {Limits.GhiChuMax} ký tự).");
    }

    private static void Required(ValidationResult r, string field, string label, string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            r.Error(field, $"Chưa nhập \"{label}\".");
        else if (value.Trim().Length > max)
            r.Error(field, $"\"{label}\" quá dài (tối đa {max} ký tự).");
    }

    private static void CheckDate(ValidationResult r, string field, string label, DateOnly d)
    {
        if (d.Year < Limits.NamMin || d.Year > Limits.NamMax)
            r.Error(field, $"\"{label}\" không hợp lệ.");
    }
}
