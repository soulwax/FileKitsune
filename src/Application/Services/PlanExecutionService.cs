using FileTransformer.Application.Abstractions;
using FileTransformer.Application.Models;
using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;
using Microsoft.Extensions.Logging;

namespace FileTransformer.Application.Services;

public sealed class PlanExecutionService
{
    private readonly IFileOperations fileOperations;
    private readonly IExecutionJournalStore executionJournalStore;
    private readonly PathSafetyService pathSafetyService;
    private readonly ILogger<PlanExecutionService> logger;

    public PlanExecutionService(
        IFileOperations fileOperations,
        IExecutionJournalStore executionJournalStore,
        PathSafetyService pathSafetyService,
        ILogger<PlanExecutionService> logger)
    {
        this.fileOperations = fileOperations;
        this.executionJournalStore = executionJournalStore;
        this.pathSafetyService = pathSafetyService;
        this.logger = logger;
    }

    public async Task<ExecutionOutcome> ExecuteAsync(
        OrganizationPlan plan,
        IEnumerable<Guid> selectedOperationIds,
        IProgress<WorkflowProgress>? progress,
        CancellationToken cancellationToken)
    {
        var selectedIds = selectedOperationIds.ToHashSet();
        var operations = plan.Operations
            .Where(operation => selectedIds.Contains(operation.Id))
            .Where(operation => operation.AllowedToExecute)
            .Where(operation => operation.OperationType is not PlanOperationType.Skip and not PlanOperationType.CreateFolder)
            .ToList();

        var journal = new ExecutionJournal
        {
            RootDirectory = plan.Settings.RootDirectory
        };

        var messages = new List<string>();
        var successCount = 0;
        var skippedCount = 0;
        var failedCount = 0;

        for (var index = 0; index < operations.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var operation = operations[index];
            progress?.Report(new WorkflowProgress
            {
                Stage = "execute",
                Processed = index,
                Total = operations.Count,
                Message = $"Executing {index + 1} of {operations.Count}: {operation.CurrentRelativePath}"
            });

            var sourceFullPath = pathSafetyService.CombineWithinRoot(plan.Settings.RootDirectory, operation.CurrentRelativePath);
            var destinationFullPath = pathSafetyService.CombineWithinRoot(plan.Settings.RootDirectory, operation.ProposedRelativePath);

            try
            {
                if (fileOperations.FileExists(destinationFullPath))
                {
                    if (plan.Settings.ConflictHandlingMode == ConflictHandlingMode.Skip)
                    {
                        skippedCount++;
                        messages.Add($"Skipped '{operation.CurrentRelativePath}' because '{operation.ProposedRelativePath}' already exists.");
                        continue;
                    }

                    destinationFullPath = await ResolveConflictAsync(destinationFullPath, cancellationToken);
                }

                var destinationDirectory = Path.GetDirectoryName(destinationFullPath) ?? plan.Settings.RootDirectory;
                await fileOperations.EnsureDirectoryAsync(destinationDirectory, cancellationToken);
                await fileOperations.MoveFileAsync(sourceFullPath, destinationFullPath, cancellationToken);

                journal.Entries.Add(new ExecutionJournalEntry
                {
                    OperationId = operation.Id,
                    SourceFullPath = sourceFullPath,
                    DestinationFullPath = destinationFullPath,
                    Outcome = "Moved",
                    Notes = operation.Reason
                });

                successCount++;
            }
            catch (Exception exception)
            {
                failedCount++;
                messages.Add($"Failed '{operation.CurrentRelativePath}': {exception.Message}");
                logger.LogError(exception, "Execution failed for {File}", operation.CurrentRelativePath);
            }
        }

        if (plan.Settings.RemoveEmptyFolders)
        {
            var removedFolders = await fileOperations.RemoveEmptyDirectoriesAsync(plan.Settings.RootDirectory, cancellationToken);
            if (removedFolders.Count > 0)
            {
                messages.Add($"Removed {removedFolders.Count} empty folders.");
            }
        }

        if (journal.Entries.Count > 0)
        {
            await executionJournalStore.SaveAsync(journal, cancellationToken);
        }

        return new ExecutionOutcome
        {
            RequestedOperations = operations.Count,
            SuccessfulOperations = successCount,
            SkippedOperations = skippedCount,
            FailedOperations = failedCount,
            Summary = $"Executed {successCount} operation(s), skipped {skippedCount}, failed {failedCount}.",
            Journal = journal.Entries.Count > 0 ? journal : null,
            Messages = messages
        };
    }

    private async Task<string> ResolveConflictAsync(string destinationFullPath, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(destinationFullPath) ?? string.Empty;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(destinationFullPath);
        var extension = Path.GetExtension(destinationFullPath);

        for (var counter = 2; counter < 10_000; counter++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var candidate = Path.Combine(directory, $"{fileNameWithoutExtension} ({counter}){extension}");
            if (!fileOperations.FileExists(candidate))
            {
                return candidate;
            }

            await Task.Yield();
        }

        throw new InvalidOperationException("Unable to find a conflict-free destination path.");
    }
}
