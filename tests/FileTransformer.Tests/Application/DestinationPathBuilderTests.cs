using FileTransformer.Application.Services;
using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;
using Xunit;

namespace FileTransformer.Tests.Application;

public sealed class DestinationPathBuilderTests
{
    [Fact]
    public void Build_LowConfidenceMixedLanguage_BecomesSuggestOnly()
    {
        var builder = new DestinationPathBuilder();
        var safety = new PathSafetyService();
        var settings = new OrganizationSettings
        {
            RootDirectory = @"C:\Root",
            FileRenameMode = FileRenameMode.SuggestCleanNames,
            LowConfidenceThreshold = 0.60d
        };

        var operation = builder.Build(
            new ScannedFile
            {
                RelativePath = "mixed notes.docx",
                RelativeDirectoryPath = string.Empty,
                FileName = "mixed notes.docx",
                Extension = ".docx",
                ModifiedUtc = new DateTimeOffset(2025, 01, 18, 10, 0, 0, TimeSpan.Zero)
            },
            new SemanticInsight
            {
                CategoryKey = "research",
                OriginalCategoryLabel = "Research",
                ProjectOrTopic = "Alpha Beta",
                LanguageContext = DetectedLanguageContext.Mixed,
                Confidence = 0.42d,
                Explanation = "Mixed-language content needs review."
            },
            settings,
            safety);

        Assert.False(operation.AllowedToExecute);
        Assert.True(operation.RequiresReview);
        Assert.Contains("Low semantic confidence", operation.WarningFlags);
        Assert.Contains("Mixed or unclear language context", operation.WarningFlags);
    }
}
