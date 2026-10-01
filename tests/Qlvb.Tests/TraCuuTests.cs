using Qlvb.Application;
using Qlvb.Domain;

namespace Qlvb.Tests;

public class TraCuuTests
{
    private static TestEnv Seed()
    {
        var env = new TestEnv();
        env.VanBan.ThemMoi(env.NewDi(trichYeu: "Kế hoạch huấn luyện năm", soKyHieu: "01/KH-TN"));
        var b = env.NewDi(doMat: "B", trichYeu: "Báo cáo tổng kết", soKyHieu: "02/BC-TN");
        b.NoiNhan = [new NoiNhanKyNhan { NoiNhan = "Sở Tài chính" }];
        env.VanBan.ThemMoi(b);
        env.VanBan.ThemMoi(env.NewDi(doMat: "A", trichYeu: null, soKyHieu: "03/TN"));
        var c = env.NewDi(trichYeu: "Văn bản sẽ hủy", soKyHieu: "04/TN");
        env.VanBan.ThemMoi(c);
        env.VanBan.Huy(LoaiSo.Di, c.Id, "Nhầm", c.PhienBan);
        env.VanBan.ThemMoi(env.NewDen());
        return env;
    }

    [Fact]
    public void QuickSearch_IgnoresDiacritics_AndCase()
    {
        using var env = Seed();
        var r = env.Store.Search(new SearchCriteria { Loai = LoaiSo.Di, TuKhoa = "KE HOACH huan" });
        Assert.Equal(1, r.Total);
        Assert.Equal("01/KH-TN", r.Items[0].SoKyHieu);
    }

    [Fact]
    public void Search_ByNoiNhan_AndDoMat_AndStatus()
    {
        using var env = Seed();
        Assert.Equal(1, env.Store.Search(new SearchCriteria { Loai = LoaiSo.Di, NoiNhan = "tai chinh" }).Total);
        Assert.Equal(1, env.Store.Search(new SearchCriteria { Loai = LoaiSo.Di, DoMatId = env.DoMat("A").Id }).Total);
        Assert.Equal(3, env.Store.Search(new SearchCriteria { Loai = LoaiSo.Di, TrangThai = LocTrangThai.HieuLuc }).Total);
        Assert.Equal(1, env.Store.Search(new SearchCriteria { Loai = LoaiSo.Di, TrangThai = LocTrangThai.DaHuy }).Total);
    }

    [Fact]
    public void Search_LikeWildcards_AreEscaped()
    {
        using var env = Seed();
        Assert.Equal(0, env.Store.Search(new SearchCriteria { Loai = LoaiSo.Di, TuKhoa = "%" }).Total);
        Assert.Equal(0, env.Store.Search(new SearchCriteria { Loai = LoaiSo.Di, SoKyHieu = "_" }).Total);
    }

    [Fact]
    public void Paging_AndSorting()
    {
        using var env = Seed();
        var p1 = env.Store.Search(new SearchCriteria { Loai = LoaiSo.Di, Limit = 2, Offset = 0, SapXep = "so_thu_tu", Giam = false });
        var p2 = env.Store.Search(new SearchCriteria { Loai = LoaiSo.Di, Limit = 2, Offset = 2, SapXep = "so_thu_tu", Giam = false });
        Assert.Equal(4, p1.Total);
        Assert.Equal([1, 2], p1.Items.Select(x => x.SoThuTu));
        Assert.Equal([3, 4], p2.Items.Select(x => x.SoThuTu));
        var all = env.Store.Search(new SearchCriteria { Loai = LoaiSo.Di, Limit = 0, SapXep = "khong_hop_le; DROP TABLE x" });
        Assert.Equal(4, all.Items.Count);
    }

    [Fact]
    public void TuyetMat_NotFoundByTrichYeuSearch()
    {
        using var env = new TestEnv();
        env.VanBan.ThemMoi(env.NewDi(doMat: "A", trichYeu: null, soKyHieu: "10/TN"));
        Assert.Equal(0, env.Store.Search(new SearchCriteria { Loai = LoaiSo.Di, TrichYeu = "a" }).Total);
    }

    [Fact]
    public void Statistics_ByDoMat_AndMonth()
    {
        using var env = Seed();
        var byDm = env.Store.ThongKe(TieuChiThongKe.DoMat, new ThongKeFilter { Loai = LoaiSo.Di, Nam = 2026 });
        Assert.Equal(3, byDm.Sum(x => x.SoLuong));
        var all = env.Store.ThongKe(TieuChiThongKe.Thang, new ThongKeFilter { Nam = 2026 });
        Assert.Equal(4, all.Sum(x => x.SoLuong));
        Assert.Equal("Tháng 10/2026", all.Single().Nhom);
        var withCancelled = env.Store.ThongKe(TieuChiThongKe.Nam, new ThongKeFilter { BaoGomDaHuy = true });
        Assert.Equal(5, withCancelled.Sum(x => x.SoLuong));
    }

    [Fact]
    public void Recent_AndYears()
    {
        using var env = Seed();
        Assert.Equal(4, env.Store.Recent(10).Count);
        Assert.Contains(2026, env.Store.Years());
    }
}
