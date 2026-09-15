# KeyPulse P7 Base UI

Date: 2026-09-15

## Shell

```
┌────────────┬─────────────────────────┐
│ KeyPulse   │  页面标题               │
│ 总览       │                         │
│ 键盘       │  Content                │
│ 鼠标       │                         │
│ 趋势       │                         │
│ 应用       │                         │
│ 设置       │                         │
└────────────┴─────────────────────────┘
```

- Sidebar 200 px, text-only nav, no icon wall
- Selected: muted accent fill + 3 px left bar, no glow
- Default size 980×640, min 800×520, `CanResize`
- P5 Debug 文本块已从默认界面移除；1 秒刷新仍跑，只更新总览数字和状态

## Pages

| Id | Title | P7 content |
|---|---|---|
| Dashboard | 总览 | 今日 4 项 Snapshot 数字（按键 / 点击 / 滚轮 / 距离）+ 状态 + 占位 |
| Keyboard | 键盘 | 占位 |
| Mouse | 鼠标 | 占位 |
| Trends | 趋势 | 占位 |
| Apps | 应用 | 占位 |
| Settings | 设置 | 主题三选一 + 只读数据路径 |

占位语气：

```
暂时还没有图表
统计数据会在后续阶段接到这里。
```

未接 LiveCharts、热力图、应用表、7 日图。开机自启 / 排除应用 / 清空数据仍是 P13。

## Resources

```
src/KeyPulse.App/Resources/Colors.xaml
src/KeyPulse.App/Resources/Typography.xaml
src/KeyPulse.App/Resources/Controls.xaml
src/KeyPulse.App/Resources/DataGrid.xaml
```

页面颜色走 `DynamicResource`。强调色 `#4E6E9E`。

## Theme

`config/settings.json` → `theme`: `Light` | `Dark` | `System`（缺省 `System`）。

`ThemeService` 按枚举替换资源字典中的 Color/Brush；`System` 读 HKCU `Themes\Personalize\AppsUseLightTheme`，并听 `UserPreferenceChanged`。深色时用 DWM `UseImmersiveDarkMode` 同步标题栏。

设置页 Radio 双向绑定 `IsLightTheme` / `IsDarkTheme` / `IsSystemTheme`。

## Lifecycle (unchanged)

- X → 隐藏托盘；采集 / Flush / 单实例 / `--startup` 未拆
- 托盘「打开 KeyPulse」显示主窗口
- 托盘「设置」显示主窗口并切到设置页
- Debug 刷新定时器未停

## Agent live run

- 启动 `KeyPulse.exe`，点过 6 个导航，标题与侧栏一致
- 设置：浅色 / 深色 / 跟随系统可切换；深色写入 `"theme": "Dark"` 后恢复 `System`
- 窗口缩放后侧栏和标题仍在
- X 后进程仍在、窗口不可见；`Local\KeyPulse.RequestExit` 后退出
