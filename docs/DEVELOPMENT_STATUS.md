# KeyPulse Development Status

Current Phase: P6 (accepted)

## Completed

- P0 Environment Preparation
- P1 Technical Spike
- P2 Project skeleton
- P3 Input capture
- P4 Aggregation
- P5 SQLite persistence
- P6 Tray and lifecycle

## Current

- Ready for P7 Base UI

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

`eb29673` — `feat: add tray lifecycle`

## Known Issues

- Inno Setup not installed (P16)
- DB Browser for SQLite not installed (optional)
- `global.json` pins SDK 8.0.425 (`rollForward: latestFeature`)
- Formal `.ico` not generated yet (P16); tray uses PNG→HICON
- WPF working set is still ~200 MB at idle; watch in P14
- Microsoft Pinyin still not user-tested
- AppCounts / app tables reserved empty until P12
- `active_seconds` persisted as 0 until AFK/activity sampling exists
- Sleep/resume and logon autostart need user hand-tests
- Debug UI is still the P5 skeleton; P7 replaces it with the product shell
- Tray menu and Debug UI still call SQLite with `GetResult()` on the UI thread; make async in P8
- Light/Dark: follow UI + architecture + implementation plan in P7 (PRD listed it as V1.1)
