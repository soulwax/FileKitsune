using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;

namespace FileTransformer.Domain.Services;

public static class StrategyPresetCatalog
{
    private static readonly IReadOnlyDictionary<OrganizationStrategyPreset, StrategyPresetDefinition> Definitions =
        new Dictionary<OrganizationStrategyPreset, StrategyPresetDefinition>
        {
            [OrganizationStrategyPreset.ManualCustom] = new()
            {
                Preset = OrganizationStrategyPreset.ManualCustom,
                DisplayName = "Manual custom",
                SegmentOrder = []
            },
            [OrganizationStrategyPreset.SemanticCategoryFirst] = new()
            {
                Preset = OrganizationStrategyPreset.SemanticCategoryFirst,
                DisplayName = "Semantic category first",
                SegmentOrder =
                [
                    PathSegmentKind.Category,
                    PathSegmentKind.FileType
                ]
            },
            [OrganizationStrategyPreset.ProjectFirst] = new()
            {
                Preset = OrganizationStrategyPreset.ProjectFirst,
                DisplayName = "Project first",
                SegmentOrder =
                [
                    PathSegmentKind.Project,
                    PathSegmentKind.Category
                ]
            },
            [OrganizationStrategyPreset.DateFirst] = new()
            {
                Preset = OrganizationStrategyPreset.DateFirst,
                DisplayName = "Date first",
                SegmentOrder =
                [
                    PathSegmentKind.Year,
                    PathSegmentKind.Month,
                    PathSegmentKind.Category
                ]
            },
            [OrganizationStrategyPreset.HybridProjectDate] = new()
            {
                Preset = OrganizationStrategyPreset.HybridProjectDate,
                DisplayName = "Hybrid project and date",
                SegmentOrder =
                [
                    PathSegmentKind.Project,
                    PathSegmentKind.Year,
                    PathSegmentKind.Month,
                    PathSegmentKind.FileType
                ]
            },
            [OrganizationStrategyPreset.ArchiveCleanup] = new()
            {
                Preset = OrganizationStrategyPreset.ArchiveCleanup,
                DisplayName = "Archive cleanup",
                SegmentOrder =
                [
                    PathSegmentKind.Category,
                    PathSegmentKind.Year
                ],
                ConservativeMoves = true,
                ConservativeRenaming = true,
                ReviewLowConfidenceByDefault = true
            },
            [OrganizationStrategyPreset.WorkDocuments] = new()
            {
                Preset = OrganizationStrategyPreset.WorkDocuments,
                DisplayName = "Work documents",
                SegmentOrder =
                [
                    PathSegmentKind.Project,
                    PathSegmentKind.Category,
                    PathSegmentKind.Year
                ]
            },
            [OrganizationStrategyPreset.ResearchLibrary] = new()
            {
                Preset = OrganizationStrategyPreset.ResearchLibrary,
                DisplayName = "Research library",
                SegmentOrder =
                [
                    PathSegmentKind.Project,
                    PathSegmentKind.Year,
                    PathSegmentKind.Category
                ]
            }
        };

    public static StrategyPresetDefinition Resolve(OrganizationPolicy policy)
    {
        if (policy.StrategyPreset == OrganizationStrategyPreset.ManualCustom)
        {
            return new StrategyPresetDefinition
            {
                Preset = OrganizationStrategyPreset.ManualCustom,
                DisplayName = "Manual custom",
                SegmentOrder = BuildManualOrder(policy)
            };
        }

        return Definitions[policy.StrategyPreset];
    }

    public static IReadOnlyList<StrategyPresetDefinition> All =>
        Definitions.Values.OrderBy(definition => definition.Preset).ToList();

    private static IReadOnlyList<PathSegmentKind> BuildManualOrder(OrganizationPolicy policy)
    {
        var items = new List<PathSegmentKind>();

        if (policy.ManualDimensions.HasFlag(OrganizationDimension.SemanticCategory))
        {
            items.Add(PathSegmentKind.Category);
        }

        if (policy.ManualDimensions.HasFlag(OrganizationDimension.Project))
        {
            items.Add(PathSegmentKind.Project);
        }

        if (policy.ManualDimensions.HasFlag(OrganizationDimension.Year))
        {
            items.Add(PathSegmentKind.Year);
        }

        if (policy.ManualDimensions.HasFlag(OrganizationDimension.Month))
        {
            items.Add(PathSegmentKind.Month);
        }

        if (policy.UseFileTypeAsSecondaryCriterion || policy.ManualDimensions.HasFlag(OrganizationDimension.FileType))
        {
            items.Add(PathSegmentKind.FileType);
        }

        return items;
    }
}
