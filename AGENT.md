# FileKitsune Agent Notes

## Mission

Build FileKitsune into a file organization workflow users can trust:

- preview-first
- explainable
- reversible
- German-first
- AI-assisted, never AI-driven

## Current State

Already implemented:

- 5-step wizard UI
- German/English UI switching with German default
- localized wizard text, option labels, dialogs, and status messages
- strategy presets in UI
- advisory strategy recommendations after preview
- exact duplicate detection with size pre-filtering and SHA-256
- duplicate review surfaced in preview
- historical rollback selection, folder-scoped undo, and diff-style rollback preview

## Non-Negotiables

- preview everything before execution
- never operate outside the selected root
- journal every executed filesystem mutation
- keep Gemini advisory only
- keep duplicate identity hash-based
- do not move logic into code-behind

## Architecture Rules

- UI in `src/App`
- orchestration in `src/Application`
- rules in `src/Domain`
- filesystem, hashing, settings, journals, and Gemini in `src/Infrastructure`

## Highest-Value Next Work

1. Add OCR/image-first handling for scanned PDFs and image-led folders.
2. Continue rollback checkpoint hardening for partial-failure recovery.
3. Improve duplicate canonical selection with real-folder feedback.

## Secondary Next Work

1. Tune duplicate/project heuristics with real folder samples.
2. Expand semantic enrichment without weakening deterministic safety.
3. Keep assistant docs aligned with implemented behavior.
