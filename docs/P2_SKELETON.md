# KeyPulse P2 Skeleton

Date: 2026-09-15  
Solution: `project/KeyPulse.sln`  
SDK: `global.json` → `8.0.425` / `rollForward: latestFeature`  
Actual `dotnet --version` under this repo: `8.0.425`

## Projects and TFM

| Project | TFM | Notes |
|---|---|---|
| `src/KeyPulse.App` | `net8.0-windows` | WPF exe `KeyPulse.exe` |
| `src/KeyPulse.Core` | `net8.0` | No WPF, no Windows TFM, no package refs |
| `src/KeyPulse.Infrastructure` | `net8.0-windows` | Win32 later; no UseWPF |
| `tests/KeyPulse.Tests` | `net8.0-windows` | Must match Infrastructure TFM |

Tests cannot be `net8.0` while referencing `KeyPulse.Infrastructure` (`net8.0-windows`). Core stays `net8.0` and does not depend on WPF.

## References

```
KeyPulse.App
├─ KeyPulse.Core
└─ KeyPulse.Infrastructure

KeyPulse.Infrastructure
└─ KeyPulse.Core

KeyPulse.Tests
├─ KeyPulse.Core
└─ KeyPulse.Infrastructure
```

Core has no project references. Spike is not in `KeyPulse.sln`. `project/spike/` is unchanged.

## NuGet (P2)

App:

- CommunityToolkit.Mvvm 8.4.0
- Microsoft.Extensions.Configuration.Json 8.0.1
- Microsoft.Extensions.DependencyInjection 8.0.1
- Microsoft.Extensions.Hosting 8.0.1
- Serilog.Extensions.Hosting 8.0.0

Infrastructure:

- Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2
- Microsoft.Extensions.Logging.Abstractions 8.0.2
- Serilog 4.2.0
- Serilog.Sinks.File 6.0.0

Tests: xunit 2.9.2 + Microsoft.NET.Test.Sdk 17.12.0

Deferred (not added): `Microsoft.Data.Sqlite`, LiveCharts, Hardcodet.NotifyIcon.Wpf.

## Logging

Path: `%LOCALAPPDATA%\KeyPulse\logs\keypulse-YYYYMMDD.log`  
Verified: `C:\Users\ORANGE\AppData\Local\KeyPulse\logs\keypulse-20260915.log`

Contains startup / host / stop only. No key names, coordinates, or window titles of other apps.

## Launch

- Window title: `KeyPulse`
- Background: `#F7F7F5`
- Content: product name + “仅统计次数，不记录输入内容”
- Agent started `KeyPulse.exe`, window visible, `WM_CLOSE` exited with code 0
- DI resolved `IAppHost` and `IAppPaths`; Serilog wrote `KeyPulse 1.0.0-dev started` / `KeyPulse stopped`

## Build / test

- `dotnet build KeyPulse.sln -c Release` — 0 Error, 0 Warning
- `dotnet test` — 4 passed
