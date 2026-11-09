using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using GoshehYar.Core.Input;

namespace GoshehYar.App.ViewModels;

public sealed class ShortcutRow : ViewModelBase
{
    private string _gesture = "";
    private string _message = "";

    public ShortcutRow(string moduleId, string title)
    {
        ModuleId = moduleId;
        Title = title;
    }

    public string ModuleId { get; }
    public string Title { get; set; }

    /// <summary>Gesture text such as "Ctrl+Alt+N", empty when none.</summary>
    public string Gesture
    {
        get => _gesture;
        set { _gesture = value; OnPropertyChanged(); OnPropertyChanged(nameof(Display)); OnPropertyChanged(nameof(HasGesture)); }
    }

    public string Display => _gesture.Length == 0 ? NoneText : _gesture;
    public string NoneText { get; set; } = "";
    public bool HasGesture => _gesture.Length > 0;

    public string Message
    {
        get => _message;
        set { _message = value; OnPropertyChanged(); }
    }
}

/// <summary>Lets the user give each tool its own global shortcut that opens it directly.</summary>
public sealed class ShortcutsViewModel : LocalizedViewModel
{
    public ShortcutsViewModel(AppController controller) : base(controller)
    {
        ClearCommand = new RelayCommand(p => { if (p is ShortcutRow r) Clear(r); });
        Build();
    }

    public ICommand ClearCommand { get; }

    public string ClearLabel => this["sc_clear"];

    public ObservableCollection<ShortcutRow> Rows { get; } = new();

    private void Build()
    {
        Rows.Clear();
        foreach (var m in Controller.Modules.All.OrderBy(m => m.DefaultOrder))
        {
            var row = new ShortcutRow(m.Id, this[m.TitleKey]) { NoneText = this["sc_none"] };
            if (S.ModuleHotkeys.TryGetValue(m.Id, out var g)) row.Gesture = g;
            Rows.Add(row);
        }
    }

    /// <summary>Called by the window when the user presses keys in a row's box.</summary>
    public void Capture(ShortcutRow row, HotkeyModifiers modifiers, int virtualKey)
    {
        if (modifiers == HotkeyModifiers.None)
        {
            row.Message = this["sc_need_modifier"];
            return;
        }

        if (!HotkeyGesture.IsSupportedKey(virtualKey))
        {
            row.Message = this["sc_unsupported"];
            return;
        }

        var gesture = new HotkeyGesture(modifiers, virtualKey);
        var text = gesture.ToString();

        if (S.HotkeyEnabled && HotkeyGesture.TryParse(S.Hotkey, out var panel) && panel.Equals(gesture))
        {
            row.Message = this["sc_same_as_panel"];
            return;
        }

        if (Rows.Any(r => r != row && r.Gesture == text))
        {
            row.Message = this["sc_duplicate"];
            return;
        }

        var previous = S.ModuleHotkeys.TryGetValue(row.ModuleId, out var old) ? old : null;
        SetStored(row.ModuleId, text);
        var failed = Controller.SyncModuleHotkeys();
        if (failed.Contains(row.ModuleId))
        {
            SetStored(row.ModuleId, previous);
            Controller.SyncModuleHotkeys();
            row.Message = this["sc_conflict"];
            return;
        }

        row.Gesture = text;
        row.Message = this["sc_saved"];
    }

    private void Clear(ShortcutRow row)
    {
        SetStored(row.ModuleId, null);
        Controller.SyncModuleHotkeys();
        row.Gesture = "";
        row.Message = this["sc_saved"];
    }

    private void SetStored(string moduleId, string? gesture)
    {
        Controller.Settings.Update(s =>
        {
            if (gesture is null) s.ModuleHotkeys.Remove(moduleId);
            else s.ModuleHotkeys[moduleId] = gesture;
        });
    }

    protected override void OnSettingsChanged(object? sender, EventArgs e)
    {
        // Titles may change with the language; keep the gestures the user is looking at.
        foreach (var row in Rows)
        {
            var module = Controller.Modules.All.FirstOrDefault(m => m.Id == row.ModuleId);
            if (module is null) continue;
            row.Title = this[module.TitleKey];
            row.NoneText = this["sc_none"];
            row.Gesture = S.ModuleHotkeys.TryGetValue(row.ModuleId, out var g) ? g : "";
        }

        OnPropertyChanged(nameof(Rows));
        base.OnSettingsChanged(sender, e);
    }
}
