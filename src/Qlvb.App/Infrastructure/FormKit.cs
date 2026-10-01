using System.Windows;
using System.Windows.Controls;
using Qlvb.Application;
using Qlvb.Domain;

namespace Qlvb.App.Infrastructure;

/// <summary>Tiện ích cho form nhập: danh sách gợi ý, thêm nhanh danh mục, chuyển đổi ngày, báo lỗi theo trường.</summary>
public static class FormKit
{
    /// <summary>Giá trị dùng gần nhất trong phiên (để điền sẵn ở lần nhập sau).</summary>
    public static readonly Dictionary<string, string> Remember = [];

    public static void FillSuggestions(ComboBox cb, string nhom)
    {
        var text = cb.Text;
        cb.ItemsSource = Ctx.DanhMuc.GoiY(nhom);
        cb.Text = text;
    }

    /// <summary>Thêm nhanh một mục danh mục rồi chọn mục đó.</summary>
    public static void QuickAdd(Window owner, ComboBox cb, string nhom)
    {
        var tenNhom = NhomDanhMuc.TenNhom[nhom].ToLowerInvariant();
        var ten = InputDialog.AskText(owner, "Thêm nhanh danh mục", $"Tên {tenNhom} mới:", cb.Text.Trim(), 1, Limits.TenMax, okText: "Thêm");
        if (ten == null) return;
        string? phuDe = null;
        if (nhom == NhomDanhMuc.LoaiVanBan)
            phuDe = InputDialog.AskText(owner, "Chữ viết tắt", $"Chữ viết tắt của \"{ten}\" (dùng trong số, ký hiệu; để trống nếu không có):", "", 0, 20);
        else if (nhom == NhomDanhMuc.NguoiKy)
            phuDe = InputDialog.AskText(owner, "Chức vụ", $"Chức vụ của \"{ten}\" (có thể để trống):", "", 0, 200);
        try
        {
            var existing = Ctx.Store.FindDanhMuc(nhom, ten);
            var m = existing is { DangDung: true } ? existing : Ctx.DanhMuc.Them(nhom, ten, phuDe);
            FillSuggestions(cb, nhom);
            cb.Text = m.Ten;
        }
        catch (Exception ex) { Dlg.Handle(ex, "thêm danh mục"); }
    }

    public static DateOnly? ToDate(DatePicker dp) => dp.SelectedDate is { } d ? DateOnly.FromDateTime(d) : null;
    public static DateTime? ToDateTime(DateOnly? d) => d?.ToDateTime(TimeOnly.MinValue);

    public static bool TryInt(TextBox tb, out int value) =>
        int.TryParse(tb.Text.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out value);

    /// <summary>Hiển thị lỗi kiểm tra hợp lệ và đưa con trỏ tới trường lỗi đầu tiên.</summary>
    public static void ShowErrors(Qlvb.Domain.ValidationResult r, IReadOnlyDictionary<string, Control> fields)
    {
        Dlg.Warn("Chưa lưu được vì:\n\n• " + string.Join("\n• ", r.Errors.Select(e => e.Message)));
        var first = r.Errors.FirstOrDefault(e => fields.ContainsKey(e.Field));
        if (first != null) FocusField(fields[first.Field]);
    }

    public static void FocusField(Control c)
    {
        c.Dispatcher.BeginInvoke(() =>
        {
            c.Focus();
            if (c is TextBox tb) tb.SelectAll();
        });
    }

    /// <summary>Hỏi người dùng có lưu khi có cảnh báo mềm (nghi trùng, ngày bất thường).</summary>
    public static bool ConfirmWarnings(KiemTraTruocLuu kt)
    {
        if (!kt.CoCanhBao) return true;
        var lines = kt.KetQua.Warnings.Select(w => "• " + w.Message).ToList();
        if (kt.NghiTrung.Count > 0)
        {
            lines.Add("• Có thể trùng với văn bản đã đăng ký:");
            lines.AddRange(kt.NghiTrung.Take(5).Select(d =>
                $"    – Số {TextUtil.So2(d.SoThuTu)}/{d.Nam}: {d.SoKyHieu}, ngày {TextUtil.FormatDate(d.NgayVanBan)}"));
        }
        return Dlg.Confirm("Cảnh báo:\n\n" + string.Join("\n", lines) + "\n\nVẫn lưu văn bản này?");
    }

    public static void SelectDoMat(ComboBox cb, int id)
    {
        foreach (var item in cb.Items)
            if (item is DoMat d && d.Id == id) { cb.SelectedItem = item; return; }
    }
}
