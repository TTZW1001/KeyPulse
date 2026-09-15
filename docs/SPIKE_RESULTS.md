# KeyPulse Spike Results

Date: 2026-09-15  
Solution: `project/spike/Spike.sln`  
TFM: `net8.0-windows` (all three projects)  
Build: `dotnet build Spike.sln -c Release` — 0 Error, 0 Warning

## Spike A Raw Input

- 后台无焦点: 通过
- 键盘: 通过（按下计数；忽略 KeyUp）
- 鼠标按键: 通过（Left / Right / Middle；X1 / X2 本机未触发）
- 滚轮: 通过（WheelUp / WheelDown 分方向）
- Delta 距离: 通过（相对移动 `sqrt(dx²+dy²)` 累加）
- 证据:
  - 实现：隐藏 `HwndSource` + `RIDEV_INPUTSINK`，独立 STA 线程收 `WM_INPUT`。回调只做 parse → increment，不更新 UI、不写库、不打按键日志。
  - 前台注入：`A=5` `Space=3` `Backspace=2` `Left/Right/Middle` 各有计数，`WheelUp=4` `WheelDown=3`。
  - 切到后台后再注入：`foreground=False` 时 `B=4`、`LeftShift=2` 继续增加；鼠标距离与 Move 事件持续累加。
  - 状态快照只含聚合计数（`key.A=5`），不含文本、不含按键顺序。
  - 进程在测试期间 `Responding=True`。

## Spike B IME

- 微软拼音: 未测（需用户在本机用微软拼音输入中文）
- 是否获取文本: 否
- KeyCode 稳定性: 代码路径稳定（VK → `A` / `Space` / `LeftShift` 等内部名）
- 证据:
  - 全仓库 Spike 代码无 `ToUnicode` / `ToUnicodeEx` / IME Composition / 剪贴板 / `GetWindowText`。
  - 运行快照从未出现汉字或组合字符串，只有键名计数。
  - 进程上存在系统默认 IME 窗口（Windows 给任何窗口挂的 `Default IME`），Spike 未读取 composition。

## Spike C Mouse Move

- 测试时长: 121.2 秒合成相对移动（约 1 ms 间隔抖动；另有真实鼠标事件混入）
- CPU: Spike.Input 进程 CPU 时间 +5.30 s / 121 s 墙钟 ≈ 单核 4.4%（高压移动期间，非空闲）
- 内存: Working Set 278 MB → 283 MB（+4 MB / 2 min，未见明显泄漏）
- UI 卡顿: 进程 `Responding=True`；UI tick 与墙钟一致
- 是否逐事件写盘: 否
- 证据:
  - `uiTicks` +119 / 121 s → UI 按 1 秒刷新，不是每个 MouseMove 刷一次。
  - `moveEvents` +8964，`WM_INPUT` +9346，`distance` 9847 → 46430 px。
  - MouseMove 路径不写 SQLite、不写逐事件日志。

## Spike D Tray

- 方案: WinForms `NotifyIcon`（WPF 项目 `UseWindowsForms`）
- 隐藏窗口: 通过（`WM_CLOSE` 后 `visible=False`，进程仍在，`state=hidden`）
- 右键菜单: 代码已接 Open / Exit；菜单点击需用户目视确认
- 双击打开: 代码已接 `NotifyIcon.DoubleClick → Show`；需用户目视确认
- Exit: 通过（退出信号走与菜单相同的 `ExitApp()`，进程 0.41 s 内退出，exit code 0，无残留进程）
- 证据: 使用 `project/assets/tray-icon.png` 缩放到 32×32 作为托盘图标。

## Spike E SQLite

- WAL: 通过（`PRAGMA journal_mode` 返回 `wal`，`spike.db-wal` 存在）
- UPSERT: 通过（A: 10 → 15，增量而非覆盖）
- Transaction: 通过（未提交的 Space+100 回滚后仍为 3）
- 并发读: 通过（写连接 200 次 UPSERT 同时另一连接连续 SELECT；最终 A=215）
- Migration: 通过（`schema_version` 0 → 1，`V001` 创建 `spike_key_stats`）
- 证据:
  - 包：`Microsoft.Data.Sqlite` 8.0.15
  - 库文件：`%TEMP%\KeyPulseSpike\spike.db`（未进 Git）
  - 仅有聚合表 `schema_version` + `spike_key_stats`，无逐事件表。

## 结论

- Raw Input 可作为 P3 主方案: YES
- 进入 P2: YES
- 需要用户讨论: 无阻塞项。以下手测不能由开发 Agent 代替，但不阻塞进入 P2（P3 前应补微软拼音）。

## Agent 已实际运行

- [x] 启动 Spike.Input，键盘 / 鼠标按键 / 滚轮计数增加
- [x] 窗口非前台时继续计数
- [x] 启动 Spike.Tray，隐藏窗口后进程仍在，Exit 真正退出
- [x] 运行 Spike.Sqlite，UPSERT 后读到累计值
- [x] `dotnet build` 0 Error

## 请用户补充手测

1. 微软拼音输入 `nihao` 上屏“你好”时，界面是否只增加 N/I/H/A/O 等按键计数、是否出现汉字内容
2. 全屏游戏或全屏应用中是否还能计数（不便测可记为未测）
3. 托盘图标在任务栏溢出区是否看得清；右键 Open / Exit、双击恢复窗口
4. 连续快速移动鼠标 5～10 分钟，任务管理器中 CPU / 内存是否可接受（Agent 已做 2 分钟合成移动）

运行方式（在 `project/spike`）：

```powershell
dotnet run --project Spike.Input -c Release
dotnet run --project Spike.Tray -c Release
dotnet run --project Spike.Sqlite -c Release
```

## Document notes

无新的设计冲突。P0 已记录的差异（`02` 粗阶段 vs `05` 的 P0–P16；深色模式 PRD vs UI 文档）与本阶段无关，不在 P1 处理。
