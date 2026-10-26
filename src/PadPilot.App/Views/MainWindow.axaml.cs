using System.ComponentModel;
using Avalonia.Controls;
using PadPilot.App.ViewModels;

namespace PadPilot.App.Views;

public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_viewModel is not null) _viewModel.PropertyChanged -= OnViewModelChanged;
            _viewModel = DataContext as MainViewModel;
            if (_viewModel is null) return;
            _viewModel.PropertyChanged += OnViewModelChanged;
            FitLayout();
        };
        Closed += (_, _) =>
        {
            if (_viewModel is not null) _viewModel.PropertyChanged -= OnViewModelChanged;
        };
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsHorizontal)) FitLayout();
    }

    /// <summary>El diseño horizontal (lista de mandos a la izquierda) necesita una ventana más ancha.</summary>
    private void FitLayout()
    {
        if (_viewModel is null) return;
        if (_viewModel.IsHorizontal)
        {
            if (Width < 860) Width = 900;
            if (Height > 780) Height = 740;
        }
        else if (Width > 700)
        {
            Width = 620;
            Height = 820;
        }
    }
}
