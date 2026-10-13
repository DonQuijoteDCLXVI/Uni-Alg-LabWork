using System.Globalization;

namespace LabProject.Visual;

/// <summary>Цвет RGBA без зависимости от WPF/GDI - сцены строятся в Core.</summary>
public readonly record struct Rgba(byte R, byte G, byte B, byte A = 255)
{
    public static Rgba Hex(string hex)
    {
        hex = hex.TrimStart('#');
        return new Rgba(
            byte.Parse(hex.AsSpan(0, 2), NumberStyles.HexNumber),
            byte.Parse(hex.AsSpan(2, 2), NumberStyles.HexNumber),
            byte.Parse(hex.AsSpan(4, 2), NumberStyles.HexNumber));
    }

    public Rgba WithAlpha(byte a) => this with { A = a };

    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    public static Rgba Lerp(Rgba a, Rgba b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return new Rgba(
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t));
    }
}

public static class Palette
{
    public static readonly Rgba White = Rgba.Hex("#FFFFFF");
    public static readonly Rgba Ink = Rgba.Hex("#1F2937");
    public static readonly Rgba Muted = Rgba.Hex("#6B7280");
    public static readonly Rgba Border = Rgba.Hex("#D1D5DB");
    public static readonly Rgba Light = Rgba.Hex("#F3F4F6");
    public static readonly Rgba Grid = Rgba.Hex("#E5E7EB");

    public static readonly Rgba Blue = Rgba.Hex("#3B82F6");
    public static readonly Rgba BlueSoft = Rgba.Hex("#DBEAFE");
    public static readonly Rgba Red = Rgba.Hex("#EF4444");
    public static readonly Rgba RedSoft = Rgba.Hex("#FEE2E2");
    public static readonly Rgba Green = Rgba.Hex("#22C55E");
    public static readonly Rgba GreenSoft = Rgba.Hex("#DCFCE7");
    public static readonly Rgba Amber = Rgba.Hex("#F59E0B");
    public static readonly Rgba AmberSoft = Rgba.Hex("#FEF3C7");
    public static readonly Rgba Purple = Rgba.Hex("#8B5CF6");
    public static readonly Rgba PurpleSoft = Rgba.Hex("#EDE9FE");

    /// <summary>Те же цвета серий, что и в исходном проекте (matplotlib).</summary>
    public static readonly Rgba[] Series =
    {
        Rgba.Hex("#1f77b4"), Rgba.Hex("#d62728"), Rgba.Hex("#2ca02c"), Rgba.Hex("#9467bd"),
        Rgba.Hex("#ff7f0e"), Rgba.Hex("#8c564b"), Rgba.Hex("#17becf"), Rgba.Hex("#e377c2"),
    };

    private static readonly Rgba[] ViridisStops =
    {
        Rgba.Hex("#440154"), Rgba.Hex("#3b528b"), Rgba.Hex("#21908d"), Rgba.Hex("#5dc863"), Rgba.Hex("#fde725"),
    };

    public static Rgba Viridis(double t)
    {
        t = Math.Clamp(t, 0, 1) * (ViridisStops.Length - 1);
        int i = Math.Min((int)t, ViridisStops.Length - 2);
        return Rgba.Lerp(ViridisStops[i], ViridisStops[i + 1], t - i);
    }
}

public enum TextAlign { Left, Center, Right }

public readonly record struct Pt(double X, double Y);

public abstract record Shape;

public sealed record RectShape(double X, double Y, double W, double H, Rgba Fill,
    Rgba? Stroke, double StrokeWidth, double Radius) : Shape;

public sealed record LineShape(double X1, double Y1, double X2, double Y2,
    Rgba Stroke, double Width, bool Dashed) : Shape;

public sealed record PolylineShape(IReadOnlyList<Pt> Points, Rgba Stroke, double Width) : Shape;

public sealed record PolygonShape(IReadOnlyList<Pt> Points, Rgba Fill, Rgba? Stroke, double StrokeWidth) : Shape;

public sealed record CircleShape(double Cx, double Cy, double R, Rgba Fill, Rgba? Stroke, double StrokeWidth) : Shape;

/// <summary>Текст; Y - вертикальный ЦЕНТР строки. Rotation - градусы вокруг (X, Y).</summary>
public sealed record TextShape(double X, double Y, string Text, double Size, Rgba Fill,
    TextAlign Align, bool Bold, double Rotation) : Shape;

/// <summary>
/// Сцена - набор векторных примитивов в логических координатах (Width x Height).
/// Все графики и анимации строятся как Scene; WPF-контрол SceneView просто
/// масштабирует её под размер окна и рисует.
/// </summary>
public sealed class Scene
{
    public double Width { get; }
    public double Height { get; }
    public Rgba Background { get; set; } = Palette.White;
    public List<Shape> Shapes { get; } = new();

    public Scene(double width, double height)
    {
        Width = width;
        Height = height;
    }

    public void Rect(double x, double y, double w, double h, Rgba fill,
        Rgba? stroke = null, double strokeWidth = 1, double radius = 0)
        => Shapes.Add(new RectShape(x, y, w, h, fill, stroke, strokeWidth, radius));

    public void Line(double x1, double y1, double x2, double y2, Rgba stroke,
        double width = 1, bool dashed = false)
        => Shapes.Add(new LineShape(x1, y1, x2, y2, stroke, width, dashed));

    public void Polyline(IReadOnlyList<Pt> pts, Rgba stroke, double width = 1)
        => Shapes.Add(new PolylineShape(pts, stroke, width));

    public void Polygon(IReadOnlyList<Pt> pts, Rgba fill, Rgba? stroke = null, double strokeWidth = 1)
        => Shapes.Add(new PolygonShape(pts, fill, stroke, strokeWidth));

    public void Circle(double cx, double cy, double r, Rgba fill, Rgba? stroke = null, double strokeWidth = 1)
        => Shapes.Add(new CircleShape(cx, cy, r, fill, stroke, strokeWidth));

    public void Text(double x, double y, string text, double size, Rgba fill,
        TextAlign align = TextAlign.Center, bool bold = false, double rotation = 0)
        => Shapes.Add(new TextShape(x, y, text, size, fill, align, bold, rotation));

    /// <summary>Грубая оценка ширины текста (для подложек и легенд).</summary>
    public static double EstimateTextWidth(string text, double size) => text.Length * size * 0.56;
}
