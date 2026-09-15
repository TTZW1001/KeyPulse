# KeyPulse Development Status

Current Phase: P3 (accepted, IME hand-test still open)

## Completed

- P0 Environment Preparation
- P1 Technical Spike
- P2 Project skeleton
- P3 Input capture

## Current

- Ready for P4 Aggregation

## Pending

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

`aad95a5` — `feat: implement raw input capture`

## Known Issues

- Inno Setup not installed (P16)
- DB Browser for SQLite not installed (optional)
- `global.json` pins SDK 8.0.425 (`rollForward: latestFeature`)
- PRD lists dark mode as a V1.1 candidate; UI / architecture treat Light/Dark/System as V1.0. Resolve before P7
- Formal `.ico` not generated yet (P16)
- WPF working set is still ~200 MB at idle; watch in P14
- Microsoft Pinyin not user-tested; do it during/before P4 if possible, required before calling input “complete”
- Live wheel was thin in short runs; parser tests cover direction
- Synthetic SendInput (even scan-code) is an unreliable Raw Input source; accept hardware tests only
- `RawInputService` stop can log twice if Dispose races with StopAsync; tidy in P6
- `DebugInputCounters` lives in App; P4 should replace it with Infrastructure aggregation
