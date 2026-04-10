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
        ".pdf",
        ".docx"
    ];

    public bool RemoveEmptyFolders { get; set; }

    public int PreviewSampleSize { get; set; } = 500;

    public int MaxFilesToScan { get; set; } = 5_000;

    public bool UseGeminiWhenAvailable { get; set; } = true;

    public int MaxConcurrentClassification { get; set; } = 4;

    public bool KeepOperationsInsideRoot { get; set; } = true;

    public OrganizationPolicy OrganizationPolicy { get; set; } = new();

    public NamingPolicy NamingPolicy { get; set; } = new();

    public ReviewPolicy ReviewPolicy { get; set; } = new();

    public DuplicatePolicy DuplicatePolicy { get; set; } = new();

    public ProtectionPolicy ProtectionPolicy { get; set; } = new();

    public bool AllowFileRename
    {
        get => NamingPolicy.AllowFileRename;
        set => NamingPolicy.AllowFileRename = value;
    }

    public FileRenameMode FileRenameMode
    {
        get => NamingPolicy.FileRenameMode;
        set => NamingPolicy.FileRenameMode = value;
    }

    public string NamingTemplate
    {
        get => NamingPolicy.NamingTemplate;
        set => NamingPolicy.NamingTemplate = value;
    }

    public FolderLanguageMode FolderLanguageMode
    {
        get => NamingPolicy.FolderLanguageMode;
        set => NamingPolicy.FolderLanguageMode = value;
    }

    public ConflictHandlingMode ConflictHandlingMode
    {
        get => NamingPolicy.ConflictHandlingMode;
        set => NamingPolicy.ConflictHandlingMode = value;
    }

    public FilenameLanguagePolicy FilenameLanguagePolicy
    {
        get => NamingPolicy.FilenameLanguagePolicy;
        set => NamingPolicy.FilenameLanguagePolicy = value;
    }

    public OrganizationDimension Dimensions
    {
        get => OrganizationPolicy.ManualDimensions;
        set => OrganizationPolicy.ManualDimensions = value;
    }

    public bool UseFileTypeAsSecondaryCriterion
    {
        get => OrganizationPolicy.UseFileTypeAsSecondaryCriterion;
        set => OrganizationPolicy.UseFileTypeAsSecondaryCriterion = value;
    }

    public OrganizationStrategyPreset StrategyPreset
    {
        get => OrganizationPolicy.StrategyPreset;
        set => OrganizationPolicy.StrategyPreset = value;
    }

    public DateSourceKind PreferredDateSource
    {
        get => OrganizationPolicy.PreferredDateSource;
        set => OrganizationPolicy.PreferredDateSource = value;
    }

    public double LowConfidenceThreshold
    {
        get => ReviewPolicy.LowConfidenceThreshold;
        set => ReviewPolicy.LowConfidenceThreshold = value;
    }

    public ExecutionMode ExecutionMode
    {
        get => ReviewPolicy.ExecutionMode;
        set => ReviewPolicy.ExecutionMode = value;
    }

    public bool SuggestOnlyOnLowConfidence
    {
        get => ReviewPolicy.SuggestOnlyOnLowConfidence;
        set => ReviewPolicy.SuggestOnlyOnLowConfidence = value;
    }

    public bool PreferGeminiFolderSuggestion
    {
        get => OrganizationPolicy.PreferGeminiFolderSuggestion;
        set => OrganizationPolicy.PreferGeminiFolderSuggestion = value;
    }

    public DuplicateHandlingMode DuplicateHandlingMode
    {
        get => DuplicatePolicy.HandlingMode;
        set => DuplicatePolicy.HandlingMode = value;
    }
}
