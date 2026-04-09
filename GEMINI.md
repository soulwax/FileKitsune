# Gemini Agent Notes For FileTransformer

This repository already contains a `gemini.json`, but the desktop application's Gemini integration is separate from that file.

## Current Gemini Integration

- Runtime Gemini usage is implemented in `src/Infrastructure/Classification/GeminiSemanticClassifier.cs`.
- The app combines Gemini output with heuristics in `src/Application/Services/SemanticClassifierCoordinator.cs`.
- Gemini is advisory only. Local review and path safety logic still control what can execute.
- API keys are stored with DPAPI in `%LocalAppData%\\FileTransformer\\settings.json`.
- The WPF app does not currently load `.env`, environment variables, or `gemini.json` for runtime configuration.

## Repo Facts That Matter

- Windows-only WPF UI on `net8.0-windows`.
- Planner pipeline: scan -> read -> classify -> date resolve -> duplicate detect -> build plan -> execute selected items.
- Content extraction currently supports text files and `.docx` only.
- Rollback is journal-based and currently limited to the latest saved execution journal.

## When Editing

- Keep Gemini optional and resilient. The heuristic path must still work when Gemini fails.
- Do not weaken the local safety checks around root confinement or path normalization.
- If you expose more Gemini-related settings in the UI, wire them through `MainWindow.xaml`, `MainWindowViewModel`, and `ProtectedAppSettingsStore`.
- If you document setup, say clearly that the current app uses the Settings UI plus the DPAPI-backed store rather than `.env`.

## Validation

```powershell
dotnet restore FileTransformer.sln
dotnet build FileTransformer.sln -c Debug
dotnet test FileTransformer.sln -c Debug
```
