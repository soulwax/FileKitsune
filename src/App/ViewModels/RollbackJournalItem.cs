using FileTransformer.Domain.Enums;

namespace FileTransformer.App.ViewModels;

public sealed class RollbackJournalItem
{
    public Guid JournalId { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public ExecutionJournalStatus Status { get; init; }

    public bool NeedsRecovery => Status is ExecutionJournalStatus.Started or ExecutionJournalStatus.Canceled;

    public int OperationCount { get; init; }

    public string Label { get; init; } = string.Empty;
}
