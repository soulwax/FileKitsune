using FileTransformer.Application.Abstractions;
using FileTransformer.Application.Models;
using FileTransformer.Application.Services;
using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FileTransformer.Tests.Application;

public sealed class OrganizationWorkflowServiceTests
{
    [Fact]
    public async Task BuildPlanAsync_CreatesPreviewPlanSummary()
    {
        var scanner = new FakeFileScanner(
        [
            new ScannedFile
            {
                FullPath = @"C:\Root\invoice-rechnung-2025.txt",
                RelativePath = "invoice-rechnung-2025.txt",
                RelativeDirectoryPath = string.Empty,
                FileName = "invoice-rechnung-2025.txt",
                Extension = ".txt",
                ModifiedUtc = new DateTimeOffset(2025, 02, 01, 8, 0, 0, TimeSpan.Zero)
            }
        ]);

        var reader = new FakeContentReader(new FileContentSnapshot
        {
            Text = "Rechnung invoice payment project alpha",
            IsTextReadable = true,
            ExtractionSource = "text"
        });

        var coordinator = new SemanticClassifierCoordinator(
            new HeuristicSemanticClassifier(),
            new NullGeminiClassifier(),
            NullLogger<SemanticClassifierCoordinator>.Instance);

        var service = new OrganizationWorkflowService(
            scanner,
            reader,
            coordinator,
            new DateResolutionService(),
            new DuplicateDetectionService(new NullFileHashProvider(), NullLogger<DuplicateDetectionService>.Instance),
            new ProtectionPolicyService(),
            new DestinationPathBuilder(new NamingPolicyService(), new ReviewDecisionService()),
            new PathSafetyService(),
            NullLogger<OrganizationWorkflowService>.Instance);

        var plan = await service.BuildPlanAsync(
            new AppSettings
            {
                Organization = new OrganizationSettings
                {
                    RootDirectory = @"C:\Root",
                    PreviewSampleSize = 10,
                    Dimensions = OrganizationDimension.SemanticCategory | OrganizationDimension.Year
                },
                Gemini = new GeminiOptions
                {
                    Enabled = false
                }
            },
            progress: null,
            CancellationToken.None);

        Assert.Single(plan.Operations);
        Assert.Equal(1, plan.Summary.TotalItems);
        Assert.Contains("invoice", plan.Operations[0].Reason, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeFileScanner : IFileScanner
    {
        private readonly IReadOnlyList<ScannedFile> files;

        public FakeFileScanner(IReadOnlyList<ScannedFile> files)
        {
            this.files = files;
        }

        public Task<IReadOnlyList<ScannedFile>> ScanAsync(
            OrganizationSettings settings,
            IProgress<WorkflowProgress>? progress,
            CancellationToken cancellationToken) => Task.FromResult(files);
    }

    private sealed class FakeContentReader : IFileContentReader
    {
        private readonly FileContentSnapshot snapshot;

        public FakeContentReader(FileContentSnapshot snapshot)
        {
            this.snapshot = snapshot;
        }

        public Task<FileContentSnapshot> ReadAsync(
            ScannedFile file,
            OrganizationSettings settings,
            CancellationToken cancellationToken) => Task.FromResult(snapshot);
    }

    private sealed class NullGeminiClassifier : IGeminiSemanticClassifier
    {
        public Task<SemanticInsight?> ClassifyAsync(
            SemanticAnalysisRequest request,
            GeminiOptions options,
            CancellationToken cancellationToken) => Task.FromResult<SemanticInsight?>(null);
    }

    private sealed class NullFileHashProvider : IFileHashProvider
    {
        public Task<string> ComputeHashAsync(string fullPath, CancellationToken cancellationToken) =>
            Task.FromResult(string.Empty);
    }
}
