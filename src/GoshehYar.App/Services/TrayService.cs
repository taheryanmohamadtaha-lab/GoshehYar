using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using GoshehYar.App.Interop;
using GoshehYar.Core.Localization;
using GoshehYar.Core.Settings;
using WinForms = System.Windows.Forms;

namespace GoshehYar.App.Services;

/// <summary>System tray icon with the menu that is always reachable, even when the panel is fully hidden.</summary>
public sealed class TrayService : IDisposable
{
    private readonly AppController _controller;
    private readonly WinForms.NotifyIcon _icon;
    private readonly WinForms.ContextMenuStrip _menu = new();

    public TrayService(AppController controller)
    {
        _controller = controller;

        _icon = new WinForms.NotifyIcon
        {
            Icon = CreateIcon(),
            Visible = true,
            ContextMenuStrip = _menu
        };

        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left) _controller.Panel.Toggle();
        };

        Refresh();
    }

    /// <summary>Rebuilds the menu (call after the language changes).</summary>
    public void Refresh()
    {
        var lang = _controller.Settings.Current.Language;

        var text = Loc.Get("app_name", lang) + (_recording ? " - " + Loc.Get("tray_recording", lang) : "");
        _icon.Text = text.Length > 60 ? text[..60] : text;
        _menu.RightToLeft = lang == AppLanguage.Persian ? WinForms.RightToLeft.Yes : WinForms.RightToLeft.No;
        _menu.Items.Clear();
        _menu.Items.Add(Loc.Get("tray_toggle", lang), null, (_, _) => _controller.Panel.Toggle());
        _menu.Items.Add(Loc.Get("tray_hide_fully", lang), null, (_, _) => _controller.Panel.HideCompletely());
        _menu.Items.Add(new WinForms.ToolStripSeparator());
        _menu.Items.Add(Loc.Get("tray_settings", lang), null, (_, _) => _controller.OpenSettings());
        _menu.Items.Add(Loc.Get("tray_restart", lang), null, (_, _) => _controller.Restart());
        _menu.Items.Add(new WinForms.ToolStripSeparator());
        _menu.Items.Add(Loc.Get("tray_exit", lang), null, (_, _) => _controller.Exit());
    }

    private bool _recording;

    /// <summary>While recording the tray tooltip says so, so a recording is never hidden from the user.</summary>
    public void SetRecording(bool recording)
    {
        _recording = recording;
        Refresh();
    }

    public void ShowBalloon(string text)
    {
        var lang = _controller.Settings.Current.Language;
        _icon.ShowBalloonTip(5000, Loc.Get("app_name", lang), text, WinForms.ToolTipIcon.Info);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }

    /// <summary>Draws the tray icon at runtime so no binary asset is needed yet.</summary>
    private static Icon CreateIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            using var path = RoundedRectangle(new Rectangle(1, 1, 30, 30), 8);
            using var fill = new SolidBrush(Color.FromArgb(255, 0x4C, 0x9A, 0xFF));
            g.FillPath(fill, path);

            using var font = new Font("Segoe UI", 17, FontStyle.Bold, GraphicsUnit.Pixel);
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString("G", font, Brushes.White, new RectangleF(0, 0, 32, 32), format);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(handle);
            return (Icon)temp.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }

    private static GraphicsPath RoundedRectangle(Rectangle r, int radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
