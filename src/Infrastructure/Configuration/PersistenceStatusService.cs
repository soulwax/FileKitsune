using FileTransformer.Application.Abstractions;
using FileTransformer.Application.Models;
using Npgsql;

namespace FileTransformer.Infrastructure.Configuration;

public sealed class PersistenceStatusService : IPersistenceStatusService
{
    private readonly PersistenceOptions options;

    public PersistenceStatusService(PersistenceOptionsResolver optionsResolver)
    {
        options = optionsResolver.GetOptions();
    }

    public async Task<PersistenceStatusSnapshot> GetStatusAsync(CancellationToken cancellationToken)
    {
        if (options.ForceOfflineMode)
        {
            return new PersistenceStatusSnapshot
            {
                Mode = PersistenceStatusMode.LocalOnly,
                PrimaryStore = "SQLite + JSON",
                SecondaryStore = "Offline forced",
                DetailKey = "PersistenceDetailForcedOffline"
            };
        }

        if (!options.UseRemotePersistence)
        {
            return new PersistenceStatusSnapshot
            {
                Mode = PersistenceStatusMode.LocalOnly,
                PrimaryStore = "SQLite + JSON",
                SecondaryStore = "No remote database configured",
                DetailKey = "PersistenceDetailLocalOnly"
            };
        }

        if (await IsRemoteAvailableAsync(cancellationToken))
        {
            return new PersistenceStatusSnapshot
            {
                Mode = PersistenceStatusMode.SharedOnline,
                PrimaryStore = "Nile/Postgres",
                SecondaryStore = "SQLite + JSON cache",
                DetailKey = "PersistenceDetailSharedOnline"
            };
        }

        return new PersistenceStatusSnapshot
        {
            Mode = PersistenceStatusMode.SharedFallback,
            PrimaryStore = "SQLite + JSON",
            SecondaryStore = "Nile/Postgres unavailable",
            DetailKey = "PersistenceDetailSharedFallback"
        };
    }

    private async Task<bool> IsRemoteAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(options.RemoteConnectionString)
            {
                Timeout = 3,
                CommandTimeout = 3
            };

            await using var connection = new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
