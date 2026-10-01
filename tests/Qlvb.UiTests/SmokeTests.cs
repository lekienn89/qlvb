using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Xunit.Abstractions;

namespace Qlvb.UiTests;

/// <summary>
/// Kiểm thử khói giao diện trên Windows: chạy QLVB.exe thật (bản portable trong thư mục riêng), thiết lập lần đầu,
/// nạp dữ liệu mẫu, mở từng trang, mở form nhập, kiểm tra quy tắc TUYỆT MẬT, khóa/mở khóa, thoát. Chụp ảnh màn hình từng bước.
/// Chỉ chạy khi có biến môi trường QLVB_EXE.
/// </summary>
public sealed class SmokeTests(ITestOutputHelper output) : IDisposable
{
    private const string Password = "MatKhau@2026";
    private const string NewPassword = "MatKhauMoi#2027";
    private readonly UIA3Automation _auto = new();
    private Application? _app;
    private readonly string _shots = Environment.GetEnvironmentVariable("QLVB_SCREENSHOTS") ?? Path.Combine(AppContext.BaseDirectory, "screenshots");
    private int _shot;

    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

    [Fact]
    public void FullFlow()
    {
        var exe = Environment.GetEnvironmentVariable("QLVB_EXE");
        if (string.IsNullOrEmpty(exe) || !OperatingSystem.IsWindows())
        {
            output.WriteLine("Bỏ qua: không có QLVB_EXE");
            return;
        }
        Directory.CreateDirectory(_shots);
        var work = _work = Path.Combine(Path.GetDirectoryName(exe)!, "..", "uiwork");
        if (Directory.Exists(work)) Directory.Delete(work, true);
        Directory.CreateDirectory(work);
        var copy = Path.Combine(work, "QLVB.exe");
        File.Copy(exe, copy);
        foreach (var dll in Directory.GetFiles(Path.GetDirectoryName(exe)!, "*.dll")) File.Copy(dll, Path.Combine(work, Path.GetFileName(dll)));
        var extractDir = Path.Combine(Path.GetTempPath(), ".net", "QLVB");
        if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
        File.WriteAllText(Path.Combine(work, "portable.flag"), "test");

        _app = Application.Launch(copy);
        var login = _app.GetMainWindow(_auto, Wait) ?? throw new Exception("Không thấy cửa sổ đăng nhập");
        Shot(login, "thiet-lap-lan-dau");

        // Thiết lập lần đầu
        Type(login, "TxtTenCoQuan", "Cơ quan Thử nghiệm");
        Type(login, "TxtNewPw1", Password);
        Type(login, "TxtNewPw2", Password);
        Click(login, "Thiết lập");

        var rec = WaitWindow("Mã khôi phục");
        Shot(rec, "ma-khoi-phuc");
        var code = rec.FindFirstDescendant(cf => cf.ByAutomationId("TxtCode"))!.AsTextBox().Text;
        Assert.Matches("^[A-Z0-9]{5}(-[A-Z0-9]{5}){3}$", code);
        Type(rec, "TxtConfirm", code[^5..]);
        rec.FindFirstDescendant(cf => cf.ByAutomationId("BtnOk"))!.AsButton().Invoke();

        var main = WaitWindow("Trang chủ");
        Thread.Sleep(1000);
        Shot(main, "trang-chu-trong");

        // Nạp dữ liệu mẫu
        Click(main, "Nạp dữ liệu mẫu");
        AnswerConfirm(true);
        Thread.Sleep(3000);
        main = WaitWindow("Trang chủ");
        Shot(main, "trang-chu-du-lieu-mau");

        foreach (var (nav, name) in new[]
                 {
                     ("Văn bản đi", "van-ban-di"), ("Văn bản đến", "van-ban-den"), ("Tra cứu", "tra-cuu"), ("Báo cáo – Thống kê", "bao-cao"),
                     ("Danh mục", "danh-muc"), ("Sao lưu / Khôi phục", "sao-luu"), ("Nhật ký", "nhat-ky"), ("Cấu hình", "cau-hinh"), ("Trợ giúp", "tro-giup"),
                 })
        {
            Nav(main, nav);
            Thread.Sleep(800);
            Shot(main, name);
            AssertNoErrorDialog();
        }

        // Form văn bản đi: chọn TUYỆT MẬT → ô trích yếu bị khóa
        Nav(main, "Văn bản đi");
        Thread.Sleep(500);
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_N);
        var form = WaitWindow("Văn bản đi", exact: true); // tiêu đề cửa sổ chính cũng chứa "Văn bản đi"
        Shot(form, "form-van-ban-di");
        var trichYeu = form.FindFirstDescendant(cf => cf.ByAutomationId("TxtTrichYeu"))!.AsTextBox();
        trichYeu.Focus();
        Keyboard.Type("Noi dung thu");
        var cb = form.FindFirstDescendant(cf => cf.ByAutomationId("CbDoMat"))!.AsComboBox();
        // Chọn TUYỆT MẬT (mức cao nhất đứng đầu) bằng bàn phím: phần mềm mở hộp xác nhận ngay trong sự kiện chọn,
        // gọi UIA Select sẽ bị chặn đến khi hộp thoại đóng.
        cb.Focus();
        Thread.Sleep(200);
        Keyboard.Press(VirtualKeyShort.HOME);
        AnswerConfirm(true);       // xác nhận xóa trích yếu
        DismissMessage();          // thông báo đã khóa ô trích yếu
        Thread.Sleep(500);
        Assert.False(trichYeu.IsEnabled);
        Assert.Equal("", trichYeu.Text);
        Shot(form, "form-tuyet-mat");

        // Lưu thiếu thông tin → thông báo lỗi tiếng Việt
        Click(form, "Lưu (Ctrl+S)");
        var warn = WaitDialog();
        Shot(warn, "loi-kiem-tra");
        DismissMessage();
        form.Close();
        AnswerConfirm(true); // bỏ thông tin chưa lưu
        Thread.Sleep(500);

        // ---------------------------------------------------------------- nghiệp vụ chính trên giao diện
        // Đăng ký một văn bản đi thật (không phải dữ liệu mẫu) bằng bàn phím
        main = WaitWindow("– Quản lý văn bản đi – đến");
        main.Focus();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_N);
        form = WaitWindow("Văn bản đi", exact: true);
        Type(form, "CbTenLoai", "Công văn");
        TypeReplace(form, "TxtSoKyHieu", "99/UI-TEST");
        var cbDm = form.FindFirstDescendant(cf => cf.ByAutomationId("CbDoMat"))!;
        cbDm.Focus();
        Thread.Sleep(200);
        Keyboard.Press(VirtualKeyShort.END); // MẬT (mức thấp nhất)
        Thread.Sleep(300);
        Type(form, "CbNguoiKy", "Trần Thị Thử");
        Type(form, "CbDonViLuu", "Văn phòng (mẫu)");
        Type(form, "TxtTrichYeu", "Kiểm thử giao diện tự động");
        Type(form, "CbNoiNhan", "Phòng Kế hoạch kiểm thử");
        Click(form, "Thêm nơi nhận");
        Shot(form, "nhap-van-ban-di");
        Click(form, "Lưu (Ctrl+S)");
        DismissMessage(); // "Đã đăng ký văn bản đi số …"
        if (!SpinWait(() => !AllWindows().Any(w => w.Title == "Văn bản đi"), Wait))
        {
            Shot(form, "form-khong-dong");
            throw new Exception("Form không đóng sau khi lưu. Hộp thoại đang mở: " + DescribeDialogs());
        }
        AssertNoErrorDialog();

        // Lọc danh sách đúng văn bản vừa nhập
        main = WaitWindow("Văn bản đi – Quản lý văn bản đi – đến");
        Type(main, "TxtSearch", "UI-TEST");
        Thread.Sleep(800);
        var row = WaitRow(main, "99/UI-TEST");
        Shot(main, "danh-sach-sau-khi-luu");

        // Sửa: đổi ghi chú → hộp xác nhận thay đổi
        row.AsGridRow().Select();
        Click(main, "Sửa");
        var edit = WaitWindow("Sửa văn bản đi", exact: true);
        Type(edit, "TxtGhiChu", "Đã sửa qua kiểm thử");
        Click(edit, "Lưu thay đổi (Ctrl+S)");
        var xacNhan = WaitWindow("Xác nhận thay đổi", exact: true);
        Shot(xacNhan, "xac-nhan-thay-doi");
        Click(xacNhan, "Lưu thay đổi");
        DismissMessageIfAny();
        Assert.True(SpinWait(() => !AllWindows().Any(w => w.Title == "Sửa văn bản đi"), Wait), "Form sửa không đóng");
        WaitRow(main, "Đã sửa qua kiểm thử");

        // Hủy văn bản (bắt buộc lý do) rồi khôi phục
        WaitRow(main, "99/UI-TEST").AsGridRow().Select();
        Click(main, "Hủy văn bản");
        var huy = WaitWindow("Hủy văn bản", exact: true);
        Keyboard.Type("Nhap nham khi kiem thu");
        Click(huy, "Hủy văn bản");
        WaitRow(main, "Đã hủy");
        Shot(main, "van-ban-da-huy");
        WaitRow(main, "99/UI-TEST").AsGridRow().Select();
        Click(main, "Khôi phục");
        AnswerConfirm(true);
        Assert.True(SpinWait(() => FindRow(main, "Đã hủy") == null, Wait), "Văn bản chưa được khôi phục");

        // Xem trước khi in sổ
        main.Focus();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_P);
        var opt = WaitWindow("In sổ");
        Click(opt, "Xem trước");
        var preview = WaitWindow("Xem trước khi in");
        Thread.Sleep(1500);
        Shot(preview, "xem-truoc-in");
        AssertNoErrorDialog();
        Click(preview, "Đóng (Esc)");
        Thread.Sleep(500);
        Click(opt, "Đóng");
        Thread.Sleep(500);
        Assert.DoesNotContain(AllWindows(), w => w.Title.StartsWith("In sổ", StringComparison.Ordinal) || w.Title.StartsWith("Xem trước", StringComparison.Ordinal));

        // Sao lưu nhanh
        Nav(main, "Sao lưu / Khôi phục");
        Thread.Sleep(500);
        Click(main, "Sao lưu nhanh");
        DismissMessage();
        Shot(main, "sao-luu-nhanh");

        // Đổi mật khẩu (dùng mật khẩu mới để mở khóa màn hình ở bước sau)
        Nav(main, "Cấu hình");
        Thread.Sleep(500);
        Click(main, "Đổi mật khẩu…");
        foreach (var pw in new[] { Password, NewPassword, NewPassword })
        {
            WaitWindow("Đổi mật khẩu", exact: true);
            Thread.Sleep(400);
            Keyboard.Type(pw);
            Keyboard.Press(VirtualKeyShort.RETURN);
            Thread.Sleep(800);
        }
        DismissMessage(); // "Đã đổi mật khẩu"

        // Xóa dữ liệu mẫu: văn bản thật phải còn nguyên
        Click(main, "Xóa dữ liệu mẫu");
        AnswerConfirm(true);
        DismissMessage();
        Nav(main, "Văn bản đi");
        Thread.Sleep(800);
        var search = main.FindFirstDescendant(c => c.ByAutomationId("TxtSearch"))!.AsTextBox();
        search.Text = "";
        Thread.Sleep(800);
        WaitRow(main, "99/UI-TEST");
        Assert.Null(FindRow(main, "[MẪU]"));
        Shot(main, "sau-khi-xoa-du-lieu-mau");

        // Xóa văn bản nhập sai (cần xác nhận, mật khẩu, lý do); đây là số cuối nên số được cấp lại
        WaitRow(main, "99/UI-TEST").AsGridRow().Select();
        Click(main, "Xóa văn bản nhập sai…");
        AnswerConfirm(true);
        WaitWindow("Xác nhận mật khẩu", exact: true);
        Thread.Sleep(400);
        Keyboard.Type(NewPassword);
        Keyboard.Press(VirtualKeyShort.RETURN);
        var lyDoXoa = WaitWindow("Lý do xóa", exact: true);
        Thread.Sleep(400);
        Keyboard.Type("Nhap sai ngay khi kiem thu");
        Click(lyDoXoa, "Xóa văn bản");
        var daXoa = WaitWindow("Quản lý văn bản đi – đến", exact: true);
        var thongBao = string.Join(" ", daXoa.FindAllDescendants(c => c.ByControlType(ControlType.Text)).Select(t => t.Name));
        Assert.True(thongBao.Contains("được cấp cho văn bản nhập tiếp theo", StringComparison.Ordinal), thongBao + Environment.NewLine + LogErrors());
        DismissMessage();
        Assert.True(SpinWait(() => FindRow(main, "99/UI-TEST") == null, Wait), "Văn bản chưa bị xóa");
        AssertNoErrorDialog();

        // Khóa màn hình
        main.Focus();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_L);
        var lockW = WaitWindow("Đã khóa");
        Shot(lockW, "khoa-man-hinh");
        Type(lockW, "TxtPassword", NewPassword);
        Click(lockW, "Mở khóa");
        Thread.Sleep(800);

        // Thoát (tự sao lưu khi thoát)
        main = WaitWindow("– Quản lý văn bản đi – đến");
        Assert.DoesNotContain(AllWindows(), w => w.Title.StartsWith("Đã khóa", StringComparison.Ordinal)); // mở khóa bằng mật khẩu mới thành công
        // Đóng bằng Alt+F4: nút X qua UIA bị chặn đồng bộ bởi hộp xác nhận thoát.
        main.Focus();
        Thread.Sleep(300);
        Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.F4);
        AnswerConfirm(true);
        var exited = SpinWait(() => _app.HasExited, TimeSpan.FromSeconds(30));
        Assert.True(exited, "Phần mềm không thoát");

        // Kiểm tra: có bản sao lưu khi thoát, nhật ký kỹ thuật không có lỗi
        var backups = Directory.GetFiles(Path.Combine(work, "Data", "Backup"), "*_khithoat.qlvbak");
        Assert.NotEmpty(backups);
        var logs = Directory.GetFiles(Path.Combine(work, "Data", "Logs"), "*.log").SelectMany(File.ReadAllLines).ToList();
        foreach (var l in logs) output.WriteLine(l);
        Assert.DoesNotContain(logs, l => l.Contains("[ERR]") || l.Contains("[FTL]"));
        Assert.DoesNotContain(logs, l => l.Contains(Password) || l.Contains(NewPassword) || l.Contains(code));
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(work, "Data", "Backup"), "*_nhanh.qlvbak"));
        // Không giải nén thư viện ra thư mục tạm của Windows khi chạy
        Assert.False(Directory.Exists(extractDir), "Phần mềm đã giải nén tệp vào " + extractDir);
    }

    // ------------------------------------------------------------------ tiện ích
    private Window WaitWindow(string titlePart, bool exact = false)
    {
        Window? found = null;
        SpinWait(() =>
        {
            found = AllWindows().FirstOrDefault(w => exact ? w.Title == titlePart : w.Title.Contains(titlePart, StringComparison.Ordinal));
            return found != null;
        }, Wait);
        if (found == null)
        {
            var titles = string.Join(" | ", AllWindows().Select(w => w.Title));
            throw new Exception($"Không thấy cửa sổ \"{titlePart}\". Đang có: {titles}");
        }
        found.SetForeground();
        return found;
    }

    private Window WaitDialog() => WaitWindow("Quản lý văn bản đi – đến", exact: true); // hộp thông báo (cửa sổ chính có tiền tố tên trang)

    /// <summary>Trả lời hộp xác nhận (nút "Đồng ý"/"Không").</summary>
    private void AnswerConfirm(bool yes)
    {
        AutomationElement? btn = null;
        SpinWait(() =>
        {
            btn = AllWindows().Select(w => w.FindFirstDescendant(cf => cf.ByName(yes ? "Đồng ý" : "Không").And(cf.ByControlType(ControlType.Button)))).FirstOrDefault(b => b != null);
            return btn != null;
        }, Wait);
        Assert.NotNull(btn);
        Thread.Sleep(200);
        btn!.AsButton().Invoke();
        Thread.Sleep(400);
    }

    /// <summary>Đóng hộp thông báo (MessageBox nút OK).</summary>
    private void DismissMessage()
    {
        AutomationElement? btn = null;
        SpinWait(() =>
        {
            btn = AllWindows().Select(w => w.FindFirstDescendant(cf => cf.ByName("OK").And(cf.ByControlType(ControlType.Button)))).FirstOrDefault(b => b != null);
            return btn != null;
        }, Wait);
        Assert.NotNull(btn);
        btn!.AsButton().Invoke();
        Thread.Sleep(300);
    }

    /// <summary>Đóng hộp thông báo nếu có xuất hiện trong vài giây.</summary>
    private void DismissMessageIfAny()
    {
        AutomationElement? btn = null;
        if (SpinWait(() => (btn = AllWindows().Select(w => w.FindFirstDescendant(c => c.ByName("OK").And(c.ByControlType(ControlType.Button)))).FirstOrDefault(b => b != null)) != null, TimeSpan.FromSeconds(4)))
        {
            btn!.AsButton().Invoke();
            Thread.Sleep(300);
        }
    }

    private string? _work;

    /// <summary>Các dòng lỗi trong nhật ký kỹ thuật (đã lọc thông tin nhạy cảm) để chẩn đoán khi kiểm thử thất bại.</summary>
    private string LogErrors()
    {
        try
        {
            var lines = Directory.GetFiles(Path.Combine(_work!, "Data", "Logs"), "*.log").SelectMany(File.ReadAllLines).ToList();
            var idx = lines.FindLastIndex(l => l.Contains("[ERR]") || l.Contains("[FTL]"));
            return idx < 0 ? "(nhật ký không có lỗi)" : string.Join(Environment.NewLine, lines.Skip(idx).Take(25));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return "(không đọc được nhật ký)"; }
    }

    private void AssertNoErrorDialog()
    {
        var err = AllWindows().FirstOrDefault(w => w.FindFirstDescendant(cf => cf.ByName("OK").And(cf.ByControlType(ControlType.Button))) != null);
        if (err != null)
        {
            Shot(err, "loi-bat-ngo");
            var text = string.Join(" ", err.FindAllDescendants(cf => cf.ByControlType(ControlType.Text)).Select(t => t.Name));
            throw new Exception("Xuất hiện hộp thoại lỗi: " + text + Environment.NewLine + LogErrors());
        }
    }

    /// <summary>Mọi cửa sổ của tiến trình, kể cả hộp thoại lồng nhiều cấp (hộp xác nhận mở từ form đang là modal).</summary>
    private List<Window> AllWindows()
    {
        var result = new List<Window>();
        var seen = new HashSet<string>();
        void Add(Window w, int depth)
        {
            string key;
            try { key = w.Properties.NativeWindowHandle.ValueOrDefault.ToString() + "|" + w.Title; }
            catch (Exception) { return; }
            if (!seen.Add(key)) return;
            result.Add(w);
            if (depth > 5) return;
            Window[] modals;
            try { modals = w.ModalWindows; } catch (Exception) { return; }
            foreach (var m in modals) Add(m, depth + 1);
        }
        try
        {
            foreach (var e in _auto.GetDesktop().FindAllChildren(cf => cf.ByProcessId(_app!.ProcessId)))
                if (e.ControlType == ControlType.Window) Add(e.AsWindow(), 0);
        }
        catch (Exception) { /* cửa sổ đang đóng/mở */ }
        try { foreach (var w in _app!.GetAllTopLevelWindows(_auto)) Add(w, 0); }
        catch (Exception) { }
        // Hộp thoại có chủ là cửa sổ khác vẫn có thể nằm dưới cửa sổ chủ trong cây UIA
        foreach (var w in result.ToList())
        {
            try
            {
                foreach (var d in w.FindAllDescendants(cf => cf.ByControlType(ControlType.Window)))
                    Add(d.AsWindow(), 1);
            }
            catch (Exception) { }
        }
        return result;
    }

    /// <summary>Dòng của bảng (DataGrid "Grid") có ô chứa đoạn văn bản.</summary>
    private static AutomationElement? FindRow(Window main, string text)
    {
        var grid = main.FindFirstDescendant(c => c.ByAutomationId("Grid"));
        if (grid == null) return null;
        foreach (var r in grid.FindAllChildren(c => c.ByControlType(ControlType.DataItem)))
            if (r.FindAllDescendants().Any(e => (e.Properties.Name.ValueOrDefault ?? "").Contains(text, StringComparison.Ordinal)))
                return r;
        return null;
    }

    private AutomationElement WaitRow(Window main, string text)
    {
        AutomationElement? row = null;
        SpinWait(() => (row = FindRow(main, text)) != null, Wait);
        if (row == null)
        {
            Shot(main, "khong-thay-dong");
            throw new Exception($"Không thấy dòng chứa \"{text}\" trong danh sách");
        }
        return row;
    }

    /// <summary>Tiêu đề và nội dung chữ của các hộp thoại đang mở (để chẩn đoán khi kiểm thử lỗi).</summary>
    private string DescribeDialogs() => string.Join(" || ", AllWindows().Select(w =>
    {
        try { return w.Title + ": " + string.Join(" ", w.FindAllDescendants(c => c.ByControlType(ControlType.Text)).Select(t => t.Name).Take(15)); }
        catch (Exception) { return "?"; }
    }));

    private static void TypeReplace(AutomationElement root, string id, string text)
    {
        var e = root.FindFirstDescendant(c => c.ByAutomationId(id)) ?? throw new Exception("Không thấy ô " + id);
        e.Focus();
        Thread.Sleep(100);
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        Keyboard.Type(text);
        Thread.Sleep(100);
    }

    private static void Type(AutomationElement root, string id, string text)
    {
        var e = root.FindFirstDescendant(cf => cf.ByAutomationId(id)) ?? throw new Exception("Không thấy ô " + id);
        e.Focus();
        Thread.Sleep(100);
        Keyboard.Type(text);
        Thread.Sleep(100);
    }

    private static void Click(AutomationElement root, string name)
    {
        var e = root.FindFirstDescendant(cf => cf.ByName(name).And(cf.ByControlType(ControlType.Button))) ?? throw new Exception("Không thấy nút " + name);
        e.AsButton().Invoke();
        Thread.Sleep(300);
    }

    private static void Nav(Window main, string name)
    {
        var e = main.FindFirstDescendant(cf => cf.ByName(name).And(cf.ByControlType(ControlType.RadioButton))) ?? throw new Exception("Không thấy menu " + name);
        e.AsRadioButton().IsChecked = true;
    }

    private void Shot(AutomationElement e, string name)
    {
        try
        {
            Capture.Element(e).ToFile(Path.Combine(_shots, $"{++_shot:00}-{name}.png"));
        }
        catch (Exception ex)
        {
            output.WriteLine($"Không chụp được {name}: {ex.Message}");
        }
    }

    private static bool SpinWait(Func<bool> cond, TimeSpan timeout)
    {
        var end = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < end)
        {
            try { if (cond()) return true; } catch (Exception) { /* cửa sổ đang chuyển */ }
            Thread.Sleep(250);
        }
        return false;
    }

    public void Dispose()
    {
        try { if (_app is { HasExited: false }) _app.Kill(); } catch (Exception) { }
        _app?.Dispose();
        _auto.Dispose();
    }
}
