using FileTransformer.Application.Abstractions;
using FileTransformer.Application.Models;
using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;
using Microsoft.Extensions.Logging;

namespace FileTransformer.Application.Services;

public sealed class RollbackService
{
    // TODO: Persist richer rollback checkpoints for partial-failure recovery across sessions.
    private readonly IExecutionJournalStore executionJournalStore;
    private readonly IFileOperations fileOperations;
    private readonly IFileHashProvider fileHashProvider;
    private readonly ILogger<RollbackService> logger;

    public RollbackService(
        IExecutionJournalStore executionJournalStore,
        IFileOperations fileOperations,
        IFileHashProvider fileHashProvider,
        ILogger<RollbackService> logger)
    {
        this.executionJournalStore = executionJournalStore;
        this.fileOperations = fileOperations;
        this.fileHashProvider = fileHashProvider;
        this.logger = logger;
    }

    public async Task<ExecutionOutcome> RollbackLatestAsync(CancellationToken cancellationToken)
    {
        var journal = await executionJournalStore.LoadLatestAsync(cancellationToken);
        return journal is null || journal.Entries.Count == 0
            ? CreateNoJournalOutcome()
            : await RollbackEntriesAsync(journal, journal.Entries, cancellationToken);
    }

    public async Task<ExecutionOutcome> RollbackAsync(Guid journalId, CancellationToken cancellationToken)
    {
        var journal = await executionJournalStore.LoadAsync(journalId, cancellationToken);
        return journal is null || journal.Entries.Count == 0
            ? CreateNoJournalOutcome()
            : await RollbackEntriesAsync(journal, journal.Entries, cancellationToken);
    }

    public async Task<ExecutionOutcome> RollbackFolderAsync(string folderPrefix, CancellationToken cancellationToken)
    {
        var journal = await executionJournalStore.LoadLatestAsync(cancellationToken);
        return journal is null || journal.Entries.Count == 0
            ? CreateNoJournalOutcome()
            : await RollbackFolderAsync(journal, folderPrefix, cancellationToken);
    }

    public async Task<ExecutionOutcome> RollbackFolderAsync(Guid journalId, string folderPrefix, CancellationToken cancellationToken)
    {
        var journal = await executionJournalStore.LoadAsync(journalId, cancellationToken);
        return journal is null || journal.Entries.Count == 0
            ? CreateNoJournalOutcome()
            : await RollbackFolderAsync(journal, folderPrefix, cancellationToken);
    }

    public Task<IReadOnlyList<ExecutionJournal>> LoadHistoryAsync(CancellationToken cancellationToken) =>
        executionJournalStore.LoadAllAsync(cancellationToken);

    public Task<ExecutionJournal?> LoadJournalAsync(Guid journalId, CancellationToken cancellationToken) =>
        executionJournalStore.LoadAsync(journalId, cancellationToken);

    public async Task<bool> MarkAbandonedAsync(Guid journalId, CancellationToken cancellationToken)
    {
        var journal = await executionJournalStore.LoadAsync(journalId, cancellationToken);
        if (journal is null)
        {
            return false;
        }

        journal.Status = ExecutionJournalStatus.Abandoned;
        journal.CompletedAtUtc ??= DateTimeOffset.UtcNow;
        await executionJournalStore.SaveAsync(journal, cancellationToken);
        return true;
    }

    public async Task<RollbackPreview> PreviewRollbackAsync(Guid journalId, CancellationToken cancellationToken)
    {
        var journal = await executionJournalStore.LoadAsync(journalId, cancellationToken);
        return BuildPreview(journal, journal?.Entries);
    }

    public async Task<RollbackPreview> PreviewRollbackFolderAsync(Guid journalId, string folderPrefix, CancellationToken cancellationToken)
    {
        var journal = await executionJournalStore.LoadAsync(journalId, cancellationToken);
        if (journal is null || journal.Entries.Count == 0)
        {
            return new RollbackPreview();
        }

        var folderFullPath = Path.Combine(journal.RootDirectory, folderPrefix);
        var entries = journal.Entries
            .Where(entry => IsInFolder(entry.DestinationFullPath, folderFullPath) || IsInFolder(entry.SourceFullPath, folderFullPath))
            .ToList();

        return BuildPreview(journal, entries);
    }

    private async Task<ExecutionOutcome> RollbackFolderAsync(
        ExecutionJournal journal,
        string folderPrefix,
        CancellationToken cancellationToken)
    {
        var folderFullPath = Path.Combine(journal.RootDirectory, folderPrefix);
        var entries = journal.Entries
            .Where(entry => IsInFolder(entry.DestinationFullPath, folderFullPath) || IsInFolder(entry.SourceFullPath, folderFullPath))
            .ToList();

        if (entries.Count == 0)
        {
            return new ExecutionOutcome
            {
                Summary = $"No operations found for folder \"{folderPrefix}\".",
                SummaryResourceKey = "StatusRollbackNoFolderOperations",
                SummaryArguments = [folderPrefix]
            };
        }

        return await RollbackEntriesAsync(journal, entries, cancellationToken, folderFullPath);
    }

    private async Task<string> TryComputeHashAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            return await fileHashProvider.ComputeHashAsync(path, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Hash verification skipped for {Path}", path);
            return string.Empty;
        }
    }

    private static bool IsInFolder(string filePath, string folderFullPath) =>
        filePath.StartsWith(folderFullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Path.GetDirectoryName(filePath), folderFullPath, StringComparison.OrdinalIgnoreCase);

    private static ExecutionOutcome CreateNoJournalOutcome() =>
        new()
        {
            Summary = "No rollback journal was found.",
            SummaryResourceKey = "StatusRollbackNoJournal"
        };

    private async Task<ExecutionOutcome> RollbackEntriesAsync(
        ExecutionJournal journal,
        IEnumerable<ExecutionJournalEntry> entries,
        CancellationToken cancellationToken,
        string? scopeDirectory = null)
    {
        var successCount = 0;
        var skippedCount = 0;
        var failedCount = 0;
        var messages = new List<string>();

        foreach (var entry in entries.OrderByDescending(item => item.ExecutedAtUtc))
        {
            cancellationToken.ThrowIfCancellationRequested();
            entry.LastRollbackAttemptedAtUtc = DateTimeOffset.UtcNow;

            try
            {
                if (!fileOperations.FileExists(entry.DestinationFullPath))
                {
                    skippedCount++;
                    entry.RollbackStatus = RollbackEntryStatus.SkippedMissingDestination;
                    entry.RollbackMessage = $"Skipped '{entry.DestinationFullPath}': file no longer at destination.";
                    messages.Add(entry.RollbackMessage);
                    continue;
                }

                if (fileOperations.FileExists(entry.SourceFullPath))
                {
                    skippedCount++;
                    entry.RollbackStatus = RollbackEntryStatus.SkippedOriginalPathOccupied;
                    entry.RollbackMessage = $"Skipped '{entry.DestinationFullPath}': a file already exists at the original path '{entry.SourceFullPath}'.";
                    messages.Add(entry.RollbackMessage);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(entry.ContentHash))
                {
                    var currentHash = await TryComputeHashAsync(entry.DestinationFullPath, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(currentHash) &&
                        !string.Equals(currentHash, entry.ContentHash, StringComparison.OrdinalIgnoreCase))
                    {
                        skippedCount++;
                        entry.RollbackStatus = RollbackEntryStatus.SkippedContentMismatch;
                        entry.RollbackMessage = $"Skipped '{entry.DestinationFullPath}': content has changed since the original move — rolling back would overwrite modified data.";
                        messages.Add(entry.RollbackMessage);
                        logger.LogWarning("Rollback skipped for {Path}: content hash mismatch.", entry.DestinationFullPath);
                        continue;
                    }
                }

                var directory = Path.GetDirectoryName(entry.SourceFullPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    await fileOperations.EnsureDirectoryAsync(directory, cancellationToken);
                }

                await fileOperations.MoveFileAsync(entry.DestinationFullPath, entry.SourceFullPath, cancellationToken);
                entry.RollbackStatus = RollbackEntryStatus.Restored;
                entry.RollbackMessage = $"Restored '{entry.DestinationFullPath}' to '{entry.SourceFullPath}'.";
                successCount++;
            }
            catch (Exception exception)
            {
                failedCount++;
                entry.RollbackStatus = RollbackEntryStatus.Failed;
                entry.RollbackMessage = $"Rollback failed for '{entry.DestinationFullPath}': {exception.Message}";
                logger.LogError(exception, "Rollback failed for {Path}", entry.DestinationFullPath);
                messages.Add(entry.RollbackMessage);
            }
        }

        try
        {
            var removedFolders = await fileOperations.RemoveEmptyDirectoriesAsync(scopeDirectory ?? journal.RootDirectory, cancellationToken);
            if (removedFolders.Count > 0)
            {
                messages.Add($"Removed {removedFolders.Count} empty folder(s) left behind by the rolled-back run.");
                logger.LogInformation("Removed {Count} empty folder(s) after rollback.", removedFolders.Count);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to remove empty directories after rollback.");
        }

        await executionJournalStore.SaveAsync(journal, cancellationToken);

        return new ExecutionOutcome
        {
            RequestedOperations = successCount + skippedCount + failedCount,
            SuccessfulOperations = successCount,
            SkippedOperations = skippedCount,
            FailedOperations = failedCount,
            Summary = $"Rolled back {successCount} operation(s), skipped {skippedCount}, failed {failedCount}.",
            SummaryResourceKey = "StatusRollbackOutcome",
            SummaryArguments = [successCount, skippedCount, failedCount],
            Messages = messages
        };
    }

    private RollbackPreview BuildPreview(ExecutionJournal? journal, IEnumerable<ExecutionJournalEntry>? entries)
    {
        if (journal is null || entries is null)
        {
            return new RollbackPreview();
        }

        return new RollbackPreview
        {
            Journal = journal,
            Entries = entries
                .OrderByDescending(item => item.ExecutedAtUtc)
                .Select(CreatePreviewEntry)
                .ToList(),
            ReadyCount = entries.Count(entry => !fileOperations.FileExists(entry.SourceFullPath) && fileOperations.FileExists(entry.DestinationFullPath)),
            MissingDestinationCount = entries.Count(entry => !fileOperations.FileExists(entry.DestinationFullPath)),
            OriginalPathOccupiedCount = entries.Count(entry => fileOperations.FileExists(entry.SourceFullPath))
        };
    }

    private RollbackPreviewEntry CreatePreviewEntry(ExecutionJournalEntry entry)
    {
        if (!fileOperations.FileExists(entry.DestinationFullPath))
        {
            return new RollbackPreviewEntry
            {
                ExecutedAtUtc = entry.ExecutedAtUtc,
                SourceFullPath = entry.SourceFullPath,
                DestinationFullPath = entry.DestinationFullPath,
                Outcome = entry.Outcome,
                Notes = entry.Notes,
                PreviewStatus = RollbackPreviewStatus.MissingDestination,
                PreviewMessage = "File no longer exists at the rollback source location."
            };
        }

        if (fileOperations.FileExists(entry.SourceFullPath))
        {
            return new RollbackPreviewEntry
            {
                ExecutedAtUtc = entry.ExecutedAtUtc,
                SourceFullPath = entry.SourceFullPath,
                DestinationFullPath = entry.DestinationFullPath,
                Outcome = entry.Outcome,
                Notes = entry.Notes,
                PreviewStatus = RollbackPreviewStatus.OriginalPathOccupied,
                PreviewMessage = "Original path is already occupied and would be skipped."
            };
        }

        return new RollbackPreviewEntry
        {
            ExecutedAtUtc = entry.ExecutedAtUtc,
            SourceFullPath = entry.SourceFullPath,
            DestinationFullPath = entry.DestinationFullPath,
            Outcome = entry.Outcome,
            Notes = entry.Notes,
            PreviewStatus = RollbackPreviewStatus.Ready,
            PreviewMessage = "Ready to restore."
        };
    }
}
