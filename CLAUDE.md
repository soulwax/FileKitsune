# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

```bash
# Build solution
dotnet build FileTransformer.sln

# Run the app (WPF — Windows only)
dotnet run --project src/App/FileTransformer.App.csproj

# Run all tests
dotnet test tests/FileTransformer.Tests/FileTransformer.Tests.csproj

# Run a single test class
dotnet test tests/FileTransformer.Tests/ --filter "FullyQualifiedName~DuplicateDetectionServiceTests"

# Run a specific test method
dotnet test tests/FileTransformer.Tests/ --filter "FullyQualifiedName~DuplicateDetectionServiceTests.SomeName"
```

## Architecture

Clean Architecture with four layers — do not collapse their boundaries:

```
Domain          → enums, domain models, no dependencies
Application     → services, abstractions (interfaces), application models; depends on Domain
Infrastructure  → implements Application abstractions; file system, Gemini API, persistence; depends on Application + Domain
App             → WPF entry point, ViewModels, Views, UI services; depends on all layers
```

DI is wired in [App.xaml.cs](src/App/App.xaml.cs) (application services) and [ServiceCollectionExtensions.cs](src/Infrastructure/ServiceCollectionExtensions.cs) (infrastructure services).

## Core Flow

The UI is a 5-step wizard (`WizardStep` enum: Folder → Strategy → Rules → Preview → ExecuteRollback), all driven by `MainWindowViewModel`.

1. User picks a root folder and strategy preset.
2. `OrganizationWorkflowService.BuildPlanAsync` scans files, classifies them (heuristic + optional Gemini), resolves dates, detects duplicates, and builds an `OrganizationPlan` (list of `PlanOperation`s).
3. User reviews the preview and can filter/deselect operations.
4. `PlanExecutionService.ExecuteAsync` applies selected operations and writes every mutation to an `ExecutionJournal`.
5. `RollbackService` can undo by journal ID, latest run, or folder prefix.

## Semantic Classification

`SemanticClassifierCoordinator` runs `HeuristicSemanticClassifier` first, then optionally `GeminiSemanticClassifier` if Gemini is enabled and an API key is present. Gemini wins when its confidence ≥ heuristic confidence or when heuristic returns `"uncategorized"`. Category definitions live in `Domain/Services/SemanticCatalog.cs`.

## Persistence

Settings and execution journals each have three implementations (JSON/Protected, SQLite, Postgres) behind resilient wrappers (`ResilientAppSettingsStore`, `ResilientExecutionJournalStore`). Mode is driven by `PersistenceOptionsResolver`, which reads env vars at startup. Default is local-only (JSON + SQLite).

Key env vars (copy `.env.example` → `.env` at repo root):

| Variable | Purpose |
|---|---|
| `GEMINI_API_KEY` | Enables Gemini classification and organization advice |
| `NILEDB_URL` / `POSTGRES_URL` / `DATABASE_URL` | Enables remote Postgres persistence |
| `FILEKITSUNE_OFFLINE_MODE` | Forces local-only mode even when a Postgres URL is set |

`AppEnvironmentPaths` walks up from the working directory looking for `.env.example`, `FileTransformer.sln`, or `.git` to locate the project root for `.env` loading.

## Key Constraints

- **Preview first** — `PlanExecutionService` only acts on operations the user has confirmed in the preview step.
- **Stay inside the selected root** — `PathSafetyService` enforces this at execution time.
- **Write-ahead journal** — a `Pending` entry is written to `IExecutionJournalStore` *before* `MoveFileAsync` runs so a crash leaves a recoverable record. After a successful move the entry is updated to `Moved` and saved again. Any entry with `Outcome = "Pending"` in a loaded journal signals an interrupted run.
- **Gemini is advisory only** — it cannot trigger mutations; it only influences classification and folder-structure suggestions.

## Known Risks / Open Items (from 2026-04-29 audit)

These were identified but not fully addressed — they need follow-up:

- **`ResilientExecutionJournalStore` store divergence** — SQLite and JSON saves are two sequential awaits with no transaction between them. A crash after the SQLite write but before JSON write leaves stores with different `RollbackStatus`. The SQLite view wins on next load but only because of tiebreaker ordering — fragile.
- **`ContentHash` not verified on rollback** — `RollbackService` has the hash in the journal entry but never compares it against the file at `DestinationFullPath` before moving it back. A file replaced at the destination after the original move is silently relocated over the source path.
- **No OS-folder blocklist** — `ProtectionPolicyService` has no hardcoded guards for `Windows`, `Program Files`, `System32`, etc. If the user selects a high root (e.g., `C:\`) and `SkipHiddenOrSystemFiles` is false, directories lacking the System attribute inside OS folders are unprotected.
- **4 pre-existing test failures** — `PersistenceOptionsResolverTests` and `PersistenceStatusServiceTests` fail because a live `.env` / environment variable on this machine satisfies a connection-string condition the tests expect to be absent. Not caused by code changes.
- **Plan-level destination collision** — two source files can resolve to the same `ProposedRelativePath` at plan-build time; the collision is only resolved at execution time. With `ConflictHandlingMode.Skip` the second file is silently dropped with no pre-execution warning to the user.

## Test Patterns

Tests are xUnit. Infrastructure tests that touch environment variables use `EnvironmentVariableCollection` (a shared fixture) to avoid cross-test pollution. No database is required for any test — Postgres/SQLite stores are mocked or avoided at the unit test level.

## Useful Seams

| File | Role |
|---|---|
| [MainWindowViewModel.cs](src/App/ViewModels/MainWindowViewModel.cs) | All wizard state and UI commands |
| [OrganizationWorkflowService.cs](src/Application/Services/OrganizationWorkflowService.cs) | Plan-building pipeline |
| [PlanExecutionService.cs](src/Application/Services/PlanExecutionService.cs) | Mutation execution + journaling |
| [RollbackService.cs](src/Application/Services/RollbackService.cs) | Undo by journal / folder |
| [SemanticClassifierCoordinator.cs](src/Application/Services/SemanticClassifierCoordinator.cs) | Heuristic + Gemini classification hand-off |
| [ProtectedAppSettingsStore.cs](src/Infrastructure/Configuration/ProtectedAppSettingsStore.cs) | DPAPI-encrypted local settings baseline |
| [ServiceCollectionExtensions.cs](src/Infrastructure/ServiceCollectionExtensions.cs) | Infrastructure DI registration |

## Best Next Slice

1. OCR/image-first handling for scanned PDFs and image-led folders
2. Richer rollback checkpoints for partial-failure recovery across sessions (`RollbackService` has a TODO)
3. Tune duplicate heuristics with real folder samples
