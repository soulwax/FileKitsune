using FileTransformer.Application.Abstractions;
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
        var service = new PlanExecutionService(fileOperations, journalStore, new PathSafetyService(), NullLogger<PlanExecutionService>.Instance);
        var operation = CreateOperation("source.txt", @"Invoices\2025\source.txt");
        var plan = CreatePlan(ConflictHandlingMode.Skip, operation);

        var outcome = await service.ExecuteAsync(plan, [operation.Id], progress: null, CancellationToken.None);

        Assert.Equal(1, outcome.SkippedOperations);
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
        var service = new PlanExecutionService(fileOperations, journalStore, new PathSafetyService(), NullLogger<PlanExecutionService>.Instance);
        var operation = CreateOperation("source.txt", @"Invoices\2025\source.txt");
        var plan = CreatePlan(ConflictHandlingMode.AppendCounter, operation);

        var outcome = await service.ExecuteAsync(plan, [operation.Id], progress: null, CancellationToken.None);

        Assert.Equal(1, outcome.SuccessfulOperations);
        Assert.Single(fileOperations.Moves);
        Assert.EndsWith("(2).txt", fileOperations.Moves[0].Destination, StringComparison.OrdinalIgnoreCase);
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

    private static OrganizationPlan CreatePlan(ConflictHandlingMode conflictHandlingMode, PlanOperation operation) =>
        new()
        {
            Settings = new OrganizationSettings
            {
                RootDirectory = @"C:\Root",
                ConflictHandlingMode = conflictHandlingMode
            },
            Operations = [operation],
            Summary = new PlanSummary { TotalItems = 1, MoveCount = 1 }
        };

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

        public Task SaveAsync(ExecutionJournal journal, CancellationToken cancellationToken)
        {
            LastSavedJournal = journal;
            return Task.CompletedTask;
        }

        public Task<ExecutionJournal?> LoadLatestAsync(CancellationToken cancellationToken) =>
            Task.FromResult(LastSavedJournal);
    }
}
