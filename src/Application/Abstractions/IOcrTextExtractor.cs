using FileTransformer.Application.Models;

namespace FileTransformer.Application.Abstractions;

public interface IOcrTextExtractor
{
    Task<OcrTextExtractionResult> TryExtractAsync(
        string fullPath,
        CancellationToken cancellationToken);
}
