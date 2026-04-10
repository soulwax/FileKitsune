using System.Text;
using FileTransformer.Domain.Models;
using FileTransformer.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FileTransformer.Tests.Infrastructure;

public sealed class LocalFileContentReaderTests
{
    [Fact]
    public async Task ReadAsync_ExtractsTextFromPdf()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        try
        {
            await File.WriteAllBytesAsync(path, CreateSimplePdf("Hallo PDF Welt"));

            var reader = new LocalFileContentReader(NullLogger<LocalFileContentReader>.Instance);
            var snapshot = await reader.ReadAsync(
                new ScannedFile
                {
                    FullPath = path,
                    RelativePath = "sample.pdf",
                    FileName = "sample.pdf",
                    Extension = ".pdf",
                    SizeBytes = new FileInfo(path).Length
                },
                new OrganizationSettings(),
                CancellationToken.None);

            Assert.True(snapshot.IsTextReadable);
            Assert.Equal("pdf", snapshot.ExtractionSource);
            Assert.Contains("Hallo PDF Welt", snapshot.Text);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task ReadAsync_ReturnsPdfEmptyWhenNoTextCanBeExtracted()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        try
        {
            await File.WriteAllBytesAsync(path, CreateSimplePdf(string.Empty));

            var reader = new LocalFileContentReader(NullLogger<LocalFileContentReader>.Instance);
            var snapshot = await reader.ReadAsync(
                new ScannedFile
                {
                    FullPath = path,
                    RelativePath = "empty.pdf",
                    FileName = "empty.pdf",
                    Extension = ".pdf",
                    SizeBytes = new FileInfo(path).Length
                },
                new OrganizationSettings(),
                CancellationToken.None);

            Assert.False(snapshot.IsTextReadable);
            Assert.Equal("pdf-empty", snapshot.ExtractionSource);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task ReadAsync_ReturnsReadFailedWhenPdfIsInvalid()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        try
        {
            await File.WriteAllTextAsync(path, "this is not a real pdf");

            var reader = new LocalFileContentReader(NullLogger<LocalFileContentReader>.Instance);
            var snapshot = await reader.ReadAsync(
                new ScannedFile
                {
                    FullPath = path,
                    RelativePath = "broken.pdf",
                    FileName = "broken.pdf",
                    Extension = ".pdf",
                    SizeBytes = new FileInfo(path).Length
                },
                new OrganizationSettings(),
                CancellationToken.None);

            Assert.False(snapshot.IsTextReadable);
            Assert.Equal("read-failed", snapshot.ExtractionSource);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task ReadAsync_SamplesLargeTextFilesFromStartAndEnd()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        var start = new string('A', 7_000);
        var tail = new string('Z', 7_000);

        try
        {
            await File.WriteAllTextAsync(path, $"{start}{tail}");

            var reader = new LocalFileContentReader(NullLogger<LocalFileContentReader>.Instance);
            var snapshot = await reader.ReadAsync(
                new ScannedFile
                {
                    FullPath = path,
                    RelativePath = "large.txt",
                    FileName = "large.txt",
                    Extension = ".txt",
                    SizeBytes = new FileInfo(path).Length
                },
                new OrganizationSettings(),
                CancellationToken.None);

            Assert.True(snapshot.IsTextReadable);
            Assert.True(snapshot.IsTruncated);
            Assert.Contains(new string('A', 256), snapshot.Text);
            Assert.Contains(new string('Z', 256), snapshot.Text);
            Assert.Contains("...", snapshot.Text);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static byte[] CreateSimplePdf(string text)
    {
        var content = string.IsNullOrEmpty(text)
            ? "BT\n/F1 24 Tf\n72 100 Td\nET\n"
            : $"BT\n/F1 24 Tf\n72 100 Td\n({EscapePdfText(text)}) Tj\nET\n";

        var objects = new[]
        {
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
            "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n",
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 144] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>\nendobj\n",
            $"4 0 obj\n<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}endstream\nendobj\n",
            "5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n"
        };

        var builder = new StringBuilder();
        builder.Append("%PDF-1.4\n");
        var offsets = new List<int> { 0 };

        foreach (var obj in objects)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append(obj);
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append($"xref\n0 {objects.Length + 1}\n");
        builder.Append("0000000000 65535 f \n");
        for (var i = 1; i < offsets.Count; i++)
        {
            builder.Append($"{offsets[i]:D10} 00000 n \n");
        }

        builder.Append("trailer\n");
        builder.Append($"<< /Size {objects.Length + 1} /Root 1 0 R >>\n");
        builder.Append("startxref\n");
        builder.Append($"{xrefOffset}\n");
        builder.Append("%%EOF");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static string EscapePdfText(string text) =>
        text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}
