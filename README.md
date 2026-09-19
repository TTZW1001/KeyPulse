<p align="center">
  <img src="assets/logo.png" alt="KeyPulse logo" width="128" />
</p>

# KeyPulse

KeyPulse 是一款面向 Windows 的本地键盘与鼠标使用统计工具。它在后台聚合按键次数、
快捷键、鼠标点击、滚轮、真实屏幕像素移动距离、趋势和应用维度统计，并通过简洁的
WPF 界面展示结果。

## 界面预览

<p align="center">
  <img src="assets/screenshots/dashboard.png" alt="KeyPulse 总览页面" width="900" />
</p>

<table>
  <tr>
    <td width="50%" align="center">
      <img src="assets/screenshots/keyboard-heatmap.png" alt="键盘按键热力图" /><br />
      <sub>键盘按键热力图</sub>
    </td>
    <td width="50%" align="center">
      <img src="assets/screenshots/mouse-heatmaps.png" alt="鼠标点击、轨迹与覆盖热力图" /><br />
      <sub>鼠标点击、轨迹与覆盖热力图</sub>
    </td>
  </tr>
  <tr>
    <td width="50%" align="center">
      <img src="assets/screenshots/trends.png" alt="键盘与鼠标趋势页面" /><br />
      <sub>键盘与鼠标趋势</sub>
    </td>
    <td width="50%" align="center">
      <img src="assets/screenshots/app-usage.png" alt="前台应用使用统计" /><br />
      <sub>前台应用使用统计</sub>
    </td>
  </tr>
</table>

<p align="center">
  <img src="assets/screenshots/dark-mode.png" alt="KeyPulse 深色模式" width="900" /><br />
  <sub>深色模式</sub>
</p>

## 隐私边界

KeyPulse 默认只保存聚合后的次数和时长，不记录：

- 输入文本、密码或连续按键序列
- 剪贴板、截图、窗口标题、文件路径或浏览器 URL
- 云端账号、遥测或任何远程上传

统计数据只保存在 `%LOCALAPPDATA%\KeyPulse`。应用维度只保存进程文件名和可选显示名，
设置页可排除指定应用。屏幕位置统计默认关闭；主动开启后，会在本机保存点击坐标的
小时聚合、轨迹密度与累计像素覆盖，不保存截图、窗口标题或逐事件时间线，并可单独清除。

## 功能

- 全局键盘和鼠标 Raw Input 统计
- 紧凑型、87 键和 104 键热力图，主键区与小键盘数字分开统计
- 常用快捷键统计（Ctrl、Alt 或 Win 组合）
- 鼠标按键、点击位置、轨迹密度和累计像素覆盖热力图
- 键盘、鼠标和屏幕热力图支持今天、最近 7 天、最近 30 天和全部范围
- 各页面独立记住上次选择的时间范围，重启后自动恢复
- 屏幕点击、轨迹和累计覆盖可合并导出为一张高分辨率 PNG
- 今日总览、趋势和应用排行
- 可配置 AFK 判定的有效使用时长，以及 15 分钟间隔划分的活动会话
- 周报、月报和最近 12 个月变化，支持同进度对比及 PNG / 离线 HTML 导出
- 点击与拖拽按 Windows 系统阈值区分，拖拽不会污染点击次数或点击热力图
- 今日小结、历史日均对比和个人活动纪录
- 一致性 ZIP 备份/恢复、位置明细保留策略和按日期范围清理
- 可选的每日/每周自动备份、保留份数管理和数据库完整性检查
- 暂停/恢复、托盘常驻、开机静默启动
- CSV 导出和清空统计数据
- 睡眠恢复、Explorer 托盘重建和数据库失败重试
- 浅色、深色和跟随系统主题
- 四套热力图色系，以及本地键盘底图和屏幕覆盖揭示图片

## 安装与使用

发布页提供 `KeyPulse-Setup-x64.exe`：完整的 x64 self-contained 安装包，不要求预装
.NET，可选择简体中文或 English，支持当前用户安装、开始菜单快捷方式和卸载。
检测到旧版本时会明确提示升级；相同版本会提示修复/覆盖安装；更高版本不会被旧安装器降级。
覆盖安装会保留 `%LOCALAPPDATA%\KeyPulse` 中的统计数据、设置和排除列表。

首次启动会显示主窗口；关闭窗口后应用继续在托盘统计。托盘菜单可暂停、打开设置或退出。
开机自启由设置页控制，启用后使用 `--startup` 静默启动。

> Windows 可能对未签名安装包显示 SmartScreen 提示。请在下载后对照 `checksums.txt`
> 验证 SHA-256。KeyPulse 1.3.0 未附带代码签名证书。

## 从源码构建

要求：Windows 10/11 x64、.NET 8 SDK。生成普通 Release 构建：

```powershell
dotnet build KeyPulse.sln -c Release
dotnet test KeyPulse.sln -c Release
```

生成 self-contained 发布目录、安装包和 SHA-256：

```powershell
.\build-release.ps1
```

构建安装包需要 Inno Setup 6；也可传入编译器路径：

```powershell
.\build-release.ps1 -IsccPath "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

输出位于 `release\`，中间 publish 目录位于 `publish\win-x64\`。这两个目录均不提交 Git。

## 已知限制

- 有效使用时长从升级至 1.3.0 后开始统计，不推断或回填旧数据；JSON 导出暂未实现
- 应用前台时长仍来自 1 秒采样，与按 AFK 计算的有效使用时长分别展示
- 物理移动距离根据显示器 DPI 估算；屏幕像素距离使用系统光标坐标计算
- Fn 本身通常不会作为独立按键上报，但系统能识别的音量等功能键仍会作为对应键统计
- 第三方全局热键工具若只吞掉末键按下事件，可由末键松开事件补记快捷键；若按下和松开均被吞掉，应用无法获知该组合
- 睡眠、重启、开机自启、中文输入法、多显示器和长时间运行仍应在目标设备上手工验证

## License

[MIT](LICENSE)
