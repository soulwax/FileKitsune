using FileTransformer.Domain.Enums;

namespace FileTransformer.Domain.Models;

public sealed class OrganizationGuidance
{
    public bool GeminiUsed { get; init; }

    public OrganizationStrategyPreset PreferredPreset { get; init; } = OrganizationStrategyPreset.ManualCustom;

    public OrganizationStructureBias StructureBias { get; init; } = OrganizationStructureBias.Balanced;

    public int SuggestedMaxDepth { get; init; } = 4;

    public string Reasoning { get; init; } = string.Empty;
}
