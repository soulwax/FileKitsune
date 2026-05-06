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
- standalone duplicate-remover flow with scan, keeper review, skip/confirm decisions, and FileKitsune quarantine execution
- standalone duplicate-remover execution writes a local JSONL audit run before any file is moved into quarantine
- current-run duplicate quarantine restore so confirmed copies can be brought back without depending on the OS Recycle Bin
- preview-first planning with reasons, warnings, confidence, and review indicators
- saved-run selection in the execute step for both full-run rollback and folder-scoped undo
- rollback preview tab for the selected saved run
- rollback preview now shows expected statuses like ready, missing destination, or original-path conflict
- rollback preview now includes an at-a-glance impact summary plus grouped diff-style path samples before undo
- rollback preview can be scoped to a selected top-level folder before running folder-only undo
- rollback confirmation dialogs now include preview-aware counts and concrete example path restores/skips
- local content extraction for text-like files, `.docx`, and `.pdf`
- large readable files are sampled from both the beginning and end instead of only taking a leading slice
- optional local OCR via Tesseract for image-led documents when `tesseract` is installed or `FILEKITSUNE_TESSERACT_PATH` is configured
- optional remote persistence via Postgres-compatible databases, including self-hosted Neon, when `NILEDB_URL`, `POSTGRES_URL`, or `DATABASE_URL` is configured
- automatic local SQLite fallback/cache for settings and journals when remote persistence is unavailable or offline mode is enabled
- visible persistence status in the execute step so users can see whether shared storage or local fallback is active
- backend support for loading historical journals by id
- persisted settings and DPAPI-protected Gemini credentials under `%LocalAppData%\FileKitsune`

## What The App Does Right Now

- scans a chosen root folder without modifying anything
- reads lightweight content from readable text-like files, `.docx`, and `.pdf`
- samples large readable documents so late-file context is not lost entirely
- attempts OCR for scanned PDFs and image files through a local Tesseract executable, then falls back to metadata-only signals when OCR is unavailable or returns no text
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
- choose `Find duplicates` to scan for exact copies, select one keeper per group, and move confirmed copies to FileKitsune quarantine

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
- review final selected-operation counts for moves, renames, duplicate routes, review-required items, and blocked/skipped items before execution is enabled
- select a saved run and preview rollback readiness before undoing
- see started or canceled execution journals that may need recovery, preview their rollback impact, roll back completed moves, or mark them abandoned
- rollback preview distinguishes pending crash checkpoints where no file moved from pending checkpoints where the move already happened and can be restored
- review the rollback impact summary to see what would restore cleanly versus be skipped
- confirm undo actions with preview-aware counts instead of a generic warning only
- roll back a full saved run or undo a top-level folder group from that run
- inspect activity and logs

## Safety Model

- destination paths are validated as Windows-safe relative paths
- final destinations must resolve inside the selected root folder
- execution revalidates selected preview items before any mutation and blocks the whole run if a selected source disappeared, changed metadata/hash where available, or can no longer resolve safely
- stale-preview blocks are surfaced in the execute step with the exact issues and a rebuild-preview action before execution can be retried
- previews report scan coverage, planned item counts, preview sampling gaps, protected items, unreadable content, duplicate hash failures, and scan/preview limit warnings
- execution asks for an extra confirmation when the current plan is incomplete because scan or preview limits were reached
- Gemini is advisory only; local logic still validates categories, fragments, and final paths
- execution currently performs moves and renames only
- the organization wizard does not delete files
- standalone duplicate removal moves confirmed copies to FileKitsune-managed quarantine after review
- duplicate removal refuses to move files if it cannot create a local audit trail first
- hidden/system files and reparse points are skipped by default unless policy settings are changed in code

## Duplicate Detection

Exact duplicate detection already exists in the current app:

- candidates are grouped by file size first
- only same-size candidates are hashed
- SHA-256 is used for exact duplicate identity
- duplicate handling can require review, route to a duplicates folder, or skip duplicate items
- standalone duplicate mode uses the same size/hash identity checks, then lets you set the keeper before any file is moved
- standalone duplicate mode writes a per-run audit file with root folder, keeper decisions, skipped groups, quarantine attempts, failures, restore attempts, and completion status
- confirmed duplicate copies are moved under `%LocalAppData%\FileKitsune\quarantine\<run-id>` and can be restored from the current run while records are still available in the UI

Current limitation:

- duplicate canonical selection is improved, but could still become more domain-aware over time

## Gemini Usage

Gemini support is optional.

- the app stores the Gemini API key encrypted with DPAPI in the current Windows user profile
- runtime settings live in `%LocalAppData%\FileKitsune\settings.json`
- the app can read environment variables or `.env` as fallback when no DPAPI value is present
- Gemini can enrich semantic understanding, but it does not decide executable paths

## OCR Usage

OCR support is optional and local-first.

- install Tesseract OCR and make `tesseract.exe` available on `PATH`, or set `FILEKITSUNE_TESSERACT_PATH` to the executable path
- FileKitsune defaults OCR languages to `deu+eng`; set `FILEKITSUNE_OCR_LANGUAGES` to another Tesseract language expression if needed
- OCR text is used as an advisory content signal for planning; local path validation and preview/execute safety checks remain authoritative
- when OCR is unavailable, times out, or returns no text, the app keeps using the existing scanned-PDF and image metadata fallback

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
- OCR depends on a local Tesseract installation and may fall back to metadata-only signals when the executable, language packs, or input format are unavailable
- duplicate canonical selection still needs strengthening

## Storage Locations

- settings: `%LocalAppData%\FileKitsune\settings.json`
- sqlite cache: `%LocalAppData%\FileKitsune\persistence.db`
- journals: `%LocalAppData%\FileKitsune\journals`
- dedup audit runs: `%LocalAppData%\FileKitsune\dedup-runs`
- dedup quarantine: `%LocalAppData%\FileKitsune\quarantine`
- logs: `%LocalAppData%\FileKitsune\logs`

## Developer Notes

- `AGENTS.md`, `AGENT.md`, `CLAUDE.md`, and `GEMINI.md` describe the current expectations for AI coding assistants
- current roadmap and remaining work are tracked in `TODO.md`
- if you change planner, execution, rollback, duplicate detection, or Gemini behavior, update the tests in `tests/FileTransformer.Tests`

## Contributing

Contributions are welcome! Please open an issue or submit a pull request with improvements, bug fixes, or new features. For major changes, please discuss them in an issue first to align on the approach.

## License

This project is licensed under the GPLv3 License. See the [LICENSE](LICENSE) file for details.
