using System;
using System.Windows.Interop;
using GoshehYar.App.Interop;

namespace GoshehYar.App.Services;

/// <summary>
/// Keyboard media keys (play/pause, next, previous, stop). The keys are only taken while the music player has a track,
/// so other players keep working with their keys the rest of the time.
/// </summary>
public sealed class MediaKeyService : IDisposable
{
    private const int IdPlayPause = 0x6101;
    private const int IdNext = 0x6102;
    private const int IdPrevious = 0x6103;
    private const int IdStop = 0x6104;

    private const uint VkNext = 0xB0;
    private const uint VkPrevious = 0xB1;
    private const uint VkStop = 0xB2;
    private const uint VkPlayPause = 0xB3;

    private readonly HwndSource _source;
    private readonly MusicPlayerService _music;
    private bool _active;

    public MediaKeyService(MusicPlayerService music)
    {
        _music = music;

        var parameters = new HwndSourceParameters("GoshehYar.MediaKeyWindow")
        {
            Width = 0,
            Height = 0,
            ParentWindow = NativeMethods.HWND_MESSAGE,
            WindowStyle = 0
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
        _music.Changed += (_, _) => SetActive(_music.HasTrack);
    }

    private void SetActive(bool active)
    {
        if (_active == active) return;
        _active = active;

        if (active)
        {
            NativeMethods.RegisterHotKey(_source.Handle, IdPlayPause, NativeMethods.MOD_NOREPEAT, VkPlayPause);
            NativeMethods.RegisterHotKey(_source.Handle, IdNext, NativeMethods.MOD_NOREPEAT, VkNext);
            NativeMethods.RegisterHotKey(_source.Handle, IdPrevious, NativeMethods.MOD_NOREPEAT, VkPrevious);
            NativeMethods.RegisterHotKey(_source.Handle, IdStop, NativeMethods.MOD_NOREPEAT, VkStop);
        }
        else
        {
            UnregisterAll();
        }
    }

    private void UnregisterAll()
    {
        NativeMethods.UnregisterHotKey(_source.Handle, IdPlayPause);
        NativeMethods.UnregisterHotKey(_source.Handle, IdNext);
        NativeMethods.UnregisterHotKey(_source.Handle, IdPrevious);
        NativeMethods.UnregisterHotKey(_source.Handle, IdStop);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != NativeMethods.WM_HOTKEY) return IntPtr.Zero;

        try
        {
            switch (wParam.ToInt32())
            {
                case IdPlayPause: _music.TogglePlayPause(); handled = true; break;
                case IdNext: _music.Next(); handled = true; break;
                case IdPrevious: _music.Previous(); handled = true; break;
                case IdStop: _music.Stop(); handled = true; break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Media key handler failed.", ex);
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_active) UnregisterAll();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
