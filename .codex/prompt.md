# New Session Prompt

Continue work on `d:\Workspace\C#\FileTransformer` as a senior .NET 8 / WPF / MVVM engineer.

## Current Product State

The app already has:

- a 5-step wizard
- German-first defaults
- switchable German/English UI
- localized wizard text, option labels, dialogs, and status updates
- strategy presets and recommendation cards
- exact duplicate detection using size pre-filtering and SHA-256
- duplicate review surfaced in preview
- latest-run rollback

## Preserve These Constraints

- preview before execution
- all paths stay inside the selected root
- Gemini is advisory only
- duplicate identity is hash-based
- no logic in code-behind
- keep App / Application / Domain / Infrastructure boundaries clean

## Highest-Value Next Task

Start the rollback hardening slice:

1. inspect `PlanExecutionService` and `RollbackService`
2. design append-safe journal persistence
3. add support for loading more than the latest run
4. add rollback-focused tests before or alongside behavior changes

## Key Files

- `src/Application/Services/PlanExecutionService.cs`
- `src/Application/Services/RollbackService.cs`
- `src/Application/Abstractions/IExecutionJournalStore.cs`
- `src/Infrastructure/*Journal*`
- `src/App/ViewModels/MainWindowViewModel.cs`
- `tests/FileTransformer.Tests`

## Validation Baseline

```powershell
dotnet build FileTransformer.sln -c Debug --no-restore
dotnet test FileTransformer.sln -c Debug
```
