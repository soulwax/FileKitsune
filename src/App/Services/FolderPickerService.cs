using Forms = System.Windows.Forms;

namespace FileTransformer.App.Services;

public sealed class FolderPickerService : IFolderPickerService
{
    public string? PickFolder(string? initialPath)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Select the root directory to organize.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (!string.IsNullOrWhiteSpace(initialPath) && System.IO.Directory.Exists(initialPath))
        {
            dialog.SelectedPath = initialPath;
        }

        return dialog.ShowDialog() == Forms.DialogResult.OK ? dialog.SelectedPath : null;
    }
}
