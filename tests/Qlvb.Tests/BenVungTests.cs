using Qlvb.Application;
using Qlvb.Domain;
using Qlvb.Infrastructure;
using Qlvb.Infrastructure.Data;
using Qlvb.Infrastructure.Services;

namespace Qlvb.Tests;

/// <summary>Kiểm thử lỗi và độ bền: mất điện, tệp hỏng/mất, đồng hồ lệch, chuyển năm, ghi đồng thời, dữ liệu bất thường.</summary>
public class BenVungTests
{
    /// <summary>Mất điện ngay sau khi lưu: chụp nguyên trạng tệp (chưa checkpoint WAL) rồi mở bản chụp.</summary>
    [Fact]
    public void PowerLoss_AfterCommit_AllSavedDocumentsSurvive()
    {
        using var env = new TestEnv();
        for (var i = 1; i <= 20; i++) env.VanBan.ThemMoi(env.NewDi(soKyHieu: $"{i}/TN"));
        var snap = Path.Combine(env.Dir, "snap");
        var src = env.Session.Paths.Database;
        var dst = Path.Combine(snap, "Database");
        Directory.CreateDirectory(dst);
        foreach (var f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(dst, Path.GetFileName(f)));
        Assert.True(File.Exists(Path.Combine(dst, AppPaths.DatabaseFileName + "-wal")), "Bản chụp phải gồm cả tệp WAL (dữ liệu chưa checkpoint)");

        using var s2 = new AppSession(new AppPaths(snap, true), env.Clock, new FakeUser(), 1000);
        s2.Login(TestEnv.Password);
        var store = s2.RequireStore();
        Assert.Equal(20, store.Search(new SearchCriteria { Loai = LoaiSo.Di, Limit = 0 }).Total);
        Assert.Null(store.VerifyAuditChain());
        Assert.Equal(21, store.PeekNextNumber(LoaiSo.Di, 2026, BoDem.SoThuTu));
    }

    /// <summary>Giao dịch dở dang (mất điện giữa chừng) không để lại bản ghi nửa vời và không làm nhảy số.</summary>
    [Fact]
    public void FailedWrite_InsideTransaction_LeavesNoPartialRecord()
    {
        using var env = new TestEnv();
        env.VanBan.ThemMoi(env.NewDi());
        var v = env.NewDi();
        // Hàm tạo nhật ký ném lỗi sau khi bản ghi và số đã được ghi trong giao dịch.
        Assert.ThrowsAny<Exception>(() => env.Store.InsertDi(v, _ => throw new IOException("Mất điện giả lập")));
        env.Reopen();
        Assert.Equal(1, env.Store.Search(new SearchCriteria { Loai = LoaiSo.Di, Limit = 0 }).Total);
        Assert.Equal(2, env.Store.PeekNextNumber(LoaiSo.Di, 2026, BoDem.SoThuTu));
        Assert.Null(env.Store.VerifyAuditChain());
    }

    [Fact]
    public void CorruptedKeyFile_LoginFailsWithClearMessage_DatabaseUntouched()
    {
        using var env = new TestEnv();
        env.VanBan.ThemMoi(env.NewDi());
        env.Session.Logout();
        var db = File.ReadAllBytes(env.Session.Paths.DatabaseFile);
        File.WriteAllText(env.Session.Paths.KeyFile, "{ day khong phai tep khoa");
        var ex = Assert.ThrowsAny<Exception>(() => env.Session.Login(TestEnv.Password));
        Assert.Contains("tệp khóa", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(env.Session.LoggedIn);
        Assert.Equal(db, File.ReadAllBytes(env.Session.Paths.DatabaseFile));
    }

    [Fact]
    public void EmptyKeyFile_LoginFailsWithClearMessage()
    {
        using var env = new TestEnv();
        env.Session.Logout();
        File.WriteAllText(env.Session.Paths.KeyFile, "");
        var ex = Assert.ThrowsAny<Exception>(() => env.Session.Login(TestEnv.Password));
        Assert.Contains("tệp khóa", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingDatabase_WithKeyFile_ReportedAsMissing_NotRecreated()
    {
        using var env = new TestEnv();
        env.Session.Logout();
        File.Delete(env.Session.Paths.DatabaseFile);
        foreach (var f in Directory.GetFiles(env.Session.Paths.Database, "qlvb.db-*")) File.Delete(f);
        Assert.Equal(TrangThaiDuLieu.ThieuTep, env.Session.TrangThai);
        Assert.ThrowsAny<Exception>(() => env.Session.Login(TestEnv.Password));
        Assert.False(File.Exists(env.Session.Paths.DatabaseFile));
    }

    [Fact]
    public void TruncatedDatabase_DetectedOnLogin()
    {
        using var env = new TestEnv();
        for (var i = 0; i < 30; i++) env.VanBan.ThemMoi(env.NewDi(soKyHieu: $"{i}/TN"));
        env.Session.Logout();
        var path = env.Session.Paths.DatabaseFile;
        var bytes = File.ReadAllBytes(path);
        File.WriteAllBytes(path, bytes[..(bytes.Length / 2)]);
        Assert.ThrowsAny<Exception>(() => env.Session.Login(TestEnv.Password));
        Assert.False(env.Session.LoggedIn);
    }

    /// <summary>Đồng hồ máy bị chỉnh lùi: vẫn cấp số tăng dần, nhưng cảnh báo người dùng.</summary>
    [Fact]
    public void ClockMovedBack_NumbersStillIncrease_AndUserWarned()
    {
        using var env = new TestEnv();
        env.VanBan.ThemMoi(env.NewDi());
        env.Clock.Now = new DateTime(2026, 9, 20, 9, 0, 0);
        var v = env.NewDi();
        var kt = env.VanBan.KiemTra(v);
        Assert.True(kt.KetQua.IsValid);
        Assert.Contains(kt.KetQua.Warnings, w => w.Message.Contains("đồng hồ", StringComparison.OrdinalIgnoreCase));
        env.VanBan.ThemMoi(v);
        Assert.Equal(2, v.SoThuTu);
        Assert.Null(env.Store.VerifyAuditChain());
    }

    [Fact]
    public void ClockMovedBack_DetectedAtLogin()
    {
        using var env = new TestEnv();
        env.VanBan.ThemMoi(env.NewDi());
        env.Session.Logout();
        env.Clock.Now = new DateTime(2026, 9, 1, 9, 0, 0);
        env.Session.Login(TestEnv.Password);
        Assert.NotNull(env.Session.CanhBaoDongHo);
        env.Session.Logout();
        env.Clock.Now = new DateTime(2026, 10, 2, 9, 0, 0);
        env.Session.Login(TestEnv.Password);
        Assert.Null(env.Session.CanhBaoDongHo);
    }

    [Fact]
    public void YearRollover_AtMidnight_StartsNewNumbering_KeepsOldYear()
    {
        using var env = new TestEnv();
        env.Clock.Now = new DateTime(2026, 12, 31, 23, 59, 0);
        env.VanBan.ThemMoi(env.NewDi());
        env.VanBan.ThemMoi(env.NewDen());
        env.Clock.Now = new DateTime(2027, 1, 1, 0, 1, 0);
        var di = env.NewDi();
        env.VanBan.ThemMoi(di);
        var den = env.NewDen(soKyHieu: "1/QĐ-NEW");
        env.VanBan.ThemMoi(den);
        Assert.Equal((2027, 1), (di.Nam, di.SoThuTu));
        Assert.Equal((2027, 1, 1), (den.Nam, den.SoThuTu, den.SoDen));
        Assert.Equal(2, env.VanBan.SoDuKien(LoaiSo.Di, 2026, BoDem.SoThuTu));
        Assert.NotNull(env.Store.CurrentSoDangKy(LoaiSo.Di, 2027));
        Assert.Contains(2027, env.Store.Years(LoaiSo.Di));
        Assert.Contains(2026, env.Store.Years(LoaiSo.Di));
    }

    /// <summary>Hai phiên cùng ghi vào một dữ liệu (ví dụ mở nhầm hai lần): số không trùng, không mất bản ghi.</summary>
    [Fact]
    public async Task ConcurrentWriters_NoDuplicateOrLostNumbers()
    {
        using var env = new TestEnv();
        env.NewDi(); // tạo sẵn danh mục
        using var s2 = env.NewSession();
        s2.Login(TestEnv.Password);
        var vb1 = env.VanBan;
        var vb2 = new VanBanService(s2.RequireStore(), env.Clock, new FakeUser());
        var errors = new System.Collections.Concurrent.ConcurrentBag<Exception>();
        void Run(VanBanService vb, int n, string p)
        {
            for (var i = 0; i < n; i++)
            {
                try
                {
                    var v = vb.TaoMoiDi();
                    v.TenLoai = "Công văn"; v.DoMatId = env.DoMat("C").Id; v.TrichYeu = "Ghi đồng thời";
                    v.NguoiKy = "Nguyễn Văn A"; v.DonViLuu = "Văn phòng"; v.SoKyHieu = $"{p}{i}/TN";
                    v.NoiNhan = [new NoiNhanKyNhan { NoiNhan = "Phòng Kế hoạch" }];
                    vb.ThemMoi(v);
                }
                catch (Exception ex) { errors.Add(ex); }
            }
        }
        var t1 = Task.Run(() => Run(vb1, 40, "a"));
        var t2 = Task.Run(() => Run(vb2, 40, "b"));
        await Task.WhenAll(t1, t2);
        Assert.Empty(errors);
        var all = env.Store.Search(new SearchCriteria { Loai = LoaiSo.Di, Limit = 0 }).Items.Select(x => x.SoThuTu).OrderBy(x => x).ToList();
        Assert.Equal(Enumerable.Range(1, 80), all);
        Assert.Null(env.Store.VerifyAuditChain());
    }

    [Theory]
    [InlineData("'; DROP TABLE van_ban_di; --")]
    [InlineData("\" OR 1=1 --")]
    [InlineData("%_[]^\\")]
    [InlineData("Robert'); DELETE FROM nhat_ky;--")]
    public void HostileText_StoredLiterally_AndSearchable(string text)
    {
        using var env = new TestEnv();
        var v = env.NewDi(trichYeu: text, soKyHieu: text);
        env.VanBan.ThemMoi(v);
        var back = env.Store.GetDi(v.Id)!;
        Assert.Equal(text.Trim(), back.TrichYeu);
        Assert.Equal(1, env.Store.Search(new SearchCriteria { Loai = LoaiSo.Di, SoKyHieu = text }).Total);
        Assert.Equal(1, env.Store.Search(new SearchCriteria { Loai = LoaiSo.Di, TrichYeu = text }).Total);
        _ = env.Store.Search(new SearchCriteria { Loai = LoaiSo.Di, TuKhoa = text });
        Assert.Null(env.Store.VerifyAuditChain());
    }

    [Fact]
    public void OverlongFields_RejectedByValidation_NotDatabase()
    {
        using var env = new TestEnv();
        var v = env.NewDi(trichYeu: new string('x', Limits.TrichYeuMax + 1), soKyHieu: new string('y', Limits.SoKyHieuMax + 1));
        var kt = env.VanBan.KiemTra(v);
        Assert.Contains(kt.KetQua.Errors, e => e.Field == nameof(VanBanDi.TrichYeu));
        Assert.Contains(kt.KetQua.Errors, e => e.Field == nameof(VanBanDi.SoKyHieu));
        Assert.Throws<ValidationException>(() => env.VanBan.ThemMoi(v));
    }

    [Fact]
    public void ControlCharacters_RemovedFromInput()
    {
        using var env = new TestEnv();
        var v = env.NewDi(trichYeu: "Dòng 1\u0000\u0007\r\nDòng 2​", soKyHieu: "5/\tTN\u0001");
        env.VanBan.ThemMoi(v);
        var back = env.Store.GetDi(v.Id)!;
        Assert.DoesNotContain('\u0000', back.TrichYeu!);
        Assert.DoesNotContain('\u0007', back.TrichYeu!);
        Assert.DoesNotContain('\u0001', back.SoKyHieu);
        Assert.Contains("Dòng 2", back.TrichYeu);
    }

    [Theory]
    [InlineData(13, "đầy")]   // SQLITE_FULL
    [InlineData(11, "hỏng")]  // SQLITE_CORRUPT
    [InlineData(10, "đọc/ghi")] // SQLITE_IOERR
    [InlineData(8, "chỉ đọc")]  // SQLITE_READONLY
    public void SqliteErrors_TranslatedToVietnamese(int code, string expected)
    {
        var ex = SqliteDataStore.TranslateForTest(new Microsoft.Data.Sqlite.SqliteException("x", code));
        Assert.True(ex is BusinessException or DatabaseOpenException, ex.GetType().Name);
        Assert.Contains(expected, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Backup_ToMissingOrInvalidFolder_FailsCleanly()
    {
        using var env = new TestEnv();
        var bad = Path.Combine(env.Dir, "khong-ton-tai", "x", "bk.qlvbak");
        var ex = Record.Exception(() => env.Backup.Create(BackupKind.ThuCong, bad));
        // Tạo thư mục nếu chưa có hoặc báo lỗi rõ ràng; không để lại tệp dở dang.
        if (ex == null) Assert.True(File.Exists(bad));
        Assert.Empty(Directory.GetFiles(env.Dir, "*.dang-ghi", SearchOption.AllDirectories));
    }
}
