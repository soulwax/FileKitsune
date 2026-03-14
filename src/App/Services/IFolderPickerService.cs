namespace FileTransformer.App.Services;

public interface IFolderPickerService
{
    string? PickFolder(string? initialPath);
}
