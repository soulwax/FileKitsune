namespace FileTransformer.Application.Models;

public sealed class DedupAuditGroup
{
    public required string KeeperFullPath { get; init; }

    public required string KeeperRelativePath { get; init; }

    public bool IsSkipped { get; init; }

    public IReadOnlyList<string> DuplicateFullPaths { get; init; } = [];

    public IReadOnlyList<string> DuplicateRelativePaths { get; init; } = [];
}
