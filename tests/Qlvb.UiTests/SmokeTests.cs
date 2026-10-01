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
        var work = Path.Combine(Path.GetDirectoryName(exe)!, "..", "uiwork");
        if (Directory.Exists(work)) Directory.Delete(work, true);
        Directory.CreateDirectory(work);
        var copy = Path.Combine(work, "QLVB.exe");
        File.Copy(exe, copy);
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
        var form = WaitWindow("Văn bản đi");
        Shot(form, "form-van-ban-di");
        var trichYeu = form.FindFirstDescendant(cf => cf.ByAutomationId("TxtTrichYeu"))!.AsTextBox();
        trichYeu.Focus();
        Keyboard.Type("Noi dung thu");
        var cb = form.FindFirstDescendant(cf => cf.ByAutomationId("CbDoMat"))!.AsComboBox();
        // Chọn TUYỆT MẬT (mức cao nhất đứng đầu). Chạy nền vì phần mềm mở hộp xác nhận ngay trong sự kiện chọn.
        _ = Task.Run(() => { try { cb.Select(0); } catch (Exception) { /* UIA chờ hộp thoại: bình thường */ } });
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

        // Khóa màn hình
        main.Focus();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_L);
        var lockW = WaitWindow("Đã khóa");
        Shot(lockW, "khoa-man-hinh");
        Type(lockW, "TxtPassword", Password);
        Click(lockW, "Mở khóa");
        Thread.Sleep(800);

        // Thoát (tự sao lưu khi thoát)
        main = WaitWindow("Quản lý văn bản đi – đến");
        main.Close();
        AnswerConfirm(true);
        var exited = SpinWait(() => _app.HasExited, TimeSpan.FromSeconds(30));
        Assert.True(exited, "Phần mềm không thoát");

        // Kiểm tra: có bản sao lưu khi thoát, nhật ký kỹ thuật không có lỗi
        var backups = Directory.GetFiles(Path.Combine(work, "Data", "Backup"), "*_khithoat.qlvbak");
        Assert.NotEmpty(backups);
        var logs = Directory.GetFiles(Path.Combine(work, "Data", "Logs"), "*.log").SelectMany(File.ReadAllLines).ToList();
        foreach (var l in logs) output.WriteLine(l);
        Assert.DoesNotContain(logs, l => l.Contains("[ERR]") || l.Contains("[FTL]"));
        Assert.DoesNotContain(logs, l => l.Contains(Password));
    }

    // ------------------------------------------------------------------ tiện ích
    private Window WaitWindow(string titlePart)
    {
        Window? found = null;
        SpinWait(() =>
        {
            found = _app!.GetAllTopLevelWindows(_auto).FirstOrDefault(w => w.Title.Contains(titlePart, StringComparison.Ordinal))
                    ?? _app.GetAllTopLevelWindows(_auto).SelectMany(w => w.ModalWindows).FirstOrDefault(w => w.Title.Contains(titlePart, StringComparison.Ordinal));
            return found != null;
        }, Wait);
        if (found == null)
        {
            var titles = string.Join(" | ", _app!.GetAllTopLevelWindows(_auto).Select(w => w.Title));
            throw new Exception($"Không thấy cửa sổ \"{titlePart}\". Đang có: {titles}");
        }
        found.SetForeground();
        return found;
    }

    private Window WaitDialog() => WaitWindow("Quản lý văn bản đi – đến");

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

    private void AssertNoErrorDialog()
    {
        var err = AllWindows().FirstOrDefault(w => w.FindFirstDescendant(cf => cf.ByName("OK").And(cf.ByControlType(ControlType.Button))) != null);
        if (err != null)
        {
            Shot(err, "loi-bat-ngo");
            var text = string.Join(" ", err.FindAllDescendants(cf => cf.ByControlType(ControlType.Text)).Select(t => t.Name));
            throw new Exception("Xuất hiện hộp thoại lỗi: " + text);
        }
    }

    private IEnumerable<Window> AllWindows()
    {
        var tops = _app!.GetAllTopLevelWindows(_auto);
        return tops.Concat(tops.SelectMany(t => t.ModalWindows));
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
