using System.Windows;
using Qlvb.App.Views;
using Qlvb.Application;
using Qlvb.Domain;

namespace Qlvb.App.Infrastructure;

/// <summary>Một dòng trong bảng danh sách: các ô dựng từ biểu mẫu sổ qua <see cref="TruongBieuMau.GiaTri"/>.</summary>
public sealed class DocRow
{
    public required VanBanBase V { get; init; }
    public required string[] Cells { get; init; }
    public bool DaHuy => V.DaHuy;
    public bool LaMau => V.LaDuLieuMau;
    public string TrangThai => V.DaHuy ? "Đã hủy" : V.LaDuLieuMau ? "Dữ liệu mẫu" : "";

    public static DocRow From(VanBanBase v, BieuMau bm, bool kyHieu) =>
        new() { V = v, Cells = bm.Cot.Select(c => TruongBieuMau.GiaTri(v, c.Truong, kyHieu)).ToArray() };
}

/// <summary>Thao tác trên một văn bản, dùng chung cho danh sách và tra cứu.</summary>
public static class DocActions
{
    /// <summary>Cho phép xóa vĩnh viễn (chế độ quản trị, bật trong Cấu hình, chỉ trong phiên hiện tại).</summary>
    public static bool AdminMode { get; set; }

    public static bool New(Window owner, LoaiSo loai)
    {
        Window w = loai == LoaiSo.Di ? new VanBanDiWindow() : new VanBanDenWindow();
        w.Owner = owner;
        w.ShowDialog();
        return loai == LoaiSo.Di ? ((VanBanDiWindow)w).Saved : ((VanBanDenWindow)w).Saved;
    }

    public static bool Edit(Window owner, VanBanBase v)
    {
        var fresh = Ctx.VanBan.Get(v.Loai, v.Id);
        if (fresh == null) { Dlg.Warn("Văn bản không còn tồn tại."); return true; }
        if (fresh.DaHuy)
        {
            Dlg.Info($"Văn bản số {TextUtil.So2(fresh.SoThuTu)}/{fresh.Nam} đã hủy ({fresh.LyDoHuy}). Khôi phục văn bản trước nếu cần sửa.");
            return false;
        }
        var so = Ctx.Store.GetSoDangKy(fresh.SoDangKyId);
        if (so is { DaKhoa: true })
        {
            Dlg.Info($"{so.TenHienThi}: sổ đã khóa nên không sửa được. Mở khóa sổ trong Danh mục → Sổ đăng ký nếu cần.");
            return false;
        }
        Window w = fresh is VanBanDi di ? new VanBanDiWindow(di) : new VanBanDenWindow((VanBanDen)fresh);
        w.Owner = owner;
        w.ShowDialog();
        return w is VanBanDiWindow a ? a.Saved : ((VanBanDenWindow)w).Saved;
    }

    public static bool Cancel(Window owner, VanBanBase v)
    {
        if (v.DaHuy) { Dlg.Info("Văn bản đã ở trạng thái hủy."); return false; }
        var lyDo = InputDialog.AskText(owner, "Hủy văn bản",
            $"Hủy văn bản số {TextUtil.So2(v.SoThuTu)}/{v.Nam} ({v.SoKyHieu}).\n" +
            "Văn bản không bị xóa: vẫn giữ số, vẫn hiển thị trên sổ với ghi chú \"ĐÃ HỦY\". Có thể khôi phục lại.\n\nLý do hủy (bắt buộc):",
            "", 3, 500, multiline: true, okText: "Hủy văn bản");
        if (lyDo == null) return false;
        return Dlg.Try(() => Ctx.VanBan.Huy(v.Loai, v.Id, lyDo, v.PhienBan), "hủy văn bản") && Changed();
    }

    public static bool Restore(VanBanBase v)
    {
        if (!v.DaHuy) return false;
        if (!Dlg.Confirm($"Khôi phục văn bản số {TextUtil.So2(v.SoThuTu)}/{v.Nam} về trạng thái hiệu lực?")) return false;
        return Dlg.Try(() => Ctx.VanBan.KhoiPhuc(v.Loai, v.Id, v.PhienBan), "khôi phục văn bản") && Changed();
    }

    public static bool HardDelete(Window owner, VanBanBase v)
    {
        if (!AdminMode) { Dlg.Info("Xóa vĩnh viễn chỉ dùng trong chế độ quản trị (Cấu hình → Chế độ quản trị)."); return false; }
        if (!v.DaHuy && !v.LaDuLieuMau) { Dlg.Warn("Chỉ xóa vĩnh viễn được văn bản đã hủy. Hãy hủy văn bản trước."); return false; }
        if (!Dlg.Confirm($"XÓA VĨNH VIỄN văn bản số {TextUtil.So2(v.SoThuTu)}/{v.Nam}?\n\n" +
                         "• Bản ghi bị xóa khỏi cơ sở dữ liệu và KHÔNG khôi phục được (trừ khi khôi phục cả bản sao lưu).\n" +
                         "• Số thứ tự của văn bản này sẽ KHÔNG được cấp lại; sổ in ra sẽ thiếu số này.\n" +
                         "• Thông thường chỉ nên HỦY (giữ lại trên sổ) theo quy định quản lý sổ.\n\nTiếp tục?", danger: true))
            return false;
        var lyDo = InputDialog.AskText(owner, "Lý do xóa vĩnh viễn", "Nhập lý do (ghi vào nhật ký):", "", 3, 500, okText: "Xóa vĩnh viễn");
        if (lyDo == null) return false;
        return Dlg.Try(() => Ctx.VanBan.XoaVinhVien(v.Loai, v.Id, lyDo), "xóa vĩnh viễn") && Changed();
    }

    private static bool Changed()
    {
        Ctx.NotifyChanged();
        return true;
    }

    /// <summary>Biểu mẫu sổ dùng để hiển thị danh sách năm <paramref name="nam"/>.</summary>
    public static BieuMau FormFor(LoaiSo loai, int? nam)
    {
        var so = nam is { } n ? Ctx.Store.CurrentSoDangKy(loai, n) : null;
        if (so != null && Ctx.Store.GetBieuMau(so.MaBieuMau) is { } bm) return bm;
        return SoDangKyService.ChonBieuMau(Ctx.Store.ListBieuMau(), loai, nam ?? Ctx.Clock.Today.Year);
    }
}
