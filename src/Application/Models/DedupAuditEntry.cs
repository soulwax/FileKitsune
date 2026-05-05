namespace FileTransformer.Application.Models;

public sealed class DedupAuditEntry
{
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    public required string Action { get; init; }

    public required string FullPath { get; init; }

    public required string RelativePath { get; init; }

    public long SizeBytes { get; init; }

    public string Status { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}
