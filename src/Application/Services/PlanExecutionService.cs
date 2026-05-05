using FileTransformer.Application.Abstractions;
using FileTransformer.Application.Models;
using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;
using Microsoft.Extensions.Logging;

namespace FileTransformer.Application.Services;

public sealed class PlanExecutionService
{
    private const double ModifiedTimestampToleranceSeconds = 2;

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

        var preflightFailures = await RevalidateBeforeExecutionAsync(plan, operations, cancellationToken);
        if (preflightFailures.Count > 0)
        {
            return new ExecutionOutcome
            {
                RequestedOperations = operations.Count,
                FailedOperations = operations.Count,
                Summary = $"Execution blocked before any file was changed. Rebuild the preview and review {preflightFailures.Count} issue(s).",
                SummaryResourceKey = "StatusExecutionPreflightFailed",
                SummaryArguments = [preflightFailures.Count],
                Messages = preflightFailures.ToList()
            };
        }

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
                    MessageResourceKey = "StatusProgressExecute",
                    MessageArguments = [index + 1, operations.Count, operation.CurrentRelativePath]
                });

                var sourceFullPath = pathSafetyService.CombineWithinRoot(plan.Settings.RootDirectory, operation.CurrentRelativePath);
                var destinationFullPath = pathSafetyService.CombineWithinRoot(plan.Settings.RootDirectory, operation.ProposedRelativePath);
                var destinationExistedBeforeMove = fileOperations.FileExists(destinationFullPath);

                ExecutionJournalEntry? entry = null;
                try
                {
                    if (!fileOperations.FileExists(sourceFullPath))
                    {
                        failedCount++;
                        messages.Add($"Skipped '{operation.CurrentRelativePath}': source file no longer exists.");
                        continue;
                    }

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

                    var destinationDirectory = Path.GetDirectoryName(destinationFullPath) ?? plan.Settings.RootDirectory;
                    await fileOperations.EnsureDirectoryAsync(destinationDirectory, cancellationToken);

                    // Write-ahead: persist intent before mutating the filesystem so a crash leaves a recoverable "Pending" record.
                    entry = new ExecutionJournalEntry
                    {
                        OperationId = operation.Id,
                        SourceFullPath = sourceFullPath,
                        DestinationFullPath = destinationFullPath,
                        SourceRelativePath = operation.CurrentRelativePath,
                        DestinationRelativePath = operation.ProposedRelativePath,
                        FileName = operation.FileName,
                        FileExtension = Path.GetExtension(operation.FileName),
                        Outcome = "Pending",
                        Notes = operation.Reason,
                        DestinationExistedBeforeMove = destinationExistedBeforeMove,
                    };
                    journal.Entries.Add(entry);
                    await executionJournalStore.SaveAsync(journal, cancellationToken);

                    await fileOperations.MoveFileAsync(sourceFullPath, destinationFullPath, cancellationToken);

                    var fileInfo = TryReadFileInfo(destinationFullPath);
                    entry.Outcome = "Moved";
                    entry.ContentHash = await TryComputeHashAsync(destinationFullPath, cancellationToken);
                    entry.FileSizeBytes = fileInfo?.Length;
                    entry.SourceCreatedUtc = fileInfo is null ? null : new DateTimeOffset(fileInfo.CreationTimeUtc);
                    entry.SourceModifiedUtc = fileInfo is null ? null : new DateTimeOffset(fileInfo.LastWriteTimeUtc);

                    successCount++;
                    await executionJournalStore.SaveAsync(journal, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    if (entry is not null)
                    {
                        entry.Outcome = "ExecutionFailed";
                        entry.RollbackStatus = RollbackEntryStatus.Failed;
                        try { await executionJournalStore.SaveAsync(journal, CancellationToken.None); } catch { }
                    }
                    throw;
                }
                catch (Exception exception)
                {
                    failedCount++;
                    messages.Add($"Failed '{operation.CurrentRelativePath}': {exception.Message}");
                    logger.LogError(exception, "Execution failed for {File}", operation.CurrentRelativePath);
                    if (entry is not null)
                    {
                        entry.Outcome = "ExecutionFailed";
                        entry.RollbackStatus = RollbackEntryStatus.Failed;
                        try { await executionJournalStore.SaveAsync(journal, CancellationToken.None); } catch { }
                    }
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
            SummaryResourceKey = "StatusExecutionOutcome",
            SummaryArguments = [successCount, skippedCount, failedCount],
            Journal = journal.Entries.Count > 0 ? journal : null,
            Messages = messages
        };
    }

    private async Task<IReadOnlyList<string>> RevalidateBeforeExecutionAsync(
        OrganizationPlan plan,
        IReadOnlyList<PlanOperation> operations,
        CancellationToken cancellationToken)
    {
        var failures = new List<string>();
        var reservedDestinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var operation in operations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string sourceFullPath;
            string destinationFullPath;
            try
            {
                sourceFullPath = pathSafetyService.CombineWithinRoot(plan.Settings.RootDirectory, operation.CurrentRelativePath);
                destinationFullPath = pathSafetyService.CombineWithinRoot(plan.Settings.RootDirectory, operation.ProposedRelativePath);
            }
            catch (Exception exception)
            {
                failures.Add($"'{operation.CurrentRelativePath}' is no longer safe to execute: {exception.Message}");
                continue;
            }

            if (!fileOperations.FileExists(sourceFullPath))
            {
                failures.Add($"'{operation.CurrentRelativePath}' no longer exists at its previewed source path.");
                continue;
            }

            var fileInfoFailure = await RevalidateSourceSnapshotAsync(operation, sourceFullPath, cancellationToken);
            if (!string.IsNullOrWhiteSpace(fileInfoFailure))
            {
                failures.Add(fileInfoFailure);
                continue;
            }

            var finalDestination = await ResolvePreflightDestinationAsync(
                plan,
                operation,
                destinationFullPath,
                reservedDestinations,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(finalDestination))
            {
                failures.Add($"'{operation.ProposedRelativePath}' is no longer conflict-free. Rebuild the preview before executing.");
                continue;
            }

            reservedDestinations.Add(finalDestination);
        }

        return failures;
    }

    private async Task<string> RevalidateSourceSnapshotAsync(
        PlanOperation operation,
        string sourceFullPath,
        CancellationToken cancellationToken)
    {
        if (operation.SourceSizeBytes is not null || operation.SourceModifiedUtc is not null)
        {
            var fileInfo = TryReadFileInfo(sourceFullPath);
            if (fileInfo is null)
            {
                return $"'{operation.CurrentRelativePath}' could not be re-read before execution.";
            }

            if (operation.SourceSizeBytes is not null && fileInfo.Length != operation.SourceSizeBytes.Value)
            {
                return $"'{operation.CurrentRelativePath}' changed size since the preview was built.";
            }

            if (operation.SourceModifiedUtc is not null)
            {
                var actualModifiedUtc = new DateTimeOffset(fileInfo.LastWriteTimeUtc);
                var delta = (actualModifiedUtc - operation.SourceModifiedUtc.Value).Duration();
                if (delta.TotalSeconds > ModifiedTimestampToleranceSeconds)
                {
                    return $"'{operation.CurrentRelativePath}' changed timestamp since the preview was built.";
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(operation.SourceContentHash))
        {
            var currentHash = await fileHashProvider.ComputeHashAsync(sourceFullPath, cancellationToken);
            if (!string.Equals(currentHash, operation.SourceContentHash, StringComparison.Ordinal))
            {
                return $"'{operation.CurrentRelativePath}' changed content since the preview was built.";
            }
        }

        return string.Empty;
    }

    private async Task<string> ResolvePreflightDestinationAsync(
        OrganizationPlan plan,
        PlanOperation operation,
        string destinationFullPath,
        HashSet<string> reservedDestinations,
        CancellationToken cancellationToken)
    {
        if (!fileOperations.FileExists(destinationFullPath) && !reservedDestinations.Contains(destinationFullPath))
        {
            return destinationFullPath;
        }

        if (plan.Settings.NamingPolicy.ConflictHandlingMode == ConflictHandlingMode.Skip)
        {
            return destinationFullPath;
        }

        var directory = Path.GetDirectoryName(destinationFullPath) ?? plan.Settings.RootDirectory;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(destinationFullPath);
        var extension = Path.GetExtension(destinationFullPath);

        for (var counter = 2; counter < 10_000; counter++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var candidate = Path.Combine(directory, $"{fileNameWithoutExtension} ({counter}){extension}");
            if (!pathSafetyService.IsWithinRoot(plan.Settings.RootDirectory, candidate))
            {
                return string.Empty;
            }

            if (!fileOperations.FileExists(candidate) && !reservedDestinations.Contains(candidate))
            {
                return candidate;
            }

            await Task.Yield();
        }

        return string.Empty;
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
