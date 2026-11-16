using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VideoZamanEtudu;

public record StepBar(string Label, double Value, string Class, string Text);
public record StackPart(string Label, double Value, string Class);
public record StackBar(string Label, IReadOnlyList<StackPart> Parts, double Load, string Text);
public record GanttRow(string Label, string Type, string TypeLabel, double Start, double End, string Text);
public record ShareRow(string Label, double Share, double Low, double High, string Class, string Text);
public record CycleBar(string Label, double Value, bool Outlier, string Text);

/// <summary>
/// Analiz grafikleri (tarayıcıdaki Web/js/charts.js ile aynı çizim): adım katkısı yatay çubuk, çevrim süresi dikey çubuk.
/// Renk sınıfa bağlıdır; her çubuğun değeri yazıyla da gösterilir (VA yeşilinin açık zeminde karşıtlığı düşük).
/// </summary>
internal static class Charts
{
    private static readonly Brush Ink = Ui.Brush("#17364a");
    private static readonly Brush Soft = Ui.Brush("#5b7383");
    private static readonly Brush Grid = Ui.Brush("#e3eaee");

    private static TextBlock Label(Canvas canvas, string text, double x, double y, Brush color, double size = 12, TextAlignment align = TextAlignment.Left, double width = 0)
    {
        var t = new TextBlock { Text = text, Foreground = color, FontSize = size };
        if (width > 0) { t.Width = width; t.TextAlignment = align; t.TextTrimming = TextTrimming.CharacterEllipsis; }
        Canvas.SetLeft(t, align == TextAlignment.Right && width > 0 ? x - width : align == TextAlignment.Center && width > 0 ? x - width / 2 : x);
        Canvas.SetTop(t, y);
        canvas.Children.Add(t);
        return t;
    }

    private static void Line(Canvas canvas, double x1, double y1, double x2, double y2, Brush color, double thickness = 1, bool dashed = false)
    {
        var line = new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = color, StrokeThickness = thickness };
        if (dashed) line.StrokeDashArray = new DoubleCollection { 4, 3 };
        canvas.Children.Add(line);
    }

    private static void Bar(Canvas canvas, double x, double y, double w, double h, Brush fill, string tip)
    {
        var rect = new Rectangle { Width = Math.Max(1, w), Height = Math.Max(1, h), Fill = fill, RadiusX = 3, RadiusY = 3, ToolTip = tip };
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        canvas.Children.Add(rect);
    }

    /// <summary>Yatay çubuklar: adım başına standart katkı (sn/çevrim), süreç sırasıyla.</summary>
    public static Canvas StepChart(IReadOnlyList<StepBar> data, double width, Func<string, string> classLabel)
    {
        double row = 26, left = width < 420 ? 96 : 150, right = width < 420 ? 70 : 90, top = 6;
        double height = top + data.Count * row + 34;
        double max = Math.Max(data.Select(d => d.Value).DefaultIfEmpty(0).Max(), 1e-9);
        var canvas = new Canvas { Width = width, Height = height };
        double plot = width - left - right;
        Line(canvas, left, top, left, top + data.Count * row, Grid);
        for (int i = 0; i < data.Count; i++)
        {
            var d = data[i];
            double y = top + i * row, w = Math.Max(2, d.Value / max * plot);
            Label(canvas, d.Label, left - 8, y + 4, Ink, 12, TextAlignment.Right, left - 12);
            Bar(canvas, left, y + 5, w, row - 10, Ui.ClassBrush(d.Class), $"{d.Label} · {d.Text}" + (d.Class != "" ? " · " + classLabel(d.Class) : ""));
            Label(canvas, d.Text, left + w + 6, y + 4, Ink);
        }
        double x = left, ly = top + data.Count * row + 12;
        foreach (var c in new[] { "VA", "SVA", "NVA", "" }.Where(c => data.Any(d => d.Class == c)))
        {
            Bar(canvas, x, ly + 3, 10, 10, Ui.ClassBrush(c), classLabel(c));
            var label = Label(canvas, classLabel(c), x + 14, ly, Soft, 11.5);
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            x += 30 + label.DesiredSize.Width;
        }
        return canvas;
    }

    /// <summary>Dikey çubuklar: çevrim başına ölçülen geçen süre; y ekseni, ortalama çizgisi; %20+ sapanlar turuncu ve ⚠.</summary>
    public static Canvas CycleChart(IReadOnlyList<CycleBar> data, double width, double? mean, string meanLabel, string axis, Func<double, string> format)
    {
        double height = 240, left = 44, right = 12, top = 30, bottom = 34;
        double peak = Math.Max(Math.Max(data.Select(d => d.Value).DefaultIfEmpty(0).Max(), mean ?? 0), 1e-9);
        double step = NiceStep(peak / 4);
        double max = Math.Ceiling(peak * 1.08 / step) * step;
        double plotW = width - left - right, plotH = height - top - bottom;
        double Y(double v) => top + plotH - v / max * plotH;
        var canvas = new Canvas { Width = width, Height = height };
        for (double v = 0; v <= max + 1e-9; v += step)
        {
            Line(canvas, left, Y(v), width - right, Y(v), Grid, v == 0 ? 1 : 0.6);
            Label(canvas, format(v), left - 6, Y(v) - 8, Soft, 10.5, TextAlignment.Right, left - 6);
        }
        double slot = plotW / Math.Max(1, data.Count), bw = Math.Max(4, Math.Min(36, slot - 4));
        int labelEvery = (int)Math.Ceiling(data.Count / 20d);
        bool values = data.Count <= 12 && slot >= 34;
        for (int i = 0; i < data.Count; i++)
        {
            var d = data[i];
            double x = left + i * slot + (slot - bw) / 2;
            Bar(canvas, x, Y(d.Value), bw, top + plotH - Y(d.Value), d.Outlier ? Ui.ClassBrush("NVA") : Ui.ClassBrush("SVA"), $"{axis} {d.Label} · {d.Text}" + (d.Outlier ? " · ⚠" : ""));
            if (i % labelEvery == 0) Label(canvas, d.Label, x + bw / 2, top + plotH + 2, Soft, 11, TextAlignment.Center, Math.Max(slot, 24));
            string tag = (d.Outlier ? "⚠ " : "") + (values ? format(d.Value) : "");
            if (tag.Length > 0) Label(canvas, tag, x + bw / 2, Y(d.Value) - 17, Ink, 10.5, TextAlignment.Center, Math.Max(slot, 40));
        }
        if (mean is double m)
        {
            Line(canvas, left, Y(m), width - right, Y(m), Ink, 1.5, true);
            Line(canvas, width - right - 24, 12, width - right, 12, Ink, 1.5, true);
            Label(canvas, meanLabel, width - right - 30, 4, Ink, 11.5, TextAlignment.Right, 260);
        }
        Label(canvas, axis, left + plotW / 2, height - 18, Soft, 11, TextAlignment.Center, 200);
        return canvas;
    }


    // ------------------------------------------------------------ v2.1

    private static readonly Brush Red = Ui.Brush("#b42318");

    /// <summary>Histogram: eşit genişlikte sınıflar, çubukta kayıt sayısı.</summary>
    public static Canvas HistogramChart(IReadOnlyList<HistogramBin> data, double width, string axis, Func<double, string> format)
    {
        double height = 200, left = 36, right = 12, top = 14, bottom = 38;
        int max = Math.Max(1, data.Select(d => d.Count).DefaultIfEmpty(0).Max());
        double plotW = width - left - right, plotH = height - top - bottom;
        double Y(double v) => top + plotH - v / max * plotH;
        var canvas = new Canvas { Width = width, Height = height };
        for (int v = 0; v <= max; v += Math.Max(1, (int)Math.Ceiling(max / 4d)))
        {
            Line(canvas, left, Y(v), width - right, Y(v), Grid, v == 0 ? 1 : 0.6);
            Label(canvas, v.ToString(), left - 6, Y(v) - 8, Soft, 10.5, TextAlignment.Right, left - 6);
        }
        double bw = plotW / Math.Max(1, data.Count);
        for (int i = 0; i < data.Count; i++)
        {
            var d = data[i];
            double x = left + i * bw;
            Bar(canvas, x + 1, Y(d.Count), Math.Max(1, bw - 2), Math.Max(0, top + plotH - Y(d.Count)), Ui.ClassBrush("SVA"), $"{format(d.From)}–{format(d.To)}: {d.Count}");
            if (d.Count > 0) Label(canvas, d.Count.ToString(), x + bw / 2, Y(d.Count) - 16, Ink, 10.5, TextAlignment.Center, Math.Max(bw, 20));
        }
        var ticks = data.Count > 6 ? new[] { 0, data.Count / 2, data.Count } : Enumerable.Range(0, data.Count + 1).ToArray();
        foreach (int i in ticks)
        {
            double v = i < data.Count ? data[i].From : data[^1].To;
            Label(canvas, format(v), left + i * bw, top + plotH + 2, Soft, 10.5, TextAlignment.Center, 60);
        }
        Label(canvas, axis, left + plotW / 2, height - 18, Soft, 11, TextAlignment.Center, 200);
        return canvas;
    }

    /// <summary>Yamazumi: istasyon başına yığılmış çubuk (sınıf rengi), takt çizgisi.</summary>
    public static Canvas YamazumiChart(IReadOnlyList<StackBar> stations, double width, double? takt, string taktLabel, Func<double, string> format, Func<string, string> classLabel)
    {
        double height = 280, left = 44, right = 12, top = 26, bottom = 40;
        double peak = Math.Max(Math.Max(stations.Select(s => s.Load).DefaultIfEmpty(0).Max(), takt ?? 0), 1e-9);
        double step = NiceStep(peak / 4), max = Math.Ceiling(peak * 1.08 / step) * step;
        double plotW = width - left - right, plotH = height - top - bottom;
        double Y(double v) => top + plotH - v / max * plotH;
        var canvas = new Canvas { Width = width, Height = height };
        for (double v = 0; v <= max + 1e-9; v += step)
        {
            Line(canvas, left, Y(v), width - right, Y(v), Grid, v == 0 ? 1 : 0.6);
            Label(canvas, format(v), left - 6, Y(v) - 8, Soft, 10.5, TextAlignment.Right, left - 6);
        }
        double slot = plotW / Math.Max(1, stations.Count), bw = Math.Max(10, Math.Min(70, slot - 16));
        for (int i = 0; i < stations.Count; i++)
        {
            var s = stations[i];
            double x = left + i * slot + (slot - bw) / 2, acc = 0;
            foreach (var p in s.Parts)
            {
                double y1 = Y(acc + p.Value), y0 = Y(acc);
                Bar(canvas, x, y1, bw, Math.Max(1, y0 - y1 - 2), Ui.ClassBrush(p.Class), $"{s.Label} · {p.Label} · {format(p.Value)}" + (p.Class != "" ? " · " + classLabel(p.Class) : ""));
                if (y0 - y1 > 16 && bw > 44) Label(canvas, p.Label, x + bw / 2, (y0 + y1) / 2 - 8, Brushes.White, 10, TextAlignment.Center, bw - 4);
                acc += p.Value;
            }
            Label(canvas, s.Text, x + bw / 2, Y(acc) - 18, Ink, 11, TextAlignment.Center, Math.Max(slot, 50));
            Label(canvas, s.Label, x + bw / 2, top + plotH + 3, Ink, 11.5, TextAlignment.Center, Math.Max(slot - 4, 40));
        }
        if (takt is double t && t > 0)
        {
            Line(canvas, left, Y(t), width - right, Y(t), Red, 1.6, true);
            Line(canvas, width - right - 24, 12, width - right, 12, Red, 1.6, true);
            Label(canvas, taktLabel, width - right - 30, 4, Ink, 11.5, TextAlignment.Right, 260);
        }
        return canvas;
    }

    /// <summary>Standart iş kombinasyonu: satır başına adım; el işi kalın, makine kesikli, yürüme ve bekleme noktalı; takt çizgisi.</summary>
    public static Canvas WorkChart(IReadOnlyList<GanttRow> rows, double width, double? takt, string taktLabel, double total, Func<double, string> format)
    {
        double row = 24, left = width < 520 ? 100 : 170, right = 60, top = 22, bottom = 30;
        double height = top + rows.Count * row + bottom;
        double peak = Math.Max(Math.Max(total, takt ?? 0), Math.Max(rows.Select(r => r.End).DefaultIfEmpty(0).Max(), 1e-9));
        double step = NiceStep(peak / 5), max = Math.Ceiling(peak * 1.04 / step) * step;
        double plotW = width - left - right;
        double X(double v) => left + v / max * plotW;
        var canvas = new Canvas { Width = width, Height = height };
        for (double v = 0; v <= max + 1e-9; v += step)
        {
            Line(canvas, X(v), top - 4, X(v), top + rows.Count * row, Grid, 0.6);
            Label(canvas, format(v), X(v), top + rows.Count * row + 2, Soft, 10.5, TextAlignment.Center, 50);
        }
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            double y = top + i * row + row / 2;
            Label(canvas, r.Label, left - 8, y - 9, Ink, 11.5, TextAlignment.Right, left - 12);
            var (brush, thick, dash) = r.Type switch
            {
                "Machine" => (Ui.ClassBrush("SVA"), 3d, new DoubleCollection { 2, 1 }),
                "Walk" => (Ui.ClassBrush("NVA"), 4d, new DoubleCollection { 0.6, 0.9 }),
                "Wait" => (Ui.ClassBrush(""), 4d, new DoubleCollection { 0.3, 1.2 }),
                _ => (Ui.ClassBrush("VA"), 7d, (DoubleCollection?)null)
            };
            var line = new Line { X1 = X(r.Start), X2 = Math.Max(X(r.Start) + 2, X(r.End)), Y1 = y, Y2 = y, Stroke = brush, StrokeThickness = thick, ToolTip = $"{r.Label} · {r.TypeLabel} · {format(r.Start)}–{format(r.End)}" };
            if (dash != null) line.StrokeDashArray = dash;
            canvas.Children.Add(line);
            Label(canvas, r.Text, X(r.End) + 5, y - 8, Soft, 10.5);
        }
        if (takt is double t && t > 0)
        {
            Line(canvas, X(t), top - 8, X(t), top + rows.Count * row, Red, 1.6, true);
            Label(canvas, taktLabel, X(t), 0, Red, 11, TextAlignment.Center, 160);
        }
        return canvas;
    }

    /// <summary>Örnekleme: kategori payı yatay çubuk ve güven aralığı çizgisi (0–100).</summary>
    public static Canvas SamplingChart(IReadOnlyList<ShareRow> rows, double width)
    {
        double row = 28, left = width < 420 ? 96 : 160, right = 90, top = 8;
        double height = top + rows.Count * row + 30, plotW = width - left - right;
        double X(double v) => left + v / 100 * plotW;
        var canvas = new Canvas { Width = width, Height = height };
        foreach (var v in new[] { 0d, 25, 50, 75, 100 })
        {
            Line(canvas, X(v), top, X(v), top + rows.Count * row, Grid, 0.6);
            Label(canvas, v.ToString(), X(v), top + rows.Count * row + 2, Soft, 10.5, TextAlignment.Center, 40);
        }
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            double y = top + i * row;
            Label(canvas, r.Label, left - 8, y + 6, Ink, 12, TextAlignment.Right, left - 12);
            Bar(canvas, left, y + 6, Math.Max(1, X(r.Share) - left), row - 12, Ui.ClassBrush(r.Class), $"{r.Label} · {r.Text}");
            Line(canvas, X(r.Low), y + row / 2, X(r.High), y + row / 2, Ink, 1.5);
            foreach (var v in new[] { r.Low, r.High }) Line(canvas, X(v), y + row / 2 - 5, X(v), y + row / 2 + 5, Ink, 1.5);
            Label(canvas, r.Text, Math.Max(X(r.High), X(r.Share)) + 6, y + 6, Ink, 11.5);
        }
        return canvas;
    }

    /// <summary>1, 2, 2.5, 5 × 10^k basamaklarından en yakın yuvarlak adım.</summary>
    private static double NiceStep(double raw)
    {
        if (!(raw > 0)) return 1;
        double p = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        foreach (var m in new[] { 1, 2, 2.5, 5, 10 }) if (raw <= m * p) return m * p;
        return 10 * p;
    }
}
