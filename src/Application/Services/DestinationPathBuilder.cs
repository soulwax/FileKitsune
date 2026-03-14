using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;
using FileTransformer.Domain.Services;

namespace FileTransformer.Application.Services;

public sealed partial class DestinationPathBuilder
{
    public PlanOperation Build(
        ScannedFile file,
        SemanticInsight insight,
        OrganizationSettings settings,
        PathSafetyService pathSafetyService)
    {
        var categoryLabel = SemanticCatalog.ResolveDisplayName(
            insight.CategoryKey,
            settings.FolderLanguageMode,
            insight.OriginalCategoryLabel);

        var segments = new List<string>();

        if (settings.Dimensions.HasFlag(OrganizationDimension.SemanticCategory))
        {
            segments.Add(WindowsPathRules.SanitizePathSegment(categoryLabel));
        }

        if (settings.Dimensions.HasFlag(OrganizationDimension.Project))
        {
            var projectSegment = BuildProjectSegment(insight, settings);
            if (!string.IsNullOrWhiteSpace(projectSegment))
            {
                segments.Add(projectSegment);
            }
        }

        if (settings.Dimensions.HasFlag(OrganizationDimension.Year))
        {
            segments.Add(file.ModifiedUtc.ToLocalTime().ToString("yyyy", CultureInfo.InvariantCulture));
        }

        if (settings.Dimensions.HasFlag(OrganizationDimension.Month))
        {
            segments.Add(file.ModifiedUtc.ToLocalTime().ToString("MM", CultureInfo.InvariantCulture));
        }

        if (settings.UseFileTypeAsSecondaryCriterion && !string.IsNullOrWhiteSpace(file.Extension))
        {
            segments.Add($"{file.Extension.Trim('.').ToUpperInvariant()} Files");
        }

        var fileName = BuildFileName(file, insight, settings);
        var candidateRelativePath = Path.Combine(segments.Append(fileName).ToArray());
        var validation = pathSafetyService.ValidateDestination(settings.RootDirectory, candidateRelativePath);
        var warningFlags = new List<string>();

        if (insight.LanguageContext is DetectedLanguageContext.Mixed or DetectedLanguageContext.Unclear)
        {
            warningFlags.Add("Mixed or unclear language context");
        }

        if (insight.Confidence < settings.LowConfidenceThreshold)
        {
            warningFlags.Add("Low semantic confidence");
        }

        warningFlags.AddRange(validation.Errors);

        var operationType = DetermineOperationType(file.RelativePath, validation.NormalizedRelativePath);
        var requiresReview = warningFlags.Count > 0;
        var allowedToExecute = validation.IsValid &&
                               !(settings.SuggestOnlyOnLowConfidence && insight.Confidence < settings.LowConfidenceThreshold);
        var riskLevel = DetermineRiskLevel(validation, warningFlags, allowedToExecute);

        return new PlanOperation
        {
            OperationType = operationType,
            CurrentRelativePath = file.RelativePath,
            ProposedRelativePath = validation.NormalizedRelativePath,
            Reason = BuildReason(insight, file, settings, warningFlags),
            Confidence = insight.Confidence,
            RiskLevel = riskLevel,
            WarningFlags = warningFlags,
            RequiresReview = requiresReview,
            AllowedToExecute = allowedToExecute,
            GeminiUsed = insight.GeminiUsed,
            LanguageContext = insight.LanguageContext,
            CategoryKey = insight.CategoryKey,
            CategoryDisplayName = categoryLabel,
            ProjectOrTopic = insight.ProjectOrTopic,
            FileName = file.FileName
        };
    }

    private static string BuildProjectSegment(SemanticInsight insight, OrganizationSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(insight.ProjectOrTopic))
        {
            return WindowsPathRules.SanitizePathSegment(insight.ProjectOrTopic);
        }

        if (!settings.PreferGeminiFolderSuggestion || string.IsNullOrWhiteSpace(insight.SuggestedFolderFragment))
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

    private static string BuildFileName(ScannedFile file, SemanticInsight insight, OrganizationSettings settings)
    {
        if (!settings.AllowFileRename || settings.FileRenameMode == FileRenameMode.KeepOriginal)
        {
            return file.FileName;
        }

        var extension = Path.GetExtension(file.FileName);
        var originalBaseName = Path.GetFileNameWithoutExtension(file.FileName);
        var cleanedBaseName = NormalizeBaseName(originalBaseName);

        if (settings.FileRenameMode == FileRenameMode.NormalizeWhitespaceAndPunctuation ||
            insight.LanguageContext is DetectedLanguageContext.Mixed or DetectedLanguageContext.Unclear ||
            insight.Confidence < settings.LowConfidenceThreshold)
        {
            return $"{cleanedBaseName}{extension}";
        }

        var tokenMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{originalName}"] = cleanedBaseName,
            ["{category}"] = WindowsPathRules.SanitizePathSegment(insight.OriginalCategoryLabel),
            ["{project}"] = WindowsPathRules.SanitizePathSegment(insight.ProjectOrTopic),
            ["{date}"] = file.ModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };

        var candidateBaseName = settings.NamingTemplate;
        foreach (var (token, value) in tokenMap)
        {
            candidateBaseName = candidateBaseName.Replace(token, value, StringComparison.OrdinalIgnoreCase);
        }

        candidateBaseName = NormalizeBaseName(candidateBaseName);
        return string.IsNullOrWhiteSpace(candidateBaseName)
            ? $"{cleanedBaseName}{extension}"
            : $"{candidateBaseName}{extension}";
    }

    private static string NormalizeBaseName(string value)
    {
        var collapsed = MultiSeparatorRegex().Replace(value.Normalize(NormalizationForm.FormC), " ");
        return WindowsPathRules.SanitizePathSegment(collapsed);
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

    private static RiskLevel DetermineRiskLevel(PathValidationResult validation, IEnumerable<string> warningFlags, bool allowedToExecute)
    {
        if (!validation.IsValid)
        {
            return RiskLevel.High;
        }

        if (!allowedToExecute || warningFlags.Contains("Low semantic confidence", StringComparer.OrdinalIgnoreCase))
        {
            return RiskLevel.Medium;
        }

        return warningFlags.Any() ? RiskLevel.Low : RiskLevel.None;
    }

    private static string BuildReason(
        SemanticInsight insight,
        ScannedFile file,
        OrganizationSettings settings,
        IReadOnlyCollection<string> warningFlags)
    {
        var builder = new StringBuilder();
        builder.Append(insight.Explanation);
        builder.Append(" Based on '");
        builder.Append(file.FileName);
        builder.Append('\'');

        if (settings.Dimensions.HasFlag(OrganizationDimension.Year))
        {
            builder.Append(", dated ");
            builder.Append(file.ModifiedUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (warningFlags.Count > 0)
        {
            builder.Append(". Review flags: ");
            builder.Append(string.Join("; ", warningFlags));
            builder.Append('.');
        }

        return builder.ToString();
    }

    [GeneratedRegex(@"[\s\-_]+")]
    private static partial Regex MultiSeparatorRegex();
}
