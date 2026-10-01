using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Qlvb.Infrastructure.Security;
using Qlvb.Infrastructure.Services;

namespace Qlvb.App.Infrastructure;

public sealed class WaitCursor : IDisposable
{
    // Con trỏ cũ là con trỏ dùng chung của hệ thống (Cursors.*): chỉ đặt lại, không được giải phóng.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213", Justification = "Con trỏ hệ thống dùng chung, không thuộc sở hữu của lớp này.")]
    private readonly Cursor? _old = Mouse.OverrideCursor;
    public WaitCursor() => Mouse.OverrideCursor = Cursors.Wait;
    public void Dispose() => Mouse.OverrideCursor = _old;
}

/// <summary>Hộp thoại nhập một giá trị (văn bản, nhiều dòng hoặc mật khẩu).</summary>
public sealed class InputDialog : Window
{
    private readonly TextBox? _text;
    private readonly PasswordBox? _pw;
    private readonly int _minLength;
    private readonly TextBlock _err = new() { Foreground = System.Windows.Media.Brushes.Firebrick, TextWrapping = TextWrapping.Wrap };

    public string Value => _pw?.Password ?? _text?.Text ?? "";

    private InputDialog(string title, string prompt, bool password, bool multiline, string initial, int minLength, int maxLength, string okText)
    {
        Title = title;
        Width = 480;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _minLength = minLength;
        var sp = new StackPanel { Margin = new Thickness(20) };
        sp.Children.Add(new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        if (password)
        {
            _pw = new PasswordBox { MaxLength = maxLength };
            sp.Children.Add(_pw);
        }
        else
        {
            _text = new TextBox { Text = initial, MaxLength = maxLength, AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap };
            if (multiline) { _text.MinHeight = 80; _text.VerticalContentAlignment = VerticalAlignment.Top; _text.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; }
            sp.Children.Add(_text);
        }
        sp.Children.Add(_err);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var ok = new Button { Content = okText, IsDefault = !multiline, Style = (Style)System.Windows.Application.Current.FindResource("PrimaryButton") };
        ok.Click += (_, _) =>
        {
            if (Value.Trim().Length < _minLength)
            {
                _err.Text = _minLength <= 1 ? "Vui lòng nhập nội dung." : $"Vui lòng nhập ít nhất {_minLength} ký tự.";
                return;
            }
            DialogResult = true;
        };
        var cancel = new Button { Content = "Hủy bỏ", IsCancel = true };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        sp.Children.Add(buttons);
        Content = sp;
        Loaded += (_, _) => { if (_pw != null) _pw.Focus(); else { _text!.Focus(); _text.SelectAll(); } };
    }

    public static string? AskText(Window? owner, string title, string prompt, string initial = "", int minLength = 1, int maxLength = 500, bool multiline = false, string okText = "Đồng ý")
    {
        var d = new InputDialog(title, prompt, false, multiline, initial, minLength, maxLength, okText) { Owner = owner };
        return d.ShowDialog() == true ? d.Value.Trim() : null;
    }

    public static string? AskPassword(Window? owner, string title, string prompt, string okText = "Đồng ý")
    {
        var d = new InputDialog(title, prompt, true, false, "", 1, KeyStore.MaxPasswordLength, okText) { Owner = owner };
        return d.ShowDialog() == true ? d.Value : null;
    }
}

public static class RestoreHelper
{
    /// <summary>Chọn tệp sao lưu, hỏi mật khẩu của bản sao lưu, xác nhận và khôi phục.</summary>
    public static bool RunFromFile(Window owner, string? path = null)
    {
        if (path == null)
        {
            var ofd = new OpenFileDialog
            {
                Title = "Chọn tệp sao lưu",
                Filter = "Tệp sao lưu QLVB (*.qlvbak)|*.qlvbak",
                InitialDirectory = Directory.Exists(Ctx.Paths.Backup) ? Ctx.Paths.Backup : null,
                CheckFileExists = true,
            };
            if (ofd.ShowDialog(owner) != true) return false;
            path = ofd.FileName;
        }
        BackupManifest m;
        try { m = BackupService.Inspect(path); }
        catch (Exception ex) { Dlg.Handle(ex); return false; }
        if (!Dlg.Confirm($"Khôi phục dữ liệu từ bản sao lưu tạo lúc {m.CreatedAt:dd/MM/yyyy HH:mm}?\n\n" +
                         "• Toàn bộ dữ liệu hiện tại sẽ được thay bằng dữ liệu trong bản sao lưu.\n" +
                         "• Phần mềm tự sao lưu an toàn dữ liệu hiện tại trước khi khôi phục.\n" +
                         "• Sau khi khôi phục, mật khẩu đăng nhập là mật khẩu đang dùng TẠI THỜI ĐIỂM SAO LƯU.", danger: true))
            return false;
        var pw = InputDialog.AskPassword(owner, "Mật khẩu của bản sao lưu", "Nhập mật khẩu đang dùng tại thời điểm tạo bản sao lưu:", "Khôi phục");
        if (pw == null) return false;
        try
        {
            using (new WaitCursor()) Ctx.Backup.Restore(path, pw);
            Ctx.NotifyChanged();
            Dlg.Info("Khôi phục dữ liệu thành công.");
            return true;
        }
        catch (Exception ex)
        {
            Dlg.Handle(ex, "khôi phục");
            return false;
        }
    }
}

/// <summary>Hộp thoại chọn một trong vài phương án (nút có nhãn tiếng Việt).</summary>
public static class ChoiceDialog
{
    /// <returns>Chỉ số phương án, hoặc -1 nếu hủy.</returns>
    public static int Ask(Window? owner, string title, string prompt, params string[] options)
    {
        var result = -1;
        var w = new Window
        {
            Title = title, Width = 460, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false, Owner = owner,
        };
        var sp = new StackPanel { Margin = new Thickness(20) };
        sp.Children.Add(new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
        var bar = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        for (var i = 0; i < options.Length; i++)
        {
            var idx = i;
            var b = new Button { Content = options[i] };
            if (i == 0) b.Style = (Style)System.Windows.Application.Current.FindResource("PrimaryButton");
            b.Click += (_, _) => { result = idx; w.DialogResult = true; };
            bar.Children.Add(b);
        }
        bar.Children.Add(new Button { Content = "Hủy bỏ", IsCancel = true });
        sp.Children.Add(bar);
        w.Content = sp;
        w.ShowDialog();
        return result;
    }
}
