using FileTransformer.Domain.Models;

namespace FileTransformer.Application.Models;

public sealed class DuplicateDetectionResult
{
    public IReadOnlyDictionary<string, DuplicateMatch> Matches { get; init; } =
        new Dictionary<string, DuplicateMatch>(StringComparer.OrdinalIgnoreCase);

    public int CandidateFileCount { get; init; }

    public int HashFailureCount { get; init; }
}
