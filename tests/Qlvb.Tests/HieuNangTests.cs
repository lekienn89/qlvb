using System.Diagnostics;
using Qlvb.Application;
using Qlvb.Domain;
using Qlvb.Infrastructure.Services;
using Xunit.Abstractions;

namespace Qlvb.Tests;

/// <summary>
/// Hiệu năng với khối lượng lớn (mặc định 50.000 văn bản giả lập, 3 năm). Chỉ chạy khi đặt biến môi trường QLVB_PERF=1
/// (hoặc QLVB_PERF=&lt;số văn bản&gt;) vì nạp dữ liệu mất vài phút.
/// </summary>
public class HieuNangTests(ITestOutputHelper output)
{
    private static int SoVanBan =>
        Environment.GetEnvironmentVariable("QLVB_PERF") is { Length: > 0 } v ? (int.TryParse(v, out var n) && n > 1 ? n : 50_000) : 0;

    [Fact]
    public void LargeDataset_Operations_StayResponsive()
    {
        var tong = SoVanBan;
        if (tong == 0)
        {
            output.WriteLine("Bỏ qua: đặt QLVB_PERF=1 để chạy kiểm thử hiệu năng.");
            return;
        }
        using var env = new TestEnv();
        var rnd = new Random(2026);
        var coQuan = Enumerable.Range(1, 200).Select(i => $"Cơ quan giả lập số {i}").ToArray();
        var noiNhan = Enumerable.Range(1, 60).Select(i => $"Phòng giả lập {i}").ToArray();
        var loai = new[] { "Công văn", "Quyết định", "Báo cáo", "Kế hoạch", "Tờ trình", "Thông báo" };
        foreach (var l in loai) env.EnsureDm(NhomDanhMuc.LoaiVanBan, l);
        var nguoiKy = Enumerable.Range(1, 15).Select(i => env.EnsureDm(NhomDanhMuc.NguoiKy, $"Người ký giả lập {i}")).ToArray();
        var donVi = Enumerable.Range(1, 10).Select(i => env.EnsureDm(NhomDanhMuc.DonVi, $"Đơn vị giả lập {i}")).ToArray();
        var doMat = env.Store.ListDoMat().ToArray();
        string Tu() => string.Join(' ', Enumerable.Range(0, 12).Select(_ => TuMau[rnd.Next(TuMau.Length)]));

        var conn = env.Session.Db!.Connection;
        void Pragma(string sql) { lock (env.Session.Db!.Gate) { using var c = conn.CreateCommand(); c.CommandText = sql; c.ExecuteNonQuery(); } }
        // Nạp nhanh: tắt fsync trong lúc nạp (chỉ ảnh hưởng tốc độ nạp, không ảnh hưởng phép đo phía sau).
        Pragma("PRAGMA synchronous=OFF");
        var sw = Stopwatch.StartNew();
        var start = new DateTime(2024, 1, 2, 8, 0, 0);
        var buocPhut = (int)((new DateTime(2026, 9, 30) - start).TotalMinutes / (tong / 2));
        for (var i = 0; i < tong / 2; i++)
        {
            env.Clock.Now = start.AddMinutes((double)i * buocPhut);
            var dm = doMat[rnd.Next(doMat.Length)];
            var di = env.VanBan.TaoMoiDi();
            di.TenLoai = loai[rnd.Next(loai.Length)];
            di.DoMatId = dm.Id;
            di.TrichYeu = dm.CamTrichYeu ? null : Tu();
            di.NguoiKy = nguoiKy[rnd.Next(nguoiKy.Length)];
            di.DonViLuu = donVi[rnd.Next(donVi.Length)];
            di.SoKyHieu = $"{di.SoThuTu}/GL-{i}";
            var nn = rnd.Next(noiNhan.Length);
            di.NoiNhan = [new NoiNhanKyNhan { NoiNhan = noiNhan[nn] }, new NoiNhanKyNhan { NoiNhan = noiNhan[(nn + 1 + rnd.Next(noiNhan.Length - 1)) % noiNhan.Length] }];
            env.VanBan.ThemMoi(di);

            var dm2 = doMat[rnd.Next(doMat.Length)];
            var den = env.VanBan.TaoMoiDen();
            den.CoQuanBanHanh = coQuan[rnd.Next(coQuan.Length)];
            den.SoKyHieu = $"{rnd.Next(1, 999)}/GL-{i}";
            den.NgayVanBan = env.Clock.Today.AddDays(-rnd.Next(0, 5));
            den.TenLoai = loai[rnd.Next(loai.Length)];
            den.DoMatId = dm2.Id;
            den.TrichYeu = dm2.CamTrichYeu ? null : Tu();
            den.DonViNhan = donVi[rnd.Next(donVi.Length)];
            env.VanBan.ThemMoi(den);
        }
        Pragma("PRAGMA synchronous=FULL");
        output.WriteLine($"Nạp {tong:N0} văn bản: {sw.Elapsed.TotalSeconds:N1} s");
        env.Clock.Now = new DateTime(2026, 10, 1, 9, 0, 0);

        var results = new List<(string Ten, double Ms, double Max)>();
        T Do<T>(string ten, double maxMs, Func<T> f)
        {
            f(); // làm nóng bộ nhớ đệm
            var s = Stopwatch.StartNew();
            var r = f();
            results.Add((ten, s.Elapsed.TotalMilliseconds, maxMs));
            return r;
        }

        var page = Do("Danh sách sổ đi năm 2026, trang 1 (50 dòng)", 1000, () => env.Store.Search(new SearchCriteria { Loai = LoaiSo.Di, Nam = 2026, Limit = 50 }));
        Assert.Equal(50, page.Items.Count);
        Do("Trang cuối, sắp xếp theo số ký hiệu", 1500, () => env.Store.Search(new SearchCriteria { Loai = LoaiSo.Di, Nam = 2026, Limit = 50, Offset = Math.Max(0, page.Total - 50), SapXep = "so_ky_hieu" }));
        var q = Do("Tìm nhanh không dấu toàn bộ lịch sử", 2000, () => env.Store.Search(new SearchCriteria { Loai = LoaiSo.Den, TuKhoa = "co quan gia lap so 17", Limit = 50 }));
        Assert.True(q.Total > 0);
        Do("Tìm nâng cao nhiều điều kiện", 2000, () => env.Store.Search(new SearchCriteria
        {
            Loai = LoaiSo.Den, Nam = 2025, CoQuanBanHanh = "giả lập số 3", DoMatId = doMat[0].Id, TrichYeu = "kế hoạch", Limit = 50,
        }));
        Do("Tất cả văn bản đến một năm (không phân trang)", 4000, () => env.Store.Search(new SearchCriteria { Loai = LoaiSo.Den, Nam = 2025, Limit = 0 }));
        Do("Thống kê theo tháng", 2000, () => env.Store.ThongKe(TieuChiThongKe.Thang, new ThongKeFilter { Nam = 2025 }));
        Do("Thống kê theo cơ quan ban hành", 2000, () => env.Store.ThongKe(TieuChiThongKe.CoQuanBanHanh, new ThongKeFilter()));
        Do("Văn bản gần đây (trang chủ)", 500, () => env.Store.Recent(10));
        Do("Danh sách sổ đăng ký kèm thống kê", 1500, () => env.Store.ListSoDangKy());
        Do("Kiểm tra trùng trước khi lưu", 500, () => env.Store.FindDuplicates(env.NewDen()));
        Do("Gợi ý cơ quan ban hành", 500, () => env.Store.Suggestions(NhomDanhMuc.CoQuanBanHanh));
        Do("Lưu một văn bản mới", 1000, () => env.VanBan.ThemMoi(env.NewDi()));
        Do("Kiểm tra chuỗi nhật ký", 15000, () => env.Store.VerifyAuditChain() ?? "ok");
        Do("Sao lưu nhanh", 30000, () => env.Backup.Create(BackupKind.Nhanh));
        Do("Đóng và đăng nhập lại", 10000, () => { env.Reopen(); return 0; });

        foreach (var (ten, ms, max) in results) output.WriteLine($"{ms,10:N0} ms  (giới hạn {max:N0})  {ten}");
        output.WriteLine($"Kích thước CSDL: {new FileInfo(env.Session.Paths.DatabaseFile).Length / 1048576.0:N1} MB");
        Assert.All(results, r => Assert.True(r.Ms <= r.Max, $"{r.Ten}: {r.Ms:N0} ms > {r.Max:N0} ms"));
    }

    private static readonly string[] TuMau =
    [
        "báo", "cáo", "kế", "hoạch", "triển", "khai", "thực", "hiện", "nhiệm", "vụ", "năm", "quý", "công", "tác", "bảo", "vệ",
        "tổng", "kết", "đánh", "giá", "kiểm", "tra", "hướng", "dẫn", "về", "việc", "tăng", "cường", "phối", "hợp", "giả", "lập",
    ];
}
