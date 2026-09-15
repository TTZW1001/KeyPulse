# KeyPulse Development Status

Current Phase: P2 (self-test complete)

## Completed

- P0 Environment Preparation
- P1 Technical Spike
- P2 Project skeleton

## Current

- Ready for P3 Input capture after total-control acceptance

## Pending

- P3 Input capture
- P4 Aggregation
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

P2 `feat: initialize project architecture`

## Known Issues

- Inno Setup not installed (P16)
- DB Browser for SQLite not installed (optional)
- `global.json` now pins SDK 8.0.425 (`rollForward: latestFeature`); default global `dotnet` without this file may still be 9.x
- PRD lists dark mode as a V1.1 candidate; UI / architecture treat Light/Dark/System as V1.0. Resolve before P7
- Formal `.ico` not generated yet (P16)
- Spike.Input WPF working set was ~270 MB during a mouse flood; watch memory in P14
- User has not hand-tested Microsoft Pinyin, tray overflow visuals, fullscreen, or a 5–10 minute real mouse run. Complete before P3 acceptance
- Tests project is `net8.0-windows` because it references Infrastructure; Core remains `net8.0`
