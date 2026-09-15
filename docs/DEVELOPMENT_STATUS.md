# KeyPulse Development Status

Current Phase: P4 (accepted)

## Completed

- P0 Environment Preparation
- P1 Technical Spike
- P2 Project skeleton
- P3 Input capture
- P4 Aggregation

## Current

- Ready for P5 SQLite persistence

## Pending

- P5 SQLite persistence
- P6 Tray and lifecycle
- P7 Base UI
- P8 Dashboard
- P9 Keyboard heatmap
- P10 Mouse page
- P11 Trends page
- P12 Apps stats
- P13 Settings
- P14 Stability
- P15 Tests
- P16 Packaging

## Last Verified Commit

`8aa219b` — `feat: add statistics aggregation`

## Known Issues

- Inno Setup not installed (P16)
- DB Browser for SQLite not installed (optional)
- `global.json` pins SDK 8.0.425 (`rollForward: latestFeature`)
- PRD lists dark mode as a V1.1 candidate; UI / architecture treat Light/Dark/System as V1.0. Resolve before P7
- Formal `.ico` not generated yet (P16)
- WPF working set is still ~200 MB at idle; watch in P14
- Microsoft Pinyin still not user-tested
- AppCounts reserved empty until P12
- `InputAggregator` is currently resolved when the window is created, which is after Raw Input start; P5/P6 should resolve the aggregator first so startup events are not dropped
- `hourly_activity_stats.active_seconds` is not in the P4 snapshot; P5 may persist 0 until AFK is implemented
- Process restart still loses unflushed counts until P5
