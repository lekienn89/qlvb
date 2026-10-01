using Qlvb.Infrastructure.Data;
using Qlvb.Infrastructure.Security;
using Qlvb.Infrastructure.Services;

namespace Qlvb.Tests;

public class SecurityTests
{
    [Fact]
    public void FirstRun_RequiresSetup_NoDefaultPassword()
    {
        using var env = new TestEnv(setup: false);
        Assert.Equal(TrangThaiDuLieu.ChuaKhoiTao, env.Session.TrangThai);
        Assert.Throws<FileNotFoundException>(() => env.Session.Login("admin"));
    }

    [Theory]
    [InlineData("short1")]
    [InlineData("chikhongcoso")]
    [InlineData("12345678901")]
    [InlineData(" Abcdefg123")]
    public void WeakPasswords_Rejected(string pw)
    {
        Assert.Throws<AuthException>(() => KeyStore.CheckPasswordPolicy(pw));
    }

    [Fact]
    public void Login_WrongPassword_FailsAndLocksOut()
    {
        using var env = new TestEnv();
        env.Session.Logout();
        for (var i = 0; i < 4; i++)
            Assert.Throws<AuthException>(() => env.Session.Login("SaiMatKhau1"));
        var ex = Assert.Throws<AuthException>(() => env.Session.Login("SaiMatKhau1"));
        Assert.NotNull(ex.RetryAfter);
        // Đang bị tạm khóa: kể cả mật khẩu đúng cũng phải chờ.
        Assert.Throws<AuthException>(() => env.Session.Login(TestEnv.Password));
    }

    [Fact]
    public void Login_CorrectPassword_Works_AndAudited()
    {
        using var env = new TestEnv();
        env.Reopen();
        Assert.True(env.Session.LoggedIn);
        Assert.Contains(env.Store.ListAudit(new()), a => a.HanhDong == "Đăng nhập");
    }

    [Fact]
    public void ChangePassword_OldStopsWorking()
    {
        using var env = new TestEnv();
        env.Session.ChangePassword(TestEnv.Password, "MatKhauMoi#99");
        env.Session.Logout();
        Assert.Throws<AuthException>(() => env.Session.Login(TestEnv.Password));
        env.Session.Login("MatKhauMoi#99");
        Assert.True(env.Session.LoggedIn);
    }

    [Fact]
    public void RecoveryCode_ResetsPassword_AndIsSingleUse()
    {
        using var env = new TestEnv();
        env.Session.Logout();
        var newCode = env.Session.ResetPassword(env.RecoveryCode.ToLowerInvariant().Replace("-", " "), "QuenRoi#2026");
        Assert.NotEqual(env.RecoveryCode, newCode);
        Assert.True(env.Session.LoggedIn);
        env.Session.Logout();
        Assert.Throws<AuthException>(() => env.Session.ResetPassword(env.RecoveryCode, "LaiQuen#2026"));
        env.Session.Login("QuenRoi#2026");
    }

    [Fact]
    public void DatabaseFile_IsEncrypted_NoPlaintextOnDisk()
    {
        using var env = new TestEnv();
        var v = env.NewDi(trichYeu: "CHUOIBIMATKHONGDUOCLO");
        env.VanBan.ThemMoi(v);
        env.Session.Logout();
        var bytes = File.ReadAllBytes(env.Session.Paths.DatabaseFile);
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain("CHUOIBIMATKHONGDUOCLO", text);
        Assert.DoesNotContain("SQLite format 3", text);
        Assert.DoesNotContain("Cơ quan Thử nghiệm", text);
        Assert.DoesNotContain(TestEnv.Password, File.ReadAllText(env.Session.Paths.KeyFile));
    }

    [Fact]
    public void WrongKey_CannotOpenDatabase()
    {
        using var env = new TestEnv();
        env.Session.Logout();
        Assert.Throws<DatabaseOpenException>(() => Database.Open(env.Session.Paths.DatabaseFile, new byte[32]));
    }

    [Fact]
    public void MissingKeyFile_ReportedAsMissing()
    {
        using var env = new TestEnv();
        env.Session.Logout();
        File.Delete(env.Session.Paths.KeyFile);
        Assert.Equal(TrangThaiDuLieu.ThieuTep, env.Session.TrangThai);
    }

    [Fact]
    public void CorruptedDatabase_DetectedOnLogin()
    {
        using var env = new TestEnv();
        for (var i = 0; i < 50; i++) env.VanBan.ThemMoi(env.NewDi(soKyHieu: $"{i}/TN"));
        env.Session.Logout();
        var path = env.Session.Paths.DatabaseFile;
        var bytes = File.ReadAllBytes(path);
        var rnd = new Random(1);
        for (var i = 8192; i < bytes.Length - 100; i += 4096) bytes[i + rnd.Next(100)] ^= 0xFF;
        File.WriteAllBytes(path, bytes);
        Assert.ThrowsAny<Exception>(() => env.Session.Login(TestEnv.Password));
        Assert.False(env.Session.LoggedIn);
    }

    [Fact]
    public void Redact_HidesQuotedValuesAndKeys()
    {
        var s = Logging.Redact("Lỗi \"Kế hoạch tác chiến\" với khóa 0123456789ABCDEF0123456789ABCDEF");
        Assert.DoesNotContain("tác chiến", s);
        Assert.DoesNotContain("0123456789ABCDEF0123456789ABCDEF", s);
    }
}
