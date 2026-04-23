namespace FileTransformer.Infrastructure.Configuration;

public static class AppEnvironmentPaths
{
    public static string ResolveWritableEnvPath(string? currentDirectory = null, string? baseDirectory = null)
    {
        var root = ResolveProjectRoot(currentDirectory, baseDirectory);
        return Path.Combine(root, ".env");
    }

    public static IReadOnlyList<string> GetCandidateEnvPaths(string? currentDirectory = null, string? baseDirectory = null)
    {
        var paths = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var fullPath = Path.GetFullPath(path);
            if (seen.Add(fullPath))
            {
                paths.Add(fullPath);
            }
        }

        AddPath(ResolveWritableEnvPath(currentDirectory, baseDirectory));
        AddPath(Path.Combine(currentDirectory ?? Directory.GetCurrentDirectory(), ".env"));
        AddPath(Path.Combine(baseDirectory ?? AppContext.BaseDirectory, ".env"));

        return paths;
    }

    private static string ResolveProjectRoot(string? currentDirectory = null, string? baseDirectory = null)
    {
        foreach (var start in new[]
                 {
                     currentDirectory ?? Directory.GetCurrentDirectory(),
                     baseDirectory ?? AppContext.BaseDirectory
                 })
        {
            var resolved = FindMarkedDirectory(start);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }
        }

        return currentDirectory ?? Directory.GetCurrentDirectory();
    }

    private static string? FindMarkedDirectory(string? startDirectory)
    {
        if (string.IsNullOrWhiteSpace(startDirectory))
        {
            return null;
        }

        var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, ".env.example")) ||
                File.Exists(Path.Combine(directory.FullName, "FileTransformer.sln")) ||
                Directory.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
