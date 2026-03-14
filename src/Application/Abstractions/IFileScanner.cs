using FileTransformer.Application.Models;
using FileTransformer.Domain.Models;

namespace FileTransformer.Application.Abstractions;

public interface IFileScanner
{
    Task<IReadOnlyList<ScannedFile>> ScanAsync(
        OrganizationSettings settings,
        IProgress<WorkflowProgress>? progress,
        CancellationToken cancellationToken);
}
