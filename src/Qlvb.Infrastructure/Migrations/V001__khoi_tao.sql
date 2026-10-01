-- QLVB – lược đồ cơ sở dữ liệu phiên bản 1
-- Căn cứ: Phụ lục III Nghị định 63/2026/NĐ-CP (thay thế theo Công văn 2663/VPCP-NC)

CREATE TABLE cau_hinh (
    khoa    TEXT PRIMARY KEY,
    gia_tri TEXT
);

CREATE TABLE bieu_mau (
    ma          TEXT PRIMARY KEY,
    loai        INTEGER NOT NULL CHECK (loai IN (1, 2)),
    phien_ban   INTEGER NOT NULL,
    hieu_luc_tu TEXT NOT NULL,
    can_cu      TEXT NOT NULL,
    dinh_nghia  TEXT NOT NULL            -- JSON: tiêu đề, cột, hướng dẫn ghi
);

CREATE TABLE do_mat (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    ten            TEXT NOT NULL UNIQUE,
    ky_hieu        TEXT NOT NULL DEFAULT '',
    muc            INTEGER NOT NULL,
    cam_trich_yeu  INTEGER NOT NULL DEFAULT 0 CHECK (cam_trich_yeu IN (0, 1)),
    dang_dung      INTEGER NOT NULL DEFAULT 1 CHECK (dang_dung IN (0, 1))
);

CREATE TABLE danh_muc (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    nhom          TEXT NOT NULL,
    ten           TEXT NOT NULL,
    ten_khoa      TEXT NOT NULL,          -- khóa so sánh không dấu, chữ thường
    phu_de        TEXT,                   -- chữ viết tắt (loại văn bản) / chức vụ (người ký)
    thu_tu        INTEGER NOT NULL DEFAULT 0,
    dang_dung     INTEGER NOT NULL DEFAULT 1 CHECK (dang_dung IN (0, 1)),
    so_lan_dung   INTEGER NOT NULL DEFAULT 0,
    la_du_lieu_mau INTEGER NOT NULL DEFAULT 0,
    UNIQUE (nhom, ten_khoa)
);

CREATE TABLE so_dang_ky (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    loai            INTEGER NOT NULL CHECK (loai IN (1, 2)),
    nam             INTEGER NOT NULL,
    quyen_so        INTEGER NOT NULL,
    ten_co_quan     TEXT NOT NULL,
    co_quan_chu_quan TEXT,
    ma_bieu_mau     TEXT NOT NULL REFERENCES bieu_mau(ma),
    ngay_mo         TEXT NOT NULL,
    da_khoa         INTEGER NOT NULL DEFAULT 0 CHECK (da_khoa IN (0, 1)),
    ngay_khoa       TEXT,
    UNIQUE (loai, nam, quyen_so)
);

-- Bộ đếm số đã cấp. Không bao giờ giảm: số của bản ghi bị xóa không được cấp lại.
CREATE TABLE bo_dem (
    loai    INTEGER NOT NULL,
    nam     INTEGER NOT NULL,
    ten     TEXT NOT NULL,
    gia_tri INTEGER NOT NULL,
    PRIMARY KEY (loai, nam, ten)
);

CREATE TABLE van_ban_di (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    so_dang_ky_id   INTEGER NOT NULL REFERENCES so_dang_ky(id),
    nam             INTEGER NOT NULL,
    so_thu_tu       INTEGER NOT NULL CHECK (so_thu_tu > 0),
    so_ky_hieu      TEXT NOT NULL,
    ngay_van_ban    TEXT NOT NULL,
    ten_loai        TEXT NOT NULL,
    trich_yeu       TEXT,
    do_mat_id       INTEGER NOT NULL REFERENCES do_mat(id),
    do_mat_ten      TEXT NOT NULL,
    do_mat_ky_hieu  TEXT NOT NULL DEFAULT '',
    nguoi_ky        TEXT NOT NULL,
    don_vi_luu      TEXT NOT NULL,
    so_luong        INTEGER NOT NULL CHECK (so_luong BETWEEN 1 AND 9999),
    ghi_chu         TEXT,
    trang_thai      INTEGER NOT NULL DEFAULT 0 CHECK (trang_thai IN (0, 1)),
    ly_do_huy       TEXT,
    thoi_gian_huy   TEXT,
    nguoi_huy       TEXT,
    ngay_dang_ky    TEXT NOT NULL,
    tao_luc         TEXT NOT NULL,
    tao_boi         TEXT NOT NULL,
    cap_nhat_luc    TEXT NOT NULL,
    cap_nhat_boi    TEXT NOT NULL,
    phien_ban       INTEGER NOT NULL DEFAULT 1,
    la_du_lieu_mau  INTEGER NOT NULL DEFAULT 0,
    search_key      TEXT NOT NULL DEFAULT '',
    UNIQUE (nam, so_thu_tu)
);

CREATE TABLE van_ban_di_noi_nhan (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    van_ban_id     INTEGER NOT NULL REFERENCES van_ban_di(id) ON DELETE CASCADE,
    thu_tu         INTEGER NOT NULL,
    noi_nhan       TEXT NOT NULL,
    da_ky          INTEGER NOT NULL DEFAULT 0,
    nguoi_ky_nhan  TEXT,
    ngay_ky_nhan   TEXT,
    UNIQUE (van_ban_id, thu_tu)
);

CREATE TABLE van_ban_den (
    id                INTEGER PRIMARY KEY AUTOINCREMENT,
    so_dang_ky_id     INTEGER NOT NULL REFERENCES so_dang_ky(id),
    nam               INTEGER NOT NULL,
    so_thu_tu         INTEGER NOT NULL CHECK (so_thu_tu > 0),
    ngay_den          TEXT NOT NULL,
    so_den            INTEGER NOT NULL CHECK (so_den > 0),
    co_quan_ban_hanh  TEXT NOT NULL,
    so_ky_hieu        TEXT NOT NULL,
    ngay_van_ban      TEXT NOT NULL,
    ten_loai          TEXT NOT NULL,
    trich_yeu         TEXT,
    do_mat_id         INTEGER NOT NULL REFERENCES do_mat(id),
    do_mat_ten        TEXT NOT NULL,
    do_mat_ky_hieu    TEXT NOT NULL DEFAULT '',
    don_vi_nhan       TEXT NOT NULL,
    da_ky_nhan        INTEGER NOT NULL DEFAULT 0,
    nguoi_ky_nhan     TEXT,
    ngay_ky_nhan      TEXT,
    ghi_chu           TEXT,
    trang_thai        INTEGER NOT NULL DEFAULT 0 CHECK (trang_thai IN (0, 1)),
    ly_do_huy         TEXT,
    thoi_gian_huy     TEXT,
    nguoi_huy         TEXT,
    ngay_dang_ky      TEXT NOT NULL,
    tao_luc           TEXT NOT NULL,
    tao_boi           TEXT NOT NULL,
    cap_nhat_luc      TEXT NOT NULL,
    cap_nhat_boi      TEXT NOT NULL,
    phien_ban         INTEGER NOT NULL DEFAULT 1,
    la_du_lieu_mau    INTEGER NOT NULL DEFAULT 0,
    search_key        TEXT NOT NULL DEFAULT '',
    UNIQUE (nam, so_thu_tu),
    UNIQUE (nam, so_den)
);

CREATE INDEX ix_di_so ON van_ban_di(so_dang_ky_id, so_thu_tu);
CREATE INDEX ix_di_ngay ON van_ban_di(ngay_van_ban);
CREATE INDEX ix_den_so ON van_ban_den(so_dang_ky_id, so_thu_tu);
CREATE INDEX ix_den_ngay ON van_ban_den(ngay_den);
CREATE INDEX ix_noi_nhan_vb ON van_ban_di_noi_nhan(van_ban_id);

CREATE TABLE nhat_ky (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    thoi_gian    TEXT NOT NULL,
    hanh_dong    TEXT NOT NULL,
    doi_tuong    TEXT NOT NULL,
    ban_ghi_id   INTEGER,
    nguoi        TEXT NOT NULL,
    mo_ta        TEXT NOT NULL,
    thay_doi     TEXT,
    prev_hash    TEXT NOT NULL,
    hash         TEXT NOT NULL
);
CREATE INDEX ix_nhat_ky_tg ON nhat_ky(thoi_gian);

-- ---------------------------------------------------------------- ràng buộc an toàn
-- Nhật ký chỉ được thêm, không được sửa hoặc xóa.
CREATE TRIGGER tg_nhat_ky_no_update BEFORE UPDATE ON nhat_ky
BEGIN SELECT RAISE(ABORT, 'Nhật ký không được sửa'); END;
CREATE TRIGGER tg_nhat_ky_no_delete BEFORE DELETE ON nhat_ky
BEGIN SELECT RAISE(ABORT, 'Nhật ký không được xóa'); END;

-- Tài liệu thuộc độ mật cấm trích yếu (mặc định TUYỆT MẬT) không được lưu trích yếu.
CREATE TRIGGER tg_di_tm_ins BEFORE INSERT ON van_ban_di
WHEN NEW.trich_yeu IS NOT NULL AND (SELECT cam_trich_yeu FROM do_mat WHERE id = NEW.do_mat_id) = 1
BEGIN SELECT RAISE(ABORT, 'CAM_TRICH_YEU'); END;
CREATE TRIGGER tg_di_tm_upd BEFORE UPDATE ON van_ban_di
WHEN NEW.trich_yeu IS NOT NULL AND (SELECT cam_trich_yeu FROM do_mat WHERE id = NEW.do_mat_id) = 1
BEGIN SELECT RAISE(ABORT, 'CAM_TRICH_YEU'); END;
CREATE TRIGGER tg_den_tm_ins BEFORE INSERT ON van_ban_den
WHEN NEW.trich_yeu IS NOT NULL AND (SELECT cam_trich_yeu FROM do_mat WHERE id = NEW.do_mat_id) = 1
BEGIN SELECT RAISE(ABORT, 'CAM_TRICH_YEU'); END;
CREATE TRIGGER tg_den_tm_upd BEFORE UPDATE ON van_ban_den
WHEN NEW.trich_yeu IS NOT NULL AND (SELECT cam_trich_yeu FROM do_mat WHERE id = NEW.do_mat_id) = 1
BEGIN SELECT RAISE(ABORT, 'CAM_TRICH_YEU'); END;

-- Số thứ tự, số đến, năm, quyển sổ không được thay đổi sau khi cấp.
CREATE TRIGGER tg_di_so_bat_bien BEFORE UPDATE OF so_thu_tu, nam, so_dang_ky_id ON van_ban_di
WHEN NEW.so_thu_tu <> OLD.so_thu_tu OR NEW.nam <> OLD.nam OR NEW.so_dang_ky_id <> OLD.so_dang_ky_id
BEGIN SELECT RAISE(ABORT, 'SO_BAT_BIEN'); END;
CREATE TRIGGER tg_den_so_bat_bien BEFORE UPDATE OF so_thu_tu, so_den, nam, so_dang_ky_id ON van_ban_den
WHEN NEW.so_thu_tu <> OLD.so_thu_tu OR NEW.so_den <> OLD.so_den OR NEW.nam <> OLD.nam OR NEW.so_dang_ky_id <> OLD.so_dang_ky_id
BEGIN SELECT RAISE(ABORT, 'SO_BAT_BIEN'); END;

-- Sổ đã khóa: không thêm, sửa, xóa văn bản.
CREATE TRIGGER tg_di_khoa_ins BEFORE INSERT ON van_ban_di
WHEN (SELECT da_khoa FROM so_dang_ky WHERE id = NEW.so_dang_ky_id) = 1
BEGIN SELECT RAISE(ABORT, 'SO_DA_KHOA'); END;
CREATE TRIGGER tg_di_khoa_upd BEFORE UPDATE ON van_ban_di
WHEN (SELECT da_khoa FROM so_dang_ky WHERE id = OLD.so_dang_ky_id) = 1
BEGIN SELECT RAISE(ABORT, 'SO_DA_KHOA'); END;
CREATE TRIGGER tg_di_khoa_del BEFORE DELETE ON van_ban_di
WHEN (SELECT da_khoa FROM so_dang_ky WHERE id = OLD.so_dang_ky_id) = 1
BEGIN SELECT RAISE(ABORT, 'SO_DA_KHOA'); END;
CREATE TRIGGER tg_den_khoa_ins BEFORE INSERT ON van_ban_den
WHEN (SELECT da_khoa FROM so_dang_ky WHERE id = NEW.so_dang_ky_id) = 1
BEGIN SELECT RAISE(ABORT, 'SO_DA_KHOA'); END;
CREATE TRIGGER tg_den_khoa_upd BEFORE UPDATE ON van_ban_den
WHEN (SELECT da_khoa FROM so_dang_ky WHERE id = OLD.so_dang_ky_id) = 1
BEGIN SELECT RAISE(ABORT, 'SO_DA_KHOA'); END;
CREATE TRIGGER tg_den_khoa_del BEFORE DELETE ON van_ban_den
WHEN (SELECT da_khoa FROM so_dang_ky WHERE id = OLD.so_dang_ky_id) = 1
BEGIN SELECT RAISE(ABORT, 'SO_DA_KHOA'); END;

-- ---------------------------------------------------------------- dữ liệu khởi tạo
INSERT INTO do_mat (ten, ky_hieu, muc, cam_trich_yeu, dang_dung) VALUES
    ('TUYỆT MẬT', 'A', 3, 1, 1),
    ('TỐI MẬT',   'B', 2, 0, 1),
    ('MẬT',       'C', 1, 0, 1);

-- Tên loại văn bản và chữ viết tắt (Phụ lục III Nghị định 30/2020/NĐ-CP). Công văn không có chữ viết tắt.
INSERT INTO danh_muc (nhom, ten, ten_khoa, phu_de, thu_tu) VALUES
    ('loai_van_ban', 'Công văn', 'cong van', NULL, 1),
    ('loai_van_ban', 'Quyết định', 'quyet dinh', 'QĐ', 2),
    ('loai_van_ban', 'Báo cáo', 'bao cao', 'BC', 3),
    ('loai_van_ban', 'Kế hoạch', 'ke hoach', 'KH', 4),
    ('loai_van_ban', 'Tờ trình', 'to trinh', 'TTr', 5),
    ('loai_van_ban', 'Thông báo', 'thong bao', 'TB', 6),
    ('loai_van_ban', 'Nghị quyết', 'nghi quyet', 'NQ', 7),
    ('loai_van_ban', 'Chỉ thị', 'chi thi', 'CT', 8),
    ('loai_van_ban', 'Quy chế', 'quy che', 'QC', 9),
    ('loai_van_ban', 'Quy định', 'quy dinh', 'QyĐ', 10),
    ('loai_van_ban', 'Hướng dẫn', 'huong dan', 'HD', 11),
    ('loai_van_ban', 'Chương trình', 'chuong trinh', 'CTr', 12),
    ('loai_van_ban', 'Phương án', 'phuong an', 'PA', 13),
    ('loai_van_ban', 'Đề án', 'de an', 'ĐA', 14),
    ('loai_van_ban', 'Dự án', 'du an', 'DA', 15),
    ('loai_van_ban', 'Biên bản', 'bien ban', 'BB', 16),
    ('loai_van_ban', 'Công điện', 'cong dien', 'CĐ', 17),
    ('loai_van_ban', 'Thông cáo', 'thong cao', 'TC', 18),
    ('loai_van_ban', 'Hợp đồng', 'hop dong', 'HĐ', 19),
    ('loai_van_ban', 'Bản ghi nhớ', 'ban ghi nho', 'BGN', 20),
    ('loai_van_ban', 'Bản thỏa thuận', 'ban thoa thuan', 'BTT', 21),
    ('loai_van_ban', 'Giấy mời', 'giay moi', 'GM', 22),
    ('loai_van_ban', 'Giấy giới thiệu', 'giay gioi thieu', 'GGT', 23),
    ('loai_van_ban', 'Giấy ủy quyền', 'giay uy quyen', 'GUQ', 24),
    ('loai_van_ban', 'Phiếu gửi', 'phieu gui', 'PG', 25),
    ('loai_van_ban', 'Phiếu chuyển', 'phieu chuyen', 'PC', 26),
    ('loai_van_ban', 'Phiếu báo', 'phieu bao', 'PB', 27);
