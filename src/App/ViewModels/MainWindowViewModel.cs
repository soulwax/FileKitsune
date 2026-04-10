using System.Collections.ObjectModel;
using System.ComponentModel;
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
    private readonly IAppSettingsStore appSettingsStore;
    private readonly OrganizationWorkflowService organizationWorkflowService;
    private readonly PlanExecutionService planExecutionService;
    private readonly RollbackService rollbackService;
    private readonly StrategyRecommendationService strategyRecommendationService;
    private readonly IFolderPickerService folderPickerService;
    private readonly IDialogService dialogService;
    private readonly ILocalizationService localizationService;
    private readonly UiLogStore uiLogStore;
    private readonly ILogger<MainWindowViewModel> logger;
    private OrganizationPlan? currentPlan;
    private CancellationTokenSource? currentCancellationTokenSource;
    private bool initialized;
    private bool suppressDuplicateSelection;

    public MainWindowViewModel(
        IAppSettingsStore appSettingsStore,
        OrganizationWorkflowService organizationWorkflowService,
        PlanExecutionService planExecutionService,
        RollbackService rollbackService,
        StrategyRecommendationService strategyRecommendationService,
        IFolderPickerService folderPickerService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        UiLogStore uiLogStore,
        ILogger<MainWindowViewModel> logger)
    {
        this.appSettingsStore = appSettingsStore;
        this.organizationWorkflowService = organizationWorkflowService;
        this.planExecutionService = planExecutionService;
        this.rollbackService = rollbackService;
        this.strategyRecommendationService = strategyRecommendationService;
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
        PlanView = CollectionViewSource.GetDefaultView(PlanOperations);
        PlanView.Filter = ApplyPlanFilter;
        LogEntries = uiLogStore.Entries;
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

    public IReadOnlyList<OptionItem<string>> AppLanguages { get; }

    public ObservableCollection<PlanOperationItemViewModel> PlanOperations { get; }

    public ObservableCollection<StrategyRecommendation> StrategyRecommendations { get; } = [];

    public ObservableCollection<DuplicateGroupItem> DuplicateGroups { get; } = [];

    public ObservableCollection<PlanOperationItemViewModel> DuplicateGroupOperations { get; } = [];

    public ObservableCollection<RollbackFolderItem> RollbackFolderGroups { get; } = [];

    public ICollectionView PlanView { get; }

    public ObservableCollection<UiLogEntry> LogEntries { get; }

    [ObservableProperty]
    private string rootDirectory = string.Empty;

    [ObservableProperty]
    private string includePatternsText = "*";

    [ObservableProperty]
    private string excludePatternsText = ".git; .vs; bin; obj; node_modules";

    [ObservableProperty]
    private string supportedExtensionsText =
        ".txt; .md; .json; .xml; .csv; .cs; .xaml; .ts; .js; .py; .docx";

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
    private string geminiModel = "gemini-1.5-flash";

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
    private DuplicateGroupItem? selectedDuplicateGroup;

    [ObservableProperty]
    private bool hasSelectedDuplicateGroup;

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
    private PlanOperationItemViewModel? selectedOperation;

    [ObservableProperty]
    private bool hasStrategyRecommendations;

    [ObservableProperty]
    private OptionItem<string>? selectedAppLanguage;

    [ObservableProperty]
    private WizardStep currentStep = WizardStep.Folder;

    public int CurrentStepIndex
    {
        get => (int)CurrentStep;
        set => CurrentStep = ClampStep(value);
    }

    public int CurrentStepNumber => CurrentStepIndex + 1;

    public int TotalWizardSteps => 5;

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
    private async Task SaveSettingsAsync()
    {
        try
        {
            await PersistSettingsAsync(CancellationToken.None);
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

            StatusMessage = outcome.Summary;
            logger.LogInformation(outcome.Summary);

            foreach (var message in outcome.Messages)
            {
                logger.LogInformation(message);
            }

            PopulateRollbackFolderGroups(outcome.Journal);
            dialogService.ShowInformation(GetString("DialogExecutionFinishedTitle"), outcome.Summary);
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
        if (!dialogService.Confirm(
                GetString("DialogRollbackLatestTitle"),
                GetString("DialogRollbackLatestBody")))
        {
            return;
        }

        try
        {
            IsBusy = true;
            var outcome = await rollbackService.RollbackLatestAsync(CancellationToken.None);
            StatusMessage = outcome.Summary;
            logger.LogInformation(outcome.Summary);

            foreach (var message in outcome.Messages)
            {
                logger.LogInformation(message);
            }

            dialogService.ShowInformation(GetString("DialogRollbackFinishedTitle"), outcome.Summary);
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
    private async Task RollbackFolderAsync(string folderName)
    {
        if (!dialogService.Confirm(
                GetString("DialogUndoFolderTitle"),
                FormatString("DialogUndoFolderBody", folderName)))
        {
            return;
        }

        try
        {
            IsBusy = true;
            var outcome = await rollbackService.RollbackFolderAsync(folderName, CancellationToken.None);
            StatusMessage = outcome.Summary;
            logger.LogInformation(outcome.Summary);

            foreach (var message in outcome.Messages)
            {
                logger.LogInformation(message);
            }

            dialogService.ShowInformation(GetString("DialogUndoFinishedTitle"), outcome.Summary);
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

    [RelayCommand(CanExecute = nameof(CanMoveBack))]
    private void Back()
    {
        CurrentStep = ClampStep(CurrentStepIndex - 1);
    }

    [RelayCommand(CanExecute = nameof(CanMoveNext))]
    private void Next()
    {
        CurrentStep = ClampStep(CurrentStepIndex + 1);
    }

    [RelayCommand]
    private void ApplyStrategyRecommendation(OrganizationStrategyPreset preset)
    {
        SelectedStrategyPreset = StrategyPresets.FirstOrDefault(option => option.Value == preset) ?? SelectedStrategyPreset;
    }

    partial void OnSelectedPlanFilterChanged(OptionItem<PlanFilterMode>? value) => PlanView.Refresh();

    partial void OnSelectedStrategyPresetChanged(OptionItem<OrganizationStrategyPreset>? value)
    {
        StrategyDisplayName = value?.Label ?? string.Empty;
    }

    partial void OnSelectedAppLanguageChanged(OptionItem<string>? value)
    {
        localizationService.ApplyLanguage(value?.Value ?? "de-DE");
        RefreshLocalizedOptionCollections();
        RefreshLocalizedState();
    }

    partial void OnCurrentStepChanged(WizardStep value)
    {
        OnPropertyChanged(nameof(CurrentStepIndex));
        OnPropertyChanged(nameof(CurrentStepNumber));
        NotifyWizardNavigationChanged();
    }

    partial void OnRootDirectoryChanged(string value) => NotifyWizardNavigationChanged();

    partial void OnIsBusyChanged(bool value) => NotifyWizardNavigationChanged();

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
    }

    private void RefreshLocalizedState()
    {
        StrategyDisplayName = SelectedStrategyPreset?.Label ?? string.Empty;

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

    private bool CanMoveBack() => !IsBusy && CurrentStep is not WizardStep.Folder;

    private bool CanMoveNext()
    {
        if (IsBusy || CurrentStep == WizardStep.ExecuteRollback)
        {
            return false;
        }

        return CurrentStep switch
        {
            WizardStep.Folder => !string.IsNullOrWhiteSpace(RootDirectory) && Directory.Exists(RootDirectory),
            WizardStep.Preview => currentPlan is not null,
            _ => true
        };
    }

    private async Task PersistSettingsAsync(CancellationToken cancellationToken)
    {
        await appSettingsStore.SaveAsync(BuildSettings(), cancellationToken);
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
                Model = string.IsNullOrWhiteSpace(GeminiModel) ? "gemini-1.5-flash" : GeminiModel.Trim(),
                MaxRequestsPerMinute = Math.Max(1, GeminiMaxRequestsPerMinute),
                RequestTimeoutSeconds = Math.Max(5, GeminiRequestTimeoutSeconds)
            }
        };
    }

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
            RollbackFolderGroups.Add(new RollbackFolderItem { FolderName = group.Key, Count = group.Count() });
        }

        HasRollbackFolderGroups = true;
    }

    private void PopulateStrategyRecommendations(OrganizationPlan plan)
    {
        StrategyRecommendations.Clear();

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
    }

    private static WizardStep ClampStep(int stepIndex) =>
        stepIndex < (int)WizardStep.Folder
            ? WizardStep.Folder
            : stepIndex > (int)WizardStep.ExecuteRollback
                ? WizardStep.ExecuteRollback
                : (WizardStep)stepIndex;

    private static string GetTopLevelRelativeFolder(string rootDirectory, string destinationFullPath)
    {
        if (!destinationFullPath.StartsWith(rootDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var relative = destinationFullPath[rootDirectory.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], 2)[0];
    }

    private static List<string> ParseList(string value) =>
        value.Split(['\r', '\n', ';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
