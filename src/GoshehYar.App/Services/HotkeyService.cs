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
    private const int ModuleIdBase = 0x6100;
    private readonly System.Collections.Generic.Dictionary<int, string> _moduleIds = new();

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

    /// <summary>Raised with the module id when a per-tool hotkey is pressed.</summary>
    public event Action<string>? ModulePressed;

    /// <summary>Registers a hotkey that opens the given tool. Replaces an earlier one for the same tool.</summary>
    public HotkeyResult RegisterModule(string moduleId, HotkeyGesture gesture)
    {
        UnregisterModule(moduleId);

        var id = ModuleIdBase;
        while (_moduleIds.ContainsKey(id)) id++;

        var modifiers = (uint)gesture.Modifiers | NativeMethods.MOD_NOREPEAT;
        if (NativeMethods.RegisterHotKey(_source.Handle, id, modifiers, (uint)gesture.VirtualKey))
        {
            _moduleIds[id] = moduleId;
            return HotkeyResult.Registered;
        }

        var error = Marshal.GetLastWin32Error();
        Logger.Warn($"RegisterHotKey({gesture}) for tool {moduleId} failed with Win32 error {error}.");
        return error == NativeMethods.ERROR_HOTKEY_ALREADY_REGISTERED ? HotkeyResult.Conflict : HotkeyResult.Invalid;
    }

    public void UnregisterModule(string moduleId)
    {
        foreach (var pair in new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int, string>>(_moduleIds))
        {
            if (pair.Value != moduleId) continue;
            NativeMethods.UnregisterHotKey(_source.Handle, pair.Key);
            _moduleIds.Remove(pair.Key);
        }
    }

    public void UnregisterAllModules()
    {
        foreach (var id in new System.Collections.Generic.List<int>(_moduleIds.Keys))
        {
            NativeMethods.UnregisterHotKey(_source.Handle, id);
        }

        _moduleIds.Clear();
    }

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

        if (msg == NativeMethods.WM_HOTKEY && _moduleIds.TryGetValue(wParam.ToInt32(), out var moduleId))
        {
            handled = true;
            try
            {
                ModulePressed?.Invoke(moduleId);
            }
            catch (Exception ex)
            {
                Logger.Error("Tool hotkey handler failed.", ex);
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAllModules();
        Unregister();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
