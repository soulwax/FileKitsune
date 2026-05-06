using System.Buffers.Binary;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FileTransformer.Application.Abstractions;
using FileTransformer.Domain.Models;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace FileTransformer.Infrastructure.FileSystem;

public sealed class LocalFileContentReader : IFileContentReader
{
    private const int MaxExtractedCharacters = 12_000;
    private const int MaxImageHeaderBytes = 512 * 1024;
    private static readonly HashSet<string> ImageExtensions =
    [
        ".jpg",
        ".jpeg",
        ".png",
        ".gif",
        ".bmp",
        ".webp",
        ".tif",
        ".tiff",
        ".heic",
        ".heif"
    ];

    private readonly ILogger<LocalFileContentReader> logger;
    private readonly IOcrTextExtractor? ocrTextExtractor;

    public LocalFileContentReader(
        ILogger<LocalFileContentReader> logger,
        IOcrTextExtractor? ocrTextExtractor = null)
    {
        this.logger = logger;
        this.ocrTextExtractor = ocrTextExtractor;
    }

    public async Task<FileContentSnapshot> ReadAsync(
        ScannedFile file,
        OrganizationSettings settings,
        CancellationToken cancellationToken)
    {
        if (!settings.SupportedContentExtensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase))
        {
            return new FileContentSnapshot
            {
                ExtractionSource = "metadata-only",
                IsTextReadable = false
            };
        }

        if (file.SizeBytes > settings.MaxFileSizeForContentInspectionBytes)
        {
            return new FileContentSnapshot
            {
                ExtractionSource = "size-limit",
                IsTextReadable = false
            };
        }

        try
        {
            return file.Extension.ToLowerInvariant() switch
            {
                ".docx" => await ReadDocxAsync(file.FullPath, cancellationToken),
                ".pdf" => await ReadPdfAsync(file.FullPath, cancellationToken),
                var extension when IsImageExtension(extension) => await ReadImageAsync(file.FullPath, extension, cancellationToken),
                _ => await ReadTextAsync(file.FullPath, cancellationToken)
            };
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not read file content for {File}", file.RelativePath);
            return new FileContentSnapshot
            {
                ExtractionSource = "read-failed",
                IsTextReadable = false
            };
        }
    }

    private static async Task<FileContentSnapshot> ReadTextAsync(string fullPath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        var (sampledText, truncated) = SampleText(text);

        return new FileContentSnapshot
        {
            Text = sampledText,
            IsTextReadable = true,
            IsTruncated = truncated,
            ExtractionSource = "text"
        };
    }

    private async Task<FileContentSnapshot> ReadPdfAsync(string fullPath, CancellationToken cancellationToken)
    {
        var snapshot = await ReadPdfWithoutOcrAsync(fullPath, cancellationToken);
        if (snapshot.IsTextReadable)
        {
            return snapshot;
        }

        return await TryApplyOcrAsync(fullPath, snapshot, cancellationToken);
    }

    private static Task<FileContentSnapshot> ReadPdfWithoutOcrAsync(string fullPath, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var document = PdfDocument.Open(fullPath);
            var builder = new StringBuilder();

            var pageCount = 0;
            var imageCount = 0;

            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                pageCount++;

                var text = page.Text;
                if (string.IsNullOrWhiteSpace(text))
                {
                    imageCount += page.GetImages().Count();
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(text.Trim());
                if (builder.Length >= MaxExtractedCharacters)
                {
                    break;
                }
            }

            var extractedText = builder.ToString().Trim();
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                if (imageCount > 0)
                {
                    return new FileContentSnapshot
                    {
                        Text = BuildPdfImageOnlySignal(pageCount, imageCount),
                        ExtractionSource = "pdf-image-only",
                        IsTextReadable = false
                    };
                }

                return new FileContentSnapshot
                {
                    ExtractionSource = "pdf-empty",
                    IsTextReadable = false
                };
            }

            var (sampledText, truncated) = SampleText(extractedText);

            return new FileContentSnapshot
            {
                Text = sampledText,
                IsTextReadable = true,
                IsTruncated = truncated,
                ExtractionSource = "pdf"
            };
        }, cancellationToken);
    }

    private async Task<FileContentSnapshot> ReadImageAsync(
        string fullPath,
        string extension,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true);
        var bytesToRead = (int)Math.Min(stream.Length, MaxImageHeaderBytes);
        var buffer = new byte[bytesToRead];
        var totalRead = 0;

        while (totalRead < bytesToRead)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, bytesToRead - totalRead), cancellationToken);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        if (totalRead != buffer.Length)
        {
            Array.Resize(ref buffer, totalRead);
        }

        var metadata = ImageMetadataReader.TryRead(buffer);
        var text = metadata is null
            ? BuildImageSignal(extension, null, null, null)
            : BuildImageSignal(metadata.Format, metadata.Width, metadata.Height, metadata.Orientation);

        var snapshot = new FileContentSnapshot
        {
            Text = text,
            ExtractionSource = metadata is null ? "image-metadata-unknown" : "image-metadata",
            IsTextReadable = false
        };

        return await TryApplyOcrAsync(fullPath, snapshot, cancellationToken);
    }

    private static Task<FileContentSnapshot> ReadDocxAsync(string fullPath, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var document = WordprocessingDocument.Open(fullPath, false);
            var body = document.MainDocumentPart?.Document.Body;
            if (body is null)
            {
                return new FileContentSnapshot
                {
                    ExtractionSource = "docx-empty",
                    IsTextReadable = false
                };
            }

            var text = string.Join(
                Environment.NewLine,
                body.Descendants<Paragraph>().Select(paragraph => paragraph.InnerText));

            var (sampledText, truncated) = SampleText(text);

            return new FileContentSnapshot
            {
                Text = sampledText,
                IsTextReadable = true,
                IsTruncated = truncated,
                ExtractionSource = "docx"
            };
        }, cancellationToken);
    }

    private static (string Text, bool Truncated) SampleText(string text)
    {
        if (text.Length <= MaxExtractedCharacters)
        {
            return (text, false);
        }

        var separator = $"{Environment.NewLine}...{Environment.NewLine}";
        var availableCharacters = MaxExtractedCharacters - separator.Length;
        if (availableCharacters <= 0)
        {
            return (text[..MaxExtractedCharacters], true);
        }

        var headLength = availableCharacters / 2;
        var tailLength = availableCharacters - headLength;
        var head = text[..headLength].TrimEnd();
        var tail = text[^tailLength..].TrimStart();
        return ($"{head}{separator}{tail}", true);
    }

    private static bool IsImageExtension(string extension) => ImageExtensions.Contains(extension);

    private async Task<FileContentSnapshot> TryApplyOcrAsync(
        string fullPath,
        FileContentSnapshot fallback,
        CancellationToken cancellationToken)
    {
        if (ocrTextExtractor is null)
        {
            return fallback;
        }

        var ocr = await ocrTextExtractor.TryExtractAsync(fullPath, cancellationToken);
        if (!ocr.Succeeded || string.IsNullOrWhiteSpace(ocr.Text))
        {
            return new FileContentSnapshot
            {
                Text = fallback.Text,
                IsTextReadable = fallback.IsTextReadable,
                IsTruncated = fallback.IsTruncated,
                ExtractionSource = fallback.ExtractionSource,
                ExtractionConfidence = ocr.Confidence,
                ExtractionMessage = ocr.Message
            };
        }

        var combinedText = string.IsNullOrWhiteSpace(fallback.Text)
            ? ocr.Text
            : $"{fallback.Text}{Environment.NewLine}{Environment.NewLine}OCR text:{Environment.NewLine}{ocr.Text}";
        var (sampledText, truncated) = SampleText(combinedText);

        return new FileContentSnapshot
        {
            Text = sampledText,
            IsTextReadable = true,
            IsTruncated = fallback.IsTruncated || truncated,
            ExtractionSource = ocr.Source,
            ExtractionConfidence = ocr.Confidence,
            ExtractionMessage = ocr.Message
        };
    }

    private static string BuildPdfImageOnlySignal(int pageCount, int imageCount) =>
        $"Image-only or scanned PDF metadata. Pages: {pageCount}. Embedded images: {imageCount}. OCR may be needed if the pages contain document text.";

    private static string BuildImageSignal(string format, int? width, int? height, string? orientation)
    {
        var builder = new StringBuilder();
        builder.Append("Image-first file metadata. Format: ");
        builder.Append(format.TrimStart('.').ToUpperInvariant());
        builder.Append('.');

        if (width is > 0 && height is > 0)
        {
            builder.Append(" Dimensions: ");
            builder.Append(width.Value);
            builder.Append(" x ");
            builder.Append(height.Value);
            builder.Append(" pixels.");

            if (!string.IsNullOrWhiteSpace(orientation))
            {
                builder.Append(" Orientation: ");
                builder.Append(orientation);
                builder.Append('.');
            }
        }

        builder.Append(" Use filename, folder context, and date metadata for organization; OCR may be needed if the image contains document text.");
        return builder.ToString();
    }

    private sealed record ImageMetadataResult(string Format, int Width, int Height)
    {
        public string Orientation => Width == Height ? "square" : Width > Height ? "landscape" : "portrait";
    }

    private static class ImageMetadataReader
    {
        public static ImageMetadataResult? TryRead(byte[] bytes)
        {
            if (TryReadPng(bytes, out var pngWidth, out var pngHeight))
            {
                return new ImageMetadataResult("PNG", pngWidth, pngHeight);
            }

            if (TryReadJpeg(bytes, out var jpegWidth, out var jpegHeight))
            {
                return new ImageMetadataResult("JPEG", jpegWidth, jpegHeight);
            }

            if (TryReadGif(bytes, out var gifWidth, out var gifHeight))
            {
                return new ImageMetadataResult("GIF", gifWidth, gifHeight);
            }

            if (TryReadBmp(bytes, out var bmpWidth, out var bmpHeight))
            {
                return new ImageMetadataResult("BMP", bmpWidth, bmpHeight);
            }

            if (TryReadWebp(bytes, out var webpWidth, out var webpHeight))
            {
                return new ImageMetadataResult("WEBP", webpWidth, webpHeight);
            }

            return null;
        }

        private static bool TryReadPng(byte[] bytes, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (bytes.Length < 24 ||
                bytes[0] != 0x89 ||
                bytes[1] != 0x50 ||
                bytes[2] != 0x4E ||
                bytes[3] != 0x47 ||
                bytes[12] != 0x49 ||
                bytes[13] != 0x48 ||
                bytes[14] != 0x44 ||
                bytes[15] != 0x52)
            {
                return false;
            }

            width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
            height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
            return width > 0 && height > 0;
        }

        private static bool TryReadJpeg(byte[] bytes, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8)
            {
                return false;
            }

            var index = 2;
            while (index + 9 < bytes.Length)
            {
                while (index < bytes.Length && bytes[index] == 0xFF)
                {
                    index++;
                }

                if (index >= bytes.Length)
                {
                    return false;
                }

                var marker = bytes[index++];
                if (marker is 0xD9 or 0xDA)
                {
                    return false;
                }

                if (index + 2 > bytes.Length)
                {
                    return false;
                }

                var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(index, 2));
                if (segmentLength < 2 || index + segmentLength > bytes.Length)
                {
                    return false;
                }

                if (IsJpegStartOfFrame(marker))
                {
                    height = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(index + 3, 2));
                    width = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(index + 5, 2));
                    return width > 0 && height > 0;
                }

                index += segmentLength;
            }

            return false;
        }

        private static bool TryReadGif(byte[] bytes, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (bytes.Length < 10 ||
                bytes[0] != 0x47 ||
                bytes[1] != 0x49 ||
                bytes[2] != 0x46)
            {
                return false;
            }

            width = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(6, 2));
            height = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(8, 2));
            return width > 0 && height > 0;
        }

        private static bool TryReadBmp(byte[] bytes, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (bytes.Length < 26 || bytes[0] != 0x42 || bytes[1] != 0x4D)
            {
                return false;
            }

            width = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(18, 4));
            height = Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(22, 4)));
            return width > 0 && height > 0;
        }

        private static bool TryReadWebp(byte[] bytes, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (bytes.Length < 30 ||
                bytes[0] != 0x52 ||
                bytes[1] != 0x49 ||
                bytes[2] != 0x46 ||
                bytes[3] != 0x46 ||
                bytes[8] != 0x57 ||
                bytes[9] != 0x45 ||
                bytes[10] != 0x42 ||
                bytes[11] != 0x50)
            {
                return false;
            }

            if (bytes[12] == 0x56 && bytes[13] == 0x50 && bytes[14] == 0x38 && bytes[15] == 0x58)
            {
                width = 1 + bytes[24] + (bytes[25] << 8) + (bytes[26] << 16);
                height = 1 + bytes[27] + (bytes[28] << 8) + (bytes[29] << 16);
                return width > 0 && height > 0;
            }

            return false;
        }

        private static bool IsJpegStartOfFrame(byte marker) =>
            marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF;
    }
}
