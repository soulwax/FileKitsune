using FileTransformer.Application.Models;
using FileTransformer.App.Services;
using MahApps.Metro.IconPacks;
using MediaBrush = System.Windows.Media.Brush;

namespace FileTransformer.App.ViewModels;

public sealed class RollbackPreviewItem
{
    public DateTimeOffset ExecutedAtLocal { get; init; }

    public string SourceRelativePath { get; init; } = string.Empty;

    public string DestinationRelativePath { get; init; } = string.Empty;

    public PackIconFileIconsKind FileTypeIconKind => FileTypeIconCatalog.Resolve(SourceRelativePath).Kind;

    public MediaBrush FileTypeIconBrush => FileTypeIconCatalog.Resolve(SourceRelativePath).Brush;

    public string Outcome { get; init; } = string.Empty;

    public string Notes { get; init; } = string.Empty;

    public string PreviewStatus { get; init; } = string.Empty;

    public string PreviewMessage { get; init; } = string.Empty;

    public RollbackPreviewStatus PreviewStatusKind { get; init; }

    public bool HasPreviewMessage => !string.IsNullOrWhiteSpace(PreviewMessage);

    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);
}
