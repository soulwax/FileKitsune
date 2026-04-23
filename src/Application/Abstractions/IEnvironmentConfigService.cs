using FileTransformer.Application.Models;

namespace FileTransformer.Application.Abstractions;

public interface IEnvironmentConfigService
{
    Task<EnvironmentFileSettings> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(EnvironmentFileSettings settings, CancellationToken cancellationToken);
}
