using System;
using System.Windows;
using GoshehYar.App.ViewModels;

namespace GoshehYar.App.Views;

public partial class TimerWindow : Window
{
    private readonly TimerViewModel _viewModel;

    public TimerWindow(TimerViewModel viewModel)
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
