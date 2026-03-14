using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;

namespace FileTransformer.Application.Services;

public sealed class ReviewDecisionService
{
    public (bool RequiresReview, bool AutoApproved, List<string> Reasons, RiskLevel RiskLevel) Evaluate(
        SemanticInsight insight,
        PlanOperationType operationType,
        OrganizationSettings settings,
        bool duplicateDetected,
        bool routedToReviewFolder,
        bool protectionPreventedTransformation,
        IEnumerable<string> warningFlags)
    {
        var review = settings.ReviewPolicy;
        var reasons = new List<string>();

        if (protectionPreventedTransformation)
        {
            reasons.Add("Protected by policy");
        }

        if (insight.Confidence < review.LowConfidenceThreshold)
        {
            reasons.Add("Low semantic confidence");
        }

        if (review.RequireReviewForRenames && operationType is PlanOperationType.Rename or PlanOperationType.MoveAndRename)
        {
            reasons.Add("Rename review is required by policy");
        }

        if (duplicateDetected)
        {
            reasons.Add("Exact duplicate detected");
        }

        if (routedToReviewFolder)
        {
            reasons.Add("Routed to review folder");
        }

        if (settings.ReviewPolicy.ExecutionMode == ExecutionMode.HeuristicsPlusGeminiReviewFirst && insight.GeminiUsed)
        {
            reasons.Add("Gemini-assisted items require review in the selected execution mode");
        }

        if (settings.ReviewPolicy.DeterministicOnlyExecution && insight.GeminiUsed)
        {
            reasons.Add("Deterministic-only execution requires explicit review of Gemini-assisted items");
        }

        reasons.AddRange(warningFlags.Where(flag => !reasons.Contains(flag, StringComparer.OrdinalIgnoreCase)));

        var requiresReview = reasons.Count > 0;
        var autoApproved = !requiresReview && insight.Confidence >= review.AutoApproveConfidenceThreshold;

        if (settings.ReviewPolicy.ExecutionMode == ExecutionMode.FullyAssisted &&
            insight.Confidence >= review.AutoApproveConfidenceThreshold &&
            !duplicateDetected &&
            !protectionPreventedTransformation)
        {
            autoApproved = true;
        }

        var riskLevel = protectionPreventedTransformation
            ? RiskLevel.High
            : requiresReview
                ? RiskLevel.Medium
                : RiskLevel.None;

        return (requiresReview, autoApproved, reasons, riskLevel);
    }
}
