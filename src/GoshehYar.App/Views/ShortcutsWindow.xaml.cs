using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GoshehYar.App.ViewModels;
using GoshehYar.Core.Input;

namespace GoshehYar.App.Views;

public partial class ShortcutsWindow : Window
{
    private readonly ShortcutsViewModel _viewModel;

    public ShortcutsWindow(ShortcutsViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    private void ShortcutBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        if (sender is not TextBox { DataContext: ShortcutRow row }) return;

        // With Alt held, WPF reports the real key in SystemKey.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin
            or Key.System or Key.None or Key.DeadCharProcessed)
        {
            return; // wait for the real key
        }

        var modifiers = HotkeyModifiers.None;
        var m = Keyboard.Modifiers;
        if (m.HasFlag(ModifierKeys.Control)) modifiers |= HotkeyModifiers.Ctrl;
        if (m.HasFlag(ModifierKeys.Alt)) modifiers |= HotkeyModifiers.Alt;
        if (m.HasFlag(ModifierKeys.Shift)) modifiers |= HotkeyModifiers.Shift;
        if (m.HasFlag(ModifierKeys.Windows)) modifiers |= HotkeyModifiers.Win;

        _viewModel.Capture(row, modifiers, KeyInterop.VirtualKeyFromKey(key));
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _viewModel.Dispose();
    }
}
