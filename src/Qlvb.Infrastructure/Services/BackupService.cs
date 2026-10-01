using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Qlvb.Application;
using Qlvb.Infrastructure.Data;
using Qlvb.Infrastructure.Security;

namespace Qlvb.Infrastructure.Services;

public enum BackupKind
{
    /// <summary>Người dùng chọn nơi lưu.</summary>
    ThuCong,
    /// <summary>Sao lưu nhanh vào thư mục mặc định.</summary>
    Nhanh,
    TuDongKhiThoat,
    TruocNangCap,
    TruocKhoiPhuc,
}

public sealed class BackupManifest
{
    public int Format { get; set; } = 1;
    public string App { get; set; } = "QLVB";
    public string AppVersion { get; set; } = "";
    public int SchemaVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Kind { get; set; } = "";
    public string DatabaseSha256 { get; set; } = "";
    public string KeyFileSha256 { get; set; } = "";
    public long DatabaseSize { get; set; }
}

public sealed record BackupInfo(string Path, DateTime CreatedAt, long Size, BackupKind? Kind, int SchemaVersion);

/// <summary>
/// Sao lưu/khôi phục. Tệp .qlvbak là gói zip gồm: manifest.json, qlvb.db (bản chụp CSDL ĐÃ MÃ HÓA bằng cùng khóa)
/// và qlvb.key (khóa dữ liệu đã được bọc bằng mật khẩu tại thời điểm sao lưu). Bản sao lưu không chứa dữ liệu rõ
/// và chỉ mở được bằng mật khẩu (hoặc mã khôi phục) đang dùng khi sao lưu.
/// </summary>
public sealed class BackupService(AppSession session)
{
    public const string Extension = ".qlvbak";
    private const string DbEntry = "qlvb.db";
    private const string KeyEntry = "qlvb.key";
    private const string ManifestEntry = "manifest.json";
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
    // Giới hạn khi đọc tệp sao lưu do người dùng chọn: chặn tệp giả mạo/"bom nén" làm đầy ổ đĩa hoặc treo máy.
    private const long MaxSmallEntry = 64 * 1024;
    internal const long MaxDatabaseEntry = 4L * 1024 * 1024 * 1024;
    private const int MaxCompressionRatio = 200;

    private string WorkDir()
    {
        var d = Path.Combine(session.Paths.Backup, ".tam");
        Directory.CreateDirectory(d);
        return d;
    }

    private static string KindCode(BackupKind k) => k switch
    {
        BackupKind.ThuCong => "thucong",
        BackupKind.Nhanh => "nhanh",
        BackupKind.TuDongKhiThoat => "khithoat",
        BackupKind.TruocNangCap => "truocnangcap",
        BackupKind.TruocKhoiPhuc => "truockhoiphuc",
        _ => "khac",
    };

    public string DefaultFileName(BackupKind kind) =>
        $"QLVB_{session.Clock.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}_{KindCode(kind)}{Extension}";

    /// <summary>Tạo bản sao lưu. <paramref name="destination"/> null → thư mục sao lưu mặc định.</summary>
    public string Create(BackupKind kind, string? destination = null)
    {
        var db = session.Db ?? throw new InvalidOperationException("Chưa đăng nhập.");
        var key = session.RequireKey();
        destination ??= Path.Combine(session.Paths.Backup, DefaultFileName(kind));
        if (!destination.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)) destination += Extension;
        var work = WorkDir();
        var snap = Path.Combine(work, Guid.NewGuid().ToString("N") + ".db");
        var partial = destination + ".dang-ghi";
        try
        {
            db.BackupTo(snap, key);
            var keyJson = session.Keys.ReadRaw();
            var manifest = new BackupManifest
            {
                AppVersion = typeof(BackupService).Assembly.GetName().Version?.ToString() ?? "",
                SchemaVersion = Migrator.CurrentVersion(db),
                CreatedAt = session.Clock.Now,
                Kind = KindCode(kind),
                DatabaseSha256 = Sha256File(snap),
                KeyFileSha256 = Sha256(System.Text.Encoding.UTF8.GetBytes(keyJson)),
                DatabaseSize = new FileInfo(snap).Length,
            };
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
            using (var fs = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: true))
                {
                    zip.CreateEntryFromFile(snap, DbEntry, CompressionLevel.Optimal);
                    using (var w = new StreamWriter(zip.CreateEntry(KeyEntry).Open())) w.Write(keyJson);
                    using (var w = new StreamWriter(zip.CreateEntry(ManifestEntry).Open())) w.Write(JsonSerializer.Serialize(manifest, JsonOpts));
                }
                fs.Flush(true);
            }
            Inspect(partial); // đọc lại để chắc chắn tệp vừa ghi dùng được
            File.Move(partial, destination, overwrite: true);
            session.Store!.AppendAudit(new AuditEntry("Sao lưu", "he_thong", null,
                $"{KindName(kind)}: {Path.GetFileName(destination)}{(kind == BackupKind.ThuCong ? " – " + Path.GetDirectoryName(destination) : "")}"));
            session.Store!.SetConfig(ConfigKeys.LanSaoLuuCuoi, SqliteDataStore.T(session.Clock.Now));
            if (kind is BackupKind.Nhanh or BackupKind.TuDongKhiThoat) Prune();
            return destination;
        }
        finally
        {
            TryDelete(snap);
            TryDelete(partial);
        }
    }

    public static string KindName(BackupKind k) => k switch
    {
        BackupKind.ThuCong => "Sao lưu thủ công",
        BackupKind.Nhanh => "Sao lưu nhanh",
        BackupKind.TuDongKhiThoat => "Tự động sao lưu khi thoát",
        BackupKind.TruocNangCap => "Sao lưu trước khi nâng cấp dữ liệu",
        BackupKind.TruocKhoiPhuc => "Sao lưu an toàn trước khi khôi phục",
        _ => "Sao lưu",
    };

    /// <summary>Giữ lại N bản tự động mới nhất trong thư mục mặc định (không xóa bản thủ công, trước nâng cấp/khôi phục).</summary>
    public void Prune()
    {
        var keep = int.TryParse(session.Store?.GetConfig(ConfigKeys.SoBanSaoLuuGiuLai), out var k) && k > 0 ? k : ConfigKeys.MacDinhSoBanSaoLuu;
        var auto = Directory.GetFiles(session.Paths.Backup, "QLVB_*" + Extension)
            .Where(f => f.Contains("_nhanh", StringComparison.Ordinal) || f.Contains("_khithoat", StringComparison.Ordinal))
            .OrderByDescending(f => f, StringComparer.Ordinal).Skip(keep);
        foreach (var f in auto) TryDelete(f);
    }

    public IReadOnlyList<BackupInfo> List()
    {
        var list = new List<BackupInfo>();
        foreach (var f in Directory.GetFiles(session.Paths.Backup, "*" + Extension))
        {
            try
            {
                var m = ReadManifest(f);
                list.Add(new BackupInfo(f, m.CreatedAt, new FileInfo(f).Length, ParseKind(m.Kind), m.SchemaVersion));
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or JsonException)
            {
                list.Add(new BackupInfo(f, File.GetLastWriteTime(f), new FileInfo(f).Length, null, 0));
            }
        }
        return list.OrderByDescending(b => b.CreatedAt).ToList();
    }

    private static BackupKind? ParseKind(string code) =>
        Enum.GetValues<BackupKind>().Cast<BackupKind?>().FirstOrDefault(k => KindCode(k!.Value) == code);

    private static BackupManifest ReadManifest(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        var e = zip.GetEntry(ManifestEntry) ?? throw new InvalidDataException("Không phải tệp sao lưu của phần mềm.");
        using var s = e.Open();
        return JsonSerializer.Deserialize<BackupManifest>(s) ?? throw new InvalidDataException("Tệp sao lưu hỏng.");
    }

    /// <summary>Kiểm tra cấu trúc và mã băm của tệp sao lưu (không cần mật khẩu).</summary>
    public static BackupManifest Inspect(string path)
    {
        try
        {
            using var zip = ZipFile.OpenRead(path);
            if (zip.Entries.Count != 3 || zip.Entries.Any(e => e.FullName is not (ManifestEntry or DbEntry or KeyEntry)))
                throw new InvalidDataException("Không phải tệp sao lưu của phần mềm (cấu trúc tệp không đúng).");
            var me = zip.GetEntry(ManifestEntry) ?? throw new InvalidDataException("Không phải tệp sao lưu của phần mềm.");
            if (me.Length > MaxSmallEntry) throw new InvalidDataException("Không phải tệp sao lưu của phần mềm (thông tin mô tả quá lớn).");
            BackupManifest m;
            using (var s = me.Open()) m = JsonSerializer.Deserialize<BackupManifest>(s) ?? throw new InvalidDataException("Tệp sao lưu hỏng.");
            if (m.App != "QLVB" || m.Format != 1) throw new InvalidDataException("Không phải tệp sao lưu của phần mềm hoặc định dạng không được hỗ trợ.");
            var de = zip.GetEntry(DbEntry) ?? throw new InvalidDataException("Tệp sao lưu thiếu dữ liệu.");
            var ke = zip.GetEntry(KeyEntry) ?? throw new InvalidDataException("Tệp sao lưu thiếu tệp khóa.");
            if (ke.Length > MaxSmallEntry) throw new InvalidDataException("Tệp sao lưu bị hỏng (tệp khóa không đúng định dạng).");
            if (de.Length > MaxDatabaseEntry || (m.DatabaseSize > 0 && de.Length != m.DatabaseSize)
                || (de.CompressedLength > 0 && de.Length / de.CompressedLength > MaxCompressionRatio))
                throw new InvalidDataException("Tệp sao lưu bị hỏng hoặc bị sửa đổi (kích thước dữ liệu không hợp lệ).");
            using (var s = de.Open())
                if (!string.Equals(Sha256(s), m.DatabaseSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Tệp sao lưu bị hỏng (mã kiểm tra dữ liệu không khớp).");
            using (var s = ke.Open())
                if (!string.Equals(Sha256(s), m.KeyFileSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Tệp sao lưu bị hỏng (mã kiểm tra tệp khóa không khớp).");
            return m;
        }
        catch (InvalidDataException) { throw; }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            throw new InvalidDataException("Không đọc được tệp sao lưu.", ex);
        }
    }

    /// <summary>
    /// Khôi phục: kiểm tra tệp, mở thử bằng mật khẩu của bản sao lưu, kiểm tra toàn vẹn, sao lưu an toàn dữ liệu hiện tại,
    /// rồi mới thay thế. Sau khi khôi phục, mật khẩu đăng nhập là mật khẩu của bản sao lưu.
    /// </summary>
    public BackupManifest Restore(string path, string backupPassword)
    {
        var m = Inspect(path);
        if (m.SchemaVersion > Migrator.LatestVersion)
            throw new InvalidDataException("Bản sao lưu được tạo bởi phiên bản phần mềm mới hơn. Hãy cập nhật phần mềm trước khi khôi phục.");
        var work = WorkDir();
        KiemTraChoTrong(work, m.DatabaseSize);
        var tmpDb = Path.Combine(work, Guid.NewGuid().ToString("N") + ".db");
        string keyJson;
        byte[]? key = null;
        try
        {
            using (var zip = ZipFile.OpenRead(path))
            {
                zip.GetEntry(DbEntry)!.ExtractToFile(tmpDb, true);
                using var r = new StreamReader(zip.GetEntry(KeyEntry)!.Open());
                keyJson = r.ReadToEnd();
            }
            key = KeyStore.TryUnlockFile(keyJson, backupPassword)
                  ?? throw new AuthException("Mật khẩu không đúng với bản sao lưu (cần mật khẩu đang dùng tại thời điểm sao lưu).");
            using (var test = Database.Open(tmpDb, key))
            {
                var problem = test.QuickCheck(full: true);
                if (problem != null) throw new InvalidDataException($"Dữ liệu trong bản sao lưu bị lỗi: {problem}");
            }
            // Sao lưu an toàn dữ liệu hiện tại.
            string? safety = null;
            if (session.LoggedIn) safety = Create(BackupKind.TruocKhoiPhuc);
            else SafetyCopyRaw();
            session.Close();
            foreach (var suffix in new[] { "-wal", "-shm" }) TryDelete(session.Paths.DatabaseFile + suffix);
            File.Copy(tmpDb, session.Paths.DatabaseFile, overwrite: true);
            session.Keys.ReplaceRaw(keyJson);
            session.Open(key, create: false);
            key = null; // phiên giữ khóa
            session.Store!.AppendAudit(new AuditEntry("Khôi phục dữ liệu", "he_thong", null,
                $"Khôi phục từ {Path.GetFileName(path)} (tạo lúc {m.CreatedAt:dd/MM/yyyy HH:mm}){(safety != null ? "; bản an toàn: " + Path.GetFileName(safety) : "")}"));
            return m;
        }
        finally
        {
            if (key != null) CryptographicOperations.ZeroMemory(key);
            TryDelete(tmpDb);
        }
    }

    /// <summary>Cần chỗ cho bản giải nén tạm, bản an toàn của dữ liệu hiện tại và dữ liệu khôi phục.</summary>
    private void KiemTraChoTrong(string dir, long dbSize)
    {
        long free;
        try { free = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(dir))!).AvailableFreeSpace; }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException) { return; }
        var current = File.Exists(session.Paths.DatabaseFile) ? new FileInfo(session.Paths.DatabaseFile).Length : 0;
        var need = 2 * dbSize + current + 50L * 1024 * 1024;
        if (free < need)
            throw new IOException($"Ổ đĩa không đủ chỗ trống để khôi phục (cần khoảng {need / 1048576.0:N0} MB, còn {free / 1048576.0:N0} MB). Hãy giải phóng dung lượng rồi thử lại.");
    }

    /// <summary>Khi chưa đăng nhập được (dữ liệu hỏng/mất khóa): sao chép nguyên trạng tệp hiện có trước khi ghi đè.</summary>
    private void SafetyCopyRaw()
    {
        var dir = Path.Combine(session.Paths.Backup, "TruocKhoiPhuc_" + session.Clock.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture));
        foreach (var f in new[] { session.Paths.DatabaseFile, session.Paths.DatabaseFile + "-wal", session.Paths.KeyFile })
        {
            if (!File.Exists(f)) continue;
            Directory.CreateDirectory(dir);
            File.Copy(f, Path.Combine(dir, Path.GetFileName(f)), true);
        }
    }

    private static string Sha256File(string path)
    {
        using var s = File.OpenRead(path);
        return Sha256(s);
    }

    private static string Sha256(Stream s) => Convert.ToHexString(SHA256.HashData(s));
    private static string Sha256(byte[] b) => Convert.ToHexString(SHA256.HashData(b));

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
