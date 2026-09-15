# KeyPulse P8 Dashboard

Date: 2026-09-15

## Merge

Displayed today / 7-day / hourly:

```
SQLite persisted rows
+
unflushed StatisticsBatch (date-keyed)
```

Query layer: `IDashboardQuery` / `DashboardQueryService`. ViewModel does not SQL.

Read DB first, then `CaptureUnflushed()`, so a Flush in between cannot double-count (may lag one tick instead).

Today totals refresh every 1s. 7-day and hourly charts refresh on first load, date change, theme change, or successful Flush (`IFlushService.Flushed`).

## LiveCharts

Package: `LiveChartsCore.SkiaSharpView.WPF` **2.0.5**

- 7-day: line, X = `M/d` (today-6 … today), Y = key presses
- Hourly: 24 columns, X = 0–23, Y = keys + clicks + wheel
- Single accent `#4E6E9E`, no rainbow
- Tooltip: `N0` counts
- Empty: still 7 / 24 zeros, no crash

## Empty / restart

- No rows + empty snapshot → `0` / 最常用「暂无」 / flat charts
- After process restart, persisted today rows still show on 总览
- Flush does **not** zero the page

## Tray

Menu totals use the same `GetTodayAsync` (async, not UI-thread `GetResult()`).

## Agent live run

- 总览 opened with persisted today (159 keys) + 7-day line + hourly bar
- SendWait `aaa` → 162 keys (live unflushed)
- X still hid to tray
