using System;
using System.Linq;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using GoshehYar.Core.Localization;
using GoshehYar.Core.Notes;
using GoshehYar.Core.Settings;

namespace GoshehYar.App.Views;

/// <summary>Pops up on top of everything when a note's reminder time comes. Offers snooze, open and dismiss.</summary>
public partial class ReminderWindow : Window
{
    private static readonly int[] SnoozeMinutes = { 5, 10, 30, 60, 180 };

    private readonly AppController _controller;
    private readonly string _noteId;

    public ReminderWindow(AppController controller, Note note)
    {
        _controller = controller;
        _noteId = note.Id;

        InitializeComponent();

        var s = controller.Settings.Current;
        string T(string key) => Loc.Get(key, s.Language);
        string N(string text) => DigitFormatter.Format(text, s.Digits);

        FlowDirection = s.Language == AppLanguage.Persian ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        Title = T("reminder_title");
        HeaderText.Text = T("reminder_title");
        TitleText.Text = string.IsNullOrWhiteSpace(note.Title) ? T("note_untitled") : note.Title.Trim();

        string body;
        if (note.IsChecklist)
        {
            var open = note.Items.Where(i => !i.Done).Select(i => "☐ " + i.Text).Take(6);
            body = string.Join(Environment.NewLine, open);
        }
        else
        {
            body = note.Body.Trim();
            if (body.Length > 240) body = body[..240] + "…";
        }

        BodyText.Text = body;
        BodyText.Visibility = body.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

        if (note.ReminderUtc is { } utc)
        {
            TimeText.Text = N(string.Format(T("reminder_at"), utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm")));
        }

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
                _controller.SnoozeNote(_noteId, m);
                Close();
            };
            SnoozePanel.Children.Add(button);
        }

        OpenButton.Content = T("reminder_open");
        DismissButton.Content = T("reminder_dismiss");

        // Bottom-right corner of the work area, so it is seen but does not cover what the user is typing.
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - 16;
        Top = area.Bottom - 320;
        SourceInitialized += (_, _) => PositionAfterLayout();
        Loaded += (_, _) =>
        {
            PositionAfterLayout();
            try { SystemSounds.Exclamation.Play(); } catch (Exception) { }
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
        _controller.OpenNote(_noteId);
        Close();
    }

    private void OnDismiss(object sender, RoutedEventArgs e) => Close();
}
