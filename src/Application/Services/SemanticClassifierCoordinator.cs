using FileTransformer.Application.Abstractions;
using FileTransformer.Application.Models;
using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;
using Microsoft.Extensions.Logging;

namespace FileTransformer.Application.Services;

public sealed class SemanticClassifierCoordinator
{
    private readonly HeuristicSemanticClassifier heuristicSemanticClassifier;
    private readonly IGeminiSemanticClassifier geminiSemanticClassifier;
    private readonly ILogger<SemanticClassifierCoordinator> logger;

    public SemanticClassifierCoordinator(
        HeuristicSemanticClassifier heuristicSemanticClassifier,
        IGeminiSemanticClassifier geminiSemanticClassifier,
        ILogger<SemanticClassifierCoordinator> logger)
    {
        this.heuristicSemanticClassifier = heuristicSemanticClassifier;
        this.geminiSemanticClassifier = geminiSemanticClassifier;
        this.logger = logger;
    }

    public async Task<SemanticInsight> ClassifyAsync(
        SemanticAnalysisRequest request,
        OrganizationSettings settings,
        GeminiOptions geminiOptions,
        CancellationToken cancellationToken)
    {
        var heuristic = await heuristicSemanticClassifier.ClassifyAsync(request, settings, cancellationToken);

        if (settings.ReviewPolicy.ExecutionMode == ExecutionMode.HeuristicsOnly ||
            !settings.UseGeminiWhenAvailable ||
            !geminiOptions.Enabled ||
            string.IsNullOrWhiteSpace(geminiOptions.ApiKey))
        {
            return heuristic;
        }

        try
        {
            var gemini = await geminiSemanticClassifier.ClassifyAsync(request, geminiOptions, cancellationToken);
            if (gemini is null)
            {
                return heuristic;
            }

            var chosenCategory = gemini.Confidence >= heuristic.Confidence || heuristic.CategoryKey == "uncategorized"
                ? gemini.CategoryKey
                : heuristic.CategoryKey;

            return new SemanticInsight
            {
                CategoryKey = chosenCategory,
                OriginalCategoryLabel = !string.IsNullOrWhiteSpace(gemini.OriginalCategoryLabel)
                    ? gemini.OriginalCategoryLabel
                    : heuristic.OriginalCategoryLabel,
                ProjectOrTopic = !string.IsNullOrWhiteSpace(gemini.ProjectOrTopic)
                    ? gemini.ProjectOrTopic
                    : heuristic.ProjectOrTopic,
                LanguageContext = gemini.LanguageContext == DetectedLanguageContext.Unclear
                    ? heuristic.LanguageContext
                    : gemini.LanguageContext,
                Confidence = Math.Max(heuristic.Confidence, gemini.Confidence),
                SuggestedFolderFragment = !string.IsNullOrWhiteSpace(gemini.SuggestedFolderFragment)
                    ? gemini.SuggestedFolderFragment
                    : heuristic.SuggestedFolderFragment,
                Explanation = $"Gemini: {gemini.Explanation} Heuristic: {heuristic.Explanation}",
                ClassificationMethod = ClassificationMethod.Hybrid,
                GeminiUsed = true,
                EvidenceKeywords = heuristic.EvidenceKeywords
            };
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Gemini classification failed for {File}", request.File.RelativePath);
            return heuristic;
        }
    }
}
