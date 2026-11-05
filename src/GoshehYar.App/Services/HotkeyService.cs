using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using GoshehYar.App.Interop;
using GoshehYar.Core.Input;

namespace GoshehYar.App.Services;

public enum HotkeyResult
{
    Registered,
    Disabled,
    Invalid,
    Conflict
}

/// <summary>Registers one system-wide hotkey through a hidden message-only window.</summary>
public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0x6001;

    private readonly HwndSource _source;
    private bool _registered;

    public HotkeyService()
    {
        var parameters = new HwndSourceParameters("GoshehYar.HotkeyWindow")
        {
            Width = 0,
            Height = 0,
            ParentWindow = NativeMethods.HWND_MESSAGE,
            WindowStyle = 0
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    public event Action? Pressed;

    public HotkeyResult Register(HotkeyGesture gesture)
    {
        Unregister();

        var modifiers = (uint)gesture.Modifiers | NativeMethods.MOD_NOREPEAT;
        if (NativeMethods.RegisterHotKey(_source.Handle, HotkeyId, modifiers, (uint)gesture.VirtualKey))
        {
            _registered = true;
            return HotkeyResult.Registered;
        }

        var error = Marshal.GetLastWin32Error();
        Logger.Warn($"RegisterHotKey({gesture}) failed with Win32 error {error}.");
        return error == NativeMethods.ERROR_HOTKEY_ALREADY_REGISTERED ? HotkeyResult.Conflict : HotkeyResult.Invalid;
    }

    public void Unregister()
    {
        if (!_registered) return;
        NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
        _registered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            try
            {
                Pressed?.Invoke();
            }
            catch (Exception ex)
            {
                Logger.Error("Hotkey handler failed.", ex);
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
