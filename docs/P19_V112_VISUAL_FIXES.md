# KeyPulse P19 — V1.1.2 visual fixes

Date: 2026-09-16

## Fixes

- Replaced the nested platform ToggleButton chrome in custom ComboBoxes. Mouse and keyboard focus now use
  the existing accent border without painting a large system-blue rectangle around the selector.
- ComboBox selected text and keyboard heatmap labels use theme-aware foreground colors. Low-frequency and
  unused keys remain readable in dark mode.
- Screen heatmap PNG export now renders its 1800×920 device-independent canvas at 96 DPI, preventing the
  rightmost panel and bottom summary from being clipped by unintended 1.5× DPI scaling. Heatmap bitmaps
  and export panels preserve the virtual desktop aspect ratio instead of squeezing tall displays.
- Application statistics use subtle alternating row surfaces: blue-gray in light mode and a desaturated
  blue-gray in dark mode. Hover and selection states still take precedence.

## Verification

- Build and full regression suite must pass for Debug and Release.
- Manually verify keyboard and trend selectors with mouse and keyboard focus in both themes.
- Export a heatmap and confirm all three panels plus the coverage/privacy footer are visible.
- Inspect the Apps table in both themes and confirm alternating, hover, selection, text, and scroll-bar
  contrast remain distinct.
