namespace FileTransformer.Infrastructure.Configuration;

public sealed class PersistenceOptions
{
    public bool UseRemotePersistence { get; init; }

    public bool ForceOfflineMode { get; init; }

    public string RemoteConnectionString { get; init; } = string.Empty;
}
