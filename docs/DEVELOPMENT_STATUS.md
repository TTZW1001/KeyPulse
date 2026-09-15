# KeyPulse Development Status

Current Phase: P3 (self-test complete)

## Completed

- P0 Environment Preparation
- P1 Technical Spike
- P2 Project skeleton
- P3 Input capture

## Current

- Ready for P4 Aggregation after total-control acceptance
- User still needs to hand-test Microsoft Pinyin (and optional fullscreen / live wheel)

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

P3 `feat: implement raw input capture`

## Known Issues

- Inno Setup not installed (P16)
- DB Browser for SQLite not installed (optional)
- `global.json` pins SDK 8.0.425 (`rollForward: latestFeature`)
- PRD lists dark mode as a V1.1 candidate; UI / architecture treat Light/Dark/System as V1.0. Resolve before P7
- Formal `.ico` not generated yet (P16)
- WPF working set is still ~200 MB at idle; watch in P14
- Microsoft Pinyin not agent-tested; must be user-tested before treating P3 as fully accepted
- Live wheel was not observed in the short agent run; parser unit tests cover direction
