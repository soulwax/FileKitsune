using FileTransformer.Application.Abstractions;
using FileTransformer.Application.Models;
using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;

namespace FileTransformer.Infrastructure.Configuration;

[SupportedOSPlatform("windows")]
public sealed class ResilientAppSettingsStore : IAppSettingsStore
{
    private readonly ProtectedAppSettingsStore protectedStore;
    private readonly SqliteAppSettingsStore sqliteStore;
    private readonly PostgresAppSettingsStore postgresStore;
    private readonly PersistenceOptions options;
    private readonly ILogger<ResilientAppSettingsStore> logger;

    public ResilientAppSettingsStore(
        ProtectedAppSettingsStore protectedStore,
        SqliteAppSettingsStore sqliteStore,
        PostgresAppSettingsStore postgresStore,
        PersistenceOptionsResolver optionsResolver,
        ILogger<ResilientAppSettingsStore> logger)
    {
        this.protectedStore = protectedStore;
        this.sqliteStore = sqliteStore;
        this.postgresStore = postgresStore;
        options = optionsResolver.GetOptions();
        this.logger = logger;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        var baseline = await protectedStore.LoadAsync(cancellationToken);

        if (!options.UseRemotePersistence)
        {
            return AppSettingsSnapshotMapper.MergeNonSecretSettings(
                baseline,
                await TryLoadSqliteAsync(cancellationToken));
        }

        var remote = await TryLoadRemoteAsync(cancellationToken);
        if (remote is not null)
        {
            await TrySaveSqliteAsync(remote, cancellationToken);
            return AppSettingsSnapshotMapper.MergeNonSecretSettings(baseline, remote);
        }

        return AppSettingsSnapshotMapper.MergeNonSecretSettings(
            baseline,
            await TryLoadSqliteAsync(cancellationToken));
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        await protectedStore.SaveAsync(settings, cancellationToken);

        var sanitized = AppSettingsSnapshotMapper.SanitizeForSharedPersistence(settings);
        await TrySaveSqliteAsync(sanitized, cancellationToken);

        if (!options.UseRemotePersistence)
        {
            return;
        }

        try
        {
            await postgresStore.SaveAsync(sanitized, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Remote settings persistence unavailable. Using local cache.");
        }
    }

    private async Task<AppSettings?> TryLoadRemoteAsync(CancellationToken cancellationToken)
    {
        try
        {
            var remote = await postgresStore.LoadAsync(cancellationToken);
            return IsMeaningful(remote) ? remote : null;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Remote settings persistence unavailable. Falling back to SQLite cache.");
            return null;
        }
    }

    private async Task<AppSettings?> TryLoadSqliteAsync(CancellationToken cancellationToken)
    {
        try
        {
            var cached = await sqliteStore.LoadAsync(cancellationToken);
            return IsMeaningful(cached) ? cached : null;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "SQLite settings cache unavailable. Using protected local settings only.");
            return null;
        }
    }

    private async Task TrySaveSqliteAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            await sqliteStore.SaveAsync(settings, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "SQLite settings cache write failed.");
        }
    }

    private static bool IsMeaningful(AppSettings settings)
    {
        return !string.IsNullOrWhiteSpace(settings.UiLanguage)
            || !string.IsNullOrWhiteSpace(settings.Organization.RootDirectory)
            || !string.IsNullOrWhiteSpace(settings.Gemini.Model)
            || settings.Organization.IncludePatterns.Count > 0
            || settings.Organization.ExcludePatterns.Count > 0;
    }
}
