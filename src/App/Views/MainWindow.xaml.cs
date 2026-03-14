using System.Windows;
using FileTransformer.App.ViewModels;

namespace FileTransformer.App.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel viewModel;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await viewModel.InitializeAsync();
    }
}
