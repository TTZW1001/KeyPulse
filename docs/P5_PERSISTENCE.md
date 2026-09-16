# KeyPulse P5 Persistence

Date: 2026-09-15

## Database

- Path: `%LOCALAPPDATA%\KeyPulse\data\keypulse.db`
- Verified: `C:\Users\ORANGE\AppData\Local\KeyPulse\data\keypulse.db`
- PRAGMA: WAL, synchronous=NORMAL, foreign_keys=ON, busy_timeout=5000, temp_store=MEMORY
- Migration: embedded `V001_Initial.sql` → `schema_version=1`
- Tables: `schema_version`, `daily_key_stats`, `daily_mouse_stats`, `hourly_activity_stats`, `app_registry`, `daily_app_stats`, `settings_meta`
- No event tables

`daily_app_stats` / `app_registry` are created and left empty (P12).

`hourly_activity_stats.active_seconds` is written as **0** (no AFK yet).

## Flush

- Default interval: 5 minutes (`PersistenceOptions.FlushInterval`); exit, suspend, and export still flush immediately.
- Exit: stop capture → `FlushNow` → shutdown
- UPSERT is `existing + excluded` (increment), never overwrite totals
- Dates come from the batch (event local dates), not flush clock
- Empty batch is a no-op

## Failure

On write failure:

1. `IStatisticsAggregator.Merge(batch)` puts the increment back into ActiveBuffer
2. Log the exception, not key names
3. Retry delays: 5s / 15s / 60s, then the normal 60s period

## Restart proof

1. Live run counted `A:5` in memory (`Persisted today keys: 0` before exit)
2. Window close flushed
3. Reopen showed memory snapshot empty and **Persisted today keys: 5**
4. Automated check `KEYPULSE_CHECK_DEFAULT_DB=1` passed

## Startup order

Resolve `IStatisticsAggregator` (and hosted `FlushService`, which constructs it) **before** `IInputCapture.StartAsync()`, so early events are not dropped.

## Tests

`dotnet test`: 45 passed (migration/WAL, two-flush accumulate, midnight two dates, empty batch, reopen, failed flush merge).
