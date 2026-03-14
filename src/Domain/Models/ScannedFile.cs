namespace FileTransformer.Domain.Models;

public sealed class ScannedFile
{
    public string FullPath { get; init; } = string.Empty;

    public string RelativePath { get; init; } = string.Empty;

    public string RelativeDirectoryPath { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string Extension { get; init; } = string.Empty;

    public long SizeBytes { get; init; }

    public DateTimeOffset CreatedUtc { get; init; }

    public DateTimeOffset ModifiedUtc { get; init; }
}
