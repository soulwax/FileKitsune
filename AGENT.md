# AGENT.md
## Quick Instructions for Codex GPT-5.4

---

## Mission

Transform this project into a:

✔ Safe  
✔ Reversible  
✔ German-first  
✔ AI-assisted file organization system  

---

## Non-Negotiable Rules

- Preview EVERYTHING before execution
- Journal ALL operations
- Rollback MUST always work
- AI is advisory only
- NEVER trust filenames for duplicates
- NEVER operate outside root directory

---

## Core Features to Build

### 1. Wizard UI
5 steps:
1. Folder
2. Strategy
3. Rules
4. Preview
5. Execute / Undo

---

### 2. German-First System
- UI default: German
- Folder names: German
- Filenames: German
- Full localization required

---

### 3. Duplicate Detection
- Use SHA-256 hashes
- Group identical files
- Move duplicates to:
  `_Duplikate_Pruefen`
- MUST support rollback

---

### 4. PDF + Content Analysis
- Extract text locally
- Support PDF + DOCX + TXT
- Fallback to metadata

---

### 5. Gemini Integration
- Use for:
  - topic detection
  - clustering
  - project inference
- NEVER generate final paths

---

### 6. Strategy Recommendations
Suggest after scan:
- Projektbasiert
- Datum
- Semantisch
- Duplikate bereinigen

---

### 7. Rollback System
- Support ALL past runs
- Preview rollback before execution
- Must be idempotent

---

## Architecture Rules

- UI → ViewModel only
- Logic → Application layer
- Rules → Domain
- IO / hashing / AI → Infrastructure

---

## Testing (Required)

- Rollback reliability
- Duplicate correctness
- PDF extraction
- AI fallback safety

---

## Goal

Build a system that users TRUST.

Not fast. Not flashy.

👉 Safe. Explainable. Reversible.