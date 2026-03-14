using System.Windows;

namespace FileTransformer.App.Services;

public sealed class DialogService : IDialogService
{
    public bool Confirm(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public void ShowInformation(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowError(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
}
