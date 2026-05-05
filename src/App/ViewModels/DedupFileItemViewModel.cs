using CommunityToolkit.Mvvm.ComponentModel;
using FileTransformer.App.Services;
using MahApps.Metro.IconPacks;
using System.IO;
using MediaBrush = System.Windows.Media.Brush;

namespace FileTransformer.App.ViewModels;

public sealed partial class DedupFileItemViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isKeeper;

    public required string FullPath { get; init; }

    public required string RelativePath { get; init; }

    public long SizeBytes { get; init; }

    public DateTimeOffset ModifiedUtc { get; init; }

    public string FileName => Path.GetFileName(RelativePath);

    public PackIconFileIconsKind FileTypeIconKind => FileTypeIconCatalog.Resolve(RelativePath).Kind;

    public MediaBrush FileTypeIconBrush => FileTypeIconCatalog.Resolve(RelativePath).Brush;

    public DateTimeOffset ModifiedLocal => ModifiedUtc.ToLocalTime();

    public double SizeMb => SizeBytes / 1024d / 1024d;
}
