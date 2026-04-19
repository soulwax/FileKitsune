using System.Collections.Generic;

namespace FileTransformer.App.ViewModels;

public sealed class RollbackPreviewSectionItem
{
    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string CountLabel { get; init; } = string.Empty;

    public IReadOnlyList<RollbackPreviewItem> Entries { get; init; } = [];

    public string RemainingLabel { get; init; } = string.Empty;

    public bool HasRemainingLabel => !string.IsNullOrWhiteSpace(RemainingLabel);
}
