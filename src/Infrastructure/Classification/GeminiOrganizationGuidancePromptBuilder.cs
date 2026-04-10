using System.Text;
using FileTransformer.Application.Models;
using FileTransformer.Domain.Models;

namespace FileTransformer.Infrastructure.Classification;

public sealed class GeminiOrganizationGuidancePromptBuilder
{
    public string BuildPrompt(
        IReadOnlyList<FileAnalysisContext> contexts,
        OrganizationSettings settings,
        int maxPromptCharacters)
    {
        var builder = new StringBuilder();
        builder.AppendLine("You advise a Windows desktop file organizer.");
        builder.AppendLine("You are advisory only. Do not propose final file paths.");
        builder.AppendLine("Recommend a strategy preset and whether the folder structure should be shallower, deeper, or balanced.");
        builder.AppendLine("Prefer German-first organization when the evidence is mixed.");
        builder.AppendLine("Return ONLY a JSON object with these fields:");
        builder.AppendLine("preferredStrategyPreset, structureBias, suggestedMaxDepth, reasoning");
        builder.AppendLine("preferredStrategyPreset must be one of: SemanticCategoryFirst, ProjectFirst, DateFirst, HybridProjectDate, ArchiveCleanup, WorkDocuments, ResearchLibrary, ManualCustom");
        builder.AppendLine("structureBias must be one of: Shallower, Deeper, Balanced");
        builder.AppendLine("suggestedMaxDepth must be an integer from 2 to 5.");
        builder.AppendLine("reasoning must be concise.");
        builder.AppendLine();
        builder.AppendLine("Current local settings:");
        builder.AppendLine($"- Strategy preset: {settings.StrategyPreset}");
        builder.AppendLine($"- Preferred date source: {settings.PreferredDateSource}");
        builder.AppendLine($"- Folder language mode: {settings.FolderLanguageMode}");
        builder.AppendLine($"- Current maximum folder depth: {settings.OrganizationPolicy.MaximumFolderDepth}");
        builder.AppendLine();
        builder.AppendLine("Sampled file evidence:");

        foreach (var context in contexts.Take(12))
        {
            var snippet = BuildSnippet(context.Content.Text, 140);
            builder.Append("- ");
            builder.Append(context.File.FileName);
            builder.Append(" | ext=");
            builder.Append(context.File.Extension);
            builder.Append(" | dir=");
            builder.Append(context.File.RelativeDirectoryPath);
            builder.Append(" | category=");
            builder.Append(context.Insight.CategoryKey);
            builder.Append(" | project=");
            builder.Append(string.IsNullOrWhiteSpace(context.Insight.ProjectOrTopic) ? "-" : context.Insight.ProjectOrTopic);
            builder.Append(" | conf=");
            builder.Append(context.Insight.Confidence.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            if (!string.IsNullOrWhiteSpace(snippet))
            {
                builder.Append(" | snippet=");
                builder.Append(snippet);
            }

            builder.AppendLine();
        }

        var prompt = builder.ToString();
        return prompt.Length <= maxPromptCharacters
            ? prompt
            : prompt[..maxPromptCharacters];
    }

    private static string BuildSnippet(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var compact = string.Join(' ', text
            .Split(new[] { '\r', '\n', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Take(24));

        return compact.Length <= maxLength
            ? compact
            : compact[..maxLength];
    }
}
