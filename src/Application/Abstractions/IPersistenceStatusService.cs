using FileTransformer.Application.Models;

namespace FileTransformer.Application.Abstractions;

public interface IPersistenceStatusService
{
    Task<PersistenceStatusSnapshot> GetStatusAsync(CancellationToken cancellationToken);
}
