using Qlvb.Application;
using Qlvb.Domain;

namespace Qlvb.Tests;

public class VanBanTests
{
    [Fact]
    public void CreateDi_AllocatesSequentialNumbers_AndOpensBookAutomatically()
    {
        using var env = new TestEnv();
        var id1 = env.VanBan.ThemMoi(env.NewDi());
        var id2 = env.VanBan.ThemMoi(env.NewDi());
        Assert.Equal(1, env.Store.GetDi(id1)!.SoThuTu);
        Assert.Equal(2, env.Store.GetDi(id2)!.SoThuTu);
        var so = env.Store.CurrentSoDangKy(LoaiSo.Di, 2026)!;
        Assert.Equal(1, so.QuyenSo);
        Assert.Equal("SO_DI_ND63_2026", so.MaBieuMau);
        Assert.Equal(2, so.SoBanGhi);
        Assert.Equal("Cơ quan Thử nghiệm", so.TenCoQuan);
    }

    [Fact]
    public void CreateDen_AllocatesSoDen_AndRejectsDuplicateSoDen()
    {
        using var env = new TestEnv();
        var a = env.NewDen();
        env.VanBan.ThemMoi(a);
        Assert.Equal(1, a.SoDen);
        var b = env.NewDen(soKyHieu: "13/QĐ-ABC");
        b.SoDen = 1;
        var ex = Assert.Throws<BusinessException>(() => env.VanBan.ThemMoi(b));
        Assert.Equal(nameof(VanBanDen.SoDen), ex.Field);
        b.SoDen = 10;
        env.VanBan.ThemMoi(b);
        Assert.Equal(11, env.VanBan.SoDuKien(LoaiSo.Den, 2026, BoDem.SoDen));
    }

    [Fact]
    public void Numbers_NeverReused_AfterCancelOrHardDelete()
    {
        using var env = new TestEnv();
        var v1 = env.NewDi();
        env.VanBan.ThemMoi(v1);
        var v2 = env.NewDi();
        env.VanBan.ThemMoi(v2);
        env.VanBan.Huy(LoaiSo.Di, v2.Id, "Nhập nhầm", v2.PhienBan);
        env.VanBan.XoaVinhVien(LoaiSo.Di, v2.Id, "Xóa thử");
        var v3 = env.NewDi();
        env.VanBan.ThemMoi(v3);
        Assert.Equal(3, v3.SoThuTu);
    }

    [Fact]
    public void Numbering_RestartsEachYear_AndContinuesAcrossBooks()
    {
        using var env = new TestEnv();
        env.VanBan.ThemMoi(env.NewDi());
        env.VanBan.ThemMoi(env.NewDi());
        var so1 = env.Store.CurrentSoDangKy(LoaiSo.Di, 2026)!;
        env.So.Khoa(so1);
        Assert.Throws<BusinessException>(() => env.VanBan.ThemMoi(env.NewDi()));
        var so2 = env.So.MoSo(LoaiSo.Di, 2026);
        Assert.Equal(2, so2.QuyenSo);
        var v = env.NewDi();
        env.VanBan.ThemMoi(v);
        Assert.Equal(3, v.SoThuTu);
        Assert.Equal(so2.Id, v.SoDangKyId);

        env.Clock.Now = new DateTime(2027, 1, 2, 8, 0, 0);
        var n = env.NewDi();
        env.VanBan.ThemMoi(n);
        Assert.Equal(2027, n.Nam);
        Assert.Equal(1, n.SoThuTu);
    }

    [Fact]
    public void FirstBook_CanStartFromGivenNumber()
    {
        using var env = new TestEnv();
        env.So.MoSo(LoaiSo.Di, 2026, soBatDau: 120);
        var v = env.NewDi();
        env.VanBan.ThemMoi(v);
        Assert.Equal(120, v.SoThuTu);
    }

    [Fact]
    public void TuyetMat_NeverStoresTrichYeu()
    {
        using var env = new TestEnv();
        var v = env.NewDi(doMat: "A", trichYeu: "Không được lưu");
        var kt = env.VanBan.KiemTra(v);
        Assert.Contains(kt.KetQua.Errors, e => e.Field == nameof(VanBanBase.TrichYeu));
        v.TrichYeu = null;
        env.VanBan.ThemMoi(v);
        Assert.Null(env.Store.GetDi(v.Id)!.TrichYeu);
        // Cột 4 khi in/xuất chỉ có tên loại
        Assert.Equal("Công văn", TruongBieuMau.GiaTri(env.Store.GetDi(v.Id)!, TruongBieuMau.TenLoaiTrichYeu));
    }

    [Fact]
    public void TuyetMat_DatabaseTriggerBlocksTrichYeu_EvenBypassingService()
    {
        using var env = new TestEnv();
        var v = env.NewDi(doMat: "A", trichYeu: null);
        env.VanBan.ThemMoi(v);
        v.TrichYeu = "Ghi lén";
        var ex = Assert.Throws<BusinessException>(() => env.Store.UpdateDi(v, new AuditEntry("x", "y", null, "z")));
        Assert.Contains("trích yếu", ex.Message);
    }

    [Fact]
    public void ChangingToTuyetMat_ClearsTrichYeu()
    {
        using var env = new TestEnv();
        var v = env.NewDi(doMat: "B");
        env.VanBan.ThemMoi(v);
        var e = env.Store.GetDi(v.Id)!;
        e.DoMatId = env.DoMat("A").Id;
        e.TrichYeu = null;
        env.VanBan.CapNhat(e);
        Assert.Null(env.Store.GetDi(v.Id)!.TrichYeu);
        Assert.Equal("TUYỆT MẬT", env.Store.GetDi(v.Id)!.DoMatTen);
    }

    [Fact]
    public void TrichYeu_NeverInAuditLog()
    {
        using var env = new TestEnv();
        var v = env.NewDi(trichYeu: "NOIDUNGNHAYCAM1");
        env.VanBan.ThemMoi(v);
        var e = env.Store.GetDi(v.Id)!;
        e.TrichYeu = "NOIDUNGNHAYCAM2";
        env.VanBan.CapNhat(e);
        foreach (var a in env.Store.ListAudit(new()))
        {
            Assert.DoesNotContain("NOIDUNGNHAYCAM", a.MoTa);
            Assert.DoesNotContain("NOIDUNGNHAYCAM", a.ThayDoi ?? "");
        }
    }

    [Fact]
    public void Edit_RecordsOldAndNewValues_KeepsNumber()
    {
        using var env = new TestEnv();
        var v = env.NewDi(soKyHieu: "01/TN");
        env.VanBan.ThemMoi(v);
        var e = env.Store.GetDi(v.Id)!;
        e.SoKyHieu = "01/TN-SUA";
        e.SoThuTu = 99;
        var diff = env.VanBan.SoSanh(e);
        Assert.Contains(diff, d => d.Truong == "Số, ký hiệu" && d.Cu == "01/TN" && d.Moi == "01/TN-SUA");
        env.VanBan.CapNhat(e);
        var saved = env.Store.GetDi(v.Id)!;
        Assert.Equal(1, saved.SoThuTu);
        Assert.Equal(2, saved.PhienBan);
        var log = env.Store.ListAudit(new()).First(a => a.HanhDong == "Sửa văn bản đi");
        Assert.Contains("01/TN-SUA", log.ThayDoi);
    }

    [Fact]
    public void Edit_StaleVersion_Rejected()
    {
        using var env = new TestEnv();
        var v = env.NewDi();
        env.VanBan.ThemMoi(v);
        var a = env.Store.GetDi(v.Id)!;
        var b = env.Store.GetDi(v.Id)!;
        a.GhiChu = "lần 1";
        env.VanBan.CapNhat(a);
        b.GhiChu = "lần 2";
        Assert.Throws<ConcurrencyException>(() => env.VanBan.CapNhat(b));
    }

    [Fact]
    public void Edit_CannotMoveToAnotherYear()
    {
        using var env = new TestEnv();
        var v = env.NewDi();
        env.VanBan.ThemMoi(v);
        var e = env.Store.GetDi(v.Id)!;
        e.NgayDangKy = new DateOnly(2025, 12, 31);
        Assert.Throws<ValidationException>(() => env.VanBan.CapNhat(e));
    }

    [Fact]
    public void Cancel_RequiresReason_SoftDeletes_AndRestore()
    {
        using var env = new TestEnv();
        var v = env.NewDi();
        env.VanBan.ThemMoi(v);
        Assert.Throws<BusinessException>(() => env.VanBan.Huy(LoaiSo.Di, v.Id, "", v.PhienBan));
        env.VanBan.Huy(LoaiSo.Di, v.Id, "Văn bản không phát hành", v.PhienBan);
        var h = env.Store.GetDi(v.Id)!;
        Assert.True(h.DaHuy);
        Assert.Equal("kiemthu", h.NguoiHuy);
        Assert.StartsWith("ĐÃ HỦY: Văn bản không phát hành", TruongBieuMau.GiaTri(h, TruongBieuMau.GhiChu));
        Assert.Throws<BusinessException>(() => env.VanBan.CapNhat(h));
        env.VanBan.KhoiPhuc(LoaiSo.Di, v.Id, h.PhienBan);
        Assert.False(env.Store.GetDi(v.Id)!.DaHuy);
    }

    [Fact]
    public void HardDelete_OnlyCancelled()
    {
        using var env = new TestEnv();
        var v = env.NewDi();
        env.VanBan.ThemMoi(v);
        Assert.Throws<BusinessException>(() => env.VanBan.XoaVinhVien(LoaiSo.Di, v.Id, "thử xóa"));
    }

    [Fact]
    public void LockedBook_BlocksEdit()
    {
        using var env = new TestEnv();
        var v = env.NewDi();
        env.VanBan.ThemMoi(v);
        env.So.Khoa(env.Store.GetSoDangKy(v.SoDangKyId)!);
        var e = env.Store.GetDi(v.Id)!;
        e.GhiChu = "sửa";
        Assert.Throws<BusinessException>(() => env.VanBan.CapNhat(e));
        Assert.Throws<BusinessException>(() => env.VanBan.Huy(LoaiSo.Di, v.Id, "lý do", e.PhienBan));
        env.So.MoKhoa(env.Store.GetSoDangKy(v.SoDangKyId)!, "Sửa sai sót");
        env.VanBan.CapNhat(e);
    }

    [Fact]
    public void Duplicates_AreSoftWarnings()
    {
        using var env = new TestEnv();
        env.VanBan.ThemMoi(env.NewDi(soKyHieu: "05/TN"));
        var kt = env.VanBan.KiemTra(env.NewDi(soKyHieu: "05/tn"));
        Assert.True(kt.KetQua.IsValid);
        Assert.Single(kt.NghiTrung);

        env.VanBan.ThemMoi(env.NewDen(soKyHieu: "77/BC-X"));
        var kt2 = env.VanBan.KiemTra(env.NewDen(soKyHieu: "77/BC-X"));
        Assert.Single(kt2.NghiTrung);
    }

    [Fact]
    public void Validation_RequiredFields_VietnameseMessages()
    {
        using var env = new TestEnv();
        var v = env.VanBan.TaoMoiDi();
        var kt = env.VanBan.KiemTra(v);
        Assert.False(kt.KetQua.IsValid);
        Assert.Contains(kt.KetQua.Errors, e => e.Field == nameof(VanBanBase.SoKyHieu));
        Assert.Contains(kt.KetQua.Errors, e => e.Field == nameof(VanBanDi.NoiNhan));
        Assert.All(kt.KetQua.Errors, e => Assert.False(string.IsNullOrWhiteSpace(e.Message)));
    }

    [Fact]
    public void Catalog_MustExist_ForNguoiKy_AutoAdd_ForNoiNhan()
    {
        using var env = new TestEnv();
        var v = env.NewDi();
        v.NguoiKy = "Người lạ";
        Assert.Contains(env.VanBan.KiemTra(v).KetQua.Errors, e => e.Field == nameof(VanBanDi.NguoiKy));
        v = env.NewDi();
        v.NoiNhan = [new NoiNhanKyNhan { NoiNhan = "Sở Mới Tinh" }];
        env.VanBan.ThemMoi(v);
        Assert.NotNull(env.Store.FindDanhMuc(NhomDanhMuc.NoiNhan, "sở mới tinh"));
    }

    [Fact]
    public void CopyPrevious_DoesNotCopyIdentifiers()
    {
        using var env = new TestEnv();
        var v = env.NewDi(soKyHieu: "09/TN");
        env.VanBan.ThemMoi(v);
        var c = env.VanBan.SaoChepDi(env.Store.GetDi(v.Id)!);
        Assert.Equal(0, c.Id);
        Assert.Equal("", c.SoKyHieu);
        Assert.Null(c.TrichYeu);
        Assert.Equal(2, c.SoThuTu);
        Assert.Equal(v.NguoiKy, c.NguoiKy);
        Assert.Single(c.NoiNhan);
    }

    [Fact]
    public void Unicode_RoundTrip_AndNormalization()
    {
        using var env = new TestEnv();
        // "Kế hoạch" dạng tổ hợp (NFD) phải được chuẩn hóa về NFC
        var nfd = "Kế hoạch   bảo   vệ";
        var v = env.NewDi(trichYeu: nfd);
        env.VanBan.ThemMoi(v);
        Assert.Equal("Kế hoạch bảo vệ", env.Store.GetDi(v.Id)!.TrichYeu);
    }

    [Fact]
    public void Inactive_DoMat_CannotBeUsedForNew()
    {
        using var env = new TestEnv();
        var c = env.DoMat("C");
        env.DanhMuc.LuuDoMat(c with { DangDung = false });
        var v = env.NewDi();
        v.DoMatId = c.Id;
        Assert.Contains(env.VanBan.KiemTra(v).KetQua.Errors, e => e.Field == nameof(VanBanBase.DoMatId));
    }

    [Fact]
    public void CannotEnableCamTrichYeu_WhenDocsHaveTrichYeu()
    {
        using var env = new TestEnv();
        env.VanBan.ThemMoi(env.NewDi(doMat: "B"));
        var b = env.DoMat("B");
        Assert.Throws<BusinessException>(() => env.DanhMuc.LuuDoMat(b with { CamTrichYeu = true }));
    }

    [Fact]
    public void UsedCatalogItem_CannotBeDeleted_OnlyDeactivated()
    {
        using var env = new TestEnv();
        env.VanBan.ThemMoi(env.NewDi());
        var m = env.Store.FindDanhMuc(NhomDanhMuc.NguoiKy, "Nguyễn Văn A")!;
        Assert.Throws<BusinessException>(() => env.DanhMuc.Xoa(m));
        env.DanhMuc.DatTrangThai(m, false);
        Assert.DoesNotContain("Nguyễn Văn A", env.DanhMuc.GoiY(NhomDanhMuc.NguoiKy));
        var unused = env.DanhMuc.Them(NhomDanhMuc.NguoiKy, "Chưa dùng");
        env.DanhMuc.Xoa(unused);
    }
}
