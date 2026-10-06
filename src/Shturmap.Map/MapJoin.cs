using SkiaSharp;

namespace Shturmap.Map;

// Objectives of one quest on one spot, joined into one marker that says how many (owner, 2026-10-06: "Sometimes you have
// two objectives to do on exactly the same spot … it shows as two icons marking the same spot. I think in these cases we
// should join the two and annotate the icon properly"; chosen from a panel of five ways drawn by the real map: "D,
// count, but make it look nice").
public static partial class MapRenderer
{
    /// <summary>How near (metres, across and up) places of different objectives of one quest lie to be one spot.</summary>
    public const double JoinMetres = 1.5;

    // The tab's height and the padding around its count (pixels, before the display's scale).
    private const float TabHeight = 13, TabPadding = 4;

    /// <summary>Whether two places of a quest's objectives are one spot (<see cref="JoinMetres"/>, across and up).</summary>
    public static bool OneSpot(Shturmap.Core.WorldPoint a, Shturmap.Core.WorldPoint b) =>
        a.HorizontalDistanceTo(b) <= JoinMetres && Math.Abs(a.Y - b.Y) <= JoinMetres;

    // Places of different objectives of one quest on one spot become one marker: an open one stands for them, the others
    // are listed in its Joined, done ones last. Possible places stay apart: a "?" couldn't say which of the joined is only
    // maybe there. Places of one objective were merged already (clusters), and only a lone place joins.
    private static void JoinSpots(List<(int Index, ShownMarker Shown)> result, float ui)
    {
        bool Joins(ShownMarker m) => m.Count == 1 && m.Marker.Kind is MarkerKind.Objective or MarkerKind.ObjectiveDone && ObjectiveOf(m.Marker) is not null;
        result.Sort((a, b) => a.Index.CompareTo(b.Index));
        for (var i = 0; i < result.Count; i++)
        {
            var first = result[i].Shown;
            if (!Joins(first) || first.Joined.Count > 0)
                continue;
            var members = new List<int> { i };
            for (var j = i + 1; j < result.Count; j++)
            {
                var other = result[j].Shown;
                if (Joins(other) && other.Marker.Group == first.Marker.Group && OneSpot(first.Marker.Position, other.Marker.Position)
                    && members.All(k => ObjectiveOf(result[k].Shown.Marker) != ObjectiveOf(other.Marker)))
                    members.Add(j);
            }
            if (members.Count < 2)
                continue;
            // An open one stands for the spot; the done ones go behind it.
            var ordered = members.OrderBy(k => result[k].Shown.Marker.Kind == MarkerKind.ObjectiveDone ? 1 : 0).ThenBy(k => k).ToList();
            var lead = result[ordered[0]];
            var joined = lead.Shown with
            {
                Joined = ordered.Select(k => result[k].Shown.Marker).ToList(),
                // Pointing at any of its objectives points at the spot.
                Pointed = ordered.Any(k => result[k].Shown.Pointed),
            };
            foreach (var k in members.Where(k => k != ordered[0]).OrderByDescending(k => k))
                result.RemoveAt(k);
            result[result.FindIndex(r => r.Index == lead.Index)] = (lead.Index, joined);
        }
    }

    /// <summary>How many of a joined marker's objectives are open: what its tab says.</summary>
    public static int OpenAt(ShownMarker m) => m.Joined.Count(j => j.Kind != MarkerKind.ObjectiveDone);

    // A marker with a tab: two or more open objectives here. With one left open it is a plain marker again (the done
    // ones say nothing more on the map), and with none, a done one.
    private static bool HasTab(ShownMarker m) => OpenAt(m) > 1;

    private static string TabText(ShownMarker m) => OpenAt(m).ToString(System.Globalization.CultureInfo.InvariantCulture);

    // The "×" a little smaller than its figure, so the figure leads.
    private static (float Times, float Figure, float Gap) TabWidths(ShownMarker m, float ui)
    {
        using var times = new SKFont(TypefaceBold, 9 * ui);
        using var figure = new SKFont(TypefaceBold, 11 * ui);
        return (times.MeasureText("×"), figure.MeasureText(TabText(m)), 0.8f * ui);
    }

    /// <summary>The tab at a marker's right, beyond its disc (pixels): where its count stands.</summary>
    private static SKRect TabBox(ShownMarker m, float ui)
    {
        var (times, figure, gap) = TabWidths(m, ui);
        var width = TabPadding * ui + times + gap + figure + TabPadding * ui + TabHeight * ui / 2 * 0.3f;
        return SKRect.Create(m.At.X + m.R - 1 * ui, m.At.Y - TabHeight * ui / 2, width, TabHeight * ui);
    }

    // How far a tab reaches beyond the disc, as the repel sees a symbol: round, so half of it on every side.
    private static float JoinedExtra(ShownMarker m, float ui) => HasTab(m) ? TabBox(m, ui).Width / 2 : 0;

    // The disc and its tab as one shape: the tab's far end rounded, as the disc is.
    private static SKPath TabShape(ShownMarker m, float ui)
    {
        var tab = TabBox(m, ui);
        var body = SKRect.Create(m.At.X, tab.Top, tab.Right - m.At.X, tab.Height);
        using var disc = new SKPath();
        disc.AddCircle(m.At.X, m.At.Y, m.R);
        using var bar = new SKPath();
        bar.AddRoundRect(body, tab.Height / 2, tab.Height / 2);
        return disc.Op(bar, SKPathOp.Union) ?? new SKPath();
    }

    // A quest marker with a tab: the disc with its glyph and, joined to it, how many objectives are open on this spot.
    private static void DrawTabbed(SKCanvas canvas, ShownMarker shown, float ui)
    {
        var (at, r, color) = (shown.At, shown.R, shown.Color);
        using var shape = TabShape(shown, ui);
        DrawCollar(canvas, shape, ui);
        using (var fill = new SKPaint { Color = color, IsAntialias = true })
            canvas.DrawPath(shape, fill);
        using (var outline = new SKPaint { Color = Background, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 * ui })
            canvas.DrawPath(shape, outline);
        // A dark hairline where the tab leaves the disc, so the disc stays a disc.
        using (var seam = new SKPaint { Color = Background.WithAlpha(150), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 * ui })
            canvas.DrawArc(SKRect.Create(at.X - r, at.Y - r, 2 * r, 2 * r), -40, 80, false, seam);
        Glyphs.Draw(canvas, shown.Marker.Objective!.Value, at, r * 1.05f, Background);

        var box = TabBox(shown, ui);
        var (times, _, gap) = TabWidths(shown, ui);
        using var ink = new SKPaint { Color = Background, IsAntialias = true };
        using var timesFont = new SKFont(TypefaceBold, 9 * ui);
        using var figureFont = new SKFont(TypefaceBold, 11 * ui);
        var x = box.Left + TabPadding * ui + 1 * ui;
        var baseline = at.Y + figureFont.Size * 0.36f;
        canvas.DrawText("×", x, baseline - 0.5f * ui, SKTextAlign.Left, timesFont, ink);
        canvas.DrawText(TabText(shown), x + times + gap, baseline, SKTextAlign.Left, figureFont, ink);
    }
}
