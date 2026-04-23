namespace FileTransformer.Infrastructure.Configuration;

public sealed class PersistenceOptionsResolver
{
    private readonly PersistenceOptions options;

    public PersistenceOptionsResolver()
    {
        var env = DotEnv.LoadIfPresent(AppEnvironmentPaths.GetCandidateEnvPaths().ToArray());

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

        var remoteConnectionString = GetValue("NILEDB_URL", "POSTGRES_URL", "DATABASE_URL") ?? string.Empty;
        var forceOfflineMode = ParseFlag(GetValue("FILEKITSUNE_OFFLINE_MODE", "FILETRANSFORMER_OFFLINE_MODE", "OFFLINE_MODE"));

        options = new PersistenceOptions
        {
            ForceOfflineMode = forceOfflineMode,
            RemoteConnectionString = remoteConnectionString,
            UseRemotePersistence = !forceOfflineMode && !string.IsNullOrWhiteSpace(remoteConnectionString)
        };
    }

    public PersistenceOptions GetOptions() => options;
}
