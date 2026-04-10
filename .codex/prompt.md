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
- duplicate canonical selection now prefers cleaner paths, then older files, then richer metadata
- duplicate hashing now has explicit size-prefilter and large-file test coverage
- duplicate review surfaced in preview
- latest-run rollback in the UI
- backend rollback history loading and append-safer journaling
- saved-run selection in the execute step for full rollback and folder-scoped undo
- rollback preview tab for the selected saved run
- rollback preview now includes expected readiness/conflict states
- rollback preview now also includes an impact summary before undo
- rollback confirmation dialogs now show preview-aware restore/skip counts
- duplicate-routed moves are covered by execution+rollback tests
- journal entries persist rollback status and last rollback attempt details
- execution journal entries now persist content hashes
- local content extraction for text-like files, DOCX, and text-based PDFs
- invalid/unreadable PDFs already fall back safely and are covered by tests
- optional Postgres/Nile-backed shared persistence for settings snapshots and journals
- local SQLite cache/fallback for offline or unavailable remote persistence
- Gemini API keys remain local and are not synced remotely
- the execute step already surfaces the current persistence mode to the user
- the strategy step already lets the user apply Gemini’s preferred preset and maximum folder depth explicitly

## Preserve These Constraints

- preview before execution
- all paths stay inside the selected root
- Gemini is advisory only
- duplicate identity is hash-based
- no logic in code-behind
- keep App / Application / Domain / Infrastructure boundaries clean

## Highest-Value Next Task

Continue the final trust-polish slice:

1. inspect `MainWindowViewModel` execute/rollback step
2. improve rollback preview/confirmation from impact summary into a stronger confirmation/diff experience
3. deepen cross-file clustering so Gemini guidance affects more than preset/depth advice
4. extend duplicate behavior only if there is still a product gap, not just a testing gap

## Key Files

- `src/Application/Services/PlanExecutionService.cs`
- `src/Application/Services/RollbackService.cs`
- `src/Application/Abstractions/IExecutionJournalStore.cs`
- `src/Infrastructure/*Journal*`
- `src/App/ViewModels/MainWindowViewModel.cs`
- `tests/FileTransformer.Tests`

## Validation Baseline

```powershell
dotnet restore FileTransformer.sln
dotnet build FileTransformer.sln -c Debug --no-restore
dotnet test tests/FileTransformer.Tests/FileTransformer.Tests.csproj -c Debug
```
