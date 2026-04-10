# Session Status

## Done

- Wizard UX is implemented.
- German-first localization is in place.
- View-model option labels, dialogs, and status messages now localize with language switching.
- Strategy recommendations are implemented and tested.
- Exact duplicate detection already uses size pre-filtering and SHA-256.
- Rollback backend now saves journals before execution starts and after each successful move.
- Rollback tests now cover restore, folder-scoped rollback, historical journal targeting, conflict skip, and idempotency.
- The execute step now exposes saved runs for full rollback and folder-scoped undo selection.
- The execute step now shows a rollback preview tab for the selected saved run.
- Rollback preview now shows expected readiness/conflict states from the rollback service.
- Journal entries now persist rollback status and last rollback attempt details.
- Build and tests are green.

## Still Open

- rollback preview exists and is status-aware, but it is still a straightforward list rather than a richer confirmation/diff flow
- richer journal metadata is still missing content-hash coverage
- PDF extraction is still missing
- duplicate canonical selection is still simplistic

## Recommended Next Slice

Rollback hardening:

1. improve rollback preview into a richer confirmation/diff flow
2. finish richer journal metadata, especially content hash coverage
3. decide whether to mark journal completion more explicitly after successful rollback
4. keep tests green while expanding coverage

## Last Known Green Commands

```powershell
dotnet build FileTransformer.sln -c Debug --no-restore
dotnet test FileTransformer.sln -c Debug --no-build
```
