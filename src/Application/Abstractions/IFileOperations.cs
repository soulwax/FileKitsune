namespace FileTransformer.Application.Abstractions;

public interface IFileOperations
{
    bool FileExists(string fullPath);

    bool DirectoryExists(string fullPath);

    Task EnsureDirectoryAsync(string fullPath, CancellationToken cancellationToken);

    Task MoveFileAsync(string sourceFullPath, string destinationFullPath, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> RemoveEmptyDirectoriesAsync(string rootDirectory, CancellationToken cancellationToken);
}
