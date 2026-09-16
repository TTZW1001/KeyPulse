# KeyPulse P17 — V1.1 Interaction Insights

Date: 2026-09-16

This document records the implemented V1.1 behavior. The frozen product specification remains outside
the Git repository at `G:\grok-project\KeyPulse\docs\06_KeyPulse_V1.1升级需求与技术设计.md`.

## Shipped behavior

- The release remains a single x64 self-contained installer. No framework-dependent or portable package
  is produced.
- Inno Setup offers Simplified Chinese and English.
- Keyboard heatmaps provide compact/laptop, TKL, and full-size presets. Compact is the default; numpad
  activity can suggest the full-size layout but never changes the user's explicit choice automatically.
- Main-row digits, numpad digits, main Enter, and numpad Enter have distinct stable key codes.
- Shortcut statistics are enabled by default and aggregate combinations containing Ctrl, Alt, or Win.
  Shift is included when held, but Shift-only letter input is not treated as a shortcut. No key sequence,
  typed text, or event timeline is stored.
- Mouse distance uses consecutive Windows screen cursor coordinates. The displayed pixel distance is exact
  for the observed cursor path; physical distance is explicitly an estimate derived from monitor DPI.
- The mouse page uses compact summary cards and a mouse-shaped button heatmap. Horizontal wheel counts are
  retained and explained in the UI.
- Screen position statistics are opt-in and off by default. When enabled, click coordinates are aggregated
  by date/hour/display layout/monitor/button/coordinate. Trajectory density is stored in a bounded grid,
  while cumulative exact-pixel coverage uses tiled bitsets.
- Multi-monitor layouts use virtual-screen coordinates and stable geometry signatures. Visualizations keep
  monitor boundaries and select the most active matching layout for the requested date range.
- Position data can be cleared independently without deleting keyboard, button, wheel, distance, trend, or
  application aggregates.
- Native ComboBox styling replaces the old platform-default dropdown appearance.

## Performance constraints

- Screen coordinate lookup happens once per raw mouse packet, not once per derived event.
- Cursor distance is constant-cost per move event.
- Trajectory capture is sampled at no more than roughly 250 Hz; persistent writes remain batched by the
  existing flush service.
- Density grids are 256 cells wide with proportional height. Exact coverage uses 256×256 bit tiles and only
  dirty tiles are flushed.
- No screenshots, window contents, window titles, URLs, or cloud uploads are introduced.

## Database and compatibility

- Schema version: 2.
- V002 adds shortcut aggregates, accurate cursor/estimated distance columns, display geometry, hourly click
  points, daily trajectory density, and cumulative occupancy tiles.
- V001 databases are upgraded transactionally and existing rows are preserved.
- CSV export adds shortcuts and click-point aggregates plus cursor pixel and estimated meter columns. Binary
  density/coverage blobs are intentionally not exported as CSV.

## Automated verification

- Shortcut normalization and Shift-only exclusion.
- Compact/TKL/full layout membership and numpad distinction.
- Exact 3-4-5 cursor distance, DPI conversion, and position-data opt-in boundary.
- V1-to-V2 migration with preservation of existing statistics.
- Full existing regression suite, including persistence retry, lifecycle, queries, exports, and shell rules.

## Manual acceptance checklist

1. Start the installer and confirm that its first page allows Simplified Chinese and English; complete a
   Chinese installation and launch KeyPulse.
2. On Keyboard, switch among compact, TKL, and full-size layouts. Press both the top-row `1` and numpad `1`
   on a full keyboard and confirm they heat independently.
3. Press examples such as `Ctrl+L`, `Ctrl+Q`, `Alt+B`, and `Win+D`; confirm they appear below the keyboard
   heatmap. Plain `Shift+A` must not appear as a shortcut.
4. On Mouse, verify left/right/middle/side buttons and vertical/horizontal wheel counts. Move the pointer in
   a known straight segment and confirm pixel distance increases; treat meters as an estimate.
5. Enable screen position statistics on Mouse or Settings. Click across the screen, move the pointer, wait
   for refresh/flush, and confirm click, trajectory, and coverage views appear with the correct monitor shape.
6. Disable position statistics and confirm the existing heatmaps stop changing. Use "清除屏幕位置数据"
   and confirm other totals remain.
7. If a second monitor is available, place it left/above the primary display and repeat step 5. Confirm both
   monitor rectangles render in their relative positions without coordinate clipping.
8. During a game or other latency-sensitive workload, compare Task Manager CPU usage with position tracking
   off and on. The feature is sampled and batched, but real-world hooks/driver/game behavior still requires
   device-level observation.
