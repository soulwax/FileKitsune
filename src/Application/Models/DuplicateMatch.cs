namespace FileTransformer.Application.Models;

public sealed class DuplicateMatch
{
    public bool IsDuplicate { get; init; }

    public string ContentHash { get; init; } = string.Empty;

    public string CanonicalRelativePath { get; init; } = string.Empty;
}
