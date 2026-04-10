using System.Text.Json;
using FileTransformer.Application.Abstractions;
using FileTransformer.Domain.Models;
using Microsoft.Data.Sqlite;

namespace FileTransformer.Infrastructure.Configuration;

public sealed class SqliteExecutionJournalStore : IExecutionJournalStore
{
    private readonly AppStoragePaths appStoragePaths;

    public SqliteExecutionJournalStore(AppStoragePaths appStoragePaths)
    {
        this.appStoragePaths = appStoragePaths;
    }

    public async Task SaveAsync(ExecutionJournal journal, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO execution_journals (journal_id, created_at_utc, payload, updated_at_utc)
            VALUES ($journalId, $createdAtUtc, $payload, $updatedAtUtc)
            ON CONFLICT(journal_id) DO UPDATE SET
                created_at_utc = excluded.created_at_utc,
                payload = excluded.payload,
                updated_at_utc = excluded.updated_at_utc;
            """;
        command.Parameters.AddWithValue("$journalId", journal.JournalId.ToString("N"));
        command.Parameters.AddWithValue("$createdAtUtc", journal.CreatedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(journal));
        command.Parameters.AddWithValue("$updatedAtUtc", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ExecutionJournal?> LoadLatestAsync(CancellationToken cancellationToken)
    {
        var journals = await LoadAllAsync(cancellationToken);
        return journals.FirstOrDefault();
    }

    public async Task<ExecutionJournal?> LoadAsync(Guid journalId, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT payload
            FROM execution_journals
            WHERE journal_id = $journalId
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$journalId", journalId.ToString("N"));

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not string payload || string.IsNullOrWhiteSpace(payload)
            ? null
            : JsonSerializer.Deserialize<ExecutionJournal>(payload);
    }

    public async Task<IReadOnlyList<ExecutionJournal>> LoadAllAsync(CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT payload
            FROM execution_journals
            ORDER BY created_at_utc DESC;
            """;

        var journals = new List<ExecutionJournal>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var payload = reader.GetString(0);
            var journal = JsonSerializer.Deserialize<ExecutionJournal>(payload);
            if (journal is not null)
            {
                journals.Add(journal);
            }
        }

        return journals;
    }

    private SqliteConnection CreateConnection()
    {
        Directory.CreateDirectory(appStoragePaths.RootDirectory);
        return new SqliteConnection($"Data Source={appStoragePaths.PersistenceDatabasePath}");
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS execution_journals (
                journal_id TEXT PRIMARY KEY,
                created_at_utc TEXT NOT NULL,
                payload TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_execution_journals_created_at_utc
                ON execution_journals(created_at_utc DESC);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
