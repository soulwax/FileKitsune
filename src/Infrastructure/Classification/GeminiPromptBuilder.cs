using System.Text;
using FileTransformer.Domain.Models;

namespace FileTransformer.Infrastructure.Classification;

public sealed class GeminiPromptBuilder
{
    public string BuildPrompt(SemanticAnalysisRequest request, int maxPromptCharacters)
    {
        var builder = new StringBuilder();
        builder.AppendLine("You classify files for a Windows desktop file organizer.");
        builder.AppendLine("Inputs may be German, English, or mixed German-English (including Denglisch).");
        builder.AppendLine("Do not assume exactly one language per file. Focus on semantic intent, not language purity.");
        builder.AppendLine("Prefer category understanding from the earliest useful paragraphs or pages. Do not ask for more text.");
        builder.AppendLine("Use concise explanations. Do not translate original file contents.");
        builder.AppendLine("Return ONLY a JSON object with these fields:");
        builder.AppendLine("category, projectTopic, detectedLanguageContext, confidence, suggestedFolderPathFragment, explanation");
        builder.AppendLine("detectedLanguageContext must be one of: German, English, Mixed, Unclear");
        builder.AppendLine("confidence must be a number from 0 to 1.");
        builder.AppendLine("suggestedFolderPathFragment is advisory only and may be empty.");
        builder.AppendLine();
        builder.AppendLine("Metadata:");
        builder.AppendLine($"- File name: {request.File.FileName}");
        builder.AppendLine($"- Extension: {request.File.Extension}");
        builder.AppendLine($"- Relative directory: {request.File.RelativeDirectoryPath}");
        builder.AppendLine($"- Modified date: {request.File.ModifiedUtc:yyyy-MM-dd}");
        builder.AppendLine($"- Size bytes: {request.File.SizeBytes}");
        builder.AppendLine();
        builder.AppendLine("Extracted text snippet:");

        var prefix = builder.ToString();
        var contentBudget = Math.Max(0, maxPromptCharacters - prefix.Length);
        if (contentBudget == 0)
        {
            return prefix.Length <= maxPromptCharacters
                ? prefix
                : prefix[..maxPromptCharacters];
        }

        var content = BuildFrontLoadedSnippet(request.Content.Text, contentBudget);
        return $"{prefix}{content}";
    }

    private static string BuildFrontLoadedSnippet(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        var paragraphs = normalized
            .Split(["\n\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(paragraph => string.Join(' ', paragraph
                .Split(['\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries)))
            .Where(paragraph => !string.IsNullOrWhiteSpace(paragraph))
            .Take(3)
            .ToList();

        if (paragraphs.Count == 0)
        {
            var compact = string.Join(' ', normalized
                .Split(['\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries)
                .Take(80));

            return compact.Length <= maxLength
                ? compact
                : compact[..maxLength];
        }

        var snippet = string.Join(Environment.NewLine + Environment.NewLine, paragraphs);
        return snippet.Length <= maxLength
            ? snippet
            : snippet[..maxLength];
    }
}
