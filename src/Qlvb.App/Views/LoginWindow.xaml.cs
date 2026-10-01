using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Qlvb.App.Infrastructure;
using Qlvb.Infrastructure.Data;
using Qlvb.Infrastructure.Security;
using Qlvb.Infrastructure.Services;

namespace Qlvb.App.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => ShowState();
        PreviewKeyDown += (_, _) => UpdateCaps();
    }

    private void ShowState()
    {
        foreach (var p in new UIElement[] { PanelLogin, PanelSetup, PanelRecovery, PanelMissing }) p.Visibility = Visibility.Collapsed;
        switch (Ctx.Session.TrangThai)
        {
            case TrangThaiDuLieu.ChuaKhoiTao:
                PanelSetup.Visibility = Visibility.Visible;
                TxtTenCoQuan.Focus();
                break;
            case TrangThaiDuLieu.SanSang:
                PanelLogin.Visibility = Visibility.Visible;
                TxtPassword.Focus();
                break;
            default:
                ShowMissing(File.Exists(Ctx.Paths.DatabaseFile)
                    ? "Không tìm thấy tệp khóa dữ liệu (qlvb.key) trong thư mục Database. Không có tệp này thì không giải mã được dữ liệu."
                    : "Không tìm thấy tệp dữ liệu (qlvb.db) trong thư mục Database.");
                break;
        }
    }

    private void ShowMissing(string msg)
    {
        foreach (var p in new UIElement[] { PanelLogin, PanelSetup, PanelRecovery }) p.Visibility = Visibility.Collapsed;
        PanelMissing.Visibility = Visibility.Visible;
        TxtMissing.Text = msg + "\n\nHãy khôi phục từ bản sao lưu gần nhất (tệp .qlvbak). Thư mục dữ liệu: " + Ctx.Paths.Root;
    }

    private void UpdateCaps() => TxtCaps.Visibility = Keyboard.IsKeyToggled(Key.CapsLock) ? Visibility.Visible : Visibility.Collapsed;

    private void TxtPassword_KeyDown(object sender, KeyEventArgs e) => UpdateCaps();

    private void BtnLogin_Click(object sender, RoutedEventArgs e)
    {
        TxtLoginError.Text = "";
        if (TxtPassword.Password.Length == 0)
        {
            TxtLoginError.Text = "Vui lòng nhập mật khẩu.";
            TxtPassword.Focus();
            return;
        }
        try
        {
            using (new WaitCursor())
                Ctx.Session.Login(TxtPassword.Password, (from, to) => { });
            if (Ctx.Session.CanhBaoDongHo is { } canhBao)
            {
                Ctx.Log.Warning("Đồng hồ máy sớm hơn thao tác cuối trong nhật ký");
                Dlg.Warn(canhBao);
            }
            DialogResult = true;
        }
        catch (AuthException ex)
        {
            TxtLoginError.Text = ex.Message;
            TxtPassword.Clear();
            TxtPassword.Focus();
        }
        catch (DatabaseOpenException ex)
        {
            Ctx.Log.Error("Không mở được CSDL: {Err}", Logging.Describe(ex));
            ShowMissing(ex.Message);
        }
        catch (InvalidDataException ex)
        {
            Ctx.Log.Error("Tệp khóa hỏng: {Err}", Logging.Describe(ex));
            ShowMissing(ex.Message);
        }
        catch (Exception ex)
        {
            Dlg.Handle(ex, "đăng nhập");
        }
    }

    private void BtnSetup_Click(object sender, RoutedEventArgs e)
    {
        TxtSetupError.Text = "";
        if (TxtNewPw1.Password != TxtNewPw2.Password)
        {
            TxtSetupError.Text = "Hai lần nhập mật khẩu không khớp.";
            TxtNewPw2.Focus();
            return;
        }
        try
        {
            KeyStore.CheckPasswordPolicy(TxtNewPw1.Password);
            string code;
            using (new WaitCursor())
                code = Ctx.Session.Setup(TxtNewPw1.Password, TxtTenCoQuan.Text);
            new RecoveryCodeWindow(code) { Owner = this }.ShowDialog();
            DialogResult = true;
        }
        catch (AuthException ex)
        {
            TxtSetupError.Text = ex.Message;
            TxtNewPw1.Focus();
        }
        catch (Exception ex)
        {
            Dlg.Handle(ex, "thiết lập");
        }
    }

    private void LnkForgot_Click(object sender, RoutedEventArgs e)
    {
        PanelLogin.Visibility = Visibility.Collapsed;
        PanelRecovery.Visibility = Visibility.Visible;
        TxtRecoveryCode.Focus();
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e) => ShowState();

    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        TxtRecError.Text = "";
        if (TxtRecPw1.Password != TxtRecPw2.Password)
        {
            TxtRecError.Text = "Hai lần nhập mật khẩu không khớp.";
            return;
        }
        try
        {
            string code;
            using (new WaitCursor())
                code = Ctx.Session.ResetPassword(TxtRecoveryCode.Text, TxtRecPw1.Password);
            Dlg.Info("Đã đặt lại mật khẩu. Mã khôi phục cũ đã hết hiệu lực; hãy ghi lại mã khôi phục MỚI ở màn hình tiếp theo.");
            new RecoveryCodeWindow(code) { Owner = this }.ShowDialog();
            DialogResult = true;
        }
        catch (AuthException ex)
        {
            TxtRecError.Text = ex.Message;
        }
        catch (DatabaseOpenException ex)
        {
            ShowMissing(ex.Message);
        }
        catch (Exception ex)
        {
            Dlg.Handle(ex, "đặt lại mật khẩu");
        }
    }

    private void LnkRestore_Click(object sender, RoutedEventArgs e)
    {
        if (RestoreHelper.RunFromFile(this)) DialogResult = true;
    }

    private void BtnExit_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
