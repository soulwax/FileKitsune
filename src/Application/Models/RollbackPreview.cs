using FileTransformer.Domain.Models;

namespace FileTransformer.Application.Models;

public sealed class RollbackPreview
{
    public ExecutionJournal? Journal { get; init; }

    public List<RollbackPreviewEntry> Entries { get; init; } = [];

    public int ReadyCount { get; init; }

    public int MissingDestinationCount { get; init; }

    public int OriginalPathOccupiedCount { get; init; }
}
