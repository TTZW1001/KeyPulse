# KeyPulse Development Status

Current Phase: P2 (accepted)

## Completed

- P0 Environment Preparation
- P1 Technical Spike
- P2 Project skeleton

## Current

- Ready for P3 Input capture

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

`857d8d9` — `feat: initialize project architecture`

## Known Issues

- Inno Setup not installed (P16)
- DB Browser for SQLite not installed (optional)
- `global.json` pins SDK 8.0.425 (`rollForward: latestFeature`)
- PRD lists dark mode as a V1.1 candidate; UI / architecture treat Light/Dark/System as V1.0. Resolve before P7
- Formal `.ico` not generated yet (P16)
- Spike.Input WPF working set was ~270 MB during a mouse flood; watch memory in P14
- User has not hand-tested Microsoft Pinyin, tray overflow visuals, fullscreen, or a 5–10 minute real mouse run. Complete IME before P3 acceptance
- Synthetic SendInput keys arrived as `VKey=0` (`VK_00`). P3 KeyMapper must prefer scan code when VKey is 0 or `VK_PROCESSKEY`
- Spike.Tray uses `Icon.FromHandle(GetHicon())` without `DestroyIcon`; fix in P6
- App and Infrastructure pin slightly different Microsoft.Extensions patch versions (8.0.1 vs 8.0.2). Harmless; align when convenient
