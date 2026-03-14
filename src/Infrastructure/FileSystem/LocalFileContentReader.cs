using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FileTransformer.Application.Abstractions;
using FileTransformer.Domain.Models;
using Microsoft.Extensions.Logging;

namespace FileTransformer.Infrastructure.FileSystem;

public sealed class LocalFileContentReader : IFileContentReader
{
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
            return file.Extension.Equals(".docx", StringComparison.OrdinalIgnoreCase)
                ? await ReadDocxAsync(file.FullPath, cancellationToken)
                : await ReadTextAsync(file.FullPath, cancellationToken);
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
        var truncated = text.Length > MaxExtractedCharacters;
        if (truncated)
        {
            text = text[..MaxExtractedCharacters];
        }

        return new FileContentSnapshot
        {
            Text = text,
            IsTextReadable = true,
            IsTruncated = truncated,
            ExtractionSource = "text"
        };
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

            var truncated = text.Length > MaxExtractedCharacters;
            if (truncated)
            {
                text = text[..MaxExtractedCharacters];
            }

            return new FileContentSnapshot
            {
                Text = text,
                IsTextReadable = true,
                IsTruncated = truncated,
                ExtractionSource = "docx"
            };
        }, cancellationToken);
    }
}
