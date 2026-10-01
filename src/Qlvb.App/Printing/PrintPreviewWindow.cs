using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Qlvb.App.Infrastructure;
using Qlvb.Application;

namespace Qlvb.App.Printing;

/// <summary>Xem trước khi in; nút In mở hộp thoại Windows để chọn máy in (kể cả "Microsoft Print to PDF"), trang, số bản.</summary>
public sealed class PrintPreviewWindow : Window
{
    private readonly FixedDocument _doc;
    private readonly string _auditDesc;
    private readonly bool _landscape;

    public PrintPreviewWindow(FixedDocument doc, string title, string auditDesc, bool landscape)
    {
        _doc = doc;
        _auditDesc = auditDesc;
        _landscape = landscape;
        Title = "Xem trước khi in – " + title;
        Width = 1100;
        Height = 800;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowState = WindowState.Maximized;
        var dock = new DockPanel();
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8) };
        var print = new Button { Content = "In… (Ctrl+P)", Style = (Style)FindResource("PrimaryButton") };
        print.Click += (_, _) => DoPrint();
        bar.Children.Add(print);
        var close = new Button { Content = "Đóng (Esc)", IsCancel = true };
        close.Click += (_, _) => Close();
        bar.Children.Add(close);
        bar.Children.Add(new TextBlock
        {
            Text = $"{doc.Pages.Count} trang. Để lưu PDF: chọn máy in \"Microsoft Print to PDF\". Bản in là tài liệu mật: quản lý theo quy định.",
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0), Foreground = (System.Windows.Media.Brush)FindResource("Muted"),
        });
        DockPanel.SetDock(bar, Dock.Top);
        dock.Children.Add(bar);
        var viewer = new DocumentViewer { Document = doc };
        // Thay nút in mặc định của DocumentViewer bằng hộp thoại có ghi nhật ký.
        viewer.CommandBindings.Add(new CommandBinding(ApplicationCommands.Print, (_, e) => { DoPrint(); e.Handled = true; }));
        // Không cho sao chép nội dung sổ (tài liệu mật) ra bộ nhớ tạm từ màn hình xem trước.
        viewer.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, (_, e) => e.Handled = true, (_, e) => { e.CanExecute = false; e.Handled = true; }));
        // Ẩn thanh tìm kiếm tiếng Anh mặc định ("Type text to find…").
        viewer.Loaded += (_, _) =>
        {
            if (viewer.Template?.FindName("PART_FindToolBarHost", viewer) is FrameworkElement find) find.Visibility = Visibility.Collapsed;
        };
        dock.Children.Add(viewer);
        Content = dock;
        InputBindings.Add(new KeyBinding(ApplicationCommands.Print, Key.P, ModifierKeys.Control));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Print, (_, _) => DoPrint()));
    }

    private void DoPrint()
    {
        var pd = new PrintDialog { UserPageRangeEnabled = true, MinPage = 1, MaxPage = (uint)_doc.Pages.Count, PageRange = new PageRange(1, _doc.Pages.Count) };
        try
        {
            pd.PrintTicket.PageOrientation = _landscape ? PageOrientation.Landscape : PageOrientation.Portrait;
            pd.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA4);
        }
        catch (Exception) { /* máy in không hỗ trợ: dùng mặc định */ }
        if (pd.ShowDialog() != true) return;
        Dlg.Try(() =>
        {
            DocumentPaginator pag = _doc.DocumentPaginator;
            var range = "tất cả";
            if (pd.PageRangeSelection == PageRangeSelection.UserPages)
            {
                pag = new PageRangePaginator(pag, Math.Max(1, pd.PageRange.PageFrom), Math.Min(_doc.Pages.Count, pd.PageRange.PageTo));
                range = $"trang {pd.PageRange.PageFrom}–{pd.PageRange.PageTo}";
            }
            using (new WaitCursor()) pd.PrintDocument(pag, Title);
            Ctx.Store.AppendAudit(new AuditEntry("In", "in_an", null, $"{_auditDesc}; {range}; máy in: {pd.PrintQueue?.FullName}"));
        }, "in");
    }

    public static void Show(Window owner, FixedDocument doc, string title, string auditDesc, bool landscape) =>
        new PrintPreviewWindow(doc, title, auditDesc, landscape) { Owner = owner }.ShowDialog();
}
