using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;

namespace FileTransformer.Application.Services;

public sealed partial class HeuristicSemanticClassifier
{
    // TODO: Blend these keyword heuristics with embeddings/vector search once v1 ships.
    private static readonly Dictionary<string, string[]> CategoryKeywords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["invoices"] = ["rechnung", "rechnungen", "invoice", "billing", "iban", "mwst", "vat", "payment"],
            ["research"] = ["research", "paper", "study", "literature", "forschung", "analyse", "report", "bericht"],
            ["music-projects"] = ["track", "mix", "master", "stem", "session", "musik", "song", "ableton", "logic"],
            ["medical-documents"] = ["befund", "arzt", "medical", "clinic", "patient", "diagnosis", "labor", "report"],
            ["code"] = ["class", "namespace", "using", "function", "code", "projekt", "solution", "build", "api"],
            ["photos"] = ["photo", "image", "jpg", "png", "urlaub", "kamera", "screenshot"],
            ["admin"] = ["admin", "steuer", "tax", "bank", "versicherung", "insurance", "registration"],
            ["contracts"] = ["vertrag", "verträge", "contract", "agreement", "nda", "terms", "lease", "signature"],
            ["teaching"] = ["vorlesung", "lecture", "course", "seminar", "teaching", "exercise", "slides"],
            ["personal-notes"] = ["notes", "note", "journal", "todo", "tagebuch", "memo", "ideas", "ideen"]
        };

    private static readonly HashSet<string> GermanMarkers =
    [
        "und", "für", "mit", "rechnung", "vertrag", "vorlesung", "befund", "über", "größe", "lehre"
    ];

    private static readonly HashSet<string> EnglishMarkers =
    [
        "and", "with", "invoice", "contract", "lecture", "report", "notes", "project", "release"
    ];

    private static readonly HashSet<string> GenericProjectTerms =
    [
        "documents",
        "dokumente",
        "files",
        "dateien",
        "misc",
        "miscellaneous",
        "archive",
        "archiv",
        "desktop",
        "download",
        "downloads"
    ];

    public Task<SemanticInsight> ClassifyAsync(
        SemanticAnalysisRequest request,
        OrganizationSettings settings,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var signalBuilder = new StringBuilder();
        signalBuilder.Append(request.File.FileName);
        signalBuilder.Append(' ');
        signalBuilder.Append(request.File.RelativeDirectoryPath);
        signalBuilder.Append(' ');
        signalBuilder.Append(request.Content.Text);

        var signalText = signalBuilder.ToString().Normalize(NormalizationForm.FormC);
        var normalized = signalText.ToLowerInvariant();
        var tokens = TokenRegex().Matches(normalized).Select(match => match.Value).ToList();

        var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var evidenceByCategory = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (category, keywords) in CategoryKeywords)
        {
            var score = 0;
            var evidence = new List<string>();

            foreach (var keyword in keywords)
            {
                if (normalized.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    score += keyword.Length > 5 ? 2 : 1;
                    evidence.Add(keyword);
                }
            }

            if (category == "code" && IsCodeExtension(request.File.Extension))
            {
                score += 4;
                evidence.Add(request.File.Extension.Trim('.'));
            }

            if (category == "photos" && IsImageExtension(request.File.Extension))
            {
                score += 4;
                evidence.Add(request.File.Extension.Trim('.'));
            }

            if (category == "music-projects" && IsAudioExtension(request.File.Extension))
            {
                score += 4;
                evidence.Add(request.File.Extension.Trim('.'));
            }

            scores[category] = score;
            evidenceByCategory[category] = evidence;
        }

        var bestCategory = scores
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .First();

        var categoryKey = bestCategory.Value == 0 ? "uncategorized" : bestCategory.Key;
        var selectedEvidence = categoryKey == "uncategorized" ? new List<string>() : evidenceByCategory[bestCategory.Key];
        var languageContext = DetectLanguageContext(tokens, normalized);
        var project = ExtractProjectHint(request.File);
        var confidence = CalculateConfidence(bestCategory.Value, request.Content.IsTextReadable, languageContext);
        var explanation = categoryKey == "uncategorized"
            ? "No strong multilingual keyword cluster was found, so the file stayed uncategorized."
            : $"Matched multilingual heuristic signals for '{categoryKey}' using {string.Join(", ", selectedEvidence.Distinct(StringComparer.OrdinalIgnoreCase).Take(4))}.";

        return Task.FromResult(new SemanticInsight
        {
            CategoryKey = categoryKey,
            OriginalCategoryLabel = ToReadableLabel(categoryKey),
            ProjectOrTopic = project,
            LanguageContext = languageContext,
            Confidence = confidence,
            SuggestedFolderFragment = string.IsNullOrWhiteSpace(project) ? categoryKey : $"{categoryKey}/{project}",
            Explanation = explanation,
            ClassificationMethod = ClassificationMethod.Heuristic,
            GeminiUsed = false,
            EvidenceKeywords = selectedEvidence.Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList()
        });
    }

    private static string ExtractProjectHint(ScannedFile file)
    {
        var directorySegments = file.RelativeDirectoryPath
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .Reverse()
            .Select(segment => segment.Trim())
            .Where(segment => segment.Length >= 3 && !GenericProjectTerms.Contains(segment.ToLowerInvariant()))
            .ToList();

        foreach (var segment in directorySegments)
        {
            if (!LooksLikeDate(segment))
            {
                return segment;
            }
        }

        var fileTokens = TokenRegex()
            .Matches(Path.GetFileNameWithoutExtension(file.FileName))
            .Select(match => match.Value)
            .Where(token => token.Length > 2 && !LooksLikeDate(token) && !GenericProjectTerms.Contains(token.ToLowerInvariant()))
            .Take(3)
            .ToList();

        return fileTokens.Count == 0
            ? string.Empty
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(string.Join(' ', fileTokens));
    }

    private static DetectedLanguageContext DetectLanguageContext(List<string> tokens, string normalized)
    {
        var germanScore = tokens.Count(token => GermanMarkers.Contains(token)) + CountUmlauts(normalized);
        var englishScore = tokens.Count(token => EnglishMarkers.Contains(token));

        if (germanScore == 0 && englishScore == 0)
        {
            return DetectedLanguageContext.Unclear;
        }

        if (germanScore > 0 && englishScore > 0)
        {
            return DetectedLanguageContext.Mixed;
        }

        return germanScore > englishScore ? DetectedLanguageContext.German : DetectedLanguageContext.English;
    }

    private static double CalculateConfidence(int score, bool usedContent, DetectedLanguageContext languageContext)
    {
        var baseConfidence = score switch
        {
            >= 8 => 0.92d,
            >= 5 => 0.78d,
            >= 3 => 0.64d,
            >= 1 => 0.48d,
            _ => 0.28d
        };

        if (usedContent)
        {
            baseConfidence += 0.05d;
        }

        if (languageContext == DetectedLanguageContext.Mixed)
        {
            baseConfidence -= 0.08d;
        }

        return Math.Clamp(baseConfidence, 0.1d, 0.98d);
    }

    private static int CountUmlauts(string value)
    {
        var count = 0;
        foreach (var character in value)
        {
            if (character is 'ä' or 'ö' or 'ü' or 'ß')
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsCodeExtension(string extension) =>
        extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".ts", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".js", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".xaml", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".xml", StringComparison.OrdinalIgnoreCase);

    private static bool IsImageExtension(string extension) =>
        extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".heic", StringComparison.OrdinalIgnoreCase);

    private static bool IsAudioExtension(string extension) =>
        extension.Equals(".wav", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".flac", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".als", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeDate(string value) =>
        DateOnly.TryParse(value, out _) ||
        Regex.IsMatch(value, @"^\d{4}([_-]?\d{2}){0,2}$", RegexOptions.CultureInvariant);

    private static string ToReadableLabel(string key) => key.Replace('-', ' ');

    [GeneratedRegex(@"[\p{L}\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();
}
