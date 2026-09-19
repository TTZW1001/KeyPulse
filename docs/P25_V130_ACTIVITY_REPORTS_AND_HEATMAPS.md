# P25 — V1.3.0 activity insight, reports, and heatmap personalization

## Scope

- Effective-use time uses a configurable AFK grace period (default 5 minutes, range 1–60).
- A new activity session begins after 15 continuous minutes without input.
- Sessions store minute-level interval summaries and aggregate counts, never raw input content.
- Trends provide separate keyboard, click, and wheel stacks, plus an effective-time view.
- Multi-day hourly charts default to a per-active-day average and may switch to range totals.
- Reports cover the current Monday–Sunday week, current calendar month, and trailing 12 months.
- Week and month comparisons use the same amount of elapsed time in the previous period.
- Reports export as a PNG summary or a self-contained offline HTML file.
- Automatic ZIP backups are opt-in, support daily/weekly schedules and keep 5 copies by default.
- Database status includes SQLite integrity checking.
- Heatmaps provide four built-in palettes. Keyboard image skins were removed after visual review.
- A user-provided screen image is copied into the local KeyPulse data directory, cropped at the
  virtual-desktop aspect ratio without stretching, and revealed by a continuous trajectory-density
  mask with local click boosts. UI preview and PNG export share the renderer.
- Managed screen-image sources are capped at 4096 px; rendered output is capped at 2048 px.
  Crop metadata includes the display-layout signature so changed layouts request a safe recrop.
- The About section links to the project repository using a local theme-adaptive GitHub mark.

## Privacy and compatibility

- Existing 1.2.x statistics are retained by schema migration V003.
- Effective time starts at the V003 migration date and is not inferred for older data.
- Screen images are user-provided visualization sources; KeyPulse still does not capture the screen.
- Imported image paths are managed local copies, so reports and statistics do not depend on the source file.
- Managed screen-image sources and crop metadata participate in ZIP backup/restore. Legacy screen
  images remain available for recropping; obsolete managed keyboard images are removed.
- Per-device statistics remain out of scope for 1.3.0.

## Validation target

- Full Release test suite.
- Release build and self-contained x64 installer.
- Upgrade install preserves `%LOCALAPPDATA%\KeyPulse` data and preferences.
- Installer, embedded product version, source revision, and checksums must agree before release.
