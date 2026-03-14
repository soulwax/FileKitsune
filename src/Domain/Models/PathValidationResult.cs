namespace FileTransformer.Domain.Models;

public sealed class PathValidationResult
{
    public bool IsValid { get; init; }

    public string NormalizedRelativePath { get; init; } = string.Empty;

    public List<string> Errors { get; init; } = [];
}
