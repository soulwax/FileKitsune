using FileTransformer.Application.Models;

namespace FileTransformer.Application.Abstractions;

public interface IDedupQuarantineService
{
    Task<DedupQuarantineRecord> QuarantineFileAsync(
        Guid runId,
        string rootDirectory,
        string sourceFullPath,
        string sourceRelativePath,
        long sizeBytes,
        CancellationToken cancellationToken);

    Task<DedupQuarantineRestoreResult> RestoreFileAsync(
        DedupQuarantineRecord record,
        CancellationToken cancellationToken);

    string GetRunDirectory(Guid runId);
}
