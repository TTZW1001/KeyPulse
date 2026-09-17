# P23 — V1.2.0 local insights, data governance, and input accuracy

## Input accuracy

- Raw mouse input now emits button-down and button-up transitions.
- An in-memory per-button gesture tracker uses the Windows drag rectangle to classify the gesture.
- A click is committed only on a paired release inside the threshold and uses the release coordinate.
- Drags, missing pairs, cross-layout movement, and paused-state remnants do not increment clicks or click heatmaps.
- Existing aggregate history is preserved and is not reclassified.

## Dashboard and exploration

- Seven-day trend uses a single selectable metric: keys, clicks, or wheel events.
- Exact cursor pixels and DPI-estimated physical distance are displayed as different certainty levels.
- Today summary reports the peak hour, top shortcut, top foreground app, historical comparison, and record day.
- Trend points and a keyboard-accessible detail button open the selected day in Trends.
- Pointer heatmaps support date, monitor, button, layer, normalization, and linear/log scale controls.
- The coverage challenge can be reset independently and is explicitly described as sampled-grid coverage.

## Data governance

- Settings displays database path, size, date range, last successful write, and write health.
- ZIP backups use SQLite's consistent backup API and include settings, exclusions, a schema/version manifest,
  and SHA-256 hashes. Restore validates hashes and SQLite integrity, then atomically replaces the database.
- Restore first creates a safety backup of the current installation and requires a restart to reload config singletons.
- Position detail retention supports 30, 90, and 365 days or permanent. New installs default to 90 days;
  existing installs remain permanent until the user chooses otherwise.
- Date-range clearing leaves cumulative coverage untouched; coverage has a separate reset action.
- The UI timer and page event refreshes stop while the window is hidden to the tray.

## Verification

- 151 Release tests pass, including click/drag pairing, backup/restore, changed-payload rejection,
  date-range isolation, and position-only retention cleanup.
- Release publish and Inno Setup compilation succeed for version 1.2.0.
- `checksums.txt` matches the generated self-contained installer.
