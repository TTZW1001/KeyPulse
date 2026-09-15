# KeyPulse Environment Check

Checked: 2026-09-15  
Stage: P0 Environment Preparation  
Machine: DESKTOP-H1MLA9K / user `orange`  
Privilege: Administrators group member, **not** currently elevated (Medium Integrity)

## System

- OS: Microsoft Windows 11 专业版 (10.0.26100, Build 26100)
- Architecture: x64 (`AMD64`, 64-bit OS and 64-bit process)
- PowerShell: 7.6.6 (Core) — recommended
- Git: git version 2.47.0.windows.2
- `%LOCALAPPDATA%`: `C:\Users\ORANGE\AppData\Local` — exists and writable
- `project/` writable: YES

## .NET

- `dotnet --version` (default SDK): 9.0.203
- Host: 10.0.10 x64
- .NET 8 SDK: **YES** — `8.0.425 [C:\Program Files\dotnet\sdk]`
- Windows Desktop runtime (WPF): **YES** — `Microsoft.WindowsDesktop.App 8.0.5 / 8.0.15 / 8.0.31`
- WPF template: **YES** — `dotnet new list wpf` lists `wpf` / `wpflib` / `wpfusercontrollib` / `wpfcustomcontrollib`
- WPF net8 create+build: **YES**
  - Command: `dotnet new wpf --framework net8.0` (CLI choice is `net8.0`; generated `TargetFramework` is `net8.0-windows`)
  - Verified in temporary `project/_env_test/`; build succeeded with 0 warnings / 0 errors; directory deleted after verification
- Future project constraint: `TargetFramework = net8.0-windows` (do not switch to .NET 9 / .NET 10 / .NET Framework)
- NuGet:
  - `nuget.org` enabled — `https://api.nuget.org/v3/index.json` (HTTP 200)
  - Visual Studio Offline Packages enabled — `C:\Program Files (x86)\Microsoft SDKs\NuGetPackages\`
  - `Microsoft.Data.Sqlite` is searchable on nuget.org (no package installed in P0)

## Optional Tools

- Visual Studio: Visual Studio Community 2022 17.13.6 (`D:\big\VS\IDE`)
- VS Code: not on PATH
- Cursor: 2.6.18 (`G:\cursor\...`)
- Grok CLI: present (`C:\Users\ORANGE\.grok\bin\grok.exe`)
- Inno Setup: **not installed** (`ISCC.exe` not found; common Inno Setup 6 paths missing)
- DB Browser for SQLite: **not installed**
- `sqlite3` CLI: not found

## Repository

- Git root: `project/` (initialized this stage, branch `main`)
- Workspace root `KeyPulse/` is **not** a Git repository
- remote: `origin` → `https://github.com/TTZW1001/KeyPulse.git` (empty remote, no refs)
- `project/.gitignore`: OK
- `project/docs/`: OK
- `project/assets/`: OK (`logo.png`, `tray-icon.png` copied from workspace `assets/`)
- workspace `docs/` / `prompt/` / `start-grok.bat` not tracked: OK
- Formal Solution **not** created in P0 (`KeyPulse.sln` / `KeyPulse.App` / `KeyPulse.Core` / `KeyPulse.Infrastructure` deferred)

## SQLite

- No database server required or installed
- Planned stack: SQLite + `Microsoft.Data.Sqlite` via NuGet
- MySQL / PostgreSQL / SQL Server / cloud DB: not needed
- Default future data path (not created in P0): `%LOCALAPPDATA%\KeyPulse\data\keypulse.db`

## Document Notes (non-blocking)

Read all five frozen design docs before this check. Minor doc differences, none blocking P0:

1. Staging: `02` uses a coarser 7-phase grouping; `05` defines P0–P16. Follow `05` (more specific implementation plan).
2. Dark mode: PRD lists dark mode as a V1.1 candidate; UI / architecture docs treat Light/Dark/System as V1.0. Follow UI + architecture unless a later stage explicitly defers it.
3. Default `dotnet` SDK on PATH is 9.0.x, but .NET 8 SDK is installed. Later stages must pin `net8.0-windows`; do not retarget to 9/10.

Privacy rules in all five docs agree: count events only; never store input text, key sequences, window titles, URLs, clipboard, screenshots, or raw event history.

## Blocking Issues

- None

## Non-blocking Issues

- Inno Setup not installed — handle in P16 packaging
- DB Browser for SQLite not installed — optional, not required
- VS Code not on PATH — Cursor + Visual Studio 2022 + .NET 8 SDK/CLI are sufficient
- Default `dotnet --version` is 9.0.203 — pin `net8.0-windows` in the future csproj (and optionally `global.json` in a later stage)
- Formal `.ico` not generated yet — P16 can derive from `project/assets/logo.png` / `tray-icon.png`
- `git init.defaultBranch` was unset globally; this repo was created with `-b main`

## Result

Ready for P1: YES
