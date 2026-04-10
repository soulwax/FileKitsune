using System.Text.Json;
using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;

namespace FileTransformer.Infrastructure.Classification;

public sealed class GeminiOrganizationGuidanceParser
{
    public OrganizationGuidance ParseApiResponse(string responseJson)
    {
        using var document = JsonDocument.Parse(responseJson);
        var candidateText = document.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(candidateText))
        {
            throw new InvalidOperationException("Gemini returned an empty organization guidance payload.");
        }

        return ParseModelPayload(candidateText);
    }

    public OrganizationGuidance ParseModelPayload(string modelJson)
    {
        using var document = JsonDocument.Parse(modelJson);
        var root = document.RootElement;

        var preset = ParsePreset(ReadRequiredString(root, "preferredStrategyPreset"));
        var structureBias = ParseStructureBias(ReadRequiredString(root, "structureBias"));
        var suggestedMaxDepth = root.GetProperty("suggestedMaxDepth").GetInt32();
        var reasoning = ReadRequiredString(root, "reasoning");

        if (suggestedMaxDepth is < 2 or > 5)
        {
            throw new InvalidOperationException("Gemini suggestedMaxDepth must be between 2 and 5.");
        }

        if (reasoning.Length > 240)
        {
            throw new InvalidOperationException("Gemini organization reasoning is too long.");
        }

        return new OrganizationGuidance
        {
            GeminiUsed = true,
            PreferredPreset = preset,
            StructureBias = structureBias,
            SuggestedMaxDepth = suggestedMaxDepth,
            Reasoning = reasoning.Trim()
        };
    }

    private static string ReadRequiredString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Missing required Gemini property '{propertyName}'.");
        }

        var text = value.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException($"Gemini property '{propertyName}' is empty.");
        }

        return text;
    }

    private static OrganizationStrategyPreset ParsePreset(string value) =>
        value.Trim() switch
        {
            "SemanticCategoryFirst" => OrganizationStrategyPreset.SemanticCategoryFirst,
            "ProjectFirst" => OrganizationStrategyPreset.ProjectFirst,
            "DateFirst" => OrganizationStrategyPreset.DateFirst,
            "HybridProjectDate" => OrganizationStrategyPreset.HybridProjectDate,
            "ArchiveCleanup" => OrganizationStrategyPreset.ArchiveCleanup,
            "WorkDocuments" => OrganizationStrategyPreset.WorkDocuments,
            "ResearchLibrary" => OrganizationStrategyPreset.ResearchLibrary,
            "ManualCustom" => OrganizationStrategyPreset.ManualCustom,
            _ => throw new InvalidOperationException("Gemini returned an unsupported strategy preset.")
        };

    private static OrganizationStructureBias ParseStructureBias(string value) =>
        value.Trim() switch
        {
            "Shallower" => OrganizationStructureBias.Shallower,
            "Deeper" => OrganizationStructureBias.Deeper,
            "Balanced" => OrganizationStructureBias.Balanced,
            _ => throw new InvalidOperationException("Gemini returned an unsupported structure bias.")
        };
}
