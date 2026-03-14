using FileTransformer.Domain.Enums;

namespace FileTransformer.Domain.Models;

public sealed class SemanticInsight
{
    public string CategoryKey { get; init; } = "uncategorized";

    public string OriginalCategoryLabel { get; init; } = "Uncategorized";

    public string ProjectOrTopic { get; init; } = string.Empty;

    public DetectedLanguageContext LanguageContext { get; init; } = DetectedLanguageContext.Unclear;

    public double Confidence { get; init; }

    public string SuggestedFolderFragment { get; init; } = string.Empty;

    public string Explanation { get; init; } = string.Empty;

    public ClassificationMethod ClassificationMethod { get; init; } = ClassificationMethod.Heuristic;

    public bool GeminiUsed { get; init; }

    public List<string> EvidenceKeywords { get; init; } = [];
}
