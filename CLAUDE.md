# Claude Notes For FileTransformer

Use this repository as a Windows-only .NET 8 WPF app, not as a generic CLI tool.

## Ground Truth

- The app starts from `src/App/App.xaml.cs`.
- The planner lives in `src/Application/Services/OrganizationWorkflowService.cs`.
- Destination validation is enforced by `PathSafetyService` and `WindowsPathRules`.
- Execution and rollback live in `PlanExecutionService` and `RollbackService`.
- Gemini is optional and advisory. The WPF app stores the API key in `%LocalAppData%\\FileTransformer\\settings.json` via DPAPI, not via `.env`.

## What To Preserve

- Preview-first workflow.
- In-root-only path safety.
- No delete operations in normal execution.
- Clear reasons, warnings, and review flags on every proposed operation.
- Heuristic fallback when Gemini is unavailable or disabled.

## Current Codebase Realities

- Text extraction supports plain text and `.docx`; OCR/PDF handling is not implemented yet.
- Some advanced settings exist in models/view-model enums but are not fully surfaced in `MainWindow.xaml` or persisted by `BuildSettings()`.
- Duplicate detection and protection rules exist in the core even though the default UI does not expose every knob.
- `gemini.json` is present in the repo, but the application runtime does not read it.

## Expected Commands

```powershell
dotnet restore FileTransformer.sln
dotnet build FileTransformer.sln -c Debug
dotnet test FileTransformer.sln -c Debug
```

## Edit Guidance

- For planning changes, inspect `OrganizationWorkflowService`, `DestinationPathBuilder`, `ReviewDecisionService`, and the related tests.
- For configuration changes, inspect `MainWindowViewModel`, `ProtectedAppSettingsStore`, and the WPF bindings together.
- For Gemini changes, inspect `GeminiSemanticClassifier`, `GeminiPromptBuilder`, `GeminiResponseParser`, and `SemanticClassifierCoordinator`.
