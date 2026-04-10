using System.Text.Json;
using FileTransformer.Application.Abstractions;
using FileTransformer.Application.Models;
using Microsoft.Data.Sqlite;

namespace FileTransformer.Infrastructure.Configuration;

public sealed class SqliteAppSettingsStore : IAppSettingsStore
{
    private readonly AppStoragePaths appStoragePaths;

    public SqliteAppSettingsStore(AppStoragePaths appStoragePaths)
    {
        this.appStoragePaths = appStoragePaths;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT payload
            FROM app_settings_cache
            WHERE cache_key = 1
            LIMIT 1;
            """;

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

        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO app_settings_cache (cache_key, payload, updated_at_utc)
            VALUES (1, $payload, $updatedAtUtc)
            ON CONFLICT(cache_key) DO UPDATE SET
                payload = excluded.payload,
                updated_at_utc = excluded.updated_at_utc;
            """;
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(settings));
        command.Parameters.AddWithValue("$updatedAtUtc", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
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
            CREATE TABLE IF NOT EXISTS app_settings_cache (
                cache_key INTEGER PRIMARY KEY CHECK (cache_key = 1),
                payload TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
