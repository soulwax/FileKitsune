using System.Text.Json;
using System.Text.RegularExpressions;
using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;

namespace FileTransformer.Infrastructure.Classification;

public sealed partial class GeminiResponseParser
{
    public SemanticInsight ParseApiResponse(string responseJson)
    {
        using var document = JsonDocument.Parse(responseJson);
        var candidateText = document.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(candidateText))
        {
            throw new InvalidOperationException("Gemini returned an empty candidate payload.");
        }

        return ParseModelPayload(candidateText);
    }

    public SemanticInsight ParseModelPayload(string modelJson)
    {
        using var document = JsonDocument.Parse(modelJson);
        var root = document.RootElement;

        var category = NormalizeCategory(ReadRequiredString(root, "category"));
        var project = ReadOptionalString(root, "projectTopic");
        var languageContext = ParseLanguageContext(ReadRequiredString(root, "detectedLanguageContext"));
        var confidence = root.GetProperty("confidence").GetDouble();
        var suggestedFolderPathFragment = ReadOptionalString(root, "suggestedFolderPathFragment");
        var explanation = ReadRequiredString(root, "explanation");

        if (confidence is < 0 or > 1)
        {
            throw new InvalidOperationException("Gemini confidence must be between 0 and 1.");
        }

        if (explanation.Length > 240)
        {
            throw new InvalidOperationException("Gemini explanation is too long.");
        }

        return new SemanticInsight
        {
            CategoryKey = category,
            OriginalCategoryLabel = ToReadableLabel(category),
            ProjectOrTopic = SanitizeFreeText(project),
            LanguageContext = languageContext,
            Confidence = confidence,
            SuggestedFolderFragment = SanitizeFolderFragment(suggestedFolderPathFragment),
            Explanation = explanation.Trim(),
            ClassificationMethod = ClassificationMethod.Gemini,
            GeminiUsed = true
        };
    }

    private static string ReadRequiredString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Missing required Gemini property '{propertyName}'.");
        }

        var text = value.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException($"Gemini property '{propertyName}' is empty.");
        }

        return text;
    }

    private static string ReadOptionalString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return string.Empty;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() ?? string.Empty : string.Empty;
    }

    private static string NormalizeCategory(string category)
    {
        var normalized = Regex.Replace(category.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-");
        normalized = normalized.Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "uncategorized" : normalized;
    }

    private static string SanitizeFreeText(string value) => MultiWhitespaceRegex().Replace(value, " ").Trim();

    private static string SanitizeFolderFragment(string value)
    {
        var sanitized = value.Replace('\\', '/');
        sanitized = MultiWhitespaceRegex().Replace(sanitized, " ").Trim().Trim('/');
        return sanitized.Length > 120 ? sanitized[..120] : sanitized;
    }

    private static string ToReadableLabel(string category) => category.Replace('-', ' ');

    private static DetectedLanguageContext ParseLanguageContext(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "german" => DetectedLanguageContext.German,
            "english" => DetectedLanguageContext.English,
            "mixed" => DetectedLanguageContext.Mixed,
            "unclear" => DetectedLanguageContext.Unclear,
            _ => throw new InvalidOperationException("Gemini returned an unsupported language context.")
        };

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiWhitespaceRegex();
}
