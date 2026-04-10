namespace FileTransformer.Application.Models;

public sealed class PersistenceStatusSnapshot
{
    public PersistenceStatusMode Mode { get; init; } = PersistenceStatusMode.LocalOnly;

    public string PrimaryStore { get; init; } = string.Empty;

    public string SecondaryStore { get; init; } = string.Empty;

    public string DetailKey { get; init; } = string.Empty;
}
