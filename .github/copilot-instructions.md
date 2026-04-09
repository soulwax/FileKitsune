# GitHub Copilot Instructions

## Repository Context

FileTransformer is a Windows-only .NET 8 WPF desktop application for safe, preview-first file reorganization. The codebase is split into `src/App`, `src/Application`, `src/Domain`, `src/Infrastructure`, and `tests/FileTransformer.Tests`.

## Coding Priorities

- Preserve the preview-first workflow and explicit review model.
- Keep all destination paths inside the selected root directory.
- Do not introduce delete behavior into the normal execution path.
- Keep Gemini optional and advisory, with heuristic fallback always available.
- Prefer deterministic, testable logic in `src/Application` and `src/Domain`.

## Important Current-State Details

- The app stores settings under `%LocalAppData%\\FileTransformer`.
- Gemini API keys are stored with DPAPI by `ProtectedAppSettingsStore`.
- The application runtime does not currently load `.env` or `gemini.json`.
- Text extraction supports text files and `.docx`; OCR/PDF support is not implemented.
- Some richer policy enums and view-model selections already exist, but not all of them are currently exposed in `MainWindow.xaml` or persisted by `MainWindowViewModel.BuildSettings()`.

## Change Guidance

- Planning flow changes usually belong in `OrganizationWorkflowService`, `DestinationPathBuilder`, `ReviewDecisionService`, or `PathSafetyService`.
- UI-facing configuration changes usually require updates to `MainWindow.xaml`, `MainWindowViewModel`, and `ProtectedAppSettingsStore`.
- Execution or rollback changes should keep journal behavior consistent and update tests.
- Add or update unit tests in `tests/FileTransformer.Tests` when modifying planner, execution, path safety, or Gemini parsing behavior.
