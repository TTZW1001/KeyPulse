# KeyPulse Development Status

Current Phase: P25 — V1.3.0 activity insight, reports, and heatmap personalization

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
- P17 V1.1 interaction insights
- P18 V1.1.1 interaction polish
- P19 V1.1.2 visual fixes
- P20 V1.1.3 dashboard detail and global-hotkey compatibility
- P21 V1.1.4 global-hotkey diagnostics and modifier recovery
- P22 V1.1.5 suppressed key-down hotkey recovery
- P23 V1.2.0 local insights, data governance, and input accuracy
- P24 V1.2.1 interface consistency and preference persistence
- P25 V1.3.0 activity insight, reports, and heatmap personalization

## Last Verified Commit

V1.3.0 release candidate — verified with 156 Release tests

## Known Issues

- DB Browser for SQLite not installed (optional)
- `global.json` pins SDK 8.0.425 (`rollForward: latestFeature`)
- Release artifacts are unsigned; SmartScreen may warn
- WPF working set is still ~200 MB at idle; watch in P14
- Microsoft Pinyin still not user-tested
- Effective time begins with schema V003; older dates intentionally show no effective-time history
- Foreground duration remains a separate 1 s sample estimate and is not used as effective time
- Sleep/resume and logon autostart need user hand-tests
- LiveCharts 2.0.5 pulls OpenTK / SkiaSharp.Views.WPF as netframework (NU1701 suppressed)
- Fn itself may not be reported by Windows; recognizable media/function results are still counted as keys
- Historical `VK_FF` rows are hidden rather than rewritten because their original keys cannot be recovered
- A third-party global hook that suppresses both key-down and key-up remains impossible to observe
- JSON export is not implemented; CSV export, PNG/HTML reports, and consistent ZIP backup/restore are available
- Physical cursor distance remains a DPI-based estimate; exact screen-coordinate distance is shown in pixels
