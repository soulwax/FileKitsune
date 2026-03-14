namespace FileTransformer.Domain.Enums;

public enum DuplicateHandlingMode
{
    Skip = 0,
    RouteToDuplicatesFolder = 1,
    RequireReview = 2
}
