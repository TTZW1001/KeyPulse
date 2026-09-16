# KeyPulse P18 — V1.1.1 UI and installer polish

Date: 2026-09-16

This patch implements the follow-up acceptance findings recorded in the external frozen specification.

## User interface

- The compact preset is a dedicated 75% laptop layout. It omits right Win/Menu, retains right Alt/Ctrl,
  shortens right Shift, and uses a separated inverted-T arrow cluster without overlapping keys.
- Keyboard layout and trend selectors show localized labels in both the popup and collapsed state, with
  widths sized for their longest current labels.
- Page-level scroll bars are visually hidden while mouse wheel, touchpad, keyboard, and programmatic
  vertical scrolling remain available. The Apps data grid keeps its own visible scroll bar.
- Unknown `VK_*` key codes remain counted and are presented as unidentified function keys instead of an
  unexplained raw code.

## Ranges and export

- Keyboard and mouse aggregate selectors support Today, Last 7 days, Last 30 days, and All.
- Screen click and trajectory heatmaps have an independent selector with the same four options.
- Exact-pixel coverage remains cumulative across all recorded time and is labelled accordingly.
- Screen heatmaps export as one high-resolution PNG containing the selected range, monitor geometry,
  click density, trajectory density, cumulative coverage, and coverage percentage. No screenshot, window
  title, URL, input content, or per-event timeline is included.

## Persistence and installation

- Normal background persistence is batched every five minutes to reduce routine disk writes. Exit,
  suspend, and export still request an immediate flush, and the bounded retry schedule is unchanged.
- Version is 1.1.1.
- The installer detects an existing per-user installation. Older versions receive an explicit upgrade
  confirmation, the same version receives a repair/overwrite confirmation, and downgrade from a newer
  version is blocked. Prompts state that statistics, settings, and exclusions are preserved and ask the
  user to exit the tray application first.

## Verification

- Automated layout checks reject compact-key overlap and verify laptop modifier/arrow membership.
- Date-range checks cover all four inclusive start dates.
- The full unit/regression suite and Release build must pass before packaging.
- Manual acceptance should verify all selector labels, hidden page scrolling, PNG save output, Chinese and
  English installer prompts, and a real 1.1.0-to-1.1.1 overlay upgrade without deleting local data.
