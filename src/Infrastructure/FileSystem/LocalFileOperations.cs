using FileTransformer.Application.Abstractions;

namespace FileTransformer.Infrastructure.FileSystem;

public sealed class LocalFileOperations : IFileOperations
{
    public bool FileExists(string fullPath) => File.Exists(fullPath);

    public bool DirectoryExists(string fullPath) => Directory.Exists(fullPath);

    public Task EnsureDirectoryAsync(string fullPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(fullPath);
        return Task.CompletedTask;
    }

    public Task MoveFileAsync(string sourceFullPath, string destinationFullPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() => File.Move(sourceFullPath, destinationFullPath, overwrite: false), cancellationToken);
    }

    public Task<IReadOnlyList<string>> RemoveEmptyDirectoriesAsync(string rootDirectory, CancellationToken cancellationToken)
    {
        return Task.Run<IReadOnlyList<string>>(() =>
        {
            var removed = new List<string>();
            var directories = Directory.EnumerateDirectories(rootDirectory, "*", SearchOption.AllDirectories)
                .OrderByDescending(path => path.Length)
                .ToList();

            foreach (var directory in directories)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    continue;
                }

                Directory.Delete(directory, false);
                removed.Add(directory);
            }

            return removed;
        }, cancellationToken);
    }
}
