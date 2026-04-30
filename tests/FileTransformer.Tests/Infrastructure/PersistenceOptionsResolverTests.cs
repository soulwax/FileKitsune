using FileTransformer.Infrastructure.Configuration;
using Xunit;

namespace FileTransformer.Tests.Infrastructure;

[Collection("EnvironmentVariables")]
public sealed class PersistenceOptionsResolverTests
{
    [Fact]
    public void GetOptions_PrefersFileKitsuneOfflineMode_WhenMultipleFlagsAreDefined()
    {
        using var envScope = new EnvironmentVariableScope(new Dictionary<string, string?>
        {
            ["FILEKITSUNE_OFFLINE_MODE"] = "true",
            ["FILETRANSFORMER_OFFLINE_MODE"] = "false",
            ["OFFLINE_MODE"] = "false",
            ["NILEDB_URL"] = null,
            ["POSTGRES_URL"] = "Host=localhost;Port=5432;Username=test;Password=test;Database=filetransformer",
            ["DATABASE_URL"] = null
        });

        var options = new PersistenceOptionsResolver().GetOptions();

        Assert.True(options.ForceOfflineMode);
        Assert.False(options.UseRemotePersistence);
    }

    [Fact]
    public void GetOptions_FallsBackToLegacyKey_WhenPrimaryKeyIsMissing()
    {
        using var envScope = new EnvironmentVariableScope(new Dictionary<string, string?>
        {
            ["FILEKITSUNE_OFFLINE_MODE"] = null,
            ["FILETRANSFORMER_OFFLINE_MODE"] = "true",
            ["OFFLINE_MODE"] = "false",
            ["NILEDB_URL"] = null,
            ["POSTGRES_URL"] = null,
            ["DATABASE_URL"] = null
        });

        var options = new PersistenceOptionsResolver().GetOptions();

        Assert.True(options.ForceOfflineMode);
    }

    [Fact]
    public void GetOptions_FallsBackToGenericKey_WhenSpecificKeysAreMissing()
    {
        using var envScope = new EnvironmentVariableScope(new Dictionary<string, string?>
        {
            ["FILEKITSUNE_OFFLINE_MODE"] = null,
            ["FILETRANSFORMER_OFFLINE_MODE"] = null,
            ["OFFLINE_MODE"] = "1",
            ["NILEDB_URL"] = null,
            ["POSTGRES_URL"] = null,
            ["DATABASE_URL"] = null
        });

        var options = new PersistenceOptionsResolver().GetOptions();

        Assert.True(options.ForceOfflineMode);
    }

    [Fact]
    public void GetOptions_NormalizesPostgresUrlConnectionStrings()
    {
        using var envScope = new EnvironmentVariableScope(new Dictionary<string, string?>
        {
            ["FILEKITSUNE_OFFLINE_MODE"] = null,
            ["FILETRANSFORMER_OFFLINE_MODE"] = null,
            ["OFFLINE_MODE"] = null,
            ["NILEDB_URL"] = null,
            ["POSTGRES_URL"] = null,
            ["DATABASE_URL"] = "postgresql://user:secret@example.test:55433/filekitsune?sslmode=require"
        });

        var options = new PersistenceOptionsResolver().GetOptions();

        Assert.True(options.UseRemotePersistence);
        Assert.Contains("Host=example.test", options.RemoteConnectionString);
        Assert.Contains("Port=55433", options.RemoteConnectionString);
        Assert.Contains("Database=filekitsune", options.RemoteConnectionString);
        Assert.Contains("Username=user", options.RemoteConnectionString);
        Assert.Contains("SSL Mode=Require", options.RemoteConnectionString);
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly Dictionary<string, string?> originalValues = [];

        public EnvironmentVariableScope(IReadOnlyDictionary<string, string?> updates)
        {
            originalValues["FILEKITSUNE_IGNORE_DOTENV"] = Environment.GetEnvironmentVariable("FILEKITSUNE_IGNORE_DOTENV");
            Environment.SetEnvironmentVariable("FILEKITSUNE_IGNORE_DOTENV", "true");

            foreach (var (key, value) in updates)
            {
                originalValues[key] = Environment.GetEnvironmentVariable(key);
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        public void Dispose()
        {
            foreach (var (key, value) in originalValues)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
