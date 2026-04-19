using FileTransformer.Application.Abstractions;
using FileTransformer.Application.Models;
using FileTransformer.Application.Services;
using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FileTransformer.Tests.Application;

public sealed class RollbackServiceTests
{
    [Fact]
    public async Task RollbackLatestAsync_RestoresMovedFile()
    {
        var journal = CreateJournal(
            rootDirectory: @"C:\Root",
            entries:
            [
                CreateEntry(@"C:\Root\source.txt", @"C:\Root\Invoices\2025\source.txt")
            ]);

        var fileOperations = new FakeFileOperations(existingFiles: [@"C:\Root\Invoices\2025\source.txt"]);
        var journalStore = new InMemoryJournalStore([journal]);
        var service = new RollbackService(journalStore, fileOperations, NullLogger<RollbackService>.Instance);

        var outcome = await service.RollbackLatestAsync(CancellationToken.None);

        Assert.Equal(1, outcome.SuccessfulOperations);
        Assert.Equal("StatusRollbackOutcome", outcome.SummaryResourceKey);
        Assert.Single(fileOperations.Moves);
        Assert.Contains(fileOperations.Moves, move =>
            move.Source == @"C:\Root\Invoices\2025\source.txt" &&
            move.Destination == @"C:\Root\source.txt");
    }

    [Fact]
    public async Task RollbackAsync_CanTargetHistoricalJournalById()
    {
        var olderJournal = CreateJournal(
            rootDirectory: @"C:\Root",
            createdAtUtc: new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero),
            entries:
            [
                CreateEntry(@"C:\Root\a.txt", @"C:\Root\Alpha\a.txt")
            ]);

        var latestJournal = CreateJournal(
            rootDirectory: @"C:\Root",
            createdAtUtc: new DateTimeOffset(2026, 4, 2, 10, 0, 0, TimeSpan.Zero),
            entries:
            [
                CreateEntry(@"C:\Root\b.txt", @"C:\Root\Beta\b.txt")
            ]);

        var fileOperations = new FakeFileOperations(existingFiles: [@"C:\Root\Alpha\a.txt", @"C:\Root\Beta\b.txt"]);
        var journalStore = new InMemoryJournalStore([olderJournal, latestJournal]);
        var service = new RollbackService(journalStore, fileOperations, NullLogger<RollbackService>.Instance);

        var outcome = await service.RollbackAsync(olderJournal.JournalId, CancellationToken.None);

        Assert.Equal(1, outcome.SuccessfulOperations);
        Assert.DoesNotContain(fileOperations.Moves, move => move.Source == @"C:\Root\Beta\b.txt");
        Assert.Contains(fileOperations.Moves, move => move.Source == @"C:\Root\Alpha\a.txt");
    }

    [Fact]
    public async Task RollbackFolderAsync_OnlyRollsBackMatchingFolder()
    {
        var journal = CreateJournal(
            rootDirectory: @"C:\Root",
            entries:
            [
                CreateEntry(@"C:\Root\a.txt", @"C:\Root\Invoices\2025\a.txt"),
                CreateEntry(@"C:\Root\b.txt", @"C:\Root\Research\2025\b.txt")
            ]);

        var fileOperations = new FakeFileOperations(existingFiles: [@"C:\Root\Invoices\2025\a.txt", @"C:\Root\Research\2025\b.txt"]);
        var journalStore = new InMemoryJournalStore([journal]);
        var service = new RollbackService(journalStore, fileOperations, NullLogger<RollbackService>.Instance);

        var outcome = await service.RollbackFolderAsync("Invoices", CancellationToken.None);

        Assert.Equal(1, outcome.SuccessfulOperations);
        Assert.DoesNotContain(fileOperations.Moves, move => move.Source == @"C:\Root\Research\2025\b.txt");
        Assert.Contains(fileOperations.Moves, move => move.Source == @"C:\Root\Invoices\2025\a.txt");
    }

    [Fact]
    public async Task RollbackLatestAsync_SkipsWhenOriginalPathAlreadyExists()
    {
        var journal = CreateJournal(
            rootDirectory: @"C:\Root",
            entries:
            [
                CreateEntry(@"C:\Root\source.txt", @"C:\Root\Invoices\2025\source.txt")
            ]);

        var fileOperations = new FakeFileOperations(existingFiles: [@"C:\Root\source.txt", @"C:\Root\Invoices\2025\source.txt"]);
        var journalStore = new InMemoryJournalStore([journal]);
        var service = new RollbackService(journalStore, fileOperations, NullLogger<RollbackService>.Instance);

        var outcome = await service.RollbackLatestAsync(CancellationToken.None);

        Assert.Equal(1, outcome.SkippedOperations);
        Assert.Empty(fileOperations.Moves);
    }

    [Fact]
    public async Task RollbackLatestAsync_IsIdempotentWhenRunTwice()
    {
        var journal = CreateJournal(
            rootDirectory: @"C:\Root",
            entries:
            [
                CreateEntry(@"C:\Root\source.txt", @"C:\Root\Invoices\2025\source.txt")
            ]);

        var fileOperations = new FakeFileOperations(existingFiles: [@"C:\Root\Invoices\2025\source.txt"]);
        var journalStore = new InMemoryJournalStore([journal]);
        var service = new RollbackService(journalStore, fileOperations, NullLogger<RollbackService>.Instance);

        var firstOutcome = await service.RollbackLatestAsync(CancellationToken.None);
        var secondOutcome = await service.RollbackLatestAsync(CancellationToken.None);

        Assert.Equal(1, firstOutcome.SuccessfulOperations);
        Assert.Equal(1, secondOutcome.SkippedOperations);
        Assert.Single(fileOperations.Moves);
    }

    [Fact]
    public async Task PreviewRollbackAsync_ReportsExpectedStatuses()
    {
        var journal = CreateJournal(
            rootDirectory: @"C:\Root",
            entries:
            [
                CreateEntry(@"C:\Root\ready.txt", @"C:\Root\Invoices\ready.txt"),
                CreateEntry(@"C:\Root\occupied.txt", @"C:\Root\Invoices\occupied.txt"),
                CreateEntry(@"C:\Root\missing.txt", @"C:\Root\Invoices\missing.txt")
            ]);

        var fileOperations = new FakeFileOperations(existingFiles:
        [
            @"C:\Root\Invoices\ready.txt",
            @"C:\Root\Invoices\occupied.txt",
            @"C:\Root\occupied.txt"
        ]);
        var journalStore = new InMemoryJournalStore([journal]);
        var service = new RollbackService(journalStore, fileOperations, NullLogger<RollbackService>.Instance);

        var preview = await service.PreviewRollbackAsync(journal.JournalId, CancellationToken.None);

        Assert.Equal(3, preview.Entries.Count);
        Assert.Equal(1, preview.ReadyCount);
        Assert.Equal(1, preview.MissingDestinationCount);
        Assert.Equal(1, preview.OriginalPathOccupiedCount);
        Assert.Contains(preview.Entries, entry => entry.SourceFullPath == @"C:\Root\ready.txt" && entry.PreviewStatus == RollbackPreviewStatus.Ready);
        Assert.Contains(preview.Entries, entry => entry.SourceFullPath == @"C:\Root\occupied.txt" && entry.PreviewStatus == RollbackPreviewStatus.OriginalPathOccupied);
        Assert.Contains(preview.Entries, entry => entry.SourceFullPath == @"C:\Root\missing.txt" && entry.PreviewStatus == RollbackPreviewStatus.MissingDestination);
    }

    [Fact]
    public async Task PreviewRollbackFolderAsync_FiltersCountsToSelectedFolder()
    {
        var journal = CreateJournal(
            rootDirectory: @"C:\Root",
            entries:
            [
                CreateEntry(@"C:\Root\ready.txt", @"C:\Root\Invoices\ready.txt"),
                CreateEntry(@"C:\Root\occupied.txt", @"C:\Root\Invoices\occupied.txt"),
                CreateEntry(@"C:\Root\elsewhere.txt", @"C:\Root\Research\elsewhere.txt")
            ]);

        var fileOperations = new FakeFileOperations(existingFiles:
        [
            @"C:\Root\Invoices\ready.txt",
            @"C:\Root\Invoices\occupied.txt",
            @"C:\Root\occupied.txt",
            @"C:\Root\Research\elsewhere.txt"
        ]);
        var journalStore = new InMemoryJournalStore([journal]);
        var service = new RollbackService(journalStore, fileOperations, NullLogger<RollbackService>.Instance);

        var preview = await service.PreviewRollbackFolderAsync(journal.JournalId, "Invoices", CancellationToken.None);

        Assert.Equal(2, preview.Entries.Count);
        Assert.Equal(1, preview.ReadyCount);
        Assert.Equal(0, preview.MissingDestinationCount);
        Assert.Equal(1, preview.OriginalPathOccupiedCount);
        Assert.DoesNotContain(preview.Entries, entry => entry.SourceFullPath == @"C:\Root\elsewhere.txt");
    }

    [Fact]
    public async Task RollbackAsync_PersistsPerEntryRollbackStatus()
    {
        var journal = CreateJournal(
            rootDirectory: @"C:\Root",
            entries:
            [
                CreateEntry(@"C:\Root\source.txt", @"C:\Root\Invoices\source.txt")
            ]);

        var fileOperations = new FakeFileOperations(existingFiles: [@"C:\Root\Invoices\source.txt"]);
        var journalStore = new InMemoryJournalStore([journal]);
        var service = new RollbackService(journalStore, fileOperations, NullLogger<RollbackService>.Instance);

        await service.RollbackAsync(journal.JournalId, CancellationToken.None);

        var savedJournal = await journalStore.LoadAsync(journal.JournalId, CancellationToken.None);
        Assert.NotNull(savedJournal);
        Assert.Single(savedJournal!.Entries);
        Assert.Equal(RollbackEntryStatus.Restored, savedJournal.Entries[0].RollbackStatus);
        Assert.False(string.IsNullOrWhiteSpace(savedJournal.Entries[0].RollbackMessage));
        Assert.NotNull(savedJournal.Entries[0].LastRollbackAttemptedAtUtc);
    }

    [Fact]
    public async Task RollbackLatestAsync_UsesNoJournalSummaryResourceKey()
    {
        var journalStore = new InMemoryJournalStore([]);
        var service = new RollbackService(
            journalStore,
            new FakeFileOperations(existingFiles: []),
            NullLogger<RollbackService>.Instance);

        var outcome = await service.RollbackLatestAsync(CancellationToken.None);

        Assert.Equal("StatusRollbackNoJournal", outcome.SummaryResourceKey);
    }

    private static ExecutionJournal CreateJournal(
        string rootDirectory,
        IReadOnlyList<ExecutionJournalEntry> entries,
        DateTimeOffset? createdAtUtc = null) =>
        new()
        {
            RootDirectory = rootDirectory,
            CreatedAtUtc = createdAtUtc ?? DateTimeOffset.UtcNow,
            CompletedAtUtc = createdAtUtc ?? DateTimeOffset.UtcNow,
            Status = ExecutionJournalStatus.Completed,
            Entries = entries.ToList()
        };

    private static ExecutionJournalEntry CreateEntry(string sourceFullPath, string destinationFullPath) =>
        new()
        {
            SourceFullPath = sourceFullPath,
            DestinationFullPath = destinationFullPath,
            ExecutedAtUtc = DateTimeOffset.UtcNow,
            Outcome = "Moved"
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
        private readonly List<ExecutionJournal> journals;

        public InMemoryJournalStore(IEnumerable<ExecutionJournal> journals)
        {
            this.journals = journals.OrderByDescending(journal => journal.CreatedAtUtc).ToList();
        }

        public Task SaveAsync(ExecutionJournal journal, CancellationToken cancellationToken)
        {
            var index = journals.FindIndex(candidate => candidate.JournalId == journal.JournalId);
            if (index >= 0)
            {
                journals[index] = journal;
            }
            else
            {
                journals.Add(journal);
                journals.Sort((left, right) => right.CreatedAtUtc.CompareTo(left.CreatedAtUtc));
            }

            return Task.CompletedTask;
        }

        public Task<ExecutionJournal?> LoadLatestAsync(CancellationToken cancellationToken) =>
            Task.FromResult(journals.FirstOrDefault());

        public Task<ExecutionJournal?> LoadAsync(Guid journalId, CancellationToken cancellationToken) =>
            Task.FromResult(journals.FirstOrDefault(journal => journal.JournalId == journalId));

        public Task<IReadOnlyList<ExecutionJournal>> LoadAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ExecutionJournal>>(journals);
    }
}
