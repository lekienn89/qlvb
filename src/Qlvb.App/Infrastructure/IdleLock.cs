using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Qlvb.App.Views;
using Qlvb.Application;

namespace Qlvb.App.Infrastructure;

/// <summary>
/// Tự khóa màn hình sau N phút không thao tác (cấu hình "Tự khóa sau (phút)", 0 = tắt).
/// Khi khóa, nội dung mọi cửa sổ bị ẩn cho đến khi nhập đúng mật khẩu.
/// </summary>
public sealed class IdleLock : IDisposable
{
    private readonly Window _main;
    private readonly DispatcherTimer _timer;
    private DateTime _last = DateTime.UtcNow;
    private bool _locked;

    public IdleLock(Window main)
    {
        _main = main;
        InputManager.Current.PreProcessInput += OnInput;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _timer.Tick += (_, _) => Check();
        _timer.Start();
    }

    private void OnInput(object sender, PreProcessInputEventArgs e)
    {
        if (e.StagingItem.Input is MouseEventArgs or KeyboardEventArgs or TextCompositionEventArgs or StylusEventArgs or TouchEventArgs)
            _last = DateTime.UtcNow;
    }

    private void Check()
    {
        if (_locked || !Ctx.Session.LoggedIn) return;
        int minutes;
        try { minutes = Ctx.CauHinh.GetInt(ConfigKeys.TuKhoaPhut, ConfigKeys.MacDinhTuKhoaPhut); }
        catch (Exception) { minutes = ConfigKeys.MacDinhTuKhoaPhut; }
        if (minutes > 0 && DateTime.UtcNow - _last >= TimeSpan.FromMinutes(minutes)) Lock();
    }

    public void Lock()
    {
        if (_locked || !Ctx.Session.LoggedIn) return;
        _locked = true;
        try
        {
            Ctx.Session.LockScreen();
            ClipboardGuard.ClearIfOurs();
            var hidden = new List<(Window W, object? Content)>();
            foreach (var w in System.Windows.Application.Current.Windows.OfType<Window>().ToList())
            {
                if (w.Content is UIElement el)
                {
                    hidden.Add((w, null));
                    el.Visibility = Visibility.Hidden;
                }
            }
            var owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? _main;
            var lw = new LockWindow();
            if (owner.IsVisible) lw.Owner = owner;
            var ok = lw.ShowDialog() == true;
            foreach (var (w, _) in hidden)
                if (w.Content is UIElement el) el.Visibility = Visibility.Visible;
            if (!ok) App.Current.Dispatcher.BeginInvoke(() => App.Current.Logout());
        }
        finally
        {
            _locked = false;
            _last = DateTime.UtcNow;
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        InputManager.Current.PreProcessInput -= OnInput;
    }
}
