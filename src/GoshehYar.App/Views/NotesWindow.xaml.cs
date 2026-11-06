using System;
using System.Windows;
using GoshehYar.App.ViewModels;

namespace GoshehYar.App.Views;

public partial class NotesWindow : Window
{
    private readonly NotesViewModel _viewModel;

    public NotesWindow(NotesViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _viewModel.Dispose(); // saves anything still pending
    }
}
