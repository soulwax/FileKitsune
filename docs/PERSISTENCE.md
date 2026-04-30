# Persistence

FileKitsune persists two kinds of state:

- user and workflow settings, so the app can reopen with the same preferences
- execution journals, so completed or partially completed file operations can be inspected and rolled back

The app uses layered persistence. Local storage is always the baseline. Postgres is optional shared persistence when `NILEDB_URL`, `POSTGRES_URL`, or `DATABASE_URL` is configured and reachable.

## Storage Locations

Local files live under `%LocalAppData%\FileKitsune`:

- `settings.json`: DPAPI-protected local settings, including local-only secrets
- `persistence.db`: SQLite cache for non-secret settings and execution journals
- `journals`: JSON execution journal files for compatibility and inspection
- `logs`: application logs

Remote Postgres stores two tables:

- `file_transformer_app_settings`
- `file_transformer_execution_journals`

The Postgres schema is created automatically on first use with `CREATE TABLE IF NOT EXISTS`.

## App Settings

App settings are represented by `AppSettings` and contain:

- `UiLanguage`: the UI culture, for example `de-DE` or `en-US`
- `Organization`: scan, planning, naming, review, duplicate, and protection preferences
- `Gemini`: Gemini feature flags and runtime options

Organization settings include:

- selected root directory
- include and exclude patterns
- content inspection limits and supported readable extensions
- preview and scan limits
- Gemini usage preference
- concurrency limits
- root confinement preference
- organization preset, dimensions, folder depth, date source, and sparse-category behavior
- naming language, rename mode, filename template, conflict handling, and filename length limits
- review thresholds and execution mode
- duplicate detection and routing policy
- protected folders/files, related-file behavior, hidden/system file behavior, and symlink/junction behavior

Gemini settings include:

- enabled/disabled state
- model name
- endpoint base URL
- rate limit
- request timeout
- maximum prompt size
- environment ping timestamp and fingerprint

## Secret Handling

Gemini API keys are not synced to Postgres.

When settings are saved, FileKitsune first writes the full local settings through the protected local settings store. Before writing settings to SQLite cache or Postgres, it sanitizes the snapshot and clears `Gemini.ApiKey`.

On load, FileKitsune merges the local protected baseline with the shared or cached non-secret settings. This means shared settings can update preferences such as language, strategy, and model, while the API key remains the current Windows user's local secret.

## Execution Journals

Execution journals are the rollback record for filesystem mutations. A journal contains:

- journal version
- journal id
- selected root directory
- creation time
- completion time, when available
- last saved time
- status, such as started, completed, canceled, or failed
- one entry per executed operation

Each journal entry contains:

- operation id
- source and destination full paths
- source and destination relative paths
- file name and extension
- execution timestamp
- operation outcome and notes
- whether the destination existed before the move
- content hash
- file size
- source creation and modification timestamps
- rollback status
- last rollback attempt timestamp
- rollback message

Journals are saved before execution starts, before relying on a filesystem mutation for rollback, after successful moves, and again when the run reaches a final status. This append-safer behavior is intentional: a crash or partial failure should still leave enough information to explain and undo what happened.

## Load And Save Behavior

Settings load order:

1. Load local protected settings as the baseline.
2. If remote persistence is enabled, try Postgres.
3. If Postgres succeeds, refresh the SQLite cache and merge non-secret remote settings over the local baseline.
4. If Postgres is unavailable, load the SQLite cache and merge it over the local baseline.

Settings save behavior:

1. Save full settings locally through the protected settings store.
2. Sanitize shared settings by removing secrets.
3. Save sanitized settings to SQLite.
4. If remote persistence is enabled, best-effort save sanitized settings to Postgres.

Journal load behavior:

1. Load and merge local SQLite journals with JSON journals.
2. If remote persistence is enabled, try Postgres.
3. Remote journals are copied into SQLite and JSON local stores.
4. Local-only journals are best-effort backfilled to Postgres.
5. Journals with the same id are de-duplicated by newest `LastSavedAtUtc`.

Journal save behavior:

1. Update `LastSavedAtUtc`.
2. Save to local SQLite.
3. Save to local JSON.
4. If remote persistence is enabled, best-effort save to Postgres.

Remote persistence failures do not block local operation. The app falls back to local SQLite and JSON storage.

## Offline Mode

Set `FILEKITSUNE_OFFLINE_MODE=true` to force local-only mode even when a Postgres connection string exists.

For tests and local tooling that must ignore `.env`, set `FILEKITSUNE_IGNORE_DOTENV=true`.

## Database URL Formats

FileKitsune accepts either Npgsql keyword connection strings or URL-style Postgres strings:

```text
Host=example.test;Port=5432;Username=user;Password=secret;Database=filekitsune;SSL Mode=Require
postgresql://user:secret@example.test:5432/filekitsune?sslmode=require
```

`DATABASE_URL_UNPOOLED` may exist in `.env` for hosting platforms that provide it, but the app currently reads `NILEDB_URL`, `POSTGRES_URL`, and `DATABASE_URL` for remote persistence selection.

## What Is Not Persisted

FileKitsune does not persist preview plans as authoritative execution state. Preview plans are rebuilt from the selected root and current settings.

Gemini recommendations, semantic classifications, and grouping suggestions are advisory planning inputs. They are not trusted as execution authority. Final paths and rollback eligibility still come from local planning, path validation, and execution journals.

File contents are not copied into settings or journals. Journals store metadata needed for explainability and rollback, including paths, hashes, sizes, and timestamps.
