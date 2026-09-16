# KeyPulse P20 — V1.1.3 dashboard and global-hotkey compatibility

## Scope

- Split each dashboard hourly column into keyboard, mouse-click, and wheel segments.
- Keep one compact stacked column per hour, a small inline legend, sparse three-hour labels, and exact hour ranges in tooltips.
- Recover global-hotkey terminal keys when a hook masks the virtual key as `VK_FF` but preserves a valid scan code.
- Drop true keyboard-overrun packets and unresolvable key packets.
- Hide historical `Unknown` / `VK_FF` key rows from key totals and keyboard rankings without guessing or rewriting user data.

## Privacy and data constraints

- Shortcut recovery uses only the current modifier state plus the stable key name derived from the scan code.
- No key sequence, text, window title, URL, screenshot, or full executable path is stored.
- Existing hourly storage already contains separate key, click, and wheel counts; no schema migration is required.

## Verification

- Parser regression covers `VK_FF` + Q scan code, overrun packets, empty scan codes, and out-of-range virtual keys.
- Shortcut regression covers left Ctrl+Q, right Ctrl+Q, and the masked Snipaste-style Ctrl+Q path through aggregation.
- Dashboard query regression verifies separated hourly counts and exclusion of invalid historical key rows.
