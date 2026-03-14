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
        services.AddSingleton<GeminiPromptBuilder>();
        services.AddSingleton<GeminiResponseParser>();
        services.AddSingleton<IAppSettingsStore, ProtectedAppSettingsStore>();
        services.AddSingleton<IExecutionJournalStore, JsonExecutionJournalStore>();
        services.AddSingleton<IFileScanner, LocalFileScanner>();
        services.AddSingleton<IFileContentReader, LocalFileContentReader>();
        services.AddSingleton<IFileOperations, LocalFileOperations>();
        services.AddSingleton<IFileHashProvider, Sha256FileHashProvider>();
        services.AddSingleton<IGeminiSemanticClassifier, GeminiSemanticClassifier>();
        services.AddHttpClient("GeminiClassifier");

        return services;
    }
}
