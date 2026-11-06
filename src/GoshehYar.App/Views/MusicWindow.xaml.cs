using System;
using System.Windows;
using System.Windows.Input;
using GoshehYar.App.ViewModels;

namespace GoshehYar.App.Views;

public partial class MusicWindow : Window
{
    private readonly MusicViewModel _viewModel;

    public MusicWindow(MusicViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    private void OnSeekDown(object sender, MouseButtonEventArgs e) => _viewModel.BeginScrub();

    private void OnSeekUp(object sender, MouseEventArgs e)
    {
        // The slider already moved to the clicked point; apply it to the player.
        _viewModel.EndScrub();
    }

    private void OnTrackDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.SelectedTrack is not null) _viewModel.PlayTrack(_viewModel.SelectedTrack);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        // Music keeps playing; only the window goes away.
        _viewModel.Dispose();
    }
}
