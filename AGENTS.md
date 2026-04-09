# FileTransformer Agent Notes

## Project Snapshot

FileTransformer is a Windows-only .NET 8 WPF desktop app for preview-first file organization. The current implementation scans a chosen root folder, extracts lightweight file content where possible, classifies files with deterministic heuristics and optional Gemini assistance, proposes safe in-root move/rename operations, and can roll back the latest executed journal.

Validated baseline in this repository:

- `dotnet restore FileTransformer.sln`
- `dotnet build FileTransformer.sln -c Debug`
- `dotnet test FileTransformer.sln -c Debug --no-build`

All three commands pass in the current checkout.

## Solution Layout

- `src/App`: WPF shell, `MainWindowViewModel`, dialogs, folder picker, UI logging, localization.
- `src/Application`: orchestration and business logic such as planning, path safety, naming, review decisions, execution, and rollback.
- `src/Domain`: enums, models, Windows path rules, semantic catalog, strategy presets.
- `src/Infrastructure`: local filesystem access, Gemini HTTP client + parser, DPAPI-backed settings store, journal persistence, Serilog setup.
- `tests/FileTransformer.Tests`: focused unit tests for planning, execution, path safety, and Gemini payload parsing.

## Current Runtime Behavior

- App startup is in `src/App/App.xaml.cs` and uses `Host.CreateApplicationBuilder()` with dependency injection.
- User settings are stored under `%LocalAppData%\\FileTransformer`.
- Gemini API keys are persisted via DPAPI in `ProtectedAppSettingsStore`; the WPF app does not currently load `.env`.
- Execution journals are written as JSON under `%LocalAppData%\\FileTransformer\\journals`.
- Structured logs are written under `%LocalAppData%\\FileTransformer\\logs`.
- Content extraction currently supports readable text files plus `.docx`. There is no OCR or PDF parser yet.
- Gemini suggestions are advisory only. `SemanticClassifierCoordinator` merges Gemini output with heuristics, and local path validation still decides what can run.
- Execution uses move/rename only. There is no delete path in the current app flow.

## Important Files And Seams

- `src/Application/Services/OrganizationWorkflowService.cs`: scan -> read -> classify -> date resolve -> duplicate detect -> build plan.
- `src/Application/Services/DestinationPathBuilder.cs`: converts analysis into proposed relative paths, warnings, review flags, and operation types.
- `src/Application/Services/PathSafetyService.cs` and `src/Domain/Services/WindowsPathRules.cs`: core in-root and Windows-safe path enforcement.
- `src/Application/Services/PlanExecutionService.cs`: executes approved operations and writes rollback journals.
- `src/Application/Services/RollbackService.cs`: replays the latest journal in reverse order and optionally removes empty folders.
- `src/Infrastructure/Classification/GeminiSemanticClassifier.cs`: rate-limited Gemini HTTP integration with retries and strict JSON expectations.
- `src/Infrastructure/FileSystem/LocalFileScanner.cs`: include/exclude matching plus hidden/system/reparse-point handling.

## Current State Caveats

- The checked-in `.env` file is not wired into app startup. If you want environment-based Gemini config, that requires code changes.
- The checked-in `gemini.json` appears to be for external Gemini tooling, not for the WPF app runtime.
- `MainWindowViewModel` already contains strategy/date/execution/duplicate/language option lists, but the current `MainWindow.xaml` does not expose most of them and `BuildSettings()` does not persist several of those selections yet.
- Duplicate detection, protection policies, and richer organization policies exist in the core, but the current UI mainly exposes the simpler root/include/exclude/naming/Gemini flow.
- Rollback currently targets the latest journal only. There is a TODO for richer cross-session recovery.

## Working Agreements

- Preserve the preview-first model: build a plan, show reasons/warnings, then execute selected approved operations.
- Preserve the safety contract: proposed destinations must stay relative to the selected root, and execution must not silently escape it.
- Prefer changing domain rules in `src/Domain`, orchestration in `src/Application`, and I/O concerns in `src/Infrastructure`.
- When exposing a new policy in the UI, wire all three places together: `MainWindow.xaml`, `MainWindowViewModel`, and settings persistence/load paths.
- When changing planning, execution, or Gemini parsing behavior, update or add tests under `tests/FileTransformer.Tests`.

## Useful Commands

```powershell
dotnet restore FileTransformer.sln
dotnet build FileTransformer.sln -c Debug
dotnet test FileTransformer.sln -c Debug
dotnet run --project src/App/FileTransformer.App.csproj
```


### Canonical File Selection
Priority:
1. Best target location
2. Oldest path
3. Richest metadata/content

### Rollback
- Duplicate operations MUST be journaled
- Must be fully reversible

---

## 🧠 3. Gemini Contextual Analysis

### Rules
- Gemini is advisory ONLY
- Local validation is required

### Input Sources
- Extracted text from:
  - PDF (NEW)
  - DOCX
  - TXT / Markdown
- Metadata fallback if extraction fails

### Processing Pipeline
1. Extract text locally
2. Summarize / chunk
3. Send to Gemini
4. Receive:
   - topic
   - project
   - semantic grouping
5. Merge with local heuristics

### Output
- Contextual grouping across files
- Project inference
- Strategy recommendation signals

### Forbidden
- Gemini generating file paths directly
- Blind trust in AI output

---

## 📄 4. PDF & Content Extraction

### Requirements
- Add PDF text extraction
- Handle large files safely
- Support partial extraction

### Behavior
- Use sampling for large PDFs
- Fallback to metadata if needed
- OCR is optional future extension

---

## 🧭 5. Strategy Recommendations

After folder analysis, propose strategies:

### Examples

#### Projektorientiert (empfohlen)
- Strong contextual clustering

#### Nach Datum
- Strong date signals

#### Semantisch
- Clean category distribution

#### Duplikate bereinigen
- High duplicate density

### Implementation
- Score strategies based on:
  - clustering strength
  - date density
  - duplicates
  - file type distribution

---

## 🧙 6. Wizard UI (REQUIRED)

Replace single screen with 5 steps:

1. Folder selection  
2. Strategy selection  
3. Rule configuration  
4. Preview  
5. Execute / Rollback  

### Requirements
- Progressive disclosure
- Advanced options hidden initially
- Clear navigation (Next / Back)

---

## 🌍 7. Language System (German-First)

### Defaults
- UI: German
- Folder naming: German
- Filename normalization: German

### Options
- English
- Bilingual

### Implementation

#### Separate Layers
1. UI language
2. Folder naming language
3. Filename language

### Technical Tasks
- Replace ALL hardcoded XAML strings
- Use resource dictionaries:
  - `Strings.de-DE.xaml`
  - `Strings.en-US.xaml`

---

## 📊 8. Duplicate + Strategy Integration

- Duplicate detection must influence strategy recommendations
- High duplicates → suggest cleanup-first strategy

---

## 🧪 Testing Requirements

### MUST INCLUDE

#### Rollback
- Full restore
- Partial restore
- Conflict handling

#### Deduplication
- Hash accuracy
- Large file handling
- Rollback after dedup

#### Content Analysis
- PDF extraction
- Gemini fallback handling

---

# ⚠️ Anti-Patterns (DO NOT DO)

- ❌ Direct file operations without journaling
- ❌ AI-generated paths without validation
- ❌ Filename-based duplicate detection
- ❌ Hardcoded UI strings
- ❌ Mixing UI and domain logic
- ❌ Destructive actions without preview

---

# 🧾 Codex Task Summary

Implement:

1. Wizard UI
2. German-first localization
3. Strategy recommendations
4. Hash-based duplicate detection
5. PDF + content extraction
6. Gemini contextual grouping
7. Historical rollback system
8. Full test coverage

---

# 🧩 Guiding Philosophy

This is NOT a file mover.

It is a:
- safe
- explainable
- reversible
- intelligent organization system

Every feature must reinforce:
👉 trust, transparency, and control