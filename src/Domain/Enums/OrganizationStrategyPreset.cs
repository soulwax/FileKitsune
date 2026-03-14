namespace FileTransformer.Domain.Enums;

public enum OrganizationStrategyPreset
{
    ManualCustom = 0,
    SemanticCategoryFirst = 1,
    ProjectFirst = 2,
    DateFirst = 3,
    HybridProjectDate = 4,
    ArchiveCleanup = 5,
    WorkDocuments = 6,
    ResearchLibrary = 7
}
