using FileTransformer.Domain.Enums;

namespace FileTransformer.Domain.Models;

public sealed class ReviewPolicy
{
    public double LowConfidenceThreshold { get; set; } = 0.55d;

    public double AutoApproveConfidenceThreshold { get; set; } = 0.82d;

    public bool RequireReviewForRenames { get; set; } = true;

    public bool SuggestOnlyOnLowConfidence { get; set; } = true;

    public bool RouteLowConfidenceToReviewFolder { get; set; } = true;

    public string ReviewFolderName { get; set; } = "Review";

    public ExecutionMode ExecutionMode { get; set; } = ExecutionMode.HeuristicsPlusGeminiReviewFirst;

    public bool DeterministicOnlyExecution { get; set; }
}
