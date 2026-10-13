namespace LabProject.Visual.Animations;

/// <summary>
/// Пошаговая анимация алгоритма: набор кадров (подпись + сцена). Сцены
/// строятся лениво по номеру кадра, поэтому длинные анимации почти ничего
/// не стоят по памяти.
/// </summary>
public sealed class Animation
{
    private readonly Func<int, Scene> _render;

    public Animation(string title, IReadOnlyList<string> captions, Func<int, Scene> render)
    {
        Title = title;
        Captions = captions;
        _render = render;
    }

    public string Title { get; }
    public IReadOnlyList<string> Captions { get; }
    public int Count => Captions.Count;

    public Scene Render(int index) => _render(Math.Clamp(index, 0, Count - 1));
}

/// <summary>Накапливает кадры: Add(подпись, функция-рисовалка).</summary>
internal sealed class AnimationBuilder
{
    private readonly string _title;
    private readonly List<string> _captions = new();
    private readonly List<Action<Scene>> _drawers = new();

    public AnimationBuilder(string title) => _title = title;

    /// <summary>Легенда цветов (рисуется внизу слева на каждом кадре).</summary>
    public (Rgba Color, string Text)[] Legend { get; set; } = Array.Empty<(Rgba, string)>();

    public void Add(string caption, Action<Scene> draw)
    {
        _captions.Add(caption);
        _drawers.Add(draw);
    }

    public Animation Build()
    {
        var captions = _captions.ToList();
        var drawers = _drawers.ToList();
        string title = _title;
        var legend = Legend;
        int count = captions.Count;
        return new Animation(title, captions, i =>
        {
            var scene = Draw.Canvas(title, captions[i]);
            drawers[i](scene);
            double lx = 40;
            foreach (var (color, text) in legend)
            {
                scene.Rect(lx, Draw.H - 24, 14, 14, color, Palette.Border, 1, 4);
                scene.Text(lx + 20, Draw.H - 17, text, 12, Palette.Muted, TextAlign.Left);
                lx += 20 + Scene.EstimateTextWidth(text, 12) + 22;
            }
            scene.Text(Draw.W - 24, Draw.H - 16, $"шаг {i + 1} из {count}", 12, Palette.Muted, TextAlign.Right);
            return scene;
        });
    }
}

internal static class Draw
{
    public const double W = 1000, H = 600;

    public static Scene Canvas(string title, string caption)
    {
        var s = new Scene(W, H);
        s.Rect(22, 18, 6, 28, Palette.Blue, null, 0, 3);
        s.Text(40, 32, title, 22, Palette.Ink, TextAlign.Left, bold: true);
        s.Text(40, 66, caption, 15, Palette.Muted, TextAlign.Left);
        return s;
    }

    public static void Cell(Scene s, double x, double y, double w, double h, string text,
        Rgba fill, Rgba? stroke = null, Rgba? ink = null, double size = 15, bool bold = false)
    {
        s.Rect(x, y, w, h, fill, stroke ?? Palette.Border, stroke is null ? 1.2 : 2.2, 6);
        if (text.Length > 0)
            s.Text(x + w / 2, y + h / 2, text, size, ink ?? Palette.Ink, TextAlign.Center, bold);
    }

    public static string Inf(double v) => double.IsPositiveInfinity(v) ? "∞" : v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    public static string Num(double v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    public static int[] RandomInts(Random rng, int count, int lo, int hi)
    {
        var a = new int[count];
        for (int i = 0; i < count; i++) a[i] = rng.Next(lo, hi + 1);
        return a;
    }
}
