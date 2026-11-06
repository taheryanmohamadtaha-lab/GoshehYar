using System;
using System.Windows;
using GoshehYar.Core.Localization;
using GoshehYar.Core.Settings;

namespace GoshehYar.App.ViewModels;

/// <summary>Base for tool windows: localized text by key (<c>{Binding [key]}</c>), RTL flow and live language refresh.</summary>
public abstract class LocalizedViewModel : ViewModelBase, IDisposable
{
    private bool _disposed;

    protected LocalizedViewModel(AppController controller)
    {
        Controller = controller;
        controller.Settings.Changed += OnSettingsChanged;
    }

    protected AppController Controller { get; }

    protected AppSettings S => Controller.Settings.Current;

    public string this[string key] => Loc.Get(key, S.Language);

    public FlowDirection FlowDirection =>
        S.Language == AppLanguage.Persian ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    /// <summary>Applies the user's digit style (Persian / English) to a text.</summary>
    protected string Num(string text) => DigitFormatter.Format(text, S.Digits);

    /// <summary>Localized format string with arguments, digits converted.</summary>
    protected string Fmt(string key, params object[] args)
    {
        // Numbers get the user's digit style; text arguments (file names, paths) stay exactly as they are.
        var converted = new object[args.Length];
        for (var i = 0; i < args.Length; i++)
        {
            converted[i] = args[i] is string ? args[i] : Num(Convert.ToString(args[i], System.Globalization.CultureInfo.InvariantCulture) ?? "");
        }

        return string.Format(this[key], converted);
    }

    protected virtual void OnSettingsChanged(object? sender, EventArgs e) => OnPropertyChanged(string.Empty);

    public virtual void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Controller.Settings.Changed -= OnSettingsChanged;
    }
}

public sealed record OptionItem<T>(T Value, string Label);
