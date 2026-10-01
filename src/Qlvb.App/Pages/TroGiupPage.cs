using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Qlvb.App.Infrastructure;

namespace Qlvb.App.Pages;

public sealed class TroGiupPage : UserControl, IPage
{
    public string Title => "Trợ giúp";

    private static readonly (string H, string[] P)[] Sections =
    [
        ("Căn cứ pháp lý", [
            "Luật Bảo vệ bí mật nhà nước số 117/2025/QH15; Nghị định số 63/2026/NĐ-CP ngày 28/02/2026 và Phụ lục III (thay thế theo Công văn số 2663/VPCP-NC ngày 27/03/2026): Sổ đăng ký bí mật nhà nước đi, đến.",
            "Thông tư 24/2020/TT-BCA (mẫu số 14, 15) chỉ dùng để đối chiếu lịch sử, không còn là căn cứ hiện hành.",
            "Máy tính dùng phần mềm phải là máy KHÔNG kết nối Internet/mạng (Luật 117/2025/QH15). Phần mềm không có bất kỳ chức năng kết nối mạng nào.",
            "Sổ in từ phần mềm phục vụ ký nhận, lưu giữ theo quy định của cơ quan; phần mềm không thay thế quy trình quản lý sổ của cơ quan.",
        ]),
        ("Bắt đầu", [
            "Lần đầu: đặt mật khẩu và ghi lại MÃ KHÔI PHỤC ra giấy. Mất cả hai thì không mở được dữ liệu.",
            "Vào Cấu hình nhập tên cơ quan, ký hiệu cơ quan, mẫu số ký hiệu. Vào Danh mục nhập người ký, đơn vị.",
            "Có thể nạp dữ liệu mẫu ở Trang chủ để làm quen, rồi bấm \"Xóa dữ liệu mẫu\" trước khi nhập thật.",
        ]),
        ("Đăng ký văn bản", [
            "Văn bản đi: đăng ký trước khi phát hành. Văn bản đến: đăng ký sau khi tiếp nhận (Điều 6 Nghị định 63/2026/NĐ-CP).",
            "Số thứ tự do phần mềm cấp tự động theo năm, tiếp nối giữa các quyển. Hủy văn bản thì số vẫn giữ; chỉ khi xóa văn bản mang số cuối cùng thì số đó mới được cấp lại.",
            "Chọn độ mật TUYỆT MẬT: ô trích yếu bị khóa, nội dung đã nhập bị xóa; cột tên loại và trích yếu trên sổ chỉ ghi tên loại.",
            "Nút + cạnh ô danh mục để thêm nhanh. Nút \"Sao chép từ văn bản trước\" chỉ chép các thông tin lặp lại; luôn kiểm tra trước khi lưu.",
            "Phần mềm cảnh báo khi nghi trùng số, ký hiệu; người dùng quyết định có lưu hay không.",
        ]),
        ("Sửa, hủy, khôi phục", [
            "Sửa: hiện bảng so sánh giá trị cũ/mới để xác nhận; mọi thay đổi ghi vào nhật ký. Không đổi được số thứ tự, số đến, năm.",
            "Hủy: cần lý do; văn bản vẫn nằm trên sổ với ghi chú \"ĐÃ HỦY\". Có thể khôi phục.",
            "Xóa văn bản nhập sai: chọn văn bản, bấm nút \"Xóa văn bản nhập sai…\" hoặc nhấn chuột phải chọn lệnh cùng tên (cần mật khẩu và lý do, ghi nhật ký). Xóa văn bản mang số cuối cùng của năm thì số đó được cấp lại; xóa văn bản ở giữa thì số đó để trống.",
            "Sổ đã khóa thì không thêm, sửa, hủy, xóa văn bản được (Danh mục → Sổ đăng ký).",
        ]),
        ("In và xuất", [
            "In sổ: chọn năm, quyển, trang bìa, khổ giấy; xem trước rồi bấm In. Lưu PDF bằng máy in \"Microsoft Print to PDF\".",
            "Xuất Excel/CSV danh sách văn bản mặc định bị tắt vì tệp không mã hóa. Báo cáo thống kê (chỉ số lượng) vẫn xuất được.",
        ]),
        ("Sao lưu", [
            "Sao lưu nhanh lưu vào thư mục Backup; nên chép thêm bản sao lưu ra thiết bị lưu trữ được quản lý.",
            "Bản sao lưu được mã hóa, mở bằng mật khẩu đang dùng lúc sao lưu. Đổi mật khẩu xong nên sao lưu lại.",
            "Khôi phục: phần mềm tự sao lưu an toàn dữ liệu hiện tại trước khi thay thế.",
        ]),
        ("Phím tắt", [
            "Ctrl+N: thêm mới • Ctrl+S: lưu • Ctrl+F: tìm • Ctrl+P: in • F5: làm mới • Esc: đóng cửa sổ • Ctrl+L: khóa màn hình • Enter trên danh sách: mở văn bản.",
        ]),
    ];

    public TroGiupPage()
    {
        var doc = new FlowDocument { FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), FontSize = 14, PagePadding = new Thickness(8), TextAlignment = TextAlignment.Left };
        doc.Blocks.Add(new Paragraph(new Run("Hướng dẫn sử dụng nhanh")) { FontSize = 22, FontWeight = FontWeights.SemiBold });
        foreach (var (h, ps) in Sections)
        {
            doc.Blocks.Add(new Paragraph(new Run(h)) { FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 4) });
            var list = new List { MarkerStyle = TextMarkerStyle.Disc };
            foreach (var p in ps) list.ListItems.Add(new ListItem(new Paragraph(new Run(p))));
            doc.Blocks.Add(list);
        }
        doc.Blocks.Add(new Paragraph(new Run("Tài liệu chi tiết đi kèm phần mềm trong thư mục docs, mở bằng các nút phía trên.")) { Foreground = System.Windows.Media.Brushes.DimGray });

        var buttons = new WrapPanel { Margin = new Thickness(8, 4, 8, 8) };
        foreach (var (text, file) in Docs)
        {
            var b = new Button { Content = text, Margin = new Thickness(0, 0, 8, 0) };
            b.Click += (_, _) => OpenDoc(file);
            buttons.Children.Add(b);
        }
        var root = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Top);
        root.Children.Add(buttons);
        root.Children.Add(new FlowDocumentScrollViewer { Document = doc, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = root;
    }

    private static readonly (string Text, string File)[] Docs =
    [
        ("Hướng dẫn sử dụng đầy đủ", "HuongDanSuDung.html"),
        ("Hướng dẫn quản trị", "HuongDanQuanTri.html"),
        ("Nhật ký thay đổi", "CHANGELOG.html"),
    ];

    /// <summary>Mở tài liệu HTML cục bộ (thư mục docs cạnh QLVB.exe) bằng trình duyệt mặc định. Tài liệu không tải gì từ mạng.</summary>
    private static void OpenDoc(string file)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "docs", file);
        if (!File.Exists(path))
        {
            Dlg.Info($"Không tìm thấy tài liệu {file} trong thư mục docs cạnh phần mềm. Hãy cài lại phần mềm hoặc giải nén lại bản portable.");
            return;
        }
        Dlg.Try(() => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true })?.Dispose(), "mở tài liệu");
    }

    public void OnShow() { }
}
