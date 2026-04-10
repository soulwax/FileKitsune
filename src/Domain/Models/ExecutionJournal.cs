using FileTransformer.Domain.Enums;

namespace FileTransformer.Domain.Models;

public sealed class ExecutionJournal
{
    public int Version { get; set; } = 2;

    public Guid JournalId { get; set; } = Guid.NewGuid();

    public string RootDirectory { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public ExecutionJournalStatus Status { get; set; } = ExecutionJournalStatus.Started;

    public List<ExecutionJournalEntry> Entries { get; set; } = [];
}
