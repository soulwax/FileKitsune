namespace FileTransformer.App.Services;

public sealed class UiLogEntry
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;

    public string Level { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}
