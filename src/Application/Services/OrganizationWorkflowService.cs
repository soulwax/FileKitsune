using FileTransformer.Application.Abstractions;
using FileTransformer.Application.Models;
using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;
using FileTransformer.Domain.Services;
using Microsoft.Extensions.Logging;

namespace FileTransformer.Application.Services;

public sealed class OrganizationWorkflowService
{
    private static readonly HashSet<string> SidecarExtensions =
    [
        ".json",
        ".xml",
        ".xmp",
        ".cue",
        ".srt",
        ".vtt"
    ];

    private readonly IFileScanner fileScanner;
    private readonly IFileContentReader fileContentReader;
    private readonly SemanticClassifierCoordinator semanticClassifierCoordinator;
    private readonly IGeminiOrganizationAdvisor geminiOrganizationAdvisor;
    private readonly DateResolutionService dateResolutionService;
    private readonly DuplicateDetectionService duplicateDetectionService;
    private readonly ProjectClusterService projectClusterService;
    private readonly ProtectionPolicyService protectionPolicyService;
    private readonly DestinationPathBuilder destinationPathBuilder;
    private readonly PathSafetyService pathSafetyService;
    private readonly ILogger<OrganizationWorkflowService> logger;

    public OrganizationWorkflowService(
        IFileScanner fileScanner,
        IFileContentReader fileContentReader,
        SemanticClassifierCoordinator semanticClassifierCoordinator,
        IGeminiOrganizationAdvisor geminiOrganizationAdvisor,
        DateResolutionService dateResolutionService,
        DuplicateDetectionService duplicateDetectionService,
        ProjectClusterService projectClusterService,
        ProtectionPolicyService protectionPolicyService,
        DestinationPathBuilder destinationPathBuilder,
        PathSafetyService pathSafetyService,
        ILogger<OrganizationWorkflowService> logger)
    {
        this.fileScanner = fileScanner;
        this.fileContentReader = fileContentReader;
        this.semanticClassifierCoordinator = semanticClassifierCoordinator;
        this.geminiOrganizationAdvisor = geminiOrganizationAdvisor;
        this.dateResolutionService = dateResolutionService;
        this.duplicateDetectionService = duplicateDetectionService;
        this.projectClusterService = projectClusterService;
        this.protectionPolicyService = protectionPolicyService;
        this.destinationPathBuilder = destinationPathBuilder;
        this.pathSafetyService = pathSafetyService;
        this.logger = logger;
    }

    public async Task<OrganizationPlan> BuildPlanAsync(
        AppSettings appSettings,
        IProgress<WorkflowProgress>? progress,
        CancellationToken cancellationToken)
    {
        var settings = appSettings.Organization;
        var strategyDefinition = StrategyPresetCatalog.Resolve(settings.OrganizationPolicy);

        progress?.Report(new WorkflowProgress
        {
            Stage = "scan",
            Processed = 0,
            Total = 0,
            MessageResourceKey = "StatusScanning"
        });
        var scannedFiles = await fileScanner.ScanAsync(settings, progress, cancellationToken);

        var selectedFiles = scannedFiles
            .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Take(settings.PreviewSampleSize)
            .ToList();

        var groupingKeys = protectionPolicyService.BuildGroupingKeys(selectedFiles, settings.ProtectionPolicy);
        var contexts = new FileAnalysisContext[selectedFiles.Count];
        using var gate = new SemaphoreSlim(Math.Max(1, settings.MaxConcurrentClassification));
        var processed = 0;

        var tasks = selectedFiles.Select(async (file, index) =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var content = await fileContentReader.ReadAsync(file, settings, cancellationToken);
                var insight = await semanticClassifierCoordinator.ClassifyAsync(
                    new SemanticAnalysisRequest
                    {
                        File = file,
                        Content = content
                    },
                    settings,
                    appSettings.Gemini,
                    cancellationToken);

                var dateResolution = dateResolutionService.Resolve(file, content, settings);
                var protection = protectionPolicyService.Evaluate(file, settings);
                var groupKey = groupingKeys.TryGetValue(file.RelativePath, out var resolvedKey)
                    ? resolvedKey
                    : $"file::{file.RelativePath}";

                contexts[index] = new FileAnalysisContext
                {
                    File = file,
                    Content = content,
                    Insight = insight,
                    DateResolution = dateResolution,
                    ProtectedByRule = protection.IsProtected,
                    ProtectionReason = protection.Reason,
                    PlanningGroupKey = groupKey,
                    PlanningGroupLeaderKey = groupKey
                };
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Plan generation failed for {File}", file.RelativePath);
                contexts[index] = new FileAnalysisContext
                {
                    File = file,
                    Content = new FileContentSnapshot(),
                    Insight = new SemanticInsight
                    {
                        CategoryKey = "uncategorized",
                        OriginalCategoryLabel = "Uncategorized",
                        Confidence = 0,
                        Explanation = $"Planning failed: {exception.Message}"
                    },
                    ProtectedByRule = true,
                    ProtectionReason = $"Planning failed: {exception.Message}",
                    PlanningGroupKey = $"file::{file.RelativePath}",
                    PlanningGroupLeaderKey = $"file::{file.RelativePath}"
                };
            }
            finally
            {
                gate.Release();
                var current = Interlocked.Increment(ref processed);
                progress?.Report(new WorkflowProgress
                {
                    Stage = "classify",
                    Processed = current,
                    Total = selectedFiles.Count,
                    MessageResourceKey = "StatusProgressClassify",
                    MessageArguments = [current, selectedFiles.Count]
                });
            }
        });

        await Task.WhenAll(tasks);

        var duplicateMatches = await duplicateDetectionService.DetectAsync(selectedFiles, settings.DuplicatePolicy, progress, cancellationToken);
        foreach (var context in contexts)
        {
            if (duplicateMatches.TryGetValue(context.File.RelativePath, out var duplicateMatch))
            {
                context.DuplicateMatch = duplicateMatch;
            }
        }

        projectClusterService.EnrichClusters(contexts);

        var guidance = await BuildOrganizationGuidanceAsync(contexts, settings, appSettings.Gemini, cancellationToken);

        var groupedContexts = contexts
            .GroupBy(context => context.PlanningGroupKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var groupLeaders = groupedContexts.ToDictionary(
            pair => pair.Key,
            pair => SelectGroupLeader(pair.Value),
            StringComparer.OrdinalIgnoreCase);

        var categoryCounts = contexts
            .GroupBy(context => groupLeaders[context.PlanningGroupKey].Insight.CategoryKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var operations = contexts
            .Select(context =>
            {
                var group = groupedContexts[context.PlanningGroupKey];
                var groupLeader = groupLeaders[context.PlanningGroupKey];
                var sharedFolderGroup = group.Count > 1 && context.PlanningGroupKey.StartsWith("basename::", StringComparison.OrdinalIgnoreCase);

                return destinationPathBuilder.Build(
                    context,
                    groupLeader,
                    settings,
                    strategyDefinition,
                    categoryCounts,
                    pathSafetyService,
                    sharedFolderGroup);
            })
            .ToArray();

        MarkDestinationCollisions(operations);

        return new OrganizationPlan
        {
            Settings = settings,
            StrategyPreset = strategyDefinition.Preset,
            Operations = operations,
            Summary = BuildSummary(operations),
            Guidance = guidance
        };
    }

    private async Task<OrganizationGuidance?> BuildOrganizationGuidanceAsync(
        IReadOnlyList<FileAnalysisContext> contexts,
        OrganizationSettings settings,
        GeminiOptions geminiOptions,
        CancellationToken cancellationToken)
    {
        if (settings.ReviewPolicy.ExecutionMode == ExecutionMode.HeuristicsOnly ||
            !settings.UseGeminiWhenAvailable ||
            !geminiOptions.Enabled ||
            string.IsNullOrWhiteSpace(geminiOptions.ApiKey))
        {
            return null;
        }

        try
        {
            return await geminiOrganizationAdvisor.AdviseAsync(contexts, settings, geminiOptions, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Gemini organization guidance failed.");
            return null;
        }
    }

    private static FileAnalysisContext SelectGroupLeader(IReadOnlyList<FileAnalysisContext> group)
    {
        return group
            .OrderBy(context => IsSidecar(context.File.Extension))
            .ThenByDescending(context => context.Insight.Confidence)
            .ThenBy(context => context.File.RelativePath, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    private static bool IsSidecar(string extension) => SidecarExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);

    private static void MarkDestinationCollisions(IEnumerable<PlanOperation> operations)
    {
        var executables = operations
            .Where(op => op.AllowedToExecute && op.OperationType != PlanOperationType.Skip)
            .GroupBy(op => op.ProposedRelativePath, StringComparer.OrdinalIgnoreCase);

        foreach (var group in executables)
        {
            foreach (var op in group.Skip(1))
            {
                op.WarningFlags.Add($"Destination collision: another file in this plan also targets \"{group.Key}\".");
            }
        }
    }

    private static PlanSummary BuildSummary(IEnumerable<PlanOperation> operations)
    {
        var operationList = operations.ToList();
        return new PlanSummary
        {
            TotalItems = operationList.Count,
            MoveCount = operationList.Count(operation => operation.OperationType == PlanOperationType.Move),
            RenameCount = operationList.Count(operation => operation.OperationType == PlanOperationType.Rename),
            MoveAndRenameCount = operationList.Count(operation => operation.OperationType == PlanOperationType.MoveAndRename),
            SkipCount = operationList.Count(operation => operation.OperationType == PlanOperationType.Skip),
            GeminiAssistedCount = operationList.Count(operation => operation.GeminiUsed),
            RequiresReviewCount = operationList.Count(operation => operation.RequiresReview),
            HighRiskCount = operationList.Count(operation => operation.RiskLevel == RiskLevel.High),
            DuplicateCount = operationList.Count(operation => operation.DuplicateDetected),
            ProtectedCount = operationList.Count(operation => operation.ProtectionPreventedTransformation),
            AutoApprovedCount = operationList.Count(operation => operation.AutoApproved)
        };
    }
}
