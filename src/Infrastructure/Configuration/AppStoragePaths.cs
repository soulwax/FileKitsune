namespace FileTransformer.Infrastructure.Configuration;

public sealed class AppStoragePaths
{
    public string RootDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FileTransformer");

    public string SettingsFilePath => Path.Combine(RootDirectory, "settings.json");

    public string JournalDirectory => Path.Combine(RootDirectory, "journals");

    public string LogsDirectory => Path.Combine(RootDirectory, "logs");
}
