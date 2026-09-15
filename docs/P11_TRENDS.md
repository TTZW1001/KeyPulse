# KeyPulse P11 Trends

Date: 2026-09-15

## Range

One ComboBox for the whole page (`TrendRangeResolver`):

| Kind | From | To |
|---|---|---|
| 今日 | today | today |
| 昨日 | today-1 | today-1 |
| 最近 7 天 | today-6 | today |
| 最近 30 天 | today-29 | today |
| 本月 | 1st of this month | today |
| 全部 | earliest `stat_date` (keys ∪ mouse ∪ hourly, plus unflushed dates) | today |
| 自定义 | picked dates | picked dates |

空库的「全部」= 今天一天。

自定义：起 > 止则对调；跨度超过 **366** 天时截成止日前 365 天（含止日共 366 天）。「全部」不截断。

## Merge

`ITrendQuery` / `TrendQueryService`：先 SQLite，再加未 Flush 且日期落在 `[from, to]` 的 Snapshot。缺日补 0。

- `GetDailyAsync`：每天按键 / 点击 / 滚轮
- `GetHourlyAsync`：0–23，区间内各小时 **按键+点击+滚轮** 求和

## Charts

LiveCharts 2.0.5，单色 `#4E6E9E`，Zoom 关闭。

- 键盘 / 鼠标 / 滚轮：按日折线
- 每小时活跃：24 柱
- Tooltip：`M/d · 次数` 或 `HH 时 · 次数`
- 切换范围时旧图保留到新数据到达，顶部显示「正在加载」
- 仅进入趋势页、改范围、Flush、主题变化时拉数（不走 1 秒壳定时器）
