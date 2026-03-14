namespace FileTransformer.Application.Models;

public sealed class GeminiOptions
{
    public bool Enabled { get; set; } = true;

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gemini-1.5-flash";

    public string EndpointBaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

    public int MaxRequestsPerMinute { get; set; } = 30;

    public int RequestTimeoutSeconds { get; set; } = 30;

    public int MaxPromptCharacters { get; set; } = 4_000;
}
