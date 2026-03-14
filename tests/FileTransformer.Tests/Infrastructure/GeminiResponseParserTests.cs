using FileTransformer.Infrastructure.Classification;
using Xunit;

namespace FileTransformer.Tests.Infrastructure;

public sealed class GeminiResponseParserTests
{
    [Fact]
    public void ParseModelPayload_ReturnsStrictSemanticInsight()
    {
        var parser = new GeminiResponseParser();

        var insight = parser.ParseModelPayload(
            """
            {
              "category": "Invoices",
              "projectTopic": "Project Atlas",
              "detectedLanguageContext": "Mixed",
              "confidence": 0.81,
              "suggestedFolderPathFragment": "Invoices/Project Atlas",
              "explanation": "Invoice and Rechnung terms both indicate billing documents."
            }
            """);

        Assert.Equal("invoices", insight.CategoryKey);
        Assert.Equal("Project Atlas", insight.ProjectOrTopic);
        Assert.Equal("Invoices/Project Atlas", insight.SuggestedFolderFragment);
    }

    [Fact]
    public void ParseModelPayload_RejectsUnsupportedLanguageValue()
    {
        var parser = new GeminiResponseParser();

        Assert.Throws<InvalidOperationException>(() => parser.ParseModelPayload(
            """
            {
              "category": "Invoices",
              "projectTopic": "",
              "detectedLanguageContext": "Deutsch",
              "confidence": 0.81,
              "suggestedFolderPathFragment": "",
              "explanation": "Unsupported language enum."
            }
            """));
    }
}
