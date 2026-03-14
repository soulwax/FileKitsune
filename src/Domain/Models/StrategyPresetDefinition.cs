using FileTransformer.Domain.Enums;

namespace FileTransformer.Domain.Models;

public sealed class StrategyPresetDefinition
{
    public required OrganizationStrategyPreset Preset { get; init; }

    public required string DisplayName { get; init; }

    public required IReadOnlyList<PathSegmentKind> SegmentOrder { get; init; }

    public bool ConservativeMoves { get; init; }

    public bool ConservativeRenaming { get; init; }

    public bool ReviewLowConfidenceByDefault { get; init; }
}
