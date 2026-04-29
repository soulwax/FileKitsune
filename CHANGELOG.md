# Changelog

## [0.2.0] - 2026-04-29

### Fixed (data-loss audit)
- `JsonExecutionJournalStore`: write to `.tmp` then atomic rename prevents zero-byte journal on crash.
- `JsonExecutionJournalStore`: corrupted per-file JSON no longer wipes the full journal list; bad files are skipped individually.
- `JsonExecutionJournalStore`: sort key changed to `Path.GetFileName` so `yyyyMMdd_HHmmss_` prefix orders correctly.
- `PlanExecutionService`: write-ahead pattern — `Pending` entry saved before `MoveFileAsync`, updated to `Moved` after; surviving `Pending` entries identify interrupted runs.
- `PlanExecutionService`: explicit pre-flight `FileExists` check before move; `OperationCanceledException` re-thrown cleanly without being swallowed by the generic `catch`.
- `LocalFileOperations`: explicit `overwrite: false` on `File.Move`.
- `RollbackService`: folder-scope filter now unions `DestinationFullPath` and `SourceFullPath` so files moved *out of* the selected folder are included.
- `RollbackService`: empty-directory cleanup scoped to the rollback folder instead of the full journal root.
- `RollbackService`: SHA-256 hash verified before each rollback move; changed files are skipped with `SkippedContentMismatch` status.
- `ResilientExecutionJournalStore`: merge tiebreaker changed from `CreatedAtUtc` to `LastSavedAtUtc` so the most-recently-written copy always wins.
- `WindowsPathRules`: reserved device-name check strips the extension first, so `NUL.pdf` / `CON.docx` are caught correctly.
- `LocalFileScanner`: OS-protected paths (`Windows`, `System`, `ProgramFiles`, etc.) resolved at startup and blocked from traversal.

### Added
- `ExecutionJournal.LastSavedAtUtc` property stamped on every save.
- `RollbackEntryStatus.SkippedContentMismatch` (value 5).
- `OrganizationWorkflowService.MarkDestinationCollisions` — plan-level collision warning added to `WarningFlags` before preview is shown.
- Environment configuration service and UI (`EnvironmentConfigViewModel`, sanity-check panel with quick-setup options).
- Gemini 2.0 Flash model support; Gemini advisor upgrades.
- Analysis profile presets for organisation policy selection.
- Rollback enhancements: per-entry status persistence, folder-scoped preview counts, idempotent re-run safety.
- Version shown in the OS window title bar (e.g. `FileKitsune 0.2.0`).

## [0.1.0] - 2026-03-14

### Added
- Initial .NET 8 WPF desktop application scaffold for semantic file and folder organization.
- Dry-run-first planning engine with explainable move and rename proposals, multilingual naming controls, and in-root safety validation.
- Deterministic heuristic classifier for German, English, and mixed German-English content, with Gemini integration behind a replaceable interface.
- Secure user-local settings storage, structured logging, rollback journal support, and unit tests for core planning and validation logic.
