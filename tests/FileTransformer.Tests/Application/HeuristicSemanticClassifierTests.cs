using FileTransformer.Application.Services;
using FileTransformer.Domain.Models;
using Xunit;

namespace FileTransformer.Tests.Application;

public sealed class HeuristicSemanticClassifierTests
{
    [Fact]
    public async Task ClassifyAsync_TreatsCommonEbookExtensionsAsResearchSignals()
    {
        var classifier = new HeuristicSemanticClassifier();

        var insight = await classifier.ClassifyAsync(
            new SemanticAnalysisRequest
            {
                File = new ScannedFile
                {
                    FullPath = @"C:\Root\library\reader-export.azw3",
                    RelativePath = @"library\reader-export.azw3",
                    RelativeDirectoryPath = "library",
                    FileName = "reader-export.azw3",
                    Extension = ".azw3"
                },
                Content = new FileContentSnapshot
                {
                    ExtractionSource = "metadata-only",
                    IsTextReadable = false
                }
            },
            new OrganizationSettings(),
            CancellationToken.None);

        Assert.Equal("research", insight.CategoryKey);
        Assert.Contains("azw3", insight.EvidenceKeywords, StringComparer.OrdinalIgnoreCase);
    }
}
