# KeyPulse Development Status

Current Phase: P6 (self-test complete)

## Completed

- P0 Environment Preparation
- P1 Technical Spike
- P2 Project skeleton
- P3 Input capture
- P4 Aggregation
- P5 SQLite persistence
- P6 Tray and lifecycle

## Current

- Ready for P7 Base UI after total-control acceptance

## Pending

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

P6 `feat: add tray lifecycle`

## Known Issues

- Inno Setup not installed (P16)
- DB Browser for SQLite not installed (optional)
- `global.json` pins SDK 8.0.425 (`rollForward: latestFeature`)
- PRD lists dark mode as a V1.1 candidate; UI / architecture treat Light/Dark/System as V1.0. Resolve before P7
- Formal `.ico` not generated yet (P16); tray uses PNG→HICON
- WPF working set is still ~200 MB at idle; watch in P14
- Microsoft Pinyin still not user-tested
- AppCounts / app tables reserved empty until P12
- `active_seconds` persisted as 0 until AFK/activity sampling exists
- Sleep/resume and logon autostart need user hand-tests
- Debug UI is still the P5 skeleton, not a product shell
