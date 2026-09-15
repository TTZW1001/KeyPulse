# KeyPulse P10 Mouse

Date: 2026-09-15

## Merge

`IMouseQuery` / `MouseQueryService`:

```
daily_mouse_stats in [from, to]
+
unflushed MouseByDate whose date is in [from, to]
```

Read DB first, then snapshot. ViewModel does not SQL.

筛选：今天 / 最近 7 天（默认 7 天），与键盘页相同。今天不含昨天。

## Distance

Always show full pixels (`32,370 px`).

Converted line only when ≥ 1 m:

- ≥ 1000 m → `约 1.82 km`
- 1 m … 999.9 m → `约 8.6 m`

96 DPI: `meters = px / 96 * 0.0254`. Never label 10 m as km.

## X1 / X2 and horizontal wheel

Hidden until count > 0. Left / Right / Middle and wheel up / down always shown.

## Share

Left / Right / Middle only (not X1/X2). Sum 0 → `暂无` and 0-width bars (no NaN).

## Trend chart

LiveCharts 2.0.5 line, accent `#4E6E9E`.

- X: `M/d` for today-6 … today (always 7 points, including when the summary filter is 今天)
- Y: **点击次数** = Left + Right + Middle + X1 + X2  
  (wheel is in the summary, not the line)

Empty: seven zeros, no crash. Chart redraws on first load, range change, theme change, or Flush. Totals refresh while the mouse page is selected (1 s shell timer).

## Not in V1.0

No screen click heatmap, no cursor trail, no “即将推出” placeholder.
