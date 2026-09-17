# P24 — V1.2.1 interface consistency and preference persistence

## Scope

- Make every hourly dashboard tooltip show a stable hour heading and value-only series rows.
- Right-align chart legends and peer selectors without crowding section titles.
- Persist keyboard, mouse, pointer heatmap, apps, trends, custom trend dates, and dashboard metric preferences independently.
- Regroup pointer heatmap controls into filters, layers, display modes, and recording/privacy.
- Replace the native-looking trend custom-date row with a themed progressive `从 / 至` layout.
- Preserve light/dark theme behavior, keyboard focus, upgrade compatibility, and local-only storage.

## Compatibility

- No SQLite schema or statistics migration.
- New preferences are optional fields in `settings.json`; 1.2.0 files keep their old settings and receive safe defaults.
- Invalid new preference values fall back per field instead of resetting the complete settings file.
- Selecting `今天` persists the semantic range and resolves against the current date on each query.
- Custom trend dates persist as ISO `yyyy-MM-dd` values.

## Verification

- Release build and all 153 automated tests pass.
- The self-contained x64 installer builds successfully and its SHA-256 matches `release/checksums.txt`.
- Verify tooltip headings at sparse and non-sparse hours.
- Verify independent preference restoration across process restarts.
- Verify light/dark themes, keyboard navigation, default/minimum window sizes, and 100–200% scaling.
- Verify an in-place 1.2.0-to-1.2.1 installer upgrade preserves `%LOCALAPPDATA%\KeyPulse`.
