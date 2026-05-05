# FileKitsune

FileKitsune is a Windows-only .NET 8 WPF desktop app for safe, preview-first file organization. It scans a chosen root folder, classifies files with local heuristics and optional Gemini assistance, builds an explainable plan, and only executes user-selected moves and renames after local safety checks pass.

## Current State

The app is usable today and currently provides:

- a 5-step wizard: folder, strategy, rules, preview, execute/undo
- a startup mode selector for either organizing files or running the standalone duplicate remover
- German-first defaults for UI, folder labels, and filename language policy
- switchable German/English UI with localized wizard text, option labels, dialogs, and status updates
- strategy presets plus recommendation cards after preview
- Gemini-backed organization guidance for strategy and folder-depth tradeoffs, with explicit one-click application
- cross-file project clustering that can converge mixed files on shared project/workstream labels
- Gemini fallback behavior is covered so malformed/unavailable responses degrade back to local heuristics and no-guidance planning
- exact duplicate detection with size pre-filtering and SHA-256 hashing
- duplicate canonical selection now prefers cleaner paths, then older files, then richer metadata
- duplicate hashing now has explicit large-file test coverage
- duplicate review in the preview step and duplicate-routing options in the rules step
- standalone duplicate-remover flow with scan, keeper review, skip/confirm decisions, and Windows Recycle Bin execution
- standalone duplicate-remover execution writes a local JSONL audit run before any file is moved to the Recycle Bin
- preview-first planning with reasons, warnings, confidence, and review indicators
- saved-run selection in the execute step for both full-run rollback and folder-scoped undo
- rollback preview tab for the selected saved run
- rollback preview now shows expected statuses like ready, missing destination, or original-path conflict
- rollback preview now includes an at-a-glance impact summary plus grouped diff-style path samples before undo
- rollback preview can be scoped to a selected top-level folder before running folder-only undo
- rollback confirmation dialogs now include preview-aware counts and concrete example path restores/skips
- local content extraction for text-like files, `.docx`, and `.pdf`
- large readable files are sampled from both the beginning and end instead of only taking a leading slice
- optional remote persistence via Postgres-compatible databases, including self-hosted Neon, when `NILEDB_URL`, `POSTGRES_URL`, or `DATABASE_URL` is configured
- automatic local SQLite fallback/cache for settings and journals when remote persistence is unavailable or offline mode is enabled
- visible persistence status in the execute step so users can see whether shared storage or local fallback is active
- backend support for loading historical journals by id
- persisted settings and DPAPI-protected Gemini credentials under `%LocalAppData%\FileKitsune`

## What The App Does Right Now

- scans a chosen root folder without modifying anything
- reads lightweight content from readable text-like files, `.docx`, and `.pdf`
- samples large readable documents so late-file context is not lost entirely
- classifies files using deterministic heuristics with optional Gemini enrichment
- harmonizes project/workstream context across related files when shared signals are strong enough
- falls back cleanly to deterministic local planning when Gemini is unavailable, unusable, or throws
- proposes move, rename, move-and-rename, skip, and duplicate-review outcomes
- lets you filter and select executable operations before execution
- journals executed operations so saved runs can be undone later
- duplicate-routed moves use the same journal and rollback path as every other executed move
- persists rollback journal state before execution starts and after each successful move
- stores content hashes in execution journal entries for stronger rollback auditing
- can cache settings and journals locally in SQLite while best-effort syncing shared persistence to Postgres

## Wizard Usage

### Mode selector

- choose `Organize files` for the existing preview-first organization wizard
- choose `Find duplicates` to scan for exact copies, select one keeper per group, and move confirmed copies to the Windows Recycle Bin

### 1. Folder

- choose the root folder to analyze
- choose the UI language: `Deutsch` or `English`

### 2. Strategy

- choose an organization preset
- after a preview has been built, review advisory recommendation cards with reason and confidence
- apply Gemini guidance explicitly when you want its preferred preset and depth tradeoff copied into the current settings

### 3. Rules

- configure include/exclude patterns and readable extensions
- control scan limits, confidence thresholds, naming behavior, language behavior, and conflict handling
- review Gemini’s current structure-bias guidance while tuning advanced planner settings after applying or comparing its advice
- choose date source, execution mode, duplicate handling, duplicate folder name, and advanced planner behavior
- enable or disable Gemini and edit model/API settings

### 4. Preview

- build the preview plan
- inspect summary cards, warnings, duplicate groups, and selected-operation details
- filter the grid and select executable items
- no filesystem changes happen here

### 5. Execute / Undo

- execute only the selected allowed operations
- select a saved run and preview rollback readiness before undoing
- review the rollback impact summary to see what would restore cleanly versus be skipped
- confirm undo actions with preview-aware counts instead of a generic warning only
- roll back a full saved run or undo a top-level folder group from that run
- inspect activity and logs

## Safety Model

- destination paths are validated as Windows-safe relative paths
- final destinations must resolve inside the selected root folder
- Gemini is advisory only; local logic still validates categories, fragments, and final paths
- execution currently performs moves and renames only
- the organization wizard does not delete files
- standalone duplicate removal moves confirmed copies to the Windows Recycle Bin after review
- duplicate removal refuses to move files if it cannot create a local audit trail first
- hidden/system files and reparse points are skipped by default unless policy settings are changed in code

## Duplicate Detection

Exact duplicate detection already exists in the current app:

- candidates are grouped by file size first
- only same-size candidates are hashed
- SHA-256 is used for exact duplicate identity
- duplicate handling can require review, route to a duplicates folder, or skip duplicate items
- standalone duplicate mode uses the same size/hash identity checks, then lets you set the keeper before any file is moved
- standalone duplicate mode writes a per-run audit file with root folder, keeper decisions, skipped groups, move attempts, failures, and completion status

Current limitation:

- duplicate canonical selection is improved, but could still become more domain-aware over time

## Gemini Usage

Gemini support is optional.

- the app stores the Gemini API key encrypted with DPAPI in the current Windows user profile
- runtime settings live in `%LocalAppData%\FileKitsune\settings.json`
- the app can read environment variables or `.env` as fallback when no DPAPI value is present
- Gemini can enrich semantic understanding, but it does not decide executable paths

## Persistence

The app now supports a layered persistence model:

- local protected settings remain in `%LocalAppData%\FileKitsune\settings.json`
- local cached settings and execution journals are also stored in `%LocalAppData%\FileKitsune\persistence.db`
- JSON journal files remain under `%LocalAppData%\FileKitsune\journals` for compatibility and inspection
- if `NILEDB_URL`, `POSTGRES_URL`, or `DATABASE_URL` is configured, the app will try to use Postgres for shared persistence
- `DATABASE_URL` may be either an Npgsql keyword connection string or a `postgres://` / `postgresql://` URL such as the connection strings exported by Neon
- if remote persistence is unavailable, the app falls back to local SQLite automatically
- set `FILEKITSUNE_OFFLINE_MODE=true` to force local-only mode even when remote connection strings are present

Security note:

- Gemini API keys are not synced to Postgres
- they remain local via DPAPI-protected settings or `.env` / process environment fallback

## Current Architecture

- `src/App`: WPF shell, MVVM view models, step views, localization, dialogs, UI services
- `src/Application`: orchestration, planning, execution, rollback, naming, duplicate detection, recommendations
- `src/Domain`: models, enums, presets, path rules, naming and protection policies
- `src/Infrastructure`: local filesystem access, hashing, settings store, journals, Gemini HTTP integration, logging
- `tests/FileTransformer.Tests`: unit tests for planning, execution, rollback, path safety, recommendations, and Gemini parsing

## Build, Test, And Run

```powershell
dotnet restore FileTransformer.sln
dotnet build FileTransformer.sln -c Debug
dotnet test FileTransformer.sln -c Debug
dotnet run --project src/App/FileTransformer.App.csproj
```

The shipped product name is `FileKitsune`, while the current repository and project filenames still use `FileTransformer.*`.
This is the current intended state for compatibility: solution, project, and test filenames remain on the `FileTransformer` technical identifier for now to avoid unnecessary breaking changes for developers, scripts, and local tooling.
If that technical naming is changed in a future release, the rename should be treated as a deliberate breaking-change migration and called out explicitly in release notes and setup guidance.

`FileTransformer.slnx` is also included if you prefer the newer solution format.

## Current Limitations

The app is still evolving. Notable gaps:

- saved-run selection exists for full historical rollback and folder-scoped undo
- rollback preview exists for saved runs and now includes grouped diff-style path samples and folder-scoped preview
- append-safer journaling exists on the backend, rollback status is recorded per entry, and execution journals now include content hashes
- richer journal metadata is better now, but a future checkpoint model could still improve partial-failure recovery further
- PDF extraction is implemented for text-based PDFs with safe fallback on invalid/unreadable files
- scanned/image-only PDFs still fall back without OCR
- OCR and image-first analysis are not implemented yet
- duplicate canonical selection still needs strengthening

## Storage Locations

- settings: `%LocalAppData%\FileKitsune\settings.json`
- sqlite cache: `%LocalAppData%\FileKitsune\persistence.db`
- journals: `%LocalAppData%\FileKitsune\journals`
- dedup audit runs: `%LocalAppData%\FileKitsune\dedup-runs`
- logs: `%LocalAppData%\FileKitsune\logs`

## Developer Notes

- `AGENTS.md`, `AGENT.md`, `CLAUDE.md`, and `GEMINI.md` describe the current expectations for AI coding assistants
- current roadmap and remaining work are tracked in `TODO.md`
- if you change planner, execution, rollback, duplicate detection, or Gemini behavior, update the tests in `tests/FileTransformer.Tests`
