namespace FileTransformer.Application.Models;

public sealed class DedupAuditRunStarted
{
    public required Guid RunId { get; init; }

    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    public required string RootDirectory { get; init; }

    public int TotalGroups { get; init; }

    public int ResolvedGroups { get; init; }

    public int SkippedGroups { get; init; }

    public int FilesPlannedForRecycle { get; init; }

    public int FilesPlannedForQuarantine { get; init; }

    public long BytesPlannedForRecycle { get; init; }

    public long BytesPlannedForQuarantine { get; init; }

    public IReadOnlyList<DedupAuditGroup> Groups { get; init; } = [];
}
