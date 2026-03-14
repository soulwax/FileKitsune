namespace FileTransformer.Domain.Models;

public sealed class SemanticAnalysisRequest
{
    public required ScannedFile File { get; init; }

    public required FileContentSnapshot Content { get; init; }

    public string MinimalMetadataSummary =>
        $"{File.FileName} | {File.Extension} | {File.RelativeDirectoryPath} | {File.ModifiedUtc:yyyy-MM-dd}";
}
