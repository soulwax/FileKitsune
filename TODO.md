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

## 10. Current Best Next Slice

Highest-value next work:

- [x] improve rollback preview/confirmation from impact summary into a clearer diff-style confirmation experience
- [ ] add OCR/image-first handling for scanned PDFs and image-led folders
- [ ] consider any last domain-specific duplicate canonical heuristics after manual testing

Why this is next:

- the wizard, rules, localization, and recommendations are already useful
- duplicate detection already exists in a usable form
- OCR, localization cleanup, and duplicate trust details are now the largest gaps still visible to users

## 11. Dedup Mode (Standalone Duplicate Remover)

Spec: `docs/superpowers/specs/2026-04-29-dedup-mode-design.md`

### WizardStep Enum & Navigation
- [ ] Add `ModeSelector = -1`, `DedupScan = 5`, `DedupReview = 6`, `DedupExecute = 7` to `WizardStep`
- [ ] Change initial step in `MainWindowViewModel` from `Folder` to `ModeSelector`
- [ ] Extend Back/Next visibility logic to handle dedup sub-flow
- [ ] Add `DataTrigger` blocks in `MainWindow.xaml` for all new steps (title, body, template)

### ModeSelector Step
- [ ] Create `WizardModeSelectorView.xaml` + code-behind
- [ ] Add two mode cards: "Organize Files" → `Folder`, "Find Duplicates" → `DedupScan`
- [ ] Hide Back and Next buttons at `ModeSelector`

### DedupScan Step
- [ ] Create `WizardDedupScanView.xaml` + code-behind
- [ ] Add `DedupRootFolder` property and folder-browse command to `MainWindowViewModel`
- [ ] Add `DedupScanCommand` — walks directory, calls `DuplicateDetectionService.DetectAsync`
- [ ] Wire progress reporting through existing `WorkflowProgress` / status message
- [ ] Populate `ObservableCollection<DedupGroupViewModel>` from scan results
- [ ] Enable Next only after scan completes

### DedupGroupViewModel & DedupFileItemViewModel
- [ ] Create `DedupGroupViewModel` (canonical path, copies, resolved/skipped state, wasted bytes, display label)
- [ ] Create `DedupFileItemViewModel` (full path, relative path, size, modified date, `IsKeeper`)
- [ ] Implement "Set as keeper" toggle — flips `IsKeeper` on selected file and clears others in group

### DedupReview Step
- [ ] Create `WizardDedupReviewView.xaml` + code-behind
- [ ] Left panel: `ListBox` of groups with keeper filename + copy count + MB wasted
- [ ] Right panel: per-file cards with Keep/Remove pill badges and "Set as keeper" button
- [ ] "Confirm" button — marks group as resolved (decision only, no files touched)
- [ ] "Skip this group" button — marks group skipped, advances selection
- [ ] "Resolve all automatically" bottom-bar button
- [ ] Enable shell Next only when all groups are resolved or skipped
- [ ] Badge tally: N groups · N resolved · N skipped

### IRecycleBinService
- [ ] Define `IRecycleBinService` interface in Application layer
- [ ] Implement `RecycleBinService` in Infrastructure using `Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile` with `RecycleOption.SendToRecycleBin`
- [ ] Register in `ServiceCollectionExtensions.cs`

### DedupExecute Step
- [ ] Create `WizardDedupExecuteView.xaml` + code-behind
- [ ] Add `DedupExecuteCommand` — flushes any remaining confirmed groups, reports progress
- [ ] Results summary: files recycled, groups skipped, MB freed, per-file errors
- [ ] "Scan another folder" button → `ModeSelector`
- [ ] "Open Recycle Bin" button → `Process.Start("shell:RecycleBinFolder")`

### Localization
- [ ] Add all `WizardStepModeSelectorTitle`, `ModeOrganize*`, `ModeDedup*`, `DedupScan*`, `DedupReview*`, `DedupExecute*` keys to `Strings.de-DE.xaml`
- [ ] Mirror all keys in `Strings.en-US.xaml`

### Tests
- [ ] Unit test `DedupGroupViewModel` keeper-toggle logic
- [ ] Unit test `RecycleBinService` (mock or integration, Windows-only)
- [ ] Verify existing organization flow tests still pass (no regressions)

## Non-Negotiables

- [x] No execution without preview
- [x] Paths stay inside the selected root
- [x] Gemini remains advisory only
- [x] Duplicate identity is hash-based, not filename-based
- [x] Core rollback scenarios and preview states covered by dedicated tests

## Guiding Principle

Users should always be able to understand:

- what will happen
- why it is proposed
- what can be undone

Safety, transparency, and reversibility still outrank automation speed.
