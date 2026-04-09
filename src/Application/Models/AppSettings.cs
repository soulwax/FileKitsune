using FileTransformer.Domain.Models;

namespace FileTransformer.Application.Models;

public sealed class AppSettings
{
    public string UiLanguage { get; set; } = "de-DE";

    public OrganizationSettings Organization { get; set; } = new();

    public GeminiOptions Gemini { get; set; } = new();
}
