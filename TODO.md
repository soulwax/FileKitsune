# TODO.md
## File-Transformer — Implementation Roadmap

This document tracks all required work to transform the application into a:

✔ Safe  
✔ Reversible  
✔ German-first  
✔ AI-assisted file organization system  

---

# 🔴 PRIORITY LEGEND

- 🔴 Critical (must be correct before release)
- 🟡 Important (major feature / UX impact)
- 🟢 Enhancement (nice to have / polish)

---

# 🧙‍♂️ 1. Wizard UI Refactor (🟡)

## Goal
Replace current single-screen UI with a guided 5-step wizard.

## Tasks

- [ ] Add `CurrentStep` enum to ViewModel
- [ ] Implement navigation:
  - [ ] NextCommand
  - [ ] BackCommand
- [ ] Split UI into steps:
  - [ ] Step 1: Folder selection
  - [ ] Step 2: Strategy selection
  - [ ] Step 3: Rule configuration
  - [ ] Step 4: Preview (DataGrid + details)
  - [ ] Step 5: Execute / Rollback
- [ ] Convert MainWindow.xaml into step container
- [ ] Add progress indicator (Step 1–5)
- [ ] Keep preview DataGrid unchanged but move to Step 4
- [ ] Ensure MVVM (no logic in code-behind)

---

# 🌍 2. Localization (German-First) (🟡)

## Goal
Full bilingual UI with German as default.

## Tasks

- [ ] Replace ALL hardcoded strings in XAML
- [ ] Move strings into:
  - [ ] Strings.de-DE.xaml
  - [ ] Strings.en-US.xaml
- [ ] Add UI language selector
- [ ] Default UI language = German
- [ ] Ensure bindings use DynamicResource
- [ ] Translate missing German strings

## Validation

- [ ] App fully usable in German
- [ ] No English fallback unless selected

---

# 🧭 3. Strategy Presets & Recommendations (🟡)

## Goal
Surface strategy presets and recommend best options after scan.

## Tasks

- [ ] Bind existing `StrategyPresets` to UI
- [ ] Add strategy selection UI (cards or list)
- [ ] Implement RecommendationService

### Recommendation Logic

- [ ] Analyze:
  - [ ] file types
  - [ ] date density
  - [ ] duplicate density
  - [ ] semantic signals
- [ ] Score strategies
- [ ] Return top 3–5 with:
  - [ ] name
  - [ ] reason
  - [ ] confidence

## UI

- [ ] Display recommended strategies
- [ ] Allow one-click selection

---

# 🧹 4. Hash-Based Duplicate Detection (🔴)

## Goal
Detect duplicates using file content (NOT filenames).

## Tasks

### Infrastructure

- [ ] Create `FileHashService`
- [ ] Implement SHA-256 hashing
- [ ] Add size-based pre-filtering

### Application Layer

- [ ] Build duplicate groups
- [ ] Define canonical selection logic:
  - [ ] best destination
  - [ ] oldest file
  - [ ] richest metadata

### UI

- [ ] Display duplicate groups in preview
- [ ] Mark duplicates clearly

### Execution

- [ ] Move duplicates to:
  `_Duplikate_Pruefen`

### Rollback

- [ ] Journal duplicate operations
- [ ] Ensure reversibility

---

# 📄 5. PDF & Content Extraction (🔴)

## Goal
Extract text from PDFs and other documents.

## Tasks

- [ ] Add PDF text extraction service
- [ ] Extend existing content pipeline:
  - [ ] PDF
  - [ ] DOCX
  - [ ] TXT
- [ ] Implement sampling for large files
- [ ] Add fallback to metadata

## Validation

- [ ] No crashes on unreadable PDFs
- [ ] Pipeline continues on failure

---

# 🧠 6. Gemini Integration (🟡)

## Goal
Use AI for contextual classification and grouping.

## Tasks

- [ ] Send extracted text to Gemini
- [ ] Request:
  - [ ] topic
  - [ ] project grouping
  - [ ] semantic category
- [ ] Merge with local heuristics

## Constraints

- [ ] Gemini NEVER generates final paths
- [ ] Results must be validated locally

---

# 🔗 7. Contextual File Grouping (🟡)

## Goal
Group related files into projects/topics.

## Tasks

- [ ] Build clustering logic:
  - [ ] based on Gemini + heuristics
- [ ] Detect project-level groupings
- [ ] Feed into strategy recommendations

---

# 🔁 8. Rollback System Upgrade (🔴)

## Goal
Make rollback flawless and historical.

## Tasks

### Journal Improvements

- [ ] Add versioning
- [ ] Store:
  - [ ] file hash
  - [ ] file size
  - [ ] timestamps
  - [ ] operation type
  - [ ] rollback status

### Execution Flow

- [ ] Write journal header BEFORE execution
- [ ] Append per operation
- [ ] Mark run complete AFTER execution

### Rollback Features

- [ ] Load historical runs
- [ ] Select run to rollback
- [ ] Preview rollback plan
- [ ] Handle:
  - [ ] missing files
  - [ ] conflicts
  - [ ] partial failures

### Guarantees

- [ ] Idempotent rollback
- [ ] Safe to run multiple times

---

# 🧪 9. Testing (🔴)

## Goal
Ensure system reliability and safety.

## Tasks

### Rollback Tests

- [ ] Full rollback
- [ ] Partial rollback
- [ ] Conflict handling
- [ ] Repeated rollback

### Deduplication Tests

- [ ] Hash correctness
- [ ] Large file handling
- [ ] Dedup rollback

### Content Tests

- [ ] PDF extraction
- [ ] Failure fallback
- [ ] Gemini unavailable scenario

---

# 🧱 10. Architecture Compliance (🔴)

## Must Ensure

- [ ] No logic in code-behind
- [ ] Application layer contains orchestration
- [ ] Domain layer contains rules
- [ ] Infrastructure handles IO, hashing, AI

---

# 🚫 11. Anti-Patterns to Avoid

- [ ] ❌ Filename-based duplicate detection
- [ ] ❌ Unjournaled file operations
- [ ] ❌ Hardcoded UI strings
- [ ] ❌ AI-generated paths without validation
- [ ] ❌ Operations outside root folder
- [ ] ❌ Non-reversible destructive actions

---

# 🟢 12. Optional Enhancements

- [ ] OCR for scanned PDFs
- [ ] Duplicate similarity detection (non-exact)
- [ ] Visual diff for duplicates
- [ ] Strategy simulation comparison
- [ ] Performance optimization for large directories

---

# ✅ Definition of Done

The system is complete when:

- [ ] All actions are previewed before execution
- [ ] Duplicate detection is hash-based and correct
- [ ] Rollback works across sessions and is reliable
- [ ] PDFs are processed for content analysis
- [ ] Gemini is integrated safely (advisory only)
- [ ] UI is fully localized (German default)
- [ ] Wizard UI is implemented
- [ ] All tests pass

---

# 🧠 Guiding Principle

This system must prioritize:

👉 Safety  
👉 Transparency  
👉 Reversibility  

Over speed or automation.

Users must always remain in control.