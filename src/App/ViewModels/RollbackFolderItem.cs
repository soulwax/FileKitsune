namespace FileTransformer.App.ViewModels;

public sealed class RollbackFolderItem
{
    public string FolderName { get; init; } = string.Empty;

    public int Count { get; init; }

    public string Label { get; init; } = string.Empty;
}
