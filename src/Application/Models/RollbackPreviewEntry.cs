namespace FileTransformer.Application.Models;

public sealed class RollbackPreviewEntry
{
    public DateTimeOffset ExecutedAtUtc { get; init; }

    public string SourceFullPath { get; init; } = string.Empty;

    public string DestinationFullPath { get; init; } = string.Empty;

    public string Outcome { get; init; } = string.Empty;

    public string Notes { get; init; } = string.Empty;

    public RollbackPreviewStatus PreviewStatus { get; init; }

    public string PreviewMessage { get; init; } = string.Empty;
}
