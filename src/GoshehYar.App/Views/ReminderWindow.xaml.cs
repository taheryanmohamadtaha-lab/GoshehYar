using System;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using GoshehYar.App.Services;
using GoshehYar.Core.Localization;
using GoshehYar.Core.Reminders;
using GoshehYar.Core.Settings;

namespace GoshehYar.App.Views;

/// <summary>
/// Pops up on top of everything when a reminder (note, event, task) comes due or a timer ends.
/// Snooze and "open" buttons appear only when the item supports them.
/// </summary>
public partial class ReminderWindow : Window
{
    private static readonly int[] SnoozeMinutes = { 5, 10, 30, 60, 180 };

    private readonly ReminderItem _item;
    private readonly IReminderSource? _source;

    public ReminderWindow(AppController controller, ReminderItem item, IReminderSource? source, bool playSound = true)
    {
        _item = item;
        _source = source;

        InitializeComponent();

        var s = controller.Settings.Current;
        string T(string key) => Loc.Get(key, s.Language);
        string N(string text) => DigitFormatter.Format(text, s.Digits);

        FlowDirection = s.Language == AppLanguage.Persian ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        var header = item.Header ?? T("reminder_title");
        Title = header;
        HeaderText.Text = header;
        TitleText.Text = string.IsNullOrWhiteSpace(item.Title) ? T("note_untitled") : item.Title.Trim();

        BodyText.Text = item.Body ?? "";
        BodyText.Visibility = string.IsNullOrWhiteSpace(item.Body) ? Visibility.Collapsed : Visibility.Visible;

        if (item.DueUtc is { } utc)
        {
            TimeText.Text = N(string.Format(T("reminder_at"), utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm")));
        }
        else
        {
            TimeText.Visibility = Visibility.Collapsed;
        }

        if (item.CanSnooze && source is not null)
        {
            SnoozeLabel.Text = T("reminder_snooze_label");
            foreach (var minutes in SnoozeMinutes)
            {
                var text = minutes >= 60
                    ? string.Format(T("reminder_snooze_hours"), minutes / 60)
                    : string.Format(T("reminder_snooze_minutes"), minutes);
                var button = new Button { Content = N(text), Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(10, 5, 10, 5) };
                var m = minutes;
                button.Click += (_, _) =>
                {
                    _source?.Snooze(_item.Id, m);
                    Close();
                };
                SnoozePanel.Children.Add(button);
            }
        }
        else
        {
            SnoozeLabel.Visibility = Visibility.Collapsed;
            SnoozePanel.Visibility = Visibility.Collapsed;
        }

        OpenButton.Content = T("reminder_open");
        OpenButton.Visibility = source is null ? Visibility.Collapsed : Visibility.Visible;
        DismissButton.Content = T("reminder_dismiss");

        // Bottom-right corner of the work area, so it is seen but does not cover what the user is typing.
        Loaded += (_, _) =>
        {
            PositionAfterLayout();
            if (playSound)
            {
                try { SystemSounds.Exclamation.Play(); } catch (Exception) { }
            }
        };
    }

    private void PositionAfterLayout()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - 16;
        Top = Math.Max(area.Top + 8, area.Bottom - ActualHeight - 16);
    }

    private void OnOpen(object sender, RoutedEventArgs e)
    {
        _source?.Open(_item.Id);
        Close();
    }

    private void OnDismiss(object sender, RoutedEventArgs e) => Close();
}
