using FileTransformer.Application.Models;
using FileTransformer.Infrastructure.Configuration;
using Xunit;

namespace FileTransformer.Tests.Infrastructure;

[Collection("EnvironmentVariables")]
public sealed class PersistenceStatusServiceTests
{
    [Fact]
    public async Task GetStatusAsync_ReturnsLocalOnly_WhenRemoteNotConfigured()
    {
        using var envScope = new EnvironmentVariableScope(new Dictionary<string, string?>
        {
            ["NILEDB_URL"] = null,
            ["POSTGRES_URL"] = null,
            ["DATABASE_URL"] = null,
            ["FILEKITSUNE_OFFLINE_MODE"] = null,
            ["FILETRANSFORMER_OFFLINE_MODE"] = null,
            ["OFFLINE_MODE"] = null
        });

        var service = new PersistenceStatusService(new PersistenceOptionsResolver());
        var snapshot = await service.GetStatusAsync(CancellationToken.None);

        Assert.Equal(PersistenceStatusMode.LocalOnly, snapshot.Mode);
        Assert.Equal("PersistenceDetailLocalOnly", snapshot.DetailKey);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsForcedOffline_WhenOfflineModeEnabled()
    {
        using var envScope = new EnvironmentVariableScope(new Dictionary<string, string?>
        {
            ["POSTGRES_URL"] = "Host=127.0.0.1;Port=1;Username=test;Password=test;Database=filetransformer",
            ["NILEDB_URL"] = null,
            ["DATABASE_URL"] = null,
            ["FILEKITSUNE_OFFLINE_MODE"] = "true",
            ["FILETRANSFORMER_OFFLINE_MODE"] = null,
            ["OFFLINE_MODE"] = null
        });

        var service = new PersistenceStatusService(new PersistenceOptionsResolver());
        var snapshot = await service.GetStatusAsync(CancellationToken.None);

        Assert.Equal(PersistenceStatusMode.LocalOnly, snapshot.Mode);
        Assert.Equal("PersistenceDetailForcedOffline", snapshot.DetailKey);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsSharedFallback_WhenRemoteUnavailable()
    {
        using var envScope = new EnvironmentVariableScope(new Dictionary<string, string?>
        {
            ["POSTGRES_URL"] = "Host=127.0.0.1;Port=1;Username=test;Password=test;Database=filetransformer",
            ["NILEDB_URL"] = null,
            ["DATABASE_URL"] = null,
            ["FILEKITSUNE_OFFLINE_MODE"] = "false",
            ["FILETRANSFORMER_OFFLINE_MODE"] = null,
            ["OFFLINE_MODE"] = null
        });

        var service = new PersistenceStatusService(new PersistenceOptionsResolver());
        var snapshot = await service.GetStatusAsync(CancellationToken.None);

        Assert.Equal(PersistenceStatusMode.SharedFallback, snapshot.Mode);
        Assert.Equal("PersistenceDetailSharedFallback", snapshot.DetailKey);
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
