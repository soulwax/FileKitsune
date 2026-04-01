using FileTransformer.Application.Models;
using FileTransformer.Application.Services;
using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;
using FileTransformer.Domain.Services;
using Xunit;

namespace FileTransformer.Tests.Application;

public sealed class DestinationPathBuilderTests
{
    [Fact]
    public void Build_LowConfidenceMixedLanguage_RequiresReviewAndFlagsReason()
    {
        var builder = new DestinationPathBuilder(new NamingPolicyService(), new ReviewDecisionService());
        var safety = new PathSafetyService();
        var settings = new OrganizationSettings
        {
            RootDirectory = @"C:\Root",
            FileRenameMode = FileRenameMode.SuggestCleanNames,
            LowConfidenceThreshold = 0.60d
        };

        var file = new ScannedFile
        {
            RelativePath = "mixed notes.docx",
            RelativeDirectoryPath = string.Empty,
            FileName = "mixed notes.docx",
            Extension = ".docx",
            ModifiedUtc = new DateTimeOffset(2025, 01, 18, 10, 0, 0, TimeSpan.Zero)
        };

        var insight = new SemanticInsight
        {
            CategoryKey = "research",
            OriginalCategoryLabel = "Research",
            ProjectOrTopic = "Alpha Beta",
            LanguageContext = DetectedLanguageContext.Mixed,
            Confidence = 0.42d,
            Explanation = "Mixed-language content needs review."
        };

        var context = new FileAnalysisContext
        {
            File = file,
            Content = new FileContentSnapshot(),
            Insight = insight
        };

        var strategyDefinition = StrategyPresetCatalog.Resolve(new OrganizationPolicy());
        var categoryCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var operation = builder.Build(
            context,
            context,
            settings,
            strategyDefinition,
            categoryCounts,
            safety,
            sharedFolderGroup: false);

        Assert.True(operation.RequiresReview);
        Assert.Contains("Low semantic confidence", operation.ReviewReasons);
    }
}
