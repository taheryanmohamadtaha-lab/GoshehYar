using System;
using System.Windows;
using System.Windows.Input;
using GoshehYar.App.ViewModels;

namespace GoshehYar.App.Views;

public partial class WeatherWindow : Window
{
    private readonly WeatherViewModel _viewModel;

    public WeatherWindow(WeatherViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    private void QueryBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _viewModel.SearchCommand.CanExecute(null))
        {
            _viewModel.SearchCommand.Execute(null);
            e.Handled = true;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _viewModel.Dispose();
    }
}
