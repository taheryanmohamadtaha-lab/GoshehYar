using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using GoshehYar.App.Interop;
using GoshehYar.App.Services;

namespace GoshehYar.App.Views;

/// <summary>Shows the Windows number of every monitor for a moment, like Settings > Display > Identify.</summary>
public static class MonitorIdentifier
{
    public static void Show(IReadOnlyList<MonitorInfo> monitors)
    {
        var index = 1;
        foreach (var monitor in monitors)
        {
            var number = ScreenService.DisplayNumber(monitor.DeviceName, index++);
            var scale = monitor.Scale <= 0 ? 1.0 : monitor.Scale;
            var size = (int)Math.Round(170 * scale);
            var x = (int)Math.Round(monitor.Bounds.X) + (int)Math.Round(40 * scale);
            var y = (int)Math.Round(monitor.Bounds.Y) + (int)Math.Round(40 * scale);

            var window = new Window
            {
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                ShowActivated = false,
                Topmost = true,
                AllowsTransparency = true,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(225, 0, 0, 0)),
                Width = 170,
                Height = 170,
                Content = new TextBlock
                {
                    Text = number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    FontSize = 110,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            void Place()
            {
                var handle = new WindowInteropHelper(window).Handle;
                if (handle != IntPtr.Zero)
                {
                    NativeMethods.SetWindowPos(handle, new IntPtr(-1), x, y, size, size, NativeMethods.SWP_NOACTIVATE);
                }
            }

            window.SourceInitialized += (_, _) => Place();
            window.Loaded += (_, _) => Place();
            window.DpiChanged += (_, _) => window.Dispatcher.BeginInvoke(new Action(Place), DispatcherPriority.Background);
            window.Show();

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                window.Close();
            };
            timer.Start();
        }
    }
}
