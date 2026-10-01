using System.Text;
using ClosedXML.Excel;
using Qlvb.Application;
using Qlvb.Domain;

namespace Qlvb.Infrastructure.Services;

public enum ExportFormat { Xlsx, Csv }

/// <summary>
/// Xuất dữ liệu. Mọi văn bản trong phần mềm đều là tài liệu, vật chứa bí mật nhà nước, nên xuất danh sách văn bản
/// ra Excel/CSV mặc định BỊ CHẶN (cấu hình "Cho phép xuất dữ liệu có độ mật"). Tệp xuất ra KHÔNG được mã hóa.
/// Trích yếu của tài liệu cấm trích yếu không bao giờ xuất hiện (dùng chung <see cref="TruongBieuMau.GiaTri"/>).
/// </summary>
public sealed class ExportService(IDataStore store)
{
    public bool DocumentExportAllowed => store.GetConfig(ConfigKeys.ChoPhepXuatMat) == "1";

    public void ExportDocuments(IReadOnlyList<VanBanBase> items, BieuMau bm, ExportFormat format, string path, bool dungKyHieuDoMat)
    {
        if (!DocumentExportAllowed)
            throw new BusinessException("Xuất danh sách văn bản mật ra tệp đang bị tắt (Cấu hình → Bảo mật). Có thể in sổ thay cho xuất tệp.");
        var header = bm.Cot.Select(c => $"({c.So}) {c.TieuDe}").ToList();
        var rows = items.Select(v => bm.Cot.Select(c => TruongBieuMau.GiaTri(v, c.Truong, dungKyHieuDoMat)).ToList()).ToList();
        Write(format, path, bm.TieuDe, header, rows);
        store.AppendAudit(new AuditEntry("Xuất dữ liệu văn bản", bm.Loai == LoaiSo.Di ? "van_ban_di" : "van_ban_den", null,
            $"Xuất {items.Count} văn bản ra tệp {format.ToString().ToUpperInvariant()}: {Path.GetFileName(path)}"));
    }

    /// <summary>Xuất bảng thống kê (chỉ số lượng, không có nội dung văn bản).</summary>
    public void ExportStatistics(string title, IReadOnlyList<string> header, IReadOnlyList<IReadOnlyList<string>> rows, ExportFormat format, string path)
    {
        Write(format, path, title, header, rows);
        store.AppendAudit(new AuditEntry("Xuất thống kê", "thong_ke", null, $"{title}: {Path.GetFileName(path)}"));
    }

    private static void Write(ExportFormat format, string path, string title, IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string>> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        if (format == ExportFormat.Csv) WriteCsv(path, header, rows);
        else WriteXlsx(path, title, header, rows);
    }

    /// <summary>Chặn "CSV injection": ô bắt đầu bằng = + - @ (hoặc tab/CR) được thêm dấu nháy đơn.</summary>
    public static string SafeCell(string? s)
    {
        s ??= "";
        return s.Length > 0 && "=+-@\t\r".Contains(s[0]) ? "'" + s : s;
    }

    private static string Csv(string s)
    {
        s = SafeCell(s);
        return s.IndexOfAny([',', '"', '\n', '\r', ';']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    private static void WriteCsv(string path, IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string>> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", header.Select(Csv)));
        foreach (var r in rows) sb.AppendLine(string.Join(",", r.Select(Csv)));
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true)); // BOM để Excel đọc đúng tiếng Việt
    }

    private static void WriteXlsx(string path, string title, IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string>> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Du lieu");
        ws.Cell(1, 1).Value = title;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        for (var i = 0; i < header.Count; i++)
        {
            var c = ws.Cell(3, i + 1);
            c.Value = header[i];
            c.Style.Font.Bold = true;
            c.Style.Alignment.WrapText = true;
            c.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            c.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEF7");
        }
        var row = 4;
        foreach (var r in rows)
        {
            for (var i = 0; i < r.Count; i++)
            {
                var c = ws.Cell(row, i + 1);
                c.SetValue(SafeCell(r[i]));
                c.Style.Alignment.WrapText = true;
                c.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            }
            row++;
        }
        var range = ws.Range(3, 1, Math.Max(3, row - 1), header.Count);
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        for (var i = 1; i <= header.Count; i++) ws.Column(i).Width = Math.Clamp(header[i - 1].Length * 0.8, 10, 45);
        ws.SheetView.FreezeRows(3);
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
        ws.PageSetup.SetRowsToRepeatAtTop(3, 3);
        wb.SaveAs(path);
    }
}
