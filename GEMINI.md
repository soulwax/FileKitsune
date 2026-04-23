# Gemini Agent Notes For FileKitsune

This repository already contains a `gemini.json`, but the desktop application's Gemini integration is separate from that file.

## Current Gemini Integration

- Runtime Gemini usage is implemented in `src/Infrastructure/Classification/GeminiSemanticClassifier.cs`.
- The app combines Gemini output with heuristics in `src/Application/Services/SemanticClassifierCoordinator.cs`.
- Gemini is advisory only. Local review and path safety logic still control what can execute.
- API keys are stored with DPAPI in `%LocalAppData%\\FileKitsune\\settings.json`.
- The WPF app can now read `.env` or environment variables as a fallback for Gemini settings when no DPAPI value is present.

## Repo Facts That Matter

- Windows-only WPF UI on `net8.0-windows`.
- Planner pipeline: scan -> read -> classify -> date resolve -> duplicate detect -> build plan -> execute selected items.
- Content extraction currently supports text files and `.docx` only.
- Strategy recommendations are already present and should remain advisory.
- Rollback is journal-based and currently limited to the latest saved execution journal.

## When Editing

- Keep Gemini optional and resilient. The heuristic path must still work when Gemini fails.
- Do not weaken the local safety checks around root confinement or path normalization.
- If you expose more Gemini-related settings in the UI, wire them through `MainWindow.xaml`, `MainWindowViewModel`, and `ProtectedAppSettingsStore`.
- If you document setup, say clearly that the Settings UI and DPAPI-backed store take priority, with `.env` only as a fallback.
- Do not let Gemini drive duplicate identity, rollback decisions, or final executable paths.

## `.env` Fallback Keys

The desktop app checks environment variables first and falls back to a local `.env` file if no DPAPI value is present.

- `GEMINI_API_KEY` (or `GOOGLE_API_KEY`)
- `GEMINI_MODEL`
- `GEMINI_ENDPOINT_BASE_URL`
- `GEMINI_MAX_REQUESTS_PER_MINUTE`
- `GEMINI_REQUEST_TIMEOUT_SECONDS`
- `GEMINI_MAX_PROMPT_CHARACTERS`
- `GEMINI_ENABLED` (`true/false`, `1/0`)

## Validation

```powershell
dotnet restore FileTransformer.sln
dotnet build FileTransformer.sln -c Debug
dotnet test FileTransformer.sln -c Debug
```
