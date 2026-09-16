# KeyPulse P21 — V1.1.4 global-hotkey diagnostics

## Confirmed failure

On the user's Windows machine, ordinary shortcuts such as Ctrl+C were aggregated but a Snipaste global Ctrl+Q hotkey was not. The persisted aggregate contained both Ctrl and Q counts but no Ctrl+Q row. The keyboard page already refreshed unflushed shortcut data about once per second, so this was not a UI refresh or persistence delay.

## Compatibility fix

- Each parsed keyboard event carries a snapshot of the Windows physical modifier state.
- Shortcut recognition combines tracked raw-input modifiers with that physical snapshot.
- A scan-code-recovered `VK_FF` terminal key may reuse a modifier that was active within the previous 250 ms. The grace applies only to explicitly recovered global-hotkey packets, never ordinary keys.
- Existing scan-code recovery and true-overrun rejection remain unchanged.

## Opt-in diagnostics

- Settings contains a temporary raw-keyboard diagnostics switch that defaults off every launch.
- While enabled, only the newest 16 entries are held in memory and shown in the UI.
- Entries include numeric virtual/scan codes, stable key names, down/up state, observed modifiers, and the shortcut decision.
- No diagnostic entry is written to SQLite or Serilog. Disabling the switch clears the buffer immediately.

## Verification

- Regression tests cover missing modifier packets, delayed recovered terminal keys, ordinary-key false-positive prevention, and diagnostics opt-in/clear behavior.
- Removing the compatibility branches makes both shortcut regression tests fail; restoring them returns the suite to green.
