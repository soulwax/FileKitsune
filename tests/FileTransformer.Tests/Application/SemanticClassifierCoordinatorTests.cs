using FileTransformer.Application.Abstractions;
using FileTransformer.Application.Models;
using FileTransformer.Application.Services;
using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FileTransformer.Tests.Application;

public sealed class SemanticClassifierCoordinatorTests
{
    [Fact]
    public async Task ClassifyAsync_FallsBackToHeuristicsWhenGeminiThrows()
    {
        var coordinator = new SemanticClassifierCoordinator(
            new HeuristicSemanticClassifier(),
            new ThrowingGeminiClassifier(),
            NullLogger<SemanticClassifierCoordinator>.Instance);

        var request = CreateRequest("rechnung invoice project alpha");
        var settings = CreateSettings();
        var gemini = CreateGeminiOptions();

        var insight = await coordinator.ClassifyAsync(request, settings, gemini, CancellationToken.None);

        Assert.False(insight.GeminiUsed);
        Assert.NotEqual(ClassificationMethod.Hybrid, insight.ClassificationMethod);
        Assert.Contains("invoice", insight.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClassifyAsync_FallsBackToHeuristicsWhenGeminiReturnsNull()
    {
        var coordinator = new SemanticClassifierCoordinator(
            new HeuristicSemanticClassifier(),
            new NullGeminiClassifier(),
            NullLogger<SemanticClassifierCoordinator>.Instance);

        var request = CreateRequest("meeting notes for project alpha");
        var settings = CreateSettings();
        var gemini = CreateGeminiOptions();

        var insight = await coordinator.ClassifyAsync(request, settings, gemini, CancellationToken.None);

        Assert.False(insight.GeminiUsed);
        Assert.NotEqual(ClassificationMethod.Hybrid, insight.ClassificationMethod);
    }

    [Fact]
    public async Task ClassifyAsync_UsesGeminiFieldsWhenGeminiReturnsUsableInsight()
    {
        var coordinator = new SemanticClassifierCoordinator(
            new HeuristicSemanticClassifier(),
            new FixedGeminiClassifier(new SemanticInsight
            {
                CategoryKey = "research",
                OriginalCategoryLabel = "Research",
                ProjectOrTopic = "Project Atlas",
                LanguageContext = DetectedLanguageContext.Mixed,
                Confidence = 0.91,
                SuggestedFolderFragment = "Projekt/Atlas",
                Explanation = "Gemini found consistent project signals.",
                ClassificationMethod = ClassificationMethod.Gemini,
                GeminiUsed = true
            }),
            NullLogger<SemanticClassifierCoordinator>.Instance);

        var request = CreateRequest("notes and planning for atlas");
        var settings = CreateSettings();
        var gemini = CreateGeminiOptions();

        var insight = await coordinator.ClassifyAsync(request, settings, gemini, CancellationToken.None);

        Assert.True(insight.GeminiUsed);
        Assert.Equal(ClassificationMethod.Hybrid, insight.ClassificationMethod);
        Assert.Equal("Project Atlas", insight.ProjectOrTopic);
        Assert.Equal("Projekt/Atlas", insight.SuggestedFolderFragment);
        Assert.Contains("Gemini:", insight.Explanation, StringComparison.Ordinal);
    }

    private static SemanticAnalysisRequest CreateRequest(string text) =>
        new()
        {
            File = new ScannedFile
            {
                FullPath = @"C:\Root\sample.txt",
                RelativePath = "sample.txt",
                FileName = "sample.txt",
                Extension = ".txt"
            },
            Content = new FileContentSnapshot
            {
                Text = text,
                IsTextReadable = true,
                ExtractionSource = "text"
            }
        };

    private static OrganizationSettings CreateSettings() =>
        new()
        {
            UseGeminiWhenAvailable = true,
            ReviewPolicy = new ReviewPolicy
            {
                ExecutionMode = ExecutionMode.HeuristicsPlusGeminiReviewFirst
            }
        };

    private static GeminiOptions CreateGeminiOptions() =>
        new()
        {
            Enabled = true,
            ApiKey = "test-key"
        };

    private sealed class ThrowingGeminiClassifier : IGeminiSemanticClassifier
    {
        public Task<SemanticInsight?> ClassifyAsync(
            SemanticAnalysisRequest request,
            GeminiOptions options,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Malformed Gemini payload.");
    }

    private sealed class NullGeminiClassifier : IGeminiSemanticClassifier
    {
        public Task<SemanticInsight?> ClassifyAsync(
            SemanticAnalysisRequest request,
            GeminiOptions options,
            CancellationToken cancellationToken) =>
            Task.FromResult<SemanticInsight?>(null);
    }

    private sealed class FixedGeminiClassifier : IGeminiSemanticClassifier
    {
        private readonly SemanticInsight insight;

        public FixedGeminiClassifier(SemanticInsight insight)
        {
            this.insight = insight;
        }

        public Task<SemanticInsight?> ClassifyAsync(
            SemanticAnalysisRequest request,
            GeminiOptions options,
            CancellationToken cancellationToken) =>
            Task.FromResult<SemanticInsight?>(insight);
    }
}
