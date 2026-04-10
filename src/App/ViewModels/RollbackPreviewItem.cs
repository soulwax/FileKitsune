namespace FileTransformer.App.ViewModels;

public sealed class RollbackPreviewItem
{
    public DateTimeOffset ExecutedAtLocal { get; init; }

    public string SourceRelativePath { get; init; } = string.Empty;

    public string DestinationRelativePath { get; init; } = string.Empty;

    public string Outcome { get; init; } = string.Empty;

    public string Notes { get; init; } = string.Empty;

    public string PreviewStatus { get; init; } = string.Empty;

    public string PreviewMessage { get; init; } = string.Empty;
}
