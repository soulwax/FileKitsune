using Npgsql;

namespace FileTransformer.Infrastructure.Configuration;

public sealed class PersistenceOptionsResolver
{
    private readonly PersistenceOptions options;

    public PersistenceOptionsResolver()
    {
        var env = ParseFlag(Environment.GetEnvironmentVariable("FILEKITSUNE_IGNORE_DOTENV"))
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : DotEnv.LoadIfPresent(AppEnvironmentPaths.GetCandidateEnvPaths().ToArray());

        string? GetValue(params string[] keys)
        {
            foreach (var key in keys)
            {
                var processValue = Environment.GetEnvironmentVariable(key);
                if (!string.IsNullOrWhiteSpace(processValue))
                {
                    return processValue;
                }

                if (env.TryGetValue(key, out var envValue) && !string.IsNullOrWhiteSpace(envValue))
                {
                    return envValue;
                }
            }

            return null;
        }

        static bool ParseFlag(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (bool.TryParse(value, out var parsed))
            {
                return parsed;
            }

            return value.Trim() switch
            {
                "1" => true,
                "yes" => true,
                "on" => true,
                _ => false
            };
        }

        var remoteConnectionString = NormalizeConnectionString(GetValue("NILEDB_URL", "POSTGRES_URL", "DATABASE_URL"));
        var forceOfflineMode = ParseFlag(GetValue("FILEKITSUNE_OFFLINE_MODE", "FILETRANSFORMER_OFFLINE_MODE", "OFFLINE_MODE"));

        options = new PersistenceOptions
        {
            ForceOfflineMode = forceOfflineMode,
            RemoteConnectionString = remoteConnectionString,
            UseRemotePersistence = !forceOfflineMode && !string.IsNullOrWhiteSpace(remoteConnectionString)
        };
    }

    public PersistenceOptions GetOptions() => options;

    private static string NormalizeConnectionString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "postgres" && uri.Scheme != "postgresql"))
        {
            return trimmed;
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'))
        };

        if (!uri.IsDefaultPort)
        {
            builder.Port = uri.Port;
        }

        var userInfoParts = uri.UserInfo.Split(':', 2);
        if (userInfoParts.Length > 0 && !string.IsNullOrWhiteSpace(userInfoParts[0]))
        {
            builder.Username = Uri.UnescapeDataString(userInfoParts[0]);
        }

        if (userInfoParts.Length > 1)
        {
            builder.Password = Uri.UnescapeDataString(userInfoParts[1]);
        }

        foreach (var (key, queryValue) in ParseQuery(uri.Query))
        {
            switch (key.ToLowerInvariant())
            {
                case "sslmode":
                    builder.SslMode = Enum.Parse<SslMode>(queryValue, ignoreCase: true);
                    break;
                case "pooling":
                    builder.Pooling = bool.Parse(queryValue);
                    break;
                case "timeout":
                case "connect_timeout":
                    builder.Timeout = int.Parse(queryValue);
                    break;
                case "commandtimeout":
                case "command_timeout":
                    builder.CommandTimeout = int.Parse(queryValue);
                    break;
            }
        }

        return builder.ConnectionString;
    }

    private static IEnumerable<(string Key, string Value)> ParseQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            yield break;
        }

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]))
            {
                continue;
            }

            yield return (Uri.UnescapeDataString(parts[0]), Uri.UnescapeDataString(parts[1]));
        }
    }
}
