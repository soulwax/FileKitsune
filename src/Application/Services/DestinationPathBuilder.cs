using System.Globalization;
using System.Text;
using FileTransformer.Application.Models;
using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;
using FileTransformer.Domain.Services;

namespace FileTransformer.Application.Services;

public sealed class DestinationPathBuilder
{
    private readonly NamingPolicyService namingPolicyService;
    private readonly ReviewDecisionService reviewDecisionService;

    public DestinationPathBuilder(
        NamingPolicyService namingPolicyService,
        ReviewDecisionService reviewDecisionService)
    {
        this.namingPolicyService = namingPolicyService;
        this.reviewDecisionService = reviewDecisionService;
    }

    public PlanOperation Build(
        FileAnalysisContext context,
        FileAnalysisContext groupLeader,
        OrganizationSettings settings,
        StrategyPresetDefinition strategyDefinition,
        IReadOnlyDictionary<string, int> categoryCounts,
        PathSafetyService pathSafetyService,
        bool sharedFolderGroup)
    {
        var warnings = new List<string>();
        var protectionPreventedTransformation = context.ProtectedByRule;
        var duplicateDetected = context.DuplicateMatch.IsDuplicate;
        var routedToReviewFolder = false;

        if (protectionPreventedTransformation)
        {
            warnings.Add(context.ProtectionReason);
            return CreateBlockedOperation(
                context,
                groupLeader,
                settings,
                strategyDefinition,
                warnings,
                protectionPreventedTransformation: true);
        }

        if (duplicateDetected && settings.DuplicatePolicy.HandlingMode == DuplicateHandlingMode.Skip)
        {
            warnings.Add($"Exact duplicate of '{context.DuplicateMatch.CanonicalRelativePath}'.");
            return CreateBlockedOperation(
                context,
                groupLeader,
                settings,
                strategyDefinition,
                warnings,
                duplicateDetected: true);
        }

        var folderSegments = BuildFolderSegments(context, groupLeader, settings, strategyDefinition, categoryCounts, sharedFolderGroup, warnings);

        if (duplicateDetected && settings.DuplicatePolicy.HandlingMode == DuplicateHandlingMode.RouteToDuplicatesFolder)
        {
            folderSegments =
            [
                WindowsPathRules.SanitizePathSegment(settings.DuplicatePolicy.DuplicatesFolderName),
                context.DuplicateMatch.ContentHash[..Math.Min(8, context.DuplicateMatch.ContentHash.Length)]
            ];

            warnings.Add($"Exact duplicate routed away from the main structure. Canonical file: {context.DuplicateMatch.CanonicalRelativePath}.");
        }

        if (ShouldRouteToReviewFolder(groupLeader, settings, strategyDefinition, duplicateDetected))
        {
            folderSegments.Insert(0, WindowsPathRules.SanitizePathSegment(settings.ReviewPolicy.ReviewFolderName));
            routedToReviewFolder = true;
            warnings.Add("Routed to review folder because policy requires manual approval.");
        }

        folderSegments = ApplyMaximumDepth(folderSegments, settings.OrganizationPolicy.MaximumFolderDepth, warnings);

        var forceConservativeRename =
            strategyDefinition.ConservativeRenaming ||
            groupLeader.Insight.LanguageContext == DetectedLanguageContext.Unclear ||
            groupLeader.Insight.Confidence < settings.ReviewPolicy.AutoApproveConfidenceThreshold;

        var fileName = namingPolicyService.BuildFileName(
            context.File,
            groupLeader.Insight,
            groupLeader.DateResolution,
            settings,
            forceConservativeRename);

        var candidateRelativePath = folderSegments.Count == 0
            ? fileName
            : Path.Combine(folderSegments.Append(fileName).ToArray());

        var validation = pathSafetyService.ValidateDestination(settings.RootDirectory, candidateRelativePath);
        warnings.AddRange(validation.Errors);

        var proposedRelativePath = validation.IsValid && !string.IsNullOrWhiteSpace(validation.NormalizedRelativePath)
            ? validation.NormalizedRelativePath
            : context.File.RelativePath;

        var operationType = DetermineOperationType(context.File.RelativePath, proposedRelativePath);
        var reviewDecision = reviewDecisionService.Evaluate(
            groupLeader.Insight,
            operationType,
            settings,
            duplicateDetected,
            routedToReviewFolder,
            protectionPreventedTransformation: false,
            warnings);

        var reason = BuildReason(context, groupLeader, strategyDefinition, warnings, reviewDecision.Reasons);
        var allowedToExecute = validation.IsValid && operationType != PlanOperationType.Skip;

        return new PlanOperation
        {
            OperationType = operationType,
            CurrentRelativePath = context.File.RelativePath,
            ProposedRelativePath = proposedRelativePath,
            Reason = reason,
            Confidence = groupLeader.Insight.Confidence,
            RiskLevel = reviewDecision.RiskLevel,
            WarningFlags = warnings,
            RequiresReview = reviewDecision.RequiresReview,
            AllowedToExecute = allowedToExecute,
            AutoApproved = allowedToExecute && reviewDecision.AutoApproved,
            GeminiUsed = groupLeader.Insight.GeminiUsed,
            LanguageContext = groupLeader.Insight.LanguageContext,
            CategoryKey = groupLeader.Insight.CategoryKey,
            CategoryDisplayName = ResolveCategoryLabel(groupLeader.Insight, settings, categoryCounts),
            ProjectOrTopic = groupLeader.Insight.ProjectOrTopic,
            FileName = context.File.FileName,
            SourceSizeBytes = context.File.SizeBytes,
            SourceModifiedUtc = context.File.ModifiedUtc,
            SourceContentHash = context.DuplicateMatch.ContentHash,
            StrategyPreset = strategyDefinition.Preset,
            ReviewReasons = reviewDecision.Reasons,
            DuplicateDetected = duplicateDetected,
            DuplicateOfRelativePath = context.DuplicateMatch.CanonicalRelativePath,
            ProtectionPreventedTransformation = false,
            ProtectionReason = string.Empty,
            RoutedToReviewFolder = routedToReviewFolder,
            DateSource = groupLeader.DateResolution.Source
        };
    }

    private PlanOperation CreateBlockedOperation(
        FileAnalysisContext context,
        FileAnalysisContext groupLeader,
        OrganizationSettings settings,
        StrategyPresetDefinition strategyDefinition,
        List<string> warnings,
        bool protectionPreventedTransformation = false,
        bool duplicateDetected = false)
    {
        var reviewDecision = reviewDecisionService.Evaluate(
            groupLeader.Insight,
            PlanOperationType.Skip,
            settings,
            duplicateDetected,
            routedToReviewFolder: false,
            protectionPreventedTransformation,
            warnings);

        return new PlanOperation
        {
            OperationType = PlanOperationType.Skip,
            CurrentRelativePath = context.File.RelativePath,
            ProposedRelativePath = context.File.RelativePath,
            Reason = BuildReason(context, groupLeader, strategyDefinition, warnings, reviewDecision.Reasons),
            Confidence = groupLeader.Insight.Confidence,
            RiskLevel = protectionPreventedTransformation ? RiskLevel.High : reviewDecision.RiskLevel,
            WarningFlags = warnings,
            RequiresReview = reviewDecision.RequiresReview,
            AllowedToExecute = false,
            AutoApproved = false,
            GeminiUsed = groupLeader.Insight.GeminiUsed,
            LanguageContext = groupLeader.Insight.LanguageContext,
            CategoryKey = groupLeader.Insight.CategoryKey,
            CategoryDisplayName = groupLeader.Insight.OriginalCategoryLabel,
            ProjectOrTopic = groupLeader.Insight.ProjectOrTopic,
            FileName = context.File.FileName,
            SourceSizeBytes = context.File.SizeBytes,
            SourceModifiedUtc = context.File.ModifiedUtc,
            SourceContentHash = context.DuplicateMatch.ContentHash,
            StrategyPreset = strategyDefinition.Preset,
            ReviewReasons = reviewDecision.Reasons,
            DuplicateDetected = duplicateDetected,
            DuplicateOfRelativePath = context.DuplicateMatch.CanonicalRelativePath,
            ProtectionPreventedTransformation = protectionPreventedTransformation,
            ProtectionReason = context.ProtectionReason,
            RoutedToReviewFolder = false,
            DateSource = groupLeader.DateResolution.Source
        };
    }

    private static List<string> BuildFolderSegments(
        FileAnalysisContext context,
        FileAnalysisContext groupLeader,
        OrganizationSettings settings,
        StrategyPresetDefinition strategyDefinition,
        IReadOnlyDictionary<string, int> categoryCounts,
        bool sharedFolderGroup,
        List<string> warnings)
    {
        var segments = new List<string>();
        var categoryLabel = ResolveCategoryLabel(groupLeader.Insight, settings, categoryCounts);
        var projectSegment = ResolveProjectSegment(groupLeader.Insight, settings);
        var dateResolution = groupLeader.DateResolution;

        foreach (var segmentKind in strategyDefinition.SegmentOrder)
        {
            switch (segmentKind)
            {
                case PathSegmentKind.Category:
                    if (!string.IsNullOrWhiteSpace(categoryLabel))
                    {
                        segments.Add(WindowsPathRules.SanitizePathSegment(categoryLabel));
                    }
                    break;

                case PathSegmentKind.Project:
                    if (!string.IsNullOrWhiteSpace(projectSegment))
                    {
                        segments.Add(projectSegment);
                    }
                    break;

                case PathSegmentKind.Year:
                    if (dateResolution.Value is not null)
                    {
                        segments.Add(dateResolution.Value.Value.ToLocalTime().ToString("yyyy", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        warnings.Add("No reliable year source was available.");
                    }
                    break;

                case PathSegmentKind.Month:
                    if (dateResolution.Value is not null)
                    {
                        segments.Add(dateResolution.Value.Value.ToLocalTime().ToString("MM", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        warnings.Add("No reliable month source was available.");
                    }
                    break;

                case PathSegmentKind.FileType:
                    if (!sharedFolderGroup && !string.IsNullOrWhiteSpace(context.File.Extension))
                    {
                        segments.Add($"{context.File.Extension.Trim('.').ToUpperInvariant()} Files");
                    }
                    break;
            }
        }

        return segments;
    }

    private static string ResolveCategoryLabel(
        SemanticInsight insight,
        OrganizationSettings settings,
        IReadOnlyDictionary<string, int> categoryCounts)
    {
        var categoryKey = insight.CategoryKey;
        var organization = settings.OrganizationPolicy;

        if (organization.MergeSparseCategories &&
            categoryCounts.TryGetValue(categoryKey, out var count) &&
            count < Math.Max(1, organization.SparseCategoryThreshold))
        {
            return organization.MiscellaneousBucketName;
        }

        return SemanticCatalog.ResolveDisplayName(
            categoryKey,
            settings.NamingPolicy.FolderLanguageMode,
            insight.OriginalCategoryLabel);
    }

    private static string ResolveProjectSegment(SemanticInsight insight, OrganizationSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(insight.ProjectOrTopic))
        {
            return WindowsPathRules.SanitizePathSegment(insight.ProjectOrTopic);
        }

        if (!settings.OrganizationPolicy.PreferGeminiFolderSuggestion || string.IsNullOrWhiteSpace(insight.SuggestedFolderFragment))
        {
            return string.Empty;
        }

        var firstSegment = insight.SuggestedFolderFragment
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(firstSegment)
            ? string.Empty
            : WindowsPathRules.SanitizePathSegment(firstSegment);
    }

    private static List<string> ApplyMaximumDepth(List<string> segments, int maximumFolderDepth, List<string> warnings)
    {
        if (maximumFolderDepth <= 0 || segments.Count <= maximumFolderDepth)
        {
            return segments;
        }

        var kept = segments.Take(Math.Max(1, maximumFolderDepth - 1)).ToList();
        var overflow = string.Join(" - ", segments.Skip(kept.Count));
        kept.Add(WindowsPathRules.SanitizePathSegment(overflow));
        warnings.Add($"Folder depth limited to {maximumFolderDepth} level(s).");
        return kept;
    }

    private static bool ShouldRouteToReviewFolder(
        FileAnalysisContext groupLeader,
        OrganizationSettings settings,
        StrategyPresetDefinition strategyDefinition,
        bool duplicateDetected)
    {
        var insight = groupLeader.Insight;
        var review = settings.ReviewPolicy;

        if (duplicateDetected && settings.DuplicatePolicy.HandlingMode == DuplicateHandlingMode.RequireReview)
        {
            return true;
        }

        if (!review.RouteLowConfidenceToReviewFolder)
        {
            return false;
        }

        return insight.Confidence < review.LowConfidenceThreshold ||
               insight.LanguageContext == DetectedLanguageContext.Unclear ||
               (strategyDefinition.ReviewLowConfidenceByDefault &&
                insight.Confidence < review.AutoApproveConfidenceThreshold);
    }

    private static PlanOperationType DetermineOperationType(string currentRelativePath, string proposedRelativePath)
    {
        if (string.Equals(currentRelativePath, proposedRelativePath, StringComparison.OrdinalIgnoreCase))
        {
            return PlanOperationType.Skip;
        }

        var currentDirectory = Path.GetDirectoryName(currentRelativePath) ?? string.Empty;
        var proposedDirectory = Path.GetDirectoryName(proposedRelativePath) ?? string.Empty;
        var currentFileName = Path.GetFileName(currentRelativePath);
        var proposedFileName = Path.GetFileName(proposedRelativePath);

        if (!string.Equals(currentDirectory, proposedDirectory, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(currentFileName, proposedFileName, StringComparison.OrdinalIgnoreCase))
        {
            return PlanOperationType.MoveAndRename;
        }

        if (!string.Equals(currentDirectory, proposedDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return PlanOperationType.Move;
        }

        if (!string.Equals(currentFileName, proposedFileName, StringComparison.OrdinalIgnoreCase))
        {
            return PlanOperationType.Rename;
        }

        return PlanOperationType.Skip;
    }

    private static string BuildReason(
        FileAnalysisContext context,
        FileAnalysisContext groupLeader,
        StrategyPresetDefinition strategyDefinition,
        IReadOnlyCollection<string> warnings,
        IReadOnlyCollection<string> reviewReasons)
    {
        var builder = new StringBuilder();
        builder.Append(groupLeader.Insight.Explanation);
        builder.Append(" Strategy: ");
        builder.Append(strategyDefinition.DisplayName);
        builder.Append(". File: '");
        builder.Append(context.File.FileName);
        builder.Append("'.");

        if (groupLeader.DateResolution.Value is not null)
        {
            builder.Append(" Date source: ");
            builder.Append(groupLeader.DateResolution.Source);
            builder.Append('.');
        }

        if (context.DuplicateMatch.IsDuplicate)
        {
            builder.Append(" Duplicate of '");
            builder.Append(context.DuplicateMatch.CanonicalRelativePath);
            builder.Append("'.");
        }

        if (warnings.Count > 0)
        {
            builder.Append(" Warnings: ");
            builder.Append(string.Join("; ", warnings));
            builder.Append('.');
        }

        if (reviewReasons.Count > 0)
        {
            builder.Append(" Review: ");
            builder.Append(string.Join("; ", reviewReasons));
            builder.Append('.');
        }

        return builder.ToString();
    }
}
