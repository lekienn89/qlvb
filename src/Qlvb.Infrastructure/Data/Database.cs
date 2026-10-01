using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Qlvb.Domain;

namespace Qlvb.Infrastructure.Data;

/// <summary>Tệp cơ sở dữ liệu không mở được (sai khóa, hỏng, không phải CSDL của phần mềm).</summary>
public sealed class DatabaseOpenException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Kết nối SQLite mã hóa toàn bộ tệp (SQLite3 Multiple Ciphers, lược đồ SQLCipher 4, AES-256).
/// Ứng dụng một người dùng nên dùng MỘT kết nối, mọi truy cập được tuần tự hóa qua <see cref="Gate"/>.
/// </summary>
public sealed class Database : IDisposable
{
    private static int _initialized;
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public SqliteConnection Connection { get; }
    public object Gate { get; } = new();
    public string FilePath { get; }

    private Database(SqliteConnection c, string path)
    {
        Connection = c;
        FilePath = path;
    }

    public static void InitNative()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 0) SQLitePCL.Batteries_V2.Init();
    }

    /// <summary>Mở (hoặc tạo) tệp CSDL bằng khóa 256 bit.</summary>
    public static Database Open(string path, byte[] key, bool create = false)
    {
        InitNative();
        if (!create && !File.Exists(path)) throw new DatabaseOpenException("Không tìm thấy tệp cơ sở dữ liệu.");
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = create ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadWrite,
            Pooling = false,
        }.ToString();
        var c = new SqliteConnection(cs);
        try
        {
            c.Open();
            ApplyKey(c, key);
            Exec(c, "PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL; PRAGMA synchronous = FULL; PRAGMA busy_timeout = 5000; PRAGMA secure_delete = ON;");
            // Kiểm tra đọc được (sai khóa → SQLITE_NOTADB)
            Exec(c, "SELECT count(*) FROM sqlite_master;");
            RegisterFunctions(c);
            return new Database(c, path);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 26)
        {
            c.Dispose();
            throw new DatabaseOpenException("Không mở được cơ sở dữ liệu: khóa không khớp hoặc tệp không phải dữ liệu của phần mềm.", ex);
        }
        catch (SqliteException ex)
        {
            c.Dispose();
            throw new DatabaseOpenException($"Không mở được cơ sở dữ liệu (mã lỗi {ex.SqliteErrorCode}).", ex);
        }
    }

    internal static void ApplyKey(SqliteConnection c, byte[] key)
    {
        if (key.Length != 32) throw new ArgumentException("Khóa phải dài 256 bit.");
        Exec(c, "PRAGMA cipher = 'sqlcipher'; PRAGMA legacy = 4;");
        // Truyền thẳng 32 byte khóa xuống thư viện mã hóa (tương đương PRAGMA hexkey), không tạo chuỗi hex
        // trong bộ nhớ quản lý: chuỗi không xóa được và có thể còn lại trong bộ nhớ đến khi bị thu gom.
        var rc = SQLitePCL.raw.sqlite3_key(c.Handle, key);
        if (rc != SQLitePCL.raw.SQLITE_OK) throw new DatabaseOpenException($"Không áp dụng được khóa dữ liệu (mã lỗi {rc}).");
    }

    /// <summary>Cấp mảng khóa ở vùng nhớ cố định (GC không sao chép đi nơi khác), để xóa được triệt để bằng ZeroMemory.</summary>
    public static byte[] PinnedCopy(ReadOnlySpan<byte> key)
    {
        var a = GC.AllocateUninitializedArray<byte>(key.Length, pinned: true);
        key.CopyTo(a);
        return a;
    }

    private static void RegisterFunctions(SqliteConnection c)
    {
        c.CreateFunction("bo_dau", (string? s) => s == null ? null : TextUtil.SearchKey(s), isDeterministic: true);
        c.CreateCollation("VI", (a, b) => string.Compare(a, b, Vi, CompareOptions.IgnoreCase));
    }

    internal static void Exec(SqliteConnection c, string sql, SqliteTransaction? tx = null)
    {
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    /// <summary>Kiểm tra nhanh tính toàn vẹn. Trả về null nếu tốt, ngược lại mô tả lỗi (không chứa dữ liệu).</summary>
    public string? QuickCheck(bool full = false)
    {
        lock (Gate)
        {
            using var cmd = Connection.CreateCommand();
            cmd.CommandText = full ? "PRAGMA integrity_check;" : "PRAGMA quick_check;";
            var lines = new List<string>();
            using var r = cmd.ExecuteReader();
            while (r.Read()) lines.Add(r.GetString(0));
            if (lines.Count == 1 && lines[0] == "ok") return null;
            var fk = ForeignKeyProblems();
            return $"Phát hiện {lines.Count} lỗi cấu trúc dữ liệu{(fk > 0 ? $", {fk} lỗi liên kết" : "")}.";
        }
    }

    private int ForeignKeyProblems()
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_key_check;";
        var n = 0;
        using var r = cmd.ExecuteReader();
        while (r.Read()) n++;
        return n;
    }

    /// <summary>Sao chép trực tuyến sang tệp mới, mã hóa bằng cùng khóa.</summary>
    public void BackupTo(string destPath, byte[] key)
    {
        if (File.Exists(destPath)) File.Delete(destPath);
        var cs = new SqliteConnectionStringBuilder { DataSource = destPath, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString();
        using var dest = new SqliteConnection(cs);
        dest.Open();
        ApplyKey(dest, key);
        lock (Gate)
        {
            Exec(Connection, "PRAGMA wal_checkpoint(FULL);");
            Connection.BackupDatabase(dest);
        }
        Exec(dest, "PRAGMA journal_mode = DELETE;");
    }

    public void Checkpoint()
    {
        lock (Gate) Exec(Connection, "PRAGMA wal_checkpoint(TRUNCATE);");
    }

    public void Dispose()
    {
        try { Checkpoint(); } catch (SqliteException) { }
        Connection.Dispose();
    }
}

/// <summary>Thông tin một bước di trú lược đồ.</summary>
public sealed record Migration(int Version, string Name, string Sql);

/// <summary>
/// Di trú lược đồ: áp dụng các tệp Migrations/V###__ten.sql theo thứ tự, mỗi bước trong một giao dịch
/// (lỗi → hoàn tác toàn bộ bước đó). Trước khi nâng cấp CSDL đã có dữ liệu, gọi <c>beforeUpgrade</c> để sao lưu.
/// </summary>
public static class Migrator
{
    public static IReadOnlyList<Migration> Embedded()
    {
        var asm = typeof(Migrator).Assembly;
        var list = new List<Migration>();
        foreach (var name in asm.GetManifestResourceNames().Where(n => n.Contains(".Migrations.V") && n.EndsWith(".sql")))
        {
            var file = name[(name.IndexOf(".Migrations.", StringComparison.Ordinal) + ".Migrations.".Length)..];
            var us = file.IndexOf("__", StringComparison.Ordinal);
            var ver = int.Parse(file[1..us], CultureInfo.InvariantCulture);
            using var s = asm.GetManifestResourceStream(name)!;
            using var rd = new StreamReader(s);
            list.Add(new Migration(ver, file[(us + 2)..^4], rd.ReadToEnd()));
        }
        var ordered = list.OrderBy(m => m.Version).ToList();
        for (var i = 0; i < ordered.Count; i++)
            if (ordered[i].Version != i + 1) throw new InvalidOperationException("Thiếu bước di trú V" + (i + 1));
        return ordered;
    }

    public static int LatestVersion => Embedded().Count;

    public static int CurrentVersion(Database db)
    {
        lock (db.Gate)
        {
            using var cmd = db.Connection.CreateCommand();
            cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='schema_version'";
            if (Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture) == 0) return 0;
            cmd.CommandText = "SELECT ifnull(max(version),0) FROM schema_version";
            return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        }
    }

    /// <returns>Số bước đã áp dụng.</returns>
    public static int Migrate(Database db, Func<DateTime> now, Action<int, int>? beforeUpgrade = null, IReadOnlyList<Migration>? migrations = null)
    {
        migrations ??= Embedded();
        var current = CurrentVersion(db);
        if (current > migrations.Count)
            throw new DatabaseOpenException($"Dữ liệu được tạo bởi phiên bản phần mềm mới hơn (lược đồ {current}). Hãy dùng phiên bản phần mềm mới hơn.");
        var pending = migrations.Where(m => m.Version > current).ToList();
        if (pending.Count == 0) { SeedForms(db); return 0; }
        if (current > 0) beforeUpgrade?.Invoke(current, migrations.Count);
        lock (db.Gate)
        {
            var c = db.Connection;
            Database.Exec(c, "CREATE TABLE IF NOT EXISTS schema_version (version INTEGER PRIMARY KEY, ten TEXT NOT NULL, ap_dung_luc TEXT NOT NULL);");
            foreach (var m in pending)
            {
                using var tx = c.BeginTransaction();
                try
                {
                    Database.Exec(c, m.Sql, tx);
                    using var cmd = c.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = "INSERT INTO schema_version(version, ten, ap_dung_luc) VALUES ($v, $t, $l)";
                    cmd.Parameters.AddWithValue("$v", m.Version);
                    cmd.Parameters.AddWithValue("$t", m.Name);
                    cmd.Parameters.AddWithValue("$l", now().ToString("s", CultureInfo.InvariantCulture));
                    cmd.ExecuteNonQuery();
                    tx.Commit();
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    throw new DatabaseOpenException($"Nâng cấp dữ liệu lên phiên bản {m.Version} thất bại; dữ liệu giữ nguyên như trước khi nâng cấp.", ex);
                }
            }
        }
        SeedForms(db);
        return pending.Count;
    }

    /// <summary>Nạp các biểu mẫu sổ đi kèm phần mềm nếu CSDL chưa có (không ghi đè biểu mẫu đã có).</summary>
    public static void SeedForms(Database db)
    {
        lock (db.Gate)
        {
            foreach (var bm in EmbeddedForms())
            {
                using var cmd = db.Connection.CreateCommand();
                cmd.CommandText = """
                    INSERT OR IGNORE INTO bieu_mau(ma, loai, phien_ban, hieu_luc_tu, can_cu, dinh_nghia)
                    VALUES ($ma, $loai, $pb, $hl, $cc, $dn)
                    """;
                cmd.Parameters.AddWithValue("$ma", bm.Ma);
                cmd.Parameters.AddWithValue("$loai", (int)bm.Loai);
                cmd.Parameters.AddWithValue("$pb", bm.PhienBan);
                cmd.Parameters.AddWithValue("$hl", bm.HieuLucTu.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("$cc", bm.CanCu);
                cmd.Parameters.AddWithValue("$dn", JsonSerializer.Serialize(bm));
                cmd.ExecuteNonQuery();
            }
        }
    }

    public static IReadOnlyList<BieuMau> EmbeddedForms()
    {
        var asm = typeof(Migrator).Assembly;
        var list = new List<BieuMau>();
        foreach (var name in asm.GetManifestResourceNames().Where(n => n.Contains(".Forms.") && n.EndsWith(".json")))
        {
            using var s = asm.GetManifestResourceStream(name)!;
            var bm = JsonSerializer.Deserialize<BieuMau>(s) ?? throw new InvalidDataException(name);
            BieuMauValidator.Validate(bm);
            list.Add(bm);
        }
        return list;
    }
}

public static class BieuMauValidator
{
    /// <summary>Biểu mẫu chỉ được tham chiếu các trường phần mềm hiểu được.</summary>
    public static void Validate(BieuMau bm)
    {
        if (string.IsNullOrWhiteSpace(bm.Ma) || bm.Cot.Count == 0) throw new InvalidDataException("Biểu mẫu không hợp lệ.");
        foreach (var c in bm.Cot)
            if (!TruongBieuMau.TatCa.Contains(c.Truong))
                throw new InvalidDataException($"Biểu mẫu {bm.Ma}: trường \"{c.Truong}\" không được hỗ trợ.");
    }
}
