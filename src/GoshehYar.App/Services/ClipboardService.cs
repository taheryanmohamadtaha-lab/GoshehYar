using System;
using System.Threading;
using System.Windows.Interop;
using GoshehYar.App.Interop;
using GoshehYar.Core.Clipboard;
using GoshehYar.Core.Settings;

namespace GoshehYar.App.Services;

/// <summary>
/// Records copied text while the user has turned the history on. Uses the clipboard change notification
/// (no polling). Text marked as private by password managers is skipped.
/// </summary>
public sealed class ClipboardService : IDisposable
{
    private readonly AppController _controller;
    private readonly HwndSource _source;
    private ClipboardStore? _store;
    private bool _listening;
    private string? _ignoreText;

    public ClipboardService(AppController controller)
    {
        _controller = controller;
        var parameters = new HwndSourceParameters("GoshehYar.ClipboardWindow")
        {
            Width = 0,
            Height = 0,
            ParentWindow = NativeMethods.HWND_MESSAGE,
            WindowStyle = 0
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    /// <summary>Raised on the UI thread after the history changed.</summary>
    public event Action? Changed;

    public bool IsListening => _listening;

    /// <summary>The history database (opened on first use). Null when it cannot be opened.</summary>
    public ClipboardStore? Store
    {
        get
        {
            if (_store is not null) return _store;
            try
            {
                _store = new ClipboardStore(AppPaths.ClipboardDatabasePath);
            }
            catch (Exception ex)
            {
                Logger.Error("The clipboard database could not be opened.", ex);
            }

            return _store;
        }
    }

    /// <summary>Starts or stops listening so it matches the setting.</summary>
    public void Sync()
    {
        var wanted = _controller.Settings.Current.ClipboardEnabled;
        if (wanted == _listening) return;

        if (wanted)
        {
            if (NativeMethods.AddClipboardFormatListener(_source.Handle)) _listening = true;
            else Logger.Warn("AddClipboardFormatListener failed.");
        }
        else
        {
            NativeMethods.RemoveClipboardFormatListener(_source.Handle);
            _listening = false;
        }
    }

    /// <summary>Copies a text from the history back to the clipboard without recording it as a new copy.</summary>
    public bool SetText(string text)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                _ignoreText = text;
                System.Windows.Clipboard.SetText(text);
                return true;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                Thread.Sleep(30);
            }
        }

        _ignoreText = null;
        return false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_CLIPBOARDUPDATE)
        {
            handled = true;
            try
            {
                OnClipboardChanged();
            }
            catch (Exception ex)
            {
                Logger.Error("Recording the clipboard failed.", ex);
            }
        }

        return IntPtr.Zero;
    }

    private void OnClipboardChanged()
    {
        if (!_listening) return;

        var text = TryReadText();
        if (text is null) return;

        if (_ignoreText is not null)
        {
            var same = text == _ignoreText;
            _ignoreText = null;
            if (same) return;
        }

        var store = Store;
        if (store is null) return;
        if (store.Add(text, _controller.Settings.Current.ClipboardMaxItems)) Changed?.Invoke();
    }

    private static string? TryReadText()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                var data = System.Windows.Clipboard.GetDataObject();
                if (data is null) return null;

                // Password managers set these formats so their secrets are not recorded.
                if (data.GetDataPresent("ExcludeClipboardContentFromMonitorProcessing")) return null;
                if (data.GetDataPresent("CanIncludeInClipboardHistory") && data.GetData("CanIncludeInClipboardHistory") is byte[] flag
                    && flag.Length > 0 && flag[0] == 0) return null;

                return data.GetDataPresent(System.Windows.DataFormats.UnicodeText)
                    ? data.GetData(System.Windows.DataFormats.UnicodeText) as string
                    : null;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                Thread.Sleep(30); // another program holds the clipboard
            }
        }

        return null;
    }

    public void Dispose()
    {
        if (_listening)
        {
            NativeMethods.RemoveClipboardFormatListener(_source.Handle);
            _listening = false;
        }

        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
