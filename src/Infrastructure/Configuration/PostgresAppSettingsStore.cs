using System.Text.Json;
using FileTransformer.Application.Abstractions;
using FileTransformer.Application.Models;
using Npgsql;

namespace FileTransformer.Infrastructure.Configuration;

public sealed class PostgresAppSettingsStore : IAppSettingsStore
{
    private readonly PersistenceOptions options;

    public PostgresAppSettingsStore(PersistenceOptionsResolver optionsResolver)
    {
        options = optionsResolver.GetOptions();
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            SELECT payload
            FROM file_transformer_app_settings
            WHERE settings_key = 1
            LIMIT 1;
            """,
            connection);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is not string payload || string.IsNullOrWhiteSpace(payload))
        {
            return new AppSettings();
        }

        return JsonSerializer.Deserialize<AppSettings>(payload) ?? new AppSettings();
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO file_transformer_app_settings (settings_key, payload, updated_at_utc)
            VALUES (1, $1, $2)
            ON CONFLICT (settings_key) DO UPDATE SET
                payload = excluded.payload,
                updated_at_utc = excluded.updated_at_utc;
            """,
            connection);
        command.Parameters.AddWithValue(JsonSerializer.Serialize(settings));
        command.Parameters.AddWithValue(DateTimeOffset.UtcNow.UtcDateTime);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private NpgsqlConnection CreateConnection() => new(options.RemoteConnectionString);

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            CREATE TABLE IF NOT EXISTS file_transformer_app_settings (
                settings_key INTEGER PRIMARY KEY,
                payload TEXT NOT NULL,
                updated_at_utc TIMESTAMPTZ NOT NULL
            );
            """,
            connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
