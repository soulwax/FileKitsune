using FileTransformer.Application.Models;
using FileTransformer.Domain.Models;

namespace FileTransformer.Infrastructure.Configuration;

internal static class AppSettingsSnapshotMapper
{
    public static AppSettings SanitizeForSharedPersistence(AppSettings settings)
    {
        return new AppSettings
        {
            UiLanguage = settings.UiLanguage,
            Organization = CloneOrganizationSettings(settings.Organization),
            Gemini = new GeminiOptions
            {
                Enabled = settings.Gemini.Enabled,
                ApiKey = string.Empty,
                Model = settings.Gemini.Model,
                EndpointBaseUrl = settings.Gemini.EndpointBaseUrl,
                MaxRequestsPerMinute = settings.Gemini.MaxRequestsPerMinute,
                RequestTimeoutSeconds = settings.Gemini.RequestTimeoutSeconds,
                MaxPromptCharacters = settings.Gemini.MaxPromptCharacters
            }
        };
    }

    public static AppSettings MergeNonSecretSettings(AppSettings baseline, AppSettings? overlay)
    {
        if (overlay is null)
        {
            return baseline;
        }

        return new AppSettings
        {
            UiLanguage = string.IsNullOrWhiteSpace(overlay.UiLanguage) ? baseline.UiLanguage : overlay.UiLanguage,
            Organization = CloneOrganizationSettings(overlay.Organization),
            Gemini = new GeminiOptions
            {
                Enabled = overlay.Gemini.Enabled,
                ApiKey = baseline.Gemini.ApiKey,
                Model = string.IsNullOrWhiteSpace(overlay.Gemini.Model) ? baseline.Gemini.Model : overlay.Gemini.Model,
                EndpointBaseUrl = string.IsNullOrWhiteSpace(overlay.Gemini.EndpointBaseUrl)
                    ? baseline.Gemini.EndpointBaseUrl
                    : overlay.Gemini.EndpointBaseUrl,
                MaxRequestsPerMinute = overlay.Gemini.MaxRequestsPerMinute <= 0
                    ? baseline.Gemini.MaxRequestsPerMinute
                    : overlay.Gemini.MaxRequestsPerMinute,
                RequestTimeoutSeconds = overlay.Gemini.RequestTimeoutSeconds <= 0
                    ? baseline.Gemini.RequestTimeoutSeconds
                    : overlay.Gemini.RequestTimeoutSeconds,
                MaxPromptCharacters = overlay.Gemini.MaxPromptCharacters <= 0
                    ? baseline.Gemini.MaxPromptCharacters
                    : overlay.Gemini.MaxPromptCharacters
            }
        };
    }

    private static OrganizationSettings CloneOrganizationSettings(OrganizationSettings settings)
    {
        return new OrganizationSettings
        {
            RootDirectory = settings.RootDirectory,
            IncludePatterns = [.. settings.IncludePatterns],
            ExcludePatterns = [.. settings.ExcludePatterns],
            MaxFileSizeForContentInspectionBytes = settings.MaxFileSizeForContentInspectionBytes,
            SupportedContentExtensions = [.. settings.SupportedContentExtensions],
            RemoveEmptyFolders = settings.RemoveEmptyFolders,
            PreviewSampleSize = settings.PreviewSampleSize,
            MaxFilesToScan = settings.MaxFilesToScan,
            UseGeminiWhenAvailable = settings.UseGeminiWhenAvailable,
            MaxConcurrentClassification = settings.MaxConcurrentClassification,
            KeepOperationsInsideRoot = settings.KeepOperationsInsideRoot,
            OrganizationPolicy = new OrganizationPolicy
            {
                ManualDimensions = settings.OrganizationPolicy.ManualDimensions,
                UseFileTypeAsSecondaryCriterion = settings.OrganizationPolicy.UseFileTypeAsSecondaryCriterion,
                StrategyPreset = settings.OrganizationPolicy.StrategyPreset,
                MaximumFolderDepth = settings.OrganizationPolicy.MaximumFolderDepth,
                MergeSparseCategories = settings.OrganizationPolicy.MergeSparseCategories,
                MiscellaneousBucketName = settings.OrganizationPolicy.MiscellaneousBucketName,
                SparseCategoryThreshold = settings.OrganizationPolicy.SparseCategoryThreshold,
                OnlyCreateDateFoldersWhenReliable = settings.OrganizationPolicy.OnlyCreateDateFoldersWhenReliable,
                PreferredDateSource = settings.OrganizationPolicy.PreferredDateSource,
                PreferGeminiFolderSuggestion = settings.OrganizationPolicy.PreferGeminiFolderSuggestion
            },
            NamingPolicy = new NamingPolicy
            {
                AllowFileRename = settings.NamingPolicy.AllowFileRename,
                FileRenameMode = settings.NamingPolicy.FileRenameMode,
                NamingTemplate = settings.NamingPolicy.NamingTemplate,
                FolderLanguageMode = settings.NamingPolicy.FolderLanguageMode,
                ConflictHandlingMode = settings.NamingPolicy.ConflictHandlingMode,
                FilenameLanguagePolicy = settings.NamingPolicy.FilenameLanguagePolicy,
                PreserveDomainSpecificTerms = settings.NamingPolicy.PreserveDomainSpecificTerms,
                MaximumFileNameLength = settings.NamingPolicy.MaximumFileNameLength,
                AddDatePrefix = settings.NamingPolicy.AddDatePrefix,
                CleanupNoiseTokens = settings.NamingPolicy.CleanupNoiseTokens,
                PreserveUmlauts = settings.NamingPolicy.PreserveUmlauts
            },
            ReviewPolicy = new ReviewPolicy
            {
                LowConfidenceThreshold = settings.ReviewPolicy.LowConfidenceThreshold,
                ExecutionMode = settings.ReviewPolicy.ExecutionMode,
                AutoApproveConfidenceThreshold = settings.ReviewPolicy.AutoApproveConfidenceThreshold,
                RequireReviewForRenames = settings.ReviewPolicy.RequireReviewForRenames,
                SuggestOnlyOnLowConfidence = settings.ReviewPolicy.SuggestOnlyOnLowConfidence,
                RouteLowConfidenceToReviewFolder = settings.ReviewPolicy.RouteLowConfidenceToReviewFolder,
                ReviewFolderName = settings.ReviewPolicy.ReviewFolderName,
                DeterministicOnlyExecution = settings.ReviewPolicy.DeterministicOnlyExecution
            },
            DuplicatePolicy = new DuplicatePolicy
            {
                EnableExactDuplicateDetection = settings.DuplicatePolicy.EnableExactDuplicateDetection,
                HandlingMode = settings.DuplicatePolicy.HandlingMode,
                DuplicatesFolderName = settings.DuplicatePolicy.DuplicatesFolderName
            },
            ProtectionPolicy = new ProtectionPolicy
            {
                ProtectedFolderPatterns = [.. settings.ProtectionPolicy.ProtectedFolderPatterns],
                ProtectedFilePatterns = [.. settings.ProtectionPolicy.ProtectedFilePatterns],
                KeepRelatedFilesTogetherByBasename = settings.ProtectionPolicy.KeepRelatedFilesTogetherByBasename,
                TreatProjectOrSessionFoldersAsAtomic = settings.ProtectionPolicy.TreatProjectOrSessionFoldersAsAtomic,
                SkipHiddenOrSystemFiles = settings.ProtectionPolicy.SkipHiddenOrSystemFiles,
                FollowSymlinksOrJunctions = settings.ProtectionPolicy.FollowSymlinksOrJunctions
            }
        };
    }
}
