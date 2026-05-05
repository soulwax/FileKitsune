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
            new NullGeminiOrganizationAdvisor(),
            new DateResolutionService(),
            new DuplicateDetectionService(new NullFileHashProvider(), NullLogger<DuplicateDetectionService>.Instance),
            new ProjectClusterService(),
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

    [Fact]
    public async Task BuildPlanAsync_ClustersMixedFilesBySharedProjectPhrase()
    {
        var scanner = new FakeFileScanner(
        [
            new ScannedFile
            {
                FullPath = @"C:\Root\incoming\kickoff-notes.txt",
                RelativePath = @"incoming\kickoff-notes.txt",
                RelativeDirectoryPath = "incoming",
                FileName = "kickoff-notes.txt",
                Extension = ".txt",
                ModifiedUtc = new DateTimeOffset(2025, 02, 01, 8, 0, 0, TimeSpan.Zero)
            },
            new ScannedFile
            {
                FullPath = @"C:\Root\archive\budget-overview.pdf",
                RelativePath = @"archive\budget-overview.pdf",
                RelativeDirectoryPath = "archive",
                FileName = "budget-overview.pdf",
                Extension = ".pdf",
                ModifiedUtc = new DateTimeOffset(2025, 02, 02, 8, 0, 0, TimeSpan.Zero)
            }
        ]);

        var reader = new SequencedContentReader(
            new FileContentSnapshot
            {
                Text = "Kickoff summary for Project Atlas.",
                IsTextReadable = true,
                ExtractionSource = "text"
            },
            new FileContentSnapshot
            {
                Text = "Budget overview and invoices for Project Atlas.",
                IsTextReadable = true,
                ExtractionSource = "pdf"
            });

        var coordinator = new SemanticClassifierCoordinator(
            new HeuristicSemanticClassifier(),
            new NullGeminiClassifier(),
            NullLogger<SemanticClassifierCoordinator>.Instance);

        var service = new OrganizationWorkflowService(
            scanner,
            reader,
            coordinator,
            new NullGeminiOrganizationAdvisor(),
            new DateResolutionService(),
            new DuplicateDetectionService(new NullFileHashProvider(), NullLogger<DuplicateDetectionService>.Instance),
            new ProjectClusterService(),
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
                    StrategyPreset = OrganizationStrategyPreset.ProjectFirst
                },
                Gemini = new GeminiOptions
                {
                    Enabled = false
                }
            },
            progress: null,
            CancellationToken.None);

        Assert.Equal(2, plan.Operations.Count);
        Assert.All(plan.Operations, operation => Assert.Equal("Project Atlas", operation.ProjectOrTopic));
        Assert.All(plan.Operations, operation => Assert.Contains("Project Atlas", operation.ProposedRelativePath, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BuildPlanAsync_FallsBackWhenGeminiOrganizationAdvisorThrows()
    {
        var scanner = new FakeFileScanner(
        [
            new ScannedFile
            {
                FullPath = @"C:\Root\incoming\notes.txt",
                RelativePath = @"incoming\notes.txt",
                RelativeDirectoryPath = "incoming",
                FileName = "notes.txt",
                Extension = ".txt",
                ModifiedUtc = new DateTimeOffset(2025, 02, 01, 8, 0, 0, TimeSpan.Zero)
            }
        ]);

        var reader = new FakeContentReader(new FileContentSnapshot
        {
            Text = "Meeting notes for project alpha",
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
            new ThrowingGeminiOrganizationAdvisor(),
            new DateResolutionService(),
            new DuplicateDetectionService(new NullFileHashProvider(), NullLogger<DuplicateDetectionService>.Instance),
            new ProjectClusterService(),
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
                    UseGeminiWhenAvailable = true
                },
                Gemini = new GeminiOptions
                {
                    Enabled = true,
                    ApiKey = "test-key"
                }
            },
            progress: null,
            CancellationToken.None);

        Assert.Single(plan.Operations);
        Assert.Null(plan.Guidance);
    }

    [Fact]
    public async Task BuildPlanAsync_ReportsScanAndPreviewCoverage()
    {
        var scanner = new FakeFileScanner(
        [
            new ScannedFile
            {
                FullPath = @"C:\Root\a.txt",
                RelativePath = "a.txt",
                RelativeDirectoryPath = string.Empty,
                FileName = "a.txt",
                Extension = ".txt",
                ModifiedUtc = new DateTimeOffset(2025, 02, 01, 8, 0, 0, TimeSpan.Zero)
            },
            new ScannedFile
            {
                FullPath = @"C:\Root\b.bin",
                RelativePath = "b.bin",
                RelativeDirectoryPath = string.Empty,
                FileName = "b.bin",
                Extension = ".bin",
                ModifiedUtc = new DateTimeOffset(2025, 02, 02, 8, 0, 0, TimeSpan.Zero)
            },
            new ScannedFile
            {
                FullPath = @"C:\Root\c.txt",
                RelativePath = "c.txt",
                RelativeDirectoryPath = string.Empty,
                FileName = "c.txt",
                Extension = ".txt",
                ModifiedUtc = new DateTimeOffset(2025, 02, 03, 8, 0, 0, TimeSpan.Zero)
            }
        ]);

        var reader = new SequencedContentReader(
            new FileContentSnapshot
            {
                Text = "Readable invoice",
                IsTextReadable = true,
                ExtractionSource = "text"
            },
            new FileContentSnapshot
            {
                IsTextReadable = false,
                ExtractionSource = "unsupported"
            });

        var service = new OrganizationWorkflowService(
            scanner,
            reader,
            new SemanticClassifierCoordinator(
                new HeuristicSemanticClassifier(),
                new NullGeminiClassifier(),
                NullLogger<SemanticClassifierCoordinator>.Instance),
            new NullGeminiOrganizationAdvisor(),
            new DateResolutionService(),
            new DuplicateDetectionService(new NullFileHashProvider(), NullLogger<DuplicateDetectionService>.Instance),
            new ProjectClusterService(),
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
                    PreviewSampleSize = 2,
                    MaxFilesToScan = 10
                },
                Gemini = new GeminiOptions
                {
                    Enabled = false
                }
            },
            progress: null,
            CancellationToken.None);

        Assert.Equal(3, plan.Summary.ScannedItems);
        Assert.Equal(2, plan.Summary.PlannedItems);
        Assert.Equal(1, plan.Summary.SkippedByPreviewSample);
        Assert.False(plan.Summary.ScanLimitHit);
        Assert.True(plan.Summary.PreviewSampleLimitHit);
        Assert.True(plan.Summary.HasIncompleteCoverage);
        Assert.Equal(1, plan.Summary.UnreadableContentCount);
    }

    [Fact]
    public async Task BuildPlanAsync_ReportsDuplicateHashFailuresInCoverage()
    {
        var scanner = new FakeFileScanner(
        [
            new ScannedFile
            {
                FullPath = @"C:\Root\a.txt",
                RelativePath = "a.txt",
                RelativeDirectoryPath = string.Empty,
                FileName = "a.txt",
                Extension = ".txt",
                SizeBytes = 10,
                ModifiedUtc = new DateTimeOffset(2025, 02, 01, 8, 0, 0, TimeSpan.Zero)
            },
            new ScannedFile
            {
                FullPath = @"C:\Root\b.txt",
                RelativePath = "b.txt",
                RelativeDirectoryPath = string.Empty,
                FileName = "b.txt",
                Extension = ".txt",
                SizeBytes = 10,
                ModifiedUtc = new DateTimeOffset(2025, 02, 02, 8, 0, 0, TimeSpan.Zero)
            }
        ]);

        var service = new OrganizationWorkflowService(
            scanner,
            new FakeContentReader(new FileContentSnapshot
            {
                Text = "Readable text",
                IsTextReadable = true,
                ExtractionSource = "text"
            }),
            new SemanticClassifierCoordinator(
                new HeuristicSemanticClassifier(),
                new NullGeminiClassifier(),
                NullLogger<SemanticClassifierCoordinator>.Instance),
            new NullGeminiOrganizationAdvisor(),
            new DateResolutionService(),
            new DuplicateDetectionService(new ThrowingFileHashProvider(@"C:\Root\b.txt"), NullLogger<DuplicateDetectionService>.Instance),
            new ProjectClusterService(),
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
                    DuplicatePolicy = new DuplicatePolicy
                    {
                        EnableExactDuplicateDetection = true
                    }
                },
                Gemini = new GeminiOptions
                {
                    Enabled = false
                }
            },
            progress: null,
            CancellationToken.None);

        Assert.Equal(1, plan.Summary.DuplicateHashFailureCount);
        Assert.Equal(0, plan.Summary.DuplicateCount);
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

    private sealed class SequencedContentReader : IFileContentReader
    {
        private readonly Queue<FileContentSnapshot> snapshots;

        public SequencedContentReader(params FileContentSnapshot[] snapshots)
        {
            this.snapshots = new Queue<FileContentSnapshot>(snapshots);
        }

        public Task<FileContentSnapshot> ReadAsync(
            ScannedFile file,
            OrganizationSettings settings,
            CancellationToken cancellationToken) => Task.FromResult(snapshots.Dequeue());
    }

    private sealed class NullGeminiClassifier : IGeminiSemanticClassifier
    {
        public Task<SemanticInsight?> ClassifyAsync(
            SemanticAnalysisRequest request,
            GeminiOptions options,
            CancellationToken cancellationToken) => Task.FromResult<SemanticInsight?>(null);
    }

    private sealed class NullGeminiOrganizationAdvisor : IGeminiOrganizationAdvisor
    {
        public Task<OrganizationGuidance?> AdviseAsync(
            IReadOnlyList<FileAnalysisContext> contexts,
            OrganizationSettings settings,
            GeminiOptions options,
            CancellationToken cancellationToken) => Task.FromResult<OrganizationGuidance?>(null);
    }

    private sealed class ThrowingGeminiOrganizationAdvisor : IGeminiOrganizationAdvisor
    {
        public Task<OrganizationGuidance?> AdviseAsync(
            IReadOnlyList<FileAnalysisContext> contexts,
            OrganizationSettings settings,
            GeminiOptions options,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Malformed Gemini organization guidance.");
    }

    private sealed class NullFileHashProvider : IFileHashProvider
    {
        public Task<string> ComputeHashAsync(string fullPath, CancellationToken cancellationToken) =>
            Task.FromResult(string.Empty);
    }

    private sealed class ThrowingFileHashProvider(string throwingPath) : IFileHashProvider
    {
        public Task<string> ComputeHashAsync(string fullPath, CancellationToken cancellationToken)
        {
            if (string.Equals(fullPath, throwingPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Cannot hash file.");
            }

            return Task.FromResult("HASH");
        }
    }
}
