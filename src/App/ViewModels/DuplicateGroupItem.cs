namespace FileTransformer.App.ViewModels;

public sealed class DuplicateGroupItem
{
    public string CanonicalRelativePath { get; init; } = string.Empty;

    public int DuplicateCount { get; init; }

    public string DuplicateList { get; init; } = string.Empty;

    public int PlannedDuplicateCount { get; init; }

    public string DisplayLabel =>
        PlannedDuplicateCount > 0
            ? $"{CanonicalRelativePath} ({PlannedDuplicateCount}/{DuplicateCount})"
            : $"{CanonicalRelativePath} ({DuplicateCount})";
}
