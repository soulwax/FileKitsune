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
   - `MainWindow.xaml` is now a wizard container.
   - New step views exist:
     - `WizardFolderStepView`
     - `WizardStrategyStepView`
     - `WizardRulesStepView`
     - `WizardPreviewStepView`
     - `WizardExecuteStepView`
   - `MainWindowViewModel` has:
     - `WizardStep`
     - `CurrentStep`
     - `CurrentStepIndex`
     - `NextCommand`
     - `BackCommand`

2. German-first localization slice
   - UI default switched to `Strings.de-DE.xaml`.
   - New `ILocalizationService` / `LocalizationService` in `src/App/Services`.
   - `AppSettings` persists `UiLanguage`.
   - `ProtectedAppSettingsStore` loads/saves `UiLanguage`.
   - `MainWindowViewModel` now exposes `AppLanguages` and `SelectedAppLanguage`.
   - Wizard views were converted from hardcoded text to resource-driven text.

3. German-first naming defaults
   - `NamingPolicy.FolderLanguageMode` default is now `NormalizeToGerman`.
   - `NamingPolicy.FilenameLanguagePolicy` default is now `PreferGerman`.
   - `OrganizationSettings` now exposes `FilenameLanguagePolicy`.
   - `MainWindowViewModel.BuildSettings()` persists filename language policy.

Validation already passed after these changes:

```powershell
dotnet build FileTransformer.sln -c Debug
dotnet test FileTransformer.sln -c Debug
```

## Important Repo State

There are local modifications not yet committed in:

- `src/App/App.xaml`
- `src/App/App.xaml.cs`
- `src/App/Localization/Strings.de-DE.xaml`
- `src/App/Localization/Strings.en-US.xaml`
- `src/App/ViewModels/MainWindowViewModel.cs`
- `src/App/Views/Wizard*.xaml`
- `src/Application/Models/AppSettings.cs`
- `src/Domain/Models/NamingPolicy.cs`
- `src/Domain/Models/OrganizationSettings.cs`
- `src/Infrastructure/Configuration/ProtectedAppSettingsStore.cs`
- new `src/App/Services/ILocalizationService.cs`
- new `src/App/Services/LocalizationService.cs`

`src/App/FileTransformer.App.csproj.lscache` also changed because of the build; treat it as generated noise unless the repo intentionally tracks it.

## What To Do Next

Continue in an agile, usable-first way.

The next highest-value slice is:

## STEP 3 — Strategy Presets + Recommendations

Goal:

- make the new Strategy step genuinely useful
- expose existing strategy controls already present in `MainWindowViewModel`
- add lightweight recommendation output after scan

Preferred implementation order:

1. Inspect current strategy-related code:
   - `src/App/ViewModels/MainWindowViewModel.cs`
   - `src/Application/Services/OrganizationWorkflowService.cs`
   - `src/Domain/Services/StrategyPresetCatalog.cs`
   - `src/Domain/Models/PlanSummary.cs`
   - `src/Domain/Models/OrganizationPlan.cs`

2. Add a recommendation model/service in Application:
   - score strategies using currently available signals only
   - keep the first version simple and explainable

3. Use signals that already exist or are easy to derive:
   - category distribution
   - date usage / date-source presence
   - duplicates count from plan summary
   - project/topic presence in plan operations
   - file type spread if easy to compute from scanned/preview data

4. Return top 3 to 5 recommendations with:
   - strategy preset
   - display name
   - reason
   - confidence score

5. Surface them in `WizardStrategyStepView.xaml`:
   - “Recommended for this folder”
   - selectable cards or simple bordered buttons
   - selecting a recommendation should set `SelectedStrategyPreset`

6. Keep it safe:
   - recommendations are advisory only
   - they do not execute anything
   - they should not change path safety or execution logic

## Constraints To Keep

- preview-first only
- no filesystem changes without explicit execution
- no AI-generated paths
- Gemini remains advisory only
- no logic in code-behind
- keep methods small and testable

## After Step 3

Next likely slices:

1. Expose more rule controls already present in the VM
2. Improve Gemini configuration to optionally use `.env`
3. Start rollback upgrade only after UI usability is in a good state

## End Condition For Next Session

Aim to finish:

- strategy recommendation service
- strategy recommendation UI in the wizard
- build green
- tests green

