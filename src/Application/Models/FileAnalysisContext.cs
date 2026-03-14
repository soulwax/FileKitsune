using FileTransformer.Domain.Models;

namespace FileTransformer.Application.Models;

public sealed class FileAnalysisContext
{
    public required ScannedFile File { get; init; }

    public required FileContentSnapshot Content { get; init; }

    public required SemanticInsight Insight { get; set; }

    public DateResolution DateResolution { get; set; } = new();

    public DuplicateMatch DuplicateMatch { get; set; } = new();

    public bool ProtectedByRule { get; set; }

    public string ProtectionReason { get; set; } = string.Empty;

    public string PlanningGroupKey { get; set; } = string.Empty;

    public string PlanningGroupLeaderKey { get; set; } = string.Empty;
}
