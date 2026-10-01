using Qlvb.Domain;

namespace Qlvb.Application;

public sealed class DanhMucService(IDataStore store)
{
    public IReadOnlyList<MucDanhMuc> List(string nhom, bool includeInactive = true, string? tuKhoa = null) =>
        store.ListDanhMuc(nhom, includeInactive, tuKhoa);

    public IReadOnlyList<string> GoiY(string nhom) => store.Suggestions(nhom);

    public IReadOnlyList<string> DangDung(string nhom) =>
        store.ListDanhMuc(nhom).Select(m => m.Ten).ToList();

    public MucDanhMuc Them(string nhom, string ten, string? phuDe = null)
    {
        Check(nhom);
        ten = TextUtil.Clean(ten) ?? throw new BusinessException("Tên không được để trống.", "Ten");
        if (ten.Length > Limits.TenMax) throw new BusinessException("Tên quá dài.", "Ten");
        var ton = store.FindDanhMuc(nhom, ten);
        if (ton != null)
        {
            if (!ton.DangDung)
                throw new BusinessException($"\"{ten}\" đã có trong danh mục nhưng đang ngừng sử dụng. Hãy bật lại mục đó.", "Ten");
            throw new BusinessException($"\"{ten}\" đã có trong danh mục.", "Ten");
        }
        var m = new MucDanhMuc { Nhom = nhom, Ten = ten, PhuDe = TextUtil.Clean(phuDe) };
        m.Id = store.AddDanhMuc(m, new AuditEntry("Thêm danh mục", "danh_muc", null,
            $"{NhomDanhMuc.TenNhom[nhom]}: {ten}{(m.PhuDe != null ? " (" + m.PhuDe + ")" : "")}"));
        return m;
    }

    public void Sua(MucDanhMuc m, string ten, string? phuDe)
    {
        ten = TextUtil.Clean(ten) ?? throw new BusinessException("Tên không được để trống.", "Ten");
        var trung = store.FindDanhMuc(m.Nhom, ten);
        if (trung != null && trung.Id != m.Id) throw new BusinessException($"\"{ten}\" đã có trong danh mục.", "Ten");
        var cu = $"{m.Ten}{(m.PhuDe != null ? " (" + m.PhuDe + ")" : "")}";
        m.Ten = ten;
        m.PhuDe = TextUtil.Clean(phuDe);
        store.UpdateDanhMuc(m, new AuditEntry("Sửa danh mục", "danh_muc", m.Id,
            $"{NhomDanhMuc.TenNhom[m.Nhom]}: \"{cu}\" → \"{ten}{(m.PhuDe != null ? " (" + m.PhuDe + ")" : "")}\". Văn bản đã đăng ký giữ nguyên tên cũ."));
    }

    public void DatTrangThai(MucDanhMuc m, bool dangDung)
    {
        if (m.DangDung == dangDung) return;
        m.DangDung = dangDung;
        store.UpdateDanhMuc(m, new AuditEntry(dangDung ? "Bật lại danh mục" : "Ngừng sử dụng danh mục", "danh_muc", m.Id,
            $"{NhomDanhMuc.TenNhom[m.Nhom]}: {m.Ten}"));
    }

    /// <summary>Chỉ xóa được mục chưa từng được văn bản nào sử dụng; mục đã dùng thì ngừng sử dụng.</summary>
    public void Xoa(MucDanhMuc m)
    {
        if (store.IsDanhMucUsed(m.Id))
            throw new BusinessException($"\"{m.Ten}\" đã được dùng trong văn bản nên không xóa được. Hãy chọn \"Ngừng sử dụng\".");
        store.DeleteDanhMuc(m.Id, new AuditEntry("Xóa danh mục", "danh_muc", m.Id, $"{NhomDanhMuc.TenNhom[m.Nhom]}: {m.Ten}"));
    }

    public void DoiThuTu(MucDanhMuc m, int thuTu)
    {
        m.ThuTu = thuTu;
        store.UpdateDanhMuc(m, new AuditEntry("Sắp xếp danh mục", "danh_muc", m.Id, $"{NhomDanhMuc.TenNhom[m.Nhom]}: {m.Ten}"));
    }

    // ---- Độ mật
    public IReadOnlyList<DoMat> ListDoMat(bool includeInactive = true) => store.ListDoMat(includeInactive);

    public void LuuDoMat(DoMat d)
    {
        var ten = TextUtil.Clean(d.Ten)?.ToUpperInvariant() ?? throw new BusinessException("Tên độ mật không được để trống.");
        var kh = TextUtil.Clean(d.KyHieu)?.ToUpperInvariant() ?? "";
        if (kh.Length > 3) throw new BusinessException("Ký hiệu độ mật tối đa 3 ký tự.");
        if (store.ListDoMat(true).Any(x => x.Id != d.Id && (TextUtil.SearchKey(x.Ten) == TextUtil.SearchKey(ten) || (kh.Length > 0 && x.KyHieu == kh))))
            throw new BusinessException("Tên hoặc ký hiệu độ mật bị trùng.");
        if (d.Id != 0)
        {
            var cu = store.GetDoMat(d.Id) ?? throw new BusinessException("Độ mật không còn tồn tại.");
            if (d.CamTrichYeu && !cu.CamTrichYeu && store.CountDocumentsWithTrichYeu(d.Id) > 0)
                throw new BusinessException("Đã có văn bản thuộc độ mật này ghi trích yếu. Không thể bật \"Cấm ghi trích yếu\" khi chưa xử lý các văn bản đó.");
        }
        store.SaveDoMat(d with { Ten = ten, KyHieu = kh }, new AuditEntry(d.Id == 0 ? "Thêm độ mật" : "Sửa độ mật", "do_mat", d.Id == 0 ? null : d.Id,
            $"{ten} ({kh}), mức {d.Muc}, cấm trích yếu: {(d.CamTrichYeu ? "có" : "không")}, đang dùng: {(d.DangDung ? "có" : "không")}"));
    }

    private static void Check(string nhom)
    {
        if (!NhomDanhMuc.TenNhom.ContainsKey(nhom)) throw new ArgumentException(nhom);
    }
}

public sealed class SoDangKyService(IDataStore store, IClock clock)
{
    public IReadOnlyList<SoDangKy> List(LoaiSo? loai = null) => store.ListSoDangKy(loai);

    /// <summary>Mở quyển sổ mới. Nếu là quyển đầu tiên của năm có thể đặt số bắt đầu (khi chuyển từ sổ giấy sang).</summary>
    public SoDangKy MoSo(LoaiSo loai, int nam, int soBatDau = 1)
    {
        if (nam < Limits.NamMin || nam > clock.Today.Year + 1)
            throw new BusinessException("Năm không hợp lệ.");
        if (soBatDau < 1 || soBatDau > 1_000_000) throw new BusinessException("Số bắt đầu không hợp lệ.");
        var hienTai = store.CurrentSoDangKy(loai, nam);
        if (hienTai != null && !hienTai.DaKhoa)
            throw new BusinessException($"{hienTai.TenHienThi} đang mở. Khóa quyển này trước khi mở quyển mới.");
        if (hienTai != null && soBatDau != 1)
            throw new BusinessException("Chỉ quyển đầu tiên của năm được đặt số bắt đầu; các quyển sau đánh số tiếp.");
        var bm = ChonBieuMau(store.ListBieuMau(), loai, nam);
        var so = new SoDangKy
        {
            Loai = loai,
            Nam = nam,
            QuyenSo = (hienTai?.QuyenSo ?? 0) + 1,
            TenCoQuan = store.GetConfig(ConfigKeys.TenCoQuan) ?? "",
            CoQuanChuQuan = store.GetConfig(ConfigKeys.CoQuanChuQuan),
            MaBieuMau = bm.Ma,
            NgayMo = clock.Today,
        };
        so.Id = store.AddSoDangKy(so, soBatDau, new AuditEntry("Mở sổ", "so_dang_ky", null,
            $"{so.TenHienThi}{(soBatDau > 1 ? $", số bắt đầu {soBatDau}" : "")}, biểu mẫu {bm.Ma}"));
        return so;
    }

    /// <summary>Biểu mẫu áp dụng cho sổ năm <paramref name="nam"/>: phiên bản có hiệu lực mới nhất (không lấy mẫu chưa có hiệu lực trong năm).</summary>
    public static BieuMau ChonBieuMau(IEnumerable<BieuMau> ds, LoaiSo loai, int nam) =>
        ds.Where(b => b.Loai == loai && b.HieuLucTu <= new DateOnly(nam, 12, 31))
          .OrderByDescending(b => b.HieuLucTu).ThenByDescending(b => b.PhienBan).FirstOrDefault()
        ?? ds.Where(b => b.Loai == loai).OrderBy(b => b.HieuLucTu).FirstOrDefault()
        ?? throw new BusinessException("Chưa có biểu mẫu sổ phù hợp.");

    public void Khoa(SoDangKy so) =>
        store.SetSoDangKyLocked(so.Id, true, new AuditEntry("Khóa sổ", "so_dang_ky", so.Id, so.TenHienThi));

    public void MoKhoa(SoDangKy so, string lyDo)
    {
        lyDo = TextUtil.Clean(lyDo) ?? "";
        if (lyDo.Length < 3) throw new BusinessException("Vui lòng nhập lý do mở khóa sổ.");
        store.SetSoDangKyLocked(so.Id, false, new AuditEntry("Mở khóa sổ", "so_dang_ky", so.Id, $"{so.TenHienThi}. Lý do: {lyDo}"));
    }
}

public sealed class CauHinhService(IDataStore store)
{
    public string Get(string key, string macDinh = "") => store.GetConfig(key) ?? macDinh;

    public int GetInt(string key, int macDinh) =>
        int.TryParse(store.GetConfig(key), out var v) ? v : macDinh;

    public bool GetBool(string key, bool macDinh = false) =>
        store.GetConfig(key) is { } v ? v == "1" : macDinh;

    public void Set(string key, string? value)
    {
        value = TextUtil.Clean(value);
        var cu = store.GetConfig(key);
        if ((cu ?? "") == (value ?? "")) return;
        var ten = ConfigKeys.TenHienThi.TryGetValue(key, out var t) ? t : key;
        store.SetConfig(key, value, new AuditEntry("Thay đổi cấu hình", "cau_hinh", null, $"{ten}: \"{cu}\" → \"{value}\""));
    }

    public void SetBool(string key, bool value) => Set(key, value ? "1" : "0");
    public void SetInt(string key, int value) => Set(key, value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public bool ChoPhepXuatMat => GetBool(ConfigKeys.ChoPhepXuatMat, false);
    public bool ChanChupManHinh => GetBool(ConfigKeys.ChanChupManHinh, false);
}

public sealed class TraCuuService(IDataStore store)
{
    public PagedResult<VanBanBase> Tim(SearchCriteria c) => store.Search(c);
    public IReadOnlyList<VanBanBase> GanDay(int n = 10) => store.Recent(n);
    public IReadOnlyList<int> CacNam(LoaiSo? loai = null) => store.Years(loai);
    public IReadOnlyList<DemTheoNhom> ThongKe(TieuChiThongKe t, ThongKeFilter f) => store.ThongKe(t, f);
}
