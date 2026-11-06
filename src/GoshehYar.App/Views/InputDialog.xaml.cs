using System.Windows;
using GoshehYar.Core.Localization;
using GoshehYar.Core.Settings;

namespace GoshehYar.App.Views;

/// <summary>Tiny "type a text" dialog (playlist names and the like).</summary>
public partial class InputDialog : Window
{
    public InputDialog(string title, string prompt, string initial, AppLanguage language)
    {
        InitializeComponent();
        Title = title;
        Prompt.Text = prompt;
        Input.Text = initial;
        OkButton.Content = Loc.Get("btn_ok", language);
        CancelButton.Content = Loc.Get("btn_cancel", language);
        FlowDirection = language == AppLanguage.Persian ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        Loaded += (_, _) =>
        {
            Input.Focus();
            Input.SelectAll();
        };
    }

    public string Value => Input.Text.Trim();

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;

    /// <summary>Returns the text, or null if cancelled or empty.</summary>
    public static string? Ask(Window? owner, string title, string prompt, string initial, AppLanguage language)
    {
        var dialog = new InputDialog(title, prompt, initial, language);
        if (owner is not null && owner.IsVisible) dialog.Owner = owner;
        return dialog.ShowDialog() == true && dialog.Value.Length > 0 ? dialog.Value : null;
    }
}
