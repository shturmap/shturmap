using SkiaSharp;

namespace Shturmap.Map;

public static partial class MapRenderer
{
    // ---- the player's extracts this raid, once the game's own list was read (docs/DESIGN.md "Map drawing") ----

    /// <summary>
    /// An extract's triangle by what the game's list said of it this raid: solid and lit when it is on the list, solid
    /// with a dark "?" in it when the list marks it "??:??:??", and struck out when the list leaves it out (owner,
    /// 2026-10-14: "Exits that are marked as not open should be marked better as such"; "Also mark exits that have a ?
    /// better"). Until a list was read, and outside raids, every extract is solid.
    /// </summary>
    private static void DrawExtract(SKCanvas canvas, MapScene scene, ShownMarker shown, float ui, DateTime now, SymbolTrace? trace)
    {
        var (at, r, color) = (shown.At, shown.R, shown.Color);
        using var tri = Triangle(at, r);
        DrawCollar(canvas, tri, ui);
        if (trace is not null)
            trace.Look = shown.NotListed ? ExitLook.NotListed : shown.Unsure ? ExitLook.Unsure : shown.Listed ? ExitLook.Listed : ExitLook.Plain;
        if (shown.NotListed)
        {
            // Not one of the player's ways out this raid: the triangle's outline on the collar's dark, faint, and a
            // stroke through it, the sign for "not available" everywhere. Faint, so the eye goes to the solid ones;
            // still there, since the list could have been read wrong.
            var faint = color.WithAlpha(ClosedAlpha);
            using var ground = new SKPaint { Color = Background.WithAlpha(215), IsAntialias = true };
            using var edge = new SKPaint { Color = faint, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f * ui, StrokeJoin = SKStrokeJoin.Round };
            canvas.DrawPath(tri, ground);
            canvas.DrawPath(tri, edge);
            var (from, to) = Strike(at, r);
            using var band = new SKPaint { Color = Background.WithAlpha(MarkerCollarAlpha), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4.6f * ui, StrokeCap = SKStrokeCap.Round };
            using var line = new SKPaint { Color = faint, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.9f * ui, StrokeCap = SKStrokeCap.Round };
            canvas.DrawLine(from, to, band);
            canvas.DrawLine(from, to, line);
            return;
        }
        using var fill = new SKPaint { Color = color, IsAntialias = true };
        using var outline = new SKPaint { Color = Background, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 * ui };
        canvas.DrawPath(tri, fill);
        canvas.DrawPath(tri, outline);
        // "??:??:??" on the list: maybe open, maybe it needs something. A dark "?" in the triangle, as a quest's glyph
        // is dark on its disc; it pops in as the list just read reaches it.
        var question = shown.Unsure ? QuestionPop(ArrivalOf(scene, shown.Marker, now)) : 0;
        if (trace is not null && shown.Unsure)
            trace.Question = question;
        // Shared by both sides: split down the middle, so it differs from your side's triangle in shape too (green and
        // khaki come within ΔE 7 with red-green colour blindness); above the "?" where there is one.
        if (shown.Marker.Kind == MarkerKind.ExtractShared)
        {
            using var split = new SKPaint { Color = Background, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.2f * ui };
            canvas.DrawLine(at.X, at.Y - r * 0.85f, at.X, at.Y + (question > 0 ? -r * 0.3f : r * 0.8f), split);
        }
        if (question > 0)
        {
            // As large as the triangle holds, and heavier than the labels' type, so it reads at a glance.
            using var font = new SKFont(QuestionFace, r * 1.62f * question);
            using var ink = new SKPaint { Color = Background, IsAntialias = true, Style = SKPaintStyle.StrokeAndFill, StrokeWidth = 0.35f * ui, StrokeJoin = SKStrokeJoin.Round };
            canvas.DrawText(PossibleTag, at.X + 0.2f * ui, at.Y + r * 0.76f, SKTextAlign.Center, font, ink);
        }
    }

    // The "?" in a "??:??:??" extract's triangle: Bahnschrift at its boldest.
    private static readonly SKTypeface QuestionFace =
        SKTypeface.FromFamilyName("Bahnschrift", new SKFontStyle(SKFontStyleWeight.Bold, SKFontStyleWidth.SemiCondensed, SKFontStyleSlant.Upright)) ?? SKTypeface.Default;

    // A closed exit's outline and stroke: faint beside the solid ones.
    private const byte ClosedAlpha = 165;

    // The stroke through a closed exit, from upper left to lower right, a little past the triangle.
    private static (SKPoint From, SKPoint To) Strike(SKPoint at, float r) =>
        (new SKPoint(at.X - r * 1.05f, at.Y - r * 1.15f), new SKPoint(at.X + r * 1.05f, at.Y + r * 0.95f));

    // An extract on the player's list this raid is lit (owner, 2026-10-05: "highlighted better on the map so it can be
    // seen that they are active"): a steady glow in its own colour around the triangle. A glow, since a ring is a
    // pick's and a pulse the pointer's; steady once lit, since nothing moves for long on a second monitor.
    private const float ListedGlowReach = 2.8f;

    /// <param name="strength">1 lit; from 0 to a little more and back to 1 while the list just read lights it.</param>
    internal static void DrawListedGlow(SKCanvas canvas, ShownMarker m, float strength = 1)
    {
        if (strength <= 0)
            return;
        var radius = m.R * ListedGlowReach * (strength > 1 ? 1 + 0.3f * (strength - 1) : 1);
        var a = Math.Min(1, strength);
        using var shader = SKShader.CreateRadialGradient(m.At, radius,
            [m.Color.WithAlpha((byte)(210 * a)), m.Color.WithAlpha((byte)(120 * a)), m.Color.WithAlpha(0)], [0.3f, 0.55f, 1f], SKShaderTileMode.Clamp);
        using var paint = new SKPaint { Shader = shader, IsAntialias = true };
        canvas.DrawCircle(m.At, radius, paint);
    }

    // ---- the list just read: the extracts it names ring out, one after another (owner, 2026-10-14: "animate the
    // icons for a couple of seconds so they are visible pre-attentively when the exits are read") ----

    /// <summary>How far apart the extracts' turns are as a list just read reaches them, nearest you first.</summary>
    public static readonly TimeSpan ExitTurn = TimeSpan.FromMilliseconds(160);

    /// <summary>The turns a list read at once takes: past them, the rest take the last one together, so it stays short.</summary>
    public const int ExitTurns = 6;

    // A lit extract's echoes: its triangle's outline leaving it, three times; one for a "??:??:??" one.
    private static readonly double[] ListedEchoes = [0, 0.5, 1.0];
    private static readonly double[] UnsureEchoes = [0];
    private const double EchoLength = 1.1;

    /// <summary>How long an extract rings out from its turn: a lit one's three echoes, a "??:??:??" one's one.</summary>
    public static TimeSpan RingLength(bool listed) => TimeSpan.FromSeconds((listed ? ListedEchoes : UnsureEchoes)[^1] + EchoLength);

    /// <summary>
    /// Seconds since this extract's turn came as a list just read reached it (negative before it), or null when it
    /// isn't ringing: no list was read just now, animation effects are off, or its ring is over (<see
    /// cref="MapScene.RingExits"/>).
    /// </summary>
    internal static double? ArrivalOf(MapScene scene, MapMarker marker, DateTime now)
    {
        if (!scene.Pulse || !scene.ExitsRinging.TryGetValue(marker.Id, out var ring))
            return null;
        var t = (now - ring.At).TotalSeconds;
        return t < RingLength(ring.Listed).TotalSeconds ? t : null;
    }

    // The glow of a lit extract as the list reaches it: none before its turn, then swelling past its strength and
    // settling.
    private static float GlowAt(double? t) => t switch
    {
        null => 1,
        < 0 => 0,
        < 0.3 => (float)(1.6 * (1 - Math.Pow(1 - t.Value / 0.3, 3))),
        < 1.0 => (float)(1.6 - 0.6 * Eased((t.Value - 0.3) / 0.7)),
        _ => 1,
    };

    // The "?" of a "??:??:??" extract: none before its turn, then popping in a size and a half, and settling.
    private static float QuestionPop(double? t) => t switch
    {
        null => 1,
        < 0 => 0,
        < 0.35 => (float)(Math.Sin(Math.PI * 0.5 * t.Value / 0.35) * (1 + 0.5 * Math.Sin(Math.PI * t.Value / 0.35))),
        _ => 1,
    };

    private static double Eased(double t) => t < 0.5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2;

    /// <summary>
    /// Under every symbol: the glow of the extracts on the player's list, and while a list just read arrives, their
    /// echoes: the triangle's own outline leaving it in its colour on a dark band, as a ping's rings leave the player
    /// and a completed quest's leave its places, in the shape that says "way out" (a pulse's round rings are the
    /// pointer's). Nothing moves with animation effects off: they are simply lit.
    /// </summary>
    private static void DrawExitsLit(SKCanvas canvas, MapScene scene, IReadOnlyList<ShownMarker> markers, float ui, DateTime now,
        Dictionary<ShownMarker, SymbolTrace>? traced)
    {
        foreach (var m in markers.Where(m => m.Listed || m.Unsure))
        {
            var t = ArrivalOf(scene, m.Marker, now);
            var trace = traced is null ? null : Traced(traced, m);
            if (trace is not null)
                trace.Arrival = t;
            if (m.Listed)
            {
                var glow = GlowAt(t);
                DrawListedGlow(canvas, m, glow);
                if (trace is not null)
                    trace.Glow = glow;
            }
            if (t is { } since)
            {
                foreach (var start in m.Listed ? ListedEchoes : UnsureEchoes)
                {
                    var p = (since - start) / EchoLength;
                    if (DrawEcho(canvas, m, p, ui))
                        trace?.EchoesDrawn.Add(p);
                }
            }
        }
    }

    // One echo of an extract's triangle, p of the way out (0 to 1); false when it is out of that range and isn't drawn.
    private static bool DrawEcho(SKCanvas canvas, ShownMarker m, double p, float ui)
    {
        if (p is < 0 or >= 1)
            return false;
        var eased = 1 - Math.Pow(1 - p, 3);
        var fade = (float)Math.Pow(1 - p, 1.5);
        var width = (float)(3.5 - 2 * p) * ui;
        using var echo = Triangle(m.At, m.R + (float)(3 + 24 * eased) * ui);
        using var band = new SKPaint
        {
            Color = Background.WithAlpha((byte)(150 * fade)), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = width + 2.5f * ui,
            StrokeJoin = SKStrokeJoin.Round,
        };
        using var line = new SKPaint
        {
            Color = m.Color.WithAlpha((byte)(255 * fade)), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = width,
            StrokeJoin = SKStrokeJoin.Round,
        };
        canvas.DrawPath(echo, band);
        canvas.DrawPath(echo, line);
        return true;
    }

    // A symbol's record in a developer's trace of the frame (Render), made by whichever drawing reaches it first.
    private static SymbolTrace Traced(Dictionary<ShownMarker, SymbolTrace> traced, ShownMarker shown)
    {
        if (!traced.TryGetValue(shown, out var trace))
            traced[shown] = trace = new SymbolTrace(shown);
        return trace;
    }
}
