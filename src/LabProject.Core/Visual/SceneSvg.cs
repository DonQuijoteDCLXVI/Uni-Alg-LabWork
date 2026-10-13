using System.Globalization;
using System.Text;

namespace LabProject.Visual;

/// <summary>Экспорт сцены в SVG (удобно для отчёта; также используется в самопроверке).</summary>
public static class SceneSvg
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static string N(double v) => v.ToString("0.##", Inv);

    private static string Col(Rgba c) => c.ToHex();

    private static string Op(string attr, Rgba c) =>
        c.A == 255 ? "" : $" {attr}-opacity=\"{(c.A / 255.0).ToString("0.##", Inv)}\"";

    private static string Pts(IReadOnlyList<Pt> p) =>
        string.Join(" ", p.Select(q => $"{N(q.X)},{N(q.Y)}"));

    private static string Esc(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    public static string ToSvg(Scene scene)
    {
        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{N(scene.Width)}\" height=\"{N(scene.Height)}\" ");
        sb.AppendLine($"viewBox=\"0 0 {N(scene.Width)} {N(scene.Height)}\" font-family=\"Segoe UI, DejaVu Sans, Arial, sans-serif\">");
        sb.AppendLine($"<rect width=\"100%\" height=\"100%\" fill=\"{Col(scene.Background)}\"/>");

        foreach (var shape in scene.Shapes)
        {
            switch (shape)
            {
                case RectShape r:
                    sb.Append($"<rect x=\"{N(r.X)}\" y=\"{N(r.Y)}\" width=\"{N(r.W)}\" height=\"{N(r.H)}\" rx=\"{N(r.Radius)}\" fill=\"{Col(r.Fill)}\"{Op("fill", r.Fill)}");
                    if (r.Stroke is Rgba rs) sb.Append($" stroke=\"{Col(rs)}\" stroke-width=\"{N(r.StrokeWidth)}\"");
                    sb.AppendLine("/>");
                    break;
                case LineShape l:
                    sb.AppendLine($"<line x1=\"{N(l.X1)}\" y1=\"{N(l.Y1)}\" x2=\"{N(l.X2)}\" y2=\"{N(l.Y2)}\" stroke=\"{Col(l.Stroke)}\" stroke-width=\"{N(l.Width)}\"{(l.Dashed ? " stroke-dasharray=\"6 4\"" : "")}/>");
                    break;
                case PolylineShape p:
                    sb.AppendLine($"<polyline points=\"{Pts(p.Points)}\" fill=\"none\" stroke=\"{Col(p.Stroke)}\" stroke-width=\"{N(p.Width)}\" stroke-linejoin=\"round\"/>");
                    break;
                case PolygonShape g:
                    sb.Append($"<polygon points=\"{Pts(g.Points)}\" fill=\"{Col(g.Fill)}\"{Op("fill", g.Fill)}");
                    if (g.Stroke is Rgba gs) sb.Append($" stroke=\"{Col(gs)}\" stroke-width=\"{N(g.StrokeWidth)}\"");
                    sb.AppendLine("/>");
                    break;
                case CircleShape c:
                    sb.Append($"<circle cx=\"{N(c.Cx)}\" cy=\"{N(c.Cy)}\" r=\"{N(c.R)}\" fill=\"{Col(c.Fill)}\"{Op("fill", c.Fill)}");
                    if (c.Stroke is Rgba cs) sb.Append($" stroke=\"{Col(cs)}\" stroke-width=\"{N(c.StrokeWidth)}\"");
                    sb.AppendLine("/>");
                    break;
                case TextShape t:
                    string anchor = t.Align switch { TextAlign.Left => "start", TextAlign.Right => "end", _ => "middle" };
                    string rot = t.Rotation != 0 ? $" transform=\"rotate({N(t.Rotation)} {N(t.X)} {N(t.Y)})\"" : "";
                    sb.AppendLine($"<text x=\"{N(t.X)}\" y=\"{N(t.Y)}\" font-size=\"{N(t.Size)}\" fill=\"{Col(t.Fill)}\" text-anchor=\"{anchor}\" dominant-baseline=\"central\"{(t.Bold ? " font-weight=\"bold\"" : "")}{rot}>{Esc(t.Text)}</text>");
                    break;
            }
        }
        sb.AppendLine("</svg>");
        return sb.ToString();
    }
}
