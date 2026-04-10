using FileTransformer.Domain.Enums;

namespace FileTransformer.Domain.Models;

public sealed class ExecutionJournalEntry
{
    public Guid OperationId { get; set; }

    public string SourceFullPath { get; set; } = string.Empty;

    public string DestinationFullPath { get; set; } = string.Empty;

    public string SourceRelativePath { get; set; } = string.Empty;

    public string DestinationRelativePath { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string FileExtension { get; set; } = string.Empty;

    public DateTimeOffset ExecutedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string Outcome { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public bool DestinationExistedBeforeMove { get; set; }

    public string ContentHash { get; set; } = string.Empty;

    public long? FileSizeBytes { get; set; }

    public DateTimeOffset? SourceCreatedUtc { get; set; }

    public DateTimeOffset? SourceModifiedUtc { get; set; }

    public RollbackEntryStatus RollbackStatus { get; set; } = RollbackEntryStatus.NotAttempted;

    public DateTimeOffset? LastRollbackAttemptedAtUtc { get; set; }

    public string RollbackMessage { get; set; } = string.Empty;
}
