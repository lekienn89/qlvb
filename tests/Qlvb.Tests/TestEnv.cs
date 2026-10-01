using Qlvb.Application;
using Qlvb.Domain;
using Qlvb.Infrastructure;
using Qlvb.Infrastructure.Services;

namespace Qlvb.Tests;

public sealed class FakeClock(DateTime now) : IClock
{
    public DateTime Now { get; set; } = now;
    public DateOnly Today => DateOnly.FromDateTime(Now);
}

public sealed class FakeUser : ICurrentUser
{
    public string Name => "kiemthu";
}

/// <summary>Môi trường kiểm thử: thư mục dữ liệu riêng, mật khẩu cố định, KDF ít vòng lặp cho nhanh.</summary>
public sealed class TestEnv : IDisposable
{
    public const string Password = "MatKhau@2026";
    public string Dir { get; }
    public FakeClock Clock { get; } = new(new DateTime(2026, 10, 1, 9, 0, 0));
    public AppSession Session { get; private set; }
    public string RecoveryCode { get; }

    public TestEnv(bool setup = true)
    {
        Dir = Path.Combine(Path.GetTempPath(), "qlvb-test-" + Guid.NewGuid().ToString("N"));
        Session = NewSession();
        RecoveryCode = setup ? Session.Setup(Password, "Cơ quan Thử nghiệm") : "";
        if (setup) Store.SetConfig(ConfigKeys.KyHieuCoQuan, "TN");
    }

    public AppSession NewSession() => new(new AppPaths(Dir, true), Clock, new FakeUser(), kdfIterations: 1000);

    public void Reopen()
    {
        Session.Dispose();
        Session = NewSession();
        Session.Login(Password);
    }

    public IDataStore Store => Session.RequireStore();
    public VanBanService VanBan => new(Store, Clock, new FakeUser());
    public DanhMucService DanhMuc => new(Store);
    public SoDangKyService So => new(Store, Clock);
    public BackupService Backup => new(Session);

    public DoMat DoMat(string kyHieu) => Store.ListDoMat(true).Single(d => d.KyHieu == kyHieu);

    public VanBanDi NewDi(string doMat = "C", string? trichYeu = "Nội dung thử nghiệm", string soKyHieu = "")
    {
        var v = VanBan.TaoMoiDi();
        v.TenLoai = "Công văn";
        v.DoMatId = DoMat(doMat).Id;
        v.TrichYeu = trichYeu;
        v.NguoiKy = EnsureDm(NhomDanhMuc.NguoiKy, "Nguyễn Văn A");
        v.DonViLuu = EnsureDm(NhomDanhMuc.DonVi, "Văn phòng");
        v.SoKyHieu = soKyHieu.Length > 0 ? soKyHieu : VanBan.GoiYSoKyHieu(v.SoThuTu, v.TenLoai, v.Nam);
        v.NoiNhan = [new NoiNhanKyNhan { NoiNhan = "Phòng Kế hoạch" }];
        return v;
    }

    public VanBanDen NewDen(string doMat = "C", string? trichYeu = "Nội dung đến", string soKyHieu = "12/QĐ-ABC")
    {
        var v = VanBan.TaoMoiDen();
        v.CoQuanBanHanh = "Bộ Thử nghiệm";
        v.SoKyHieu = soKyHieu;
        v.NgayVanBan = Clock.Today.AddDays(-2);
        v.TenLoai = "Quyết định";
        v.DoMatId = DoMat(doMat).Id;
        v.TrichYeu = trichYeu;
        v.DonViNhan = "Văn phòng";
        return v;
    }

    public string EnsureDm(string nhom, string ten)
    {
        if (Store.FindDanhMuc(nhom, ten) == null) DanhMuc.Them(nhom, ten);
        return ten;
    }

    public void Dispose()
    {
        Session.Dispose();
        try { Directory.Delete(Dir, true); } catch (IOException) { }
    }
}
