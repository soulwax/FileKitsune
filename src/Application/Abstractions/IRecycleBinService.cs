namespace FileTransformer.Application.Abstractions;

public interface IRecycleBinService
{
    Task RecycleFileAsync(string fullPath, CancellationToken cancellationToken);
}
