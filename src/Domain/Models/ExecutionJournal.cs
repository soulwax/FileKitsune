namespace FileTransformer.Domain.Models;

public sealed class ExecutionJournal
{
    public Guid JournalId { get; init; } = Guid.NewGuid();

    public string RootDirectory { get; init; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public List<ExecutionJournalEntry> Entries { get; init; } = [];
}
