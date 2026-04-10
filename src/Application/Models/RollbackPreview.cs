using FileTransformer.Domain.Models;

namespace FileTransformer.Application.Models;

public sealed class RollbackPreview
{
    public ExecutionJournal? Journal { get; init; }

    public List<RollbackPreviewEntry> Entries { get; init; } = [];
}
