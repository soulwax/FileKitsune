using FileTransformer.Application.Abstractions;
using FileTransformer.Application.Models;
using FileTransformer.Domain.Models;
using Microsoft.Extensions.Logging;

namespace FileTransformer.Application.Services;

public sealed class RollbackService
{
    // TODO: Persist richer rollback checkpoints for partial-failure recovery across sessions.
    private readonly IExecutionJournalStore executionJournalStore;
    private readonly IFileOperations fileOperations;
    private readonly ILogger<RollbackService> logger;

    public RollbackService(
        IExecutionJournalStore executionJournalStore,
        IFileOperations fileOperations,
        ILogger<RollbackService> logger)
    {
        this.executionJournalStore = executionJournalStore;
        this.fileOperations = fileOperations;
        this.logger = logger;
    }

    public async Task<ExecutionOutcome> RollbackLatestAsync(CancellationToken cancellationToken)
    {
        var journal = await executionJournalStore.LoadLatestAsync(cancellationToken);
        if (journal is null || journal.Entries.Count == 0)
        {
            return new ExecutionOutcome
            {
                Summary = "No rollback journal was found."
            };
        }

        return await RollbackEntriesAsync(journal.Entries, cancellationToken);
    }

    public async Task<ExecutionOutcome> RollbackFolderAsync(string folderPrefix, CancellationToken cancellationToken)
    {
        var journal = await executionJournalStore.LoadLatestAsync(cancellationToken);
        if (journal is null || journal.Entries.Count == 0)
        {
            return new ExecutionOutcome
            {
                Summary = "No rollback journal was found."
            };
        }

        var folderFullPath = Path.Combine(journal.RootDirectory, folderPrefix);
        var entries = journal.Entries
            .Where(entry =>
                entry.DestinationFullPath.StartsWith(folderFullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetDirectoryName(entry.DestinationFullPath), folderFullPath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (entries.Count == 0)
        {
            return new ExecutionOutcome
            {
                Summary = $"No operations found for folder \"{folderPrefix}\"."
            };
        }

        return await RollbackEntriesAsync(entries, cancellationToken);
    }

    private async Task<ExecutionOutcome> RollbackEntriesAsync(
        IEnumerable<ExecutionJournalEntry> entries,
        CancellationToken cancellationToken)
    {
        var successCount = 0;
        var failedCount = 0;
        var messages = new List<string>();

        foreach (var entry in entries.OrderByDescending(item => item.ExecutedAtUtc))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (!fileOperations.FileExists(entry.DestinationFullPath) || fileOperations.FileExists(entry.SourceFullPath))
                {
                    messages.Add($"Skipped rollback for '{entry.DestinationFullPath}'.");
                    continue;
                }

                var directory = Path.GetDirectoryName(entry.SourceFullPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    await fileOperations.EnsureDirectoryAsync(directory, cancellationToken);
                }

                await fileOperations.MoveFileAsync(entry.DestinationFullPath, entry.SourceFullPath, cancellationToken);
                successCount++;
            }
            catch (Exception exception)
            {
                failedCount++;
                logger.LogError(exception, "Rollback failed for {Path}", entry.DestinationFullPath);
                messages.Add($"Rollback failed for '{entry.DestinationFullPath}': {exception.Message}");
            }
        }

        var total = successCount + failedCount + messages.Count(m => m.StartsWith("Skipped", StringComparison.Ordinal));
        return new ExecutionOutcome
        {
            RequestedOperations = total,
            SuccessfulOperations = successCount,
            FailedOperations = failedCount,
            Summary = $"Rolled back {successCount} operation(s), failed {failedCount}.",
            Messages = messages
        };
    }
}
