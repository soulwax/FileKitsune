using System.Text;
using FileTransformer.Domain.Models;

namespace FileTransformer.Infrastructure.Classification;

public sealed class GeminiPromptBuilder
{
    public string BuildPrompt(SemanticAnalysisRequest request, int maxPromptCharacters)
    {
        var content = request.Content.Text;
        if (content.Length > maxPromptCharacters)
        {
            content = content[..maxPromptCharacters];
        }

        var builder = new StringBuilder();
        builder.AppendLine("You classify files for a Windows desktop file organizer.");
        builder.AppendLine("Inputs may be German, English, or mixed German-English (including Denglisch).");
        builder.AppendLine("Do not assume exactly one language per file. Focus on semantic intent, not language purity.");
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
        builder.AppendLine(content);
        return builder.ToString();
    }
}
