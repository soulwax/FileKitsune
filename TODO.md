# TODO.md

## FileKitsune Rebrand And Implementation Status

This roadmap tracks the current FileKitsune rebrand and where the app stands before the trust model is fully delivered.

## Rebrand Baseline

Current project branding direction:

- [x] Product name is now `FileKitsune`
- [x] Primary logo asset currently lives at `Assets/filekitsune.png`
- [x] Replace remaining `FileTransformer` user-facing naming across app UI, docs, packaging, and metadata
- [ ] Rename technical repo identifiers such as solution, project, assembly, and namespace names where worthwhile
- [ ] Wire additional assets from `Assets/` as they are added

## Visual Polish And File-Type Symbols

- [x] Add WPF-ready file icon dependency: `MahApps.Metro.IconPacks.FileIcons`
- [x] Add WPF-ready general action/status icon dependency: `MahApps.Metro.IconPacks.Material`
- [x] Create first FileKitsune icon mapping service from file extension to icon kind and color
- [x] Add file-type symbols to preview grid rows, selected-operation details, and duplicate review cards
- [x] Add file-type symbols to rollback preview rows
- [ ] Extend icon mapping from file extension only to semantic category, operation type, and localized tooltip
- [ ] Build a small FileKitsune-native overlay set for app-specific states: duplicate, keeper, review needed, protected, Gemini-advised, moved, renamed, rollback-ready, rollback-blocked
- [ ] Decide whether to supplement dependency icons with custom SVG/XAML document silhouettes for a more macOS-like but non-copycat FileKitsune house style

## Modern Visual Direction

Make FileKitsune feel beautiful, calm, and modern without weakening its safety-first character.

- [ ] Define a compact visual language for trust states: ready, warning, blocked, protected, rollback-ready, rollback-blocked, duplicate, keeper, and review-needed.
- [ ] Create a more deliberate typography scale for wizard titles, section headings, dense lists, and audit details.
- [ ] Polish the main wizard rhythm: consistent spacing, quieter section bands, stronger focus states, and fewer visually competing panels.
- [ ] Introduce a FileKitsune-native document silhouette style for common file families so icons feel like part of the app, not only a dependency pack.
- [ ] Add subtle status color semantics that remain readable in both German and English, with no meaning conveyed by color alone.
- [ ] Modernize the execute and rollback screens first, because those are the trust-critical moments where beauty should mean clarity.
- [ ] Add visual regression screenshots or a lightweight UI smoke checklist before major visual refactors.

## Current Baseline

Completed and usable today:

- [x] 5-step wizard flow
- [x] German-first defaults
- [x] switchable German/English UI
- [x] strategy presets exposed in UI
- [x] strategy recommendations after preview
- [x] exact duplicate detection using size pre-filtering + SHA-256
- [x] duplicate review surfaced in preview
- [x] latest-run rollback
- [x] rollback backend can target a specific journal id
- [x] saved-run selection for full-run rollback and folder-scoped undo in the execute step
- [x] Add rollback preview for the selected saved run
- [x] Show expected rollback readiness/conflict states in the rollback preview
- [x] preview-first execution model
- [x] settings persistence including UI language and Gemini settings
- [x] optional remote Postgres/Nile persistence with local SQLite fallback for offline/unavailable mode
- [x] shared persistence/offline status exposed in the UI

Validated baseline:

- [x] `dotnet build FileTransformer.sln -c Debug`
- [x] `dotnet test FileTransformer.sln -c Debug`

## 1. Wizard UX

- [x] Add `CurrentStep` enum to the view model
- [x] Add `NextCommand` and `BackCommand`
- [x] Split UI into 5 wizard steps
- [x] Keep preview DataGrid in the preview step
- [x] Keep MVVM boundary intact
- [x] Add progress indication for step navigation

## 2. Localization

- [x] Replace hardcoded wizard XAML strings with resources
- [x] Add `Strings.de-DE.xaml` and `Strings.en-US.xaml` coverage for wizard flow
- [x] Add UI language selector
- [x] Default UI language to German
- [x] Localize view-model supplied option labels
- [x] Localize view-model dialogs and status messages
- [x] Make Application-layer progress/status text fully resource-driven end-to-end

## 3. Strategy Presets And Recommendations

- [x] Bind existing `StrategyPresets` to the UI
- [x] Add strategy selection UI
- [x] Implement recommendation scoring service
- [x] Score recommendations from category, date, duplicate, review, and project/topic signals
- [x] Return advisory recommendations with name, reason, and confidence
- [x] Allow one-click preset selection from recommendation cards
- [x] Add tests for recommendation behavior

## 4. Duplicate Detection And Duplicate UX

- [x] Use SHA-256 for exact duplicate identity
- [x] Group by file size before hashing
- [x] Surface duplicates in preview
- [x] Expose duplicate handling mode in the Rules step
- [x] Support routing duplicates to a dedicated folder in planning
- [x] Improve canonical file selection beyond alphabetical path order
- [x] Add dedicated duplicate tests for large files and rollback behavior
- [x] Strengthen duplicate journaling expectations in rollback scenarios

## 5. Content Extraction

- [x] TXT/text-like extraction
- [x] DOCX extraction
- [x] PDF extraction
- [x] Sampling for large PDFs/documents
- [x] Fallback tests for unreadable documents

## 6. Gemini Integration

- [x] Keep Gemini optional
- [x] Keep heuristic fallback working
- [x] Support DPAPI-backed stored credentials
- [x] Support `.env` / environment fallback when no stored key exists
- [x] Keep Gemini secrets local even when shared persistence is enabled
- [x] Use Gemini only as advisory enrichment
- [x] Enrich project clustering and cross-file grouping further
- [x] Let Gemini influence strategy/depth tradeoffs through explicit user-applied guidance
- [x] Surface Gemini structure guidance in both Strategy and Rules steps
- [x] Add more Gemini fallback tests around unavailable/partial responses

## 7. Contextual Grouping

- [x] Group related files across types into stronger project clusters
- [x] Feed those clusters into destination planning

## 8. Rollback Upgrade

- [x] Add journal versioning
- [x] Persist content hash on journal entries
- [x] Persist richer journal entry metadata: broader provenance/details beyond hash, size, and timestamps already present
- [x] Persist rollback status and last rollback attempt message on journal entries
- [x] Save execution journal header before mutation starts
- [x] Append successful operations during execution
- [x] Mark runs complete after execution
- [x] Support historical run selection in backend services/store
- [x] Support historical run selection for folder-scoped undo in the UI
- [x] Support historical run selection for full-run rollback in the UI
- [x] Cache journals in SQLite and optionally sync them to Postgres/Nile
- [x] Add rollback preview
- [x] Add rollback impact summary to the rollback preview
- [x] Make rollback confirmation dialogs preview-aware
- [x] Handle missing files, conflicts, and repeated rollback cleanly
- [x] Make rollback idempotent by design and by tests

## 9. Test Coverage

- [x] Add `RollbackServiceTests`
- [x] Cover full rollback
- [x] Cover partial rollback
- [x] Cover rollback conflict handling
- [x] Cover repeated rollback/idempotency
- [x] Add duplicate hashing tests
- [x] Add duplicate rollback tests
- [x] Add PDF extraction tests
- [x] Add Gemini fallback/unavailable tests

## 10. Fundamental Missing Pieces

These are not cosmetic. They close trust gaps where a user could misunderstand scope, execute a stale plan, or lose confidence after a failure.

- [x] Add execution preflight revalidation before any mutation: source still exists, source metadata/hash still matches the preview where available, destination is still conflict-free or intentionally conflict-resolved, and every final path still resolves inside the selected root.
- [x] Surface stale-preview results in the UI and require the user to rebuild the preview before execution.
- [x] Add clear scan/preview coverage reporting: total scanned, previewed/planned count, skipped count, protected count, scan limit hit, preview sample limit hit, and unreadable content count.
- [x] Add duplicate hash failure counts to scan/preview coverage reporting once duplicate detection exposes failure statistics.
- [x] Block or strongly warn on execution when the current plan is incomplete because `MaxFilesToScan` or `PreviewSampleSize` truncated the folder.
- [ ] Add full OCR text extraction for scanned PDFs and image files, preferably local/offline-first, with extraction source and confidence visible in the preview.
- [x] Add an audit trail for standalone dedup runs: root folder, duplicate groups, selected keepers, quarantined files, skipped groups, failures, and timestamp.
- [x] Replace Recycle Bin-only dedup execution with a FileKitsune-managed quarantine/restore flow so duplicate removal remains recoverable even if the OS Recycle Bin is unavailable, disabled, or later emptied.
- [x] Add recovery UI for incomplete/canceled/crashed execution journals so users can see pending operations, roll back completed moves, or mark abandoned.
- [ ] Add checkpoint-level rollback hardening for partial failures across process restarts, including tests for pending journal entries and interrupted rollback attempts.
- [x] Add a final execution review screen that summarizes exactly how many selected operations will move, rename, route duplicates, skip, or require review before the execute button is enabled.

## 11. Current Best Next Slice

Highest-value next work:

- [x] improve rollback preview/confirmation from impact summary into a clearer diff-style confirmation experience
- [x] add image-first metadata handling and scanned-PDF detection for image-led folders
- [ ] add full OCR text extraction for scanned PDFs and images
- [x] add execution preflight revalidation and stale-preview handling
- [x] make partial scan/preview coverage impossible to miss before execution
- [x] add dedup run audit history beyond relying on the Windows Recycle Bin
- [x] add FileKitsune-managed dedup quarantine with explicit restore
- [ ] consider any last domain-specific duplicate canonical heuristics after manual testing

Why this is next:

- the wizard, rules, localization, and recommendations are already useful
- duplicate detection already exists in a usable form
- stale-preview handling, partial-coverage clarity, OCR, and dedup audit history are now the largest gaps still visible to users

## 12. Dedup Mode (Standalone Duplicate Remover)

Spec: `docs/superpowers/specs/2026-04-29-dedup-mode-design.md`

### WizardStep Enum & Navigation
- [x] Add `ModeSelector = -1`, `DedupScan = 5`, `DedupReview = 6`, `DedupExecute = 7` to `WizardStep`
- [x] Change initial step in `MainWindowViewModel` from `Folder` to `ModeSelector`
- [x] Extend Back/Next visibility logic to handle dedup sub-flow
- [x] Add `DataTrigger` blocks in `MainWindow.xaml` for all new steps (title, body, template)

### ModeSelector Step
- [x] Create `WizardModeSelectorView.xaml` + code-behind
- [x] Add two mode cards: "Organize Files" → `Folder`, "Find Duplicates" → `DedupScan`
- [x] Hide Back and Next buttons at `ModeSelector`

### DedupScan Step
- [x] Create `WizardDedupScanView.xaml` + code-behind
- [x] Add `DedupRootFolder` property and folder-browse command to `MainWindowViewModel`
- [x] Add `DedupScanCommand` — walks directory, calls `DuplicateDetectionService.DetectAsync`
- [x] Wire progress reporting through existing `WorkflowProgress` / status message
- [x] Populate `ObservableCollection<DedupGroupViewModel>` from scan results
- [x] Enable Next only after scan completes

### DedupGroupViewModel & DedupFileItemViewModel
- [x] Create `DedupGroupViewModel` (canonical path, copies, resolved/skipped state, wasted bytes, display label)
- [x] Create `DedupFileItemViewModel` (full path, relative path, size, modified date, `IsKeeper`)
- [x] Implement "Set as keeper" toggle — flips `IsKeeper` on selected file and clears others in group

### DedupReview Step
- [x] Create `WizardDedupReviewView.xaml` + code-behind
- [x] Left panel: `ListBox` of groups with keeper filename + copy count + MB wasted
- [x] Right panel: per-file cards with Keep/Remove pill badges and "Set as keeper" button
- [x] "Confirm" button — marks group as resolved (decision only, no files touched)
- [x] "Skip this group" button — marks group skipped, advances selection
- [x] "Resolve all automatically" bottom-bar button
- [x] Enable shell Next only when all groups are resolved or skipped
- [x] Badge tally: N groups · N resolved · N skipped

### IRecycleBinService
- [x] Define `IRecycleBinService` interface in Application layer
- [x] Implement `RecycleBinService` in Infrastructure using `Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile` with `RecycleOption.SendToRecycleBin`
- [x] Register in `ServiceCollectionExtensions.cs`

### DedupExecute Step
- [x] Create `WizardDedupExecuteView.xaml` + code-behind
- [x] Add `DedupExecuteCommand` — flushes any remaining confirmed groups, reports progress
- [x] Results summary: files quarantined, groups skipped, recoverable MB, per-file errors
- [x] "Scan another folder" button → `ModeSelector`
- [x] Legacy "Open Recycle Bin" button → `Process.Start("shell:RecycleBinFolder")`
- [x] Replace Recycle Bin execution controls with "Open quarantine folder" and current-run restore controls
- [x] Write a local JSONL audit run before moving any duplicate to FileKitsune quarantine
- [x] Show the dedup audit file path after execution
- [x] Show the FileKitsune quarantine folder path after execution

### Localization
- [x] Add all `WizardStepModeSelectorTitle`, `ModeOrganize*`, `ModeDedup*`, `DedupScan*`, `DedupReview*`, `DedupExecute*` keys to `Strings.de-DE.xaml`
- [x] Mirror all keys in `Strings.en-US.xaml`

### Tests
- [x] Unit test `DedupGroupViewModel` keeper-toggle logic
- [x] Unit test `RecycleBinService` (mock or integration, Windows-only)
- [x] Unit test dedup audit JSONL persistence
- [x] Verify existing organization flow tests still pass (no regressions)

## Non-Negotiables

- [x] No execution without preview
- [x] Paths stay inside the selected root
- [x] Gemini remains advisory only
- [x] Duplicate identity is hash-based, not filename-based
- [x] Core rollback scenarios and preview states covered by dedicated tests
- [x] No file-removal flow runs without a local audit trail first
- [x] No file-removal flow should rely solely on the OS Recycle Bin for recoverability

## Guiding Principle

Users should always be able to understand:

- what will happen
- why it is proposed
- what can be undone

Safety, transparency, and reversibility still outrank automation speed.
