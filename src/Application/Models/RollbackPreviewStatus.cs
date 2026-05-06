namespace FileTransformer.Application.Models;

public enum RollbackPreviewStatus
{
    Ready = 0,
    MissingDestination = 1,
    OriginalPathOccupied = 2,
    PendingNoMutation = 3
}
