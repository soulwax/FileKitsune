# CLAUDE.md

## FileTransformer Assistant Notes

Current repo reality:

- Windows-only `.NET 8` WPF app
- MVVM wizard flow is already in place
- German is the default UI and naming direction
- strategy recommendations are implemented
- exact duplicate detection already uses SHA-256 with size pre-filtering
- rollback still only supports the latest run

## Most Important Constraints

- preview first
- stay inside the selected root
- Gemini is advisory only
- journal every executed mutation
- do not collapse App / Application / Domain / Infrastructure boundaries

## Best Next Slice

Focus on rollback hardening:

1. richer journals
2. append-safe execution journaling
3. historical rollback selection
4. rollback tests

## Useful Seams

- `src/App/ViewModels/MainWindowViewModel.cs`
- `src/Application/Services/PlanExecutionService.cs`
- `src/Application/Services/RollbackService.cs`
- `src/Infrastructure/Configuration/ProtectedAppSettingsStore.cs`
- `tests/FileTransformer.Tests`
