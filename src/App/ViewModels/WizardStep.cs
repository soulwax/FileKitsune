namespace FileTransformer.App.ViewModels;

public enum WizardStep
{
    ModeSelector = -1,
    Folder = 0,
    Strategy = 1,
    Rules = 2,
    Preview = 3,
    ExecuteRollback = 4,
    DedupScan = 5,
    DedupReview = 6,
    DedupExecute = 7
}
