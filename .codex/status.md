# Session Status

## Done

- Wizard UX is implemented.
- German-first localization is in place.
- View-model option labels, dialogs, and status messages now localize with language switching.
- Strategy recommendations are implemented and tested.
- Exact duplicate detection already uses size pre-filtering and SHA-256.
- Duplicate canonical selection now prefers cleaner paths, then older files, then richer metadata.
- Duplicate hashing now has explicit size-prefilter and large-file coverage.
- Duplicate-routed moves are now covered by execution+rollback tests.
- Rollback backend now saves journals before execution starts and after each successful move.
- Rollback tests now cover restore, folder-scoped rollback, historical journal targeting, conflict skip, and idempotency.
- The execute step now exposes saved runs for full rollback and folder-scoped undo selection.
- The execute step now shows a rollback preview tab for the selected saved run.
- Rollback preview now shows expected readiness/conflict states from the rollback service.
- Rollback preview now includes an impact summary for ready restores versus expected skips/conflicts.
- Rollback confirmation dialogs now reuse preview counts for selected runs and folder-scoped undo.
- Journal entries now persist rollback status and last rollback attempt details.
- Execution journals now persist content hashes for executed files.
- Local content extraction now supports text-like files, DOCX, and text-based PDFs.
- Invalid or unreadable PDFs now fail safely with tested fallback behavior.
- Optional Postgres/Nile-backed shared persistence now exists for settings snapshots and journals.
- Local SQLite caching/fallback now keeps the app usable when remote persistence is offline or unavailable.
- Gemini secrets stay local and are not synced to shared persistence.
- The execute step now shows whether persistence is local-only, shared-online, or running on fallback.
- Gemini organization guidance can now be applied explicitly to strategy preset and maximum folder depth.
- Build and tests are green.

## Still Open

- rollback preview and confirmation are now impact-aware, but they are still not a fuller diff-style confirmation flow
- richer journal metadata is still incomplete beyond the now-persisted content hash
- duplicate behavior could still become more domain-aware, but the naive alphabetical canonical choice is gone

## Recommended Next Slice

Trust-polish follow-up:

1. improve rollback preview into a richer confirmation/diff flow
2. finish any remaining journal metadata polish beyond content hashes
3. deepen duplicate rollback and large-file hashing coverage
4. deepen cross-file clustering so Gemini guidance affects more than preset/depth advice

## Last Known Green Commands

```powershell
dotnet restore FileTransformer.sln
dotnet build FileTransformer.sln -c Debug --no-restore
dotnet test tests/FileTransformer.Tests/FileTransformer.Tests.csproj -c Debug
```
