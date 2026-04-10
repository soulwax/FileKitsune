using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;
using FileTransformer.Infrastructure.Classification;
using Xunit;

namespace FileTransformer.Tests.Infrastructure;

public sealed class GeminiOrganizationGuidanceParserTests
{
    [Fact]
    public void ParseModelPayload_ReturnsStrictOrganizationGuidance()
    {
        var parser = new GeminiOrganizationGuidanceParser();

        var guidance = parser.ParseModelPayload(
            """
            {
              "preferredStrategyPreset": "ProjectFirst",
              "structureBias": "Shallower",
              "suggestedMaxDepth": 3,
              "reasoning": "Project signals dominate and a flatter tree reduces fragmentation."
            }
            """);

        Assert.Equal(OrganizationStrategyPreset.ProjectFirst, guidance.PreferredPreset);
        Assert.Equal(OrganizationStructureBias.Shallower, guidance.StructureBias);
        Assert.Equal(3, guidance.SuggestedMaxDepth);
    }

    [Fact]
    public void ParseModelPayload_RejectsUnsupportedPreset()
    {
        var parser = new GeminiOrganizationGuidanceParser();

        Assert.Throws<InvalidOperationException>(() => parser.ParseModelPayload(
            """
            {
              "preferredStrategyPreset": "InventedPreset",
              "structureBias": "Balanced",
              "suggestedMaxDepth": 4,
              "reasoning": "Unsupported."
            }
            """));
    }
}
