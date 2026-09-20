using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KeyPulse.Core;
using KeyPulse.Core.Statistics;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using MediaFontFamily = System.Windows.Media.FontFamily;
using MediaPen = System.Windows.Media.Pen;
using WpfFlowDirection = System.Windows.FlowDirection;
using WpfPoint = System.Windows.Point;

namespace KeyPulse.App.Services;

public sealed class ReportExportService
{
    public async Task ExportHtmlAsync(ActivityReport report, string path, CancellationToken cancellationToken = default)
    {
        var title = $"KeyPulse 活动报告 · {report.From:yyyy-MM-dd} 至 {report.To:yyyy-MM-dd}";
        var max = Math.Max(1, report.Series.Max(item => item.ActivityCount));
        var bars = string.Join("", report.Series.Select(item =>
            $"<div class='bar-wrap' title='{H(item.Label)} · {item.ActivityCount:N0} 次活动'><div class='bar' style='height:{Math.Max(2, item.ActivityCount * 150.0 / max):0.#}px'></div><span>{H(item.Label)}</span></div>"));
        var html = $$$"""
            <!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <title>{{{H(title)}}}</title><style>
            :root{color-scheme:light dark;--bg:#f6f7f8;--card:#fff;--text:#18202b;--muted:#667080;--line:#dfe3e8;--accent:#4e6e9e;--soft:#e8eef7}
            @media(prefers-color-scheme:dark){:root{--bg:#14171b;--card:#1d2127;--text:#eef1f5;--muted:#aab1bc;--line:#343a43;--soft:#293446}}
            *{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:15px/1.55 system-ui,-apple-system,"Segoe UI",sans-serif}main{max-width:980px;margin:40px auto;padding:0 24px}h1{font-size:28px;margin:0 0 4px}.sub{color:var(--muted);margin-bottom:28px}.cards{display:grid;grid-template-columns:repeat(4,1fr);gap:12px}.card{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:18px}.label{color:var(--muted);font-size:13px}.value{font-size:24px;font-weight:650;margin-top:4px}.change{color:var(--accent);font-size:13px}.section{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:20px;margin-top:16px}h2{font-size:18px;margin:0 0 14px}.chart{height:190px;display:flex;align-items:flex-end;gap:8px;border-bottom:1px solid var(--line);padding:0 4px}.bar-wrap{height:180px;flex:1;min-width:8px;display:flex;flex-direction:column;justify-content:flex-end;align-items:center}.bar{width:min(28px,80%);background:var(--accent);border-radius:4px 4px 0 0}.bar-wrap span{color:var(--muted);font-size:10px;height:22px;margin-top:5px;white-space:nowrap}.facts{display:grid;grid-template-columns:1fr 1fr;gap:16px}.fact{padding:12px 0;border-bottom:1px solid var(--line)}footer{color:var(--muted);font-size:12px;margin:22px 0}
            @media(max-width:720px){.cards{grid-template-columns:1fr 1fr}.facts{grid-template-columns:1fr}.bar-wrap span{display:none}}
            </style></head><body><main><h1>KeyPulse 活动报告</h1><div class="sub">{{{report.From:yyyy-MM-dd}}} 至 {{{report.To:yyyy-MM-dd}}} · 本地生成，不包含输入内容</div>
            <div class="cards"><div class="card"><div class="label">有效使用</div><div class="value">{{{Duration(report.Current.EffectiveActiveSeconds)}}}</div><div class="change">{{{Compare(report.Current.EffectiveActiveSeconds,report.Comparison.EffectiveActiveSeconds)}}}</div></div>
            <div class="card"><div class="label">活动会话</div><div class="value">{{{report.Current.SessionCount:N0}}} 次</div><div class="change">{{{Compare(report.Current.SessionCount,report.Comparison.SessionCount)}}}</div></div>
            <div class="card"><div class="label">按键</div><div class="value">{{{report.Current.KeyPressCount:N0}}}</div><div class="change">{{{Compare(report.Current.KeyPressCount,report.Comparison.KeyPressCount)}}}</div></div>
            <div class="card"><div class="label">鼠标点击</div><div class="value">{{{report.Current.MouseClickCount:N0}}}</div><div class="change">{{{Compare(report.Current.MouseClickCount,report.Comparison.MouseClickCount)}}}</div></div></div>
            <section class="section"><h2>活动变化</h2><div class="chart">{{{bars}}}</div></section>
            <section class="section"><h2>本期摘要</h2><div class="facts"><div class="fact"><div class="label">最常用按键</div><b>{{{H(report.TopKey ?? "暂无")}}} · {{{report.TopKeyCount:N0}}} 次</b></div><div class="fact"><div class="label">有效使用最长的应用</div><b>{{{H(report.TopApp ?? "暂无")}}} · {{{Duration(report.TopAppSeconds)}}}</b></div><div class="fact"><div class="label">滚轮事件</div><b>{{{report.Current.WheelEventCount:N0}}} 次</b></div><div class="fact"><div class="label">鼠标移动</div><b>{{{report.Current.DistancePixels:N0}}} px</b></div></div></section>
            <footer>由 KeyPulse {{{ProductInfo.Version}}} 生成 · 对比区间：{{{report.ComparisonFrom:yyyy-MM-dd}}} 至 {{{report.ComparisonTo:yyyy-MM-dd}}}</footer></main></body></html>
            """;
        await File.WriteAllTextAsync(path, html, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
    }

    public void ExportPng(ActivityReport report, string path)
    {
        const int width = 1400, height = 900;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var bg = new SolidColorBrush(MediaColor.FromRgb(247, 248, 250));
            var card = MediaBrushes.White;
            var text = new SolidColorBrush(MediaColor.FromRgb(25, 32, 43));
            var muted = new SolidColorBrush(MediaColor.FromRgb(101, 112, 128));
            var accent = new SolidColorBrush(MediaColor.FromRgb(78, 110, 158));
            dc.DrawRectangle(bg, null, new Rect(0, 0, width, height));
            DrawText(dc, "KeyPulse 活动报告", 54, 46, 34, text, FontWeights.SemiBold);
            DrawText(dc, $"{report.From:yyyy-MM-dd} 至 {report.To:yyyy-MM-dd} · 本地聚合统计", 54, 96, 17, muted);
            var cards = new[]
            {
                ("有效使用", Duration(report.Current.EffectiveActiveSeconds), Compare(report.Current.EffectiveActiveSeconds, report.Comparison.EffectiveActiveSeconds)),
                ("活动会话", $"{report.Current.SessionCount:N0} 次", Compare(report.Current.SessionCount, report.Comparison.SessionCount)),
                ("按键", report.Current.KeyPressCount.ToString("N0"), Compare(report.Current.KeyPressCount, report.Comparison.KeyPressCount)),
                ("鼠标点击", report.Current.MouseClickCount.ToString("N0"), Compare(report.Current.MouseClickCount, report.Comparison.MouseClickCount))
            };
            for (var i = 0; i < cards.Length; i++)
            {
                var x = 54 + i * 326;
                dc.DrawRoundedRectangle(card, new MediaPen(new SolidColorBrush(MediaColor.FromRgb(224, 228, 234)), 1), new Rect(x, 140, 306, 142), 12, 12);
                DrawText(dc, cards[i].Item1, x + 20, 160, 15, muted);
                DrawText(dc, cards[i].Item2, x + 20, 191, 27, text, FontWeights.SemiBold);
                DrawText(dc, cards[i].Item3, x + 20, 239, 14, accent);
            }
            dc.DrawRoundedRectangle(card, new MediaPen(new SolidColorBrush(MediaColor.FromRgb(224, 228, 234)), 1), new Rect(54, 310, 1290, 370), 12, 12);
            DrawText(dc, "活动变化", 78, 334, 20, text, FontWeights.SemiBold);
            var max = Math.Max(1, report.Series.Max(item => item.ActivityCount));
            var slot = 1230d / Math.Max(1, report.Series.Count);
            for (var i = 0; i < report.Series.Count; i++)
            {
                var item = report.Series[i];
                var barHeight = item.ActivityCount * 245d / max;
                var barWidth = Math.Min(42, slot * .64);
                var x = 84 + slot * i + (slot - barWidth) / 2;
                dc.DrawRoundedRectangle(accent, null, new Rect(x, 620 - barHeight, barWidth, barHeight), 4, 4);
                if (report.Series.Count <= 12 || i % Math.Max(1, report.Series.Count / 8) == 0)
                    DrawText(dc, item.Label, x, 632, 12, muted);
            }
            DrawText(dc, $"最常用按键  {report.TopKey ?? "暂无"} · {report.TopKeyCount:N0} 次", 68, 722, 18, text, FontWeights.SemiBold);
            DrawText(dc, $"有效使用最长应用  {report.TopApp ?? "暂无"} · {Duration(report.TopAppSeconds)}", 68, 764, 18, text, FontWeights.SemiBold);
            DrawText(dc, $"滚轮 {report.Current.WheelEventCount:N0} 次   ·   鼠标移动 {report.Current.DistancePixels:N0} px", 68, 808, 15, muted);
            DrawText(dc, $"由 KeyPulse {ProductInfo.Version} 生成 · 不包含输入内容、屏幕画面或窗口标题", 68, 852, 13, muted);
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static void DrawText(DrawingContext dc, string value, double x, double y, double size, System.Windows.Media.Brush brush, FontWeight? weight = null)
    {
        var formatted = new FormattedText(value, CultureInfo.GetCultureInfo("zh-CN"), WpfFlowDirection.LeftToRight,
            new Typeface(new MediaFontFamily("Microsoft YaHei UI"), FontStyles.Normal, weight ?? FontWeights.Normal, FontStretches.Normal),
            size, brush, 1);
        dc.DrawText(formatted, new WpfPoint(x, y));
    }

    private static string H(string value) => WebUtility.HtmlEncode(value);
    private static string Duration(long seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return span.TotalHours >= 1 ? $"{(int)span.TotalHours} 小时 {span.Minutes} 分钟" : $"{span.Minutes} 分钟";
    }
    private static string Compare(long current, long previous) => previous <= 0
        ? "暂无可比基线"
        : $"较上个同期 {(current / (double)previous - 1) * 100:+0;-0;0}%";
}
