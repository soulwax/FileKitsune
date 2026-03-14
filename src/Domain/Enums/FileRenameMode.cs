namespace FileTransformer.Domain.Enums;

public enum FileRenameMode
{
    KeepOriginal = 0,
    NormalizeWhitespaceAndPunctuation = 1,
    SuggestCleanNames = 2
}
