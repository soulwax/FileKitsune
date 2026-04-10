using FileTransformer.Application.Models;
using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;
using FileTransformer.Domain.Services;

namespace FileTransformer.Application.Services;

public sealed class StrategyRecommendationService
{
    public IReadOnlyList<StrategyRecommendation> Recommend(OrganizationPlan plan, int maxCount = 4)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var operations = plan.Operations;
        if (operations.Count == 0 || maxCount <= 0)
        {
            return [];
        }

        var total = (double)operations.Count;
        var nonUncategorizedCount = operations.Count(operation =>
            !string.IsNullOrWhiteSpace(operation.CategoryKey) &&
            !string.Equals(operation.CategoryKey, "uncategorized", StringComparison.OrdinalIgnoreCase));
        var distinctCategories = operations
            .Select(operation => operation.CategoryKey)
            .Where(category => !string.IsNullOrWhiteSpace(category) &&
                               !string.Equals(category, "uncategorized", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var projectSignalCount = operations.Count(operation => !string.IsNullOrWhiteSpace(operation.ProjectOrTopic));
        var datedCount = operations.Count(operation => operation.DateSource != DateSourceKind.None);
        var duplicateCount = operations.Count(operation => operation.DuplicateDetected);
        var reviewCount = operations.Count(operation => operation.RequiresReview);
        var skipCount = operations.Count(operation => operation.OperationType == PlanOperationType.Skip);
        var fileTypeSpread = operations
            .Select(operation => Path.GetExtension(operation.FileName))
            .Where(extension => !string.IsNullOrWhiteSpace(extension))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var semanticCoverage = nonUncategorizedCount / total;
        var categoryDiversity = Math.Min(1d, distinctCategories / 4d);
        var projectDensity = projectSignalCount / total;
        var dateDensity = datedCount / total;
        var duplicateDensity = duplicateCount / total;
        var reviewDensity = reviewCount / total;
        var skipDensity = skipCount / total;
        var fileTypeDensity = Math.Min(1d, fileTypeSpread / 5d);

        var categoryCounts = operations
            .GroupBy(operation => operation.CategoryKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var workDocumentDensity = RatioForCategories(categoryCounts, total, "invoices", "admin", "contracts", "code");
        var researchDensity = RatioForCategories(categoryCounts, total, "research", "teaching", "personal-notes");

        var recommendations =
            new[]
            {
                CreateRecommendation(
                    OrganizationStrategyPreset.SemanticCategoryFirst,
                    0.30d + categoryDiversity * 0.30d + semanticCoverage * 0.25d + fileTypeDensity * 0.10d + (1d - duplicateDensity) * 0.05d,
                    semanticCoverage >= 0.70d
                        ? "Strong semantic category coverage across the previewed files."
                        : "Category signals are present and give the safest general-purpose grouping."),
                CreateRecommendation(
                    OrganizationStrategyPreset.ProjectFirst,
                    0.22d + projectDensity * 0.48d + fileTypeDensity * 0.15d + semanticCoverage * 0.10d,
                    projectDensity >= 0.55d
                        ? "Many files already show project or topic signals."
                        : "Project/topic hints are present and can anchor the folder structure."),
                CreateRecommendation(
                    OrganizationStrategyPreset.DateFirst,
                    0.20d + dateDensity * 0.58d + (1d - reviewDensity) * 0.08d,
                    dateDensity >= 0.60d
                        ? "A large share of files has a usable date signal."
                        : "Date signals are available often enough to support chronological grouping."),
                CreateRecommendation(
                    OrganizationStrategyPreset.HybridProjectDate,
                    0.22d + projectDensity * 0.30d + dateDensity * 0.30d + fileTypeDensity * 0.10d,
                    projectDensity >= 0.35d && dateDensity >= 0.35d
                        ? "Both project and date signals are strong, so a combined structure should work well."
                        : "Project and date signals are both present, making a mixed strategy a good compromise."),
                CreateRecommendation(
                    OrganizationStrategyPreset.ArchiveCleanup,
                    0.18d + duplicateDensity * 0.34d + reviewDensity * 0.22d + skipDensity * 0.12d + (1d - semanticCoverage) * 0.08d,
                    duplicateDensity >= 0.15d || reviewDensity >= 0.35d
                        ? "The preview contains duplicates or many review-heavy items that benefit from a conservative cleanup pass."
                        : "The preview looks mixed enough that a conservative archive cleanup pass may reduce risk."),
                CreateRecommendation(
                    OrganizationStrategyPreset.WorkDocuments,
                    0.20d + workDocumentDensity * 0.45d + projectDensity * 0.15d + dateDensity * 0.08d,
                    workDocumentDensity >= 0.35d
                        ? "Work-oriented categories such as invoices, admin, contracts, or code are common."
                        : "Several files look work-related, so a work-document structure may fit."),
                CreateRecommendation(
                    OrganizationStrategyPreset.ResearchLibrary,
                    0.20d + researchDensity * 0.48d + dateDensity * 0.08d + projectDensity * 0.06d,
                    researchDensity >= 0.35d
                        ? "Research, teaching, or notes categories are prominent in the preview."
                        : "The preview has enough research-like signals to support a library-style structure.")
            };

        if (plan.Guidance is { GeminiUsed: true } guidance)
        {
            recommendations = recommendations
                .Select(recommendation =>
                {
                    var confidence = recommendation.Confidence;
                    var reason = recommendation.Reason;

                    if (recommendation.Preset == guidance.PreferredPreset)
                    {
                        confidence = Math.Min(0.98d, confidence + 0.12d);
                        reason = $"{reason} Gemini also prefers this preset and suggests a {DescribeBias(guidance.StructureBias)} structure with depth {guidance.SuggestedMaxDepth}.";
                    }
                    else if (recommendation.Preset is OrganizationStrategyPreset.HybridProjectDate &&
                             guidance.StructureBias == OrganizationStructureBias.Balanced)
                    {
                        confidence = Math.Min(0.98d, confidence + 0.03d);
                    }

                    return new StrategyRecommendation
                    {
                        Preset = recommendation.Preset,
                        DisplayName = recommendation.DisplayName,
                        Reason = reason,
                        Confidence = confidence
                    };
                })
                .ToArray();
        }

        return recommendations
            .OrderByDescending(recommendation => recommendation.Confidence)
            .ThenBy(recommendation => recommendation.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(maxCount)
            .ToList();
    }

    private static StrategyRecommendation CreateRecommendation(
        OrganizationStrategyPreset preset,
        double score,
        string reason)
    {
        var definition = StrategyPresetCatalog.All.First(definition => definition.Preset == preset);
        return new StrategyRecommendation
        {
            Preset = preset,
            DisplayName = definition.DisplayName,
            Reason = reason,
            Confidence = Math.Clamp(score, 0.15d, 0.98d)
        };
    }

    private static double RatioForCategories(
        IReadOnlyDictionary<string, int> categoryCounts,
        double total,
        params string[] categories)
    {
        if (total <= 0)
        {
            return 0;
        }

        var count = 0;
        foreach (var category in categories)
        {
            if (categoryCounts.TryGetValue(category, out var value))
            {
                count += value;
            }
        }

        return count / total;
    }

    private static string DescribeBias(OrganizationStructureBias bias) =>
        bias switch
        {
            OrganizationStructureBias.Shallower => "shallower",
            OrganizationStructureBias.Deeper => "deeper",
            _ => "balanced"
        };
}
