using FileTransformer.Application.Services;
using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;
using Xunit;

namespace FileTransformer.Tests.Application;

public sealed class StrategyRecommendationServiceTests
{
    [Fact]
    public void Recommend_ProjectAndDateHeavyPlan_PrefersHybridOrProjectStrategies()
    {
        var service = new StrategyRecommendationService();
        var plan = CreatePlan(
        [
            CreateOperation("Project Atlas", DateSourceKind.ContentDerived, "research", ".docx"),
            CreateOperation("Project Atlas", DateSourceKind.FileName, "research", ".pdf"),
            CreateOperation("Project Atlas", DateSourceKind.ModifiedTime, "teaching", ".md"),
            CreateOperation("Project Atlas", DateSourceKind.ContentDerived, "research", ".txt")
        ]);

        var recommendations = service.Recommend(plan, maxCount: 3);

        Assert.NotEmpty(recommendations);
        Assert.Contains(recommendations.Take(2), recommendation =>
            recommendation.Preset is OrganizationStrategyPreset.HybridProjectDate or OrganizationStrategyPreset.ProjectFirst);
    }

    [Fact]
    public void Recommend_DuplicateAndReviewHeavyPlan_SuggestsArchiveCleanup()
    {
        var service = new StrategyRecommendationService();
        var plan = CreatePlan(
        [
            CreateOperation(string.Empty, DateSourceKind.None, "uncategorized", ".txt", duplicateDetected: true, requiresReview: true, operationType: PlanOperationType.Skip),
            CreateOperation(string.Empty, DateSourceKind.None, "uncategorized", ".jpg", duplicateDetected: true, requiresReview: true, operationType: PlanOperationType.Skip),
            CreateOperation(string.Empty, DateSourceKind.ModifiedTime, "photos", ".png", duplicateDetected: false, requiresReview: true),
            CreateOperation(string.Empty, DateSourceKind.None, "uncategorized", ".zip", duplicateDetected: false, requiresReview: true)
        ]);

        var recommendations = service.Recommend(plan, maxCount: 3);

        Assert.NotEmpty(recommendations);
        Assert.Equal(OrganizationStrategyPreset.ArchiveCleanup, recommendations[0].Preset);
    }

    private static OrganizationPlan CreatePlan(IReadOnlyList<PlanOperation> operations) =>
        new()
        {
            Settings = new OrganizationSettings
            {
                RootDirectory = @"C:\Root"
            },
            Operations = operations,
            Summary = new PlanSummary
            {
                TotalItems = operations.Count,
                DuplicateCount = operations.Count(operation => operation.DuplicateDetected),
                RequiresReviewCount = operations.Count(operation => operation.RequiresReview),
                SkipCount = operations.Count(operation => operation.OperationType == PlanOperationType.Skip)
            }
        };

    private static PlanOperation CreateOperation(
        string projectOrTopic,
        DateSourceKind dateSource,
        string categoryKey,
        string extension,
        bool duplicateDetected = false,
        bool requiresReview = false,
        PlanOperationType operationType = PlanOperationType.Move) =>
        new()
        {
            OperationType = operationType,
            CurrentRelativePath = $"file{Guid.NewGuid():N}{extension}",
            ProposedRelativePath = $"target{Path.DirectorySeparatorChar}file{Guid.NewGuid():N}{extension}",
            CategoryKey = categoryKey,
            CategoryDisplayName = categoryKey,
            ProjectOrTopic = projectOrTopic,
            FileName = $"example{extension}",
            DateSource = dateSource,
            DuplicateDetected = duplicateDetected,
            RequiresReview = requiresReview,
            AllowedToExecute = operationType != PlanOperationType.Skip
        };
}
