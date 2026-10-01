using Qlvb.Application;
using Qlvb.Domain;
using Qlvb.Infrastructure.Data;
using Qlvb.Infrastructure.Security;
using Qlvb.Infrastructure.Services;

namespace Qlvb.Tests;

public class HeThongTests
{
    [Fact]
    public void AuditChain_DetectsTampering()
    {
        using var env = new TestEnv();
        env.VanBan.ThemMoi(env.NewDi());
        Assert.Null(env.Store.VerifyAuditChain());
        // Giả lập sửa trực tiếp (bỏ trigger) để kiểm tra phát hiện.
        var c = env.Session.Db!.Connection;
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "DROP TRIGGER tg_nhat_ky_no_update; UPDATE nhat_ky SET mo_ta='đã sửa' WHERE id=2;";
            cmd.ExecuteNonQuery();
        }
        Assert.NotNull(env.Store.VerifyAuditChain());
    }

    [Fact]
    public void AuditLog_IsAppendOnly()
    {
        using var env = new TestEnv();
        var c = env.Session.Db!.Connection;
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM nhat_ky";
        Assert.ThrowsAny<Microsoft.Data.Sqlite.SqliteException>(() => cmd.ExecuteNonQuery());
    }

    [Fact]
    public void Backup_And_Restore_RoundTrip()
    {
        using var env = new TestEnv();
        var v = env.NewDi(soKyHieu: "01/SAOLUU");
        env.VanBan.ThemMoi(v);
        var file = env.Backup.Create(BackupKind.Nhanh);
        Assert.True(File.Exists(file));
        var m = BackupService.Inspect(file);
        Assert.Equal(Migrator.LatestVersion, m.SchemaVersion);

        env.VanBan.ThemMoi(env.NewDi(soKyHieu: "02/SAUSAOLUU"));
        Assert.Equal(2, env.Store.Search(new SearchCriteria { Loai = LoaiSo.Di }).Total);

        env.Backup.Restore(file, TestEnv.Password);
        Assert.Equal(1, env.Store.Search(new SearchCriteria { Loai = LoaiSo.Di }).Total);
        Assert.Contains(env.Store.ListAudit(new()), a => a.HanhDong == "Khôi phục dữ liệu");
        // Có bản an toàn trước khi khôi phục
        Assert.Contains(Directory.GetFiles(env.Session.Paths.Backup), f => f.Contains("truockhoiphuc"));
        Assert.Null(env.Store.VerifyAuditChain());
    }

    [Fact]
    public void Restore_WrongPassword_KeepsCurrentData()
    {
        using var env = new TestEnv();
        env.VanBan.ThemMoi(env.NewDi());
        var file = env.Backup.Create(BackupKind.Nhanh);
        env.VanBan.ThemMoi(env.NewDi());
        Assert.Throws<AuthException>(() => env.Backup.Restore(file, "SaiMatKhau9"));
        Assert.True(env.Session.LoggedIn);
        Assert.Equal(2, env.Store.Search(new SearchCriteria { Loai = LoaiSo.Di }).Total);
    }

    [Fact]
    public void Restore_CorruptedBackup_Rejected()
    {
        using var env = new TestEnv();
        env.VanBan.ThemMoi(env.NewDi());
        var file = env.Backup.Create(BackupKind.ThuCong, Path.Combine(env.Dir, "ngoai", "sl"));
        Assert.EndsWith(".qlvbak", file);
        var bytes = File.ReadAllBytes(file);
        bytes[bytes.Length / 3] ^= 0x55;
        File.WriteAllBytes(file, bytes);
        Assert.Throws<InvalidDataException>(() => env.Backup.Restore(file, TestEnv.Password));
        Assert.True(env.Session.LoggedIn);
    }

    [Fact]
    public void Restore_WhenNotLoggedIn_AfterKeyLoss()
    {
        using var env = new TestEnv();
        env.VanBan.ThemMoi(env.NewDi());
        var file = env.Backup.Create(BackupKind.Nhanh);
        env.Session.Logout();
        File.Delete(env.Session.Paths.KeyFile);
        Assert.Equal(TrangThaiDuLieu.ThieuTep, env.Session.TrangThai);
        new BackupService(env.Session).Restore(file, TestEnv.Password);
        Assert.True(env.Session.LoggedIn);
        Assert.Equal(1, env.Store.Search(new SearchCriteria { Loai = LoaiSo.Di }).Total);
    }

    [Fact]
    public void Backup_PrunesOldQuickBackups()
    {
        using var env = new TestEnv();
        env.Store.SetConfig(ConfigKeys.SoBanSaoLuuGiuLai, "2");
        for (var i = 0; i < 4; i++)
        {
            env.Clock.Now = env.Clock.Now.AddMinutes(1);
            env.Backup.Create(BackupKind.Nhanh);
        }
        Assert.Equal(2, Directory.GetFiles(env.Session.Paths.Backup, "*_nhanh.qlvbak").Length);
    }

    [Fact]
    public void Migrations_AreIdempotent_AndVersioned()
    {
        using var env = new TestEnv();
        Assert.Equal(Migrator.LatestVersion, Migrator.CurrentVersion(env.Session.Db!));
        Assert.Equal(0, Migrator.Migrate(env.Session.Db!, () => env.Clock.Now));
        Assert.Equal(2, env.Store.ListBieuMau().Count);
    }

    [Fact]
    public void FailedMigration_RollsBack()
    {
        using var env = new TestEnv();
        var bad = Migrator.Embedded().Append(new Migration(Migrator.LatestVersion + 1, "hong", "CREATE TABLE x(a); SELECT * FROM khong_ton_tai;")).ToList();
        Assert.Throws<DatabaseOpenException>(() => Migrator.Migrate(env.Session.Db!, () => env.Clock.Now, null, bad));
        Assert.Equal(Migrator.LatestVersion, Migrator.CurrentVersion(env.Session.Db!));
        using var cmd = env.Session.Db!.Connection.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE name='x'";
        Assert.Equal(0L, (long)cmd.ExecuteScalar()!);
    }

    [Fact]
    public void Export_BlockedByDefault_AllowedWhenEnabled_AndAudited()
    {
        using var env = new TestEnv();
        env.VanBan.ThemMoi(env.NewDi(doMat: "A", trichYeu: null, soKyHieu: "=cmd|calc"));
        env.VanBan.ThemMoi(env.NewDi(trichYeu: "Trích yếu Mật"));
        var items = env.Store.Search(new SearchCriteria { Loai = LoaiSo.Di, Limit = 0, Giam = false }).Items;
        var bm = env.Store.GetBieuMau("SO_DI_ND63_2026")!;
        var exp = new ExportService(env.Store);
        var path = Path.Combine(env.Session.Paths.Export, "so.csv");
        Assert.Throws<BusinessException>(() => exp.ExportDocuments(items, bm, ExportFormat.Csv, path, false));
        new CauHinhService(env.Store).SetBool(ConfigKeys.ChoPhepXuatMat, true);
        exp.ExportDocuments(items, bm, ExportFormat.Csv, path, false);
        var csv = File.ReadAllText(path);
        Assert.Contains("'=cmd|calc", csv);
        Assert.Contains("Trích yếu Mật", csv);
        exp.ExportDocuments(items, bm, ExportFormat.Xlsx, Path.ChangeExtension(path, ".xlsx"), true);
        Assert.True(new FileInfo(Path.ChangeExtension(path, ".xlsx")).Length > 1000);
        Assert.Equal(2, env.Store.ListAudit(new()).Count(a => a.HanhDong == "Xuất dữ liệu văn bản"));
    }

    [Fact]
    public void SampleData_LoadAndDelete_ResetsNumbering()
    {
        using var env = new TestEnv();
        var svc = new DuLieuMauService(env.Store, env.VanBan, env.Clock);
        Assert.True(svc.CoTheNap());
        var n = svc.Nap(10);
        Assert.Equal(20, n);
        Assert.True(svc.DangCoDuLieuMau);
        Assert.All(env.Store.Search(new SearchCriteria { Loai = LoaiSo.Di, Limit = 0 }).Items, v => Assert.True(v.LaDuLieuMau));
        // Tuyệt mật trong dữ liệu mẫu cũng không có trích yếu
        Assert.All(env.Store.Search(new SearchCriteria { Loai = LoaiSo.Den, Limit = 0 }).Items.Where(v => v.DoMatTen == "TUYỆT MẬT"), v => Assert.Null(v.TrichYeu));
        Assert.Equal(20, svc.Xoa());
        Assert.False(svc.DangCoDuLieuMau);
        Assert.Equal(0, env.Store.Search(new SearchCriteria { Loai = LoaiSo.Di }).Total);
        Assert.Null(env.Store.FindDanhMuc(NhomDanhMuc.NguoiKy, "Nguyễn Văn Mẫu"));
        var v = env.NewDi();
        env.VanBan.ThemMoi(v);
        Assert.Equal(1, v.SoThuTu);
        Assert.False(svc.CoTheNap());
    }

    [Fact]
    public void Forms_DefineTenColumns_PerDecree()
    {
        var forms = Migrator.EmbeddedForms();
        Assert.All(forms, f => Assert.Equal(10, f.Cot.Count));
        Assert.Contains(forms, f => f.Loai == LoaiSo.Di && f.Cot[3].Truong == TruongBieuMau.TenLoaiTrichYeu);
    }
}
