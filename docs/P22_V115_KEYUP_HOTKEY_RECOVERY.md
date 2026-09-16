# P22 — V1.1.5 suppressed key-down hotkey recovery

## Field evidence

Snipaste's `Ctrl+Q` global shortcut produced this observable sequence:

1. `LeftCtrl` key-down, with system modifier state `Ctrl`.
2. No `Q` key-down packet reached KeyPulse.
3. `Q` key-up arrived with virtual key `0x51`, scan code `0x10`, and system modifier state `Ctrl`.
4. `LeftCtrl` key-up arrived afterwards.

The earlier shortcut tracker only emitted decisions for key-down packets, so this valid
sequence could never increment `Ctrl+Q`.

## Design

- Track non-modifier key-down packets in memory while a chord is active.
- A normal matching key-up is ignored because its key-down already made the decision.
- If a non-modifier key-up has no matching key-down and Ctrl, Alt, or Win is still held or
  reported by Windows, recover one shortcut decision from the key-up.
- Persist the recovered shortcut before returning from the key-up path.
- Keep ordinary key counts key-down-only. Recovery therefore does not invent a `Q` press.
- Show a successful recovered decision in the opt-in, memory-only input diagnostic panel.
- Clear transient state on pause, error, clear, and tracker reset.

## Privacy and limits

The change retains only transient key names needed for de-duplication. It does not persist
event order, typed text, window titles, or per-event timestamps. If another global-hook
application suppresses both the key-down and key-up packets, KeyPulse has no event from
which to recover the shortcut.

## Verification

- A regression test first reproduced the failure using the exact Ctrl-down / Q-up sequence.
- The recovered sequence increments `Ctrl+Q` once and does not increment the plain `Q` count.
- A normal Ctrl+Q down/up sequence is not double-counted.
- The diagnostic panel reports `组合判定：Ctrl+Q` for the recovered Q key-up.
- Full Release test suite passes.
