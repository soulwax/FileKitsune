namespace FileTransformer.Application.Models;

public sealed class EnvironmentSanityItem
{
    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string ValuePreview { get; init; } = string.Empty;

    public string SourceLabel { get; init; } = string.Empty;

    public EnvironmentSanityStatus Status { get; init; }

    public string Message { get; init; } = string.Empty;
}
