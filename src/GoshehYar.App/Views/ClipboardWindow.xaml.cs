using System;
using System.Windows;
using GoshehYar.App.ViewModels;

namespace GoshehYar.App.Views;

public partial class ClipboardWindow : Window
{
    private readonly ClipboardViewModel _viewModel;

    public ClipboardWindow(ClipboardViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _viewModel.Dispose();
    }
}
