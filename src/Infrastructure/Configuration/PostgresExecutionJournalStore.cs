using System.Text.Json;
using FileTransformer.Application.Abstractions;
using FileTransformer.Domain.Models;
using Npgsql;

namespace FileTransformer.Infrastructure.Configuration;

public sealed class PostgresExecutionJournalStore : IExecutionJournalStore
{
    private readonly PersistenceOptions options;

    public PostgresExecutionJournalStore(PersistenceOptionsResolver optionsResolver)
    {
        options = optionsResolver.GetOptions();
    }

    public async Task SaveAsync(ExecutionJournal journal, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO file_transformer_execution_journals (journal_id, created_at_utc, payload, updated_at_utc)
            VALUES ($1, $2, $3, $4)
            ON CONFLICT (journal_id) DO UPDATE SET
                created_at_utc = excluded.created_at_utc,
                payload = excluded.payload,
                updated_at_utc = excluded.updated_at_utc;
            """,
            connection);
        command.Parameters.AddWithValue(journal.JournalId);
        command.Parameters.AddWithValue(journal.CreatedAtUtc.UtcDateTime);
        command.Parameters.AddWithValue(JsonSerializer.Serialize(journal));
        command.Parameters.AddWithValue(DateTimeOffset.UtcNow.UtcDateTime);
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

        await using var command = new NpgsqlCommand(
            """
            SELECT payload
            FROM file_transformer_execution_journals
            WHERE journal_id = $1
            LIMIT 1;
            """,
            connection);
        command.Parameters.AddWithValue(journalId);

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

        await using var command = new NpgsqlCommand(
            """
            SELECT payload
            FROM file_transformer_execution_journals
            ORDER BY created_at_utc DESC;
            """,
            connection);

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

    private NpgsqlConnection CreateConnection() => new(options.RemoteConnectionString);

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            CREATE TABLE IF NOT EXISTS file_transformer_execution_journals (
                journal_id UUID PRIMARY KEY,
                created_at_utc TIMESTAMPTZ NOT NULL,
                payload TEXT NOT NULL,
                updated_at_utc TIMESTAMPTZ NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_file_transformer_execution_journals_created_at_utc
                ON file_transformer_execution_journals(created_at_utc DESC);
            """,
            connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
