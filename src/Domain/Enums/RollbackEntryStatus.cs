namespace FileTransformer.Domain.Enums;

public enum RollbackEntryStatus
{
    NotAttempted = 0,
    Restored = 1,
    SkippedMissingDestination = 2,
    SkippedOriginalPathOccupied = 3,
    Failed = 4,
    SkippedContentMismatch = 5,
    SkippedNoMutation = 6
}
