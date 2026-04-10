# Session Status

## Done

- Wizard UX is implemented.
- German-first localization is in place.
- View-model option labels, dialogs, and status messages now localize with language switching.
- Strategy recommendations are implemented and tested.
- Exact duplicate detection already uses size pre-filtering and SHA-256.
- Build and tests are green.

## Still Open

- rollback is still latest-run only
- journals are still saved at end-of-run instead of append-safe per operation
- rollback lacks dedicated tests
- PDF extraction is still missing
- duplicate canonical selection is still simplistic

## Recommended Next Slice

Rollback hardening:

1. journal versioning and richer entries
2. append-safe execution journaling
3. historical rollback selection
4. rollback tests

## Last Known Green Commands

```powershell
dotnet build FileTransformer.sln -c Debug --no-restore
dotnet test FileTransformer.sln -c Debug --no-build
```
