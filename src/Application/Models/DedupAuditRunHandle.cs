namespace FileTransformer.Application.Models;

public sealed class DedupAuditRunHandle
{
    public required Guid RunId { get; init; }

    public required string AuditFilePath { get; init; }
}
