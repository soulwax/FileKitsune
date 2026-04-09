# New Session Prompt

Continue work on `d:\Workspace\C#\FileTransformer` as a senior .NET 8 / WPF / MVVM engineer.

## Project Direction

The app is being transformed into a:

- safe, preview-first file organization system
- reversible system with strong rollback
- German-first UX and naming flow
- AI-assisted organizer where Gemini is advisory only

Preserve architecture:

- UI in `src/App`
- orchestration in `src/Application`
- rules in `src/Domain`
- IO / hashing / journals / Gemini in `src/Infrastructure`

Do not move logic into code-behind.

## Current Progress

Already implemented in this branch:

1. Wizard shell refactor
   - `MainWindow.xaml` is now a 5-step wizard container.
   - Step views exist:
     - `WizardFolderStepView`
     - `WizardStrategyStepView`
     - `WizardRulesStepView`
     - `WizardPreviewStepView`
     - `WizardExecuteStepView`
   - `MainWindowViewModel` has:
     - `WizardStep`
     - `CurrentStep`
     - `CurrentStepIndex`
     - `CurrentStepNumber`
     - `NextCommand`
     - `BackCommand`

2. German-first localization slice
   - UI default switched to `Strings.de-DE.xaml`.
   - `ILocalizationService` / `LocalizationService` added in `src/App/Services`.
   - `AppSettings` persists `UiLanguage`.
   - `ProtectedAppSettingsStore` loads/saves `UiLanguage`.
   - `MainWindowViewModel` exposes `AppLanguages` and `SelectedAppLanguage`.
   - Wizard views were converted from hardcoded text to resource-driven text.

3. German-first naming defaults
   - `NamingPolicy.FolderLanguageMode` default is now `NormalizeToGerman`.
   - `NamingPolicy.FilenameLanguagePolicy` default is now `PreferGerman`.
   - `OrganizationSettings` now exposes `FilenameLanguagePolicy`.
   - `MainWindowViewModel.BuildSettings()` persists filename language policy.

4. Strategy recommendations
   - `StrategyRecommendationService` added in Application layer.
   - `StrategyRecommendation` model added.
   - recommendations are populated after scan and shown in the Strategy step
   - recommendation cards are advisory only and set `SelectedStrategyPreset`
   - tests were added in `StrategyRecommendationServiceTests`

Validation passed after the latest feature work:

```powershell
dotnet build FileTransformer.sln -c Debug --no-restore
dotnet test FileTransformer.sln -c Debug
```

## Important Repo State

There are local modifications not yet committed in:

- `README.md`
- `src/App/App.xaml`
- `src/App/App.xaml.cs`
- `src/App/Localization/Strings.de-DE.xaml`
- `src/App/Localization/Strings.en-US.xaml`
- `src/App/ViewModels/MainWindowViewModel.cs`
- `src/App/Views/Wizard*.xaml`
- `src/Application/Models/AppSettings.cs`
- new `src/Application/Models/StrategyRecommendation.cs`
- new `src/Application/Services/StrategyRecommendationService.cs`
- `src/Domain/Models/NamingPolicy.cs`
- `src/Domain/Models/OrganizationSettings.cs`
- `src/Infrastructure/Configuration/ProtectedAppSettingsStore.cs`
- new `src/App/Services/ILocalizationService.cs`
- new `src/App/Services/LocalizationService.cs`
- new test `tests/FileTransformer.Tests/Application/StrategyRecommendationServiceTests.cs`

Tracked `.lscache` files may also show changes from build generation. Treat them as generated noise unless the repo intentionally wants them updated.

## What To Do Next

Continue in an agile, usable-first way.

The next highest-value slice is:

## STEP 3.5 / NEXT SLICE — Expose More Rule Controls Already Present In The ViewModel

Goal:

- make the Rules step actually reflect more of the existing planner capabilities
- wire existing VM/domain options into settings persistence and the wizard UI
- improve usefulness before starting the heavier rollback upgrade

Priority controls to expose next:

1. `SelectedPreferredDateSource`
2. `SelectedExecutionMode`
3. `SelectedDuplicateHandlingMode`
4. `SelectedFilenameLanguagePolicy` is already exposed, so keep it stable
5. any missing strategy-related or planner-related settings already modeled but not persisted

## Recommended Implementation Order

1. Inspect current settings flow:
   - `src/App/ViewModels/MainWindowViewModel.cs`
   - `src/Application/Models/AppSettings.cs`
   - `src/Domain/Models/OrganizationSettings.cs`
   - `src/Domain/Models/OrganizationPolicy.cs`
   - `src/Domain/Models/ReviewPolicy.cs`
   - `src/Domain/Models/DuplicatePolicy.cs`
   - `src/Infrastructure/Configuration/ProtectedAppSettingsStore.cs`

2. Fix persistence gaps in `BuildSettings()` / `ApplySettings()`:
   - strategy preset
   - preferred date source
   - execution mode
   - duplicate handling mode
   - any already-exposed language controls that still depend on defaults only

3. Update `WizardRulesStepView.xaml` to surface those existing options cleanly:
   - keep progressive disclosure in mind
   - prefer adding grouped controls over adding a giant wall of settings

4. Add or update tests if the persistence or behavior changes are substantial.

## Constraints To Keep

- preview-first only
- no filesystem changes without explicit execution
- no AI-generated paths
- Gemini remains advisory only
- no logic in code-behind
- keep methods small and testable

## After This Slice

Next likely slices:

1. Improve Gemini configuration to optionally use `.env`
2. Start rollback upgrade:
   - richer journals
   - historical rollback selection
   - rollback preview
3. Add PDF extraction

## End Condition For Next Session

Aim to finish:

- persistence of more rule controls
- Rules step UI for those controls
- build green
- tests green
