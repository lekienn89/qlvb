using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Qlvb.Application;
using Qlvb.Domain;

namespace Qlvb.Infrastructure.Data;

/// <summary>
/// Cài đặt <see cref="IDataStore"/> trên SQLite. Mỗi thao tác ghi chạy trong một giao dịch, nhật ký được ghi
/// trong cùng giao dịch (dữ liệu và nhật ký cùng thành công hoặc cùng hủy).
/// </summary>
public sealed partial class SqliteDataStore(Database db, IClock clock, ICurrentUser user) : IDataStore
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private const string GenesisHash = "0000000000000000000000000000000000000000000000000000000000000000";
    private SqliteConnection C => db.Connection;

    // ================================================================== tiện ích
    private T Read<T>(Func<T> f)
    {
        lock (db.Gate) return f();
    }

    private T Write<T>(Func<SqliteTransaction, T> f)
    {
        lock (db.Gate)
        {
            using var tx = C.BeginTransaction();
            try
            {
                var r = f(tx);
                tx.Commit();
                return r;
            }
            catch (SqliteException ex)
            {
                tx.Rollback();
                throw Translate(ex);
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }

    private void Write(Action<SqliteTransaction> f) => Write<int>(tx => { f(tx); return 0; });

    private static Exception Translate(SqliteException ex)
    {
        var m = ex.Message;
        if (m.Contains("CAM_TRICH_YEU")) return new BusinessException("Tài liệu thuộc độ mật này không được ghi trích yếu (Điều 6 Nghị định 63/2026/NĐ-CP).", "TrichYeu");
        if (m.Contains("SO_DA_KHOA")) return new BusinessException("Sổ đăng ký đã khóa, không thể thay đổi văn bản trong sổ này.");
        if (m.Contains("SO_BAT_BIEN")) return new BusinessException("Không được thay đổi số thứ tự, số đến, năm hoặc quyển sổ của văn bản đã đăng ký.");
        if (ex.SqliteErrorCode == 19 && m.Contains("so_den")) return new BusinessException("Số đến này đã được dùng trong năm.", "SoDen");
        if (ex.SqliteErrorCode == 19 && m.Contains("UNIQUE")) return new BusinessException("Dữ liệu bị trùng với bản ghi đã có.");
        if (ex.SqliteErrorCode is 13) return new BusinessException("Ổ đĩa đã đầy, không ghi được dữ liệu. Hãy giải phóng dung lượng rồi thử lại.");
        if (ex.SqliteErrorCode is 11 or 26) return new DatabaseOpenException("Cơ sở dữ liệu bị hỏng. Hãy khôi phục từ bản sao lưu gần nhất.", ex);
        if (ex.SqliteErrorCode is 10) return new BusinessException("Lỗi đọc/ghi ổ đĩa, dữ liệu chưa được lưu. Hãy kiểm tra ổ đĩa (USB bị rút, ổ hỏng…) rồi thử lại.");
        if (ex.SqliteErrorCode is 8) return new BusinessException("Thư mục dữ liệu đang ở chế độ chỉ đọc, không ghi được. Hãy kiểm tra quyền ghi của thư mục dữ liệu.");
        if (ex.SqliteErrorCode is 5 or 6) return new BusinessException("Dữ liệu đang bận (có thể phần mềm đang mở ở nơi khác). Vui lòng thử lại sau giây lát.");
        return ex;
    }

    internal static Exception TranslateForTest(SqliteException ex) => Translate(ex);

    private SqliteCommand Cmd(string sql, SqliteTransaction? tx = null, params (string, object?)[] ps)
    {
        var cmd = C.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = tx;
        foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        return cmd;
    }

    private int Exec(string sql, SqliteTransaction? tx, params (string, object?)[] ps)
    {
        using var cmd = Cmd(sql, tx, ps);
        return cmd.ExecuteNonQuery();
    }

    private object? Scalar(string sql, SqliteTransaction? tx, params (string, object?)[] ps)
    {
        using var cmd = Cmd(sql, tx, ps);
        var r = cmd.ExecuteScalar();
        return r is DBNull ? null : r;
    }

    private long LastId(SqliteTransaction tx) => (long)Scalar("SELECT last_insert_rowid()", tx)!;

    private List<T> Query<T>(string sql, Func<SqliteDataReader, T> map, SqliteTransaction? tx = null, params (string, object?)[] ps)
    {
        using var cmd = Cmd(sql, tx, ps);
        using var r = cmd.ExecuteReader();
        var list = new List<T>();
        while (r.Read()) list.Add(map(r));
        return list;
    }

    internal static string D(DateOnly d) => d.ToString("yyyy-MM-dd", Inv);
    internal static string? D(DateOnly? d) => d?.ToString("yyyy-MM-dd", Inv);
    internal static string T(DateTime t) => t.ToString("yyyy-MM-ddTHH:mm:ss", Inv);
    internal static DateOnly PD(string s) => DateOnly.ParseExact(s, "yyyy-MM-dd", Inv);
    internal static DateTime PT(string s) => DateTime.ParseExact(s, "yyyy-MM-ddTHH:mm:ss", Inv);

    private static string? S(SqliteDataReader r, string c) { var i = r.GetOrdinal(c); return r.IsDBNull(i) ? null : r.GetString(i); }
    private static string S0(SqliteDataReader r, string c) => S(r, c) ?? "";
    private static int I(SqliteDataReader r, string c) => r.GetInt32(r.GetOrdinal(c));
    private static long L(SqliteDataReader r, string c) => r.GetInt64(r.GetOrdinal(c));
    private static long? LN(SqliteDataReader r, string c) { var i = r.GetOrdinal(c); return r.IsDBNull(i) ? null : r.GetInt64(i); }
    private static bool B(SqliteDataReader r, string c) => r.GetInt64(r.GetOrdinal(c)) != 0;

    internal static string EscapeLike(string s) => s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    // ================================================================== cấu hình
    public string? GetConfig(string key) => Read(() => (string?)Scalar("SELECT gia_tri FROM cau_hinh WHERE khoa=$k", null, ("$k", key)));

    public void SetConfig(string key, string? value, AuditEntry? audit = null) => Write(tx =>
    {
        Exec("INSERT INTO cau_hinh(khoa, gia_tri) VALUES($k,$v) ON CONFLICT(khoa) DO UPDATE SET gia_tri=excluded.gia_tri", tx, ("$k", key), ("$v", value));
        if (audit != null) Audit(tx, audit);
    });

    // ================================================================== độ mật
    private static DoMat MapDoMat(SqliteDataReader r) =>
        new(I(r, "id"), S0(r, "ten"), S0(r, "ky_hieu"), I(r, "muc"), B(r, "cam_trich_yeu"), B(r, "dang_dung"));

    public IReadOnlyList<DoMat> ListDoMat(bool includeInactive = false) => Read(() =>
        Query($"SELECT * FROM do_mat {(includeInactive ? "" : "WHERE dang_dung=1")} ORDER BY muc DESC, id", MapDoMat));

    public DoMat? GetDoMat(int id) => Read(() => Query("SELECT * FROM do_mat WHERE id=$id", MapDoMat, null, ("$id", id)).FirstOrDefault());

    public int SaveDoMat(DoMat d, AuditEntry audit) => Write(tx =>
    {
        int id;
        if (d.Id == 0)
        {
            Exec("INSERT INTO do_mat(ten, ky_hieu, muc, cam_trich_yeu, dang_dung) VALUES($t,$k,$m,$c,$d)", tx,
                ("$t", d.Ten), ("$k", d.KyHieu), ("$m", d.Muc), ("$c", d.CamTrichYeu ? 1 : 0), ("$d", d.DangDung ? 1 : 0));
            id = (int)LastId(tx);
        }
        else
        {
            Exec("UPDATE do_mat SET ten=$t, ky_hieu=$k, muc=$m, cam_trich_yeu=$c, dang_dung=$d WHERE id=$id", tx,
                ("$t", d.Ten), ("$k", d.KyHieu), ("$m", d.Muc), ("$c", d.CamTrichYeu ? 1 : 0), ("$d", d.DangDung ? 1 : 0), ("$id", d.Id));
            id = d.Id;
        }
        Audit(tx, audit with { BanGhiId = id });
        return id;
    });

    public int CountDocumentsWithTrichYeu(int doMatId) => Read(() => Convert.ToInt32(Scalar(
        "SELECT (SELECT count(*) FROM van_ban_di WHERE do_mat_id=$d AND trich_yeu IS NOT NULL) + (SELECT count(*) FROM van_ban_den WHERE do_mat_id=$d AND trich_yeu IS NOT NULL)",
        null, ("$d", doMatId)), Inv));

    // ================================================================== danh mục
    private static MucDanhMuc MapDm(SqliteDataReader r) => new()
    {
        Id = L(r, "id"), Nhom = S0(r, "nhom"), Ten = S0(r, "ten"), PhuDe = S(r, "phu_de"), ThuTu = I(r, "thu_tu"),
        DangDung = B(r, "dang_dung"), SoLanDung = I(r, "so_lan_dung"), LaDuLieuMau = B(r, "la_du_lieu_mau"),
    };

    public IReadOnlyList<MucDanhMuc> ListDanhMuc(string nhom, bool includeInactive = false, string? tuKhoa = null) => Read(() =>
    {
        var sql = new StringBuilder("SELECT * FROM danh_muc WHERE nhom=$n");
        var ps = new List<(string, object?)> { ("$n", nhom) };
        if (!includeInactive) sql.Append(" AND dang_dung=1");
        var terms = TextUtil.SearchTerms(tuKhoa);
        for (var i = 0; i < terms.Length; i++)
        {
            sql.Append($" AND (ten_khoa LIKE $q{i} ESCAPE '\\' OR bo_dau(phu_de) LIKE $q{i} ESCAPE '\\')");
            ps.Add(($"$q{i}", "%" + EscapeLike(terms[i]) + "%"));
        }
        sql.Append(nhom == NhomDanhMuc.LoaiVanBan ? " ORDER BY thu_tu, ten COLLATE VI" : " ORDER BY ten COLLATE VI");
        return Query(sql.ToString(), MapDm, null, [.. ps]);
    });

    public MucDanhMuc? FindDanhMuc(string nhom, string ten) => Read(() => FindDm(nhom, ten, null));

    private MucDanhMuc? FindDm(string nhom, string ten, SqliteTransaction? tx) =>
        Query("SELECT * FROM danh_muc WHERE nhom=$n AND ten_khoa=$k", MapDm, tx, ("$n", nhom), ("$k", Khoa(ten))).FirstOrDefault();

    private static string Khoa(string ten) => TextUtil.SearchKey(TextUtil.Clean(ten) ?? "");

    public long AddDanhMuc(MucDanhMuc m, AuditEntry audit) => Write(tx =>
    {
        if (m.ThuTu == 0 && m.Nhom == NhomDanhMuc.LoaiVanBan)
            m.ThuTu = Convert.ToInt32(Scalar("SELECT ifnull(max(thu_tu),0)+1 FROM danh_muc WHERE nhom=$n", tx, ("$n", m.Nhom)), Inv);
        Exec("INSERT INTO danh_muc(nhom, ten, ten_khoa, phu_de, thu_tu, dang_dung, so_lan_dung, la_du_lieu_mau) VALUES($n,$t,$k,$p,$o,$d,$s,$m)", tx,
            ("$n", m.Nhom), ("$t", m.Ten), ("$k", Khoa(m.Ten)), ("$p", m.PhuDe), ("$o", m.ThuTu), ("$d", m.DangDung ? 1 : 0), ("$s", m.SoLanDung), ("$m", m.LaDuLieuMau ? 1 : 0));
        var id = LastId(tx);
        Audit(tx, audit with { BanGhiId = id });
        return id;
    });

    public void UpdateDanhMuc(MucDanhMuc m, AuditEntry audit) => Write(tx =>
    {
        Exec("UPDATE danh_muc SET ten=$t, ten_khoa=$k, phu_de=$p, thu_tu=$o, dang_dung=$d WHERE id=$id", tx,
            ("$t", m.Ten), ("$k", Khoa(m.Ten)), ("$p", m.PhuDe), ("$o", m.ThuTu), ("$d", m.DangDung ? 1 : 0), ("$id", m.Id));
        Audit(tx, audit);
    });

    public bool IsDanhMucUsed(long id) => Read(() =>
    {
        var m = Query("SELECT * FROM danh_muc WHERE id=$id", MapDm, null, ("$id", id)).FirstOrDefault();
        return m != null && UsedTx(m, null);
    });

    public void DeleteDanhMuc(long id, AuditEntry audit) => Write(tx =>
    {
        Exec("DELETE FROM danh_muc WHERE id=$id", tx, ("$id", id));
        Audit(tx, audit);
    });

    public void TouchDanhMuc(string nhom, string ten, bool laDuLieuMau) => Write(tx =>
    {
        var m = FindDm(nhom, ten, tx);
        if (m != null)
        {
            Exec("UPDATE danh_muc SET so_lan_dung = so_lan_dung + 1, la_du_lieu_mau = CASE WHEN $m = 0 THEN 0 ELSE la_du_lieu_mau END WHERE id=$id", tx,
                ("$id", m.Id), ("$m", laDuLieuMau ? 1 : 0));
            return;
        }
        Exec("INSERT INTO danh_muc(nhom, ten, ten_khoa, so_lan_dung, la_du_lieu_mau) VALUES($n,$t,$k,1,$m)", tx,
            ("$n", nhom), ("$t", ten), ("$k", Khoa(ten)), ("$m", laDuLieuMau ? 1 : 0));
        var id = LastId(tx);
        if (!laDuLieuMau)
            Audit(tx, new AuditEntry("Thêm danh mục (tự động)", "danh_muc", id, $"{NhomDanhMuc.TenNhom[nhom]}: {ten}"));
    });

    public IReadOnlyList<string> Suggestions(string nhom, int limit = 200) => Read(() =>
        Query("SELECT ten FROM danh_muc WHERE nhom=$n AND dang_dung=1 ORDER BY so_lan_dung DESC, thu_tu, ten COLLATE VI LIMIT $l",
            r => r.GetString(0), null, ("$n", nhom), ("$l", limit)));

    // ================================================================== biểu mẫu
    public BieuMau? GetBieuMau(string ma) => ListBieuMau().FirstOrDefault(b => b.Ma == ma);

    public IReadOnlyList<BieuMau> ListBieuMau() => Read(() =>
        Query("SELECT dinh_nghia FROM bieu_mau ORDER BY hieu_luc_tu", r => JsonSerializer.Deserialize<BieuMau>(r.GetString(0))!));

    // ================================================================== sổ đăng ký
    private const string SoSql = """
        SELECT s.*,
          CASE s.loai WHEN 1 THEN (SELECT min(so_thu_tu) FROM van_ban_di WHERE so_dang_ky_id=s.id) ELSE (SELECT min(so_thu_tu) FROM van_ban_den WHERE so_dang_ky_id=s.id) END AS so_dau,
          CASE s.loai WHEN 1 THEN (SELECT max(so_thu_tu) FROM van_ban_di WHERE so_dang_ky_id=s.id) ELSE (SELECT max(so_thu_tu) FROM van_ban_den WHERE so_dang_ky_id=s.id) END AS so_cuoi,
          CASE s.loai WHEN 1 THEN (SELECT min(ngay_dang_ky) FROM van_ban_di WHERE so_dang_ky_id=s.id) ELSE (SELECT min(ngay_den) FROM van_ban_den WHERE so_dang_ky_id=s.id) END AS ngay_dau,
          CASE s.loai WHEN 1 THEN (SELECT max(ngay_dang_ky) FROM van_ban_di WHERE so_dang_ky_id=s.id) ELSE (SELECT max(ngay_den) FROM van_ban_den WHERE so_dang_ky_id=s.id) END AS ngay_cuoi,
          CASE s.loai WHEN 1 THEN (SELECT count(*) FROM van_ban_di WHERE so_dang_ky_id=s.id) ELSE (SELECT count(*) FROM van_ban_den WHERE so_dang_ky_id=s.id) END AS so_ban_ghi
        FROM so_dang_ky s
        """;

    private static SoDangKy MapSo(SqliteDataReader r) => new()
    {
        Id = L(r, "id"), Loai = (LoaiSo)I(r, "loai"), Nam = I(r, "nam"), QuyenSo = I(r, "quyen_so"),
        TenCoQuan = S0(r, "ten_co_quan"), CoQuanChuQuan = S(r, "co_quan_chu_quan"), MaBieuMau = S0(r, "ma_bieu_mau"),
        NgayMo = PD(S0(r, "ngay_mo")), DaKhoa = B(r, "da_khoa"), NgayKhoa = S(r, "ngay_khoa") is { } k ? PT(k) : null,
        SoDau = (int?)LN(r, "so_dau"), SoCuoi = (int?)LN(r, "so_cuoi"),
        NgayDau = S(r, "ngay_dau") is { } a ? PD(a) : null, NgayCuoi = S(r, "ngay_cuoi") is { } b ? PD(b) : null,
        SoBanGhi = I(r, "so_ban_ghi"),
    };

    public IReadOnlyList<SoDangKy> ListSoDangKy(LoaiSo? loai = null) => Read(() =>
        Query(SoSql + (loai is { } l ? $" WHERE s.loai={(int)l}" : "") + " ORDER BY s.nam DESC, s.loai, s.quyen_so DESC", MapSo));

    public SoDangKy? GetSoDangKy(long id) => Read(() => Query(SoSql + " WHERE s.id=$id", MapSo, null, ("$id", id)).FirstOrDefault());

    public SoDangKy? CurrentSoDangKy(LoaiSo loai, int nam) => Read(() => CurrentSo(loai, nam, null));

    private SoDangKy? CurrentSo(LoaiSo loai, int nam, SqliteTransaction? tx) =>
        Query(SoSql + " WHERE s.loai=$l AND s.nam=$n ORDER BY s.quyen_so DESC LIMIT 1", MapSo, tx, ("$l", (int)loai), ("$n", nam)).FirstOrDefault();

    public long AddSoDangKy(SoDangKy s, int soBatDau, AuditEntry audit) => Write(tx => AddSo(s, soBatDau, audit, tx));

    private long AddSo(SoDangKy s, int soBatDau, AuditEntry audit, SqliteTransaction tx)
    {
        Exec("""
            INSERT INTO so_dang_ky(loai, nam, quyen_so, ten_co_quan, co_quan_chu_quan, ma_bieu_mau, ngay_mo, da_khoa)
            VALUES($l,$n,$q,$t,$c,$m,$ngay,0)
            """, tx, ("$l", (int)s.Loai), ("$n", s.Nam), ("$q", s.QuyenSo), ("$t", s.TenCoQuan), ("$c", s.CoQuanChuQuan),
            ("$m", s.MaBieuMau), ("$ngay", D(s.NgayMo)));
        var id = LastId(tx);
        if (soBatDau > 1)
        {
            foreach (var ten in s.Loai == LoaiSo.Di ? new[] { BoDem.SoThuTu } : [BoDem.SoThuTu, BoDem.SoDen])
                Exec("INSERT INTO bo_dem(loai, nam, ten, gia_tri) VALUES($l,$n,$t,$v) ON CONFLICT(loai,nam,ten) DO UPDATE SET gia_tri=max(gia_tri, excluded.gia_tri)",
                    tx, ("$l", (int)s.Loai), ("$n", s.Nam), ("$t", ten), ("$v", soBatDau - 1));
        }
        Audit(tx, audit with { BanGhiId = id });
        return id;
    }

    public void SetSoDangKyLocked(long id, bool locked, AuditEntry audit) => Write(tx =>
    {
        Exec("UPDATE so_dang_ky SET da_khoa=$k, ngay_khoa=$t WHERE id=$id", tx,
            ("$k", locked ? 1 : 0), ("$t", locked ? T(clock.Now) : null), ("$id", id));
        Audit(tx, audit);
    });

    public int PeekNextNumber(LoaiSo loai, int nam, string boDem) => Read(() => Counter(loai, nam, boDem, null) + 1);

    private int Counter(LoaiSo loai, int nam, string ten, SqliteTransaction? tx) =>
        Convert.ToInt32(Scalar("SELECT gia_tri FROM bo_dem WHERE loai=$l AND nam=$n AND ten=$t", tx,
            ("$l", (int)loai), ("$n", nam), ("$t", ten)) ?? 0L, Inv);

    private void SetCounter(LoaiSo loai, int nam, string ten, int value, SqliteTransaction tx) =>
        Exec("INSERT INTO bo_dem(loai, nam, ten, gia_tri) VALUES($l,$n,$t,$v) ON CONFLICT(loai,nam,ten) DO UPDATE SET gia_tri=max(gia_tri, excluded.gia_tri)",
            tx, ("$l", (int)loai), ("$n", nam), ("$t", ten), ("$v", value));

    /// <summary>Quyển sổ đang mở để ghi; nếu năm chưa có sổ thì tự mở quyển 1.</summary>
    private SoDangKy OpenBook(LoaiSo loai, int nam, bool sample, SqliteTransaction tx)
    {
        var so = CurrentSo(loai, nam, tx);
        if (so == null)
        {
            var bms = Query("SELECT dinh_nghia FROM bieu_mau", r => JsonSerializer.Deserialize<BieuMau>(r.GetString(0))!, tx);
            var bm = SoDangKyService.ChonBieuMau(bms, loai, nam);
            so = new SoDangKy
            {
                Loai = loai, Nam = nam, QuyenSo = 1, MaBieuMau = bm.Ma, NgayMo = clock.Today,
                TenCoQuan = (string?)Scalar("SELECT gia_tri FROM cau_hinh WHERE khoa=$k", tx, ("$k", ConfigKeys.TenCoQuan)) ?? "",
                CoQuanChuQuan = (string?)Scalar("SELECT gia_tri FROM cau_hinh WHERE khoa=$k", tx, ("$k", ConfigKeys.CoQuanChuQuan)),
            };
            so.Id = AddSo(so, 1, new AuditEntry("Mở sổ (tự động)", "so_dang_ky", null,
                $"{so.TenHienThi}, biểu mẫu {bm.Ma}{(sample ? " – dữ liệu mẫu" : "")}"), tx);
            return so;
        }
        if (so.DaKhoa)
            throw new BusinessException($"{so.TenHienThi} đã khóa. Mở quyển sổ mới (menu Danh mục → Sổ đăng ký) để tiếp tục đăng ký.");
        return so;
    }
}
