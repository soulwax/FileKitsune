# FileTransformer

FileTransformer is a production-minded Windows 10/11 desktop application that scans a user-selected root directory, understands files by meaning instead of extension alone, and generates a safe, explainable reorganization plan before any filesystem changes happen.

## Highlights

- `WPF + MVVM` desktop UI built on .NET 8 and CommunityToolkit.Mvvm
- `Preview-first` workflow with plan summary, confidence indicators, risk flags, and selective execution
- `Deterministic core` planning rules that validate every destination locally, even when Gemini contributes semantic hints
- `Multilingual support` for German, English, and mixed German-English content, including umlaut-safe handling and configurable category language output
- `Fallback heuristics` so planning still works when Gemini is disabled or unavailable
- `Structured logging` plus an operation journal for rollback of app-performed moves and renames where feasible

## Solution layout

- `src/App` WPF shell, view models, commands, resource dictionaries, and desktop services
- `src/Application` orchestration, planning, execution, and abstractions
- `src/Domain` domain models, enums, and safety-focused path/naming rules
- `src/Infrastructure` filesystem, Gemini client, settings persistence, and logging integration
- `tests/FileTransformer.Tests` unit tests for the core logic

## Requirements

- Windows 10 or Windows 11
- .NET SDK 8 or newer with the .NET 8 desktop runtime installed

## First run

1. Build the solution.
2. Start the WPF app.
3. Pick a root folder.
4. Configure scan and naming settings.
5. Add a Gemini API key in the Settings area if you want LLM-assisted classification.
6. Run a scan and review the preview plan.
7. Execute all or selected operations after reviewing the reasons and risk flags.

The app stores settings and encrypted Gemini credentials under `%LocalAppData%\\FileTransformer`.

## Build and test

```powershell
dotnet restore
dotnet build
dotnet test
```

## Gemini notes

- Gemini usage is optional.
- The API key is stored encrypted in the current Windows user profile via DPAPI.
- Gemini suggestions are advisory only. The planner validates and normalizes all resulting paths locally before any change is allowed.

## Future enhancements

- Embeddings and vector-assisted topic grouping
- OCR for scanned documents and image-first workflows
- Image classification and richer media understanding
- Duplicate detection and archival policies
- Smarter rollback for partial failures and cross-session recovery
