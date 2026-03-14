using FileTransformer.Application.Abstractions;
using FileTransformer.Application.Models;
using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;
using Microsoft.Extensions.Logging;

namespace FileTransformer.Application.Services;

public sealed class OrganizationWorkflowService
{
    private readonly IFileScanner fileScanner;
    private readonly IFileContentReader fileContentReader;
    private readonly SemanticClassifierCoordinator semanticClassifierCoordinator;
    private readonly DestinationPathBuilder destinationPathBuilder;
    private readonly PathSafetyService pathSafetyService;
    private readonly ILogger<OrganizationWorkflowService> logger;

    public OrganizationWorkflowService(
        IFileScanner fileScanner,
        IFileContentReader fileContentReader,
        SemanticClassifierCoordinator semanticClassifierCoordinator,
        DestinationPathBuilder destinationPathBuilder,
        PathSafetyService pathSafetyService,
        ILogger<OrganizationWorkflowService> logger)
    {
        this.fileScanner = fileScanner;
        this.fileContentReader = fileContentReader;
        this.semanticClassifierCoordinator = semanticClassifierCoordinator;
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

        progress?.Report(new WorkflowProgress { Stage = "scan", Message = "Scanning files...", Processed = 0, Total = 0 });
        var scannedFiles = await fileScanner.ScanAsync(settings, progress, cancellationToken);

        var selectedFiles = scannedFiles
            .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Take(settings.PreviewSampleSize)
            .ToList();

        var operations = new PlanOperation[selectedFiles.Count];
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

                operations[index] = destinationPathBuilder.Build(file, insight, settings, pathSafetyService);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Plan generation failed for {File}", file.RelativePath);
                operations[index] = new PlanOperation
                {
                    OperationType = PlanOperationType.Skip,
                    CurrentRelativePath = file.RelativePath,
                    ProposedRelativePath = file.RelativePath,
                    FileName = file.FileName,
                    Confidence = 0,
                    RequiresReview = true,
                    AllowedToExecute = false,
                    RiskLevel = RiskLevel.High,
                    WarningFlags = ["Planning failure"],
                    Reason = $"Planning failed: {exception.Message}",
                    CategoryKey = "uncategorized",
                    CategoryDisplayName = "Uncategorized",
                    ProjectOrTopic = string.Empty
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
                    Message = $"Analyzed {current} of {selectedFiles.Count} files."
                });
            }
        });

        await Task.WhenAll(tasks);

        return new OrganizationPlan
        {
            Settings = settings,
            Operations = operations,
            Summary = BuildSummary(operations)
        };
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
            HighRiskCount = operationList.Count(operation => operation.RiskLevel == RiskLevel.High)
        };
    }
}
