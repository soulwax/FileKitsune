namespace FileTransformer.App.ViewModels;

public sealed class EnvironmentSanityItemViewModel
{
    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string ValuePreview { get; init; } = string.Empty;

    public string SourceLabel { get; init; } = string.Empty;

    public string StatusLabel { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}
