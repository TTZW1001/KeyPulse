# KeyPulse P3 Input Capture

Date: 2026-09-15  
Commit target: `feat: implement raw input capture`

## Layout

- Core: `Events/*`, `Models/KeyCode`, `Models/MouseButton`, `IInputCapture`
- Infrastructure: `Input/RawInputService`, `HiddenInputWindow`, `RawKeyboardParser`, `RawMouseParser`, `RawInputNativeMethods`, `KeyMapper`
- App: 1s Debug UI + optional Pause. No P/Invoke, no RAWINPUT parse.

## Listener

- Hidden Win32 popup window on a dedicated STA thread
- `RegisterRawInputDevices` keyboard + mouse with `RIDEV_INPUTSINK`
- Callback: parse → raise `InputEvent` → return
- No DB, no UI update, no per-key log, no clipboard, no `GetWindowText`

Unfocused: **yes**. After switching to Notepad, `B` and `LeftShift` still increased.

## KeyMapper

1. If `VKey` is `0` or `VK_PROCESSKEY` (0xE5) → scan code (Set 1), never `VK_00`
2. Otherwise virtual key, with E0 / make-code for Left/Right Ctrl, Shift, Alt
3. Unknown scan code → `Unknown`

Names match later `daily_key_stats.key_code`: `A`, `Space`, `LeftCtrl`, `F1`, `ArrowUp`, …

## Agent live run (`KeyPulse.exe`)

| Input | Result |
|---|---|
| A ×5, Space ×3, Backspace ×2 | counted with those names |
| After Notepad focus: B ×4, LeftShift ×2 | counted |
| Real mouse (not SendInput) | Left 1, Distance 265 px |
| Wheel | not seen in this short run; parser tests cover +/− |
| UI ticks | 2 → 9 over ~10s (1s refresh, not per MouseMove) |
| Log | `Raw Input listener started/stopped` only; no key names |

## IME

微软拼音: **未测**. Code does not call `ToUnicode` / IME APIs.

## Raw Input sufficient?

**YES** for P4. No fallback to `WH_KEYBOARD_LL`.

## Tests

`dotnet test`: 29 passed (KeyMapper, scan-code fallback, key up ignored, wheel sign, absolute move ignored).

## Not in this phase

SQLite, StatisticsBuffer, tray, navigation, heatmap.
