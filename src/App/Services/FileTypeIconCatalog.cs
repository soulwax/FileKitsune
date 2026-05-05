using System.IO;
using MahApps.Metro.IconPacks;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace FileTransformer.App.Services;

public static class FileTypeIconCatalog
{
    private static readonly MediaBrush PdfBrush = CreateBrush(0xD9, 0x2D, 0x20);
    private static readonly MediaBrush DocumentBrush = CreateBrush(0x2B, 0x6C, 0xC4);
    private static readonly MediaBrush SpreadsheetBrush = CreateBrush(0x2F, 0x85, 0x54);
    private static readonly MediaBrush PresentationBrush = CreateBrush(0xDD, 0x6B, 0x20);
    private static readonly MediaBrush ImageBrush = CreateBrush(0x80, 0x56, 0xD6);
    private static readonly MediaBrush AudioBrush = CreateBrush(0xB8, 0x32, 0x80);
    private static readonly MediaBrush VideoBrush = CreateBrush(0x31, 0x80, 0x9B);
    private static readonly MediaBrush CodeBrush = CreateBrush(0x2D, 0x37, 0x48);
    private static readonly MediaBrush DataBrush = CreateBrush(0x37, 0x4B, 0x8A);
    private static readonly MediaBrush ArchiveBrush = CreateBrush(0x71, 0x80, 0x96);
    private static readonly MediaBrush TextBrush = CreateBrush(0x4A, 0x55, 0x68);
    private static readonly MediaBrush DefaultBrush = CreateBrush(0x63, 0x6B, 0x7A);

    public static FileTypeIconDescriptor Resolve(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => new(PackIconFileIconsKind.AdobeAcrobat, PdfBrush),
            ".doc" or ".docx" or ".rtf" => new(PackIconFileIconsKind.Word, DocumentBrush),
            ".xls" or ".xlsx" or ".ods" => new(PackIconFileIconsKind.Excel, SpreadsheetBrush),
            ".ppt" or ".pptx" or ".key" => new(PackIconFileIconsKind.Powerpoint, PresentationBrush),
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".tif" or ".tiff" or ".heic" or ".heif" => new(PackIconFileIconsKind.Image, ImageBrush),
            ".mp3" or ".wav" or ".flac" or ".aiff" or ".ogg" or ".als" => new(PackIconFileIconsKind.Audacity, AudioBrush),
            ".mp4" or ".mov" or ".avi" or ".mkv" or ".webm" => new(PackIconFileIconsKind.Video, VideoBrush),
            ".zip" or ".7z" or ".rar" or ".tar" or ".gz" or ".bz2" => new(PackIconFileIconsKind.Brotli, ArchiveBrush),
            ".json" or ".jsonl" or ".jsonld" => new(PackIconFileIconsKind.Json1, DataBrush),
            ".xml" or ".xaml" => new(PackIconFileIconsKind.Config, DataBrush),
            ".csv" or ".tsv" => new(PackIconFileIconsKind.Excel, DataBrush),
            ".md" or ".markdown" => new(PackIconFileIconsKind.Markdownlint, TextBrush),
            ".txt" or ".log" => new(PackIconFileIconsKind.Textile, TextBrush),
            ".cs" => new(PackIconFileIconsKind.CSharp, CodeBrush),
            ".csproj" or ".sln" or ".slnx" => new(PackIconFileIconsKind.CSharp, CodeBrush),
            ".js" or ".jsx" => new(PackIconFileIconsKind.Jsx, CodeBrush),
            ".ts" or ".tsx" => new(PackIconFileIconsKind.Typescript, CodeBrush),
            ".html" or ".htm" => new(PackIconFileIconsKind.Fthtml, CodeBrush),
            ".css" => new(PackIconFileIconsKind.Postcss, CodeBrush),
            ".sql" or ".sqlite" or ".db" => new(PackIconFileIconsKind.Sqlite, DataBrush),
            ".py" => new(PackIconFileIconsKind.ConfigPython, CodeBrush),
            ".java" => new(PackIconFileIconsKind.Source, CodeBrush),
            ".cpp" or ".c" or ".h" => new(PackIconFileIconsKind.Cpp, CodeBrush),
            ".yaml" or ".yml" => new(PackIconFileIconsKind.Yaml, DataBrush),
            ".epub" or ".mobi" or ".azw" or ".azw3" or ".fb2" or ".djvu" or ".djv" => new(PackIconFileIconsKind.Docbook, DocumentBrush),
            _ => new(PackIconFileIconsKind.Default, DefaultBrush)
        };
    }

    private static MediaSolidColorBrush CreateBrush(byte red, byte green, byte blue)
    {
        var brush = new MediaSolidColorBrush(MediaColor.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
}
