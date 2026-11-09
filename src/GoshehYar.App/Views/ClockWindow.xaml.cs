using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using GoshehYar.App.ViewModels;

namespace GoshehYar.App.Views;

public partial class ClockWindow : Window
{
    private readonly ClockViewModel _viewModel;

    public ClockWindow(ClockViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        BuildTicks();
    }

    /// <summary>Adds the 60 tick marks (longer every 5) to the clock face; they sit under the hands.</summary>
    private void BuildTicks()
    {
        for (var i = 0; i < 60; i++)
        {
            var major = i % 5 == 0;
            var tick = new Line
            {
                X1 = 100,
                Y1 = 8,
                X2 = 100,
                Y2 = major ? 20 : 13,
                StrokeThickness = major ? 3 : 1,
                Stroke = (Brush)FindResource("Brush.Muted"),
                RenderTransform = new RotateTransform(i * 6.0, 100, 100)
            };
            Face.Children.Insert(1, tick);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _viewModel.Dispose();
    }
}
