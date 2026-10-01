using System.Windows;

namespace Qlvb.App.Views;

public partial class RecoveryCodeWindow : Window
{
    private readonly string _code;
    private bool _confirmed;

    public RecoveryCodeWindow(string code)
    {
        InitializeComponent();
        _code = code;
        TxtCode.Text = code;
        Closing += (_, e) => { if (!_confirmed) e.Cancel = true; };
        Loaded += (_, _) => TxtConfirm.Focus();
    }

    private void TxtConfirm_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) =>
        BtnOk.IsEnabled = TxtConfirm.Text.Trim() == _code[^5..];

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        _confirmed = true;
        TxtCode.Clear();
        DialogResult = true;
    }
}
