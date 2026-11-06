using System;
using System.Windows;
using GoshehYar.App.ViewModels;

namespace GoshehYar.App.Views;

public partial class RecorderWindow : Window
{
    private readonly RecorderViewModel _viewModel;

    public RecorderWindow(RecorderViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        // A running recording is not affected: it lives in the app and the tray icon shows it.
        _viewModel.Dispose();
    }
}
