using CommunityToolkit.Mvvm.ComponentModel;
using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;

namespace FileTransformer.App.ViewModels;

public sealed partial class PlanOperationItemViewModel : ObservableObject
{
    public PlanOperationItemViewModel(PlanOperation operation)
    {
        Operation = operation;
        isSelected = CanSelect && operation.OperationType != PlanOperationType.Skip;
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
}
