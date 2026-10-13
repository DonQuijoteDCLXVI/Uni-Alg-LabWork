using System.Globalization;
using LabProject.Core;

namespace LabProject.Visual;

/// <summary>Одна серия для режима "Сравнить алгоритмы".</summary>
public sealed record PlotSeries(string Name, IReadOnlyList<double> N, IReadOnlyList<double> Y);

/// <summary>
/// 2D-графики (аналог Plot2DCanvas): scatter эмпирических точек + кривая
/// аппроксимации с MSE в легенде; а также несколько серий на одном графике.
/// </summary>
public static class PlotBuilder
{
    public const double W = 1000, H = 640;
    private const double L = 100, R = 40, T = 70, B = 80;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ---------------------------- форматирование ---------------------------- //

    public static string G4(double v) =>
        v.ToString("G4", Inv).Replace("E+", "e+").Replace("E-", "e-").Replace("e+0", "e+").Replace("e-0", "e-");

    public static string FormatTick(double v)
    {
        if (Math.Abs(v) < 1e-300) return "0";
        double a = Math.Abs(v);
        if (a >= 1e6 || a < 1e-3)
            return v.ToString("0.##e+0", Inv);
        return v.ToString("0.######", Inv);
    }

    public static List<double> NiceTicks(double min, double max, int target = 6)
    {
        if (!(max > min)) max = min + 1;
        double raw = (max - min) / target;
        double mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        double norm = raw / mag;
        double step = (norm < 1.5 ? 1 : norm < 3 ? 2 : norm < 7 ? 5 : 10) * mag;
        var ticks = new List<double>();
        for (double v = Math.Ceiling(min / step - 1e-9) * step; v <= max + step * 1e-9; v += step)
            ticks.Add(Math.Round(v / step) * step);
        return ticks;
    }

    // ------------------------------- каркас осей ------------------------------- //

    private sealed class Axes
    {
        public double XMin, XMax, YMin, YMax;
        public double X(double v) => L + (v - XMin) / (XMax - XMin) * (W - L - R);
        public double Y(double v) => H - B - (v - YMin) / (YMax - YMin) * (H - T - B);
    }

    private static Axes DrawFrame(Scene s, double xMin, double xMax, double yMin, double yMax,
        string title, string xLabel, string yLabel)
    {
        if (!(xMax > xMin)) { xMin -= 1; xMax += 1; }
        if (!(yMax > yMin)) { yMax = yMin + 1; }
        var ax = new Axes { XMin = xMin, XMax = xMax, YMin = yMin, YMax = yMax };

        // карточка графика
        s.Rect(L, T, W - L - R, H - T - B, Palette.White, Palette.Border, 1);

        foreach (double ty in NiceTicks(yMin, yMax))
        {
            double y = ax.Y(ty);
            s.Line(L, y, W - R, y, Palette.Grid, 1, dashed: true);
            s.Text(L - 10, y, FormatTick(ty), 12, Palette.Muted, TextAlign.Right);
        }
        foreach (double tx in NiceTicks(xMin, xMax, 8))
        {
            double x = ax.X(tx);
            s.Line(x, T, x, H - B, Palette.Grid, 1, dashed: true);
            s.Text(x, H - B + 18, FormatTick(tx), 12, Palette.Muted);
        }

        s.Text(W / 2, 30, title, 20, Palette.Ink, TextAlign.Center, bold: true);
        s.Text((L + W - R) / 2, H - 24, xLabel, 14, Palette.Ink);
        s.Text(26, (T + H - B) / 2, yLabel, 14, Palette.Ink, TextAlign.Center, false, -90);
        return ax;
    }

    private static void DrawLegend(Scene s, IReadOnlyList<(Rgba Color, string Label, bool Marker)> items)
    {
        if (items.Count == 0) return;
        double width = items.Max(i => Scene.EstimateTextWidth(i.Label, 13)) + 56;
        double height = items.Count * 24 + 12;
        double x = L + 14, y = T + 14;
        s.Rect(x, y, width, height, Palette.White.WithAlpha(235), Palette.Border, 1, 6);
        for (int i = 0; i < items.Count; i++)
        {
            double cy = y + 18 + i * 24;
            if (items[i].Marker)
                s.Circle(x + 20, cy, 5, items[i].Color, Palette.White, 1);
            else
                s.Line(x + 8, cy, x + 32, cy, items[i].Color, 3);
            s.Text(x + 42, cy, items[i].Label, 13, Palette.Ink, TextAlign.Left);
        }
    }

    /// <summary>Рисует ломаную, разбивая её на куски там, где она выходит за область графика.</summary>
    private static void DrawClipped(Scene s, IReadOnlyList<Pt> pts, Rgba color, double width)
    {
        var run = new List<Pt>();
        foreach (var p in pts)
        {
            bool inside = !double.IsNaN(p.Y) && p.Y >= T - 1 && p.Y <= H - B + 1;
            if (inside) run.Add(p);
            else { if (run.Count >= 2) s.Polyline(run.ToList(), color, width); run.Clear(); }
        }
        if (run.Count >= 2) s.Polyline(run, color, width);
    }

    // ------------------------------- публичный API ------------------------------- //

    public static Scene Placeholder(string title = "Запустите эксперимент, чтобы увидеть график",
        string xLabel = "n", string yLabel = "значение метрики")
    {
        var s = new Scene(W, H);
        DrawFrame(s, 0, 1, 0, 1, title, xLabel, yLabel);
        return s;
    }

    /// <summary>
    /// Одна серия: точки замеров + (опционально) кривая C*f(n) и MSE в легенде.
    /// reveal (0..1) - прогресс анимации появления: точки "вырастают" по очереди,
    /// затем прорисовывается кривая аппроксимации; 1 - готовый график.
    /// </summary>
    public static Scene Single(IReadOnlyList<double> nValues, IReadOnlyList<double> values,
        string metricLabel, string title, ApproximationResult? fit = null, double reveal = 1.0)
    {
        var s = new Scene(W, H);
        double xMin = nValues.Min(), xMax = nValues.Max();
        double xPad = (xMax - xMin) * 0.03;
        double yMax = values.Max();
        double yMin = Math.Min(0, values.Min());
        double yPad = (yMax - yMin) * 0.08;
        if (yMax - yMin <= 0) yPad = 1;

        var ax = DrawFrame(s, xMin - xPad, xMax + xPad, yMin, yMax + yPad, title, "n", metricLabel);

        var legend = new List<(Rgba, string, bool)>();

        if (fit is not null && Approximation.ModelFunctions.TryGetValue(fit.ModelName, out var f))
        {
            var curve = new List<Pt>();
            for (int i = 0; i <= 300; i++)
            {
                double n = xMin + (xMax - xMin) * i / 300.0;
                curve.Add(new Pt(ax.X(n), ax.Y(fit.C * f(n))));
            }

            double curveProgress = Math.Clamp((reveal - 0.25) / 0.75, 0, 1);
            int visible = (int)Math.Round(curveProgress * 300) + 1;
            if (curveProgress > 0 && visible >= 2)
            {
                var shown = curve.Take(visible).ToList();

                // мягкая заливка под кривой (значения за пределами графика обрезаются по границам)
                double top = T, bottom = H - B;
                var area = new List<Pt> { new Pt(shown[0].X, bottom) };
                area.AddRange(shown.Select(p => new Pt(p.X, Math.Clamp(double.IsNaN(p.Y) ? bottom : p.Y, top, bottom))));
                area.Add(new Pt(shown[^1].X, bottom));
                s.Polygon(area, Palette.Series[1].WithAlpha(26));

                DrawClipped(s, shown, Palette.Series[1], 2.8);
            }
            legend.Add((Palette.Series[1],
                $"Аппроксимация: {G4(fit.C)}·{fit.ModelName}  (MSE={G4(fit.Mse)})", false));
        }

        int count = nValues.Count;
        for (int i = 0; i < count; i++)
        {
            double appearAt = (double)i / Math.Max(1, count) * 0.7;
            double p = Math.Clamp((reveal - appearAt) / 0.3, 0, 1);
            if (p <= 0) continue;
            double cx = ax.X(nValues[i]), cy = ax.Y(values[i]);
            s.Circle(cx, cy, 10 * p, Palette.Series[0].WithAlpha(40));
            s.Circle(cx, cy, 4.8 * p, Palette.Series[0], Palette.White, 1.4);
        }
        legend.Insert(0, (Palette.Series[0], "Эмпирические данные", true));

        DrawLegend(s, legend);
        return s;
    }

    /// <summary>Несколько серий на одном графике (режим "Сравнить алгоритмы").</summary>
    public static Scene Multiple(IReadOnlyList<PlotSeries> series, string metricLabel, string title)
    {
        var s = new Scene(W, H);
        var all = series.Where(p => p.N.Count > 0).ToList();
        if (all.Count == 0)
            return Placeholder("Нет данных для сравнения", "n", metricLabel);

        double xMin = all.Min(p => p.N.Min()), xMax = all.Max(p => p.N.Max());
        double xPad = (xMax - xMin) * 0.03;
        double yMax = all.Max(p => p.Y.Max());
        double yMin = Math.Min(0, all.Min(p => p.Y.Min()));
        double yPad = (yMax - yMin) * 0.08;
        if (yMax - yMin <= 0) yPad = 1;

        var ax = DrawFrame(s, xMin - xPad, xMax + xPad, yMin, yMax + yPad, title, "n", metricLabel);

        var legend = new List<(Rgba, string, bool)>();
        for (int k = 0; k < all.Count; k++)
        {
            var color = Palette.Series[k % Palette.Series.Length];
            var order = Enumerable.Range(0, all[k].N.Count).OrderBy(i => all[k].N[i]).ToList();
            var pts = order.Select(i => new Pt(ax.X(all[k].N[i]), ax.Y(all[k].Y[i]))).ToList();
            if (pts.Count >= 2) s.Polyline(pts, color, 2);
            foreach (var p in pts) s.Circle(p.X, p.Y, 3.5, color, Palette.White, 1);
            legend.Add((color, all[k].Name, false));
        }
        DrawLegend(s, legend);
        return s;
    }
}
