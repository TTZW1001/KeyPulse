# KeyPulse P16 Packaging

Date: 2026-09-15

Implementation commit: `7c450c3` (`chore: add installer and readme`)

## Release configuration

- Target: `win-x64`, self-contained, Release, .NET 8.
- Application version: `1.0.0`.
- Multi-resolution `keypulse.ico` and `tray.ico`: 16, 20, 24, 32, 40, 48, 64, 128, 256 px.
- The application executable embeds `keypulse.ico`; the tray loads `tray.ico` from the publish directory.
- `README.md`, MIT `LICENSE`, Inno Setup script, and `build-release.ps1` are committed.
- Inno Setup 6.7.3 was installed for the current user to compile the installer.

## Generated artifacts

The ignored local `release/` directory contains:

| Artifact | Size | SHA-256 |
|---|---:|---|
| `KeyPulse-Setup-x64.exe` | 58,255,908 bytes | `ed888d765c315e885e4be647e1af3d405302196d97af2eecdf5fe5e2af9c938f` |
| `KeyPulse-portable.zip` | 80,129,440 bytes | `5a50d408d1a341912547493ed8e9efc8178187df026e2510592241afd65a952c` |
| `checksums.txt` | 179 bytes | Contains both hashes above |

The ZIP contains `KeyPulse.exe`, `tray.ico`, and `appsettings.json`; it contains no
`docs/`, `prompt/`, PDB, database, log, or user configuration files.

## Verification

- Release build: 0 warnings, 0 errors.
- Automated tests: 120 passed, 0 failed.
- Published executable PE architecture: AMD64 (`0x8664`).
- Published file/product version: `1.0.0` / `1.0.0+7c450c3...`.
- Installer compile: Inno Setup exit code 0.
- Isolated silent install into `release/install-smoke`: exit code 0; application and uninstaller present.
- Silent uninstall: exit code 0; isolated install directory removed.

## Release notes and limitations

- Artifacts are not code-signed. Windows SmartScreen may warn; users should verify SHA-256.
- A GitHub Release/tag is not created by the build script; repository publishing is a separate authenticated action.
- Sleep/resume, logon startup, Microsoft Pinyin, Explorer restart, and long-run behavior remain user hand tests.
