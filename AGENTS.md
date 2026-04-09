# FileTransformer Agent Notes

## Project Snapshot

FileTransformer is a Windows-only .NET 8 WPF desktop app for preview-first file organization. The current implementation scans a chosen root folder, extracts lightweight file content where possible, classifies files with deterministic heuristics and optional Gemini assistance, proposes safe in-root move/rename operations, and can roll back the latest executed journal.

Validated baseline in this repository:

- `dotnet restore FileTransformer.sln`
- `dotnet build FileTransformer.sln -c Debug`
- `dotnet test FileTransformer.sln -c Debug --no-build`

All three commands pass in the current checkout.

## Solution Layout

- `src/App`: WPF shell, `MainWindowViewModel`, dialogs, folder picker, UI logging, localization.
- `src/Application`: orchestration and business logic such as planning, path safety, naming, review decisions, execution, and rollback.
- `src/Domain`: enums, models, Windows path rules, semantic catalog, strategy presets.
- `src/Infrastructure`: local filesystem access, Gemini HTTP client + parser, DPAPI-backed settings store, journal persistence, Serilog setup.
- `tests/FileTransformer.Tests`: focused unit tests for planning, execution, path safety, and Gemini payload parsing.

## Current Runtime Behavior

- App startup is in `src/App/App.xaml.cs` and uses `Host.CreateApplicationBuilder()` with dependency injection.
- User settings are stored under `%LocalAppData%\\FileTransformer`.
- Gemini API keys are persisted via DPAPI in `ProtectedAppSettingsStore`; the WPF app does not currently load `.env`.
- Execution journals are written as JSON under `%LocalAppData%\\FileTransformer\\journals`.
- Structured logs are written under `%LocalAppData%\\FileTransformer\\logs`.
- Content extraction currently supports readable text files plus `.docx`. There is no OCR or PDF parser yet.
- Gemini suggestions are advisory only. `SemanticClassifierCoordinator` merges Gemini output with heuristics, and local path validation still decides what can run.
- Execution uses move/rename only. There is no delete path in the current app flow.

## Important Files And Seams

- `src/Application/Services/OrganizationWorkflowService.cs`: scan -> read -> classify -> date resolve -> duplicate detect -> build plan.
- `src/Application/Services/DestinationPathBuilder.cs`: converts analysis into proposed relative paths, warnings, review flags, and operation types.
- `src/Application/Services/PathSafetyService.cs` and `src/Domain/Services/WindowsPathRules.cs`: core in-root and Windows-safe path enforcement.
- `src/Application/Services/PlanExecutionService.cs`: executes approved operations and writes rollback journals.
- `src/Application/Services/RollbackService.cs`: replays the latest journal in reverse order and optionally removes empty folders.
- `src/Infrastructure/Classification/GeminiSemanticClassifier.cs`: rate-limited Gemini HTTP integration with retries and strict JSON expectations.
- `src/Infrastructure/FileSystem/LocalFileScanner.cs`: include/exclude matching plus hidden/system/reparse-point handling.

## Current State Caveats

- The checked-in `.env` file is not wired into app startup. If you want environment-based Gemini config, that requires code changes.
- The checked-in `gemini.json` appears to be for external Gemini tooling, not for the WPF app runtime.
- `MainWindowViewModel` already contains strategy/date/execution/duplicate/language option lists, but the current `MainWindow.xaml` does not expose most of them and `BuildSettings()` does not persist several of those selections yet.
- Duplicate detection, protection policies, and richer organization policies exist in the core, but the current UI mainly exposes the simpler root/include/exclude/naming/Gemini flow.
- Rollback currently targets the latest journal only. There is a TODO for richer cross-session recovery.

## Working Agreements

- Preserve the preview-first model: build a plan, show reasons/warnings, then execute selected approved operations.
- Preserve the safety contract: proposed destinations must stay relative to the selected root, and execution must not silently escape it.
- Prefer changing domain rules in `src/Domain`, orchestration in `src/Application`, and I/O concerns in `src/Infrastructure`.
- When exposing a new policy in the UI, wire all three places together: `MainWindow.xaml`, `MainWindowViewModel`, and settings persistence/load paths.
- When changing planning, execution, or Gemini parsing behavior, update or add tests under `tests/FileTransformer.Tests`.

## Useful Commands

```powershell
dotnet restore FileTransformer.sln
dotnet build FileTransformer.sln -c Debug
dotnet test FileTransformer.sln -c Debug
dotnet run --project src/App/FileTransformer.App.csproj
```
