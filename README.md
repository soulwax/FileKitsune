# FileTransformer

FileTransformer is a Windows-only .NET 8 WPF desktop application for preview-first file organization. It scans a user-selected root folder, extracts lightweight semantic signals from file names and readable content, builds an explainable move/rename plan, and only executes user-selected operations after local safety validation.

## What The App Does Today

- Builds a preview plan before any filesystem mutation.
- Classifies files with deterministic heuristics and optional Gemini assistance.
- Supports German, English, and mixed-language content signals.
- Proposes move, rename, or move-and-rename operations with reasons, warnings, confidence, and review flags.
- Enforces Windows-safe relative paths and keeps operations inside the chosen root folder.
- Writes rollback journals for executed moves and renames and can undo the latest run.
- Stores logs and user settings under `%LocalAppData%\\FileTransformer`.

## Current Architecture

- `src/App`: WPF shell, MVVM view models, dialogs, folder picker, UI log sink, localization resources.
- `src/Application`: orchestration and business logic for scanning workflows, semantic coordination, naming, review decisions, execution, and rollback.
- `src/Domain`: models, enums, semantic catalogs, strategy presets, and Windows path rules.
- `src/Infrastructure`: local filesystem access, Gemini HTTP integration, DPAPI-backed settings storage, JSON journal persistence, and Serilog configuration.
- `tests/FileTransformer.Tests`: focused unit tests around planning, execution, path safety, and Gemini response parsing.

## Workflow

1. Choose a root folder in the WPF app.
2. Configure include/exclude patterns, readable extensions, size limits, naming behavior, and Gemini usage.
3. Run a scan to build a preview plan.
4. Review proposed operations, warnings, and review-required items in the grid.
5. Execute selected allowed operations.
6. Optionally roll back the latest execution journal or a top-level folder group from that run.

## Safety Model

- Destination paths are validated twice: first as Windows-safe relative paths, then as paths that still resolve inside the selected root folder.
- Gemini is advisory only. The planner still uses local rules to validate categories, folder fragments, and final paths.
- Normal execution performs moves and renames only. There is no delete step in the current workflow.
- Existing conflicts are either skipped or resolved by appending a numeric counter, depending on naming policy.
- Hidden/system files and reparse points are skipped by default during scanning unless policy settings are changed in code.

## Content And Classification

- Readable content currently includes text-like files plus `.docx`.
- Large files above the configured content inspection limit are handled as metadata-only.
- The heuristic classifier recognizes multilingual keyword clusters for categories such as invoices, research, code, photos, contracts, teaching, and personal notes.
- Gemini calls are rate-limited, retried on transient failures, and merged with heuristic results.
- Date resolution can come from content, file names, modified time, or created time, with reliability checks in the core planner.

## Build, Test, And Run

```powershell
dotnet restore FileTransformer.sln
dotnet build FileTransformer.sln -c Debug
dotnet test FileTransformer.sln -c Debug
dotnet run --project src/App/FileTransformer.App.csproj
```

`FileTransformer.slnx` is included alongside `FileTransformer.sln` if you prefer the newer solution format.

## Gemini Configuration

- Gemini usage is optional.
- The desktop app stores the API key encrypted with DPAPI in the current Windows user profile.
- The runtime settings store is `%LocalAppData%\\FileTransformer\\settings.json`.
- The current WPF app does not load `.env` or `gemini.json` for runtime Gemini configuration.

## Current Implementation Notes

- The planner pipeline is implemented in `OrganizationWorkflowService` and currently processes the scanned set up to the configured preview sample size.
- Exact duplicate detection, protection policies, richer strategy presets, and additional review settings already exist in the core domain/application layers.
- The current WPF screen exposes the primary scan, naming, and Gemini controls, but some richer policy options present in models and view-model enums are not fully wired through the UI yet.
- Rollback currently works from the latest saved journal and supports whole-run rollback plus top-level folder rollback from that journal.
- OCR, PDF extraction, image classification, and richer rollback checkpoints are not implemented yet.

## Developer Notes

- `AGENTS.md`, `CLAUDE.md`, `GEMINI.md`, and `.github/copilot-instructions.md` describe the current codebase for AI coding assistants.
- If you change planner behavior, path safety, execution, or Gemini parsing, update the related unit tests in `tests/FileTransformer.Tests`.
