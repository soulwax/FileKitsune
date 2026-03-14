using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;
using FileTransformer.Domain.Services;

namespace FileTransformer.Application.Services;

public sealed partial class NamingPolicyService
{
    private static readonly HashSet<string> NoiseTokens =
    [
        "copy",
        "final",
        "neu",
        "scan",
        "img",
        "version",
        "ver",
        "draft",
        "entwurf"
    ];

    private static readonly HashSet<string> DomainTerms =
    [
        "rechnung",
        "invoice",
        "vertrag",
        "contract",
        "befund",
        "labor",
        "steuer",
        "tax",
        "mix",
        "master",
        "track",
        "session",
        "projekt",
        "project",
        "semester",
        "seminar"
    ];

    public string BuildFileName(
        ScannedFile file,
        SemanticInsight insight,
        DateResolution dateResolution,
        OrganizationSettings settings,
        bool forceConservativeRename)
    {
        var naming = settings.NamingPolicy;
        if (!naming.AllowFileRename || naming.FileRenameMode == FileRenameMode.KeepOriginal)
        {
            return file.FileName;
        }

        var extension = Path.GetExtension(file.FileName);
        var baseName = Path.GetFileNameWithoutExtension(file.FileName);
        var normalized = NormalizeBaseName(baseName, naming);

        if (naming.FileRenameMode == FileRenameMode.NormalizeWhitespaceAndPunctuation || forceConservativeRename)
        {
            return FinalizeFileName(normalized, extension, dateResolution, naming);
        }

        var candidate = naming.NamingTemplate;
        var categoryLabel = ResolveCategoryToken(insight, naming);
        var projectLabel = NormalizeToken(insight.ProjectOrTopic, naming);
        var tokenMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{originalName}"] = normalized,
            ["{category}"] = categoryLabel,
            ["{project}"] = projectLabel,
            ["{date}"] = dateResolution.Value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty
        };

        foreach (var (token, value) in tokenMap)
        {
            candidate = candidate.Replace(token, value, StringComparison.OrdinalIgnoreCase);
        }

        candidate = NormalizeToken(candidate, naming);
        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = normalized;
        }

        return FinalizeFileName(candidate, extension, dateResolution, naming);
    }

    private static string ResolveCategoryToken(SemanticInsight insight, NamingPolicy naming) =>
        naming.FilenameLanguagePolicy switch
        {
            FilenameLanguagePolicy.PreferGerman => NormalizeToken(
                SemanticCatalog.ResolveDisplayName(insight.CategoryKey, FolderLanguageMode.NormalizeToGerman, insight.OriginalCategoryLabel),
                naming),
            FilenameLanguagePolicy.PreferEnglish => NormalizeToken(
                SemanticCatalog.ResolveDisplayName(insight.CategoryKey, FolderLanguageMode.NormalizeToEnglish, insight.OriginalCategoryLabel),
                naming),
            FilenameLanguagePolicy.FolderLanguageOnly => string.Empty,
            _ => NormalizeToken(insight.OriginalCategoryLabel, naming)
        };

    private static string FinalizeFileName(
        string candidate,
        string extension,
        DateResolution dateResolution,
        NamingPolicy naming)
    {
        var finalBaseName = candidate;

        if (naming.AddDatePrefix && dateResolution.Value is not null && dateResolution.IsReliable)
        {
            finalBaseName = $"{dateResolution.Value:yyyy-MM-dd}_{finalBaseName}";
        }

        finalBaseName = NormalizeToken(finalBaseName, naming);

        var maxBaseLength = Math.Max(16, naming.MaximumFileNameLength - extension.Length);
        if (finalBaseName.Length > maxBaseLength)
        {
            finalBaseName = finalBaseName[..maxBaseLength].Trim();
        }

        return $"{finalBaseName}{extension}";
    }

    private static string NormalizeBaseName(string value, NamingPolicy naming)
    {
        var normalized = NormalizeToken(value, naming);

        if (!naming.CleanupNoiseTokens)
        {
            return normalized;
        }

        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var filtered = parts.Where(part =>
        {
            var token = part.Trim().Trim('_', '-', '.', '(', ')').ToLowerInvariant();
            if (naming.PreserveDomainSpecificTerms && DomainTerms.Contains(token))
            {
                return true;
            }

            return !NoiseTokens.Contains(token);
        });

        var cleaned = string.Join(' ', filtered);
        return string.IsNullOrWhiteSpace(cleaned) ? normalized : cleaned;
    }

    private static string NormalizeToken(string value, NamingPolicy naming)
    {
        var token = MultiSeparatorRegex().Replace(value.Normalize(NormalizationForm.FormC), " ").Trim();
        token = WindowsPathRules.SanitizePathSegment(token);

        if (!naming.PreserveUmlauts)
        {
            token = token
                .Replace("ä", "ae", StringComparison.Ordinal)
                .Replace("ö", "oe", StringComparison.Ordinal)
                .Replace("ü", "ue", StringComparison.Ordinal)
                .Replace("Ä", "Ae", StringComparison.Ordinal)
                .Replace("Ö", "Oe", StringComparison.Ordinal)
                .Replace("Ü", "Ue", StringComparison.Ordinal)
                .Replace("ß", "ss", StringComparison.Ordinal);
        }

        return token;
    }

    [GeneratedRegex(@"[\s\-_]+")]
    private static partial Regex MultiSeparatorRegex();
}
