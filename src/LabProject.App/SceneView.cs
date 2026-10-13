using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LabProject.Visual;

namespace LabProject.App;

/// <summary>
/// Контрол, который рисует Scene (набор векторных примитивов из Core):
/// масштабирует сцену под размер окна с сохранением пропорций. Все графики,
/// 3D-поверхность и анимации алгоритмов отображаются через него.
/// Перетаскивание мышью сообщается событием Dragged (для поворота 3D-поверхности).
/// </summary>
public sealed class SceneView : FrameworkElement
{
    public static readonly DependencyProperty SceneProperty = DependencyProperty.Register(
        nameof(Scene), typeof(LabProject.Visual.Scene), typeof(SceneView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public LabProject.Visual.Scene? Scene
    {
        get => (LabProject.Visual.Scene?)GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    /// <summary>Сдвиг мыши (в пикселях) при перетаскивании левой кнопкой.</summary>
    public event EventHandler<Vector>? Dragged;

    private Point _lastMouse;

    // ----------------------------- кэши ресурсов рисования ----------------------------- //

    private static readonly Dictionary<Rgba, SolidColorBrush> Brushes_ = new();
    private static readonly Dictionary<(Rgba, double, bool), Pen> Pens = new();
    private static readonly Typeface Regular = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private static readonly Typeface Bold = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    private static SolidColorBrush BrushOf(Rgba c)
    {
        if (!Brushes_.TryGetValue(c, out var b))
        {
            b = new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B));
            b.Freeze();
            Brushes_[c] = b;
        }
        return b;
    }

    private static Pen PenOf(Rgba c, double width, bool dashed = false)
    {
        if (!Pens.TryGetValue((c, width, dashed), out var p))
        {
            p = new Pen(BrushOf(c), width) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            if (dashed)
                p.DashStyle = new DashStyle(new double[] { 4, 3 }, 0);
            p.Freeze();
            Pens[(c, width, dashed)] = p;
        }
        return p;
    }

    // ----------------------------- отрисовка ----------------------------- //

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double w = ActualWidth, h = ActualHeight;
        dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, w, h));
        var scene = Scene;
        if (scene is null || w <= 0 || h <= 0)
            return;

        double scale = Math.Min(w / scene.Width, h / scene.Height);
        double ox = (w - scene.Width * scale) / 2, oy = (h - scene.Height * scale) / 2;
        PaintScene(dc, scene, ox, oy, scale, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

    private static void PaintScene(DrawingContext dc, LabProject.Visual.Scene scene,
        double ox, double oy, double scale, double pixelsPerDip)
    {
        dc.PushTransform(new TranslateTransform(ox, oy));
        dc.PushTransform(new ScaleTransform(scale, scale));
        dc.DrawRectangle(BrushOf(scene.Background), null, new Rect(0, 0, scene.Width, scene.Height));

        foreach (var shape in scene.Shapes)
        {
            switch (shape)
            {
                case RectShape r:
                    if (r.W < 0 || r.H < 0) break;
                    var rect = new Rect(r.X, r.Y, r.W, r.H);
                    var rpen = r.Stroke is Rgba rs ? PenOf(rs, r.StrokeWidth) : null;
                    if (r.Radius > 0)
                        dc.DrawRoundedRectangle(BrushOf(r.Fill), rpen, rect, r.Radius, r.Radius);
                    else
                        dc.DrawRectangle(BrushOf(r.Fill), rpen, rect);
                    break;

                case LineShape l:
                    dc.DrawLine(PenOf(l.Stroke, l.Width, l.Dashed), new Point(l.X1, l.Y1), new Point(l.X2, l.Y2));
                    break;

                case PolylineShape pl:
                    if (pl.Points.Count < 2) break;
                    dc.DrawGeometry(null, PenOf(pl.Stroke, pl.Width), BuildGeometry(pl.Points, closed: false));
                    break;

                case PolygonShape pg:
                    if (pg.Points.Count < 3) break;
                    dc.DrawGeometry(BrushOf(pg.Fill), pg.Stroke is Rgba gs ? PenOf(gs, pg.StrokeWidth) : null,
                        BuildGeometry(pg.Points, closed: true));
                    break;

                case CircleShape c:
                    dc.DrawEllipse(BrushOf(c.Fill), c.Stroke is Rgba cs ? PenOf(cs, c.StrokeWidth) : null,
                        new Point(c.Cx, c.Cy), c.R, c.R);
                    break;

                case TextShape t:
                    DrawText(dc, t, pixelsPerDip * scale);
                    break;
            }
        }
        dc.Pop();
        dc.Pop();
    }

    private static Geometry BuildGeometry(IReadOnlyList<Pt> pts, bool closed)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(pts[0].X, pts[0].Y), isFilled: closed, isClosed: closed);
            var rest = new List<Point>(pts.Count - 1);
            for (int i = 1; i < pts.Count; i++)
                rest.Add(new Point(pts[i].X, pts[i].Y));
            ctx.PolyLineTo(rest, isStroked: true, isSmoothJoin: true);
        }
        geo.Freeze();
        return geo;
    }

    private static void DrawText(DrawingContext dc, TextShape t, double pixelsPerDip)
    {
        var ft = new FormattedText(
            t.Text, CultureInfo.GetCultureInfo("ru-RU"), FlowDirection.LeftToRight,
            t.Bold ? Bold : Regular, t.Size, BrushOf(t.Fill), pixelsPerDip);

        double x = t.Align switch
        {
            TextAlign.Left => t.X,
            TextAlign.Right => t.X - ft.Width,
            _ => t.X - ft.Width / 2,
        };
        var origin = new Point(x, t.Y - ft.Height / 2);

        if (t.Rotation != 0)
        {
            dc.PushTransform(new RotateTransform(t.Rotation, t.X, t.Y));
            dc.DrawText(ft, origin);
            dc.Pop();
        }
        else
        {
            dc.DrawText(ft, origin);
        }
    }

    // ----------------------------- мышь ----------------------------- //

    protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _lastMouse = e.GetPosition(this);
        CaptureMouse();
    }

    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!IsMouseCaptured)
            return;
        var p = e.GetPosition(this);
        var delta = p - _lastMouse;
        _lastMouse = p;
        if (delta.X != 0 || delta.Y != 0)
            Dragged?.Invoke(this, delta);
    }

    protected override void OnMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (IsMouseCaptured)
            ReleaseMouseCapture();
    }

    /// <summary>Плавное проявление (используется при смене вкладки / нового результата).</summary>
    public void FadeIn(int milliseconds = 320)
    {
        var anim = new System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut },
        };
        BeginAnimation(UIElement.OpacityProperty, anim);
    }

    // ----------------------------- экспорт ----------------------------- //

    /// <summary>Сохраняет текущую сцену в PNG (в 2 раза крупнее логического размера).</summary>
    public void SavePng(string path)
    {
        var scene = Scene ?? throw new InvalidOperationException("Нет сцены для сохранения");
        const double factor = 2.0;
        int pw = (int)(scene.Width * factor), ph = (int)(scene.Height * factor);

        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
            PaintScene(dc, scene, 0, 0, factor, 1.0);

        var bmp = new RenderTargetBitmap(pw, ph, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(dv);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(path);
        encoder.Save(fs);
    }
}
