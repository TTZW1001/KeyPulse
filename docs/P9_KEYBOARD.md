# KeyPulse P9 Keyboard Heatmap

Date: 2026-09-15

## Layout

ANSI **104** keys (`KeyboardLayoutDefinition.Keys`).

Main cluster + nav (Ins/Home/PgUp, arrows) + numpad. Units: 1u key, wide modifiers, Space 6.25u, NumPad `+` / Enter 2u tall.

Labels are keycap text (`Ctrl`, `Enter`, `←`), not typed characters. No ToUnicode / IME.

## Unmapped key_codes

Counts whose `key_code` is not on the 104 layout (media keys, `VK_xx`, `Unknown`, F13–F24, …) are listed under **其他**, up to 12, by count.

They are still stored in SQLite; they are not dropped.

Numpad Enter and main Enter share the internal name `Enter` (existing KeyMapper). Both keycaps show the same merged count.

## Color

```
t = log(1 + count) / log(1 + max)
```

`max` is the highest count **on the layout** in the current range. `t=0` (including all-zero) → unused fill (light `#E6E6E2` / dark `#2C2C2C`). `t=1` → `#4E6E9E`. Single hue, no rainbow.

Hover: `Space · 3,842 次` (KeyCode + count). Nothing else.

## Query

`IKeyboardQuery.GetKeyCountsAsync(from, to)`:

```
daily_key_stats in [from, to]
+
unflushed KeyCountsByDate whose date is in [from, to]
```

Default range: last 7 days (today-6 … today). Toggle: 今天 / 最近 7 天.

## Agent live run

- Opened 键盘: 104-key heatmap, Space/A darker
- Hover tooltip on a key (`… · N 次`)
- Switched 今天 / 最近 7 天
- Narrow width uses horizontal scroll; keys stay tappable
