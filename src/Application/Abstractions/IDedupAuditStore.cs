using FileTransformer.Application.Models;

namespace FileTransformer.Application.Abstractions;

public interface IDedupAuditStore
{
    Task<DedupAuditRunHandle> StartRunAsync(DedupAuditRunStarted run, CancellationToken cancellationToken);

    Task AppendEntryAsync(Guid runId, DedupAuditEntry entry, CancellationToken cancellationToken);

    Task CompleteRunAsync(Guid runId, DedupAuditRunCompleted completed, CancellationToken cancellationToken);
}
