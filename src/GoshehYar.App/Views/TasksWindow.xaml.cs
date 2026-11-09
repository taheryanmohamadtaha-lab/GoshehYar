using System;
using System.Windows;
using System.Windows.Input;
using GoshehYar.App.ViewModels;

namespace GoshehYar.App.Views;

public partial class TasksWindow : Window
{
    private readonly TasksViewModel _viewModel;

    public TasksWindow(TasksViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        Loaded += (_, _) => NewBox.Focus();
    }

    private void NewBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _viewModel.AddCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void SaveClick(object sender, RoutedEventArgs e) => _viewModel.Commit();

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _viewModel.Dispose();
    }
}
