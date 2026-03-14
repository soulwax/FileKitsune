namespace FileTransformer.App.Services;

public interface IDialogService
{
    bool Confirm(string title, string message);

    void ShowInformation(string title, string message);

    void ShowError(string title, string message);
}
