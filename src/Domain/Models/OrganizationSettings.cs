using FileTransformer.Domain.Enums;

namespace FileTransformer.Domain.Models;

public sealed class OrganizationSettings
{
    public string RootDirectory { get; set; } = string.Empty;

    public List<string> IncludePatterns { get; set; } = ["*"];

    public List<string> ExcludePatterns { get; set; } =
    [
        ".git",
        ".vs",
        "bin",
        "obj",
        "node_modules"
    ];

    public long MaxFileSizeForContentInspectionBytes { get; set; } = 512 * 1024;

    public List<string> SupportedContentExtensions { get; set; } =
    [
        ".txt",
        ".md",
        ".json",
        ".xml",
        ".csv",
        ".cs",
        ".csproj",
        ".sln",
        ".xaml",
        ".js",
        ".ts",
        ".tsx",
        ".jsx",
        ".html",
        ".css",
        ".sql",
        ".py",
        ".java",
        ".cpp",
        ".c",
        ".h",
        ".docx"
    ];

    public bool RemoveEmptyFolders { get; set; }

    public bool AllowFileRename { get; set; } = true;

    public FileRenameMode FileRenameMode { get; set; } = FileRenameMode.NormalizeWhitespaceAndPunctuation;

    public string NamingTemplate { get; set; } = "{originalName}";

    public FolderLanguageMode FolderLanguageMode { get; set; } = FolderLanguageMode.PreserveOriginal;

    public ConflictHandlingMode ConflictHandlingMode { get; set; } = ConflictHandlingMode.AppendCounter;

    public int PreviewSampleSize { get; set; } = 500;

    public int MaxFilesToScan { get; set; } = 5_000;

    public OrganizationDimension Dimensions { get; set; } =
        OrganizationDimension.SemanticCategory |
        OrganizationDimension.Project |
        OrganizationDimension.Year;

    public bool UseFileTypeAsSecondaryCriterion { get; set; } = true;

    public bool UseGeminiWhenAvailable { get; set; } = true;

    public int MaxConcurrentClassification { get; set; } = 4;

    public double LowConfidenceThreshold { get; set; } = 0.55d;

    public bool SuggestOnlyOnLowConfidence { get; set; } = true;

    public bool KeepOperationsInsideRoot { get; set; } = true;

    public bool PreferGeminiFolderSuggestion { get; set; } = true;
}
