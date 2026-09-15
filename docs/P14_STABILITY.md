# KeyPulse P14 Stability

Date: 2026-09-15

Implementation commit: `ff6421c` (`fix: harden lifecycle recovery`)

## Lifecycle recovery

- Suspend uses the existing immediate Flush path.
- Resume checks Raw Input state and retries `StartAsync` when the listener is down.
- Resume refreshes the foreground sample and ensures its periodic loop is alive.
- Session ending stops Raw Input and performs a best-effort immediate Flush without showing UI.
- A stopped Raw Input message loop now reports `IsListening == false`, so recovery can detect it.

## Explorer recovery

- A message-only native window listens for the registered `TaskbarCreated` message.
- The existing `NotifyIcon` is hidden and shown again when Explorer recreates the taskbar.
- Recovery does not create another application host, Mutex, or Raw Input listener.

## Persistence and visible errors

- Database initialization and write failures set a visible `无法写入统计数据` status.
- The banner's `恢复` action retries Raw Input and/or an immediate Flush as applicable.
- Failed batches are merged back into the active buffer and retried with backoff.
- If initial database setup failed, initialization is retried before a pending batch is written.
- Subscriber failures after a successful commit cannot merge an already-committed batch back into memory.
- Corrupt `settings.json` and `excluded-apps.json` still fall back to defaults.

## Automated verification

- `dotnet build KeyPulse.sln -c Release --no-restore -m:1`: 0 warnings, 0 errors.
- `dotnet test KeyPulse.sln -c Release --no-build --no-restore -m:1`: 112 passed.
- Tests cover Resume restart, SessionEnding stop/Flush, Suspend Flush, TaskbarCreated routing,
  corrupt configuration fallback, Flush merge/error state, initialization recovery, and committed-batch safety.
- Privacy scan confirms production source still contains neither `GetWindowText` nor `ToUnicode`.

## User hand tests still required

These require a real interactive Windows session and are not marked as agent-tested:

1. Sleep and wake: counts continue increasing after resume.
2. Explorer restart: the existing tray icon returns and is not duplicated.
3. Logon autostart: `--startup` starts silently with only the tray icon.
4. Microsoft Pinyin: the UI/database contains key names and counts only, never composed text.
5. Long run: observe stability and working set over at least 4–8 hours.
