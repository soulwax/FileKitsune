using FileTransformer.Application.Abstractions;
using FileTransformer.Application.Models;

namespace FileTransformer.Infrastructure.Configuration;

public sealed class EnvironmentConfigService : IEnvironmentConfigService
{
    private static readonly string[] ManagedKeys =
    [
        "GEMINI_ENABLED",
        "GEMINI_API_KEY",
        "GEMINI_MODEL",
        "GEMINI_ENDPOINT_BASE_URL",
        "GEMINI_MAX_REQUESTS_PER_MINUTE",
        "GEMINI_REQUEST_TIMEOUT_SECONDS",
        "GEMINI_MAX_PROMPT_CHARACTERS",
        "FILEKITSUNE_OFFLINE_MODE"
    ];

    public Task<EnvironmentFileSettings> LoadAsync(CancellationToken cancellationToken)
    {
        var path = AppEnvironmentPaths.ResolveWritableEnvPath();
        var values = DotEnv.LoadIfPresent(path);

        return Task.FromResult(new EnvironmentFileSettings
        {
            FilePath = path,
            GeminiEnabled = GetValue(values, "GEMINI_ENABLED"),
            GeminiApiKey = GetValue(values, "GEMINI_API_KEY"),
            GeminiModel = GetValue(values, "GEMINI_MODEL"),
            GeminiEndpointBaseUrl = GetValue(values, "GEMINI_ENDPOINT_BASE_URL"),
            GeminiMaxRequestsPerMinute = GetValue(values, "GEMINI_MAX_REQUESTS_PER_MINUTE"),
            GeminiRequestTimeoutSeconds = GetValue(values, "GEMINI_REQUEST_TIMEOUT_SECONDS"),
            GeminiMaxPromptCharacters = GetValue(values, "GEMINI_MAX_PROMPT_CHARACTERS"),
            FileKitsuneOfflineMode = GetValue(values, "FILEKITSUNE_OFFLINE_MODE")
        });
    }

    public async Task SaveAsync(EnvironmentFileSettings settings, CancellationToken cancellationToken)
    {
        var path = string.IsNullOrWhiteSpace(settings.FilePath) ? AppEnvironmentPaths.ResolveWritableEnvPath() : settings.FilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var lines = File.Exists(path)
            ? File.ReadAllLines(path).ToList()
            : [];

        var desiredValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["GEMINI_ENABLED"] = settings.GeminiEnabled,
            ["GEMINI_API_KEY"] = settings.GeminiApiKey,
            ["GEMINI_MODEL"] = settings.GeminiModel,
            ["GEMINI_ENDPOINT_BASE_URL"] = settings.GeminiEndpointBaseUrl,
            ["GEMINI_MAX_REQUESTS_PER_MINUTE"] = settings.GeminiMaxRequestsPerMinute,
            ["GEMINI_REQUEST_TIMEOUT_SECONDS"] = settings.GeminiRequestTimeoutSeconds,
            ["GEMINI_MAX_PROMPT_CHARACTERS"] = settings.GeminiMaxPromptCharacters,
            ["FILEKITSUNE_OFFLINE_MODE"] = settings.FileKitsuneOfflineMode
        };

        var handledKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < lines.Count; index++)
        {
            if (!TryGetKey(lines[index], out var key) || !desiredValues.ContainsKey(key))
            {
                continue;
            }

            lines[index] = $"{key}={Quote(desiredValues[key])}";
            handledKeys.Add(key);
        }

        if (lines.Count > 0 && lines[^1].Length > 0)
        {
            lines.Add(string.Empty);
        }

        if (handledKeys.Count < ManagedKeys.Length)
        {
            lines.Add("# FileKitsune env settings");
        }

        foreach (var key in ManagedKeys)
        {
            if (handledKeys.Contains(key))
            {
                continue;
            }

            lines.Add($"{key}={Quote(desiredValues[key])}");
        }

        await File.WriteAllLinesAsync(path, lines, cancellationToken);
    }

    private static string GetValue(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : string.Empty;

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    private static bool TryGetKey(string line, out string key)
    {
        key = string.Empty;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var trimmed = line.Trim();
        if (trimmed.StartsWith('#') || trimmed.StartsWith(';'))
        {
            return false;
        }

        if (trimmed.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[7..].Trim();
        }

        var separatorIndex = trimmed.IndexOf('=', StringComparison.Ordinal);
        if (separatorIndex <= 0)
        {
            return false;
        }

        key = trimmed[..separatorIndex].Trim();
        return !string.IsNullOrWhiteSpace(key);
    }
}
