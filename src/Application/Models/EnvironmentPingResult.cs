namespace FileTransformer.Application.Models;

public sealed class EnvironmentPingResult
{
    public EnvironmentSanityStatus Status { get; init; }

    public string Message { get; init; } = string.Empty;

    public string SuccessfulFingerprint { get; init; } = string.Empty;

    public DateTimeOffset? SuccessfulAtUtc { get; init; }
}
