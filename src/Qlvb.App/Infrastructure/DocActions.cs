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

    /// <summary>Xóa văn bản nhập sai: hỏi xác nhận, mật khẩu và lý do. Số cuối cùng của năm được cấp lại.</summary>
    public static bool Delete(Window owner, VanBanBase v)
    {
        var so = Ctx.Store.GetSoDangKy(v.SoDangKyId);
        if (so is { DaKhoa: true })
        {
            Dlg.Info($"{so.TenHienThi}: sổ đã khóa nên không xóa được văn bản. Mở khóa sổ trong Danh mục → Sổ đăng ký nếu cần.");
            return false;
        }
        var soCuoi = Ctx.VanBan.LaSoCuoi(v);
        var stt = TextUtil.So2(v.SoThuTu);
        if (!Dlg.Confirm($"Xóa văn bản số {stt}/{v.Nam} ({v.SoKyHieu}) khỏi sổ?\n\n" +
                         "• Dùng khi văn bản bị nhập sai (sai số, sai ngày, sai nội dung…). Nếu chỉ sai một vài thông tin, có thể Sửa thay vì xóa.\n" +
                         "• Văn bản bị xóa hẳn, không khôi phục được (trừ khi khôi phục cả bản sao lưu).\n" +
                         (soCuoi
                             ? $"• Đây là văn bản mang số cuối cùng của năm {v.Nam}: số {stt} sẽ được cấp lại cho văn bản nhập tiếp theo.\n"
                             : $"• Số {stt} sẽ để trống trên sổ (không cấp lại) vì đã có văn bản số lớn hơn.\n") +
                         "• Việc xóa được ghi vào nhật ký kèm lý do.\n\nTiếp tục?", danger: true))
            return false;
        var pw = InputDialog.AskPassword(owner, "Xác nhận mật khẩu", "Xóa văn bản là thao tác quan trọng. Nhập mật khẩu để xác nhận:");
        if (pw == null) return false;
        if (!Ctx.Session.Keys.Verify(pw)) { Dlg.Warn("Mật khẩu không đúng."); return false; }
        var lyDo = InputDialog.AskText(owner, "Lý do xóa", "Lý do xóa (bắt buộc, ghi vào nhật ký):", "", 3, 500, multiline: true, okText: "Xóa văn bản");
        if (lyDo == null) return false;
        var thuHoi = false;
        if (!Dlg.Try(() => thuHoi = Ctx.VanBan.XoaVinhVien(v.Loai, v.Id, lyDo), "xóa văn bản")) return false;
        Dlg.Info(thuHoi ? $"Đã xóa. Số {stt} sẽ được cấp cho văn bản nhập tiếp theo." : $"Đã xóa. Số {stt} để trống trên sổ.");
        return Changed();
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
