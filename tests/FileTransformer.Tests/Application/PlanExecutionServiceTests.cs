using FileTransformer.Application.Abstractions;
using FileTransformer.Application.Models;
using FileTransformer.Application.Services;
using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FileTransformer.Tests.Application;

public sealed class PlanExecutionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_SkipsWhenConflictHandlingIsSkip()
    {
        var fileOperations = new FakeFileOperations(
            existingFiles:
            [
                @"C:\Root\source.txt",
                @"C:\Root\Invoices\2025\source.txt"
            ]);

        var journalStore = new InMemoryJournalStore();
        var service = new PlanExecutionService(
            fileOperations,
            new FakeHashProvider(),
            journalStore,
            new PathSafetyService(),
            NullLogger<PlanExecutionService>.Instance);
        var operation = CreateOperation("source.txt", @"Invoices\2025\source.txt");
        var plan = CreatePlan(ConflictHandlingMode.Skip, operation);

        var outcome = await service.ExecuteAsync(plan, [operation.Id], progress: null, CancellationToken.None);

        Assert.Equal(1, outcome.SkippedOperations);
        Assert.Equal("StatusExecutionOutcome", outcome.SummaryResourceKey);
        Assert.Empty(fileOperations.Moves);
    }

    [Fact]
    public async Task ExecuteAsync_AppendsCounterWhenConflictHandlingRequestsIt()
    {
        var fileOperations = new FakeFileOperations(
            existingFiles:
            [
                @"C:\Root\source.txt",
                @"C:\Root\Invoices\2025\source.txt"
            ]);

        var journalStore = new InMemoryJournalStore();
        var service = new PlanExecutionService(
            fileOperations,
            new FakeHashProvider(),
            journalStore,
            new PathSafetyService(),
            NullLogger<PlanExecutionService>.Instance);
        var operation = CreateOperation("source.txt", @"Invoices\2025\source.txt");
        var plan = CreatePlan(ConflictHandlingMode.AppendCounter, operation);

        var outcome = await service.ExecuteAsync(plan, [operation.Id], progress: null, CancellationToken.None);

        Assert.Equal(1, outcome.SuccessfulOperations);
        Assert.Single(fileOperations.Moves);
        Assert.EndsWith("(2).txt", fileOperations.Moves[0].Destination, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_PersistsJournalBeforeAndAfterSuccessfulOperations()
    {
        var sourcePath = Path.GetTempFileName();
        var rootDirectory = Path.GetDirectoryName(sourcePath)!;
        var sourceName = Path.GetFileName(sourcePath);
        var destinationRelativePath = Path.Combine("Invoices", "2025", sourceName);
        var destinationFullPath = Path.Combine(rootDirectory, destinationRelativePath);

        try
        {
            var fileOperations = new FakeFileOperations(existingFiles: [sourcePath]);
            var journalStore = new InMemoryJournalStore();
            var service = new PlanExecutionService(
                fileOperations,
                new FakeHashProvider(),
                journalStore,
                new PathSafetyService(),
                NullLogger<PlanExecutionService>.Instance);
            var operation = CreateOperation(sourceName, destinationRelativePath);
            var plan = CreatePlan(rootDirectory, ConflictHandlingMode.AppendCounter, operation);

            var outcome = await service.ExecuteAsync(plan, [operation.Id], progress: null, CancellationToken.None);

            Assert.Equal(1, outcome.SuccessfulOperations);
            Assert.NotNull(journalStore.LastSavedJournal);
            Assert.Equal(4, journalStore.SaveCount);
            Assert.Equal(ExecutionJournalStatus.Completed, journalStore.LastSavedJournal!.Status);
            Assert.Single(journalStore.LastSavedJournal.Entries);
            Assert.Equal(destinationFullPath, journalStore.LastSavedJournal.Entries[0].DestinationFullPath);
            Assert.Equal(sourceName, journalStore.LastSavedJournal.Entries[0].SourceRelativePath);
            Assert.Equal(destinationRelativePath, journalStore.LastSavedJournal.Entries[0].DestinationRelativePath);
            Assert.Equal(sourceName, journalStore.LastSavedJournal.Entries[0].FileName);
            Assert.Equal(Path.GetExtension(sourceName), journalStore.LastSavedJournal.Entries[0].FileExtension);
            Assert.Equal($"HASH::{destinationFullPath}", journalStore.LastSavedJournal.Entries[0].ContentHash);
        }
        finally
        {
            if (File.Exists(sourcePath))
            {
                File.Delete(sourcePath);
            }

            if (File.Exists(destinationFullPath))
            {
                File.Delete(destinationFullPath);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateRoutedMove_CanBeRolledBackFromJournal()
    {
        var sourcePath = Path.GetTempFileName();
        var rootDirectory = Path.GetDirectoryName(sourcePath)!;
        var sourceName = Path.GetFileName(sourcePath);
        var destinationRelativePath = Path.Combine("Zu prüfende Duplikate", "ABCDEF12", sourceName);
        var destinationFullPath = Path.Combine(rootDirectory, destinationRelativePath);

        try
        {
            var fileOperations = new FakeFileOperations(existingFiles: [sourcePath]);
            var journalStore = new InMemoryJournalStore();
            var executionService = new PlanExecutionService(
                fileOperations,
                new FakeHashProvider(),
                journalStore,
                new PathSafetyService(),
                NullLogger<PlanExecutionService>.Instance);
            var rollbackService = new RollbackService(
                journalStore,
                fileOperations,
                new FakeHashProvider(),
                NullLogger<RollbackService>.Instance);

            var operation = new PlanOperation
            {
                OperationType = PlanOperationType.Move,
                CurrentRelativePath = sourceName,
                ProposedRelativePath = destinationRelativePath,
                AllowedToExecute = true,
                FileName = sourceName,
                DuplicateDetected = true,
                DuplicateOfRelativePath = @"Docs\canonical.txt",
                Reason = "Exact duplicate routed away from the main structure."
            };
            var plan = CreatePlan(rootDirectory, ConflictHandlingMode.AppendCounter, operation);

            var executeOutcome = await executionService.ExecuteAsync(plan, [operation.Id], progress: null, CancellationToken.None);
            var rollbackOutcome = await rollbackService.RollbackLatestAsync(CancellationToken.None);

            Assert.Equal(1, executeOutcome.SuccessfulOperations);
            Assert.Equal(1, rollbackOutcome.SuccessfulOperations);
            Assert.Contains(fileOperations.Moves, move =>
                move.Source == sourcePath &&
                move.Destination == destinationFullPath);
            Assert.Contains(fileOperations.Moves, move =>
                move.Source == destinationFullPath &&
                move.Destination == sourcePath);
            Assert.Equal(ExecutionJournalStatus.Completed, journalStore.LastSavedJournal!.Status);
            Assert.Single(journalStore.LastSavedJournal.Entries);
            Assert.Equal(destinationFullPath, journalStore.LastSavedJournal.Entries[0].DestinationFullPath);
        }
        finally
        {
            if (File.Exists(sourcePath))
            {
                File.Delete(sourcePath);
            }

            if (File.Exists(destinationFullPath))
            {
                File.Delete(destinationFullPath);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_ReportsLocalizedProgressKey()
    {
        var sourcePath = Path.GetTempFileName();
        var rootDirectory = Path.GetDirectoryName(sourcePath)!;
        var sourceName = Path.GetFileName(sourcePath);
        var destinationRelativePath = Path.Combine("Invoices", "2025", sourceName);
        var progressReports = new List<WorkflowProgress>();

        try
        {
            var fileOperations = new FakeFileOperations(existingFiles: [sourcePath]);
            var journalStore = new InMemoryJournalStore();
            var service = new PlanExecutionService(
                fileOperations,
                new FakeHashProvider(),
                journalStore,
                new PathSafetyService(),
                NullLogger<PlanExecutionService>.Instance);
            var operation = CreateOperation(sourceName, destinationRelativePath);
            var plan = CreatePlan(rootDirectory, ConflictHandlingMode.AppendCounter, operation);
            var progress = new CollectingProgress(progressReports);

            await service.ExecuteAsync(plan, [operation.Id], progress, CancellationToken.None);

            var report = Assert.Single(progressReports);
            Assert.Equal("execute", report.Stage);
            Assert.Equal("StatusProgressExecute", report.MessageResourceKey);
            Assert.Equal(3, report.MessageArguments.Length);
            Assert.Equal(1, report.MessageArguments[0]);
            Assert.Equal(1, report.MessageArguments[1]);
            Assert.Equal(sourceName, report.MessageArguments[2]);
        }
        finally
        {
            if (File.Exists(sourcePath))
            {
                File.Delete(sourcePath);
            }
        }
    }

    private static PlanOperation CreateOperation(string currentRelativePath, string proposedRelativePath) =>
        new()
        {
            OperationType = PlanOperationType.Move,
            CurrentRelativePath = currentRelativePath,
            ProposedRelativePath = proposedRelativePath,
            AllowedToExecute = true,
            FileName = Path.GetFileName(currentRelativePath),
            Reason = "Test move"
        };

    private static OrganizationPlan CreatePlan(string rootDirectory, ConflictHandlingMode conflictHandlingMode, PlanOperation operation) =>
        new()
        {
            Settings = new OrganizationSettings
            {
                RootDirectory = rootDirectory,
                ConflictHandlingMode = conflictHandlingMode
            },
            Operations = [operation],
            Summary = new PlanSummary { TotalItems = 1, MoveCount = 1 }
        };

    private static OrganizationPlan CreatePlan(ConflictHandlingMode conflictHandlingMode, PlanOperation operation) =>
        CreatePlan(@"C:\Root", conflictHandlingMode, operation);

    private sealed class FakeFileOperations : IFileOperations
    {
        private readonly HashSet<string> existingFiles;

        public FakeFileOperations(IEnumerable<string> existingFiles)
        {
            this.existingFiles = new HashSet<string>(existingFiles, StringComparer.OrdinalIgnoreCase);
        }

        public List<(string Source, string Destination)> Moves { get; } = [];

        public bool FileExists(string fullPath) => existingFiles.Contains(fullPath);

        public bool DirectoryExists(string fullPath) => true;

        public Task EnsureDirectoryAsync(string fullPath, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task MoveFileAsync(string sourceFullPath, string destinationFullPath, CancellationToken cancellationToken)
        {
            existingFiles.Remove(sourceFullPath);
            existingFiles.Add(destinationFullPath);
            Moves.Add((sourceFullPath, destinationFullPath));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> RemoveEmptyDirectoriesAsync(string rootDirectory, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class InMemoryJournalStore : IExecutionJournalStore
    {
        public ExecutionJournal? LastSavedJournal { get; private set; }

        public int SaveCount { get; private set; }

        public Task SaveAsync(ExecutionJournal journal, CancellationToken cancellationToken)
        {
            LastSavedJournal = journal;
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task<ExecutionJournal?> LoadLatestAsync(CancellationToken cancellationToken) =>
            Task.FromResult(LastSavedJournal);

        public Task<ExecutionJournal?> LoadAsync(Guid journalId, CancellationToken cancellationToken) =>
            Task.FromResult(LastSavedJournal?.JournalId == journalId ? LastSavedJournal : null);

        public Task<IReadOnlyList<ExecutionJournal>> LoadAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ExecutionJournal>>(LastSavedJournal is null ? [] : [LastSavedJournal]);
    }

    private sealed class FakeHashProvider : IFileHashProvider
    {
        public Task<string> ComputeHashAsync(string fullPath, CancellationToken cancellationToken) =>
            Task.FromResult($"HASH::{fullPath}");
    }

    private sealed class CollectingProgress(List<WorkflowProgress> reports) : IProgress<WorkflowProgress>
    {
        public void Report(WorkflowProgress value) => reports.Add(value);
    }
}
