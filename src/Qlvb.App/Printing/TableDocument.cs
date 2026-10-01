using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Qlvb.App.Printing;

public sealed record PrintColumn(string Title, double Weight, bool Center, string? Number = null);

/// <summary>Đặc tả một tài liệu in dạng bảng (sổ đăng ký, báo cáo).</summary>
public sealed class TableSpec
{
    public bool Landscape { get; init; } = true;
    /// <summary>Dòng tiêu đề lặp ở đầu mỗi trang (nhỏ).</summary>
    public string RunningTitle { get; init; } = "";
    /// <summary>Tiêu đề lớn ở trang bảng đầu tiên (nếu không có trang bìa).</summary>
    public IReadOnlyList<string> FirstPageTitle { get; init; } = [];
    public required IReadOnlyList<PrintColumn> Columns { get; init; }
    public required IReadOnlyList<string[]> Rows { get; init; }
    /// <summary>Hàng được in nghiêng (ví dụ văn bản đã hủy).</summary>
    public Func<int, bool>? Italic { get; init; }
    public bool NumberRow { get; init; } = true;
    public Func<Size, FixedPage>? Cover { get; init; }
    public string FooterLeft { get; init; } = "";
    public string FooterRight { get; init; } = "";
    public double FontSize { get; init; } = 14; // ~10.5pt
}

/// <summary>
/// Dựng tài liệu in khổ A4 (dọc/ngang) từ bảng: lặp tiêu đề cột mỗi trang, tự xuống dòng, đánh số trang "Trang x/y",
/// ghi ngày in. Dùng FixedDocument nên xem trước và in ra giống hệt nhau, in PDF bằng máy in "Microsoft Print to PDF".
/// </summary>
public static class TableDocument
{
    private const double Dpi = 96.0;
    private static readonly Typeface Face = new(new FontFamily("Times New Roman"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private static readonly Typeface FaceBold = new(new FontFamily("Times New Roman"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
    private const double Pad = 4;

    public static Size A4(bool landscape) => landscape ? new Size(297 / 25.4 * Dpi, 210 / 25.4 * Dpi) : new Size(210 / 25.4 * Dpi, 297 / 25.4 * Dpi);
    public static double Mm(double mm) => mm / 25.4 * Dpi;

    public static FixedDocument Build(TableSpec spec, DateTime printedAt)
    {
        var size = A4(spec.Landscape);
        var doc = new FixedDocument();
        doc.DocumentPaginator.PageSize = size;
        // Lề: trái 25 mm (đóng sổ), còn lại 12 mm
        double left = Mm(25), right = Mm(12), top = Mm(12), bottom = Mm(14);
        var width = size.Width - left - right;
        var totalW = spec.Columns.Sum(c => c.Weight);
        var colW = spec.Columns.Select(c => c.Weight / totalW * width).ToArray();
        var fs = spec.FontSize;

        double headH = spec.Columns.Select((c, i) => Measure(c.Title, colW[i], fs, true)).Max() + 2 * Pad;
        double numH = spec.NumberRow ? Measure("(1)", 100, fs - 2, false) + 2 * Pad : 0;
        double runH = Measure("X", 200, fs - 2, false) + 6;
        double footH = Measure("X", 200, fs - 3, false) + 4;
        var firstTitleH = spec.FirstPageTitle.Count == 0 || spec.Cover != null ? 0
            : spec.FirstPageTitle.Sum(t => Measure(t, width, fs + 4, true) + 4) + 8;

        // Phân trang
        var pages = new List<List<(int Index, double Height)>>();
        var current = new List<(int, double)>();
        double avail = size.Height - top - bottom - runH - headH - numH - footH - firstTitleH;
        double used = 0;
        for (var r = 0; r < spec.Rows.Count; r++)
        {
            var row = spec.Rows[r];
            var h = row.Select((t, i) => Measure(t, colW[i], fs, false)).DefaultIfEmpty(0).Max() + 2 * Pad;
            if (current.Count > 0 && used + h > avail)
            {
                pages.Add(current);
                current = [];
                used = 0;
                avail = size.Height - top - bottom - runH - headH - numH - footH;
            }
            current.Add((r, h));
            used += h;
        }
        if (current.Count > 0 || pages.Count == 0) pages.Add(current);

        var hasCover = spec.Cover != null;
        var totalPages = pages.Count + (hasCover ? 1 : 0);
        var pageNo = 0;
        if (hasCover)
        {
            var cover = spec.Cover!(size);
            pageNo++;
            AddFooter(cover, size, left, right, bottom, spec, printedAt, pageNo, totalPages, fs);
            Add(doc, cover);
        }
        for (var p = 0; p < pages.Count; p++)
        {
            pageNo++;
            var page = new FixedPage { Width = size.Width, Height = size.Height, Background = Brushes.White };
            double y = top;
            Text(page, spec.RunningTitle, left, y, width, fs - 2, false, TextAlignment.Left, FontStyles.Italic);
            y += runH;
            if (p == 0 && firstTitleH > 0)
            {
                foreach (var t in spec.FirstPageTitle)
                {
                    var th = Measure(t, width, fs + 4, true);
                    Text(page, t, left, y, width, fs + 4, true, TextAlignment.Center);
                    y += th + 4;
                }
                y += 8;
            }
            // Tiêu đề cột
            double x = left;
            for (var i = 0; i < spec.Columns.Count; i++)
            {
                Cell(page, spec.Columns[i].Title, x, y, colW[i], headH, fs, true, TextAlignment.Center, FontStyles.Normal, Brushes.WhiteSmoke);
                x += colW[i];
            }
            y += headH;
            if (spec.NumberRow)
            {
                x = left;
                for (var i = 0; i < spec.Columns.Count; i++)
                {
                    Cell(page, spec.Columns[i].Number ?? $"({i + 1})", x, y, colW[i], numH, fs - 2, false, TextAlignment.Center, FontStyles.Italic, null);
                    x += colW[i];
                }
                y += numH;
            }
            foreach (var (idx, h) in pages[p])
            {
                x = left;
                var italic = spec.Italic?.Invoke(idx) == true ? FontStyles.Italic : FontStyles.Normal;
                for (var i = 0; i < spec.Columns.Count; i++)
                {
                    Cell(page, spec.Rows[idx][i], x, y, colW[i], h, fs, false,
                        spec.Columns[i].Center ? TextAlignment.Center : TextAlignment.Left, italic, null);
                    x += colW[i];
                }
                y += h;
            }
            if (pages[p].Count == 0)
                Text(page, "(Không có dữ liệu)", left, y + 10, width, fs, false, TextAlignment.Center, FontStyles.Italic);
            AddFooter(page, size, left, right, bottom, spec, printedAt, pageNo, totalPages, fs);
            Add(doc, page);
        }
        return doc;
    }

    private static void AddFooter(FixedPage page, Size size, double left, double right, double bottom, TableSpec spec, DateTime printedAt, int pageNo, int total, double fs)
    {
        var width = size.Width - left - right;
        var y = size.Height - bottom + 2;
        var f = fs - 3;
        Text(page, $"In ngày {printedAt:dd/MM/yyyy HH:mm}{(spec.FooterLeft.Length > 0 ? " – " + spec.FooterLeft : "")}", left, y, width / 2.5, f, false, TextAlignment.Left);
        Text(page, $"Trang {pageNo}/{total}", left, y, width, f, false, TextAlignment.Center);
        Text(page, spec.FooterRight, left + width - width / 2.5, y, width / 2.5, f, false, TextAlignment.Right);
    }

    private static void Add(FixedDocument doc, FixedPage page)
    {
        var pc = new PageContent();
        ((System.Windows.Markup.IAddChild)pc).AddChild(page);
        doc.Pages.Add(pc);
    }

    public static double Measure(string text, double width, double fontSize, bool bold)
    {
        if (string.IsNullOrEmpty(text)) text = " ";
        var ft = new FormattedText(text, CultureInfo.GetCultureInfo("vi-VN"), FlowDirection.LeftToRight, bold ? FaceBold : Face, fontSize, Brushes.Black, 1.0)
        {
            MaxTextWidth = Math.Max(10, width - 2 * Pad),
            Trimming = TextTrimming.None,
        };
        return Math.Ceiling(ft.Height);
    }

    public static TextBlock Text(FixedPage page, string text, double x, double y, double w, double fs, bool bold, TextAlignment align, FontStyle? style = null)
    {
        var tb = new TextBlock
        {
            Text = text, Width = w, FontFamily = Face.FontFamily, FontSize = fs, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            FontStyle = style ?? FontStyles.Normal, TextWrapping = TextWrapping.Wrap, TextAlignment = align, Foreground = Brushes.Black,
        };
        FixedPage.SetLeft(tb, x);
        FixedPage.SetTop(tb, y);
        page.Children.Add(tb);
        return tb;
    }

    private static void Cell(FixedPage page, string text, double x, double y, double w, double h, double fs, bool bold, TextAlignment align, FontStyle style, Brush? bg)
    {
        var rect = new Rectangle { Width = w, Height = h, Stroke = Brushes.Black, StrokeThickness = 0.75, Fill = bg ?? Brushes.Transparent };
        FixedPage.SetLeft(rect, x);
        FixedPage.SetTop(rect, y);
        page.Children.Add(rect);
        Text(page, text, x + Pad, y + Pad, w - 2 * Pad, fs, bold, align, style);
    }
}

/// <summary>In một khoảng trang của tài liệu (cho hộp thoại in chọn trang).</summary>
public sealed class PageRangePaginator(DocumentPaginator inner, int from, int to) : DocumentPaginator
{
    public override DocumentPage GetPage(int pageNumber) => inner.GetPage(pageNumber + from - 1);
    public override bool IsPageCountValid => true;
    public override int PageCount => Math.Max(0, Math.Min(to, inner.PageCount) - from + 1);
    public override Size PageSize { get => inner.PageSize; set => inner.PageSize = value; }
    public override IDocumentPaginatorSource? Source => inner.Source;
}
