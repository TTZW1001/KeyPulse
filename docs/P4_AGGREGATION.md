# KeyPulse P4 Aggregation

Date: 2026-09-15

## Layout

Core (domain):

- `Statistics/TrackingState`, `HourBucket`, `HourlyActivity`, `MouseTotals`
- `Statistics/StatisticsSnapshot`, `StatisticsBatch`
- `IStatisticsAggregator`, `IStatisticsReader`

Infrastructure:

- `Aggregation/StatisticsBuffer` (mutable, per-date keys/mouse + hourly buckets)
- `Aggregation/InputAggregator` (subscribes to `IInputCapture`, dual buffer)

Snapshot/Batch live in Core so interfaces do not depend on Infrastructure.

App no longer owns counters. `DebugInputCounters` was deleted.

## Swap

```
lock
  swap ActiveBuffer / FlushBuffer
  clear the new ActiveBuffer
unlock
return FlushBuffer as an immutable StatisticsBatch
```

Batch is the increment since the previous swap, not process lifetime totals. Dictionaries in the batch are copies, so later swaps cannot change a returned batch.

UI calls `CaptureSnapshot()` only (unflushed active buffer). P4 does not auto-swap and does not write SQLite.

## Pause

`TrackingState.Paused` / `Running` / `Error`.

Paused: Raw Input still runs; aggregator discards events. Resume continues on the same active buffer. Pause is `SetState`, not a UI-only flag.

Error is set if Raw Input start fails.

## Midnight

Hour and daily key/mouse buckets use the event timestamp’s **local** date and hour, not swap time. A 23:59 event and a 00:01 event land in different `DateOnly` / hour buckets. Covered by unit test.

## AppCounts

Present on snapshot and batch, always empty. P12 will fill process names. P4 does not call `GetForegroundWindow` / `GetWindowText`.

## Agent live run

- A×4 while Running
- Pause + more A → still A:4, `State: Paused`, log `Tracking paused`
- Resume + B×3 → A:4 B:3, hour keys 7
- Real mouse distance 206 px (not SendInput)
- UI ticks ~1/s

## Tests

`dotnet test`: 37 passed (Pause/Resume, Swap increment + immutability, midnight buckets, wheel direction, concurrent Add, empty AppCounts).
