using MahApps.Metro.IconPacks;
using MediaBrush = System.Windows.Media.Brush;

namespace FileTransformer.App.Services;

public sealed record FileTypeIconDescriptor(PackIconFileIconsKind Kind, MediaBrush Brush);
