using FileTransformer.Domain.Enums;

namespace FileTransformer.Domain.Models;

public sealed class DuplicatePolicy
{
    public bool EnableExactDuplicateDetection { get; set; }

    public DuplicateHandlingMode HandlingMode { get; set; } = DuplicateHandlingMode.RequireReview;

    public string DuplicatesFolderName { get; set; } = "Duplicates";
}
