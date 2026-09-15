# KeyPulse P6 Lifecycle

Date: 2026-09-15

## Tray

- WinForms `NotifyIcon` in the WPF app (`UseWindowsForms`)
- Icon: `assets/tray-icon.png` copied to output, 32×32
- `GetHicon` handle is cloned then **DestroyIcon** (fixes the Spike leak)
- Paused state uses a grayscale clone of the same PNG
- Double-click / “打开 KeyPulse” / “设置” → show main window (no Settings page)
- Tooltip: `KeyPulse · 正在统计` / `KeyPulse · 已暂停`
- Right-click today line is **persisted today + unflushed snapshot**, never window titles or key sequences

## Close vs Exit

- `ShutdownMode = OnExplicitShutdown`
- Title-bar X: hide, `ShowInTaskbar=false`, Raw Input and Flush keep running
- First hide can prompt “将继续在后台统计” + “是否不再提示？” (`config/settings.json` → `hideToTrayHintDismissed`)
- Tray **退出**: stop capture → Flush → dispose tray → `Shutdown`

## Single instance

- Mutex: `Local\KeyPulse.SingleInstance`
- Show signal: `Local\KeyPulse.ShowWindow` (second process sets this, then exits)
- Second process does **not** start Raw Input or open the database
- Agent/test exit signal: `Local\KeyPulse.RequestExit` (same path as tray Exit)

## Startup

- HKCU `Software\Microsoft\Windows\CurrentVersion\Run`
- Value name: `KeyPulse`
- Command: `"<exe>" --startup`
- `--startup` starts capture + Flush + tray, does not Show the main window
- Unit tests use an in-memory `IRunKeyStore`; they do not write the real Run key

## Power

- `SystemEvents.PowerModeChanged`
- Suspend: Flush
- Resume: log only (listener and flush timer were not stopped)
- Sleep/resume **not agent-tested**

## Agent live run

- X: process stayed up, window not visible
- Keys while hidden: `A:4` after re-show
- Second `KeyPulse.exe`: exited, one process left
- `--startup`: `MainWindowHandle=0`
- Exit signal: process ended with code 0; log `Flush succeeded` on the earlier explicit exit

## User hand tests

1. Close window, type, reopen — counts still rising
2. Tray icon in the overflow area
3. Logon autostart (requires sign-out / reboot)
4. Sleep / wake still counting
