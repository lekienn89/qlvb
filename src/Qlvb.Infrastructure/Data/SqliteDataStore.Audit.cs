using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Qlvb.Application;

namespace Qlvb.Infrastructure.Data;

public sealed partial class SqliteDataStore
{
    /// <summary>
    /// Ghi nhật ký có chuỗi băm: hash = SHA-256(prev_hash + nội dung bản ghi). Sửa/xóa một dòng sẽ làm đứt chuỗi
    /// và bị phát hiện khi kiểm tra. Nội dung nhật ký không bao giờ chứa mật khẩu, khóa hoặc trích yếu.
    /// </summary>
    private void Audit(SqliteTransaction tx, AuditEntry e)
    {
        var prev = (string?)Scalar("SELECT hash FROM nhat_ky ORDER BY id DESC LIMIT 1", tx) ?? GenesisHash;
        var tg = T(clock.Now);
        var nguoi = user.Name;
        var hash = Hash(prev, tg, e.HanhDong, e.DoiTuong, e.BanGhiId, nguoi, e.MoTa, e.ThayDoiJson);
        Exec("""
            INSERT INTO nhat_ky(thoi_gian, hanh_dong, doi_tuong, ban_ghi_id, nguoi, mo_ta, thay_doi, prev_hash, hash)
            VALUES($tg,$hd,$dt,$id,$ng,$mt,$td,$ph,$h)
            """, tx, ("$tg", tg), ("$hd", e.HanhDong), ("$dt", e.DoiTuong), ("$id", e.BanGhiId), ("$ng", nguoi),
            ("$mt", e.MoTa), ("$td", e.ThayDoiJson), ("$ph", prev), ("$h", hash));
    }

    private static string Hash(string prev, string tg, string hd, string dt, long? id, string ng, string mt, string? td)
    {
        var payload = JsonSerializer.Serialize(new object?[] { prev, tg, hd, dt, id, ng, mt, td });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    public void AppendAudit(AuditEntry e) => Write(tx => Audit(tx, e));

    public IReadOnlyList<AuditRecord> ListAudit(AuditFilter f) => Read(() =>
    {
        var w = new List<string>();
        var ps = new List<(string, object?)>();
        if (f.Tu is { } a) { w.Add("thoi_gian >= $a"); ps.Add(("$a", T(a))); }
        if (f.Den is { } b) { w.Add("thoi_gian <= $b"); ps.Add(("$b", T(b))); }
        if (!string.IsNullOrWhiteSpace(f.HanhDong)) { w.Add("bo_dau(hanh_dong) LIKE $h ESCAPE '\\'"); ps.Add(("$h", "%" + EscapeLike(Domain.TextUtil.SearchKey(f.HanhDong)) + "%")); }
        if (!string.IsNullOrWhiteSpace(f.DoiTuong)) { w.Add("doi_tuong = $d"); ps.Add(("$d", f.DoiTuong)); }
        ps.Add(("$l", f.Limit <= 0 ? 5000 : f.Limit));
        var sql = $"SELECT * FROM nhat_ky{(w.Count > 0 ? " WHERE " + string.Join(" AND ", w) : "")} ORDER BY id DESC LIMIT $l";
        return Query(sql, r => new AuditRecord
        {
            Id = L(r, "id"), ThoiGian = PT(S0(r, "thoi_gian")), HanhDong = S0(r, "hanh_dong"), DoiTuong = S0(r, "doi_tuong"),
            BanGhiId = LN(r, "ban_ghi_id"), NguoiThucHien = S0(r, "nguoi"), MoTa = S0(r, "mo_ta"), ThayDoi = S(r, "thay_doi"),
        }, null, [.. ps]);
    });

    public string? VerifyAuditChain() => Read(() =>
    {
        using var cmd = Cmd("SELECT * FROM nhat_ky ORDER BY id");
        using var r = cmd.ExecuteReader();
        var prev = GenesisHash;
        long n = 0, lastId = 0;
        while (r.Read())
        {
            n++;
            var id = L(r, "id");
            if (lastId != 0 && id != lastId + 1)
                return $"Nhật ký bị mất dòng giữa mục số {lastId} và {id}.";
            var ph = S0(r, "prev_hash");
            var h = Hash(ph, S0(r, "thoi_gian"), S0(r, "hanh_dong"), S0(r, "doi_tuong"), LN(r, "ban_ghi_id"), S0(r, "nguoi"), S0(r, "mo_ta"), S(r, "thay_doi"));
            if (ph != prev || h != S0(r, "hash"))
                return $"Nhật ký bị thay đổi tại mục số {id}.";
            prev = h;
            lastId = id;
        }
        return null;
    });
}
