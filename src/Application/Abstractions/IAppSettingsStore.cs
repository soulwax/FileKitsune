using FileTransformer.Application.Models;

namespace FileTransformer.Application.Abstractions;

public interface IAppSettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken);
}
