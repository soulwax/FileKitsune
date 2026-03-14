namespace FileTransformer.Domain.Models;

public sealed class SemanticCategoryDefinition
{
    public required string Key { get; init; }

    public required string EnglishLabel { get; init; }

    public required string GermanLabel { get; init; }
}
