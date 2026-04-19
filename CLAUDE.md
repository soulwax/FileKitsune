# CLAUDE.md

## FileTransformer Assistant Notes

Current repo reality:

- Windows-only `.NET 8` WPF app
- MVVM wizard flow is already in place
- German is the default UI and naming direction
- strategy recommendations are implemented
- exact duplicate detection already uses SHA-256 with size pre-filtering
- rollback now supports saved-run selection, folder-scoped undo, and diff-style preview/confirmation

## Most Important Constraints

- preview first
- stay inside the selected root
- Gemini is advisory only
- journal every executed mutation
- do not collapse App / Application / Domain / Infrastructure boundaries

## Best Next Slice

Focus on trust-polish gaps after rollback UX:

1. OCR/image-first handling for scanned PDFs and image-led folders
2. fully resource-drive remaining Application-layer progress text
3. continue richer rollback checkpoints for partial-failure recovery
4. tune duplicate heuristics with real folder samples

## Useful Seams

- `src/App/ViewModels/MainWindowViewModel.cs`
- `src/Application/Services/PlanExecutionService.cs`
- `src/Application/Services/RollbackService.cs`
- `src/Infrastructure/Configuration/ProtectedAppSettingsStore.cs`
- `tests/FileTransformer.Tests`
