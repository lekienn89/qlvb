using System.Text.Json;
using Qlvb.Domain;

namespace Qlvb.Application;

/// <summary>Kết quả kiểm tra trước khi lưu: lỗi cứng (không cho lưu) và cảnh báo mềm (người dùng quyết định).</summary>
public sealed class KiemTraTruocLuu
{
    public ValidationResult KetQua { get; init; } = new();
    public IReadOnlyList<VanBanBase> NghiTrung { get; init; } = [];
    public bool CoCanhBao => KetQua.Warnings.Count > 0 || NghiTrung.Count > 0;
}

public sealed record ThayDoiTruong(string Truong, string Cu, string Moi);

public sealed class VanBanService(IDataStore store, IClock clock, ICurrentUser user)
{
    private const string TrichYeuAn = "(nội dung trích yếu – không ghi vào nhật ký)";

    // ------------------------------------------------------------------ tạo mới
    public VanBanDi TaoMoiDi()
    {
        var today = clock.Today;
        return new VanBanDi
        {
            NgayDangKy = today,
            NgayVanBan = today,
            Nam = today.Year,
            SoThuTu = store.PeekNextNumber(LoaiSo.Di, today.Year, BoDem.SoThuTu),
            SoLuong = 1,
            NoiNhan = [],
        };
    }

    public VanBanDen TaoMoiDen()
    {
        var today = clock.Today;
        return new VanBanDen
        {
            NgayDangKy = today,
            NgayDen = today,
            NgayVanBan = today,
            Nam = today.Year,
            SoThuTu = store.PeekNextNumber(LoaiSo.Den, today.Year, BoDem.SoThuTu),
            SoDen = store.PeekNextNumber(LoaiSo.Den, today.Year, BoDem.SoDen),
        };
    }

    /// <summary>
    /// Sao chép thông tin lặp lại từ bản ghi trước để tạo mới. KHÔNG sao chép thông tin định danh
    /// (ID, số thứ tự, số đến, số ký hiệu, ngày văn bản, trích yếu, ký nhận, ghi chú, trạng thái).
    /// </summary>
    public VanBanDi SaoChepDi(VanBanDi nguon)
    {
        var v = TaoMoiDi();
        v.TenLoai = nguon.TenLoai;
        v.DoMatId = nguon.DoMatId;
        v.NguoiKy = nguon.NguoiKy;
        v.DonViLuu = nguon.DonViLuu;
        v.SoLuong = nguon.SoLuong;
        v.NoiNhan = nguon.NoiNhan.OrderBy(n => n.ThuTu)
            .Select((n, i) => new NoiNhanKyNhan { ThuTu = i + 1, NoiNhan = n.NoiNhan }).ToList();
        return v;
    }

    public VanBanDen SaoChepDen(VanBanDen nguon)
    {
        var v = TaoMoiDen();
        v.CoQuanBanHanh = nguon.CoQuanBanHanh;
        v.TenLoai = nguon.TenLoai;
        v.DoMatId = nguon.DoMatId;
        v.DonViNhan = nguon.DonViNhan;
        return v;
    }

    public string GoiYSoKyHieu(int soThuTu, string? tenLoai, int nam)
    {
        var loai = string.IsNullOrWhiteSpace(tenLoai) ? null : store.FindDanhMuc(NhomDanhMuc.LoaiVanBan, tenLoai);
        return SoKyHieu.GoiY(
            store.GetConfig(ConfigKeys.MauSoKyHieu) ?? ConfigKeys.MacDinhMauSoKyHieu,
            store.GetConfig(ConfigKeys.MauSoKyHieuCongVan) ?? ConfigKeys.MacDinhMauCongVan,
            soThuTu, loai?.PhuDe, store.GetConfig(ConfigKeys.KyHieuCoQuan), nam);
    }

    public int SoDuKien(LoaiSo loai, int nam, string boDem) => store.PeekNextNumber(loai, nam, boDem);

    // ------------------------------------------------------------------ kiểm tra
    public KiemTraTruocLuu KiemTra(VanBanBase v)
    {
        Normalize(v);
        var doMat = store.GetDoMat(v.DoMatId);
        var result = v switch
        {
            VanBanDi di => VanBanValidator.Validate(di, doMat, clock.Today),
            VanBanDen den => VanBanValidator.Validate(den, doMat, clock.Today),
            _ => throw new ArgumentException(nameof(v)),
        };
        if (doMat is { DangDung: false } && v.Id == 0)
            result.Error(nameof(v.DoMatId), $"Độ mật \"{doMat.Ten}\" đã ngừng sử dụng.");
        KiemTraDanhMuc(v, result);
        if (v.Id != 0)
        {
            var cu = Get(v.Loai, v.Id) ?? throw new BusinessException("Văn bản không còn tồn tại.");
            if (cu.Nam != NamCuaBanGhi(v))
                result.Error(v is VanBanDen ? nameof(VanBanDen.NgayDen) : nameof(VanBanBase.NgayDangKy),
                    $"Không được chuyển văn bản sang năm khác (văn bản thuộc sổ năm {cu.Nam}).");
        }
        var dups = result.IsValid ? store.FindDuplicates(v).Where(x => x.Id != v.Id).ToList() : [];
        return new KiemTraTruocLuu { KetQua = result, NghiTrung = dups };
    }

    private void KiemTraDanhMuc(VanBanBase v, ValidationResult r)
    {
        // Các trường bắt buộc chọn từ danh mục (có thể "thêm nhanh" ngay trên form).
        void Must(string nhom, string field, string label, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            var m = store.FindDanhMuc(nhom, value);
            if (m == null)
                r.Error(field, $"\"{value}\" chưa có trong danh mục {label}. Bấm nút + để thêm nhanh.");
            else if (!m.DangDung && v.Id == 0)
                r.Error(field, $"\"{value}\" trong danh mục {label} đã ngừng sử dụng.");
        }
        Must(NhomDanhMuc.LoaiVanBan, nameof(v.TenLoai), "loại văn bản", v.TenLoai);
        if (v is VanBanDi di)
        {
            Must(NhomDanhMuc.NguoiKy, nameof(di.NguoiKy), "người ký", di.NguoiKy);
            Must(NhomDanhMuc.DonVi, nameof(di.DonViLuu), "đơn vị", di.DonViLuu);
        }
    }

    private static int NamCuaBanGhi(VanBanBase v) => v is VanBanDen den ? den.NgayDen.Year : v.NgayDangKy.Year;

    private static void Normalize(VanBanBase v)
    {
        v.SoKyHieu = TextUtil.Clean(v.SoKyHieu) ?? "";
        v.TenLoai = TextUtil.Clean(v.TenLoai) ?? "";
        v.TrichYeu = TextUtil.CleanMultiline(v.TrichYeu);
        v.GhiChu = TextUtil.CleanMultiline(v.GhiChu);
        switch (v)
        {
            case VanBanDi di:
                di.NguoiKy = TextUtil.Clean(di.NguoiKy) ?? "";
                di.DonViLuu = TextUtil.Clean(di.DonViLuu) ?? "";
                di.NoiNhan = di.NoiNhan
                    .Select(n => { var c = n.Clone(); c.NoiNhan = TextUtil.Clean(c.NoiNhan) ?? ""; c.NguoiKyNhan = TextUtil.Clean(c.NguoiKyNhan); return c; })
                    .Where(n => n.NoiNhan.Length > 0)
                    .Select((n, i) => { n.ThuTu = i + 1; return n; }).ToList();
                break;
            case VanBanDen den:
                den.CoQuanBanHanh = TextUtil.Clean(den.CoQuanBanHanh) ?? "";
                den.DonViNhan = TextUtil.Clean(den.DonViNhan) ?? "";
                den.NguoiKyNhan = TextUtil.Clean(den.NguoiKyNhan);
                den.NgayDangKy = den.NgayDen;
                break;
        }
    }

    // ------------------------------------------------------------------ lưu
    /// <summary>Lưu văn bản mới. Gọi <see cref="KiemTra"/> trước để hiển thị cảnh báo cho người dùng.</summary>
    public long ThemMoi(VanBanBase v)
    {
        if (v.Id != 0) throw new InvalidOperationException("Văn bản đã có ID.");
        var kt = KiemTra(v);
        if (!kt.KetQua.IsValid) throw new ValidationException(kt.KetQua);
        var doMat = store.GetDoMat(v.DoMatId)!;
        if (doMat.CamTrichYeu) v.TrichYeu = null;
        v.DoMatTen = doMat.Ten;
        v.DoMatKyHieu = doMat.KyHieu;
        v.Nam = NamCuaBanGhi(v);
        v.TrangThai = TrangThaiBanGhi.HieuLuc;
        var now = clock.Now;
        v.TaoLuc = v.CapNhatLuc = now;
        v.TaoBoi = v.CapNhatBoi = user.Name;
        v.PhienBan = 1;
        GhiNhoDanhMuc(v);
        return v switch
        {
            VanBanDi di => store.InsertDi(di, x => new AuditEntry("Tạo văn bản đi", "van_ban_di", x.Id,
                $"Đăng ký số {TextUtil.So2(x.SoThuTu)}/{x.Nam}, ký hiệu {x.SoKyHieu}, độ mật {x.DoMatTen}")),
            VanBanDen den => store.InsertDen(den, x => new AuditEntry("Tạo văn bản đến", "van_ban_den", x.Id,
                $"Đăng ký số {TextUtil.So2(x.SoThuTu)}/{x.Nam}, số đến {x.SoDen}, ký hiệu {x.SoKyHieu}, độ mật {x.DoMatTen}")),
            _ => throw new ArgumentException(nameof(v)),
        };
    }

    /// <summary>Danh sách thay đổi giữa bản ghi đang lưu và bản ghi mới (để hiển thị xác nhận và ghi nhật ký).</summary>
    public IReadOnlyList<ThayDoiTruong> SoSanh(VanBanBase moi)
    {
        var cu = Get(moi.Loai, moi.Id) ?? throw new BusinessException("Văn bản không còn tồn tại.");
        Normalize(moi);
        var doMatMoi = store.GetDoMat(moi.DoMatId);
        var list = new List<ThayDoiTruong>();
        void Cmp(string label, string? a, string? b)
        {
            if ((a ?? "") != (b ?? "")) list.Add(new(label, a ?? "", b ?? ""));
        }
        Cmp("Số, ký hiệu", cu.SoKyHieu, moi.SoKyHieu);
        Cmp("Ngày tháng văn bản", TextUtil.FormatDate(cu.NgayVanBan), TextUtil.FormatDate(moi.NgayVanBan));
        Cmp("Tên loại", cu.TenLoai, moi.TenLoai);
        Cmp("Độ mật", cu.DoMatTen, doMatMoi?.Ten);
        if ((cu.TrichYeu ?? "") != (moi.TrichYeu ?? ""))
            list.Add(new("Trích yếu", "(nội dung cũ)", doMatMoi is { CamTrichYeu: true } ? "(xóa – độ mật cấm ghi trích yếu)" : "(nội dung mới)"));
        Cmp("Ghi chú", cu.GhiChu, moi.GhiChu);
        switch (cu, moi)
        {
            case (VanBanDi a, VanBanDi b):
                Cmp("Ngày đăng ký", TextUtil.FormatDate(a.NgayDangKy), TextUtil.FormatDate(b.NgayDangKy));
                Cmp("Người ký", a.NguoiKy, b.NguoiKy);
                Cmp("Đơn vị lưu", a.DonViLuu, b.DonViLuu);
                Cmp("Số lượng", a.SoLuong.ToString(), b.SoLuong.ToString());
                Cmp("Nơi nhận và ký nhận", TruongBieuMau.GiaTri(a, TruongBieuMau.NoiNhanKyNhan),
                    TruongBieuMau.GiaTri(b, TruongBieuMau.NoiNhanKyNhan));
                break;
            case (VanBanDen a, VanBanDen b):
                Cmp("Ngày đến", TextUtil.FormatDate(a.NgayDen), TextUtil.FormatDate(b.NgayDen));
                Cmp("Cơ quan ban hành", a.CoQuanBanHanh, b.CoQuanBanHanh);
                Cmp("Đơn vị nhận", a.DonViNhan, b.DonViNhan);
                Cmp("Ký nhận", KyNhan(a), KyNhan(b));
                break;
        }
        return list;
    }

    private static string KyNhan(VanBanDen d) =>
        d.DaKyNhan || d.NgayKyNhan != null ? $"Đã ký {d.NguoiKyNhan} {TextUtil.FormatDate(d.NgayKyNhan)}".Trim() : "Chưa ký";

    public void CapNhat(VanBanBase v)
    {
        var cu = Get(v.Loai, v.Id) ?? throw new BusinessException("Văn bản không còn tồn tại.");
        KiemTraSoKhoa(cu.SoDangKyId);
        if (cu.DaHuy) throw new BusinessException("Văn bản đã hủy, không sửa được. Khôi phục văn bản trước nếu cần sửa.");
        if (cu.PhienBan != v.PhienBan) throw new ConcurrencyException();
        var kt = KiemTra(v);
        if (!kt.KetQua.IsValid) throw new ValidationException(kt.KetQua);
        var changes = SoSanh(v);
        var doMat = store.GetDoMat(v.DoMatId)!;
        if (doMat.CamTrichYeu) v.TrichYeu = null;
        v.DoMatTen = doMat.Ten;
        v.DoMatKyHieu = doMat.KyHieu;
        // Giữ nguyên các trường định danh
        v.SoThuTu = cu.SoThuTu;
        v.Nam = cu.Nam;
        v.SoDangKyId = cu.SoDangKyId;
        if (v is VanBanDen den && cu is VanBanDen cuDen) den.SoDen = cuDen.SoDen;
        v.CapNhatLuc = clock.Now;
        v.CapNhatBoi = user.Name;
        GhiNhoDanhMuc(v);
        var json = JsonSerializer.Serialize(changes);
        var audit = new AuditEntry(v is VanBanDi ? "Sửa văn bản đi" : "Sửa văn bản đến",
            v is VanBanDi ? "van_ban_di" : "van_ban_den", v.Id,
            $"Sửa số {TextUtil.So2(cu.SoThuTu)}/{cu.Nam}: {string.Join(", ", changes.Select(c => c.Truong))}", json);
        switch (v)
        {
            case VanBanDi di: store.UpdateDi(di, audit); break;
            case VanBanDen d: store.UpdateDen(d, audit); break;
        }
    }

    public void Huy(LoaiSo loai, long id, string lyDo, int phienBan)
    {
        lyDo = TextUtil.Clean(lyDo) ?? "";
        if (lyDo.Length < 3) throw new BusinessException("Vui lòng nhập lý do hủy.", "LyDo");
        if (lyDo.Length > 500) throw new BusinessException("Lý do hủy quá dài (tối đa 500 ký tự).", "LyDo");
        var cu = Get(loai, id) ?? throw new BusinessException("Văn bản không còn tồn tại.");
        KiemTraSoKhoa(cu.SoDangKyId);
        if (cu.DaHuy) throw new BusinessException("Văn bản đã ở trạng thái hủy.");
        store.SetTrangThai(loai, id, TrangThaiBanGhi.DaHuy, lyDo, user.Name, clock.Now, phienBan,
            new AuditEntry(loai == LoaiSo.Di ? "Hủy văn bản đi" : "Hủy văn bản đến", BangCua(loai), id,
                $"Hủy số {TextUtil.So2(cu.SoThuTu)}/{cu.Nam}. Lý do: {lyDo}"));
    }

    public void KhoiPhuc(LoaiSo loai, long id, int phienBan)
    {
        var cu = Get(loai, id) ?? throw new BusinessException("Văn bản không còn tồn tại.");
        KiemTraSoKhoa(cu.SoDangKyId);
        if (!cu.DaHuy) return;
        store.SetTrangThai(loai, id, TrangThaiBanGhi.HieuLuc, null, user.Name, clock.Now, phienBan,
            new AuditEntry(loai == LoaiSo.Di ? "Khôi phục văn bản đi" : "Khôi phục văn bản đến", BangCua(loai), id,
                $"Khôi phục số {TextUtil.So2(cu.SoThuTu)}/{cu.Nam} (bỏ trạng thái hủy)"));
    }

    /// <summary>Xóa vật lý – chỉ dùng trong chế độ quản trị. Số đã cấp KHÔNG được cấp lại.</summary>
    public void XoaVinhVien(LoaiSo loai, long id, string lyDo)
    {
        lyDo = TextUtil.Clean(lyDo) ?? "";
        if (lyDo.Length < 3) throw new BusinessException("Vui lòng nhập lý do xóa.", "LyDo");
        var cu = Get(loai, id) ?? throw new BusinessException("Văn bản không còn tồn tại.");
        KiemTraSoKhoa(cu.SoDangKyId);
        if (!cu.DaHuy && !cu.LaDuLieuMau)
            throw new BusinessException("Chỉ được xóa vĩnh viễn văn bản đã hủy.");
        store.HardDelete(loai, id, new AuditEntry(loai == LoaiSo.Di ? "Xóa vĩnh viễn văn bản đi" : "Xóa vĩnh viễn văn bản đến",
            BangCua(loai), id, $"Xóa vĩnh viễn số {TextUtil.So2(cu.SoThuTu)}/{cu.Nam}, ký hiệu {cu.SoKyHieu}. Lý do: {lyDo}"));
    }

    public VanBanBase? Get(LoaiSo loai, long id) => loai == LoaiSo.Di ? store.GetDi(id) : store.GetDen(id);

    private void KiemTraSoKhoa(long soDangKyId)
    {
        var so = store.GetSoDangKy(soDangKyId);
        if (so is { DaKhoa: true })
            throw new BusinessException($"{so.TenHienThi}: sổ đã khóa. Mở khóa sổ (menu Danh mục → Sổ đăng ký) trước khi thay đổi.");
    }

    private static string BangCua(LoaiSo l) => l == LoaiSo.Di ? "van_ban_di" : "van_ban_den";

    /// <summary>Ghi nhớ giá trị vừa dùng: tự thêm cơ quan, nơi nhận, đơn vị nhận mới vào danh mục; tăng số lần dùng.</summary>
    private void GhiNhoDanhMuc(VanBanBase v)
    {
        void Remember(string nhom, string? ten)
        {
            ten = TextUtil.Clean(ten);
            if (ten != null) store.TouchDanhMuc(nhom, ten, v.LaDuLieuMau);
        }
        Remember(NhomDanhMuc.LoaiVanBan, v.TenLoai);
        switch (v)
        {
            case VanBanDi di:
                Remember(NhomDanhMuc.NguoiKy, di.NguoiKy);
                Remember(NhomDanhMuc.DonVi, di.DonViLuu);
                foreach (var n in di.NoiNhan) Remember(NhomDanhMuc.NoiNhan, n.NoiNhan);
                break;
            case VanBanDen den:
                Remember(NhomDanhMuc.CoQuanBanHanh, den.CoQuanBanHanh);
                Remember(NhomDanhMuc.DonVi, den.DonViNhan);
                break;
        }
    }

    public static string TrichYeuNhatKy => TrichYeuAn;
}

public static class BoDem
{
    public const string SoThuTu = "stt";
    public const string SoDen = "so_den";
}
