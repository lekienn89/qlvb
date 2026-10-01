using System.Windows;
using System.Windows.Controls;
using Qlvb.App.Infrastructure;
using Qlvb.Application;

namespace Qlvb.App.Pages;

/// <summary>Xem nhật ký thao tác (chỉ đọc) và kiểm tra tính toàn vẹn chuỗi băm.</summary>
public sealed class NhatKyPage : UserControl, IPage
{
    private readonly DataGrid _grid = new();
    private readonly DatePicker _tu = new() { Width = 130 };
    private readonly DatePicker _den = new() { Width = 130 };
    private readonly TextBox _hanhDong = new() { Width = 200 };
    private readonly TextBlock _status = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
    public string Title => "Nhật ký";

    private sealed record Row(long Id, string ThoiGian, string HanhDong, string DoiTuong, string BanGhi, string Nguoi, string MoTa, string ThayDoi);

    public NhatKyPage()
    {
        var dock = new DockPanel();
        var h = new TextBlock { Text = "Nhật ký thao tác", Style = (Style)FindResource("H1") };
        DockPanel.SetDock(h, Dock.Top);
        dock.Children.Add(h);
        var bar = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        void L(string t, UIElement c)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
            sp.Children.Add(new TextBlock { Text = t, Style = (Style)FindResource("Hint") });
            sp.Children.Add(c);
            bar.Children.Add(sp);
        }
        L("Từ ngày", _tu);
        L("Đến ngày", _den);
        L("Hành động chứa", _hanhDong);
        var find = new Button { Content = "Xem", Style = (Style)FindResource("PrimaryButton"), VerticalAlignment = VerticalAlignment.Bottom };
        find.Click += (_, _) => OnShow();
        bar.Children.Add(find);
        var verify = new Button { Content = "Kiểm tra toàn vẹn", VerticalAlignment = VerticalAlignment.Bottom };
        verify.Click += (_, _) => Verify();
        bar.Children.Add(verify);
        bar.Children.Add(_status);
        DockPanel.SetDock(bar, Dock.Top);
        dock.Children.Add(bar);
        var note = new TextBlock
        {
            Style = (Style)FindResource("Hint"), Margin = new Thickness(0, 6, 0, 0),
            Text = "Nhật ký chỉ được ghi thêm, không sửa/xóa được; mỗi dòng liên kết băm với dòng trước để phát hiện can thiệp. Nhật ký không ghi mật khẩu và không ghi nội dung trích yếu.",
        };
        DockPanel.SetDock(note, Dock.Bottom);
        dock.Children.Add(note);
        var wrap = (Style)FindResource("WrapCell");
        _grid.Columns.Add(DanhMucPage.Col("#", nameof(Row.Id), 60));
        _grid.Columns.Add(DanhMucPage.Col("Thời gian", nameof(Row.ThoiGian), 145));
        _grid.Columns.Add(DanhMucPage.Col("Hành động", nameof(Row.HanhDong), 190));
        _grid.Columns.Add(DanhMucPage.Col("Đối tượng", nameof(Row.DoiTuong), 110));
        _grid.Columns.Add(DanhMucPage.Col("ID bản ghi", nameof(Row.BanGhi), 80));
        _grid.Columns.Add(DanhMucPage.Col("Người thực hiện", nameof(Row.Nguoi), 120));
        var mo = DanhMucPage.Col("Mô tả", nameof(Row.MoTa), 2, true);
        mo.ElementStyle = wrap;
        _grid.Columns.Add(mo);
        var td = DanhMucPage.Col("Giá trị cũ → mới", nameof(Row.ThayDoi), 2, true);
        td.ElementStyle = wrap;
        _grid.Columns.Add(td);
        dock.Children.Add(_grid);
        Content = dock;
    }

    public void OnShow() => Dlg.Try(() =>
    {
        var f = new AuditFilter
        {
            Tu = _tu.SelectedDate,
            Den = _den.SelectedDate?.AddDays(1).AddSeconds(-1),
            HanhDong = _hanhDong.Text,
            Limit = 5000,
        };
        _grid.ItemsSource = Ctx.Store.ListAudit(f).Select(a => new Row(a.Id, a.ThoiGian.ToString("dd/MM/yyyy HH:mm:ss"), a.HanhDong, a.DoiTuong,
            a.BanGhiId?.ToString() ?? "", a.NguoiThucHien, a.MoTa, FormatChanges(a.ThayDoi))).ToList();
    }, "nhật ký");

    private static string FormatChanges(string? json)
    {
        if (string.IsNullOrEmpty(json)) return "";
        try
        {
            var list = System.Text.Json.JsonSerializer.Deserialize<List<ThayDoiTruong>>(json) ?? [];
            return string.Join("\n", list.Select(c => $"{c.Truong}: \"{c.Cu}\" → \"{c.Moi}\""));
        }
        catch (System.Text.Json.JsonException) { return json; }
    }

    private void Verify() => Dlg.Try(() =>
    {
        string? r;
        using (new WaitCursor()) r = Ctx.Store.VerifyAuditChain();
        _status.Text = r == null ? "✔ Nhật ký toàn vẹn." : "✖ " + r;
        _status.Foreground = r == null ? System.Windows.Media.Brushes.DarkGreen : System.Windows.Media.Brushes.Firebrick;
        if (r != null) Dlg.Warn("Phát hiện nhật ký bị can thiệp: " + r + "\n\nHãy báo cáo người có thẩm quyền và đối chiếu với bản sao lưu.");
    }, "kiểm tra nhật ký");
}
