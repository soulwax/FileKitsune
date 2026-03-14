namespace FileTransformer.Domain.Models;

public sealed class ExecutionJournalEntry
{
    public Guid OperationId { get; init; }

    public string SourceFullPath { get; init; } = string.Empty;

    public string DestinationFullPath { get; init; } = string.Empty;

    public DateTimeOffset ExecutedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string Outcome { get; init; } = string.Empty;

    public string Notes { get; init; } = string.Empty;
}
