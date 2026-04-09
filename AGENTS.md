# FileTransformer Agent Guide

## Project Mission

FileTransformer is a Windows-only .NET 8 WPF application for safe, explainable, preview-first file organization. Every change should strengthen these product promises:

- preview before execution
- operations stay inside the selected root
- rollback remains reliable and explicit
- AI assists classification and recommendations, but never overrides local safety checks
- German is the primary folder-naming language by default

## Current Architecture

- `src/App`: WPF shell, MVVM view models, dialogs, localization resources, UI-only concerns
- `src/Application`: orchestration, planning, execution, rollback, naming, recommendations
- `src/Domain`: rules, enums, catalogs, presets, path safety primitives
- `src/Infrastructure`: file system access, hashing, settings persistence, journals, Gemini integration, logging
- `tests/FileTransformer.Tests`: unit tests for safety-critical behavior

## Current Baseline

Validated in this repository:

- `dotnet restore FileTransformer.sln`
- `dotnet build FileTransformer.sln -c Debug`
- `dotnet test FileTransformer.sln -c Debug`

Today the app already:

- scans a root folder and builds a preview plan
- classifies files with heuristics and optional Gemini assistance
- proposes move, rename, or move-and-rename operations with reasons and warnings
- journals executed operations and can roll back the latest run
- stores settings and journals under `%LocalAppData%\\FileTransformer`

## Product Direction To Preserve

### 1. Wizard-like UX

The UI should move toward a 5-step flow:

1. Folder
2. Strategy
3. Rules
4. Preview
5. Execute / Undo

Progressive disclosure is preferred. Show presets first, then advanced knobs only when needed.

### 2. German-first organization

- default folder naming language: German
- filename language remains configurable
- UI language and output language are separate concerns

### 3. AI-assisted semantics

Gemini should have an important role in:

- context detection
- topic and project inference
- grouping suggestions
- strategy recommendation signals

But Gemini must remain advisory only. Final paths, execution eligibility, and root-bound safety are always decided locally.

### 4. Strong rollback

Target direction:

- historical run selection, not latest-only rollback
- append-safe journaling for partial failures
- idempotent rollback behavior
- clear conflict and skip reporting

### 5. Several organization strategies

The codebase already contains strategy presets and related policy models. Agents should prefer exposing and wiring the existing capabilities before inventing new parallel systems.

## Important Seams

- `src/App/Views/MainWindow.xaml`
- `src/App/ViewModels/MainWindowViewModel.cs`
- `src/Application/Services/OrganizationWorkflowService.cs`
- `src/Application/Services/PlanExecutionService.cs`
- `src/Application/Services/RollbackService.cs`
- `src/Application/Services/DestinationPathBuilder.cs`
- `src/Application/Services/SemanticClassifierCoordinator.cs`
- `src/Infrastructure/Classification/GeminiSemanticClassifier.cs`
- `src/Infrastructure/Configuration/ProtectedAppSettingsStore.cs`

## Implementation Rules

- Keep UI logic in the App layer. Do not move planning logic into code-behind.
- Keep orchestration in Application, stable rules in Domain, and I/O or vendor integrations in Infrastructure.
- Preserve in-root safety checks through `PathSafetyService` and `WindowsPathRules`.
- Journal filesystem mutations before relying on them for rollback.
- Prefer extending existing settings and models instead of introducing duplicate configuration paths.
- If the app should use `.env` for Gemini, wire it intentionally and document how it interacts with DPAPI-backed settings.

## Gemini Rules

- Use Gemini for semantic context, not for authority over final paths.
- Keep heuristic fallback working when Gemini is disabled, unavailable, rate-limited, or returns unusable output.
- If Gemini is used to analyze file trees or propose strategies, its outputs must still be converted into explainable, local, deterministic decisions.

## Localization Rules

- Replace hardcoded UI strings with resource keys.
- Keep both `Strings.de-DE.xaml` and `Strings.en-US.xaml` complete.
- Treat UI language, folder label language, and filename language as separate settings.

## Testing Priorities

If you change these areas, tests are expected:

- rollback and journal behavior
- duplicate detection and hashing behavior
- path safety and root confinement
- Gemini fallback and parsing behavior
- recommendation or preset-selection logic

## Anti-Patterns

- direct file mutations without journaling
- AI-generated paths accepted without validation
- trusting filenames instead of hashes for duplicate identity
- mixing UI concerns into domain logic
- destructive actions without preview or rollback data

## Working Style

- Prefer small, composable extensions over sweeping rewrites.
- Wire existing view-model options before adding net-new parallel controls.
- Keep documentation current when behavior changes, especially `README.md`, `AGENT.md`, and this file.
