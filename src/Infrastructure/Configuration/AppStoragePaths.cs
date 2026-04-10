namespace FileTransformer.Infrastructure.Configuration;

public sealed class AppStoragePaths
{
    public AppStoragePaths(string? rootDirectory = null)
    {
        RootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FileTransformer")
            : rootDirectory;
    }

    public string RootDirectory { get; }

    public string SettingsFilePath => Path.Combine(RootDirectory, "settings.json");

    public string PersistenceDatabasePath => Path.Combine(RootDirectory, "persistence.db");

    public string JournalDirectory => Path.Combine(RootDirectory, "journals");

    public string LogsDirectory => Path.Combine(RootDirectory, "logs");
}
