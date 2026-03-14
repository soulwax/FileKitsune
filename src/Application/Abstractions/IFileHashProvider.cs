namespace FileTransformer.Application.Abstractions;

public interface IFileHashProvider
{
    Task<string> ComputeHashAsync(string fullPath, CancellationToken cancellationToken);
}
