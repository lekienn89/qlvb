using System.Security.Cryptography;
using Qlvb.Application;
using Qlvb.Infrastructure.Data;
using Qlvb.Infrastructure.Security;

namespace Qlvb.Infrastructure.Services;

public enum TrangThaiDuLieu
{
    /// <summary>Chưa có dữ liệu: chạy thiết lập lần đầu.</summary>
    ChuaKhoiTao,
    /// <summary>Có tệp khóa và CSDL: đăng nhập.</summary>
    SanSang,
    /// <summary>Có CSDL nhưng mất tệp khóa, hoặc ngược lại: chỉ có thể khôi phục từ bản sao lưu.</summary>
    ThieuTep,
}

public sealed class WindowsUser : ICurrentUser
{
    public string Name { get; } = Environment.UserName is { Length: > 0 } n ? n : "người dùng";
}

/// <summary>
/// Phiên làm việc: thiết lập lần đầu, đăng nhập (mở khóa dữ liệu), đăng xuất, đổi mật khẩu, khôi phục mật khẩu.
/// Khóa dữ liệu chỉ nằm trong bộ nhớ khi đã đăng nhập, bị xóa (ghi đè 0) khi đăng xuất.
/// </summary>
public sealed class AppSession : IDisposable
{
    private byte[]? _key;
    private readonly int _iterations;

    public AppPaths Paths { get; }
    public IClock Clock { get; }
    public ICurrentUser User { get; }
    public KeyStore Keys { get; }
    public Database? Db { get; private set; }
    public SqliteDataStore? Store { get; private set; }
    public bool LoggedIn => Db != null;
    /// <summary>Sự kiện sau mỗi lần mở dữ liệu thành công (đăng nhập/khôi phục).</summary>
    public event Action? Opened;

    public AppSession(AppPaths paths, IClock? clock = null, ICurrentUser? user = null, int kdfIterations = KeyStore.DefaultIterations)
    {
        Paths = paths;
        Clock = clock ?? new SystemClock();
        User = user ?? new WindowsUser();
        _iterations = kdfIterations;
        paths.EnsureCreated();
        Keys = new KeyStore(paths.KeyFile, kdfIterations);
    }

    public TrangThaiDuLieu TrangThai
    {
        get
        {
            var k = File.Exists(Paths.KeyFile);
            var d = File.Exists(Paths.DatabaseFile);
            if (!k && !d) return TrangThaiDuLieu.ChuaKhoiTao;
            return k && d ? TrangThaiDuLieu.SanSang : TrangThaiDuLieu.ThieuTep;
        }
    }

    public IDataStore RequireStore() => Store ?? throw new InvalidOperationException("Chưa đăng nhập.");

    internal byte[] RequireKey() => _key ?? throw new InvalidOperationException("Chưa đăng nhập.");

    /// <summary>Thiết lập lần đầu. Trả về mã khôi phục để người dùng ghi lại.</summary>
    public string Setup(string password, string? tenCoQuan)
    {
        if (TrangThai != TrangThaiDuLieu.ChuaKhoiTao) throw new InvalidOperationException("Dữ liệu đã được khởi tạo.");
        var (key, code) = Keys.Create(password);
        try
        {
            Open(key, create: true);
            var store = Store!;
            store.AppendAudit(new AuditEntry("Khởi tạo dữ liệu", "he_thong", null, "Thiết lập lần đầu, tạo mật khẩu và mã khôi phục"));
            if (!string.IsNullOrWhiteSpace(tenCoQuan))
                store.SetConfig(ConfigKeys.TenCoQuan, Domain.TextUtil.Clean(tenCoQuan));
            return code;
        }
        catch
        {
            // Thiết lập dở dang: dọn để lần sau thiết lập lại.
            Close();
            TryDelete(Paths.DatabaseFile);
            TryDelete(Paths.KeyFile);
            throw;
        }
    }

    public void Login(string password, Action<int, int>? beforeUpgrade = null)
    {
        var key = Keys.Unlock(password);
        Open(key, create: false, beforeUpgrade);
        KiemTraDongHo();
        Store!.AppendAudit(new AuditEntry("Đăng nhập", "he_thong", null, "Đăng nhập thành công"));
    }

    /// <summary>Cảnh báo khi đồng hồ máy sớm hơn thao tác cuối cùng đã ghi (máy bị chỉnh lùi giờ, hết pin CMOS…). null nếu bình thường.</summary>
    public string? CanhBaoDongHo { get; private set; }

    private void KiemTraDongHo()
    {
        var last = Store!.ListAudit(new AuditFilter { Limit = 1 }).FirstOrDefault()?.ThoiGian;
        var now = Clock.Now;
        CanhBaoDongHo = last is { } t && t > now.AddMinutes(5)
            ? $"Ngày giờ của máy tính ({now:dd/MM/yyyy HH:mm}) đang sớm hơn thao tác gần nhất đã ghi trong nhật ký ({t:dd/MM/yyyy HH:mm}).\n\n" +
              "Đồng hồ máy có thể đã bị chỉnh lùi hoặc hết pin. Hãy kiểm tra lại ngày giờ trước khi đăng ký văn bản, vì ngày đăng ký lấy theo đồng hồ máy."
            : null;
    }

    /// <summary>Kiểm tra mật khẩu khi mở khóa màn hình. Ghi nhật ký kết quả.</summary>
    public bool Unlock(string password)
    {
        var ok = Keys.Verify(password);
        Store?.AppendAudit(new AuditEntry(ok ? "Mở khóa màn hình" : "Mở khóa màn hình thất bại", "he_thong", null,
            ok ? "Nhập đúng mật khẩu" : "Nhập sai mật khẩu"));
        return ok;
    }

    public void LockScreen() => Store?.AppendAudit(new AuditEntry("Khóa màn hình", "he_thong", null, "Khóa màn hình"));

    public void Logout()
    {
        if (Store != null) Store.AppendAudit(new AuditEntry("Đăng xuất", "he_thong", null, "Đăng xuất"));
        Close();
    }

    public void ChangePassword(string oldPassword, string newPassword)
    {
        Keys.ChangePassword(oldPassword, newPassword);
        Store?.AppendAudit(new AuditEntry("Đổi mật khẩu", "he_thong", null, "Đổi mật khẩu thành công"));
    }

    public string RegenerateRecoveryCode(string password)
    {
        var code = Keys.RegenerateRecoveryCode(password);
        Store?.AppendAudit(new AuditEntry("Tạo mã khôi phục mới", "he_thong", null, "Mã khôi phục cũ hết hiệu lực"));
        return code;
    }

    /// <summary>Quên mật khẩu: đặt mật khẩu mới bằng mã khôi phục, đăng nhập luôn. Trả về mã khôi phục mới.</summary>
    public string ResetPassword(string recoveryCode, string newPassword)
    {
        var (key, code) = Keys.ResetWithRecoveryCode(recoveryCode, newPassword);
        Open(key, create: false);
        Store!.AppendAudit(new AuditEntry("Đặt lại mật khẩu bằng mã khôi phục", "he_thong", null, "Đặt lại mật khẩu; đã cấp mã khôi phục mới"));
        return code;
    }

    /// <summary>Mở dữ liệu bằng khóa. Khóa được chép sang vùng nhớ cố định; mảng truyền vào bị xóa (ghi 0).</summary>
    internal void Open(byte[] key, bool create, Action<int, int>? beforeUpgrade = null)
    {
        Close();
        var pinned = Database.PinnedCopy(key);
        CryptographicOperations.ZeroMemory(key);
        key = pinned;
        Database db;
        try { db = Database.Open(Paths.DatabaseFile, key, create); }
        catch
        {
            CryptographicOperations.ZeroMemory(key);
            throw;
        }
        try
        {
            if (!create)
            {
                var problem = db.QuickCheck();
                if (problem != null)
                    throw new DatabaseOpenException($"Cơ sở dữ liệu bị hỏng ({problem}) Hãy khôi phục từ bản sao lưu gần nhất.");
            }
            _key = key;
            Db = db;
            Store = new SqliteDataStore(db, Clock, User);
            Migrator.Migrate(db, () => Clock.Now, (from, to) =>
            {
                beforeUpgrade?.Invoke(from, to);
                new BackupService(this).Create(BackupKind.TruocNangCap);
            });
        }
        catch
        {
            Store = null;
            Db = null;
            db.Dispose();
            CryptographicOperations.ZeroMemory(key);
            _key = null;
            throw;
        }
        Opened?.Invoke();
    }

    internal void Close()
    {
        Store = null;
        Db?.Dispose();
        Db = null;
        if (_key != null) CryptographicOperations.ZeroMemory(_key);
        _key = null;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { }
    }

    public void Dispose() => Close();
}
