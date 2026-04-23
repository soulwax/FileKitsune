using FileTransformer.Application.Models;
using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;
using FileTransformer.Infrastructure.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Data.Sqlite;
using Npgsql;
using System.Runtime.Versioning;
using Xunit;

namespace FileTransformer.Tests.Infrastructure;

[Collection("EnvironmentVariables")]
public sealed class ResilientPersistenceTests
{
    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task ResilientAppSettingsStore_FallsBackToLocalStores_WhenRemoteUnavailable()
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

        var rootPath = CreateTempDirectory();
        try
        {
            var appStoragePaths = new AppStoragePaths(rootPath);
            var protectedStore = new ProtectedAppSettingsStore(appStoragePaths, new AppEnvironmentResolver());
            var sqliteStore = new SqliteAppSettingsStore(appStoragePaths);
            var postgresStore = new PostgresAppSettingsStore(new PersistenceOptionsResolver());
            var store = new ResilientAppSettingsStore(
                protectedStore,
                sqliteStore,
                postgresStore,
                new PersistenceOptionsResolver(),
                NullLogger<ResilientAppSettingsStore>.Instance);

            var expected = new AppSettings
            {
                UiLanguage = "en-US",
                Organization = new OrganizationSettings
                {
                    RootDirectory = @"D:\Data",
                    StrategyPreset = OrganizationStrategyPreset.ProjectFirst
                },
                Gemini = new GeminiOptions
                {
                    Enabled = true,
                    ApiKey = "super-secret",
                    Model = "gemini-2.5-flash"
                }
            };

            await store.SaveAsync(expected, CancellationToken.None);
            var loaded = await store.LoadAsync(CancellationToken.None);

            Assert.Equal("en-US", loaded.UiLanguage);
            Assert.Equal(@"D:\Data", loaded.Organization.RootDirectory);
            Assert.Equal(OrganizationStrategyPreset.ProjectFirst, loaded.Organization.StrategyPreset);
            Assert.Equal("super-secret", loaded.Gemini.ApiKey);
            Assert.Equal("gemini-2.5-flash", loaded.Gemini.Model);
            Assert.True(File.Exists(appStoragePaths.PersistenceDatabasePath));
            Assert.True(File.Exists(appStoragePaths.SettingsFilePath));
        }
        finally
        {
            CleanupDirectory(rootPath);
        }
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task ResilientExecutionJournalStore_FallsBackToLocalStores_WhenRemoteUnavailable()
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

        var rootPath = CreateTempDirectory();
        try
        {
            var appStoragePaths = new AppStoragePaths(rootPath);
            var sqliteStore = new SqliteExecutionJournalStore(appStoragePaths);
            var jsonStore = new JsonExecutionJournalStore(appStoragePaths);
            var postgresStore = new PostgresExecutionJournalStore(new PersistenceOptionsResolver());
            var store = new ResilientExecutionJournalStore(
                sqliteStore,
                jsonStore,
                postgresStore,
                new PersistenceOptionsResolver(),
                NullLogger<ResilientExecutionJournalStore>.Instance);

            var journal = new ExecutionJournal
            {
                JournalId = Guid.NewGuid(),
                RootDirectory = rootPath,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                Status = ExecutionJournalStatus.Completed,
                Entries =
                [
                    new ExecutionJournalEntry
                    {
                        OperationId = Guid.NewGuid(),
                        SourceFullPath = Path.Combine(rootPath, "a.txt"),
                        DestinationFullPath = Path.Combine(rootPath, "organized", "a.txt"),
                        Outcome = "Moved",
                        Notes = "test"
                    }
                ]
            };

            await store.SaveAsync(journal, CancellationToken.None);

            var loaded = await store.LoadAsync(journal.JournalId, CancellationToken.None);
            var latest = await store.LoadLatestAsync(CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.NotNull(latest);
            Assert.Equal(journal.JournalId, loaded!.JournalId);
            Assert.Equal(journal.JournalId, latest!.JournalId);
            Assert.True(File.Exists(appStoragePaths.PersistenceDatabasePath));
            Assert.True(Directory.Exists(appStoragePaths.JournalDirectory));
        }
        finally
        {
            CleanupDirectory(rootPath);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "FileTransformerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CleanupDirectory(string rootPath)
    {
        SqliteConnection.ClearAllPools();
        NpgsqlConnection.ClearAllPools();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (Directory.Exists(rootPath))
                {
                    Directory.Delete(rootPath, recursive: true);
                }

                return;
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException) when (attempt < 4)
            {
                Thread.Sleep(100);
            }
        }
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly Dictionary<string, string?> originalValues = [];

        public EnvironmentVariableScope(IReadOnlyDictionary<string, string?> updates)
        {
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
