namespace FileTransformer.Application.Models;

public sealed class DedupQuarantineRestoreResult
{
    public required DedupQuarantineRecord Record { get; init; }

    public required string Status { get; init; }

    public string Message { get; init; } = string.Empty;

    public bool Restored => string.Equals(Status, "Restored", StringComparison.Ordinal);
}
