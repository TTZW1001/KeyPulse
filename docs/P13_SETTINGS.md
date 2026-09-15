# KeyPulse P13 Settings

Date: 2026-09-15

## Layout

Grouped list, no cards:

| Section | Contents |
|---|---|
| 常规 | 开机自动启动；启动后最小化到托盘（只读，始终 `--startup`） |
| 外观 | 主题 浅色 / 深色 / 跟随系统（P7） |
| 排除应用 | 默认 LockApp.exe / LogonUI.exe（不可删）+ 用户项增删；输入或浏览 exe，只存文件名 |
| 数据 | 只读路径；导出 CSV；清空统计数据（二次确认） |
| 关于 | KeyPulse、版本、`仅统计次数，不记录输入内容`、数据根目录 |

Flush 周期保持 **60 秒**，页面有只读说明。**AFK 未做**，不要当成已实现。未做 JSON 导出、数据库备份、清空今日。

## Startup

设置页开关与托盘「开机自启」共用 `IStartupService` / HKCU Run。命令行始终：

```
"<exe>" --startup
```

没有会弹窗的自启项。

## Exclusion

`%LOCALAPPDATA%\KeyPulse\config\excluded-apps.json`。`Exclude(@"C:\...\KeePass.exe")` → `"KeePass.exe"`。默认项 `Remove` 为空操作。

## Export

用户选目录。UTF-8 BOM。文件名带当天日期：

```
keypulse-daily-key-stats-YYYY-MM-DD.csv
keypulse-daily-mouse-stats-YYYY-MM-DD.csv
keypulse-hourly-activity-YYYY-MM-DD.csv
keypulse-daily-app-stats-YYYY-MM-DD.csv
```

应用表列：`stat_date,process_name,display_name,...`。表头不含 title / url / path。导出前 Flush。未在用户库上点导出，用临时目录单测。

## Clear

确认文案：`统计数字会删掉，排除列表可保留。此操作不能撤销。`

持 Flush 锁 → 事务删除四张统计表 + `app_registry` → 清空内存 buffer。排除 JSON 不动。失败弹错误框，不假装成功。未在用户库上点清空，用临时目录单测。

## Live UI

Started `KeyPulse.exe`, opened 设置.

- 开机自启 On → HKCU Run 含 `--startup`，再关回 Off
- 添加 `notepad.exe`（JSON 只有文件名），默认项移除按钮禁用，再移除用户项
- 关于：KeyPulse / `1.0.0-dev` / 隐私句 / `%LOCALAPPDATA%\KeyPulse`

## Tests

`dotnet build` 0 Error. `dotnet test` 100 passed（排除只存文件名、清空后 keys 空且 JSON 仍在、CSV 表头无 title/url/path、自启命令含 `--startup`）。
