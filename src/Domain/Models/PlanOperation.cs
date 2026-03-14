using FileTransformer.Domain.Enums;

namespace FileTransformer.Domain.Models;

public sealed class PlanOperation
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public PlanOperationType OperationType { get; init; }

    public string CurrentRelativePath { get; init; } = string.Empty;

    public string ProposedRelativePath { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public double Confidence { get; init; }

    public RiskLevel RiskLevel { get; init; } = RiskLevel.None;

    public List<string> WarningFlags { get; init; } = [];

    public bool RequiresReview { get; init; }

    public bool AllowedToExecute { get; init; } = true;

    public bool GeminiUsed { get; init; }

    public DetectedLanguageContext LanguageContext { get; init; } = DetectedLanguageContext.Unclear;

    public string CategoryKey { get; init; } = string.Empty;

    public string CategoryDisplayName { get; init; } = string.Empty;

    public string ProjectOrTopic { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;
}
