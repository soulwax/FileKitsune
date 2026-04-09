using FileTransformer.Domain.Enums;

namespace FileTransformer.Application.Models;

public sealed class StrategyRecommendation
{
    public OrganizationStrategyPreset Preset { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public double Confidence { get; init; }
}
