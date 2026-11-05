using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using GoshehYar.App.Services;
using GoshehYar.Core.Localization;
using GoshehYar.Core.Settings;

namespace GoshehYar.App;

public partial class GoshehYarApplication : Application
{
    private SingleInstance? _singleInstance;
    private AppController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Logger.Error("Unhandled exception (terminating: " + args.IsTerminating + ")", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Logger.Error("Unobserved task exception.", args.Exception);
            args.SetObserved();
        };

        var restarted = e.Args.Contains("--restarted", StringComparer.OrdinalIgnoreCase);
        var autostart = e.Args.Contains(AutostartService.AutostartArgument, StringComparer.OrdinalIgnoreCase);

        _singleInstance = new SingleInstance();
        var wait = restarted ? TimeSpan.FromSeconds(8) : TimeSpan.Zero;
        if (!_singleInstance.TryAcquire(wait))
        {
            // Another copy is already running: ask it to show its panel, then quit quietly.
            SingleInstance.SignalExisting();
            Shutdown();
            return;
        }

        try
        {
            _controller = new AppController(this, _singleInstance, autostart);
            _controller.Start();
        }
        catch (Exception ex)
        {
            Logger.Error("Startup failed.", ex);
            MessageBox.Show(
                Loc.Get("err_unexpected", AppLanguage.Persian) + Environment.NewLine + ex.Message,
                "GoshehYar",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // One failing widget must not take the whole program down.
        Logger.Error("Unhandled UI exception.", e.Exception);
        e.Handled = true;
    }
}
