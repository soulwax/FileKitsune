using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileTransformer.App.Services;
using FileTransformer.Application.Abstractions;
using FileTransformer.Application.Models;
using FileTransformer.Application.Services;
using FileTransformer.Domain.Enums;
using FileTransformer.Domain.Models;
using Microsoft.Extensions.Logging;

namespace FileTransformer.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private const int RollbackPreviewSectionSampleLimit = 4;
    private const int RollbackConfirmationSampleLimit = 2;
    private readonly IAppSettingsStore appSettingsStore;
    private readonly IEnvironmentConfigService environmentConfigService;
    private readonly IEnvironmentSanityService environmentSanityService;
    private readonly IPersistenceStatusService persistenceStatusService;
    private readonly OrganizationWorkflowService organizationWorkflowService;
    private readonly PlanExecutionService planExecutionService;
    private readonly RollbackService rollbackService;
    private readonly StrategyRecommendationService strategyRecommendationService;
    private readonly IFileScanner fileScanner;
    private readonly DuplicateDetectionService duplicateDetectionService;
    private readonly IRecycleBinService recycleBinService;
    private readonly IDedupAuditStore dedupAuditStore;
    private readonly IFolderPickerService folderPickerService;
    private readonly IDialogService dialogService;
    private readonly ILocalizationService localizationService;
    private readonly UiLogStore uiLogStore;
    private readonly ILogger<MainWindowViewModel> logger;
    private OrganizationPlan? currentPlan;
    private CancellationTokenSource? currentCancellationTokenSource;
    private bool initialized;
    private bool suppressDuplicateSelection;
    private bool suppressAnalysisProfileSelection;
    private DateTimeOffset? geminiEnvironmentPingValidatedAtUtcCache;
    private string geminiEnvironmentPingFingerprintCache = string.Empty;

    public MainWindowViewModel(
        IAppSettingsStore appSettingsStore,
        IEnvironmentConfigService environmentConfigService,
        IEnvironmentSanityService environmentSanityService,
        IPersistenceStatusService persistenceStatusService,
        OrganizationWorkflowService organizationWorkflowService,
        PlanExecutionService planExecutionService,
        RollbackService rollbackService,
        StrategyRecommendationService strategyRecommendationService,
        IFileScanner fileScanner,
        DuplicateDetectionService duplicateDetectionService,
        IRecycleBinService recycleBinService,
        IDedupAuditStore dedupAuditStore,
        IFolderPickerService folderPickerService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        UiLogStore uiLogStore,
        ILogger<MainWindowViewModel> logger)
    {
        this.appSettingsStore = appSettingsStore;
        this.environmentConfigService = environmentConfigService;
        this.environmentSanityService = environmentSanityService;
        this.persistenceStatusService = persistenceStatusService;
        this.organizationWorkflowService = organizationWorkflowService;
        this.planExecutionService = planExecutionService;
        this.rollbackService = rollbackService;
        this.strategyRecommendationService = strategyRecommendationService;
        this.fileScanner = fileScanner;
        this.duplicateDetectionService = duplicateDetectionService;
        this.recycleBinService = recycleBinService;
        this.dedupAuditStore = dedupAuditStore;
        this.folderPickerService = folderPickerService;
        this.dialogService = dialogService;
        this.localizationService = localizationService;
        this.uiLogStore = uiLogStore;
        this.logger = logger;

        RenameModes = [];
        FolderLanguageModes = [];
        ConflictModes = [];
        PlanFilters = [];
        StrategyPresets = [];
        DateSources = [];
        ExecutionModes = [];
        DuplicateHandlingModes = [];
        FilenameLanguagePolicies = [];
        AnalysisProfiles = [];
        AppLanguages =
        [
            new("de-DE", "Deutsch"),
            new("en-US", "English")
        ];

        RefreshLocalizedOptionCollections();

        selectedRenameMode = RenameModes[1];
        selectedFolderLanguageMode = FolderLanguageModes[1];
        selectedConflictMode = ConflictModes[1];
        selectedPlanFilter = PlanFilters[0];
        selectedStrategyPreset = StrategyPresets[0];
        selectedPreferredDateSource = DateSources[2];
        selectedExecutionMode = ExecutionModes[0];
        selectedDuplicateHandlingMode = DuplicateHandlingModes[0];
        selectedFilenameLanguagePolicy = FilenameLanguagePolicies[1];
        selectedAnalysisProfile = AnalysisProfiles[1];
        maxFolderDepth = 4;
        mergeSparseCategories = false;
        sparseCategoryThreshold = 2;
        miscellaneousBucketName = "Misc";
        onlyCreateDateFoldersWhenReliable = true;
        preferGeminiFolderSuggestion = true;
        suggestOnlyOnLowConfidence = true;
        requireReviewForRenames = true;
        routeLowConfidenceToReviewFolder = true;
        reviewFolderName = "Review";
        autoApproveConfidenceThreshold = 0.82d;
        enableExactDuplicateDetection = false;
        duplicatesFolderName = "Zu prüfende Duplikate";
        selectedAppLanguage = AppLanguages[0];

        PlanOperations = [];
        DedupGroups = [];
        DedupExecutionErrors = [];
        PlanView = CollectionViewSource.GetDefaultView(PlanOperations);
        PlanView.Filter = ApplyPlanFilter;
        LogEntries = uiLogStore.Entries;
        EnvironmentSanityItems = [];
        StatusMessage = GetString("StatusReady");
        StrategyDisplayName = selectedStrategyPreset.Label;
    }

    public ObservableCollection<OptionItem<FileRenameMode>> RenameModes { get; }

    public ObservableCollection<OptionItem<FolderLanguageMode>> FolderLanguageModes { get; }

    public ObservableCollection<OptionItem<ConflictHandlingMode>> ConflictModes { get; }

    public ObservableCollection<OptionItem<PlanFilterMode>> PlanFilters { get; }

    public ObservableCollection<OptionItem<OrganizationStrategyPreset>> StrategyPresets { get; }

    public ObservableCollection<OptionItem<DateSourceKind>> DateSources { get; }

    public ObservableCollection<OptionItem<ExecutionMode>> ExecutionModes { get; }

    public ObservableCollection<OptionItem<DuplicateHandlingMode>> DuplicateHandlingModes { get; }

    public ObservableCollection<OptionItem<FilenameLanguagePolicy>> FilenameLanguagePolicies { get; }

    public ObservableCollection<OptionItem<string>> AnalysisProfiles { get; }

    public IReadOnlyList<OptionItem<string>> AppLanguages { get; }

    public ObservableCollection<PlanOperationItemViewModel> PlanOperations { get; }

    public ObservableCollection<StrategyRecommendation> StrategyRecommendations { get; } = [];

    public ObservableCollection<DuplicateGroupItem> DuplicateGroups { get; } = [];

    public ObservableCollection<PlanOperationItemViewModel> DuplicateGroupOperations { get; } = [];

    public ObservableCollection<DedupGroupViewModel> DedupGroups { get; }

    public ObservableCollection<string> DedupExecutionErrors { get; }

    public ObservableCollection<RollbackFolderItem> RollbackFolderGroups { get; } = [];

    public ObservableCollection<RollbackJournalItem> RollbackHistory { get; } = [];

    public ObservableCollection<RollbackPreviewItem> RollbackPreviewEntries { get; } = [];

    public ObservableCollection<RollbackPreviewSectionItem> RollbackPreviewSections { get; } = [];

    public ICollectionView PlanView { get; }

    public ObservableCollection<UiLogEntry> LogEntries { get; }

    public ObservableCollection<EnvironmentSanityItemViewModel> EnvironmentSanityItems { get; }

    [ObservableProperty]
    private string rootDirectory = string.Empty;

    [ObservableProperty]
    private string dedupRootFolder = string.Empty;

    [ObservableProperty]
    private string includePatternsText = "*";

    [ObservableProperty]
    private string excludePatternsText = ".git; .vs; bin; obj; node_modules";

    [ObservableProperty]
    private string supportedExtensionsText =
        ".txt; .md; .json; .xml; .csv; .cs; .xaml; .ts; .js; .py; .pdf; .docx; .epub; .mobi; .azw; .azw3; .fb2; .djvu; .cbz; .cbr";

    [ObservableProperty]
    private int maxFileSizeKb = 512;

    [ObservableProperty]
    private int previewSampleSize = 500;

    [ObservableProperty]
    private int maxFilesToScan = 5000;

    [ObservableProperty]
    private bool removeEmptyFolders;

    [ObservableProperty]
    private bool allowFileRename = true;

    [ObservableProperty]
    private string namingTemplate = "{originalName}";

    [ObservableProperty]
    private OptionItem<FileRenameMode>? selectedRenameMode;

    [ObservableProperty]
    private OptionItem<FolderLanguageMode>? selectedFolderLanguageMode;

    [ObservableProperty]
    private OptionItem<ConflictHandlingMode>? selectedConflictMode;

    [ObservableProperty]
    private OptionItem<PlanFilterMode>? selectedPlanFilter;

    [ObservableProperty]
    private bool useSemanticCategoryDimension = true;

    [ObservableProperty]
    private bool useProjectDimension = true;

    [ObservableProperty]
    private bool useYearDimension = true;

    [ObservableProperty]
    private bool useMonthDimension;

    [ObservableProperty]
    private bool useFileTypeSecondaryCriterion = true;

    [ObservableProperty]
    private bool useGemini = true;

    [ObservableProperty]
    private string geminiApiKey = string.Empty;

    [ObservableProperty]
    private string geminiModel = "gemini-2.0-flash";

    [ObservableProperty]
    private int geminiMaxRequestsPerMinute = 30;

    [ObservableProperty]
    private int geminiRequestTimeoutSeconds = 30;

    [ObservableProperty]
    private double lowConfidenceThreshold = 0.55d;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isProgressIndeterminate;

    [ObservableProperty]
    private double progressValue;

    [ObservableProperty]
    private double progressMaximum = 1;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private int totalItems;

    [ObservableProperty]
    private int moveCount;

    [ObservableProperty]
    private int renameCount;

    [ObservableProperty]
    private int needsReviewCount;

    [ObservableProperty]
    private int geminiAssistedCount;

    [ObservableProperty]
    private int duplicateCount;

    [ObservableProperty]
    private bool hasDuplicateGroups;

    [ObservableProperty]
    private bool hasDedupGroups;

    [ObservableProperty]
    private bool dedupScanCompleted;

    [ObservableProperty]
    private DuplicateGroupItem? selectedDuplicateGroup;

    [ObservableProperty]
    private DedupGroupViewModel? selectedDedupGroup;

    [ObservableProperty]
    private bool hasSelectedDuplicateGroup;

    [ObservableProperty]
    private bool hasSelectedDedupGroup;

    [ObservableProperty]
    private OptionItem<OrganizationStrategyPreset>? selectedStrategyPreset;

    [ObservableProperty]
    private OptionItem<DateSourceKind>? selectedPreferredDateSource;

    [ObservableProperty]
    private OptionItem<ExecutionMode>? selectedExecutionMode;

    [ObservableProperty]
    private OptionItem<DuplicateHandlingMode>? selectedDuplicateHandlingMode;

    [ObservableProperty]
    private OptionItem<FilenameLanguagePolicy>? selectedFilenameLanguagePolicy;

    [ObservableProperty]
    private OptionItem<string>? selectedAnalysisProfile;

    [ObservableProperty]
    private int maxFolderDepth;

    [ObservableProperty]
    private bool mergeSparseCategories;

    [ObservableProperty]
    private int sparseCategoryThreshold;

    [ObservableProperty]
    private string miscellaneousBucketName = string.Empty;

    [ObservableProperty]
    private bool onlyCreateDateFoldersWhenReliable;

    [ObservableProperty]
    private bool preferGeminiFolderSuggestion;

    [ObservableProperty]
    private bool suggestOnlyOnLowConfidence;

    [ObservableProperty]
    private bool requireReviewForRenames;

    [ObservableProperty]
    private bool routeLowConfidenceToReviewFolder;

    [ObservableProperty]
    private string reviewFolderName = string.Empty;

    [ObservableProperty]
    private double autoApproveConfidenceThreshold = 0.82d;

    [ObservableProperty]
    private bool enableExactDuplicateDetection;

    [ObservableProperty]
    private string duplicatesFolderName = string.Empty;

    [ObservableProperty]
    private string strategyDisplayName = string.Empty;

    [ObservableProperty]
    private bool hasRollbackFolderGroups;

    [ObservableProperty]
    private bool hasRollbackHistory;

    [ObservableProperty]
    private RollbackJournalItem? selectedRollbackJournal;

    [ObservableProperty]
    private bool hasSelectedRollbackJournal;

    [ObservableProperty]
    private string selectedRollbackJournalSummary = string.Empty;

    [ObservableProperty]
    private bool hasRollbackPreviewEntries;

    [ObservableProperty]
    private int rollbackPreviewReadyCount;

    [ObservableProperty]
    private int rollbackPreviewMissingDestinationCount;

    [ObservableProperty]
    private int rollbackPreviewOriginalPathOccupiedCount;

    [ObservableProperty]
    private string rollbackPreviewScopeLabel = string.Empty;

    [ObservableProperty]
    private string rollbackPreviewScopeBody = string.Empty;

    [ObservableProperty]
    private bool isRollbackPreviewScopedToFolder;

    [ObservableProperty]
    private string persistenceModeLabel = string.Empty;

    [ObservableProperty]
    private string persistencePrimaryStore = string.Empty;

    [ObservableProperty]
    private string persistenceSecondaryStore = string.Empty;

    [ObservableProperty]
    private string persistenceDetail = string.Empty;

    [ObservableProperty]
    private PlanOperationItemViewModel? selectedOperation;

    [ObservableProperty]
    private bool hasStrategyRecommendations;

    [ObservableProperty]
    private bool hasGeminiOrganizationGuidance;

    [ObservableProperty]
    private string geminiOrganizationGuidanceTitle = string.Empty;

    [ObservableProperty]
    private string geminiOrganizationGuidanceBody = string.Empty;

    [ObservableProperty]
    private bool hasEnvironmentSanityItems;

    [ObservableProperty]
    private string environmentSanitySummary = string.Empty;

    [ObservableProperty]
    private string environmentFilePath = string.Empty;

    [ObservableProperty]
    private string envGeminiEnabled = string.Empty;

    [ObservableProperty]
    private string envGeminiApiKey = string.Empty;

    [ObservableProperty]
    private string envGeminiModel = string.Empty;

    [ObservableProperty]
    private string envGeminiEndpointBaseUrl = string.Empty;

    [ObservableProperty]
    private string envGeminiMaxRequestsPerMinute = string.Empty;

    [ObservableProperty]
    private string envGeminiRequestTimeoutSeconds = string.Empty;

    [ObservableProperty]
    private string envGeminiMaxPromptCharacters = string.Empty;

    [ObservableProperty]
    private string envFileKitsuneOfflineMode = string.Empty;

    [ObservableProperty]
    private string geminiEnvironmentPingStatus = string.Empty;

    [ObservableProperty]
    private string geminiEnvironmentPingMessage = string.Empty;

    [ObservableProperty]
    private bool isGeminiEnvironmentPingInProgress;

    [ObservableProperty]
    private bool isGeminiEnvironmentPingAllowed = true;

    [ObservableProperty]
    private string geminiEnvironmentPingAvailabilityMessage = string.Empty;

    [ObservableProperty]
    private OptionItem<string>? selectedAppLanguage;

    [ObservableProperty]
    private int dedupResolvedCount;

    [ObservableProperty]
    private int dedupSkippedCount;

    [ObservableProperty]
    private int dedupFilesRecycledCount;

    [ObservableProperty]
    private int dedupRecycleErrorCount;

    [ObservableProperty]
    private long dedupBytesFreed;

    [ObservableProperty]
    private string dedupExecutionSummary = string.Empty;

    [ObservableProperty]
    private string dedupLastAuditPath = string.Empty;

    [ObservableProperty]
    private bool hasDedupExecutionResults;

    [ObservableProperty]
    private WizardStep currentStep = WizardStep.ModeSelector;

    public int CurrentStepIndex
    {
        get => (int)CurrentStep;
        set => CurrentStep = ClampStep(value);
    }

    public int CurrentStepNumber =>
        CurrentStep switch
        {
            WizardStep.ModeSelector => 1,
            WizardStep.DedupScan => 1,
            WizardStep.DedupReview => 2,
            WizardStep.DedupExecute => 3,
            _ => CurrentStepIndex + 1
        };

    public int TotalWizardSteps => IsDedupFlow(CurrentStep) ? 3 : 5;

    public bool IsWizardShellNavigationVisible => CurrentStep is not WizardStep.ModeSelector;

    public bool AllDedupGroupsReviewed =>
        DedupScanCompleted && DedupGroups.All(group => group.IsResolved || group.IsSkipped);

    public string DedupReviewTally =>
        FormatString("DedupReviewTally", DedupGroups.Count, DedupResolvedCount, DedupSkippedCount);

    public double DedupBytesFreedMb => DedupBytesFreed / 1024d / 1024d;

    public async Task InitializeAsync()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;

        try
        {
            var settings = await appSettingsStore.LoadAsync(CancellationToken.None);
            ApplySettings(settings);
            await ReloadEnvironmentEditorAsync(CancellationToken.None);
            await RefreshEnvironmentSanityAsync(CancellationToken.None);
            await RefreshRollbackHistoryAsync(CancellationToken.None);
            await RefreshPersistenceStatusAsync(CancellationToken.None);
            logger.LogInformation("Settings loaded from user profile.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to load settings.");
            dialogService.ShowError(GetString("DialogLoadFailedTitle"), FormatString("DialogLoadFailedBody", exception.Message));
        }
    }

    [RelayCommand]
    private void BrowseRoot()
    {
        var selectedFolder = folderPickerService.PickFolder(RootDirectory);
        if (!string.IsNullOrWhiteSpace(selectedFolder))
        {
            RootDirectory = selectedFolder;
        }
    }

    [RelayCommand]
    private void BrowseDedupRoot()
    {
        var selectedFolder = folderPickerService.PickFolder(DedupRootFolder);
        if (!string.IsNullOrWhiteSpace(selectedFolder))
        {
            DedupRootFolder = selectedFolder;
        }
    }

    [RelayCommand]
    private void SelectOrganizeMode()
    {
        CurrentStep = WizardStep.Folder;
    }

    [RelayCommand]
    private void SelectDedupMode()
    {
        if (string.IsNullOrWhiteSpace(DedupRootFolder))
        {
            DedupRootFolder = RootDirectory;
        }

        CurrentStep = WizardStep.DedupScan;
    }

    [RelayCommand]
    private void ScanAnotherFolder()
    {
        ResetDedupState(clearRoot: false);
        CurrentStep = WizardStep.ModeSelector;
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            await PersistSettingsAsync(CancellationToken.None);
            await RefreshPersistenceStatusAsync(CancellationToken.None);
            StatusMessage = GetString("StatusSettingsSaved");
            logger.LogInformation("User settings saved.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Saving settings failed.");
            dialogService.ShowError(GetString("DialogSaveFailedTitle"), FormatString("DialogSaveFailedBody", exception.Message));
        }
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (!ValidateRootDirectory())
        {
            return;
        }

        await PersistSettingsAsync(CancellationToken.None);
        ResetProgress();
        PlanOperations.Clear();
        StrategyRecommendations.Clear();
        HasGeminiOrganizationGuidance = false;
        GeminiOrganizationGuidanceTitle = string.Empty;
        GeminiOrganizationGuidanceBody = string.Empty;
        HasStrategyRecommendations = false;
        DuplicateGroups.Clear();
        HasDuplicateGroups = false;
        DuplicateGroupOperations.Clear();
        HasSelectedDuplicateGroup = false;
        currentPlan = null;

        currentCancellationTokenSource = new CancellationTokenSource();

        try
        {
            IsBusy = true;
            StatusMessage = GetString("StatusScanning");

            var progress = new Progress<WorkflowProgress>(UpdateProgress);
            currentPlan = await organizationWorkflowService.BuildPlanAsync(
                BuildSettings(),
                progress,
                currentCancellationTokenSource.Token);

            foreach (var operation in currentPlan.Operations)
            {
                PlanOperations.Add(new PlanOperationItemViewModel(operation));
            }

            UpdateSummary(currentPlan.Summary);
            PopulateStrategyRecommendations(currentPlan);
            PopulateDuplicateGroups(currentPlan);
            PlanView.Refresh();
            StatusMessage = FormatString("StatusPreviewReady", currentPlan.Summary.TotalItems);
            logger.LogInformation("Preview plan built with {Count} operations.", currentPlan.Operations.Count);
            NotifyWizardNavigationChanged();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = GetString("StatusCanceled");
            logger.LogInformation("Scan operation canceled.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Scan operation failed.");
            dialogService.ShowError(GetString("DialogScanFailedTitle"), exception.Message);
            StatusMessage = GetString("StatusScanFailed");
        }
        finally
        {
            IsBusy = false;
            currentCancellationTokenSource?.Dispose();
            currentCancellationTokenSource = null;
            NotifyWizardNavigationChanged();
        }
    }

    [RelayCommand]
    private async Task DedupScanAsync()
    {
        if (!ValidateDedupRootFolder())
        {
            return;
        }

        ResetProgress();
        ResetDedupState(clearRoot: false);
        currentCancellationTokenSource = new CancellationTokenSource();

        try
        {
            IsBusy = true;
            StatusMessage = GetString("StatusDedupScanning");

            var settings = BuildSettings().Organization;
            settings.RootDirectory = DedupRootFolder;
            settings.IncludePatterns = ["*"];
            settings.DuplicatePolicy = new DuplicatePolicy
            {
                EnableExactDuplicateDetection = true,
                HandlingMode = DuplicateHandlingMode.RequireReview,
                DuplicatesFolderName = DuplicatesFolderName
            };

            var progress = new Progress<WorkflowProgress>(UpdateProgress);
            var files = await fileScanner.ScanAsync(settings, progress, currentCancellationTokenSource.Token);
            var matches = await duplicateDetectionService.DetectAsync(
                files,
                settings.DuplicatePolicy,
                progress,
                currentCancellationTokenSource.Token);

            PopulateDedupGroups(files, matches);
            DedupScanCompleted = true;
            StatusMessage = FormatString("StatusDedupScanComplete", DedupGroups.Count);
            logger.LogInformation("Standalone dedup scan found {Count} duplicate groups.", DedupGroups.Count);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = GetString("StatusCanceled");
            logger.LogInformation("Standalone dedup scan canceled.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Standalone dedup scan failed.");
            dialogService.ShowError(GetString("DialogDedupScanFailedTitle"), exception.Message);
            StatusMessage = GetString("StatusDedupScanFailed");
        }
        finally
        {
            IsBusy = false;
            currentCancellationTokenSource?.Dispose();
            currentCancellationTokenSource = null;
            NotifyWizardNavigationChanged();
        }
    }

    [RelayCommand]
    private void ConfirmSelectedDedupGroup()
    {
        if (SelectedDedupGroup is null)
        {
            return;
        }

        SelectedDedupGroup.MarkResolved();
        AdvanceDedupSelection();
        RefreshDedupReviewState();
    }

    [RelayCommand]
    private void SkipSelectedDedupGroup()
    {
        if (SelectedDedupGroup is null)
        {
            return;
        }

        SelectedDedupGroup.MarkSkipped();
        AdvanceDedupSelection();
        RefreshDedupReviewState();
    }

    [RelayCommand]
    private void ResolveAllDedupAutomatically()
    {
        foreach (var group in DedupGroups.Where(group => !group.IsResolved && !group.IsSkipped))
        {
            group.MarkResolved();
        }

        SelectedDedupGroup = DedupGroups.FirstOrDefault();
        RefreshDedupReviewState();
    }

    [RelayCommand]
    private async Task DedupExecuteAsync()
    {
        if (!AllDedupGroupsReviewed)
        {
            dialogService.ShowInformation(GetString("DialogDedupReviewRequiredTitle"), GetString("DialogDedupReviewRequiredBody"));
            return;
        }

        var resolvedGroups = DedupGroups.Where(group => group.IsResolved && !group.IsSkipped).ToList();
        if (resolvedGroups.Count == 0)
        {
            DedupExecutionSummary = FormatString("DedupExecuteSummary", 0, DedupGroups.Count(group => group.IsSkipped), 0d, 0);
            HasDedupExecutionResults = true;
            return;
        }

        currentCancellationTokenSource = new CancellationTokenSource();
        var recycledCount = 0;
        var errorCount = 0;
        long bytesFreed = 0;
        var filesToRecycle = resolvedGroups.SelectMany(group => group.Files.Where(file => !file.IsKeeper)).ToList();
        var processed = 0;
        DedupExecutionErrors.Clear();
        DedupLastAuditPath = string.Empty;

        DedupAuditRunHandle auditRun;
        try
        {
            auditRun = await dedupAuditStore.StartRunAsync(
                BuildDedupAuditRunStarted(filesToRecycle),
                CancellationToken.None);
            DedupLastAuditPath = auditRun.AuditFilePath;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Dedup execution audit could not be started. No files will be recycled.");
            dialogService.ShowError(GetString("DialogDedupAuditFailedTitle"), GetString("DialogDedupAuditFailedBody"));
            StatusMessage = GetString("StatusDedupAuditFailed");
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = GetString("StatusDedupExecuting");
            ProgressMaximum = Math.Max(1, filesToRecycle.Count);
            ProgressValue = 0;
            IsProgressIndeterminate = false;

            foreach (var group in resolvedGroups)
            {
                foreach (var file in group.Files.Where(file => !file.IsKeeper))
                {
                    currentCancellationTokenSource.Token.ThrowIfCancellationRequested();
                    if (!IsPathInsideDedupRoot(file.FullPath))
                    {
                        errorCount++;
                        DedupExecutionErrors.Add(FormatString("DedupExecuteErrorOutOfRoot", file.RelativePath));
                        logger.LogWarning("Dedup recycle skipped for out-of-root path {Path}", file.FullPath);
                        await dedupAuditStore.AppendEntryAsync(
                            auditRun.RunId,
                            CreateDedupAuditEntry(file, "recycle-result", "SkippedOutOfRoot", "File was outside the selected dedup root and was not moved."),
                            CancellationToken.None);
                        continue;
                    }

                    try
                    {
                        await dedupAuditStore.AppendEntryAsync(
                            auditRun.RunId,
                            CreateDedupAuditEntry(file, "recycle-attempt", "Pending", "Recycle Bin move is about to start."),
                            currentCancellationTokenSource.Token);

                        await recycleBinService.RecycleFileAsync(file.FullPath, currentCancellationTokenSource.Token);
                        recycledCount++;
                        bytesFreed += file.SizeBytes;

                        try
                        {
                            await dedupAuditStore.AppendEntryAsync(
                                auditRun.RunId,
                                CreateDedupAuditEntry(file, "recycle-result", "MovedToRecycleBin", "File was handed to the Windows Recycle Bin."),
                                CancellationToken.None);
                        }
                        catch (Exception auditException)
                        {
                            errorCount++;
                            DedupExecutionErrors.Add(FormatString("DedupExecuteAuditWriteFailed", file.RelativePath, auditException.Message));
                            logger.LogError(auditException, "Dedup audit append failed after recycling {Path}", file.FullPath);
                        }
                    }
                    catch (Exception exception)
                    {
                        errorCount++;
                        DedupExecutionErrors.Add(FormatString("DedupExecuteErrorLine", file.RelativePath, exception.Message));
                        logger.LogWarning(exception, "Dedup recycle failed for {Path}", file.FullPath);

                        try
                        {
                            await dedupAuditStore.AppendEntryAsync(
                                auditRun.RunId,
                                CreateDedupAuditEntry(file, "recycle-result", "Failed", exception.Message),
                                CancellationToken.None);
                        }
                        catch (Exception auditException)
                        {
                            logger.LogError(auditException, "Dedup audit append failed after recycle failure for {Path}", file.FullPath);
                        }
                    }
                    finally
                    {
                        processed++;
                        ProgressValue = processed;
                        StatusMessage = FormatString("StatusDedupExecuteProgress", processed, filesToRecycle.Count);
                    }
                }
            }

            DedupFilesRecycledCount = recycledCount;
            DedupRecycleErrorCount = errorCount;
            DedupBytesFreed = bytesFreed;
            OnPropertyChanged(nameof(DedupBytesFreedMb));
            DedupExecutionSummary = FormatString(
                "DedupExecuteSummary",
                recycledCount,
                DedupGroups.Count(group => group.IsSkipped),
                DedupBytesFreedMb,
                errorCount);

            await dedupAuditStore.CompleteRunAsync(
                auditRun.RunId,
                new DedupAuditRunCompleted
                {
                    Status = errorCount == 0 ? "Completed" : "CompletedWithErrors",
                    FilesRecycled = recycledCount,
                    FilesSkipped = DedupGroups.Count(group => group.IsSkipped),
                    Errors = errorCount,
                    BytesFreed = bytesFreed
                },
                CancellationToken.None);

            HasDedupExecutionResults = true;
            StatusMessage = DedupExecutionSummary;
        }
        catch (OperationCanceledException)
        {
            await dedupAuditStore.CompleteRunAsync(
                auditRun.RunId,
                new DedupAuditRunCompleted
                {
                    Status = "Canceled",
                    FilesRecycled = recycledCount,
                    FilesSkipped = DedupGroups.Count(group => group.IsSkipped),
                    Errors = errorCount,
                    BytesFreed = bytesFreed
                },
                CancellationToken.None);
            StatusMessage = GetString("StatusCanceled");
        }
        finally
        {
            IsBusy = false;
            currentCancellationTokenSource?.Dispose();
            currentCancellationTokenSource = null;
            NotifyWizardNavigationChanged();
        }
    }

    [RelayCommand]
    private void OpenRecycleBin()
    {
        Process.Start(new ProcessStartInfo("shell:RecycleBinFolder") { UseShellExecute = true });
    }

    [RelayCommand]
    private void CancelCurrentWork()
    {
        currentCancellationTokenSource?.Cancel();
    }

    [RelayCommand]
    private void SelectExecutable()
    {
        SetSelection(item => item.CanSelect);
    }

    [RelayCommand]
    private void ClearSelection()
    {
        SetSelection(_ => false);
    }

    [RelayCommand]
    private void SelectDuplicateGroupOperations()
    {
        if (SelectedDuplicateGroup is null)
        {
            return;
        }

        var target = SelectedDuplicateGroup.CanonicalRelativePath;
        SetSelection(item =>
            item.CanSelect &&
            item.Operation.DuplicateDetected &&
            string.Equals(item.Operation.DuplicateOfRelativePath, target, StringComparison.OrdinalIgnoreCase));
    }

    [RelayCommand]
    private void SelectDuplicateGroup()
    {
        if (SelectedDuplicateGroup is null || currentPlan is null)
        {
            return;
        }

        suppressDuplicateSelection = true;
        try
        {
            foreach (var item in PlanOperations)
            {
                item.IsDuplicateGroupSelected = false;
            }

            var target = SelectedDuplicateGroup.CanonicalRelativePath;
            foreach (var item in PlanOperations)
            {
                if (item.Operation.DuplicateDetected &&
                    string.Equals(item.Operation.DuplicateOfRelativePath, target, StringComparison.OrdinalIgnoreCase))
                {
                    item.IsDuplicateGroupSelected = true;
                }
            }
        }
        finally
        {
            suppressDuplicateSelection = false;
        }
    }

    [RelayCommand]
    private async Task ExecuteSelectedAsync()
    {
        if (currentPlan is null)
        {
            dialogService.ShowInformation(GetString("DialogNothingToExecuteTitle"), GetString("DialogNothingToExecuteBody"));
            return;
        }

        var selectedIds = PlanOperations.Where(item => item.IsSelected && item.CanSelect).Select(item => item.OperationId).ToList();
        if (selectedIds.Count == 0)
        {
            dialogService.ShowInformation(GetString("DialogNothingSelectedTitle"), GetString("DialogNothingSelectedBody"));
            return;
        }

        if (!dialogService.Confirm(
                GetString("DialogConfirmExecutionTitle"),
                FormatString("DialogConfirmExecutionBody", selectedIds.Count)))
        {
            return;
        }

        currentCancellationTokenSource = new CancellationTokenSource();

        try
        {
            IsBusy = true;
            StatusMessage = GetString("StatusExecuting");

            var outcome = await planExecutionService.ExecuteAsync(
                currentPlan,
                selectedIds,
                new Progress<WorkflowProgress>(UpdateProgress),
                currentCancellationTokenSource.Token);

            var outcomeSummary = FormatExecutionOutcomeSummary(outcome);
            StatusMessage = outcomeSummary;
            logger.LogInformation(outcomeSummary);

            foreach (var message in outcome.Messages)
            {
                logger.LogInformation(message);
            }

            await RefreshRollbackHistoryAsync(CancellationToken.None, outcome.Journal?.JournalId);
            await RefreshPersistenceStatusAsync(CancellationToken.None);
            dialogService.ShowInformation(GetString("DialogExecutionFinishedTitle"), outcomeSummary);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = GetString("StatusCanceled");
            logger.LogInformation("Execution canceled.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Execution failed.");
            dialogService.ShowError(GetString("DialogExecutionFailedTitle"), exception.Message);
        }
        finally
        {
            IsBusy = false;
            currentCancellationTokenSource?.Dispose();
            currentCancellationTokenSource = null;
        }
    }

    [RelayCommand]
    private async Task RollbackLatestAsync()
    {
        var journals = await rollbackService.LoadHistoryAsync(CancellationToken.None);
        var latestJournal = journals.FirstOrDefault();
        var confirmationBody = latestJournal is null
            ? GetString("DialogRollbackLatestBody")
            : await BuildRollbackConfirmationBodyAsync(latestJournal.JournalId);

        if (!dialogService.Confirm(
                GetString("DialogRollbackLatestTitle"),
                confirmationBody))
        {
            return;
        }

        try
        {
            IsBusy = true;
            var outcome = await rollbackService.RollbackLatestAsync(CancellationToken.None);
            var outcomeSummary = FormatExecutionOutcomeSummary(outcome);
            StatusMessage = outcomeSummary;
            logger.LogInformation(outcomeSummary);

            foreach (var message in outcome.Messages)
            {
                logger.LogInformation(message);
            }

            await RefreshRollbackHistoryAsync(CancellationToken.None, SelectedRollbackJournal?.JournalId);
            await RefreshPersistenceStatusAsync(CancellationToken.None);
            dialogService.ShowInformation(GetString("DialogRollbackFinishedTitle"), outcomeSummary);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Rollback failed.");
            dialogService.ShowError(GetString("DialogRollbackFailedTitle"), exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshEnvironmentSanityAsync()
    {
        await RefreshEnvironmentSanityAsync(CancellationToken.None);
    }

    [RelayCommand]
    private async Task ReloadEnvironmentEditorAsync()
    {
        await ReloadEnvironmentEditorAsync(CancellationToken.None);
    }

    [RelayCommand]
    private async Task SaveEnvironmentEditorAsync()
    {
        try
        {
            await environmentConfigService.SaveAsync(BuildEnvironmentFileSettings(), CancellationToken.None);
            await ReloadEnvironmentEditorAsync(CancellationToken.None);
            await RefreshEnvironmentSanityAsync(CancellationToken.None);
            GeminiEnvironmentPingMessage = GetString("EnvironmentSanityEnvSaved");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Saving .env settings failed.");
            GeminiEnvironmentPingStatus = GetEnvironmentSanityStatusLabel(EnvironmentSanityStatus.Invalid);
            GeminiEnvironmentPingMessage = exception.Message;
        }
    }

    [RelayCommand]
    private async Task PingGeminiEnvironmentAsync()
    {
        try
        {
            IsGeminiEnvironmentPingInProgress = true;
            GeminiEnvironmentPingStatus = GetEnvironmentSanityStatusLabel(EnvironmentSanityStatus.Optional);
            GeminiEnvironmentPingMessage = GetString("EnvironmentSanityPingRunning");

            var settings = BuildSettings();
            var result = await environmentSanityService.PingGeminiAsync(settings.Gemini, CancellationToken.None);
            GeminiEnvironmentPingStatus = GetEnvironmentSanityStatusLabel(result.Status);
            GeminiEnvironmentPingMessage = result.Message;

            if (result.Status == EnvironmentSanityStatus.Valid &&
                !string.IsNullOrWhiteSpace(result.SuccessfulFingerprint) &&
                result.SuccessfulAtUtc is not null)
            {
                geminiEnvironmentPingFingerprintCache = result.SuccessfulFingerprint;
                geminiEnvironmentPingValidatedAtUtcCache = result.SuccessfulAtUtc;
                settings.Gemini.EnvironmentPingFingerprint = result.SuccessfulFingerprint;
                settings.Gemini.EnvironmentPingValidatedAtUtc = result.SuccessfulAtUtc;
                await appSettingsStore.SaveAsync(settings, CancellationToken.None);
            }

            await RefreshEnvironmentSanityAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Gemini environment ping failed.");
            GeminiEnvironmentPingStatus = GetEnvironmentSanityStatusLabel(EnvironmentSanityStatus.Invalid);
            GeminiEnvironmentPingMessage = exception.Message;
        }
        finally
        {
            IsGeminiEnvironmentPingInProgress = false;
        }
    }

    [RelayCommand]
    private async Task RollbackSelectedAsync()
    {
        if (SelectedRollbackJournal is null)
        {
            dialogService.ShowInformation(GetString("DialogRollbackSelectionRequiredTitle"), GetString("DialogRollbackSelectionRequiredBody"));
            return;
        }

        var confirmationBody = await BuildRollbackConfirmationBodyAsync(SelectedRollbackJournal.JournalId, SelectedRollbackJournal.Label);
        if (!dialogService.Confirm(
                GetString("DialogRollbackSelectedTitle"),
                confirmationBody))
        {
            return;
        }

        try
        {
            IsBusy = true;
            var outcome = await rollbackService.RollbackAsync(SelectedRollbackJournal.JournalId, CancellationToken.None);
            var outcomeSummary = FormatExecutionOutcomeSummary(outcome);
            StatusMessage = outcomeSummary;
            logger.LogInformation(outcomeSummary);

            foreach (var message in outcome.Messages)
            {
                logger.LogInformation(message);
            }

            await RefreshRollbackHistoryAsync(CancellationToken.None, SelectedRollbackJournal.JournalId);
            await RefreshPersistenceStatusAsync(CancellationToken.None);
            dialogService.ShowInformation(GetString("DialogRollbackFinishedTitle"), outcomeSummary);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Rollback for selected journal failed.");
            dialogService.ShowError(GetString("DialogRollbackFailedTitle"), exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RollbackFolderAsync(string folderName)
    {
        var confirmationBody = await BuildRollbackFolderConfirmationBodyAsync(folderName);
        if (!dialogService.Confirm(
                GetString("DialogUndoFolderTitle"),
                confirmationBody))
        {
            return;
        }

        try
        {
            IsBusy = true;
            var outcome = SelectedRollbackJournal is null
                ? await rollbackService.RollbackFolderAsync(folderName, CancellationToken.None)
                : await rollbackService.RollbackFolderAsync(SelectedRollbackJournal.JournalId, folderName, CancellationToken.None);
            var outcomeSummary = FormatExecutionOutcomeSummary(outcome);
            StatusMessage = outcomeSummary;
            logger.LogInformation(outcomeSummary);

            foreach (var message in outcome.Messages)
            {
                logger.LogInformation(message);
            }

            await RefreshRollbackHistoryAsync(CancellationToken.None, SelectedRollbackJournal?.JournalId);
            await RefreshPersistenceStatusAsync(CancellationToken.None);
            dialogService.ShowInformation(GetString("DialogUndoFinishedTitle"), outcomeSummary);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Folder rollback failed.");
            dialogService.ShowError(GetString("DialogUndoFailedTitle"), exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PreviewSelectedRollbackRunAsync()
    {
        if (SelectedRollbackJournal is null)
        {
            return;
        }

        try
        {
            await LoadRollbackJournalSelectionAsync(SelectedRollbackJournal.JournalId);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Loading rollback preview for selected journal failed.");
            dialogService.ShowError(GetString("DialogRollbackPreviewFailedTitle"), exception.Message);
        }
    }

    [RelayCommand]
    private async Task PreviewRollbackFolderAsync(string folderName)
    {
        if (SelectedRollbackJournal is null)
        {
            return;
        }

        try
        {
            var preview = await rollbackService.PreviewRollbackFolderAsync(SelectedRollbackJournal.JournalId, folderName, CancellationToken.None);
            ApplyRollbackPreview(
                preview,
                FormatString("RollbackPreviewScopeFolderLabel", folderName),
                FormatString("RollbackPreviewScopeFolderBody", folderName),
                scopedToFolder: true);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Loading rollback preview for folder {Folder} failed.", folderName);
            dialogService.ShowError(GetString("DialogRollbackPreviewFailedTitle"), exception.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanMoveBack))]
    private void Back()
    {
        CurrentStep = GetPreviousStep(CurrentStep);
    }

    [RelayCommand(CanExecute = nameof(CanMoveNext))]
    private void Next()
    {
        CurrentStep = GetNextStep(CurrentStep);
    }

    [RelayCommand]
    private void ApplyStrategyRecommendation(OrganizationStrategyPreset preset)
    {
        SelectedStrategyPreset = StrategyPresets.FirstOrDefault(option => option.Value == preset) ?? SelectedStrategyPreset;
    }

    [RelayCommand]
    private void ApplyGeminiGuidance()
    {
        if (currentPlan?.Guidance is not { GeminiUsed: true } guidance)
        {
            return;
        }

        SelectedStrategyPreset = StrategyPresets.FirstOrDefault(option => option.Value == guidance.PreferredPreset)
                                 ?? SelectedStrategyPreset;
        MaxFolderDepth = Math.Clamp(guidance.SuggestedMaxDepth, 2, 5);
        StatusMessage = FormatString(
            "StatusGeminiGuidanceApplied",
            GetStrategyDisplayName(guidance.PreferredPreset),
            MaxFolderDepth);
    }

    partial void OnSelectedPlanFilterChanged(OptionItem<PlanFilterMode>? value)
    {
        PlanView?.Refresh();
    }

    partial void OnSelectedStrategyPresetChanged(OptionItem<OrganizationStrategyPreset>? value)
    {
        StrategyDisplayName = value?.Label ?? string.Empty;
    }

    partial void OnSelectedAnalysisProfileChanged(OptionItem<string>? value)
    {
        if (suppressAnalysisProfileSelection || value is null)
        {
            return;
        }

        ApplyAnalysisProfile(value.Value, updateSelection: false);
    }

    partial void OnSelectedAppLanguageChanged(OptionItem<string>? value)
    {
        localizationService.ApplyLanguage(value?.Value ?? "de-DE");
        RefreshLocalizedOptionCollections();
        RefreshLocalizedState();
        RefreshEnvironmentSanityAfterLocalization();
        RefreshPersistenceStatusAfterLocalization();
        RefreshRollbackStateAfterLocalization();
    }

    partial void OnCurrentStepChanged(WizardStep value)
    {
        OnPropertyChanged(nameof(CurrentStepIndex));
        OnPropertyChanged(nameof(CurrentStepNumber));
        NotifyWizardNavigationChanged();
    }

    partial void OnRootDirectoryChanged(string value) => NotifyWizardNavigationChanged();

    partial void OnDedupRootFolderChanged(string value) => NotifyWizardNavigationChanged();

    partial void OnSelectedDedupGroupChanged(DedupGroupViewModel? value)
    {
        HasSelectedDedupGroup = value is not null;
    }

    partial void OnDedupBytesFreedChanged(long value) => OnPropertyChanged(nameof(DedupBytesFreedMb));

    partial void OnIsBusyChanged(bool value) => NotifyWizardNavigationChanged();

    partial void OnSelectedRollbackJournalChanged(RollbackJournalItem? value)
    {
        HasSelectedRollbackJournal = value is not null;

        if (value is null)
        {
            SelectedRollbackJournalSummary = string.Empty;
            ResetRollbackPreview();
            PopulateRollbackFolderGroups(null);
            return;
        }

        _ = LoadRollbackJournalSelectionAsync(value.JournalId);
    }

    private void RefreshLocalizedOptionCollections()
    {
        var selectedRenameModeValue = SelectedRenameMode?.Value ?? FileRenameMode.NormalizeWhitespaceAndPunctuation;
        var selectedFolderLanguageModeValue = SelectedFolderLanguageMode?.Value ?? FolderLanguageMode.NormalizeToGerman;
        var selectedConflictModeValue = SelectedConflictMode?.Value ?? ConflictHandlingMode.AppendCounter;
        var selectedPlanFilterValue = SelectedPlanFilter?.Value ?? PlanFilterMode.All;
        var selectedStrategyPresetValue = SelectedStrategyPreset?.Value ?? OrganizationStrategyPreset.SemanticCategoryFirst;
        var selectedDateSourceValue = SelectedPreferredDateSource?.Value ?? DateSourceKind.ModifiedTime;
        var selectedExecutionModeValue = SelectedExecutionMode?.Value ?? ExecutionMode.HeuristicsPlusGeminiReviewFirst;
        var selectedDuplicateHandlingModeValue = SelectedDuplicateHandlingMode?.Value ?? DuplicateHandlingMode.RequireReview;
        var selectedFilenameLanguagePolicyValue = SelectedFilenameLanguagePolicy?.Value ?? FilenameLanguagePolicy.PreferGerman;
        var selectedAnalysisProfileValue = SelectedAnalysisProfile?.Value ?? "standard";

        ResetOptions(RenameModes,
        [
            new(FileRenameMode.KeepOriginal, GetString("OptionRenameKeepOriginal")),
            new(FileRenameMode.NormalizeWhitespaceAndPunctuation, GetString("OptionRenameNormalizeWhitespace")),
            new(FileRenameMode.SuggestCleanNames, GetString("OptionRenameSuggestCleanNames"))
        ]);

        ResetOptions(FolderLanguageModes,
        [
            new(FolderLanguageMode.PreserveOriginal, GetString("OptionFolderLanguagePreserveOriginal")),
            new(FolderLanguageMode.NormalizeToGerman, GetString("OptionFolderLanguageGerman")),
            new(FolderLanguageMode.NormalizeToEnglish, GetString("OptionFolderLanguageEnglish")),
            new(FolderLanguageMode.UseBilingualLabels, GetString("OptionFolderLanguageBilingual"))
        ]);

        ResetOptions(ConflictModes,
        [
            new(ConflictHandlingMode.Skip, GetString("OptionConflictSkip")),
            new(ConflictHandlingMode.AppendCounter, GetString("OptionConflictAppendCounter"))
        ]);

        ResetOptions(PlanFilters,
        [
            new(PlanFilterMode.All, GetString("OptionPlanFilterAll")),
            new(PlanFilterMode.ExecutableOnly, GetString("OptionPlanFilterExecutable")),
            new(PlanFilterMode.NeedsReview, GetString("OptionPlanFilterNeedsReview")),
            new(PlanFilterMode.GeminiOnly, GetString("OptionPlanFilterGemini")),
            new(PlanFilterMode.Duplicates, GetString("OptionPlanFilterDuplicates"))
        ]);

        ResetOptions(StrategyPresets,
        [
            new(OrganizationStrategyPreset.SemanticCategoryFirst, GetString("OptionStrategySemanticCategoryFirst")),
            new(OrganizationStrategyPreset.ProjectFirst, GetString("OptionStrategyProjectFirst")),
            new(OrganizationStrategyPreset.DateFirst, GetString("OptionStrategyDateFirst")),
            new(OrganizationStrategyPreset.HybridProjectDate, GetString("OptionStrategyHybridProjectDate")),
            new(OrganizationStrategyPreset.ArchiveCleanup, GetString("OptionStrategyArchiveCleanup")),
            new(OrganizationStrategyPreset.WorkDocuments, GetString("OptionStrategyWorkDocuments")),
            new(OrganizationStrategyPreset.ResearchLibrary, GetString("OptionStrategyResearchLibrary")),
            new(OrganizationStrategyPreset.ManualCustom, GetString("OptionStrategyManualCustom"))
        ]);

        ResetOptions(DateSources,
        [
            new(DateSourceKind.ContentDerived, GetString("OptionDateSourceContentDerived")),
            new(DateSourceKind.FileName, GetString("OptionDateSourceFileName")),
            new(DateSourceKind.ModifiedTime, GetString("OptionDateSourceModifiedTime")),
            new(DateSourceKind.CreatedTime, GetString("OptionDateSourceCreatedTime"))
        ]);

        ResetOptions(ExecutionModes,
        [
            new(ExecutionMode.HeuristicsPlusGeminiReviewFirst, GetString("OptionExecutionModeReviewFirst")),
            new(ExecutionMode.FullyAssisted, GetString("OptionExecutionModeFullyAssisted")),
            new(ExecutionMode.HeuristicsOnly, GetString("OptionExecutionModeHeuristicsOnly"))
        ]);

        ResetOptions(DuplicateHandlingModes,
        [
            new(DuplicateHandlingMode.RequireReview, GetString("OptionDuplicateHandlingRequireReview")),
            new(DuplicateHandlingMode.RouteToDuplicatesFolder, GetString("OptionDuplicateHandlingRouteToFolder")),
            new(DuplicateHandlingMode.Skip, GetString("OptionDuplicateHandlingSkip"))
        ]);

        ResetOptions(FilenameLanguagePolicies,
        [
            new(FilenameLanguagePolicy.PreserveSourceLanguage, GetString("OptionFilenameLanguagePreserveSource")),
            new(FilenameLanguagePolicy.PreferGerman, GetString("OptionFilenameLanguagePreferGerman")),
            new(FilenameLanguagePolicy.PreferEnglish, GetString("OptionFilenameLanguagePreferEnglish")),
            new(FilenameLanguagePolicy.FolderLanguageOnly, GetString("OptionFilenameLanguageFolderOnly"))
        ]);

        ResetOptions(AnalysisProfiles,
        [
            new("fast", GetString("OptionAnalysisProfileFast")),
            new("standard", GetString("OptionAnalysisProfileStandard")),
            new("deep", GetString("OptionAnalysisProfileDeep")),
            new("custom", GetString("OptionAnalysisProfileCustom"))
        ]);

        SelectedRenameMode = RenameModes.First(option => option.Value == selectedRenameModeValue);
        SelectedFolderLanguageMode = FolderLanguageModes.First(option => option.Value == selectedFolderLanguageModeValue);
        SelectedConflictMode = ConflictModes.First(option => option.Value == selectedConflictModeValue);
        SelectedPlanFilter = PlanFilters.First(option => option.Value == selectedPlanFilterValue);
        SelectedStrategyPreset = StrategyPresets.First(option => option.Value == selectedStrategyPresetValue);
        SelectedPreferredDateSource = DateSources.First(option => option.Value == selectedDateSourceValue);
        SelectedExecutionMode = ExecutionModes.First(option => option.Value == selectedExecutionModeValue);
        SelectedDuplicateHandlingMode =
            DuplicateHandlingModes.First(option => option.Value == selectedDuplicateHandlingModeValue);
        SelectedFilenameLanguagePolicy =
            FilenameLanguagePolicies.First(option => option.Value == selectedFilenameLanguagePolicyValue);
        SelectedAnalysisProfile = AnalysisProfiles.First(option => option.Value == selectedAnalysisProfileValue);
    }

    private void RefreshLocalizedState()
    {
        StrategyDisplayName = SelectedStrategyPreset?.Label ?? string.Empty;
        PersistenceModeLabel = LocalizePersistenceModeLabel(PersistenceModeLabel);
        GeminiEnvironmentPingStatus = LocalizeEnvironmentSanityStatusLabel(GeminiEnvironmentPingStatus);

        if (!IsBusy && currentPlan is null && string.IsNullOrWhiteSpace(StatusMessage))
        {
            StatusMessage = GetString("StatusReady");
        }
        else if (string.Equals(StatusMessage, "Ready.", StringComparison.Ordinal) ||
                 string.Equals(StatusMessage, "Bereit.", StringComparison.Ordinal))
        {
            StatusMessage = GetString("StatusReady");
        }
    }

    private string FormatProgressMessage(WorkflowProgress progress)
    {
        if (!string.IsNullOrWhiteSpace(progress.MessageResourceKey))
        {
            return FormatString(progress.MessageResourceKey, progress.MessageArguments);
        }

        return progress.Stage switch
        {
            "scan" when progress.Processed > 0 => FormatString("StatusProgressScan", progress.Processed),
            "scan" => GetString("StatusScanning"),
            "classify" => FormatString("StatusProgressClassify", progress.Processed, progress.Total),
            "duplicates" => FormatString("StatusProgressDuplicates", progress.Processed, progress.Total),
            "execute" => FormatString("StatusProgressExecute", progress.Processed + 1, progress.Total, progress.Message),
            _ => string.IsNullOrWhiteSpace(progress.Message) ? GetString("StatusReady") : progress.Message
        };
    }

    private string FormatExecutionOutcomeSummary(ExecutionOutcome outcome)
    {
        if (!string.IsNullOrWhiteSpace(outcome.SummaryResourceKey))
        {
            return FormatString(outcome.SummaryResourceKey, outcome.SummaryArguments);
        }

        return outcome.Summary;
    }

    private string GetString(string resourceKey) => localizationService.GetString(resourceKey);

    private string FormatString(string resourceKey, params object[] arguments) =>
        localizationService.Format(resourceKey, arguments);

    private static void ResetOptions<T>(
        ObservableCollection<OptionItem<T>> target,
        IReadOnlyList<OptionItem<T>> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private bool ApplyPlanFilter(object item)
    {
        if (item is not PlanOperationItemViewModel operation || SelectedPlanFilter is null)
        {
            return true;
        }

        return SelectedPlanFilter.Value switch
        {
            PlanFilterMode.ExecutableOnly => operation.CanSelect,
            PlanFilterMode.NeedsReview => operation.Operation.RequiresReview,
            PlanFilterMode.GeminiOnly => operation.Operation.GeminiUsed,
            PlanFilterMode.Duplicates => operation.Operation.DuplicateDetected,
            _ => true
        };
    }

    partial void OnMaxFileSizeKbChanged(int value) => SyncAnalysisProfileSelection();

    partial void OnPreviewSampleSizeChanged(int value) => SyncAnalysisProfileSelection();

    partial void OnMaxFilesToScanChanged(int value) => SyncAnalysisProfileSelection();

    partial void OnUseGeminiChanged(bool value) => SyncAnalysisProfileSelection();

    partial void OnEnableExactDuplicateDetectionChanged(bool value) => SyncAnalysisProfileSelection();

    private void SetSelection(Func<PlanOperationItemViewModel, bool> predicate)
    {
        var anySelected = false;

        foreach (var item in PlanOperations)
        {
            item.IsSelected = predicate(item);
            if (item.IsSelected)
            {
                anySelected = true;
            }
        }

        if (!anySelected)
        {
            dialogService.ShowInformation(GetString("DialogNoItemsSelectedTitle"), GetString("DialogNoItemsSelectedBody"));
        }
    }

    private bool CanMoveBack() => !IsBusy && CurrentStep is not WizardStep.ModeSelector;

    private bool CanMoveNext()
    {
        if (IsBusy || CurrentStep is WizardStep.ModeSelector or WizardStep.ExecuteRollback or WizardStep.DedupExecute)
        {
            return false;
        }

        return CurrentStep switch
        {
            WizardStep.Folder => !string.IsNullOrWhiteSpace(RootDirectory) && Directory.Exists(RootDirectory),
            WizardStep.Preview => currentPlan is not null,
            WizardStep.DedupScan => DedupScanCompleted,
            WizardStep.DedupReview => AllDedupGroupsReviewed,
            _ => true
        };
    }

    private async Task PersistSettingsAsync(CancellationToken cancellationToken)
    {
        await appSettingsStore.SaveAsync(BuildSettings(), cancellationToken);
    }

    private async Task ReloadEnvironmentEditorAsync(CancellationToken cancellationToken)
    {
        var settings = await environmentConfigService.LoadAsync(cancellationToken);
        EnvironmentFilePath = settings.FilePath;
        EnvGeminiEnabled = settings.GeminiEnabled;
        EnvGeminiApiKey = settings.GeminiApiKey;
        EnvGeminiModel = settings.GeminiModel;
        EnvGeminiEndpointBaseUrl = settings.GeminiEndpointBaseUrl;
        EnvGeminiMaxRequestsPerMinute = settings.GeminiMaxRequestsPerMinute;
        EnvGeminiRequestTimeoutSeconds = settings.GeminiRequestTimeoutSeconds;
        EnvGeminiMaxPromptCharacters = settings.GeminiMaxPromptCharacters;
        EnvFileKitsuneOfflineMode = settings.FileKitsuneOfflineMode;
    }

    private async Task RefreshEnvironmentSanityAsync(CancellationToken cancellationToken)
    {
        var items = await environmentSanityService.GetChecklistAsync(cancellationToken);
        var pingAvailability = await environmentSanityService.GetGeminiPingAvailabilityAsync(BuildSettings().Gemini, cancellationToken);
        EnvironmentSanityItems.Clear();

        foreach (var item in items)
        {
            EnvironmentSanityItems.Add(new EnvironmentSanityItemViewModel
            {
                Key = item.Key,
                DisplayName = item.DisplayName,
                ValuePreview = item.ValuePreview,
                SourceLabel = item.SourceLabel,
                StatusLabel = GetEnvironmentSanityStatusLabel(item.Status),
                Message = item.Message
            });
        }

        HasEnvironmentSanityItems = EnvironmentSanityItems.Count > 0;

        var validCount = items.Count(item => item.Status == EnvironmentSanityStatus.Valid);
        var missingCount = items.Count(item => item.Status == EnvironmentSanityStatus.Missing);
        var invalidCount = items.Count(item => item.Status == EnvironmentSanityStatus.Invalid);
        var optionalCount = items.Count(item => item.Status == EnvironmentSanityStatus.Optional);
        EnvironmentSanitySummary = FormatString(
            "EnvironmentSanitySummary",
            validCount,
            missingCount,
            invalidCount,
            optionalCount);
        IsGeminiEnvironmentPingAllowed = pingAvailability.CanPing;
        GeminiEnvironmentPingAvailabilityMessage = pingAvailability.Message;
    }

    private async Task RefreshPersistenceStatusAsync(CancellationToken cancellationToken)
    {
        var snapshot = await persistenceStatusService.GetStatusAsync(cancellationToken);
        PersistenceModeLabel = snapshot.Mode switch
        {
            PersistenceStatusMode.LocalOnly => GetString("PersistenceModeLocalOnly"),
            PersistenceStatusMode.SharedOnline => GetString("PersistenceModeSharedOnline"),
            PersistenceStatusMode.SharedFallback => GetString("PersistenceModeSharedFallback"),
            _ => snapshot.Mode.ToString()
        };
        PersistencePrimaryStore = FormatString("PersistencePrimaryStore", snapshot.PrimaryStore);
        PersistenceSecondaryStore = FormatString("PersistenceSecondaryStore", snapshot.SecondaryStore);
        PersistenceDetail = string.IsNullOrWhiteSpace(snapshot.DetailKey)
            ? string.Empty
            : GetString(snapshot.DetailKey);
    }

    private string LocalizePersistenceModeLabel(string currentLabel) =>
        currentLabel switch
        {
            "Local only" or "Nur lokal" => GetString("PersistenceModeLocalOnly"),
            "Shared online" or "Geteilt online" => GetString("PersistenceModeSharedOnline"),
            "Shared fallback" or "Geteilter Fallback" => GetString("PersistenceModeSharedFallback"),
            _ => currentLabel
        };

    private async void RefreshPersistenceStatusAfterLocalization()
    {
        try
        {
            await RefreshPersistenceStatusAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Refreshing persistence status after localization failed.");
        }
    }

    private async void RefreshEnvironmentSanityAfterLocalization()
    {
        try
        {
            await RefreshEnvironmentSanityAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Refreshing environment sanity after localization failed.");
        }
    }

    private async void RefreshRollbackStateAfterLocalization()
    {
        if (!HasRollbackHistory)
        {
            return;
        }

        try
        {
            await RefreshRollbackHistoryAsync(CancellationToken.None, SelectedRollbackJournal?.JournalId);
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Refreshing rollback state after localization failed.");
        }
    }

    private async Task<string> BuildRollbackConfirmationBodyAsync(Guid journalId, string? label = null)
    {
        var preview = await rollbackService.PreviewRollbackAsync(journalId, CancellationToken.None);
        if (preview.Journal is null || preview.Entries.Count == 0)
        {
            return label is null
                ? GetString("DialogRollbackLatestBody")
                : FormatString("DialogRollbackSelectedBody", label);
        }

        var resolvedLabel = label ?? FormatString(
            "RollbackHistoryItemLabel",
            preview.Journal.CreatedAtUtc.ToLocalTime(),
            preview.Entries.Count,
            GetJournalStatusLabel(preview.Journal.Status));

        return BuildRollbackImpactConfirmationBody(
            FormatString("DialogRollbackImpactIntro", resolvedLabel),
            preview);
    }

    private async Task<string> BuildRollbackFolderConfirmationBodyAsync(string folderName)
    {
        if (SelectedRollbackJournal is null)
        {
            return FormatString("DialogUndoFolderBody", folderName);
        }

        var preview = await rollbackService.PreviewRollbackFolderAsync(SelectedRollbackJournal.JournalId, folderName, CancellationToken.None);
        if (preview.Journal is null || preview.Entries.Count == 0)
        {
            return FormatString("DialogUndoFolderBody", folderName);
        }

        return BuildRollbackImpactConfirmationBody(
            FormatString("DialogRollbackFolderImpactIntro", folderName),
            preview);
    }

    private AppSettings BuildSettings()
    {
        var dimensions = OrganizationDimension.None;
        if (UseSemanticCategoryDimension)
        {
            dimensions |= OrganizationDimension.SemanticCategory;
        }

        if (UseProjectDimension)
        {
            dimensions |= OrganizationDimension.Project;
        }

        if (UseYearDimension)
        {
            dimensions |= OrganizationDimension.Year;
        }

        if (UseMonthDimension)
        {
            dimensions |= OrganizationDimension.Month;
        }

        if (UseFileTypeSecondaryCriterion)
        {
            dimensions |= OrganizationDimension.FileType;
        }

        var lowConfidence = Math.Clamp(LowConfidenceThreshold, 0.1d, 0.95d);
        var autoApprove = Math.Clamp(AutoApproveConfidenceThreshold, lowConfidence, 0.98d);

        return new AppSettings
        {
            UiLanguage = SelectedAppLanguage?.Value ?? "de-DE",
            Organization = new OrganizationSettings
            {
                RootDirectory = RootDirectory.Trim(),
                IncludePatterns = ParseList(IncludePatternsText),
                ExcludePatterns = ParseList(ExcludePatternsText),
                SupportedContentExtensions = ParseList(SupportedExtensionsText),
                MaxFileSizeForContentInspectionBytes = Math.Max(1, MaxFileSizeKb) * 1024L,
                PreviewSampleSize = Math.Max(1, PreviewSampleSize),
                MaxFilesToScan = Math.Max(1, MaxFilesToScan),
                RemoveEmptyFolders = RemoveEmptyFolders,
                AllowFileRename = AllowFileRename,
                FileRenameMode = SelectedRenameMode?.Value ?? FileRenameMode.NormalizeWhitespaceAndPunctuation,
                NamingTemplate = string.IsNullOrWhiteSpace(NamingTemplate) ? "{originalName}" : NamingTemplate.Trim(),
                FolderLanguageMode = SelectedFolderLanguageMode?.Value ?? FolderLanguageMode.NormalizeToGerman,
                FilenameLanguagePolicy = SelectedFilenameLanguagePolicy?.Value ?? FilenameLanguagePolicy.PreferGerman,
                ConflictHandlingMode = SelectedConflictMode?.Value ?? ConflictHandlingMode.AppendCounter,
                Dimensions = dimensions == OrganizationDimension.None
                    ? OrganizationDimension.SemanticCategory | OrganizationDimension.Year
                    : dimensions,
                UseFileTypeAsSecondaryCriterion = UseFileTypeSecondaryCriterion,
                UseGeminiWhenAvailable = UseGemini,
                OrganizationPolicy = new OrganizationPolicy
                {
                    StrategyPreset = SelectedStrategyPreset?.Value ?? OrganizationStrategyPreset.SemanticCategoryFirst,
                    ManualDimensions = dimensions == OrganizationDimension.None
                        ? OrganizationDimension.SemanticCategory | OrganizationDimension.Year
                        : dimensions,
                    UseFileTypeAsSecondaryCriterion = UseFileTypeSecondaryCriterion,
                    PreferredDateSource = SelectedPreferredDateSource?.Value ?? DateSourceKind.ModifiedTime,
                    MaximumFolderDepth = Math.Max(1, MaxFolderDepth),
                    MergeSparseCategories = MergeSparseCategories,
                    SparseCategoryThreshold = Math.Max(1, SparseCategoryThreshold),
                    MiscellaneousBucketName = string.IsNullOrWhiteSpace(MiscellaneousBucketName)
                        ? "Misc"
                        : MiscellaneousBucketName.Trim(),
                    OnlyCreateDateFoldersWhenReliable = OnlyCreateDateFoldersWhenReliable,
                    PreferGeminiFolderSuggestion = PreferGeminiFolderSuggestion
                },
                ReviewPolicy = new ReviewPolicy
                {
                    LowConfidenceThreshold = lowConfidence,
                    AutoApproveConfidenceThreshold = autoApprove,
                    RequireReviewForRenames = RequireReviewForRenames,
                    SuggestOnlyOnLowConfidence = SuggestOnlyOnLowConfidence,
                    RouteLowConfidenceToReviewFolder = RouteLowConfidenceToReviewFolder,
                    ReviewFolderName = string.IsNullOrWhiteSpace(ReviewFolderName) ? "Review" : ReviewFolderName.Trim(),
                    ExecutionMode = SelectedExecutionMode?.Value ?? ExecutionMode.HeuristicsPlusGeminiReviewFirst
                },
                DuplicatePolicy = new DuplicatePolicy
                {
                    EnableExactDuplicateDetection = EnableExactDuplicateDetection,
                    HandlingMode = SelectedDuplicateHandlingMode?.Value ?? DuplicateHandlingMode.RequireReview,
                    DuplicatesFolderName = string.IsNullOrWhiteSpace(DuplicatesFolderName)
                        ? "Zu prüfende Duplikate"
                        : DuplicatesFolderName.Trim()
                }
            },
            Gemini = new GeminiOptions
            {
                Enabled = UseGemini,
                ApiKey = GeminiApiKey.Trim(),
                Model = string.IsNullOrWhiteSpace(GeminiModel) ? "gemini-2.0-flash" : GeminiModel.Trim(),
                MaxRequestsPerMinute = Math.Max(1, GeminiMaxRequestsPerMinute),
                RequestTimeoutSeconds = Math.Max(5, GeminiRequestTimeoutSeconds),
                EnvironmentPingValidatedAtUtc = geminiEnvironmentPingValidatedAtUtcCache,
                EnvironmentPingFingerprint = geminiEnvironmentPingFingerprintCache
            }
        };
    }

    private EnvironmentFileSettings BuildEnvironmentFileSettings() =>
        new()
        {
            FilePath = EnvironmentFilePath,
            GeminiEnabled = EnvGeminiEnabled.Trim(),
            GeminiApiKey = EnvGeminiApiKey.Trim(),
            GeminiModel = EnvGeminiModel.Trim(),
            GeminiEndpointBaseUrl = EnvGeminiEndpointBaseUrl.Trim(),
            GeminiMaxRequestsPerMinute = EnvGeminiMaxRequestsPerMinute.Trim(),
            GeminiRequestTimeoutSeconds = EnvGeminiRequestTimeoutSeconds.Trim(),
            GeminiMaxPromptCharacters = EnvGeminiMaxPromptCharacters.Trim(),
            FileKitsuneOfflineMode = EnvFileKitsuneOfflineMode.Trim()
        };

    private void ApplySettings(AppSettings settings)
    {
        SelectedAppLanguage = AppLanguages.FirstOrDefault(option => option.Value == settings.UiLanguage) ?? AppLanguages[0];
        RootDirectory = settings.Organization.RootDirectory;
        IncludePatternsText = string.Join("; ", settings.Organization.IncludePatterns);
        ExcludePatternsText = string.Join("; ", settings.Organization.ExcludePatterns);
        SupportedExtensionsText = string.Join("; ", settings.Organization.SupportedContentExtensions);
        MaxFileSizeKb = (int)(settings.Organization.MaxFileSizeForContentInspectionBytes / 1024);
        PreviewSampleSize = settings.Organization.PreviewSampleSize;
        MaxFilesToScan = settings.Organization.MaxFilesToScan;
        RemoveEmptyFolders = settings.Organization.RemoveEmptyFolders;
        AllowFileRename = settings.Organization.AllowFileRename;
        NamingTemplate = settings.Organization.NamingTemplate;
        UseSemanticCategoryDimension = settings.Organization.Dimensions.HasFlag(OrganizationDimension.SemanticCategory);
        UseProjectDimension = settings.Organization.Dimensions.HasFlag(OrganizationDimension.Project);
        UseYearDimension = settings.Organization.Dimensions.HasFlag(OrganizationDimension.Year);
        UseMonthDimension = settings.Organization.Dimensions.HasFlag(OrganizationDimension.Month);
        UseFileTypeSecondaryCriterion = settings.Organization.UseFileTypeAsSecondaryCriterion;
        LowConfidenceThreshold = settings.Organization.LowConfidenceThreshold;

        SelectedRenameMode = RenameModes.First(option => option.Value == settings.Organization.FileRenameMode);
        SelectedFolderLanguageMode = FolderLanguageModes.First(option => option.Value == settings.Organization.FolderLanguageMode);
        SelectedConflictMode = ConflictModes.First(option => option.Value == settings.Organization.ConflictHandlingMode);
        SelectedStrategyPreset = StrategyPresets.First(option => option.Value == settings.Organization.StrategyPreset);
        SelectedPreferredDateSource = DateSources.First(option => option.Value == settings.Organization.PreferredDateSource);
        SelectedExecutionMode = ExecutionModes.First(option => option.Value == settings.Organization.ExecutionMode);
        SelectedDuplicateHandlingMode =
            DuplicateHandlingModes.First(option => option.Value == settings.Organization.DuplicateHandlingMode);
        SelectedFilenameLanguagePolicy =
            FilenameLanguagePolicies.First(option => option.Value == settings.Organization.FilenameLanguagePolicy);
        MaxFolderDepth = settings.Organization.OrganizationPolicy.MaximumFolderDepth;
        MergeSparseCategories = settings.Organization.OrganizationPolicy.MergeSparseCategories;
        SparseCategoryThreshold = settings.Organization.OrganizationPolicy.SparseCategoryThreshold;
        MiscellaneousBucketName = settings.Organization.OrganizationPolicy.MiscellaneousBucketName;
        OnlyCreateDateFoldersWhenReliable = settings.Organization.OrganizationPolicy.OnlyCreateDateFoldersWhenReliable;
        PreferGeminiFolderSuggestion = settings.Organization.OrganizationPolicy.PreferGeminiFolderSuggestion;
        SuggestOnlyOnLowConfidence = settings.Organization.ReviewPolicy.SuggestOnlyOnLowConfidence;
        RequireReviewForRenames = settings.Organization.ReviewPolicy.RequireReviewForRenames;
        RouteLowConfidenceToReviewFolder = settings.Organization.ReviewPolicy.RouteLowConfidenceToReviewFolder;
        ReviewFolderName = settings.Organization.ReviewPolicy.ReviewFolderName;
        AutoApproveConfidenceThreshold = settings.Organization.ReviewPolicy.AutoApproveConfidenceThreshold;
        EnableExactDuplicateDetection = settings.Organization.DuplicatePolicy.EnableExactDuplicateDetection;
        DuplicatesFolderName = settings.Organization.DuplicatePolicy.DuplicatesFolderName;

        UseGemini = settings.Gemini.Enabled;
        GeminiApiKey = settings.Gemini.ApiKey;
        GeminiModel = settings.Gemini.Model;
        GeminiMaxRequestsPerMinute = settings.Gemini.MaxRequestsPerMinute;
        GeminiRequestTimeoutSeconds = settings.Gemini.RequestTimeoutSeconds;
        geminiEnvironmentPingValidatedAtUtcCache = settings.Gemini.EnvironmentPingValidatedAtUtc;
        geminiEnvironmentPingFingerprintCache = settings.Gemini.EnvironmentPingFingerprint;
        SyncAnalysisProfileSelection();
    }

    private void UpdateProgress(WorkflowProgress progress)
    {
        ProgressMaximum = Math.Max(1, progress.Total == 0 ? 1 : progress.Total);
        ProgressValue = Math.Min(ProgressMaximum, progress.Processed);
        IsProgressIndeterminate = progress.Total == 0;
        StatusMessage = FormatProgressMessage(progress);
    }

    private void UpdateSummary(PlanSummary summary)
    {
        TotalItems = summary.TotalItems;
        MoveCount = summary.MoveCount + summary.MoveAndRenameCount;
        RenameCount = summary.RenameCount + summary.MoveAndRenameCount;
        NeedsReviewCount = summary.RequiresReviewCount;
        GeminiAssistedCount = summary.GeminiAssistedCount;
        DuplicateCount = summary.DuplicateCount;
    }

    [RelayCommand]
    private void ApplyFastAnalysisProfile() => ApplyAnalysisProfile("fast");

    [RelayCommand]
    private void ApplyStandardAnalysisProfile() => ApplyAnalysisProfile("standard");

    [RelayCommand]
    private void ApplyDeepAnalysisProfile() => ApplyAnalysisProfile("deep");

    private void ApplyAnalysisProfile(string profile, bool updateSelection = true)
    {
        switch (profile)
        {
            case "fast":
                MaxFileSizeKb = 256;
                PreviewSampleSize = 150;
                MaxFilesToScan = 1_000;
                UseGemini = false;
                EnableExactDuplicateDetection = false;
                break;
            case "deep":
                MaxFileSizeKb = 1_024;
                PreviewSampleSize = 1_200;
                MaxFilesToScan = 10_000;
                UseGemini = true;
                EnableExactDuplicateDetection = true;
                break;
            default:
                MaxFileSizeKb = 512;
                PreviewSampleSize = 500;
                MaxFilesToScan = 5_000;
                UseGemini = true;
                EnableExactDuplicateDetection = false;
                profile = "standard";
                break;
        }

        if (updateSelection)
        {
            suppressAnalysisProfileSelection = true;
            SelectedAnalysisProfile = AnalysisProfiles.First(option => option.Value == profile);
            suppressAnalysisProfileSelection = false;
        }

        StatusMessage = FormatString("StatusAnalysisProfileApplied", SelectedAnalysisProfile?.Label ?? profile);
    }

    private void SyncAnalysisProfileSelection()
    {
        if (AnalysisProfiles.Count == 0 || suppressAnalysisProfileSelection)
        {
            return;
        }

        var profile = DetermineAnalysisProfile();
        var option = AnalysisProfiles.FirstOrDefault(item => item.Value == profile) ?? AnalysisProfiles[1];

        suppressAnalysisProfileSelection = true;
        SelectedAnalysisProfile = option;
        suppressAnalysisProfileSelection = false;
    }

    private string DetermineAnalysisProfile()
    {
        if (MaxFileSizeKb == 256 &&
            PreviewSampleSize == 150 &&
            MaxFilesToScan == 1_000 &&
            !UseGemini &&
            !EnableExactDuplicateDetection)
        {
            return "fast";
        }

        if (MaxFileSizeKb == 512 &&
            PreviewSampleSize == 500 &&
            MaxFilesToScan == 5_000 &&
            UseGemini &&
            !EnableExactDuplicateDetection)
        {
            return "standard";
        }

        if (MaxFileSizeKb == 1_024 &&
            PreviewSampleSize == 1_200 &&
            MaxFilesToScan == 10_000 &&
            UseGemini &&
            EnableExactDuplicateDetection)
        {
            return "deep";
        }

        return "custom";
    }

    private void PopulateDuplicateGroups(OrganizationPlan plan)
    {
        DuplicateGroups.Clear();
        SelectedDuplicateGroup = null;
        DuplicateGroupOperations.Clear();
        HasSelectedDuplicateGroup = false;

        var groups = plan.Operations
            .Where(operation => operation.DuplicateDetected && !string.IsNullOrWhiteSpace(operation.DuplicateOfRelativePath))
            .GroupBy(operation => operation.DuplicateOfRelativePath, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var group in groups)
        {
            var plannedDuplicates = group
                .Where(operation => operation.OperationType is not PlanOperationType.Skip)
                .ToList();

            var duplicates = group
                .Select(operation => operation.CurrentRelativePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            DuplicateGroups.Add(new DuplicateGroupItem
            {
                CanonicalRelativePath = group.Key,
                DuplicateCount = duplicates.Count,
                DuplicateList = duplicates.Count == 0 ? string.Empty : string.Join(", ", duplicates),
                PlannedDuplicateCount = plannedDuplicates.Count
            });
        }

        HasDuplicateGroups = DuplicateGroups.Count > 0;
    }

    partial void OnSelectedDuplicateGroupChanged(DuplicateGroupItem? value)
    {
        if (suppressDuplicateSelection)
        {
            return;
        }

        SelectDuplicateGroup();
        PopulateDuplicateGroupOperations();
    }

    private void PopulateDuplicateGroupOperations()
    {
        DuplicateGroupOperations.Clear();

        if (SelectedDuplicateGroup is null)
        {
            HasSelectedDuplicateGroup = false;
            return;
        }

        var target = SelectedDuplicateGroup.CanonicalRelativePath;
        foreach (var item in PlanOperations.Where(item =>
                     item.Operation.DuplicateDetected &&
                     string.Equals(item.Operation.DuplicateOfRelativePath, target, StringComparison.OrdinalIgnoreCase)))
        {
            DuplicateGroupOperations.Add(item);
        }

        HasSelectedDuplicateGroup = DuplicateGroupOperations.Count > 0;
    }

    private void PopulateDedupGroups(
        IReadOnlyList<ScannedFile> files,
        IReadOnlyDictionary<string, DuplicateMatch> matches)
    {
        DedupGroups.Clear();
        var filesByRelativePath = files.ToDictionary(file => file.RelativePath, StringComparer.OrdinalIgnoreCase);

        var groups = matches
            .GroupBy(match => match.Value.CanonicalRelativePath, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            if (!filesByRelativePath.TryGetValue(group.Key, out var canonical))
            {
                continue;
            }

            var groupFiles = new List<DedupFileItemViewModel>
            {
                CreateDedupFileItem(canonical, isKeeper: true)
            };

            groupFiles.AddRange(group
                .Select(match => filesByRelativePath.TryGetValue(match.Key, out var file) ? file : null)
                .OfType<ScannedFile>()
                .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
                .Select(file => CreateDedupFileItem(file, isKeeper: false)));

            if (groupFiles.Count <= 1)
            {
                continue;
            }

            var viewModel = new DedupGroupViewModel(groupFiles);
            viewModel.PropertyChanged += (_, _) => RefreshDedupReviewState();
            DedupGroups.Add(viewModel);
        }

        HasDedupGroups = DedupGroups.Count > 0;
        SelectedDedupGroup = DedupGroups.FirstOrDefault();
        RefreshDedupReviewState();
    }

    private static DedupFileItemViewModel CreateDedupFileItem(ScannedFile file, bool isKeeper) =>
        new()
        {
            FullPath = file.FullPath,
            RelativePath = file.RelativePath,
            SizeBytes = file.SizeBytes,
            ModifiedUtc = file.ModifiedUtc,
            IsKeeper = isKeeper
        };

    private DedupAuditRunStarted BuildDedupAuditRunStarted(IReadOnlyList<DedupFileItemViewModel> filesToRecycle) =>
        new()
        {
            RunId = Guid.NewGuid(),
            RootDirectory = DedupRootFolder,
            TotalGroups = DedupGroups.Count,
            ResolvedGroups = DedupGroups.Count(group => group.IsResolved && !group.IsSkipped),
            SkippedGroups = DedupGroups.Count(group => group.IsSkipped),
            FilesPlannedForRecycle = filesToRecycle.Count,
            BytesPlannedForRecycle = filesToRecycle.Sum(file => file.SizeBytes),
            Groups = DedupGroups.Select(CreateDedupAuditGroup).ToList()
        };

    private static DedupAuditGroup CreateDedupAuditGroup(DedupGroupViewModel group)
    {
        var keeper = group.Keeper ?? group.Files.First();
        var duplicates = group.Files.Where(file => !file.IsKeeper).ToList();

        return new DedupAuditGroup
        {
            KeeperFullPath = keeper.FullPath,
            KeeperRelativePath = keeper.RelativePath,
            IsSkipped = group.IsSkipped,
            DuplicateFullPaths = duplicates.Select(file => file.FullPath).ToList(),
            DuplicateRelativePaths = duplicates.Select(file => file.RelativePath).ToList()
        };
    }

    private static DedupAuditEntry CreateDedupAuditEntry(
        DedupFileItemViewModel file,
        string action,
        string status,
        string message) =>
        new()
        {
            Action = action,
            FullPath = file.FullPath,
            RelativePath = file.RelativePath,
            SizeBytes = file.SizeBytes,
            Status = status,
            Message = message
        };

    private void ResetDedupState(bool clearRoot)
    {
        DedupGroups.Clear();
        HasDedupGroups = false;
        DedupScanCompleted = false;
        SelectedDedupGroup = null;
        HasSelectedDedupGroup = false;
        DedupResolvedCount = 0;
        DedupSkippedCount = 0;
        DedupFilesRecycledCount = 0;
        DedupRecycleErrorCount = 0;
        DedupBytesFreed = 0;
        DedupExecutionSummary = string.Empty;
        DedupLastAuditPath = string.Empty;
        DedupExecutionErrors.Clear();
        HasDedupExecutionResults = false;
        if (clearRoot)
        {
            DedupRootFolder = string.Empty;
        }

        OnPropertyChanged(nameof(AllDedupGroupsReviewed));
        OnPropertyChanged(nameof(DedupReviewTally));
        OnPropertyChanged(nameof(DedupBytesFreedMb));
        NotifyWizardNavigationChanged();
    }

    private void RefreshDedupReviewState()
    {
        DedupResolvedCount = DedupGroups.Count(group => group.IsResolved);
        DedupSkippedCount = DedupGroups.Count(group => group.IsSkipped);
        HasDedupGroups = DedupGroups.Count > 0;
        HasSelectedDedupGroup = SelectedDedupGroup is not null;
        OnPropertyChanged(nameof(AllDedupGroupsReviewed));
        OnPropertyChanged(nameof(DedupReviewTally));
        NotifyWizardNavigationChanged();
    }

    private void AdvanceDedupSelection()
    {
        SelectedDedupGroup = DedupGroups.FirstOrDefault(group => !group.IsResolved && !group.IsSkipped)
                             ?? SelectedDedupGroup;
    }

    private bool ValidateDedupRootFolder()
    {
        if (string.IsNullOrWhiteSpace(DedupRootFolder) || !Directory.Exists(DedupRootFolder))
        {
            dialogService.ShowError(GetString("DialogRootRequiredTitle"), GetString("DialogRootRequiredBody"));
            return false;
        }

        DedupRootFolder = Path.GetFullPath(DedupRootFolder);
        return true;
    }

    private bool IsPathInsideDedupRoot(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(DedupRootFolder))
        {
            return false;
        }

        var root = Path.GetFullPath(DedupRootFolder)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidate = Path.GetFullPath(fullPath);
        return candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase);
    }

    private void ResetProgress()
    {
        ProgressMaximum = 1;
        ProgressValue = 0;
        IsProgressIndeterminate = true;
    }

    private bool ValidateRootDirectory()
    {
        if (string.IsNullOrWhiteSpace(RootDirectory) || !Directory.Exists(RootDirectory))
        {
            dialogService.ShowError(GetString("DialogRootRequiredTitle"), GetString("DialogRootRequiredBody"));
            return false;
        }

        return true;
    }

    private void PopulateRollbackFolderGroups(ExecutionJournal? journal)
    {
        RollbackFolderGroups.Clear();

        if (journal is null || journal.Entries.Count == 0)
        {
            HasRollbackFolderGroups = false;
            return;
        }

        var groups = journal.Entries
            .GroupBy(entry => GetTopLevelRelativeFolder(journal.RootDirectory, entry.DestinationFullPath),
                StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (groups.Count <= 1)
        {
            HasRollbackFolderGroups = false;
            return;
        }

        foreach (var group in groups)
        {
            RollbackFolderGroups.Add(new RollbackFolderItem
            {
                FolderName = group.Key,
                Count = group.Count(),
                Label = FormatString("RollbackFolderItemLabel", group.Key, group.Count())
            });
        }

        HasRollbackFolderGroups = true;
    }

    private async Task RefreshRollbackHistoryAsync(CancellationToken cancellationToken, Guid? preferredJournalId = null)
    {
        RollbackHistory.Clear();

        var journals = await rollbackService.LoadHistoryAsync(cancellationToken);
        foreach (var journal in journals)
        {
            RollbackHistory.Add(new RollbackJournalItem
            {
                JournalId = journal.JournalId,
                CreatedAtUtc = journal.CreatedAtUtc,
                Status = journal.Status,
                OperationCount = journal.Entries.Count,
                Label = FormatString(
                    "RollbackHistoryItemLabel",
                    journal.CreatedAtUtc.ToLocalTime(),
                    journal.Entries.Count,
                    GetJournalStatusLabel(journal.Status))
            });
        }

        HasRollbackHistory = RollbackHistory.Count > 0;
        var selectedJournalId = preferredJournalId ?? SelectedRollbackJournal?.JournalId;
        SelectedRollbackJournal = RollbackHistory.FirstOrDefault(item => item.JournalId == selectedJournalId) ??
                                  RollbackHistory.FirstOrDefault();
    }

    private async Task LoadRollbackJournalSelectionAsync(Guid journalId)
    {
        try
        {
            var journal = await rollbackService.LoadJournalAsync(journalId, CancellationToken.None);
            PopulateRollbackFolderGroups(journal);
            var preview = await rollbackService.PreviewRollbackAsync(journalId, CancellationToken.None);
            ApplyRollbackPreview(
                preview,
                journal is null
                    ? string.Empty
                    : FormatString(
                        "RollbackPreviewScopeRunLabel",
                        FormatString(
                            "RollbackHistoryItemLabel",
                            journal.CreatedAtUtc.ToLocalTime(),
                            journal.Entries.Count,
                            GetJournalStatusLabel(journal.Status))),
                GetString("RollbackPreviewScopeRunBody"),
                scopedToFolder: false);
            SelectedRollbackJournalSummary = journal is null
                ? string.Empty
                : FormatString(
                    "RollbackHistorySelectedSummary",
                    journal.CreatedAtUtc.ToLocalTime(),
                    journal.Entries.Count,
                    GetJournalStatusLabel(journal.Status),
                    journal.RootDirectory);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Loading rollback journal selection failed.");
            SelectedRollbackJournalSummary = string.Empty;
            ResetRollbackPreview();
            PopulateRollbackFolderGroups(null);
        }
    }

    private void ApplyRollbackPreview(
        RollbackPreview preview,
        string scopeLabel,
        string scopeBody,
        bool scopedToFolder)
    {
        RollbackPreviewEntries.Clear();
        RollbackPreviewSections.Clear();
        RollbackPreviewScopeLabel = scopeLabel;
        RollbackPreviewScopeBody = scopeBody;
        IsRollbackPreviewScopedToFolder = scopedToFolder;

        if (preview.Journal is null || preview.Entries.Count == 0)
        {
            ResetRollbackPreviewSummary();
            HasRollbackPreviewEntries = false;
            return;
        }

        var items = preview.Entries
            .OrderByDescending(item => item.ExecutedAtUtc)
            .ThenBy(item => item.DestinationFullPath, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new RollbackPreviewItem
            {
                ExecutedAtLocal = entry.ExecutedAtUtc.ToLocalTime(),
                SourceRelativePath = GetRelativePathWithinRoot(preview.Journal.RootDirectory, entry.SourceFullPath),
                DestinationRelativePath = GetRelativePathWithinRoot(preview.Journal.RootDirectory, entry.DestinationFullPath),
                Outcome = entry.Outcome,
                Notes = entry.Notes,
                PreviewStatusKind = entry.PreviewStatus,
                PreviewStatus = GetRollbackPreviewStatusLabel(entry.PreviewStatus),
                PreviewMessage = GetRollbackPreviewMessage(entry.PreviewStatus)
            })
            .ToList();

        foreach (var item in items)
        {
            RollbackPreviewEntries.Add(item);
        }

        PopulateRollbackPreviewSections(items);
        HasRollbackPreviewEntries = RollbackPreviewEntries.Count > 0;
        RollbackPreviewReadyCount = preview.ReadyCount;
        RollbackPreviewMissingDestinationCount = preview.MissingDestinationCount;
        RollbackPreviewOriginalPathOccupiedCount = preview.OriginalPathOccupiedCount;
    }

    private void PopulateRollbackPreviewSections(IReadOnlyList<RollbackPreviewItem> items)
    {
        RollbackPreviewSections.Clear();
        AddRollbackPreviewSection(
            items,
            RollbackPreviewStatus.Ready,
            "RollbackPreviewSectionReadyTitle",
            "RollbackPreviewSectionReadyBody");
        AddRollbackPreviewSection(
            items,
            RollbackPreviewStatus.MissingDestination,
            "RollbackPreviewSectionMissingTitle",
            "RollbackPreviewSectionMissingBody");
        AddRollbackPreviewSection(
            items,
            RollbackPreviewStatus.OriginalPathOccupied,
            "RollbackPreviewSectionBlockedTitle",
            "RollbackPreviewSectionBlockedBody");
    }

    private void AddRollbackPreviewSection(
        IReadOnlyList<RollbackPreviewItem> items,
        RollbackPreviewStatus status,
        string titleKey,
        string descriptionKey)
    {
        var matches = items
            .Where(item => item.PreviewStatusKind == status)
            .ToList();

        if (matches.Count == 0)
        {
            return;
        }

        RollbackPreviewSections.Add(new RollbackPreviewSectionItem
        {
            Title = GetString(titleKey),
            Description = GetString(descriptionKey),
            CountLabel = FormatString("RollbackPreviewSectionCountLabel", matches.Count),
            Entries = matches.Take(RollbackPreviewSectionSampleLimit).ToList(),
            RemainingLabel = matches.Count > RollbackPreviewSectionSampleLimit
                ? FormatString("RollbackPreviewSectionRemainingLabel", matches.Count - RollbackPreviewSectionSampleLimit)
                : string.Empty
        });
    }

    private string BuildRollbackImpactConfirmationBody(string intro, RollbackPreview preview)
    {
        var lines = new List<string>
        {
            intro,
            string.Empty,
            FormatString("DialogRollbackImpactReadyLine", preview.ReadyCount),
            FormatString("DialogRollbackImpactMissingLine", preview.MissingDestinationCount),
            FormatString("DialogRollbackImpactBlockedLine", preview.OriginalPathOccupiedCount)
        };

        var exampleSections = BuildRollbackImpactExampleSections(preview);
        if (exampleSections.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add(GetString("DialogRollbackImpactExamplesTitle"));
            lines.AddRange(exampleSections);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private List<string> BuildRollbackImpactExampleSections(RollbackPreview preview)
    {
        var orderedEntries = preview.Entries
            .OrderByDescending(item => item.ExecutedAtUtc)
            .ThenBy(item => item.DestinationFullPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var rootDirectory = preview.Journal?.RootDirectory;

        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            return [];
        }

        var sections = new List<string>();
        AppendRollbackImpactExampleSection(
            sections,
            orderedEntries,
            rootDirectory,
            RollbackPreviewStatus.Ready,
            "DialogRollbackImpactReadyExamplesTitle");
        AppendRollbackImpactExampleSection(
            sections,
            orderedEntries,
            rootDirectory,
            RollbackPreviewStatus.MissingDestination,
            "DialogRollbackImpactMissingExamplesTitle");
        AppendRollbackImpactExampleSection(
            sections,
            orderedEntries,
            rootDirectory,
            RollbackPreviewStatus.OriginalPathOccupied,
            "DialogRollbackImpactBlockedExamplesTitle");

        return sections;
    }

    private void AppendRollbackImpactExampleSection(
        List<string> sections,
        IReadOnlyList<RollbackPreviewEntry> entries,
        string rootDirectory,
        RollbackPreviewStatus status,
        string titleKey)
    {
        var matches = entries
            .Where(entry => entry.PreviewStatus == status)
            .ToList();

        if (matches.Count == 0)
        {
            return;
        }

        sections.Add(GetString(titleKey));

        foreach (var entry in matches.Take(RollbackConfirmationSampleLimit))
        {
            sections.Add(FormatString(
                "DialogRollbackImpactExampleLine",
                GetRelativePathWithinRoot(rootDirectory, entry.DestinationFullPath),
                GetRelativePathWithinRoot(rootDirectory, entry.SourceFullPath)));
        }

        var remainingCount = matches.Count - RollbackConfirmationSampleLimit;
        if (remainingCount > 0)
        {
            sections.Add(FormatString("DialogRollbackImpactMoreExamplesLine", remainingCount));
        }
    }

    private void ResetRollbackPreview()
    {
        RollbackPreviewEntries.Clear();
        RollbackPreviewSections.Clear();
        RollbackPreviewScopeLabel = string.Empty;
        RollbackPreviewScopeBody = string.Empty;
        IsRollbackPreviewScopedToFolder = false;
        HasRollbackPreviewEntries = false;
        ResetRollbackPreviewSummary();
    }

    private void ResetRollbackPreviewSummary()
    {
        RollbackPreviewReadyCount = 0;
        RollbackPreviewMissingDestinationCount = 0;
        RollbackPreviewOriginalPathOccupiedCount = 0;
    }

    private string GetJournalStatusLabel(ExecutionJournalStatus status) =>
        status switch
        {
            ExecutionJournalStatus.Started => GetString("RollbackStatusStarted"),
            ExecutionJournalStatus.Completed => GetString("RollbackStatusCompleted"),
            ExecutionJournalStatus.Canceled => GetString("RollbackStatusCanceled"),
            _ => status.ToString()
        };

    private string GetRollbackPreviewStatusLabel(RollbackPreviewStatus status) =>
        status switch
        {
            RollbackPreviewStatus.Ready => GetString("RollbackPreviewStatusReady"),
            RollbackPreviewStatus.MissingDestination => GetString("RollbackPreviewStatusMissingDestination"),
            RollbackPreviewStatus.OriginalPathOccupied => GetString("RollbackPreviewStatusOriginalPathOccupied"),
            _ => status.ToString()
        };

    private string GetRollbackPreviewMessage(RollbackPreviewStatus status) =>
        status switch
        {
            RollbackPreviewStatus.Ready => GetString("RollbackPreviewMessageReady"),
            RollbackPreviewStatus.MissingDestination => GetString("RollbackPreviewMessageMissingDestination"),
            RollbackPreviewStatus.OriginalPathOccupied => GetString("RollbackPreviewMessageOriginalPathOccupied"),
            _ => string.Empty
        };

    private string GetEnvironmentSanityStatusLabel(EnvironmentSanityStatus status) =>
        status switch
        {
            EnvironmentSanityStatus.Valid => GetString("EnvironmentSanityStatusValid"),
            EnvironmentSanityStatus.Missing => GetString("EnvironmentSanityStatusMissing"),
            EnvironmentSanityStatus.Invalid => GetString("EnvironmentSanityStatusInvalid"),
            _ => GetString("EnvironmentSanityStatusOptional")
        };

    private string LocalizeEnvironmentSanityStatusLabel(string currentLabel) =>
        currentLabel switch
        {
            "Valid" or "Gueltig" => GetString("EnvironmentSanityStatusValid"),
            "Missing" or "Fehlt" => GetString("EnvironmentSanityStatusMissing"),
            "Invalid" or "Ungueltig" => GetString("EnvironmentSanityStatusInvalid"),
            "Optional" => GetString("EnvironmentSanityStatusOptional"),
            _ => currentLabel
        };

    private void PopulateStrategyRecommendations(OrganizationPlan plan)
    {
        StrategyRecommendations.Clear();

        if (plan.Guidance is { GeminiUsed: true } guidance)
        {
            HasGeminiOrganizationGuidance = true;
            GeminiOrganizationGuidanceTitle = FormatString(
                "GeminiGuidanceTitle",
                GetStrategyDisplayName(guidance.PreferredPreset));
            GeminiOrganizationGuidanceBody = FormatString(
                "GeminiGuidanceBody",
                GetStructureBiasLabel(guidance.StructureBias),
                guidance.SuggestedMaxDepth,
                guidance.Reasoning);
        }
        else
        {
            HasGeminiOrganizationGuidance = false;
            GeminiOrganizationGuidanceTitle = string.Empty;
            GeminiOrganizationGuidanceBody = string.Empty;
        }

        foreach (var recommendation in strategyRecommendationService.Recommend(plan))
        {
            StrategyRecommendations.Add(recommendation);
        }

        HasStrategyRecommendations = StrategyRecommendations.Count > 0;
    }

    private void NotifyWizardNavigationChanged()
    {
        BackCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CurrentStepIndex));
        OnPropertyChanged(nameof(CurrentStepNumber));
        OnPropertyChanged(nameof(TotalWizardSteps));
        OnPropertyChanged(nameof(IsWizardShellNavigationVisible));
    }

    private static WizardStep ClampStep(int stepIndex) =>
        stepIndex < (int)WizardStep.ModeSelector
            ? WizardStep.ModeSelector
            : stepIndex > (int)WizardStep.DedupExecute
                ? WizardStep.DedupExecute
                : (WizardStep)stepIndex;

    private static WizardStep GetPreviousStep(WizardStep step) =>
        step switch
        {
            WizardStep.Folder => WizardStep.ModeSelector,
            WizardStep.Strategy => WizardStep.Folder,
            WizardStep.Rules => WizardStep.Strategy,
            WizardStep.Preview => WizardStep.Rules,
            WizardStep.ExecuteRollback => WizardStep.Preview,
            WizardStep.DedupScan => WizardStep.ModeSelector,
            WizardStep.DedupReview => WizardStep.DedupScan,
            WizardStep.DedupExecute => WizardStep.DedupReview,
            _ => WizardStep.ModeSelector
        };

    private static WizardStep GetNextStep(WizardStep step) =>
        step switch
        {
            WizardStep.Folder => WizardStep.Strategy,
            WizardStep.Strategy => WizardStep.Rules,
            WizardStep.Rules => WizardStep.Preview,
            WizardStep.Preview => WizardStep.ExecuteRollback,
            WizardStep.DedupScan => WizardStep.DedupReview,
            WizardStep.DedupReview => WizardStep.DedupExecute,
            _ => step
        };

    private static bool IsDedupFlow(WizardStep step) =>
        step is WizardStep.DedupScan or WizardStep.DedupReview or WizardStep.DedupExecute;

    private static string GetTopLevelRelativeFolder(string rootDirectory, string destinationFullPath)
    {
        if (!destinationFullPath.StartsWith(rootDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var relative = destinationFullPath[rootDirectory.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], 2)[0];
    }

    private static string GetRelativePathWithinRoot(string rootDirectory, string fullPath)
    {
        if (!fullPath.StartsWith(rootDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return fullPath;
        }

        return fullPath[rootDirectory.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private string GetStrategyDisplayName(OrganizationStrategyPreset preset) =>
        StrategyPresets.FirstOrDefault(option => option.Value == preset)?.Label
        ?? preset.ToString();

    private string GetStructureBiasLabel(OrganizationStructureBias bias) =>
        bias switch
        {
            OrganizationStructureBias.Shallower => GetString("GeminiGuidanceBiasShallower"),
            OrganizationStructureBias.Deeper => GetString("GeminiGuidanceBiasDeeper"),
            _ => GetString("GeminiGuidanceBiasBalanced")
        };

    private static List<string> ParseList(string value) =>
        value.Split(['\r', '\n', ';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
