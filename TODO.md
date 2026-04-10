# TODO.md

## FileTransformer Implementation Status

This roadmap tracks where the app stands now and what remains before the trust model is fully delivered.

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
- [x] preview-first execution model
- [x] settings persistence including UI language and Gemini settings

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
- [ ] Make Application-layer progress/status text fully resource-driven end-to-end

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
- [ ] Improve canonical file selection beyond alphabetical path order
- [ ] Add dedicated duplicate tests for large files and rollback behavior
- [ ] Strengthen duplicate journaling expectations in rollback scenarios

## 5. Content Extraction

- [x] TXT/text-like extraction
- [x] DOCX extraction
- [ ] PDF extraction
- [ ] Sampling for large PDFs/documents
- [ ] Fallback tests for unreadable documents

## 6. Gemini Integration

- [x] Keep Gemini optional
- [x] Keep heuristic fallback working
- [x] Support DPAPI-backed stored credentials
- [x] Support `.env` / environment fallback when no stored key exists
- [x] Use Gemini only as advisory enrichment
- [ ] Enrich project clustering and cross-file grouping further
- [ ] Add more Gemini fallback tests around unavailable/partial responses

## 7. Contextual Grouping

- [ ] Group related files across types into stronger project clusters
- [ ] Feed those clusters into recommendations and destination planning

## 8. Rollback Upgrade

- [ ] Add journal versioning
- [ ] Persist richer journal entry metadata: hash, size, timestamps, rollback status
- [ ] Save execution journal header before mutation starts
- [ ] Append successful operations during execution
- [ ] Mark runs complete after execution
- [ ] Support historical run selection
- [ ] Add rollback preview
- [ ] Handle missing files, conflicts, and repeated rollback cleanly
- [ ] Make rollback idempotent by design and by tests

## 9. Test Coverage

- [ ] Add `RollbackServiceTests`
- [ ] Cover full rollback
- [ ] Cover partial rollback
- [ ] Cover rollback conflict handling
- [ ] Cover repeated rollback/idempotency
- [ ] Add duplicate hashing tests
- [ ] Add duplicate rollback tests
- [ ] Add PDF extraction tests
- [ ] Add Gemini fallback/unavailable tests

## 10. Current Best Next Slice

Highest-value next work:

- [ ] harden rollback journaling and history selection

Why this is next:

- the wizard, rules, localization, and recommendations are already useful
- duplicate detection already exists in a usable form
- rollback is now the largest trust gap still visible to users

## Non-Negotiables

- [x] No execution without preview
- [x] Paths stay inside the selected root
- [x] Gemini remains advisory only
- [x] Duplicate identity is hash-based, not filename-based
- [ ] All rollback guarantees covered by dedicated tests

## Guiding Principle

Users should always be able to understand:

- what will happen
- why it is proposed
- what can be undone

Safety, transparency, and reversibility still outrank automation speed.
