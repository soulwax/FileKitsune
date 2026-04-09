# AGENT.md

## Mission

Turn FileTransformer into a system users can trust:

- safe
- explainable
- reversible
- German-first for folder naming
- strongly AI-assisted, but never AI-driven

## Non-Negotiables

- Preview everything before execution.
- Never operate outside the selected root.
- Journal every executed filesystem mutation.
- Rollback must stay reliable and explicit.
- Gemini is important for context detection and strategy suggestions, but advisory only.
- Local validation always wins over model output.

## Preferred Delivery Order

1. Expose the existing strategy and language options already present in the view model.
2. Move the UI toward a 5-step wizard: folder, strategy, rules, preview, execute/undo.
3. Finish German/English localization.
4. Improve Gemini-assisted context detection and recommendations.
5. Strengthen historical rollback and rollback tests.

## Architecture Rules

- UI only in `src/App`
- orchestration in `src/Application`
- rules in `src/Domain`
- filesystem, hashing, journals, settings, Gemini, and logging in `src/Infrastructure`

## Critical Feature Directions

### German-first

- folder names default to German
- UI language is selectable
- filename language remains configurable

### Gemini

- use for context detection, project/topic inference, clustering hints, and strategy recommendation signals
- never let it generate final executable paths without local validation
- `.env` Gemini credentials should help local analysis if intentionally wired in code

### Rollback

- support more than latest-only history
- remain idempotent
- surface conflicts and skips clearly
- cover with dedicated tests

## Testing Focus

- rollback reliability
- duplicate correctness
- path safety
- Gemini fallback safety
- strategy recommendation logic

## Goal

Build a file organization workflow that feels careful, transparent, and reversible at every step.
