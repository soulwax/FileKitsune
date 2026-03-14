using CommunityToolkit.Mvvm.ComponentModel;
using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;

namespace FileTransformer.App.ViewModels;

public sealed partial class PlanOperationItemViewModel : ObservableObject
{
    public PlanOperationItemViewModel(PlanOperation operation)
    {
        Operation = operation;
        isSelected = CanSelect && operation.AutoApproved;
    }

    public PlanOperation Operation { get; }

    [ObservableProperty]
    private bool isSelected;

    public Guid OperationId => Operation.Id;

    public bool CanSelect => Operation.AllowedToExecute && Operation.OperationType != PlanOperationType.Skip;

    public string OperationType => Operation.OperationType.ToString();

    public string CurrentRelativePath => Operation.CurrentRelativePath;

    public string ProposedRelativePath => string.IsNullOrWhiteSpace(Operation.ProposedRelativePath)
        ? Operation.CurrentRelativePath
        : Operation.ProposedRelativePath;

    public string Reason => Operation.Reason;

    public string Confidence => $"{Operation.Confidence:P0}";

    public string Risk => Operation.RiskLevel.ToString();

    public string Warnings => Operation.WarningFlags.Count == 0 ? "None" : string.Join(" | ", Operation.WarningFlags);

    public string Gemini => Operation.GeminiUsed ? "Gemini + heuristics" : "Heuristics";

    public string Language => Operation.LanguageContext.ToString();

    public string Category => Operation.CategoryDisplayName;

    public string Project => string.IsNullOrWhiteSpace(Operation.ProjectOrTopic) ? "None" : Operation.ProjectOrTopic;

    public string Strategy => Operation.StrategyPreset.ToString();

    public string Review => Operation.ReviewReasons.Count == 0 ? "Auto-approved" : string.Join(" | ", Operation.ReviewReasons);

    public string Duplicate => Operation.DuplicateDetected ? $"Duplicate of {Operation.DuplicateOfRelativePath}" : "No";

    public string Protection => string.IsNullOrWhiteSpace(Operation.ProtectionReason) ? "None" : Operation.ProtectionReason;

    public string Approval => Operation.AutoApproved ? "Auto" : Operation.RequiresReview ? "Review" : "Manual";
}
