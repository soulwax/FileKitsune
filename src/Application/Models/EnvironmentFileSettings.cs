namespace FileTransformer.Application.Models;

public sealed class EnvironmentFileSettings
{
    public string FilePath { get; init; } = string.Empty;

    public string GeminiEnabled { get; set; } = string.Empty;

    public string GeminiApiKey { get; set; } = string.Empty;

    public string GeminiModel { get; set; } = string.Empty;

    public string GeminiEndpointBaseUrl { get; set; } = string.Empty;

    public string GeminiMaxRequestsPerMinute { get; set; } = string.Empty;

    public string GeminiRequestTimeoutSeconds { get; set; } = string.Empty;

    public string GeminiMaxPromptCharacters { get; set; } = string.Empty;

    public string FileKitsuneOfflineMode { get; set; } = string.Empty;
}
