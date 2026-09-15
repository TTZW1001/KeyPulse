# KeyPulse P12 Apps

Date: 2026-09-15

## process_name / display_name

`ForegroundAppSampler` every **1000 ms** (range 500–1000) calls `ForegroundAppResolver`:

```
GetForegroundWindow
→ GetWindowThreadProcessId   (HWND → PID only; never GetWindowText)
→ OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)
→ QueryFullProcessImageName  (in-memory path only)
→ Path.GetFileName           → process_name  e.g. chrome.exe
→ FileVersionInfo.FileDescription → display_name  e.g. Google Chrome (nullable)
```

The full image path is never stored on `ForegroundApp`, never written to SQLite, never shown in the UI. Resolve failure → skip this sample (no `"unknown window"`). Input events read the cache only; if the cache is empty the global key/mouse totals still increment.

## Sampling

| | |
|---|---|
| Interval | 1000 ms |
| First sample | immediately on sampler start |
| Input path | `InputAggregator.Record` reads `IForegroundAppCache.Current` |
| Sleep/gap | active_seconds ignored if elapsed &lt; 500 ms or &gt; 5 s |

## Exclusion

Default (in code, always on):

```
LockApp.exe
LogonUI.exe
```

User extras: `%LOCALAPPDATA%\KeyPulse\config\excluded-apps.json` (`{"processes":["KeePass.exe"]}`). Case-insensitive `process_name`. Excluded apps are omitted from `AppCounts` / `daily_app_stats`; **global keyboard and mouse totals still count**.

Apps page has an **排除** button per row (writes the JSON immediately). Footer lists current exclusions. Settings page is not the editor (P13).

## active_seconds

Coarse estimate: each sampler tick while tracking is Running and the previous foreground app is not excluded adds `round(elapsed seconds)` (typically 1). Hourly `active_seconds` remains 0.

## Merge / Flush

`StatisticsBatch.AppStatsByDate` is swapped and flushed in the same transaction as keys/mouse/hourly. Failure merges the batch back (including app rows). `IAppQuery` = SQLite `daily_app_stats` ⨝ `app_registry` + unflushed snapshot in `[from, to]`. Ranking default: key presses descending.

## Privacy proof (no title / URL / path)

Unit tests: `AppTables_HaveNoTitleUrlPathColumns`, `Source_DoesNotCallGetWindowText`, `Flush_WritesAppTables_WithoutPathOrTitle`.

Against the live DB after a run and clean exit:

```text
python -c "import sqlite3,os; db=os.path.expandvars(r'%LOCALAPPDATA%\KeyPulse\data\keypulse.db'); con=sqlite3.connect(db); print([tuple(r) for r in con.execute('PRAGMA table_info(app_registry)')]); print([tuple(r) for r in con.execute('PRAGMA table_info(daily_app_stats)')]); print(list(con.execute('SELECT process_name, display_name FROM app_registry')))"
```

Expected columns:

```
app_registry:     app_id, process_name, display_name, first_seen_at, last_seen_at
daily_app_stats:  stat_date, app_id, key_press_count, mouse_click_count, wheel_event_count, mouse_distance_pixels, active_seconds
```

No `title` / `url` / `path` columns. Live rows after P12 verification: `msedge.exe` / `Microsoft Edge`, `KeyPulse.exe` / `KeyPulse` — filenames only.

```text
dotnet test tests/KeyPulse.Tests -c Release --filter Source_DoesNotCallGetWindowText
```

## Live UI

Started `KeyPulse.exe`, opened 应用. Ranking showed Microsoft Edge then KeyPulse, 今天/最近 7 天, 排除 buttons, footer `已排除 LockApp.exe、LogonUI.exe`. Empty copy unused (data present). No path, no window title, no Oops.

## Tests

`dotnet build` 0 Error. `dotnet test` 94 passed (resolver fake PID, exclusion vs global totals, flush + PRAGMA, source scan).
