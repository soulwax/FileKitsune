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
    // TODO: Add OCR/image classification hooks for scanned PDFs and image-first folders.
    private const int MaxExtractedCharacters = 12_000;
    private readonly ILogger<LocalFileContentReader> logger;

    public LocalFileContentReader(ILogger<LocalFileContentReader> logger)
    {
        this.logger = logger;
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

    private static Task<FileContentSnapshot> ReadPdfAsync(string fullPath, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var document = PdfDocument.Open(fullPath);
            var builder = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var text = page.Text;
                if (string.IsNullOrWhiteSpace(text))
                {
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

        const string separator = $"{Environment.NewLine}...{Environment.NewLine}";
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
}
