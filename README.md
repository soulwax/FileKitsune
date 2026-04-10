# FileTransformer

FileTransformer is a Windows-only .NET 8 WPF desktop app for safe, preview-first file organization. It scans a chosen root folder, classifies files with local heuristics and optional Gemini assistance, builds an explainable plan, and only executes user-selected moves and renames after local safety checks pass.

## Current State

The app is usable today and currently provides:

- a 5-step wizard: folder, strategy, rules, preview, execute/undo
- German-first defaults for UI, folder labels, and filename language policy
- switchable German/English UI with localized wizard text, option labels, dialogs, and status updates
- strategy presets plus recommendation cards after preview
- exact duplicate detection with size pre-filtering and SHA-256 hashing
- duplicate review in the preview step and duplicate-routing options in the rules step
- preview-first planning with reasons, warnings, confidence, and review indicators
- rollback for the latest execution journal, including top-level folder rollback for that latest run
- saved-run selection in the execute step for both full-run rollback and folder-scoped undo
- rollback preview tab for the selected saved run
- rollback preview now shows expected statuses like ready, missing destination, or original-path conflict
- backend support for loading historical journals by id
- persisted settings and DPAPI-protected Gemini credentials under `%LocalAppData%\FileTransformer`

## What The App Does Right Now

- scans a chosen root folder without modifying anything
- reads lightweight content from readable text-like files and `.docx`
- classifies files using deterministic heuristics with optional Gemini enrichment
- proposes move, rename, move-and-rename, skip, and duplicate-review outcomes
- lets you filter and select executable operations before execution
- journals executed operations so the latest run can be undone later
- persists rollback journal state before execution starts and after each successful move

## Wizard Usage

### 1. Folder

- choose the root folder to analyze
- choose the UI language: `Deutsch` or `English`

### 2. Strategy

- choose an organization preset
- after a preview has been built, review advisory recommendation cards with reason and confidence

### 3. Rules

- configure include/exclude patterns and readable extensions
- control scan limits, confidence thresholds, naming behavior, language behavior, and conflict handling
- choose date source, execution mode, duplicate handling, duplicate folder name, and advanced planner behavior
- enable or disable Gemini and edit model/API settings

### 4. Preview

- build the preview plan
- inspect summary cards, warnings, duplicate groups, and selected-operation details
- filter the grid and select executable items
- no filesystem changes happen here

### 5. Execute / Undo

- execute only the selected allowed operations
- roll back the latest run, or undo a top-level folder group from that latest run
- inspect activity and logs

## Safety Model

- destination paths are validated as Windows-safe relative paths
- final destinations must resolve inside the selected root folder
- Gemini is advisory only; local logic still validates categories, fragments, and final paths
- execution currently performs moves and renames only
- there is no delete flow
- hidden/system files and reparse points are skipped by default unless policy settings are changed in code

## Duplicate Detection

Exact duplicate detection already exists in the current app:

- candidates are grouped by file size first
- only same-size candidates are hashed
- SHA-256 is used for exact duplicate identity
- duplicate handling can require review, route to a duplicates folder, or skip duplicate items

Current limitation:

- canonical duplicate selection is still simple and should become smarter before calling the feature complete

## Gemini Usage

Gemini support is optional.

- the app stores the Gemini API key encrypted with DPAPI in the current Windows user profile
- runtime settings live in `%LocalAppData%\FileTransformer\settings.json`
- the app can read environment variables or `.env` as fallback when no DPAPI value is present
- Gemini can enrich semantic understanding, but it does not decide executable paths

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

`FileTransformer.slnx` is also included if you prefer the newer solution format.

## Current Limitations

The app is still evolving. Notable gaps:

- saved-run selection exists for full historical rollback and folder-scoped undo
- rollback preview exists for saved runs, but there is still no dedicated diff-style confirmation flow
- append-safer journaling exists on the backend, but richer journal metadata and rollback-status tracking are still incomplete
- append-safer journaling exists on the backend, and rollback status is now recorded per entry
- richer journal metadata like content hash is still incomplete
- PDF extraction is not implemented yet
- OCR and image-first analysis are not implemented yet
- some Application-layer progress messages are still generated in English and should be made fully resource-driven end-to-end
- duplicate canonical selection still needs strengthening

## Storage Locations

- settings: `%LocalAppData%\FileTransformer\settings.json`
- journals: `%LocalAppData%\FileTransformer\journals`
- logs: `%LocalAppData%\FileTransformer\logs`

## Developer Notes

- `AGENTS.md`, `AGENT.md`, `CLAUDE.md`, and `GEMINI.md` describe the current expectations for AI coding assistants
- current roadmap and remaining work are tracked in `TODO.md`
- if you change planner, execution, rollback, duplicate detection, or Gemini behavior, update the tests in `tests/FileTransformer.Tests`
