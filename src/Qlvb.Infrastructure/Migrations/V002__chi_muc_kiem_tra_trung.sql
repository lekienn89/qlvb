-- QLVB – lược đồ phiên bản 2: khóa so khớp số, ký hiệu (không dấu, chữ thường) và chỉ mục để kiểm tra trùng nhanh
-- khi dữ liệu lớn. Không thay đổi dữ liệu nghiệp vụ.

-- Tạm bỏ trigger chặn sửa văn bản trong sổ đã khóa để điền cột kỹ thuật mới, rồi tạo lại nguyên trạng.
DROP TRIGGER tg_di_khoa_upd;
DROP TRIGGER tg_den_khoa_upd;

ALTER TABLE van_ban_di ADD COLUMN khoa_so_ky_hieu TEXT NOT NULL DEFAULT '';
ALTER TABLE van_ban_den ADD COLUMN khoa_so_ky_hieu TEXT NOT NULL DEFAULT '';
UPDATE van_ban_di SET khoa_so_ky_hieu = bo_dau(so_ky_hieu);
UPDATE van_ban_den SET khoa_so_ky_hieu = bo_dau(so_ky_hieu);

CREATE TRIGGER tg_di_khoa_upd BEFORE UPDATE ON van_ban_di
WHEN (SELECT da_khoa FROM so_dang_ky WHERE id = OLD.so_dang_ky_id) = 1
BEGIN SELECT RAISE(ABORT, 'SO_DA_KHOA'); END;
CREATE TRIGGER tg_den_khoa_upd BEFORE UPDATE ON van_ban_den
WHEN (SELECT da_khoa FROM so_dang_ky WHERE id = OLD.so_dang_ky_id) = 1
BEGIN SELECT RAISE(ABORT, 'SO_DA_KHOA'); END;

CREATE INDEX ix_di_trung ON van_ban_di(nam, khoa_so_ky_hieu);
CREATE INDEX ix_den_trung ON van_ban_den(khoa_so_ky_hieu, ngay_van_ban);
CREATE INDEX ix_di_tao ON van_ban_di(tao_luc);
CREATE INDEX ix_den_tao ON van_ban_den(tao_luc);
