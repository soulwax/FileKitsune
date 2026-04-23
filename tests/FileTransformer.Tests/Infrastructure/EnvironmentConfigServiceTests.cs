using FileTransformer.Application.Models;
using FileTransformer.Infrastructure.Configuration;
using Xunit;

namespace FileTransformer.Tests.Infrastructure;

[Collection("EnvironmentVariables")]
public sealed class EnvironmentConfigServiceTests
{
    [Fact]
    public void ResolveWritableEnvPath_prefers_repo_root_from_nested_app_directory()
    {
        var rootPath = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(rootPath, ".env.example"), "GEMINI_API_KEY=\"\"");
            var appDirectory = Path.Combine(rootPath, "src", "App");
            Directory.CreateDirectory(appDirectory);

            var path = AppEnvironmentPaths.ResolveWritableEnvPath(appDirectory, appDirectory);

            Assert.Equal(Path.Combine(rootPath, ".env"), path);
        }
        finally
        {
            CleanupDirectory(rootPath);
        }
    }

    [Fact]
    public async Task EnvironmentConfigService_saves_and_loads_managed_values_from_repo_root_env()
    {
        var rootPath = CreateTempDirectory();
        var originalCurrentDirectory = Directory.GetCurrentDirectory();

        try
        {
            File.WriteAllText(Path.Combine(rootPath, ".env.example"), "GEMINI_API_KEY=\"\"");
            var appDirectory = Path.Combine(rootPath, "src", "App");
            Directory.CreateDirectory(appDirectory);
            Directory.SetCurrentDirectory(appDirectory);

            var service = new EnvironmentConfigService();
            var settings = new EnvironmentFileSettings
            {
                GeminiApiKey = "test-key",
                GeminiModel = "gemini-2.0-flash",
                GeminiEndpointBaseUrl = "https://generativelanguage.googleapis.com/v1beta",
                GeminiEnabled = "true",
                GeminiMaxRequestsPerMinute = "30",
                GeminiRequestTimeoutSeconds = "30",
                GeminiMaxPromptCharacters = "4000",
                FileKitsuneOfflineMode = "false"
            };

            await service.SaveAsync(settings, CancellationToken.None);
            var loaded = await service.LoadAsync(CancellationToken.None);

            Assert.Equal(Path.Combine(rootPath, ".env"), loaded.FilePath);
            Assert.Equal("test-key", loaded.GeminiApiKey);
            Assert.Equal("gemini-2.0-flash", loaded.GeminiModel);
            Assert.Equal("https://generativelanguage.googleapis.com/v1beta", loaded.GeminiEndpointBaseUrl);
            Assert.Equal("true", loaded.GeminiEnabled);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCurrentDirectory);
            CleanupDirectory(rootPath);
        }
    }

    [Fact]
    public async Task EnvironmentConfigService_preserves_unmanaged_lines_and_resolver_reads_saved_values_immediately()
    {
        var rootPath = CreateTempDirectory();
        var originalCurrentDirectory = Directory.GetCurrentDirectory();

        try
        {
            File.WriteAllLines(
                Path.Combine(rootPath, ".env"),
                [
                    "UNRELATED_KEY=\"keep-me\"",
                    "GEMINI_API_KEY=\"old-key\""
                ]);
            File.WriteAllText(Path.Combine(rootPath, ".env.example"), "GEMINI_API_KEY=\"\"");
            var appDirectory = Path.Combine(rootPath, "src", "App");
            Directory.CreateDirectory(appDirectory);
            Directory.SetCurrentDirectory(appDirectory);

            var service = new EnvironmentConfigService();
            await service.SaveAsync(new EnvironmentFileSettings
            {
                GeminiApiKey = "new-key",
                GeminiModel = "gemini-2.0-flash",
                GeminiEndpointBaseUrl = "https://generativelanguage.googleapis.com/v1beta"
            }, CancellationToken.None);

            var envText = await File.ReadAllTextAsync(Path.Combine(rootPath, ".env"));
            Assert.Contains("UNRELATED_KEY=\"keep-me\"", envText, StringComparison.Ordinal);
            Assert.Contains("GEMINI_API_KEY=\"new-key\"", envText, StringComparison.Ordinal);

            var resolver = new AppEnvironmentResolver(
                processEnvironment: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                dotEnvValues: null,
                currentDirectory: appDirectory,
                baseDirectory: appDirectory);

            var resolved = resolver.GetValue("GEMINI_API_KEY");
            Assert.NotNull(resolved);
            Assert.Equal(".env", resolved!.Source);
            Assert.Equal("new-key", resolved.Value);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCurrentDirectory);
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
        if (!Directory.Exists(rootPath))
        {
            return;
        }

        Directory.Delete(rootPath, recursive: true);
    }
}
