using System;
using System.Windows;
using GoshehYar.App.ViewModels;

namespace GoshehYar.App.Views;

public partial class CalendarWindow : Window
{
    private readonly CalendarViewModel _viewModel;

    public CalendarWindow(CalendarViewModel viewModel)
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
