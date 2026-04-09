# FileTransformer

FileTransformer is a Windows-only .NET 8 WPF desktop application for safe, preview-first file organization. It analyzes a user-selected root folder, classifies files with local heuristics and optional Gemini assistance, builds an explainable move/rename plan, and only runs user-selected operations after local safety validation.

## Current State

The app is usable today and currently provides:

- a 5-step wizard flow: folder, strategy, rules, preview, execute/undo
- German-first defaults for both UI and naming behavior
- switchable UI language between German and English
- preview-first planning with reasons, warnings, confidence, and review indicators
- in-root-only path validation before anything can execute
- rollback for the latest execution journal, including top-level folder rollback from that latest run
- persisted user settings and encrypted Gemini credentials under `%LocalAppData%\\FileTransformer`

## What The App Does Right Now

- scans a chosen root folder without modifying anything
- extracts lightweight content from readable text-like files and `.docx`
- classifies files using deterministic heuristics and optional Gemini enrichment
- proposes move, rename, or move-and-rename operations
- lets you review the plan before selecting operations to execute
- journals executed operations so they can be rolled back later

## Wizard Usage

### 1. Folder

- select the root folder to analyze
- choose the UI language: `Deutsch` or `English`

### 2. Strategy

- choose the primary organization strategy preset
- this step currently exposes the preset selector; recommendation cards are planned next

### 3. Rules

- configure include and exclude patterns
- define readable extensions and scan limits
- set naming behavior, folder label language, filename language, and conflict handling
- enable or disable Gemini and provide a model/API key if desired

### 4. Preview

- build the preview plan
- review the summary cards, filter the plan, inspect warnings, and select executable items
- no filesystem changes happen in this step

### 5. Execute / Undo

- execute only the selected allowed operations
- roll back the latest run, or undo a top-level folder group from the latest run
- view activity details and logs

## Safety Model

- destination paths are validated as Windows-safe relative paths
- final destinations must still resolve inside the selected root folder
- Gemini is advisory only; local logic still validates categories, fragments, and final paths
- execution currently performs moves and renames only
- there is no delete flow in the current app
- hidden/system files and reparse points are skipped by default during scanning unless policy settings are changed in code

## German-First Behavior

The current defaults are intentionally German-first:

- default UI language: `de-DE`
- default folder naming language: German
- default filename language policy: German

These are user-adjustable in the wizard.

## Gemini Usage

Gemini support is optional.

- the app stores the Gemini API key encrypted with DPAPI in the current Windows user profile
- runtime settings live in `%LocalAppData%\\FileTransformer\\settings.json`
- the current desktop app does not yet load `.env` or `gemini.json` for runtime configuration
- Gemini can enrich semantic understanding, but it does not get to decide executable paths

## Current Architecture

- `src/App`: WPF shell, MVVM view models, step views, localization, dialogs, UI services
- `src/Application`: orchestration, planning, execution, rollback, naming, classifier coordination
- `src/Domain`: models, enums, strategy presets, semantic catalog, Windows path rules
- `src/Infrastructure`: local filesystem access, Gemini HTTP integration, settings store, journal store, logging
- `tests/FileTransformer.Tests`: unit tests for planning, execution, path safety, and Gemini parsing

## Build, Test, And Run

```powershell
dotnet restore FileTransformer.sln
dotnet build FileTransformer.sln -c Debug
dotnet test FileTransformer.sln -c Debug
dotnet run --project src/App/FileTransformer.App.csproj
```

`FileTransformer.slnx` is also included if you prefer the newer solution format.

## Current Limitations

The app is still evolving. Notable current gaps:

- strategy recommendations are not implemented yet
- duplicate handling exists in core logic but is not yet surfaced as a complete UX flow
- rollback still targets the latest journal only
- historical rollback selection and rollback preview are not implemented yet
- PDF extraction is not implemented yet
- OCR and image-first analysis are not implemented yet
- some richer planner options exist in the view model and domain, but not all of them are exposed in the current wizard

## Storage Locations

- settings: `%LocalAppData%\\FileTransformer\\settings.json`
- journals: `%LocalAppData%\\FileTransformer\\journals`
- logs: `%LocalAppData%\\FileTransformer\\logs`

## Developer Notes

- `AGENTS.md`, `AGENT.md`, `CLAUDE.md`, and `GEMINI.md` describe the current repo expectations for AI coding assistants
- current roadmap and remaining work are tracked in `TODO.md`
- if you change planner, execution, rollback, or Gemini behavior, update the tests in `tests/FileTransformer.Tests`
