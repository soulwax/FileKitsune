using FileTransformer.Domain.Enums;

namespace FileTransformer.Domain.Models;

public sealed class OrganizationPlan
{
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public required OrganizationSettings Settings { get; init; }

    public OrganizationStrategyPreset StrategyPreset { get; init; } = OrganizationStrategyPreset.ManualCustom;

    public required IReadOnlyList<PlanOperation> Operations { get; init; }

    public required PlanSummary Summary { get; init; }
}
