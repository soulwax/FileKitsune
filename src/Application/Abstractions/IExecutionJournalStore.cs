using FileTransformer.Domain.Models;

namespace FileTransformer.Application.Abstractions;

public interface IExecutionJournalStore
{
    Task SaveAsync(ExecutionJournal journal, CancellationToken cancellationToken);

    Task<ExecutionJournal?> LoadLatestAsync(CancellationToken cancellationToken);
}
