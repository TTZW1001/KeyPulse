# KeyPulse P15 Tests

Date: 2026-09-15

Implementation commit: `576c9e3` (`test: expand coverage`)

## Automated coverage by behavior

| Risk area | Evidence |
|---|---|
| Key mapping | Common keys, IME process-key fallback, unknown scan codes, extended navigation/numpad distinction |
| Aggregation | Pause/resume, concurrent input, immutable buffer swap, failed-batch merge |
| Date boundary | Events and persisted rows split correctly across local midnight |
| Mouse wheel | Vertical/horizontal and positive/negative directions remain distinct |
| App exclusion/privacy | Default and user exclusions, case-insensitive matching, process-name-only storage |
| Migration | Expected schema, WAL, privacy-negative schema checks, repeated initialization is idempotent |
| SQLite/Repository | Reopen persistence, empty batch, failure recovery, transaction-backed writes |
| UPSERT | Repeated writes accumulate key, mouse, hourly, and per-app totals without replacement |
| Export | Four UTF-8 BOM CSVs, exact safe headers, comma/quote escaping |
| Stability | Resume, session ending, Explorer routing, corrupt config, database recovery |

Release verification:

- `dotnet build KeyPulse.sln -c Release --no-restore -m:1`: 0 warnings, 0 errors.
- `dotnet test KeyPulse.sln -c Release --no-build --no-restore -m:1`: 119 passed, 0 failed.
- Production source scan: no calls to `GetWindowText`, `GetWindowTextLength`, `ToUnicode`,
  `ToUnicodeEx`, `MainWindowTitle`, or UI Automation text APIs.

No numeric line/branch coverage is claimed. The repository does not currently include an
XPlat coverage collector, and P15 prioritizes high-risk behavior assertions over a vanity percentage.

## Explicitly not applicable to V1.0

- AFK behavior is not tested because AFK auto-pause is not implemented by product decision.
- Database backup is not tested because V1.0 does not implement a DB backup feature.

## User hand-test matrix

The following remain real-device/manual checks and are not marked as automated passes:

- Microsoft Pinyin and English input
- Browser, VS Code, game, and File Explorer foreground use
- Full-screen use and dual monitors
- Sleep/resume and reboot
- Logon autostart and silent tray startup
- Explorer restart and tray restoration
- 4–8 hour or multi-day resource/stability observation
