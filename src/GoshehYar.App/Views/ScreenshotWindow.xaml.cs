using System;
using System.Windows;
using GoshehYar.App.ViewModels;

namespace GoshehYar.App.Views;

public partial class ScreenshotWindow : Window
{
    private readonly ScreenshotViewModel _viewModel;

    public ScreenshotWindow(ScreenshotViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        // While a capture runs this window steps aside so it is not in the picture.
        viewModel.SetWindowVisible = visible =>
        {
            if (visible)
            {
                Show();
                Activate();
            }
            else
            {
                Hide();
            }
        };
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _viewModel.SetWindowVisible = null;
        _viewModel.Dispose();
    }
}
