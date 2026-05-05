namespace FileTransformer.Application.Models;

public sealed class DedupAuditRunCompleted
{
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    public required string Status { get; init; }

    public int FilesRecycled { get; init; }

    public int FilesSkipped { get; init; }

    public int Errors { get; init; }

    public long BytesFreed { get; init; }
}
