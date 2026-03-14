using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;

namespace FileTransformer.Domain.Services;

public static class SemanticCatalog
{
    private static readonly Dictionary<string, SemanticCategoryDefinition> Categories =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["invoices"] = new() { Key = "invoices", EnglishLabel = "Invoices", GermanLabel = "Rechnungen" },
            ["research"] = new() { Key = "research", EnglishLabel = "Research", GermanLabel = "Forschung" },
            ["music-projects"] = new() { Key = "music-projects", EnglishLabel = "Music Projects", GermanLabel = "Musikprojekte" },
            ["medical-documents"] = new() { Key = "medical-documents", EnglishLabel = "Medical Documents", GermanLabel = "Medizinische Dokumente" },
            ["code"] = new() { Key = "code", EnglishLabel = "Code", GermanLabel = "Code" },
            ["photos"] = new() { Key = "photos", EnglishLabel = "Photos", GermanLabel = "Fotos" },
            ["admin"] = new() { Key = "admin", EnglishLabel = "Admin", GermanLabel = "Verwaltung" },
            ["contracts"] = new() { Key = "contracts", EnglishLabel = "Contracts", GermanLabel = "Vertraege" },
            ["teaching"] = new() { Key = "teaching", EnglishLabel = "Teaching", GermanLabel = "Lehre" },
            ["personal-notes"] = new() { Key = "personal-notes", EnglishLabel = "Personal Notes", GermanLabel = "Persoenliche Notizen" },
            ["uncategorized"] = new() { Key = "uncategorized", EnglishLabel = "Uncategorized", GermanLabel = "Unsortiert" }
        };

    public static IReadOnlyCollection<SemanticCategoryDefinition> All => Categories.Values;

    public static string ResolveDisplayName(
        string categoryKey,
        FolderLanguageMode languageMode,
        string? originalLabel = null)
    {
        if (!Categories.TryGetValue(categoryKey, out var category))
        {
            return string.IsNullOrWhiteSpace(originalLabel) ? "Uncategorized" : originalLabel.Trim();
        }

        return languageMode switch
        {
            FolderLanguageMode.NormalizeToGerman => category.GermanLabel,
            FolderLanguageMode.NormalizeToEnglish => category.EnglishLabel,
            FolderLanguageMode.UseBilingualLabels => $"{category.GermanLabel} - {category.EnglishLabel}",
            _ => string.IsNullOrWhiteSpace(originalLabel) ? category.EnglishLabel : originalLabel.Trim()
        };
    }
}
