using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using GoshehYar.App.Services;
using GoshehYar.Core.Clipboard;

namespace GoshehYar.App.ViewModels;

public sealed class ClipRow
{
    public ClipRow(ClipEntry entry, string chars, string time)
    {
        Entry = entry;
        Id = entry.Id;
        Pinned = entry.Pinned;
        Full = entry.Text;
        var oneLine = entry.Text.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();
        Preview = oneLine.Length > 160 ? oneLine[..160] + "…" : oneLine;
        Meta = time + " · " + chars;
    }

    public ClipEntry Entry { get; }
    public long Id { get; }
    public bool Pinned { get; }
    public string Full { get; }
    public string Preview { get; }
    public string Meta { get; }
}

public sealed class ClipboardViewModel : LocalizedViewModel
{
    private readonly ClipboardService _service;
    private string _search = "";
    private string _status = "";

    public ClipboardViewModel(AppController controller) : base(controller)
    {
        _service = controller.ClipboardHistory;
        CopyCommand = new RelayCommand(p => { if (p is ClipRow r) CopyBack(r); });
        PinCommand = new RelayCommand(p => { if (p is ClipRow r) Run(() => _service.Store?.SetPinned(r.Id, !r.Pinned)); });
        DeleteCommand = new RelayCommand(p => { if (p is ClipRow r) Run(() => _service.Store?.Delete(r.Id)); });
        ClearCommand = new RelayCommand(() => ClearWith(keepPinned: true));
        ClearAllCommand = new RelayCommand(() => ClearWith(keepPinned: false));
        _service.Changed += Reload;
        Reload();
    }

    public ICommand CopyCommand { get; }
    public ICommand PinCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand ClearAllCommand { get; }

    public ObservableCollection<ClipRow> Rows { get; } = new();

    public bool Enabled
    {
        get => S.ClipboardEnabled;
        set
        {
            if (S.ClipboardEnabled == value) return;
            Controller.Settings.Update(s => s.ClipboardEnabled = value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsOff));
            Reload();
        }
    }

    public string CopyLabel => this["clip_copy"];
    public string PinLabel => this["clip_pin"] + " / " + this["clip_unpin"];
    public string DeleteLabel => this["clip_delete"];

    public bool IsOff => !S.ClipboardEnabled;

    public string SearchText
    {
        get => _search;
        set { _search = value ?? ""; OnPropertyChanged(); Reload(); }
    }

    public string Status
    {
        get => _status;
        private set { _status = value; OnPropertyChanged(); }
    }

    public string CountText => Fmt("clip_count", Rows.Count);
    public bool IsEmpty => !IsOff && Rows.Count == 0;

    private void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Logger.Error("Clipboard history operation failed.", ex);
            Status = this["clip_db_error"];
        }

        Reload();
    }

    private void CopyBack(ClipRow row)
    {
        Status = _service.SetText(row.Full) ? this["clip_copied"] : this["calc_copy_failed"];
    }

    private void ClearWith(bool keepPinned)
    {
        var key = keepPinned ? "clip_confirm_clear" : "clip_confirm_clear_all";
        if (MessageBox.Show(this[key], this["mod_clipboard"], MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        Run(() => _service.Store?.Clear(keepPinned));
        Status = this["clip_cleared"];
    }

    private void Reload()
    {
        try
        {
            var entries = _service.Store?.GetAll(_search) ?? Array.Empty<ClipEntry>();
            Rows.Clear();
            foreach (var e in entries)
            {
                var local = e.CreatedUtc.ToLocalTime().ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);
                Rows.Add(new ClipRow(e, Fmt("clip_chars", e.Text.Length), Num(local)));
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Loading the clipboard history failed.", ex);
            Status = this["clip_db_error"];
        }

        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(IsEmpty));
    }

    protected override void OnSettingsChanged(object? sender, EventArgs e)
    {
        Reload();
        base.OnSettingsChanged(sender, e);
    }

    public override void Dispose()
    {
        _service.Changed -= Reload;
        base.Dispose();
    }
}
