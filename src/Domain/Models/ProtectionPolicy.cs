namespace FileTransformer.Domain.Models;

public sealed class ProtectionPolicy
{
    public List<string> ProtectedFolderPatterns { get; set; } = [];

    public List<string> ProtectedFilePatterns { get; set; } = [];

    public bool KeepRelatedFilesTogetherByBasename { get; set; } = true;

    public bool TreatProjectOrSessionFoldersAsAtomic { get; set; }

    public bool SkipHiddenOrSystemFiles { get; set; } = true;

    public bool FollowSymlinksOrJunctions { get; set; }
}
