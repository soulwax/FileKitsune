using FileTransformer.Domain.Enums;

namespace FileTransformer.Domain.Models;

public sealed class OrganizationPolicy
{
    public OrganizationStrategyPreset StrategyPreset { get; set; } = OrganizationStrategyPreset.SemanticCategoryFirst;

    public OrganizationDimension ManualDimensions { get; set; } =
        OrganizationDimension.SemanticCategory |
        OrganizationDimension.Project |
        OrganizationDimension.Year;

    public bool UseFileTypeAsSecondaryCriterion { get; set; } = true;

    public int MaximumFolderDepth { get; set; } = 4;

    public bool MergeSparseCategories { get; set; }

    public string MiscellaneousBucketName { get; set; } = "Misc";

    public int SparseCategoryThreshold { get; set; } = 2;

    public bool OnlyCreateDateFoldersWhenReliable { get; set; } = true;

    public DateSourceKind PreferredDateSource { get; set; } = DateSourceKind.ModifiedTime;

    public bool PreferGeminiFolderSuggestion { get; set; } = true;
}
