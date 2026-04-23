namespace FileTransformer.Application.Models;

public sealed class EnvironmentPingAvailability
{
    public bool CanPing { get; init; }

    public string Message { get; init; } = string.Empty;

    public string CurrentFingerprint { get; init; } = string.Empty;

    public DateTimeOffset? NextAllowedAtUtc { get; init; }
}
