using System.Text;
using Microsoft.Data.Sqlite;
using Qlvb.Application;
using Qlvb.Domain;

namespace Qlvb.Infrastructure.Data;

public sealed partial class SqliteDataStore
{
    // ================================================================== đọc
    private static void MapBase(SqliteDataReader r, VanBanBase v)
    {
        v.Id = L(r, "id"); v.SoDangKyId = L(r, "so_dang_ky_id"); v.Nam = I(r, "nam"); v.SoThuTu = I(r, "so_thu_tu");
        v.SoKyHieu = S0(r, "so_ky_hieu"); v.NgayVanBan = PD(S0(r, "ngay_van_ban")); v.TenLoai = S0(r, "ten_loai");
        v.TrichYeu = S(r, "trich_yeu"); v.DoMatId = I(r, "do_mat_id"); v.DoMatTen = S0(r, "do_mat_ten"); v.DoMatKyHieu = S0(r, "do_mat_ky_hieu");
        v.GhiChu = S(r, "ghi_chu"); v.TrangThai = (TrangThaiBanGhi)I(r, "trang_thai"); v.LyDoHuy = S(r, "ly_do_huy");
        v.ThoiGianHuy = S(r, "thoi_gian_huy") is { } h ? PT(h) : null; v.NguoiHuy = S(r, "nguoi_huy");
        v.NgayDangKy = PD(S0(r, "ngay_dang_ky")); v.TaoLuc = PT(S0(r, "tao_luc")); v.TaoBoi = S0(r, "tao_boi");
        v.CapNhatLuc = PT(S0(r, "cap_nhat_luc")); v.CapNhatBoi = S0(r, "cap_nhat_boi"); v.PhienBan = I(r, "phien_ban");
        v.LaDuLieuMau = B(r, "la_du_lieu_mau");
    }

    private static VanBanDi MapDi(SqliteDataReader r)
    {
        var v = new VanBanDi();
        MapBase(r, v);
        v.NguoiKy = S0(r, "nguoi_ky"); v.DonViLuu = S0(r, "don_vi_luu"); v.SoLuong = I(r, "so_luong");
        return v;
    }

    private static VanBanDen MapDen(SqliteDataReader r)
    {
        var v = new VanBanDen();
        MapBase(r, v);
        v.NgayDen = PD(S0(r, "ngay_den")); v.SoDen = I(r, "so_den"); v.CoQuanBanHanh = S0(r, "co_quan_ban_hanh");
        v.DonViNhan = S0(r, "don_vi_nhan"); v.DaKyNhan = B(r, "da_ky_nhan"); v.NguoiKyNhan = S(r, "nguoi_ky_nhan");
        v.NgayKyNhan = S(r, "ngay_ky_nhan") is { } k ? PD(k) : null;
        return v;
    }

    private void LoadNoiNhan(IReadOnlyCollection<VanBanDi> list, SqliteTransaction? tx = null)
    {
        if (list.Count == 0) return;
        var map = list.ToDictionary(v => v.Id);
        foreach (var chunk in map.Keys.Chunk(500))
        {
            var sql = $"SELECT * FROM van_ban_di_noi_nhan WHERE van_ban_id IN ({string.Join(",", chunk)}) ORDER BY van_ban_id, thu_tu";
            foreach (var (id, n) in Query(sql, r => (L(r, "van_ban_id"), new NoiNhanKyNhan
            {
                ThuTu = I(r, "thu_tu"), NoiNhan = S0(r, "noi_nhan"), DaKy = B(r, "da_ky"),
                NguoiKyNhan = S(r, "nguoi_ky_nhan"), NgayKyNhan = S(r, "ngay_ky_nhan") is { } d ? PD(d) : null,
            }), tx))
                map[id].NoiNhan.Add(n);
        }
    }

    public VanBanDi? GetDi(long id) => Read(() =>
    {
        var v = Query("SELECT * FROM van_ban_di WHERE id=$id", MapDi, null, ("$id", id)).FirstOrDefault();
        if (v != null) LoadNoiNhan([v]);
        return v;
    });

    public VanBanDen? GetDen(long id) => Read(() => Query("SELECT * FROM van_ban_den WHERE id=$id", MapDen, null, ("$id", id)).FirstOrDefault());

    // ================================================================== ghi
    private static string SearchKeyOf(VanBanBase v)
    {
        var parts = new List<string?> { v.SoThuTu.ToString(), TextUtil.So2(v.SoThuTu), v.SoKyHieu, v.TenLoai, v.TrichYeu, v.DoMatTen, v.GhiChu, v.LyDoHuy };
        switch (v)
        {
            case VanBanDi di:
                parts.Add(di.NguoiKy); parts.Add(di.DonViLuu);
                parts.AddRange(di.NoiNhan.Select(n => n.NoiNhan));
                parts.AddRange(di.NoiNhan.Select(n => n.NguoiKyNhan));
                break;
            case VanBanDen den:
                parts.Add(den.SoDen.ToString()); parts.Add(den.CoQuanBanHanh); parts.Add(den.DonViNhan); parts.Add(den.NguoiKyNhan);
                break;
        }
        return " " + string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => TextUtil.SearchKey(p!))) + " ";
    }

    private (string, object?)[] BaseParams(VanBanBase v) =>
    [
        ("$so_dang_ky_id", v.SoDangKyId), ("$nam", v.Nam), ("$so_thu_tu", v.SoThuTu), ("$so_ky_hieu", v.SoKyHieu),
        ("$ngay_van_ban", D(v.NgayVanBan)), ("$ten_loai", v.TenLoai), ("$trich_yeu", v.TrichYeu), ("$do_mat_id", v.DoMatId),
        ("$do_mat_ten", v.DoMatTen), ("$do_mat_ky_hieu", v.DoMatKyHieu), ("$ghi_chu", v.GhiChu), ("$ngay_dang_ky", D(v.NgayDangKy)),
        ("$tao_luc", T(v.TaoLuc)), ("$tao_boi", v.TaoBoi), ("$cap_nhat_luc", T(v.CapNhatLuc)), ("$cap_nhat_boi", v.CapNhatBoi),
        ("$phien_ban", v.PhienBan), ("$la_du_lieu_mau", v.LaDuLieuMau ? 1 : 0), ("$search_key", SearchKeyOf(v)),
        ("$khoa_skh", TextUtil.SearchKey(v.SoKyHieu)),
    ];

    public long InsertDi(VanBanDi v, Func<VanBanDi, AuditEntry> audit) => Write(tx =>
    {
        var so = OpenBook(LoaiSo.Di, v.Nam, v.LaDuLieuMau, tx);
        v.SoDangKyId = so.Id;
        v.SoThuTu = Counter(LoaiSo.Di, v.Nam, BoDem.SoThuTu, tx) + 1;
        SetCounter(LoaiSo.Di, v.Nam, BoDem.SoThuTu, v.SoThuTu, tx);
        Exec("""
            INSERT INTO van_ban_di(so_dang_ky_id, nam, so_thu_tu, so_ky_hieu, ngay_van_ban, ten_loai, trich_yeu, do_mat_id, do_mat_ten, do_mat_ky_hieu,
              nguoi_ky, don_vi_luu, so_luong, ghi_chu, trang_thai, ngay_dang_ky, tao_luc, tao_boi, cap_nhat_luc, cap_nhat_boi, phien_ban, la_du_lieu_mau, search_key, khoa_so_ky_hieu)
            VALUES($so_dang_ky_id, $nam, $so_thu_tu, $so_ky_hieu, $ngay_van_ban, $ten_loai, $trich_yeu, $do_mat_id, $do_mat_ten, $do_mat_ky_hieu,
              $nguoi_ky, $don_vi_luu, $so_luong, $ghi_chu, 0, $ngay_dang_ky, $tao_luc, $tao_boi, $cap_nhat_luc, $cap_nhat_boi, $phien_ban, $la_du_lieu_mau, $search_key, $khoa_skh)
            """, tx, [.. BaseParams(v), ("$nguoi_ky", v.NguoiKy), ("$don_vi_luu", v.DonViLuu), ("$so_luong", v.SoLuong)]);
        v.Id = LastId(tx);
        SaveNoiNhan(v, tx);
        Audit(tx, audit(v));
        return v.Id;
    });

    private void SaveNoiNhan(VanBanDi v, SqliteTransaction tx)
    {
        Exec("DELETE FROM van_ban_di_noi_nhan WHERE van_ban_id=$id", tx, ("$id", v.Id));
        var i = 0;
        foreach (var n in v.NoiNhan)
        {
            n.ThuTu = ++i;
            Exec("INSERT INTO van_ban_di_noi_nhan(van_ban_id, thu_tu, noi_nhan, da_ky, nguoi_ky_nhan, ngay_ky_nhan) VALUES($v,$t,$n,$d,$k,$ng)", tx,
                ("$v", v.Id), ("$t", n.ThuTu), ("$n", n.NoiNhan), ("$d", n.DaKy || n.NgayKyNhan != null ? 1 : 0), ("$k", n.NguoiKyNhan), ("$ng", D(n.NgayKyNhan)));
        }
    }

    public long InsertDen(VanBanDen v, Func<VanBanDen, AuditEntry> audit) => Write(tx =>
    {
        var so = OpenBook(LoaiSo.Den, v.Nam, v.LaDuLieuMau, tx);
        v.SoDangKyId = so.Id;
        v.SoThuTu = Counter(LoaiSo.Den, v.Nam, BoDem.SoThuTu, tx) + 1;
        SetCounter(LoaiSo.Den, v.Nam, BoDem.SoThuTu, v.SoThuTu, tx);
        var nextDen = Counter(LoaiSo.Den, v.Nam, BoDem.SoDen, tx) + 1;
        if (v.SoDen <= 0) v.SoDen = nextDen;
        else if (Convert.ToInt64(Scalar("SELECT count(*) FROM van_ban_den WHERE nam=$n AND so_den=$s", tx, ("$n", v.Nam), ("$s", v.SoDen)), Inv) > 0)
            throw new BusinessException($"Số đến {v.SoDen} đã được dùng trong năm {v.Nam}. Số đến tiếp theo là {nextDen}.", nameof(VanBanDen.SoDen));
        SetCounter(LoaiSo.Den, v.Nam, BoDem.SoDen, v.SoDen, tx);
        Exec("""
            INSERT INTO van_ban_den(so_dang_ky_id, nam, so_thu_tu, ngay_den, so_den, co_quan_ban_hanh, so_ky_hieu, ngay_van_ban, ten_loai, trich_yeu,
              do_mat_id, do_mat_ten, do_mat_ky_hieu, don_vi_nhan, da_ky_nhan, nguoi_ky_nhan, ngay_ky_nhan, ghi_chu, trang_thai, ngay_dang_ky,
              tao_luc, tao_boi, cap_nhat_luc, cap_nhat_boi, phien_ban, la_du_lieu_mau, search_key, khoa_so_ky_hieu)
            VALUES($so_dang_ky_id, $nam, $so_thu_tu, $ngay_den, $so_den, $co_quan, $so_ky_hieu, $ngay_van_ban, $ten_loai, $trich_yeu,
              $do_mat_id, $do_mat_ten, $do_mat_ky_hieu, $don_vi_nhan, $da_ky, $nguoi_ky_nhan, $ngay_ky_nhan, $ghi_chu, 0, $ngay_dang_ky,
              $tao_luc, $tao_boi, $cap_nhat_luc, $cap_nhat_boi, $phien_ban, $la_du_lieu_mau, $search_key, $khoa_skh)
            """, tx, [.. BaseParams(v), .. DenParams(v)]);
        v.Id = LastId(tx);
        Audit(tx, audit(v));
        return v.Id;
    });

    private static (string, object?)[] DenParams(VanBanDen v) =>
    [
        ("$ngay_den", D(v.NgayDen)), ("$so_den", v.SoDen), ("$co_quan", v.CoQuanBanHanh), ("$don_vi_nhan", v.DonViNhan),
        ("$da_ky", v.DaKyNhan || v.NgayKyNhan != null ? 1 : 0), ("$nguoi_ky_nhan", v.NguoiKyNhan), ("$ngay_ky_nhan", D(v.NgayKyNhan)),
    ];

    public void UpdateDi(VanBanDi v, AuditEntry audit) => Write(tx =>
    {
        var n = Exec("""
            UPDATE van_ban_di SET so_ky_hieu=$so_ky_hieu, ngay_van_ban=$ngay_van_ban, ten_loai=$ten_loai, trich_yeu=$trich_yeu, do_mat_id=$do_mat_id,
              do_mat_ten=$do_mat_ten, do_mat_ky_hieu=$do_mat_ky_hieu, nguoi_ky=$nguoi_ky, don_vi_luu=$don_vi_luu, so_luong=$so_luong, ghi_chu=$ghi_chu,
              ngay_dang_ky=$ngay_dang_ky, cap_nhat_luc=$cap_nhat_luc, cap_nhat_boi=$cap_nhat_boi, phien_ban=phien_ban+1, search_key=$search_key, khoa_so_ky_hieu=$khoa_skh
            WHERE id=$id AND phien_ban=$phien_ban
            """, tx, [.. BaseParams(v), ("$nguoi_ky", v.NguoiKy), ("$don_vi_luu", v.DonViLuu), ("$so_luong", v.SoLuong), ("$id", v.Id)]);
        if (n == 0) throw new ConcurrencyException();
        SaveNoiNhan(v, tx);
        Audit(tx, audit);
        v.PhienBan++;
    });

    public void UpdateDen(VanBanDen v, AuditEntry audit) => Write(tx =>
    {
        var n = Exec("""
            UPDATE van_ban_den SET ngay_den=$ngay_den, co_quan_ban_hanh=$co_quan, so_ky_hieu=$so_ky_hieu, ngay_van_ban=$ngay_van_ban, ten_loai=$ten_loai,
              trich_yeu=$trich_yeu, do_mat_id=$do_mat_id, do_mat_ten=$do_mat_ten, do_mat_ky_hieu=$do_mat_ky_hieu, don_vi_nhan=$don_vi_nhan,
              da_ky_nhan=$da_ky, nguoi_ky_nhan=$nguoi_ky_nhan, ngay_ky_nhan=$ngay_ky_nhan, ghi_chu=$ghi_chu, ngay_dang_ky=$ngay_dang_ky,
              cap_nhat_luc=$cap_nhat_luc, cap_nhat_boi=$cap_nhat_boi, phien_ban=phien_ban+1, search_key=$search_key, khoa_so_ky_hieu=$khoa_skh
            WHERE id=$id AND phien_ban=$phien_ban
            """, tx, [.. BaseParams(v), .. DenParams(v), ("$id", v.Id)]);
        if (n == 0) throw new ConcurrencyException();
        Audit(tx, audit);
        v.PhienBan++;
    });

    private static string Bang(LoaiSo l) => l == LoaiSo.Di ? "van_ban_di" : "van_ban_den";

    public void SetTrangThai(LoaiSo loai, long id, TrangThaiBanGhi trangThai, string? lyDo, string nguoi, DateTime luc, int phienBan, AuditEntry audit) => Write(tx =>
    {
        var huy = trangThai == TrangThaiBanGhi.DaHuy;
        var n = Exec($"""
            UPDATE {Bang(loai)} SET trang_thai=$t, ly_do_huy=$l, thoi_gian_huy=$tg, nguoi_huy=$ng, cap_nhat_luc=$c, cap_nhat_boi=$cb, phien_ban=phien_ban+1
            WHERE id=$id AND phien_ban=$pb
            """, tx, ("$t", (int)trangThai), ("$l", huy ? lyDo : null), ("$tg", huy ? T(luc) : null), ("$ng", huy ? nguoi : null),
            ("$c", T(luc)), ("$cb", nguoi), ("$id", id), ("$pb", phienBan));
        if (n == 0) throw new ConcurrencyException();
        RefreshSearchKey(loai, id, tx);
        Audit(tx, audit);
    });

    private void RefreshSearchKey(LoaiSo loai, long id, SqliteTransaction tx)
    {
        VanBanBase? v;
        if (loai == LoaiSo.Di)
        {
            var di = Query("SELECT * FROM van_ban_di WHERE id=$id", MapDi, tx, ("$id", id)).FirstOrDefault();
            if (di != null) LoadNoiNhan([di], tx);
            v = di;
        }
        else v = Query("SELECT * FROM van_ban_den WHERE id=$id", MapDen, tx, ("$id", id)).FirstOrDefault();
        if (v == null) return;
        // Cập nhật search_key không làm thay đổi phiên bản.
        Exec($"UPDATE {Bang(loai)} SET search_key=$k WHERE id=$id", tx, ("$k", SearchKeyOf(v)), ("$id", id));
    }

    public bool HardDelete(LoaiSo loai, long id, Func<bool, AuditEntry> audit) => Write(tx =>
    {
        var row = Query($"SELECT nam, so_thu_tu{(loai == LoaiSo.Den ? ", so_den" : "")} FROM {Bang(loai)} WHERE id=$id",
            r => (Nam: r.GetInt32(0), Stt: r.GetInt32(1), SoDen: loai == LoaiSo.Den ? r.GetInt32(2) : 0), tx, ("$id", id)).FirstOrDefault();
        if (row.Nam == 0) throw new BusinessException("Văn bản không còn tồn tại.");
        Exec($"DELETE FROM {Bang(loai)} WHERE id=$id", tx, ("$id", id));
        // Chỉ thu hồi số CUỐI CÙNG đã cấp: xóa văn bản ở giữa thì số đó để trống, không làm xáo trộn số của văn bản khác.
        var thuHoi = Counter(loai, row.Nam, BoDem.SoThuTu, tx) == row.Stt;
        if (thuHoi) LuiBoDem(loai, row.Nam, BoDem.SoThuTu, row.Stt, tx);
        if (loai == LoaiSo.Den && Counter(loai, row.Nam, BoDem.SoDen, tx) == row.SoDen) LuiBoDem(loai, row.Nam, BoDem.SoDen, row.SoDen, tx);
        Audit(tx, audit(thuHoi));
        return thuHoi;
    });

    private void LuiBoDem(LoaiSo loai, int nam, string ten, int so, SqliteTransaction tx) =>
        Exec("UPDATE bo_dem SET gia_tri=$v WHERE loai=$l AND nam=$n AND ten=$t AND gia_tri=$so", tx,
            ("$v", so - 1), ("$l", (int)loai), ("$n", nam), ("$t", ten), ("$so", so));

    // ================================================================== tìm kiếm
    private static readonly Dictionary<string, string> SortDi = new()
    {
        ["so_thu_tu"] = "nam {0}, so_thu_tu {0}", ["ngay_van_ban"] = "ngay_van_ban {0}, so_thu_tu {0}", ["ngay_dang_ky"] = "ngay_dang_ky {0}, so_thu_tu {0}",
        ["so_ky_hieu"] = "so_ky_hieu COLLATE VI {0}", ["ten_loai"] = "ten_loai COLLATE VI {0}, so_thu_tu {0}", ["do_mat"] = "do_mat_ten COLLATE VI {0}, so_thu_tu {0}",
        ["nguoi_ky"] = "nguoi_ky COLLATE VI {0}, so_thu_tu {0}", ["don_vi_luu"] = "don_vi_luu COLLATE VI {0}, so_thu_tu {0}",
    };

    private static readonly Dictionary<string, string> SortDen = new()
    {
        ["so_thu_tu"] = "nam {0}, so_thu_tu {0}", ["so_den"] = "nam {0}, so_den {0}", ["ngay_den"] = "ngay_den {0}, so_thu_tu {0}",
        ["ngay_van_ban"] = "ngay_van_ban {0}, so_thu_tu {0}", ["so_ky_hieu"] = "so_ky_hieu COLLATE VI {0}",
        ["ten_loai"] = "ten_loai COLLATE VI {0}, so_thu_tu {0}", ["do_mat"] = "do_mat_ten COLLATE VI {0}, so_thu_tu {0}",
        ["co_quan_ban_hanh"] = "co_quan_ban_hanh COLLATE VI {0}, so_thu_tu {0}", ["don_vi_nhan"] = "don_vi_nhan COLLATE VI {0}, so_thu_tu {0}",
    };

    private static (string Where, List<(string, object?)> Ps) BuildWhere(SearchCriteria c)
    {
        var w = new List<string>();
        var ps = new List<(string, object?)>();
        var di = c.Loai == LoaiSo.Di;
        void Like(string col, string? value, string name)
        {
            var terms = TextUtil.SearchTerms(value);
            for (var i = 0; i < terms.Length; i++)
            {
                w.Add($"bo_dau({col}) LIKE ${name}{i} ESCAPE '\\'");
                ps.Add(($"${name}{i}", "%" + EscapeLike(terms[i]) + "%"));
            }
        }
        if (c.Nam is { } nam) { w.Add("nam = $nam"); ps.Add(("$nam", nam)); }
        if (c.SoDangKyId is { } sid) { w.Add("so_dang_ky_id = $sid"); ps.Add(("$sid", sid)); }
        if (c.SoThuTuTu is { } a) { w.Add("so_thu_tu >= $stt1"); ps.Add(("$stt1", a)); }
        if (c.SoThuTuDen is { } b) { w.Add("so_thu_tu <= $stt2"); ps.Add(("$stt2", b)); }
        if (!di && c.SoDen is { } sd) { w.Add("so_den = $sd"); ps.Add(("$sd", sd)); }
        var ngay = di ? "ngay_van_ban" : "ngay_den";
        if (c.TuNgay is { } t1) { w.Add($"{ngay} >= $n1"); ps.Add(("$n1", D(t1))); }
        if (c.DenNgay is { } t2) { w.Add($"{ngay} <= $n2"); ps.Add(("$n2", D(t2))); }
        if (c.NgayVanBanTu is { } v1) { w.Add("ngay_van_ban >= $v1"); ps.Add(("$v1", D(v1))); }
        if (c.NgayVanBanDen is { } v2) { w.Add("ngay_van_ban <= $v2"); ps.Add(("$v2", D(v2))); }
        if (c.DoMatId is { } dm) { w.Add("do_mat_id = $dm"); ps.Add(("$dm", dm)); }
        Like("so_ky_hieu", c.SoKyHieu, "skh");
        Like("ten_loai", c.TenLoai, "tl");
        // Trích yếu của tài liệu cấm trích yếu luôn NULL nên không thể bị tìm thấy qua trường này.
        Like("trich_yeu", c.TrichYeu, "ty");
        if (di)
        {
            Like("nguoi_ky", c.NguoiKy, "nk");
            Like("don_vi_luu", c.DonViLuu, "dvl");
            var terms = TextUtil.SearchTerms(c.NoiNhan);
            for (var i = 0; i < terms.Length; i++)
            {
                w.Add($"EXISTS (SELECT 1 FROM van_ban_di_noi_nhan nn WHERE nn.van_ban_id = van_ban_di.id AND bo_dau(nn.noi_nhan) LIKE $nn{i} ESCAPE '\\')");
                ps.Add(($"$nn{i}", "%" + EscapeLike(terms[i]) + "%"));
            }
        }
        else
        {
            Like("co_quan_ban_hanh", c.CoQuanBanHanh, "cq");
            Like("don_vi_nhan", c.DonViNhan, "dvn");
        }
        var keys = TextUtil.SearchTerms(c.TuKhoa);
        for (var i = 0; i < keys.Length; i++)
        {
            w.Add($"search_key LIKE $k{i} ESCAPE '\\'");
            ps.Add(($"$k{i}", "%" + EscapeLike(keys[i]) + "%"));
        }
        switch (c.TrangThai)
        {
            case LocTrangThai.HieuLuc: w.Add("trang_thai = 0"); break;
            case LocTrangThai.DaHuy: w.Add("trang_thai = 1"); break;
        }
        return (w.Count == 0 ? "" : " WHERE " + string.Join(" AND ", w), ps);
    }

    public PagedResult<VanBanBase> Search(SearchCriteria c) => Read(() =>
    {
        var di = c.Loai == LoaiSo.Di;
        var table = Bang(c.Loai);
        var (where, ps) = BuildWhere(c);
        var total = Convert.ToInt32(Scalar($"SELECT count(*) FROM {table}{where}", null, [.. ps]), Inv);
        var sorts = di ? SortDi : SortDen;
        var order = string.Format(sorts.GetValueOrDefault(c.SapXep) ?? sorts["so_thu_tu"], c.Giam ? "DESC" : "ASC");
        var sql = new StringBuilder($"SELECT * FROM {table}{where} ORDER BY {order}, id {(c.Giam ? "DESC" : "ASC")}");
        if (c.Limit > 0)
        {
            sql.Append(" LIMIT $lim OFFSET $off");
            ps.Add(("$lim", c.Limit));
            ps.Add(("$off", Math.Max(0, c.Offset)));
        }
        if (di)
        {
            var list = Query(sql.ToString(), MapDi, null, [.. ps]);
            LoadNoiNhan(list);
            return new PagedResult<VanBanBase>(list, total);
        }
        return new PagedResult<VanBanBase>(Query<VanBanBase>(sql.ToString(), MapDen, null, [.. ps]), total);
    });

    public IReadOnlyList<VanBanBase> FindDuplicates(VanBanBase v) => Read(() =>
    {
        var skh = TextUtil.SearchKey(v.SoKyHieu);
        if (skh.Length == 0) return (IReadOnlyList<VanBanBase>)[];
        if (v is VanBanDi di)
        {
            // Cùng số, ký hiệu trong cùng năm.
            var list = Query("SELECT * FROM van_ban_di WHERE trang_thai=0 AND id<>$id AND nam=$n AND khoa_so_ky_hieu=$s LIMIT 20", MapDi, null,
                ("$id", v.Id), ("$n", v.NgayDangKy.Year), ("$s", skh));
            LoadNoiNhan(list);
            return list;
        }
        var den = (VanBanDen)v;
        // Cùng cơ quan ban hành + số ký hiệu + ngày văn bản (văn bản đến có thể đã được đăng ký ở năm trước).
        return Query<VanBanBase>("SELECT * FROM van_ban_den WHERE trang_thai=0 AND id<>$id AND khoa_so_ky_hieu=$s AND ngay_van_ban=$d AND bo_dau(co_quan_ban_hanh)=$c LIMIT 20",
            MapDen, null, ("$id", v.Id), ("$s", skh), ("$d", D(v.NgayVanBan)), ("$c", TextUtil.SearchKey(den.CoQuanBanHanh)));
    });

    public IReadOnlyList<VanBanBase> Recent(int limit) => Read(() =>
    {
        var di = Query("SELECT * FROM van_ban_di WHERE trang_thai=0 ORDER BY tao_luc DESC, id DESC LIMIT $l", MapDi, null, ("$l", limit));
        LoadNoiNhan(di);
        var den = Query("SELECT * FROM van_ban_den WHERE trang_thai=0 ORDER BY tao_luc DESC, id DESC LIMIT $l", MapDen, null, ("$l", limit));
        return (IReadOnlyList<VanBanBase>)di.Cast<VanBanBase>().Concat(den).OrderByDescending(x => x.TaoLuc).ThenByDescending(x => x.Id).Take(limit).ToList();
    });

    public IReadOnlyList<int> Years(LoaiSo? loai = null) => Read(() =>
    {
        var parts = new List<string>();
        if (loai is null or LoaiSo.Di) parts.Add("SELECT nam FROM van_ban_di");
        if (loai is null or LoaiSo.Den) parts.Add("SELECT nam FROM van_ban_den");
        parts.Add(loai is null ? "SELECT nam FROM so_dang_ky" : $"SELECT nam FROM so_dang_ky WHERE loai={(int)loai}");
        var years = Query($"SELECT DISTINCT nam FROM ({string.Join(" UNION ", parts)}) ORDER BY nam DESC", r => r.GetInt32(0));
        if (!years.Contains(clock.Today.Year)) years.Insert(0, clock.Today.Year);
        return (IReadOnlyList<int>)years.OrderByDescending(x => x).ToList();
    });

    // ================================================================== thống kê
    public IReadOnlyList<DemTheoNhom> ThongKe(TieuChiThongKe tieuChi, ThongKeFilter f) => Read(() =>
    {
        string? Col(LoaiSo l) => tieuChi switch
        {
            TieuChiThongKe.Nam => "CAST(nam AS TEXT)",
            TieuChiThongKe.Thang => "'Tháng ' || substr(ngay_dang_ky, 6, 2) || '/' || substr(ngay_dang_ky, 1, 4)",
            TieuChiThongKe.DoMat => "do_mat_ten",
            TieuChiThongKe.LoaiVanBan => "ten_loai",
            TieuChiThongKe.NguoiKy => l == LoaiSo.Di ? "nguoi_ky" : null,
            TieuChiThongKe.CoQuanBanHanh => l == LoaiSo.Den ? "co_quan_ban_hanh" : null,
            TieuChiThongKe.DonVi => l == LoaiSo.Di ? "don_vi_luu" : "don_vi_nhan",
            _ => null,
        };
        var ps = new List<(string, object?)>();
        var w = new List<string>();
        if (!f.BaoGomDaHuy) w.Add("trang_thai = 0");
        if (f.Nam is { } n) { w.Add("nam = $nam"); ps.Add(("$nam", n)); }
        if (f.TuNgay is { } a) { w.Add("ngay_dang_ky >= $a"); ps.Add(("$a", D(a))); }
        if (f.DenNgay is { } b) { w.Add("ngay_dang_ky <= $b"); ps.Add(("$b", D(b))); }
        if (f.DoMatId is { } dm) { w.Add("do_mat_id = $dm"); ps.Add(("$dm", dm)); }
        var where = w.Count == 0 ? "" : " WHERE " + string.Join(" AND ", w);
        var parts = new List<string>();
        foreach (var l in new[] { LoaiSo.Di, LoaiSo.Den })
        {
            if (f.Loai is { } fl && fl != l) continue;
            var col = Col(l);
            if (col != null) parts.Add($"SELECT {col} AS nhom, ngay_dang_ky, nam, do_mat_ten FROM {Bang(l)}{where}");
        }
        if (parts.Count == 0) return (IReadOnlyList<DemTheoNhom>)[];
        var order = tieuChi switch
        {
            TieuChiThongKe.Nam => "nhom DESC",
            TieuChiThongKe.Thang => "min(ngay_dang_ky)",
            TieuChiThongKe.DoMat => "so_luong DESC",
            _ => "so_luong DESC, nhom COLLATE VI",
        };
        return Query($"SELECT nhom, count(*) AS so_luong, min(ngay_dang_ky) FROM ({string.Join(" UNION ALL ", parts)}) GROUP BY nhom ORDER BY {order}",
            r => new DemTheoNhom(r.IsDBNull(0) ? "(không có)" : r.GetString(0), r.GetInt32(1)), null, [.. ps]);
    });

    // ================================================================== dữ liệu mẫu
    public int DeleteSampleData(AuditEntry audit) => Write(tx =>
    {
        var locked = Convert.ToInt64(Scalar("""
            SELECT count(*) FROM so_dang_ky s WHERE s.da_khoa=1 AND (EXISTS(SELECT 1 FROM van_ban_di WHERE so_dang_ky_id=s.id AND la_du_lieu_mau=1)
              OR EXISTS(SELECT 1 FROM van_ban_den WHERE so_dang_ky_id=s.id AND la_du_lieu_mau=1))
            """, tx), Inv);
        if (locked > 0) throw new BusinessException("Có dữ liệu mẫu nằm trong sổ đã khóa. Mở khóa sổ đó trước khi xóa dữ liệu mẫu.");
        var n = Exec("DELETE FROM van_ban_di WHERE la_du_lieu_mau=1", tx) + Exec("DELETE FROM van_ban_den WHERE la_du_lieu_mau=1", tx);
        // Năm không còn văn bản thật: xóa sổ trống và đặt lại bộ đếm để số thật bắt đầu từ 01.
        Exec("""
            DELETE FROM so_dang_ky WHERE
              (loai=1 AND NOT EXISTS(SELECT 1 FROM van_ban_di d WHERE d.nam=so_dang_ky.nam)) OR
              (loai=2 AND NOT EXISTS(SELECT 1 FROM van_ban_den d WHERE d.nam=so_dang_ky.nam))
            """, tx);
        Exec("""
            DELETE FROM bo_dem WHERE
              (loai=1 AND NOT EXISTS(SELECT 1 FROM van_ban_di d WHERE d.nam=bo_dem.nam)) OR
              (loai=2 AND NOT EXISTS(SELECT 1 FROM van_ban_den d WHERE d.nam=bo_dem.nam))
            """, tx);
        var used = Query("SELECT id FROM danh_muc WHERE la_du_lieu_mau=1", r => r.GetInt64(0), tx);
        foreach (var id in used)
        {
            var m = Query("SELECT * FROM danh_muc WHERE id=$id", MapDm, tx, ("$id", id)).First();
            if (!UsedTx(m, tx)) Exec("DELETE FROM danh_muc WHERE id=$id", tx, ("$id", id));
        }
        Exec("INSERT INTO cau_hinh(khoa, gia_tri) VALUES($k,'0') ON CONFLICT(khoa) DO UPDATE SET gia_tri='0'", tx, ("$k", ConfigKeys.DaCoDuLieuMau));
        Audit(tx, audit with { MoTa = $"{audit.MoTa} ({n} văn bản)" });
        return n;
    });

    private bool UsedTx(MucDanhMuc m, SqliteTransaction? tx)
    {
        var sql = m.Nhom switch
        {
            NhomDanhMuc.LoaiVanBan => "SELECT EXISTS(SELECT 1 FROM van_ban_di WHERE bo_dau(ten_loai)=$k) OR EXISTS(SELECT 1 FROM van_ban_den WHERE bo_dau(ten_loai)=$k)",
            NhomDanhMuc.NguoiKy => "SELECT EXISTS(SELECT 1 FROM van_ban_di WHERE bo_dau(nguoi_ky)=$k)",
            NhomDanhMuc.DonVi => "SELECT EXISTS(SELECT 1 FROM van_ban_di WHERE bo_dau(don_vi_luu)=$k) OR EXISTS(SELECT 1 FROM van_ban_den WHERE bo_dau(don_vi_nhan)=$k)",
            NhomDanhMuc.NoiNhan => "SELECT EXISTS(SELECT 1 FROM van_ban_di_noi_nhan WHERE bo_dau(noi_nhan)=$k)",
            NhomDanhMuc.CoQuanBanHanh => "SELECT EXISTS(SELECT 1 FROM van_ban_den WHERE bo_dau(co_quan_ban_hanh)=$k)",
            _ => "SELECT 0",
        };
        return Convert.ToInt64(Scalar(sql, tx, ("$k", Khoa(m.Ten))), Inv) != 0;
    }
}
