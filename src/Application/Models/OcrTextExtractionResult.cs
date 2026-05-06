namespace FileTransformer.Application.Models;

public sealed class OcrTextExtractionResult
{
    public bool Succeeded { get; init; }

    public string Text { get; init; } = string.Empty;

    public double? Confidence { get; init; }

    public string Source { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}
