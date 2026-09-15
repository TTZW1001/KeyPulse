# KeyPulse Development Status

Current Phase: P16 (accepted) — V1.0 complete

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
- P13 Settings
- P14 Stability
- P15 Tests
- P16 Packaging

## Last Verified Commit

`7c450c3` — `chore: add installer and readme`

## Known Issues

- DB Browser for SQLite not installed (optional)
- `global.json` pins SDK 8.0.425 (`rollForward: latestFeature`)
- Release artifacts are unsigned; SmartScreen may warn
- WPF working set is still ~200 MB at idle; watch in P14
- Microsoft Pinyin still not user-tested
- Hourly `active_seconds` still 0; app-level `active_seconds` is a 1 s foreground sample estimate
- AFK pause is not implemented; Flush stays 60 s
- Sleep/resume and logon autostart need user hand-tests
- LiveCharts 2.0.5 pulls OpenTK / SkiaSharp.Views.WPF as netframework (NU1701 suppressed)
- Numpad Enter and main Enter share key_code `Enter`
- Keyboard page still refreshes from the 1s shell timer when not visible
- No DB backup / JSON export / clear-today (P13 shipped CSV export + clear-all only)
