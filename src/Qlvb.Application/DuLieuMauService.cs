using Qlvb.Domain;

namespace Qlvb.Application;

/// <summary>
/// Dữ liệu mẫu để làm quen phần mềm. Toàn bộ là dữ liệu GIẢ (tên, số hiệu, nội dung bịa đặt), được đánh dấu
/// "dữ liệu mẫu" và xóa được bằng một nút. Chỉ nạp khi chưa có văn bản thật.
/// </summary>
public sealed class DuLieuMauService(IDataStore store, VanBanService vanBan, IClock clock)
{
    private static readonly string[] CoQuan = ["Sở Mẫu Thử A", "Ban Ví Dụ B", "Cục Thử Nghiệm C", "Văn phòng Mẫu D", "Viện Giả Định E"];
    private static readonly string[] NoiNhan = ["Phòng Mẫu 1", "Phòng Mẫu 2", "Đơn vị Thử 3", "Chi nhánh Ví dụ 4", "Ban Giả định 5"];
    private static readonly (string Ten, string ChucVu)[] NguoiKy = [("Nguyễn Văn Mẫu", "Giám đốc"), ("Trần Thị Thử", "Phó Giám đốc"), ("Lê Văn Ví Dụ", "Chánh Văn phòng")];
    private static readonly string[] DonVi = ["Văn phòng (mẫu)", "Phòng Tổng hợp (mẫu)", "Phòng Nghiệp vụ (mẫu)"];
    private static readonly string[] Loai = ["Công văn", "Báo cáo", "Kế hoạch", "Quyết định", "Thông báo", "Tờ trình"];
    private static readonly string[] NoiDung =
    [
        "V/v kiểm tra dữ liệu thử nghiệm phần mềm", "Kết quả diễn tập giả định quý", "Phương án mẫu phục vụ tập huấn",
        "Nội dung minh họa dùng cho hướng dẫn sử dụng", "Tổng hợp số liệu giả định", "Đề xuất mẫu để kiểm thử chức năng in sổ",
    ];

    public bool CoTheNap() =>
        store.Search(new SearchCriteria { Loai = LoaiSo.Di, Limit = 1 }).Total == 0
        && store.Search(new SearchCriteria { Loai = LoaiSo.Den, Limit = 1 }).Total == 0;

    public bool DangCoDuLieuMau => store.GetConfig(ConfigKeys.DaCoDuLieuMau) == "1";

    /// <returns>Số văn bản mẫu đã tạo.</returns>
    public int Nap(int soMoiLoai = 30, int seed = 2026)
    {
        if (!CoTheNap()) throw new BusinessException("Chỉ nạp dữ liệu mẫu khi chưa có văn bản nào.");
        var rnd = new Random(seed);
        foreach (var (ten, cv) in NguoiKy) Them(NhomDanhMuc.NguoiKy, ten, cv);
        foreach (var d in DonVi) Them(NhomDanhMuc.DonVi, d, null);
        var doMat = store.ListDoMat();
        var today = clock.Today;
        var start = new DateOnly(today.Year, 1, 2);
        var span = Math.Max(1, today.DayNumber - start.DayNumber);
        var n = 0;
        foreach (var ngay in Enumerable.Range(0, soMoiLoai).Select(_ => start.AddDays(rnd.Next(span + 1))).Order())
        {
            var di = vanBan.TaoMoiDi();
            var dm = doMat[rnd.Next(doMat.Count)];
            di.LaDuLieuMau = true;
            di.NgayDangKy = ngay;
            di.NgayVanBan = ngay;
            di.TenLoai = Loai[rnd.Next(Loai.Length)];
            di.DoMatId = dm.Id;
            di.TrichYeu = dm.CamTrichYeu ? null : "[MẪU] " + NoiDung[rnd.Next(NoiDung.Length)];
            di.NguoiKy = NguoiKy[rnd.Next(NguoiKy.Length)].Ten;
            di.DonViLuu = DonVi[rnd.Next(DonVi.Length)];
            di.SoLuong = rnd.Next(1, 6);
            di.SoThuTu = vanBan.SoDuKien(LoaiSo.Di, ngay.Year, BoDem.SoThuTu);
            di.SoKyHieu = vanBan.GoiYSoKyHieu(di.SoThuTu, di.TenLoai, ngay.Year) + "-MAU";
            di.NoiNhan = NoiNhan.OrderBy(_ => rnd.Next()).Take(rnd.Next(1, 4))
                .Select((x, i) => new NoiNhanKyNhan { ThuTu = i + 1, NoiNhan = x }).ToList();
            vanBan.ThemMoi(di);
            n++;
        }
        foreach (var ngay in Enumerable.Range(0, soMoiLoai).Select(_ => start.AddDays(rnd.Next(span + 1))).Order())
        {
            var den = vanBan.TaoMoiDen();
            var dm = doMat[rnd.Next(doMat.Count)];
            den.LaDuLieuMau = true;
            den.NgayDen = ngay;
            den.NgayDangKy = ngay;
            den.NgayVanBan = ngay.AddDays(-rnd.Next(0, 5));
            den.CoQuanBanHanh = CoQuan[rnd.Next(CoQuan.Length)];
            den.SoKyHieu = $"{rnd.Next(1, 300)}/MAU-{rnd.Next(1, 9)}";
            den.TenLoai = Loai[rnd.Next(Loai.Length)];
            den.DoMatId = dm.Id;
            den.TrichYeu = dm.CamTrichYeu ? null : "[MẪU] " + NoiDung[rnd.Next(NoiDung.Length)];
            den.DonViNhan = DonVi[rnd.Next(DonVi.Length)];
            vanBan.ThemMoi(den);
            n++;
        }
        store.SetConfig(ConfigKeys.DaCoDuLieuMau, "1", new AuditEntry("Nạp dữ liệu mẫu", "he_thong", null, $"Tạo {n} văn bản mẫu (dữ liệu giả)"));
        return n;
    }

    public int Xoa() => store.DeleteSampleData(new AuditEntry("Xóa dữ liệu mẫu", "he_thong", null, "Xóa toàn bộ dữ liệu mẫu"));

    private void Them(string nhom, string ten, string? phuDe)
    {
        if (store.FindDanhMuc(nhom, ten) != null) return;
        store.AddDanhMuc(new MucDanhMuc { Nhom = nhom, Ten = ten, PhuDe = phuDe, LaDuLieuMau = true },
            new AuditEntry("Thêm danh mục", "danh_muc", null, $"{NhomDanhMuc.TenNhom[nhom]}: {ten} (dữ liệu mẫu)"));
    }
}
