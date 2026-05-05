using FileTransformer.Application.Abstractions;
using FileTransformer.Infrastructure.Classification;
using FileTransformer.Infrastructure.Configuration;
using FileTransformer.Infrastructure.FileSystem;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.Versioning;

namespace FileTransformer.Infrastructure;

[SupportedOSPlatform("windows")]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFileTransformerInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<AppStoragePaths>();
        services.AddSingleton<AppEnvironmentResolver>();
        services.AddSingleton<IEnvironmentConfigService, EnvironmentConfigService>();
        services.AddSingleton<PersistenceOptionsResolver>();
        services.AddSingleton<IEnvironmentSanityService, EnvironmentSanityService>();
        services.AddSingleton<IPersistenceStatusService, PersistenceStatusService>();
        services.AddSingleton<IGeminiOrganizationAdvisor, GeminiOrganizationAdvisor>();
        services.AddSingleton<GeminiPromptBuilder>();
        services.AddSingleton<GeminiOrganizationGuidancePromptBuilder>();
        services.AddSingleton<GeminiResponseParser>();
        services.AddSingleton<GeminiOrganizationGuidanceParser>();
        services.AddSingleton<ProtectedAppSettingsStore>();
        services.AddSingleton<SqliteAppSettingsStore>();
        services.AddSingleton<PostgresAppSettingsStore>();
        services.AddSingleton<IAppSettingsStore, ResilientAppSettingsStore>();
        services.AddSingleton<JsonExecutionJournalStore>();
        services.AddSingleton<SqliteExecutionJournalStore>();
        services.AddSingleton<PostgresExecutionJournalStore>();
        services.AddSingleton<IExecutionJournalStore, ResilientExecutionJournalStore>();
        services.AddSingleton<IFileScanner, LocalFileScanner>();
        services.AddSingleton<IFileContentReader, LocalFileContentReader>();
        services.AddSingleton<IFileOperations, LocalFileOperations>();
        services.AddSingleton<IFileHashProvider, Sha256FileHashProvider>();
        services.AddSingleton<IRecycleBinService, RecycleBinService>();
        services.AddSingleton<IDedupAuditStore, JsonDedupAuditStore>();
        services.AddSingleton<IGeminiSemanticClassifier, GeminiSemanticClassifier>();
        services.AddHttpClient("GeminiClassifier");

        return services;
    }
}
