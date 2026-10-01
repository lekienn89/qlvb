using System.Windows;
using Qlvb.App.Infrastructure;

namespace Qlvb.App.Views;

public partial class LockWindow : Window
{
    private bool _allowClose;

    public LockWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => TxtPassword.Focus();
        // Không cho đóng bằng Alt+F4 khi chưa mở khóa.
        Closing += (_, e) => { if (!_allowClose) e.Cancel = true; };
    }

    private void BtnUnlock_Click(object sender, RoutedEventArgs e)
    {
        bool ok;
        try
        {
            using (new WaitCursor()) ok = Ctx.Session.Unlock(TxtPassword.Password);
        }
        catch (Exception ex)
        {
            Dlg.Handle(ex, "mở khóa");
            return;
        }
        TxtPassword.Clear();
        if (!ok)
        {
            TxtError.Text = "Mật khẩu không đúng.";
            TxtPassword.Focus();
            return;
        }
        _allowClose = true;
        DialogResult = true;
    }

    private void BtnLogout_Click(object sender, RoutedEventArgs e)
    {
        _allowClose = true;
        DialogResult = false;
    }
}
