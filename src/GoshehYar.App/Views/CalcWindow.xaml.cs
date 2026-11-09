using System;
using System.Windows;
using System.Windows.Input;
using GoshehYar.App.ViewModels;

namespace GoshehYar.App.Views;

public partial class CalcWindow : Window
{
    private readonly CalcViewModel _viewModel;

    public CalcWindow(CalcViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        Loaded += (_, _) => ExprBox.Focus();
    }

    private void ExprBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _viewModel.Evaluate();
            ExprBox.CaretIndex = ExprBox.Text.Length;
            e.Handled = true;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _viewModel.Dispose();
    }
}
