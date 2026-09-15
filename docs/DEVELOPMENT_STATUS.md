# KeyPulse Development Status

Current Phase: P9 (implemented, pending acceptance)

## Completed

- P0 Environment Preparation
- P1 Technical Spike
- P2 Project skeleton
- P3 Input capture
- P4 Aggregation
- P5 SQLite persistence
- P6 Tray and lifecycle
- P7 Base UI
- P8 Dashboard
- P9 Keyboard heatmap

## Current

- Ready for P10 Mouse page

## Pending

- P10 Mouse page
- P11 Trends page
- P12 Apps stats
- P13 Settings
- P14 Stability
- P15 Tests
- P16 Packaging

## Last Verified Commit

`b383dd7` — `feat: add keyboard heatmap`

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
- LiveCharts 2.0.5 pulls OpenTK / SkiaSharp.Views.WPF as netframework (NU1701 suppressed)
- Numpad Enter and main Enter share key_code `Enter`
