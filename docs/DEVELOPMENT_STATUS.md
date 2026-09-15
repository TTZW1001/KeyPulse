# KeyPulse Development Status

Current Phase: P12 (accepted)

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
- P10 Mouse page
- P11 Trends page
- P12 Apps stats

## Current

- Ready for P13 Settings

## Pending

- P13 Settings
- P14 Stability
- P15 Tests
- P16 Packaging

## Last Verified Commit

`15bcfc0` — `feat: add app stats`

## Known Issues

- Inno Setup not installed (P16)
- DB Browser for SQLite not installed (optional)
- `global.json` pins SDK 8.0.425 (`rollForward: latestFeature`)
- Formal `.ico` not generated yet (P16); tray uses PNG→HICON
- WPF working set is still ~200 MB at idle; watch in P14
- Microsoft Pinyin still not user-tested
- Hourly `active_seconds` still 0; app-level `active_seconds` is a 1 s foreground sample estimate
- Sleep/resume and logon autostart need user hand-tests
- LiveCharts 2.0.5 pulls OpenTK / SkiaSharp.Views.WPF as netframework (NU1701 suppressed)
- Numpad Enter and main Enter share key_code `Enter`
- Keyboard page still refreshes from the 1s shell timer when not visible
