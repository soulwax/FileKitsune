using FileTransformer.Domain.Enums;

namespace FileTransformer.Domain.Models;

public sealed class NamingPolicy
{
    public bool AllowFileRename { get; set; } = true;

    public FileRenameMode FileRenameMode { get; set; } = FileRenameMode.NormalizeWhitespaceAndPunctuation;

    public string NamingTemplate { get; set; } = "{originalName}";

    public FolderLanguageMode FolderLanguageMode { get; set; } = FolderLanguageMode.PreserveOriginal;

    public FilenameLanguagePolicy FilenameLanguagePolicy { get; set; } = FilenameLanguagePolicy.PreserveSourceLanguage;

    public ConflictHandlingMode ConflictHandlingMode { get; set; } = ConflictHandlingMode.AppendCounter;

    public bool PreserveDomainSpecificTerms { get; set; } = true;

    public int MaximumFileNameLength { get; set; } = 120;

    public bool AddDatePrefix { get; set; }

    public bool CleanupNoiseTokens { get; set; }

    public bool PreserveUmlauts { get; set; } = true;
}
