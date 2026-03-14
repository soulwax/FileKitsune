namespace FileTransformer.Domain.Models;

public sealed class FileContentSnapshot
{
    public string Text { get; init; } = string.Empty;

    public bool IsTextReadable { get; init; }

    public bool IsTruncated { get; init; }

    public string ExtractionSource { get; init; } = string.Empty;

    public int CharacterCount => Text.Length;
}
