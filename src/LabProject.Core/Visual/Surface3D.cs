namespace LabProject.Visual;

/// <summary>
/// 3D-поверхность время(n, m) (аналог Plot3DCanvas). Проекция - ортографическая
/// с поворотом (yaw) и наклоном (pitch); грани рисуются от дальних к ближним
/// (алгоритм художника). Углы можно менять мышью - см. SceneView.
/// </summary>
public static class SurfaceBuilder
{
    public const double W = 1000, H = 640;
    public const double DefaultYaw = -0.9, DefaultPitch = 0.55;

    private readonly record struct P3(double X, double Y, double Z, double Raw = 0);

    private static (double Sx, double Sy, double Depth) Project(P3 p, double yaw, double pitch)
    {
        double cy = Math.Cos(yaw), sy = Math.Sin(yaw);
        double xr = p.X * cy - p.Y * sy;
        double yr = p.X * sy + p.Y * cy;
        double cp = Math.Cos(pitch), sp = Math.Sin(pitch);
        double up = p.Z * cp + yr * sp;     // "вверх" на экране
        double depth = yr * cp - p.Z * sp;  // больше - дальше от камеры
        return (xr, -up, depth);
    }

    public static Scene Placeholder(string title = "Запустите эксперимент для умножения матриц")
    {
        var s = new Scene(W, H);
        s.Text(W / 2, 30, title, 20, Palette.Ink, TextAlign.Center, true);
        s.Text(W / 2, H / 2, "3D-поверхность: время(n, m)", 16, Palette.Muted);
        return s;
    }

    public static Scene Build(IReadOnlyList<int> nValues, IReadOnlyList<int> mValues,
        IReadOnlyList<IReadOnlyList<double>> timeGrid, string title,
        double yaw = DefaultYaw, double pitch = DefaultPitch, string zLabel = "время, с", double grow = 1.0)
    {
        if (nValues.Count < 2 || mValues.Count < 2)
        {
            var ph = Placeholder(title);
            ph.Text(W / 2, H / 2 + 34, "Для поверхности нужно минимум 2 значения n и 2 значения m", 14, Palette.Muted);
            return ph;
        }

        pitch = Math.Clamp(pitch, 0.08, 1.45);
        var s = new Scene(W, H);
        s.Text(W / 2, 30, title, 20, Palette.Ink, TextAlign.Center, true);

        double n0 = nValues[0], n1 = nValues[^1], m0 = mValues[0], m1 = mValues[^1];
        double zMin = double.MaxValue, zMax = double.MinValue;
        foreach (var row in timeGrid)
            foreach (double z in row) { zMin = Math.Min(zMin, z); zMax = Math.Max(zMax, z); }
        double zSpan = zMax - zMin > 0 ? zMax - zMin : 1;

        // grow (0..1): "вырастание" поверхности при анимации появления; цвет берётся по настоящему значению
        grow = Math.Clamp(grow, 0.0, 1.0);
        P3 World(int i, int j)
        {
            double raw = (timeGrid[i][j] - zMin) / zSpan;
            return new P3((nValues[i] - n0) / (n1 - n0) * 2 - 1, (mValues[j] - m0) / (m1 - m0) * 2 - 1, raw * grow, raw);
        }

        // --- границы проекции куба [-1,1]x[-1,1]x[0,1] для масштабирования ---
        var corners = new List<(double, double, double)>();
        foreach (double x in new[] { -1.0, 1.0 })
            foreach (double y in new[] { -1.0, 1.0 })
                foreach (double z in new[] { 0.0, 1.0 })
                    corners.Add(Project(new P3(x, y, z), yaw, pitch));
        double minX = corners.Min(c => c.Item1), maxX = corners.Max(c => c.Item1);
        double minY = corners.Min(c => c.Item2), maxY = corners.Max(c => c.Item2);

        const double areaL = 130, areaR = 840, areaT = 90, areaB = 560;
        double scale = Math.Min((areaR - areaL) / (maxX - minX), (areaB - areaT) / (maxY - minY));
        double offX = areaL + ((areaR - areaL) - (maxX - minX) * scale) / 2 - minX * scale;
        double offY = areaT + ((areaB - areaT) - (maxY - minY) * scale) / 2 - minY * scale;

        Pt Screen(P3 p)
        {
            var (sx, sy, _) = Project(p, yaw, pitch);
            return new Pt(offX + sx * scale, offY + sy * scale);
        }

        // --- пол ---
        var floor = new[] { new P3(-1, -1, 0), new P3(1, -1, 0), new P3(1, 1, 0), new P3(-1, 1, 0) };
        s.Polygon(floor.Select(Screen).ToList(), Palette.Light, Palette.Border, 1.2);
        for (int g = 1; g < 4; g++)
        {
            double t = -1 + g * 0.5;
            s.Line(Screen(new P3(t, -1, 0)).X, Screen(new P3(t, -1, 0)).Y, Screen(new P3(t, 1, 0)).X, Screen(new P3(t, 1, 0)).Y, Palette.Grid, 1);
            s.Line(Screen(new P3(-1, t, 0)).X, Screen(new P3(-1, t, 0)).Y, Screen(new P3(1, t, 0)).X, Screen(new P3(1, t, 0)).Y, Palette.Grid, 1);
        }

        // --- грани поверхности (дальние - первыми) ---
        var quads = new List<(double Depth, Pt[] Poly, Rgba Color)>();
        for (int i = 0; i < nValues.Count - 1; i++)
        {
            for (int j = 0; j < mValues.Count - 1; j++)
            {
                var p00 = World(i, j); var p10 = World(i + 1, j);
                var p11 = World(i + 1, j + 1); var p01 = World(i, j + 1);
                double zAvg = (p00.Raw + p10.Raw + p11.Raw + p01.Raw) / 4;
                double depth = (Project(p00, yaw, pitch).Depth + Project(p10, yaw, pitch).Depth +
                                Project(p11, yaw, pitch).Depth + Project(p01, yaw, pitch).Depth) / 4;
                quads.Add((depth, new[] { Screen(p00), Screen(p10), Screen(p11), Screen(p01) }, Palette.Viridis(zAvg)));
            }
        }
        foreach (var q in quads.OrderByDescending(q => q.Depth))
            s.Polygon(q.Poly, q.Color, q.Color, 0.6);

        // --- оси: n и m - по двум ближним рёбрам пола, z - у крайнего левого угла ---
        var floorScreen = floor.Select(p => (P: p, S: Screen(p))).ToList();
        var center = Screen(new P3(0, 0, 0));
        var front = floorScreen.OrderByDescending(c => c.S.Y).First().P;

        void AxisEdge(P3 a, P3 b, double va, double vb, string label)
        {
            var sa = Screen(a); var sb = Screen(b);
            s.Line(sa.X, sa.Y, sb.X, sb.Y, Palette.Ink, 2);
            Pt Out(Pt p, double d)
            {
                double dx = p.X - center.X, dy = p.Y - center.Y;
                double len = Math.Max(1e-6, Math.Sqrt(dx * dx + dy * dy));
                return new Pt(p.X + dx / len * d, p.Y + dy / len * d);
            }
            // подписи концов - вдоль самого ребра наружу (чтобы в общем углу не накладывались)
            Pt Along(Pt from, Pt to, double d)
            {
                double dx = from.X - to.X, dy = from.Y - to.Y;
                double len = Math.Max(1e-6, Math.Sqrt(dx * dx + dy * dy));
                return new Pt(from.X + dx / len * d, from.Y + dy / len * d);
            }
            var ta = Along(sa, sb, 22); var tb = Along(sb, sa, 22);
            s.Text(ta.X, ta.Y, PlotBuilder.FormatTick(va), 12, Palette.Muted);
            s.Text(tb.X, tb.Y, PlotBuilder.FormatTick(vb), 12, Palette.Muted);
            var mid = Out(new Pt((sa.X + sb.X) / 2, (sa.Y + sb.Y) / 2), 38);
            s.Text(mid.X, mid.Y, label, 15, Palette.Ink, TextAlign.Center, true);
        }

        // ребро вдоль n (меняется X) и ребро вдоль m (меняется Y), смежные с ближним углом
        AxisEdge(new P3(-front.X, front.Y, 0), front,
            front.X > 0 ? n0 : n1, front.X > 0 ? n1 : n0, "n");
        AxisEdge(new P3(front.X, -front.Y, 0), front,
            front.Y > 0 ? m0 : m1, front.Y > 0 ? m1 : m0, "m");

        var left = floorScreen.OrderBy(c => c.S.X).First().P;
        var zb = Screen(new P3(left.X, left.Y, 0)); var zt = Screen(new P3(left.X, left.Y, 1));
        s.Line(zb.X, zb.Y, zt.X, zt.Y, Palette.Ink, 2);
        s.Text(zt.X - 10, zt.Y, PlotBuilder.FormatTick(zMax), 12, Palette.Muted, TextAlign.Right);
        s.Text(zt.X - 14, (zb.Y + zt.Y) / 2, zLabel, 14, Palette.Ink, TextAlign.Center, true, -90);

        // --- цветовая шкала ---
        const double cbX = 890, cbTop = 120, cbH = 400, cbW = 22;
        const int strips = 40;
        for (int k = 0; k < strips; k++)
        {
            double t0 = 1 - (k + 1) / (double)strips;
            s.Rect(cbX, cbTop + k * cbH / strips, cbW, cbH / strips + 0.6, Palette.Viridis(t0 + 0.5 / strips));
        }
        s.Rect(cbX, cbTop, cbW, cbH, Palette.White.WithAlpha(0), Palette.Border, 1);
        s.Text(cbX + cbW + 8, cbTop, PlotBuilder.FormatTick(zMax), 12, Palette.Muted, TextAlign.Left);
        s.Text(cbX + cbW + 8, cbTop + cbH, PlotBuilder.FormatTick(zMin), 12, Palette.Muted, TextAlign.Left);
        s.Text(cbX + cbW / 2, cbTop - 18, zLabel, 12, Palette.Ink);

        s.Text(W / 2, H - 18, "Потяните мышью, чтобы повернуть поверхность", 12, Palette.Muted);
        return s;
    }
}
