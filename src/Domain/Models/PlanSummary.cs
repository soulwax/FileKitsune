namespace FileTransformer.Domain.Models;

public sealed class PlanSummary
{
    public int TotalItems { get; init; }

    public int MoveCount { get; init; }

    public int RenameCount { get; init; }

    public int MoveAndRenameCount { get; init; }

    public int SkipCount { get; init; }

    public int GeminiAssistedCount { get; init; }

    public int RequiresReviewCount { get; init; }

    public int HighRiskCount { get; init; }

    public int DuplicateCount { get; init; }

    public int ProtectedCount { get; init; }

    public int AutoApprovedCount { get; init; }
}
