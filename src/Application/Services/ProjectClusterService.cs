using System.Globalization;
using System.Text.RegularExpressions;
using FileTransformer.Application.Models;
using FileTransformer.Domain.Models;

namespace FileTransformer.Application.Services;

public sealed partial class ProjectClusterService
{
    private static readonly HashSet<string> GenericTokens =
    [
        "documents",
        "dokumente",
        "files",
        "dateien",
        "misc",
        "archive",
        "archiv",
        "incoming",
        "input",
        "review",
        "downloads",
        "download"
    ];

    public void EnrichClusters(IReadOnlyList<FileAnalysisContext> contexts)
    {
        if (contexts.Count < 2)
        {
            return;
        }

        var candidates = contexts
            .Select(context => new
            {
                Context = context,
                Candidate = ResolveClusterCandidate(context)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Candidate.NormalizedKey))
            .ToList();

        foreach (var group in candidates
                     .GroupBy(item => item.Candidate.NormalizedKey, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() >= 2))
        {
            var items = group.ToList();
            var distinctDirectories = items
                .Select(item => item.Context.File.RelativeDirectoryPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            var distinctExtensions = items
                .Select(item => item.Context.File.Extension)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            if (distinctDirectories < 2 && distinctExtensions < 2)
            {
                continue;
            }

            var canonicalLabel = items
                .Select(item => item.Candidate.DisplayLabel)
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .GroupBy(label => label, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(grouping => grouping.Count())
                .ThenByDescending(grouping => grouping.Key.Length)
                .Select(grouping => grouping.Key)
                .FirstOrDefault()
                ?? items[0].Candidate.DisplayLabel;

            foreach (var item in items)
            {
                item.Context.Insight = WithProjectTopic(item.Context.Insight, canonicalLabel);
                if (!item.Context.PlanningGroupKey.StartsWith("atomic::", StringComparison.OrdinalIgnoreCase) &&
                    !item.Context.PlanningGroupKey.StartsWith("basename::", StringComparison.OrdinalIgnoreCase))
                {
                    item.Context.PlanningGroupKey = $"project-cluster::{group.Key}";
                    item.Context.PlanningGroupLeaderKey = item.Context.PlanningGroupKey;
                }
            }
        }
    }

    private static ClusterCandidate ResolveClusterCandidate(FileAnalysisContext context)
    {
        var topic = FirstNonEmpty(
            ExtractTopicFromContent(context.Content.Text),
            context.Insight.ProjectOrTopic,
            ExtractTopicFromSuggestedFolder(context.Insight.SuggestedFolderFragment),
            ExtractTopicFromRelativeDirectory(context.File.RelativeDirectoryPath));

        var normalizedKey = NormalizeKey(topic);
        return new ClusterCandidate(normalizedKey, NormalizeDisplayLabel(topic));
    }

    private static SemanticInsight WithProjectTopic(SemanticInsight insight, string canonicalLabel)
    {
        var suggestedFolderFragment = string.IsNullOrWhiteSpace(canonicalLabel)
            ? insight.SuggestedFolderFragment
            : string.IsNullOrWhiteSpace(insight.CategoryKey) || string.Equals(insight.CategoryKey, "uncategorized", StringComparison.OrdinalIgnoreCase)
                ? canonicalLabel
                : $"{insight.CategoryKey}/{canonicalLabel}";

        return new SemanticInsight
        {
            CategoryKey = insight.CategoryKey,
            OriginalCategoryLabel = insight.OriginalCategoryLabel,
            ProjectOrTopic = canonicalLabel,
            LanguageContext = insight.LanguageContext,
            Confidence = insight.Confidence,
            SuggestedFolderFragment = suggestedFolderFragment,
            Explanation = insight.Explanation,
            ClassificationMethod = insight.ClassificationMethod,
            GeminiUsed = insight.GeminiUsed,
            EvidenceKeywords = insight.EvidenceKeywords
        };
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string ExtractTopicFromSuggestedFolder(string suggestedFolderFragment)
    {
        if (string.IsNullOrWhiteSpace(suggestedFolderFragment))
        {
            return string.Empty;
        }

        var segments = suggestedFolderFragment
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return segments.Length >= 2 ? segments[1] : segments.FirstOrDefault() ?? string.Empty;
    }

    private static string ExtractTopicFromContent(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var match = ProjectPhraseRegex().Match(text);
        if (!match.Success)
        {
            return string.Empty;
        }

        var prefix = NormalizeDisplayLabel(match.Groups["prefix"].Value);
        var topic = NormalizeDisplayLabel(match.Groups["topic"].Value);
        return string.IsNullOrWhiteSpace(prefix) || string.IsNullOrWhiteSpace(topic)
            ? topic
            : $"{prefix} {topic}";
    }

    private static string ExtractTopicFromRelativeDirectory(string relativeDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(relativeDirectoryPath))
        {
            return string.Empty;
        }

        var segments = relativeDirectoryPath
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Reverse();

        foreach (var segment in segments)
        {
            var normalized = NormalizeKey(segment);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return NormalizeDisplayLabel(segment);
            }
        }

        return string.Empty;
    }

    private static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var tokens = TokenRegex()
            .Matches(value)
            .Select(match => match.Value.ToLowerInvariant())
            .Where(token => token.Length >= 3 && !GenericTokens.Contains(token))
            .ToList();

        return tokens.Count == 0 ? string.Empty : string.Join("-", tokens);
    }

    private static string NormalizeDisplayLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var compact = MultiWhitespaceRegex().Replace(value, " ").Trim();
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(compact);
    }

    private readonly record struct ClusterCandidate(string NormalizedKey, string DisplayLabel);

    [GeneratedRegex(@"(?<prefix>project|projekt)\s+(?<topic>[\p{L}\p{N}\-_/]{3,40})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ProjectPhraseRegex();

    [GeneratedRegex(@"[\p{L}\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiWhitespaceRegex();
}
