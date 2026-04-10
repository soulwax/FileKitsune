using FileTransformer.Application.Abstractions;
using FileTransformer.Domain.Models;
using Microsoft.Extensions.Logging;

namespace FileTransformer.Infrastructure.Configuration;

public sealed class ResilientExecutionJournalStore : IExecutionJournalStore
{
    private readonly SqliteExecutionJournalStore sqliteStore;
    private readonly JsonExecutionJournalStore jsonStore;
    private readonly PostgresExecutionJournalStore postgresStore;
    private readonly PersistenceOptions options;
    private readonly ILogger<ResilientExecutionJournalStore> logger;

    public ResilientExecutionJournalStore(
        SqliteExecutionJournalStore sqliteStore,
        JsonExecutionJournalStore jsonStore,
        PostgresExecutionJournalStore postgresStore,
        PersistenceOptionsResolver optionsResolver,
        ILogger<ResilientExecutionJournalStore> logger)
    {
        this.sqliteStore = sqliteStore;
        this.jsonStore = jsonStore;
        this.postgresStore = postgresStore;
        options = optionsResolver.GetOptions();
        this.logger = logger;
    }

    public async Task SaveAsync(ExecutionJournal journal, CancellationToken cancellationToken)
    {
        await sqliteStore.SaveAsync(journal, cancellationToken);
        await jsonStore.SaveAsync(journal, cancellationToken);

        if (!options.UseRemotePersistence)
        {
            return;
        }

        try
        {
            await postgresStore.SaveAsync(journal, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Remote journal persistence unavailable. Using local stores.");
        }
    }

    public async Task<ExecutionJournal?> LoadLatestAsync(CancellationToken cancellationToken)
    {
        var journals = await LoadAllAsync(cancellationToken);
        return journals.FirstOrDefault();
    }

    public async Task<ExecutionJournal?> LoadAsync(Guid journalId, CancellationToken cancellationToken)
    {
        if (options.UseRemotePersistence)
        {
            try
            {
                var remote = await postgresStore.LoadAsync(journalId, cancellationToken);
                if (remote is not null)
                {
                    await sqliteStore.SaveAsync(remote, cancellationToken);
                    await jsonStore.SaveAsync(remote, cancellationToken);
                    return remote;
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Remote journal lookup unavailable. Falling back to local stores.");
            }
        }

        return await sqliteStore.LoadAsync(journalId, cancellationToken)
            ?? await jsonStore.LoadAsync(journalId, cancellationToken);
    }

    public async Task<IReadOnlyList<ExecutionJournal>> LoadAllAsync(CancellationToken cancellationToken)
    {
        var localSqlite = await sqliteStore.LoadAllAsync(cancellationToken);
        var localJson = await jsonStore.LoadAllAsync(cancellationToken);
        var merged = Merge(localSqlite, localJson);

        if (!options.UseRemotePersistence)
        {
            return merged;
        }

        try
        {
            var remote = await postgresStore.LoadAllAsync(cancellationToken);
            foreach (var journal in remote)
            {
                await sqliteStore.SaveAsync(journal, cancellationToken);
                await jsonStore.SaveAsync(journal, cancellationToken);
            }

            foreach (var localOnly in merged.Where(local => remote.All(remoteJournal => remoteJournal.JournalId != local.JournalId)))
            {
                try
                {
                    await postgresStore.SaveAsync(localOnly, cancellationToken);
                }
                catch (Exception exception)
                {
                    logger.LogDebug(exception, "Could not backfill local journal {JournalId} to remote store.", localOnly.JournalId);
                }
            }

            return Merge(remote, merged);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Remote journal history unavailable. Falling back to local stores.");
            return merged;
        }
    }

    private static IReadOnlyList<ExecutionJournal> Merge(
        IEnumerable<ExecutionJournal> primary,
        IEnumerable<ExecutionJournal> secondary)
    {
        return primary
            .Concat(secondary)
            .GroupBy(journal => journal.JournalId)
            .Select(group => group.OrderByDescending(candidate => candidate.CreatedAtUtc).First())
            .OrderByDescending(journal => journal.CreatedAtUtc)
            .ToList();
    }
}
