using FileTransformer.Application.Abstractions;
using FileTransformer.Application.Models;
using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;
using Microsoft.Extensions.Logging;

namespace FileTransformer.Application.Services;

public sealed class PlanExecutionService
{
    // TODO: Add duplicate-detection policies before execution when the planner grows beyond v1.
    private readonly IFileOperations fileOperations;
    private readonly IFileHashProvider fileHashProvider;
    private readonly IExecutionJournalStore executionJournalStore;
    private readonly PathSafetyService pathSafetyService;
    private readonly ILogger<PlanExecutionService> logger;

    public PlanExecutionService(
        IFileOperations fileOperations,
        IFileHashProvider fileHashProvider,
        IExecutionJournalStore executionJournalStore,
        PathSafetyService pathSafetyService,
        ILogger<PlanExecutionService> logger)
    {
        this.fileOperations = fileOperations;
        this.fileHashProvider = fileHashProvider;
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

        if (operations.Count > 0)
        {
            await executionJournalStore.SaveAsync(journal, cancellationToken);
        }

        try
        {
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
                var destinationExistedBeforeMove = fileOperations.FileExists(destinationFullPath);

                try
                {
                    if (destinationExistedBeforeMove)
                    {
                        if (plan.Settings.NamingPolicy.ConflictHandlingMode == ConflictHandlingMode.Skip)
                        {
                            skippedCount++;
                            messages.Add($"Skipped '{operation.CurrentRelativePath}' because '{operation.ProposedRelativePath}' already exists.");
                            continue;
                        }

                        destinationFullPath = await ResolveConflictAsync(destinationFullPath, cancellationToken);
                    }

                    var fileInfo = TryReadFileInfo(sourceFullPath);
                    var destinationDirectory = Path.GetDirectoryName(destinationFullPath) ?? plan.Settings.RootDirectory;
                    await fileOperations.EnsureDirectoryAsync(destinationDirectory, cancellationToken);
                    await fileOperations.MoveFileAsync(sourceFullPath, destinationFullPath, cancellationToken);
                    var contentHash = await TryComputeHashAsync(destinationFullPath, cancellationToken);

                    journal.Entries.Add(new ExecutionJournalEntry
                    {
                        OperationId = operation.Id,
                        SourceFullPath = sourceFullPath,
                        DestinationFullPath = destinationFullPath,
                        Outcome = "Moved",
                        Notes = operation.Reason,
                        DestinationExistedBeforeMove = destinationExistedBeforeMove,
                        ContentHash = contentHash,
                        FileSizeBytes = fileInfo?.Length,
                        SourceCreatedUtc = fileInfo is null ? null : new DateTimeOffset(fileInfo.CreationTimeUtc),
                        SourceModifiedUtc = fileInfo is null ? null : new DateTimeOffset(fileInfo.LastWriteTimeUtc)
                    });

                    successCount++;
                    await executionJournalStore.SaveAsync(journal, cancellationToken);
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

            journal.Status = ExecutionJournalStatus.Completed;
            journal.CompletedAtUtc = DateTimeOffset.UtcNow;
            if (operations.Count > 0)
            {
                await executionJournalStore.SaveAsync(journal, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            journal.Status = ExecutionJournalStatus.Canceled;
            if (operations.Count > 0)
            {
                await executionJournalStore.SaveAsync(journal, CancellationToken.None);
            }

            throw;
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

    private static FileInfo? TryReadFileInfo(string path)
    {
        try
        {
            return File.Exists(path) ? new FileInfo(path) : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string> TryComputeHashAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            return await fileHashProvider.ComputeHashAsync(path, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Hash computation failed for executed file {File}", path);
            return string.Empty;
        }
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
