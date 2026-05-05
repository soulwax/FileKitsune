namespace FileTransformer.Application.Models;

public sealed class DedupQuarantineRecord
{
    public required Guid RunId { get; init; }

    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    public required string OriginalFullPath { get; init; }

    public required string OriginalRelativePath { get; init; }

    public required string QuarantineFullPath { get; init; }

    public required string QuarantineRunDirectory { get; init; }

    public long SizeBytes { get; init; }

    public string Status { get; init; } = "MovedToQuarantine";
}
