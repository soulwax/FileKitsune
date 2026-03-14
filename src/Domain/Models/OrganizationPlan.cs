namespace FileTransformer.Domain.Models;

public sealed class OrganizationPlan
{
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public required OrganizationSettings Settings { get; init; }

    public required IReadOnlyList<PlanOperation> Operations { get; init; }

    public required PlanSummary Summary { get; init; }
}
