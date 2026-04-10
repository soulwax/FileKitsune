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
- latest-run rollback in the UI
- backend rollback history loading and append-safer journaling
- saved-run selection in the execute step for full rollback and folder-scoped undo
- rollback preview tab for the selected saved run
- rollback preview now includes expected readiness/conflict states
- journal entries persist rollback status and last rollback attempt details

## Preserve These Constraints

- preview before execution
- all paths stay inside the selected root
- Gemini is advisory only
- duplicate identity is hash-based
- no logic in code-behind
- keep App / Application / Domain / Infrastructure boundaries clean

## Highest-Value Next Task

Continue the rollback slice in the UI and journal model:

1. inspect `MainWindowViewModel` execute/rollback step
2. improve rollback preview into a stronger confirmation/diff experience
3. finish richer journal metadata, especially content hash coverage
4. extend journal metadata only in ways that keep existing rollback behavior stable

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
