using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Qlvb.Application;

namespace Qlvb.App.Views;

/// <summary>Xác nhận các thay đổi (giá trị cũ → mới) trước khi lưu bản sửa.</summary>
public sealed class ConfirmChangesWindow : Window
{
    private ConfirmChangesWindow(IReadOnlyList<ThayDoiTruong> changes)
    {
        Title = "Xác nhận thay đổi";
        Width = 720;
        Height = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        var dock = new DockPanel { Margin = new Thickness(16) };
        var head = new TextBlock { Text = $"Bạn sắp thay đổi {changes.Count} thông tin. Kiểm tra lại trước khi lưu (thay đổi được ghi vào nhật ký):", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(head, Dock.Top);
        dock.Children.Add(head);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        var ok = new Button { Content = "Lưu thay đổi", IsDefault = true, Style = (Style)FindResource("PrimaryButton") };
        ok.Click += (_, _) => DialogResult = true;
        buttons.Children.Add(ok);
        buttons.Children.Add(new Button { Content = "Quay lại sửa", IsCancel = true });
        DockPanel.SetDock(buttons, Dock.Bottom);
        dock.Children.Add(buttons);
        var grid = new DataGrid { ItemsSource = changes };
        var wrap = (Style)FindResource("WrapCell");
        grid.Columns.Add(new DataGridTextColumn { Header = "Thông tin", Binding = new Binding(nameof(ThayDoiTruong.Truong)), Width = new DataGridLength(160) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Giá trị cũ", Binding = new Binding(nameof(ThayDoiTruong.Cu)), Width = new DataGridLength(1, DataGridLengthUnitType.Star), ElementStyle = wrap });
        grid.Columns.Add(new DataGridTextColumn { Header = "Giá trị mới", Binding = new Binding(nameof(ThayDoiTruong.Moi)), Width = new DataGridLength(1, DataGridLengthUnitType.Star), ElementStyle = wrap });
        dock.Children.Add(grid);
        Content = dock;
    }

    public static bool Ask(Window owner, IReadOnlyList<ThayDoiTruong> changes) =>
        new ConfirmChangesWindow(changes) { Owner = owner }.ShowDialog() == true;
}
