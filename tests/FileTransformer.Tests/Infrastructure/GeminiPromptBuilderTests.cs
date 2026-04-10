using FileTransformer.Domain.Models;
using FileTransformer.Infrastructure.Classification;
using Xunit;

namespace FileTransformer.Tests.Infrastructure;

public sealed class GeminiPromptBuilderTests
{
    [Fact]
    public void BuildPrompt_PrioritizesEarlyParagraphsOverLateText()
    {
        var builder = new GeminiPromptBuilder();

        var prompt = builder.BuildPrompt(
            new SemanticAnalysisRequest
            {
                File = new ScannedFile
                {
                    FullPath = @"C:\Root\Library\sample.epub",
                    RelativePath = @"Library\sample.epub",
                    RelativeDirectoryPath = "Library",
                    FileName = "sample.epub",
                    Extension = ".epub",
                    ModifiedUtc = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    SizeBytes = 10_000
                },
                Content = new FileContentSnapshot
                {
                    IsTextReadable = true,
                    ExtractionSource = "text",
                    Text = string.Join(
                        Environment.NewLine + Environment.NewLine,
                        "First paragraph with the actual topic and category clues.",
                        "Second paragraph still relevant for classification.",
                        "Third paragraph still okay.",
                        "Late appendix that should not dominate the prompt.",
                        new string('x', 1200))
                }
            },
            1_600);

        Assert.Contains("First paragraph with the actual topic", prompt, StringComparison.Ordinal);
        Assert.Contains("Second paragraph still relevant", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Late appendix that should not dominate", prompt, StringComparison.Ordinal);
    }
}
