using SkiaSharp;
using Shturmap.Core.Maps;

namespace Shturmap.Map;

// The squad grid (owner, 2026-10-26, from a panel of five: E; docs/DESIGN.md §4, "Map drawing", *The grid*): squares of
// 100 m a squad says out loud ("Delta 5-8"), so a teammate's place is found on the map at a glance, without touching it.
// Its lines lie in the artwork's layer, a lighter step of the ground under them with no hue of their own, under every
// symbol, name and route and broken under the names; ticks at the thirds of each line until the keypad's lines fade in
// with the zoom; the columns' letters along the top and bottom and the rows' numbers along the left and right, on strips
// pinned to the view; a small reference at each square's top-left corner, left out where a name or a symbol lies under
// it; and the player's own reference on the marker's tag and large at the map's top left. Laid out with the frame's
// layout (Layout), so once per view and not for every frame of a pulse or a ping. Under the pointer (owner, 2026-10-26),
// the square and its ninth lit, the strips' letter and number, and its reference on the readout's second line: drawn
// from the pointer and that layout, never laid out (GridPointer).
public static partial class MapRenderer
{
    /// <summary>The strips along the top and bottom of the view, with the columns' letters (pixels, before scaling). The
    /// map's own controls step inside them (the window's buttons, floor list, notes and foot; the scale bar, the edge badge
    /// and the chevrons here).</summary>
    public const float GridStripHigh = 18;

    /// <summary>The strips along the left and right of the view, with the rows' numbers, two digits at most (pixels,
    /// before scaling).</summary>
    public const float GridStripWide = 22;

    /// <summary>Where a third of a square is at least this wide on screen (pixels, before scaling), the keypad's lines are
    /// in whole: about 60 px (owner, 2026-10-26). They fade in from <see cref="KeypadFrom"/>, as the player zooms; nothing
    /// else moves. One keypad level only.</summary>
    public const float KeypadAt = 60;

    /// <summary>Where the keypad's lines start to fade in (pixels a third, before scaling), and its ticks to fade out.</summary>
    public const float KeypadFrom = 45;

    // The 100 m lines give way only where the squares are too small to read as squares (pixels, before scaling): below the
    // whole map's view on the widest maps at the smallest window.
    private const float LinesAt = 20, LinesFrom = 12;

    // Ink in the overlay blend: a lighter step of the ground under the line, no hue of its own, so it reads alike on light
    // streets and dark blocks and all but vanishes on the empty ground around a map (the panel's E: on Streets the 100 m
    // lines' median ΔL* about 21 at its view 3, about twice the quiet grid's).
    private const byte GridLineAlpha = 230, GridKeypadAlpha = 90;

    // The keypad's floor on the artwork's drawn map: ink at this strength in the plus blend, six levels lighter whatever the
    // ground (UI QA pass of the squad grid, F6), so on dark ground, where the overlay steps a line up least, its lines still
    // read: on Streets' dark blocks at three times a median ΔL* of 3.7 became 6.6, under the 100 m lines' 8.9 there.
    private const byte GridKeypadFloorAlpha = 7;

    // On the sheet (SheetPanel, L* about 6) the overlay steps the lines up by about 4: there the step is added instead,
    // ink at these strengths in the plus blend, about the same ΔL* (about 20 for the 100 m lines, 7 for the keypad's).
    private const byte SheetLineAlpha = 48, SheetKeypadAlpha = 18;

    // A square's reference: small, in ink at about half strength on a dark halo, so it reads where it is looked for and
    // otherwise stays back. The strips' words stronger: they are what a spoken reference is found by.
    private const byte GridReferenceAlpha = 120, GridStripTextAlpha = 215, GridStripAlpha = 224;
    private const float GridReferenceSize = 10.5f, GridStripTextSize = 11;

    // A square shown narrower or lower than this (pixels, before scaling) carries no reference: two stacked read as one.
    private const float GridReferenceRoom = 30;

    // How far above and below its box a reference keeps from anything placed (pixels, before scaling); beside it, a
    // label's gap (LabelGap).
    private const float GridReferenceMargin = 3;

    // How far a tick reaches each way from its 100 m line (pixels, before scaling).
    private const float GridTickArm = 4;

    /// <summary>The squad grid as one frame draws it, laid out with the frame's layout (<see cref="Layout"/>).</summary>
    /// <param name="Inner">The view inside the four strips.</param>
    /// <param name="Extent">The grid's squares on screen: from A1's top-left corner to the last square's bottom-right.</param>
    /// <param name="Verticals">The 100 m lines across the view: their x, snapped to the pixel's middle.</param>
    /// <param name="Horizontals">The 100 m lines down the view: their y.</param>
    /// <param name="ThirdVerticals">The keypad's lines between them, while they show.</param>
    /// <param name="ThirdHorizontals">The keypad's lines between them, while they show.</param>
    /// <param name="Lines">How strongly the 100 m lines are drawn, 0 to 1.</param>
    /// <param name="Keypad">How far the keypad's lines are in (1) or still ticks (0).</param>
    /// <param name="Letters">Each column's letter, at the middle of its part in view.</param>
    /// <param name="Numbers">Each row's number, at the middle of its part in view.</param>
    /// <param name="References">"D3" at each square's top-left corner in view (its baseline's start).</param>
    /// <param name="LeftOut">The squares whose reference was left out for a name or a symbol under it.</param>
    public sealed record GridLayout(SKRect Inner, SKRect Extent, float[] Verticals, float[] Horizontals, float[] ThirdVerticals,
        float[] ThirdHorizontals, float Lines, float Keypad, IReadOnlyList<(float At, string Text)> Letters,
        IReadOnlyList<(float At, string Text)> Numbers, IReadOnlyList<(SKPoint At, string Text)> References, int LeftOut)
    {
        /// <summary>The player's reference at the map's top left, while a position is shown; null otherwise.</summary>
        public YouReadout? You { get; init; }

        /// <summary>The 100 m lines as drawn: each in view, broken under the names and labels.</summary>
        public IReadOnlyList<(SKPoint From, SKPoint To)> Runs { get; init; } = [];

        /// <summary>The keypad's lines as drawn, while they show at all, broken as the 100 m lines are.</summary>
        public IReadOnlyList<(SKPoint From, SKPoint To)> ThirdRuns { get; init; } = [];

        /// <summary>The ticks at the thirds of each 100 m line, until the keypad's lines are in: each arm, the line's own
        /// pixel left alone; none under a name or a label.</summary>
        public IReadOnlyList<(SKPoint From, SKPoint To)> Ticks { get; init; } = [];
    }

    /// <summary>The player's reference in its fixed place: YOU, the reference large in sand, the age small.</summary>
    /// <param name="Box">Where its plate stands, inside the strips at the map's top left.</param>
    /// <param name="Reference">"D5-8".</param>
    /// <param name="Age">"2 MIN OLD" once the position is a minute old; empty before.</param>
    /// <param name="Stale">From <see cref="PlayerStale"/>: the age said in sand, as the tag is framed in it.</param>
    public sealed record YouReadout(SKRect Box, string Reference, string Age, bool Stale);

    /// <summary>The camera's <see cref="Camera.Inset"/> for a scene: the grid's strips at each side (pixels), so what a view
    /// frames lies inside them; none without the grid.</summary>
    public static SKSize GridInset(MapScene scene, float ui) =>
        scene.ShowGrid ? new SKSize(GridStripWide * ui, GridStripHigh * ui) : SKSize.Empty;

    /// <summary>The room the grid's strips take at the view's edges (pixels): none without the grid.</summary>
    internal static (float Left, float Top, float Right, float Bottom) GridStrips(MapScene scene, float ui) =>
        scene.ShowGrid ? (GridStripWide * ui, GridStripHigh * ui, GridStripWide * ui, GridStripHigh * ui) : (0, 0, 0, 0);

    /// <summary>The view inside the grid's strips (the whole view without the grid): what the map's controls drawn here
    /// keep inside, and where a label may stand.</summary>
    internal static SKRect GridInner(Camera camera, MapScene scene, float ui)
    {
        var (left, top, right, bottom) = GridStrips(scene, ui);
        return new SKRect(left, top, Math.Max(left, camera.Viewport.Width - right), Math.Max(top, camera.Viewport.Height - bottom));
    }

    // The strips themselves, and past them the world outside the view: no label is placed under them, nor where it
    // couldn't be seen beyond them.
    private static IEnumerable<SKRect> StripRects(Camera camera, MapScene scene, float ui)
    {
        if (!scene.ShowGrid)
            yield break;
        const float Far = 100_000;
        var inner = GridInner(camera, scene, ui);
        yield return new SKRect(-Far, -Far, Far, inner.Top);
        yield return new SKRect(-Far, inner.Bottom, Far, Far);
        yield return new SKRect(-Far, inner.Top, inner.Left, inner.Bottom);
        yield return new SKRect(inner.Right, inner.Top, Far, inner.Bottom);
    }

    /// <summary>
    /// The tag beside the player (principle 8, "Position is occasional"): with the grid, the reference from the first
    /// moment ("D5-8") and its age with it once the position is a minute old ("D5-8 · 2 MIN OLD"), larger and framed in
    /// sand from <see cref="PlayerStale"/>; without it, the age alone from a minute, as before. Null while it says nothing.
    /// </summary>
    internal static (string Text, bool Stale)? PlayerTag(MapScene scene, TimeSpan age)
    {
        if (scene.ShowGrid && scene.Player is { } player && scene.Grid.At(player.Position) is { } reference)
        {
            if (age < PlayerOld)
                return (reference.ToString(), false);
            return (age.TotalMinutes < 60
                ? MapTexts.PlayerTagMinutesOld(reference: reference.ToString(), minutes: (int)age.TotalMinutes)
                : MapTexts.PlayerTagHoursOld(reference: reference.ToString(), hours: (int)age.TotalHours), age >= PlayerStale);
        }
        return age >= PlayerOld ? AgeTag(age) : null;
    }

    // The player's reference at the map's top left, inside the strips: its plate's room is taken before any label is placed.
    private static YouReadout? YouOf(Camera camera, MapScene scene, float ui, DateTime now)
    {
        if (!scene.ShowGrid || scene.Player is not { } player || scene.Grid.At(player.Position) is not { } at)
            return null;
        var age = Shturmap.Core.Logs.WallClock.Elapsed(player.At, now);
        var ageText = age >= PlayerOld ? AgeTag(age).Text : "";
        var reference = at.ToString();
        using var small = new SKFont(TypefaceBold, YouSmall * ui);
        using var large = new SKFont(TypefaceBold, YouLarge * ui);
        var width = YouPad * ui + small.MeasureText(MapTexts.MapGridYou) + YouGap * ui + large.MeasureText(reference)
                    + (ageText.Length > 0 ? YouGap * ui + small.MeasureText(ageText) : 0) + YouPad * ui;
        var inner = GridInner(camera, scene, ui);
        return new YouReadout(SKRect.Create(inner.Left + YouInset * ui, inner.Top + YouInset * ui, width, YouHigh * ui), reference, ageText, age >= PlayerStale);
    }

    // The readout's type: the glance's large figure (22) for the reference, the small label size for YOU and the age.
    private const float YouLarge = 22, YouSmall = 11, YouPad = 10, YouGap = 8, YouHigh = 36, YouInset = 8;

    /// <summary>
    /// The grid's lines and words for a frame: the 100 m lines and the keypad's in view, how strongly each shows, the
    /// strips' letters and numbers, and each square's reference where nothing in <paramref name="taken"/> (the frame's
    /// symbols, badges, leaders, labels, names, plates, the scale bar, the player's ring and tag, the readout) lies under it.
    /// </summary>
    /// <param name="breaks">Where the lines break: the map's names and the markers' labels, each a little larger.</param>
    private static GridLayout? GridOf(Camera camera, MapScene scene, float ui, IReadOnlyList<SKRect> taken, IReadOnlyList<SKRect> breaks, YouReadout? you)
    {
        if (!scene.ShowGrid)
            return null;
        var grid = scene.Grid;
        SKPoint At(double u, double v)
        {
            var (x, z) = grid.World(u, v);
            return camera.ToScreen(scene.Projection.ToMap(x, z));
        }

        // The projection stands square on screen (every rotation a multiple of 90°), so a square is dx wide and dy high
        // everywhere: Icebreaker's, whose render is stretched, are oblong.
        var origin = At(grid.OriginU, grid.OriginV);
        var far = At(grid.OriginU + SquadGrid.Square, grid.OriginV + SquadGrid.Square);
        var (dx, dy) = (far.X - origin.X, far.Y - origin.Y);
        if (!(dx > 0.01f) || !(dy > 0.01f))
            return null;
        var (w, h) = (camera.Viewport.Width, camera.Viewport.Height);
        var inner = GridInner(camera, scene, ui);
        var extent = new SKRect(origin.X, origin.Y, origin.X + grid.Columns * dx, origin.Y + grid.Rows * dy);
        var smaller = Math.Min(dx, dy);
        var lines = Math.Clamp((smaller - LinesFrom * ui) / ((LinesAt - LinesFrom) * ui), 0, 1);
        var keypad = Math.Clamp((smaller / 3 - KeypadFrom * ui) / ((KeypadAt - KeypadFrom) * ui), 0, 1);

        // The lines in view, each on its pixel's middle; the keypad's only while they show, without the 100 m ones.
        static float[] Across(float from, float step, int count, int every, float size)
        {
            var found = new List<float>();
            var k0 = Math.Max(0, (int)Math.Ceiling(-from / step - 1e-4));
            var k1 = Math.Min(count, (int)Math.Floor((size - from) / step + 1e-4));
            for (var k = k0; k <= k1; k++)
            {
                if (every > 1 && k % every == 0)
                    continue;
                found.Add(MathF.Floor(from + k * step) + 0.5f);
            }
            return [.. found];
        }

        var verticals = Across(origin.X, dx, grid.Columns, 1, w);
        var horizontals = Across(origin.Y, dy, grid.Rows, 1, h);
        // The thirds: the keypad's lines once in, its ticks before.
        var thirdVerticals = Across(origin.X, dx / 3, grid.Columns * 3, 3, w);
        var thirdHorizontals = Across(origin.Y, dy / 3, grid.Rows * 3, 3, h);

        // Each column's letter and each row's number at the middle of its part inside the strips; one with too little of
        // itself in view has none (two would run into each other).
        static List<(float, string)> Spans(float from, float step, int count, float lo, float hi, float least, float edge, Func<int, string> name)
        {
            var spans = new List<(float, string)>();
            for (var i = 0; i < count; i++)
            {
                var (a, b) = (Math.Max(lo, from + i * step), Math.Min(hi, from + (i + 1) * step));
                if (b - a < least)
                    continue;
                spans.Add((Math.Clamp((a + b) / 2, lo + edge, hi - edge), name(i)));
            }
            return spans;
        }

        var letters = Spans(origin.X, dx, grid.Columns, inner.Left, inner.Right, 10 * ui, 8 * ui, SquadGrid.Letter);
        var numbers = Spans(origin.Y, dy, grid.Rows, inner.Top, inner.Bottom, 10 * ui, 10 * ui,
            i => (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));

        // "D3" at each square's top-left corner, where the square's part inside the strips starts: a square whose corner
        // is out of view keeps its reference at the view's edge. Left out where anything of the frame lies under it.
        var references = new List<(SKPoint, string)>();
        var leftOut = 0;
        var room = GridReferenceRoom * ui;
        if (smaller >= room)
        {
            // What the frame placed, the window's controls over the map with it (MapScene.ControlsOver).
            var obstacles = taken.Where(t => t.IntersectsWith(inner)).ToList();
            using var font = new SKFont(TypefaceBold, GridReferenceSize * ui);
            var c0 = Math.Max(0, (int)Math.Floor((inner.Left - origin.X) / dx));
            var c1 = Math.Min(grid.Columns - 1, (int)Math.Floor((inner.Right - origin.X) / dx));
            var r0 = Math.Max(0, (int)Math.Floor((inner.Top - origin.Y) / dy));
            var r1 = Math.Min(grid.Rows - 1, (int)Math.Floor((inner.Bottom - origin.Y) / dy));
            for (var c = c0; c <= c1; c++)
            {
                for (var r = r0; r <= r1; r++)
                {
                    var cell = new SKRect(origin.X + c * dx, origin.Y + r * dy, origin.X + (c + 1) * dx, origin.Y + (r + 1) * dy);
                    if (!cell.IntersectsWith(inner))
                        continue;
                    cell.Intersect(inner);
                    if (cell.Width < room || cell.Height < room)
                        continue;
                    var text = SquadGrid.Letter(c) + (r + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    var box = new SKRect(cell.Left + 2 * ui, cell.Top + 2 * ui, cell.Left + 6 * ui + font.MeasureText(text), cell.Top + 5 * ui + font.Size);
                    if (box.Right > cell.Right || box.Bottom > cell.Bottom)
                        continue;
                    // Nor where something stands close beside it: a name a few pixels after it on its line read as one with
                    // it ("A8 Crash Site"; UI QA pass of the squad grid, F7), so it keeps a label's gap from all of it.
                    var clear = SKRect.Inflate(box, LabelGap * ui, GridReferenceMargin * ui);
                    if (obstacles.Any(t => t.IntersectsWith(clear)))
                    {
                        leftOut++;
                        continue;
                    }
                    references.Add((new SKPoint(cell.Left + 4 * ui, cell.Top + 3 * ui + font.Size * 0.82f), text));
                }
            }
        }
        // The lines' runs in view, broken under the names and labels: laid out here once per view, so a frame of a pulse or
        // a ping draws them without working out a clip.
        var view = new SKRect(Math.Max(0, extent.Left), Math.Max(0, extent.Top), Math.Min(w, extent.Right), Math.Min(h, extent.Bottom));
        var near = breaks.Where(b => b.IntersectsWith(view)).ToList();
        var runs = new List<(SKPoint, SKPoint)>();
        var thirdRuns = new List<(SKPoint, SKPoint)>();
        var ticks = new List<(SKPoint, SKPoint)>();
        if (lines > 0 && view.Width > 0 && view.Height > 0)
        {
            foreach (var x in verticals)
                Runs(runs, x, view.Top, view.Bottom, true, near);
            foreach (var y in horizontals)
                Runs(runs, y, view.Left, view.Right, false, near);
            if (keypad > 0)
            {
                foreach (var x in thirdVerticals)
                    Runs(thirdRuns, x, view.Top, view.Bottom, true, near);
                foreach (var y in thirdHorizontals)
                    Runs(thirdRuns, y, view.Left, view.Right, false, near);
            }
            if (keypad < 1)
            {
                var arm = MathF.Round(GridTickArm * ui);
                bool Clear(float x, float y) => !near.Any(b => b.Contains(x, y));
                foreach (var x in verticals)
                {
                    foreach (var y in thirdHorizontals)
                    {
                        if (y <= view.Top || y >= view.Bottom || !Clear(x, y))
                            continue;
                        ticks.Add((new SKPoint(x - arm, y), new SKPoint(x - 0.5f, y)));
                        ticks.Add((new SKPoint(x + 0.5f, y), new SKPoint(x + arm, y)));
                    }
                }
                foreach (var y in horizontals)
                {
                    foreach (var x in thirdVerticals)
                    {
                        if (x <= view.Left || x >= view.Right || !Clear(x, y))
                            continue;
                        ticks.Add((new SKPoint(x, y - arm), new SKPoint(x, y - 0.5f)));
                        ticks.Add((new SKPoint(x, y + 0.5f), new SKPoint(x, y + arm)));
                    }
                }
            }
        }
        return new GridLayout(inner, extent, verticals, horizontals, thirdVerticals, thirdHorizontals, lines, keypad, letters, numbers, references, leftOut)
        {
            You = you,
            Runs = runs,
            ThirdRuns = thirdRuns,
            Ticks = ticks,
        };
    }

    // A line's runs from from to to, broken where a box lies across it: at x for a vertical line, at y for a horizontal one.
    private static void Runs(List<(SKPoint, SKPoint)> into, float at, float from, float to, bool vertical, List<SKRect> breaks)
    {
        var cuts = new List<(float A, float B)>();
        foreach (var b in breaks)
        {
            if (vertical ? at >= b.Left && at <= b.Right : at >= b.Top && at <= b.Bottom)
                cuts.Add(vertical ? (b.Top, b.Bottom) : (b.Left, b.Right));
        }
        cuts.Sort((p, q) => p.A.CompareTo(q.A));
        var start = from;
        void Add(float a, float b)
        {
            if (b - a >= 1)
                into.Add(vertical ? (new SKPoint(at, a), new SKPoint(at, b)) : (new SKPoint(a, at), new SKPoint(b, at)));
        }
        foreach (var (a, b) in cuts)
        {
            if (b <= start)
                continue;
            if (a > start)
                Add(start, Math.Min(a, to));
            start = Math.Max(start, b);
            if (start >= to)
                return;
        }
        Add(start, to);
    }

    // The lines in the artwork's layer: drawn before every name, symbol and route, broken under the names and labels (each
    // name's box, a little larger; GridOf lays the runs out), so a line never runs through a word. Then the references.
    private static void DrawGrid(SKCanvas canvas, Camera camera, MapScene scene, MapLayout layout, float ui, bool sheet, GridPointer? pointer)
    {
        if (layout.Grid is not { } grid)
            return;
        // The square under the pointer and its ninth first, under the lines and everything else.
        if (pointer is not null)
            DrawPointerSquare(canvas, camera, scene, grid, pointer, sheet);
        // On the sheet, a panel too dark for the overlay to step it up, the same step is added: ink at a low strength, plus.
        using var paint = new SKPaint { StrokeWidth = 1, IsAntialias = false, Style = SKPaintStyle.Stroke, BlendMode = sheet ? SKBlendMode.Plus : SKBlendMode.Overlay };
        var (lineAlpha, keypadAlpha) = sheet ? (SheetLineAlpha, SheetKeypadAlpha) : (GridLineAlpha, GridKeypadAlpha);
        // One path a kind, so where its lines cross a pixel isn't lightened twice.
        void Draw(IReadOnlyList<(SKPoint From, SKPoint To)> runs, SKPaint with)
        {
            using var builder = new SKPathBuilder();
            foreach (var (from, to) in runs)
            {
                builder.MoveTo(from);
                builder.LineTo(to);
            }
            using var path = builder.Detach();
            canvas.DrawPath(path, with);
        }
        void Stroke(IReadOnlyList<(SKPoint From, SKPoint To)> runs, float alpha)
        {
            if (runs.Count == 0 || alpha < 1)
                return;
            paint.Color = Ink.WithAlpha((byte)Math.Min(255, alpha));
            Draw(runs, paint);
        }

        Stroke(grid.ThirdRuns, keypadAlpha * grid.Keypad * grid.Lines);
        // On the artwork's drawn map the keypad's overlay is joined by a small added step, a floor of lightening where the
        // ground is too dark for the overlay to step it up (UI QA pass of the squad grid, F6); no hue, still a step below the
        // 100 m lines. Not on the empty ground around the map (the artwork's ground, as a hazard keeps to it), where the grid
        // all but vanishes by design and the floor would draw the keypad as strong as the 100 m lines; nor on tiles or the
        // sheet, which has its own added step.
        var floorAlpha = (byte)Math.Round(GridKeypadFloorAlpha * grid.Keypad * grid.Lines);
        if (!sheet && floorAlpha > 0 && grid.ThirdRuns.Count > 0 && GroundShader(camera, scene) is { } ground)
        {
            using (ground)
            {
                using var floor = new SKPaint
                {
                    StrokeWidth = 1, IsAntialias = false, Style = SKPaintStyle.Stroke, BlendMode = SKBlendMode.Plus, Color = Ink.WithAlpha(floorAlpha),
                };
                using var onGround = OnGround(floor, ground);
                Draw(grid.ThirdRuns, floor);
            }
        }
        Stroke(grid.Runs, lineAlpha * grid.Lines);
        // Until the keypad's lines are in, a tick at each third of every 100 m line, each way.
        Stroke(grid.Ticks, lineAlpha * (1 - grid.Keypad) * grid.Lines);
        if (grid.References.Count == 0)
            return;
        using var font = new SKFont(TypefaceBold, GridReferenceSize * ui);
        using var halo = new SKPaint { Color = Background.WithAlpha(150), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f * ui, StrokeJoin = SKStrokeJoin.Round };
        using var text = new SKPaint { Color = Ink.WithAlpha(GridReferenceAlpha), IsAntialias = true };
        foreach (var (at, words) in grid.References)
        {
            canvas.DrawText(words, at.X, at.Y, SKTextAlign.Left, font, halo);
            canvas.DrawText(words, at.X, at.Y, SKTextAlign.Left, font, text);
        }
    }

    // The strips pinned to the view, over everything the map draws: the letters along the top and bottom, the numbers
    // along the left and right, a mark where each 100 m line meets them, the square under the pointer's letter and number
    // lit; then the readout at the top left (DrawReadout).
    private static void DrawGridStrips(SKCanvas canvas, Camera camera, MapScene scene, MapLayout layout, float ui, GridPointer? pointer)
    {
        if (layout.Grid is not { } grid)
            return;
        var (w, h) = (camera.Viewport.Width, camera.Viewport.Height);
        var inner = grid.Inner;
        using var strip = new SKPaint { Color = Background.WithAlpha(GridStripAlpha) };
        canvas.DrawRect(new SKRect(0, 0, w, inner.Top), strip);
        canvas.DrawRect(new SKRect(0, inner.Bottom, w, h), strip);
        canvas.DrawRect(new SKRect(0, inner.Top, inner.Left, inner.Bottom), strip);
        canvas.DrawRect(new SKRect(inner.Right, inner.Top, w, inner.Bottom), strip);
        // Along the square under the pointer, each strip a step lighter (Raised, the selected thing's), as a spreadsheet's
        // header lights up: the column's span on the top and bottom strips, the row's on the left and right.
        if (pointer is not null)
        {
            using var raised = new SKPaint { Color = Palette.Sk(Palette.Raised) };
            var (left, right) = (Math.Max(inner.Left, MathF.Floor(pointer.Square.Left) + 1), Math.Min(inner.Right, MathF.Floor(pointer.Square.Right)));
            var (top, bottom) = (Math.Max(inner.Top, MathF.Floor(pointer.Square.Top) + 1), Math.Min(inner.Bottom, MathF.Floor(pointer.Square.Bottom)));
            if (right > left)
            {
                canvas.DrawRect(new SKRect(left, 0, right, inner.Top), raised);
                canvas.DrawRect(new SKRect(left, inner.Bottom, right, h), raised);
            }
            if (bottom > top)
            {
                canvas.DrawRect(new SKRect(0, top, inner.Left, bottom), raised);
                canvas.DrawRect(new SKRect(inner.Right, top, w, bottom), raised);
            }
        }
        using var edge = new SKPaint { Color = Palette.Sk(Palette.Line), StrokeWidth = 1, Style = SKPaintStyle.Stroke };
        canvas.DrawRect(new SKRect(inner.Left - 0.5f, inner.Top - 0.5f, inner.Right + 0.5f, inner.Bottom + 0.5f), edge);
        using var mark = new SKPaint { Color = Palette.Sk(Palette.LineStrong), StrokeWidth = 1, Style = SKPaintStyle.Stroke };
        var reach = 5 * ui;
        foreach (var x in grid.Verticals)
        {
            if (x <= inner.Left || x >= inner.Right || x < grid.Extent.Left - 1 || x > grid.Extent.Right + 1)
                continue;
            canvas.DrawLine(x, inner.Top - reach, x, inner.Top, mark);
            canvas.DrawLine(x, inner.Bottom, x, inner.Bottom + reach, mark);
        }
        foreach (var y in grid.Horizontals)
        {
            if (y <= inner.Top || y >= inner.Bottom || y < grid.Extent.Top - 1 || y > grid.Extent.Bottom + 1)
                continue;
            canvas.DrawLine(inner.Left - reach, y, inner.Left, y, mark);
            canvas.DrawLine(inner.Right, y, inner.Right + reach, y, mark);
        }
        using var font = new SKFont(TypefaceBold, GridStripTextSize * ui);
        using var ink = new SKPaint { Color = Ink.WithAlpha(GridStripTextAlpha), IsAntialias = true };
        // The column's letter and the row's number of the square under the pointer: the strips' type a step stronger, bold
        // in full ink, as an index map's or a spreadsheet's headers light up; the others as they are.
        using var litFont = new SKFont(TypefaceStrong, GridStripTextSize * ui);
        using var lit = new SKPaint { Color = Ink, IsAntialias = true };
        var (litLetter, litNumber) = pointer is { Reference: var at }
            ? (SquadGrid.Letter(at.Column), at.Row.ToString(System.Globalization.CultureInfo.InvariantCulture))
            : (null, null);
        var cap = font.Size * 0.36f;
        foreach (var (x, letter) in grid.Letters)
        {
            var (face, paint) = letter == litLetter ? (litFont, lit) : (font, ink);
            canvas.DrawText(letter, x, inner.Top / 2 + cap, SKTextAlign.Center, face, paint);
            canvas.DrawText(letter, x, (inner.Bottom + h) / 2 + cap, SKTextAlign.Center, face, paint);
        }
        foreach (var (y, number) in grid.Numbers)
        {
            var (face, paint) = number == litNumber ? (litFont, lit) : (font, ink);
            canvas.DrawText(number, inner.Left / 2, y + cap, SKTextAlign.Center, face, paint);
            canvas.DrawText(number, (inner.Right + w) / 2, y + cap, SKTextAlign.Center, face, paint);
        }
        DrawReadout(canvas, scene, grid, pointer, ui);
    }

    // The readout at the map's top left: YOU's line while a position is shown, and the pointer's under it while the
    // pointer is over the map (alone out of a raid, in YOU's place), on one plate with a LineStrong hairline. A line isn't
    // drawn under a plate or note of the window (MapScene.PlateOver), its room still kept, and the pointer's isn't while
    // the pointer is on it (GridPointer.LineShown).
    private static void DrawReadout(SKCanvas canvas, MapScene scene, GridLayout grid, GridPointer? pointer, float ui)
    {
        var you = grid.You is { } shown && !scene.PlateOver.Any(over => over.IntersectsWith(shown.Box)) ? shown : null;
        var line = pointer is { LineShown: true } ? pointer : null;
        if (you is null && line is null)
            return;
        var box = you is null ? line!.Line
            : line is null ? you.Box
            : new SKRect(you.Box.Left, you.Box.Top, Math.Max(you.Box.Right, line.Line.Right), line.Line.Bottom);
        using var plate = new SKPaint { Color = Background.WithAlpha(235) };
        using var frame = new SKPaint { Color = Palette.Sk(Palette.LineStrong), Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        canvas.DrawRect(box, plate);
        canvas.DrawRect(SKRect.Inflate(box, -0.5f, -0.5f), frame);
        if (you is not null)
            DrawYou(canvas, you, ui);
        if (line is not null)
            DrawPointerLine(canvas, line, ui);
    }

    // YOU, the reference large in sand (the player's colour, as the marker), the age small: muted, in sand once stale.
    private static void DrawYou(SKCanvas canvas, YouReadout you, float ui)
    {
        using var small = new SKFont(TypefaceBold, YouSmall * ui);
        using var large = new SKFont(TypefaceBold, YouLarge * ui);
        using var muted = new SKPaint { Color = Muted, IsAntialias = true };
        using var sand = new SKPaint { Color = Player, IsAntialias = true };
        var baseline = you.Box.MidY + YouLarge * ui * 0.36f;
        var x = you.Box.Left + YouPad * ui;
        canvas.DrawText(MapTexts.MapGridYou, x, baseline, SKTextAlign.Left, small, muted);
        x += small.MeasureText(MapTexts.MapGridYou) + YouGap * ui;
        canvas.DrawText(you.Reference, x, baseline, SKTextAlign.Left, large, sand);
        x += large.MeasureText(you.Reference) + YouGap * ui;
        if (you.Age.Length > 0)
            canvas.DrawText(you.Age, x, baseline, SKTextAlign.Left, small, you.Stale ? sand : muted);
    }

    // ---- the grid under the pointer (owner, 2026-10-26: "When you are with the mouse over the map highlight the current
    // square and sub-square and show the currently mouse-overed position somewhere where it makes sense") ----

    /// <summary>
    /// The square and the ninth under the pointer while it is over the map with the grid on, as a frame draws them
    /// (docs/DESIGN.md §4, "Map drawing", *The grid*, *Under the pointer*): the square lit faintly and its ninth a little
    /// more, the strips' letter and number a step stronger, and its reference on the readout's line at the map's top left.
    /// Made for each frame from <see cref="MapScene.Pointer"/> and the frame's layout, never laid out itself.
    /// </summary>
    /// <param name="Reference">The square and ninth under the pointer: "D5-8".</param>
    /// <param name="Square">The square on screen (pixels), whole; drawn inside the strips.</param>
    /// <param name="Ninth">Its ninth on screen.</param>
    /// <param name="Line">Where the readout's line stands: under YOU while a position is shown, else in YOU's place.</param>
    /// <param name="LineShown">Whether that line is drawn: not while the pointer is on it, where it would cover what the
    /// pointer is on, nor under a plate or note of the window (<see cref="MapScene.PlateOver"/>), as YOU.</param>
    public sealed record GridPointer(GridReference Reference, SKRect Square, SKRect Ninth, SKRect Line, bool LineShown)
    {
        /// <summary>What a frame shows of it: a move of the pointer that keeps both draws nothing anew.</summary>
        public (GridReference Reference, bool LineShown) Shown => (Reference, LineShown);
    }

    // The square under the pointer and its ninth: the grid's own lightening step at a low strength, no hue, as its lines
    // are: Ink in the overlay blend, and on the artwork's drawn map a floor in the plus blend, where the overlay steps a
    // dark ground up least (as the keypad's lines have it); on the sheet the step added (its panel too dark for the
    // overlay). The ninth's on top of the square's.
    private const byte PointerSquareAlpha = 100, PointerNinthAlpha = 90, PointerFloorAlpha = 9, PointerNinthFloorAlpha = 11;
    private const byte SheetPointerSquareAlpha = 22, SheetPointerNinthAlpha = 26;

    // The readout's line: the reference a step under YOU's (the title's 17 to YOU's 22) and in ink, not sand (sand is the
    // player's alone); its label as YOU's.
    private const float PointerLarge = 17, PointerHigh = 30, PointerTuck = 6;

    /// <summary>
    /// The square and ninth under the pointer (<see cref="MapScene.Pointer"/>) as the frame on screen would draw them, from
    /// that frame's layout; null without the grid, with the pointer off the map, over a strip, or off the lettered grid
    /// (left of A, past Z, above row 1: <see cref="SquadGrid.At(double, double)"/>).
    /// </summary>
    public static GridPointer? PointerOf(Camera camera, MapScene scene, float ui) =>
        scene.Pointer is null || !scene.ShowGrid ? null : PointerOf(camera, scene, LayoutOf(camera, scene, ui), ui);

    /// <summary>
    /// Whether a move of the pointer needs a frame: what it would show of the grid under it (the reference, the readout's
    /// line) differs from what the last frame drew (<see cref="MapScene.PointerShown"/>). From the last frame's layout,
    /// never laid out anew: a move inside one ninth draws nothing, and a pointer's move places no label.
    /// </summary>
    public static bool PointerMoved(Camera camera, MapScene scene, float ui)
    {
        if (scene.LastLayout?.Layout is not { } layout)
            return scene.Pointer is not null && scene.ShowGrid;
        return PointerOf(camera, scene, layout, ui)?.Shown != scene.PointerShown;
    }

    private static GridPointer? PointerOf(Camera camera, MapScene scene, MapLayout layout, float ui)
    {
        if (scene.Pointer is not { } at || layout.Grid is not { } grid || !grid.Inner.Contains(at))
            return null;
        var (x, z) = scene.Projection.ToWorld(camera.ToMap(at));
        var squad = scene.Grid;
        if (squad.At(x, z) is not { Keypad: { } keypad } reference)
            return null;
        SKPoint On(double u, double v)
        {
            var (wx, wz) = squad.World(u, v);
            return camera.ToScreen(scene.Projection.ToMap(wx, wz));
        }
        // As GridOf lays the lines out (A1's corner, a square dx by dy, each line from + k · step), so the square's and the
        // ninth's edges fall on the lines' own pixels.
        var origin = On(squad.OriginU, squad.OriginV);
        var far = On(squad.OriginU + SquadGrid.Square, squad.OriginV + SquadGrid.Square);
        var (dx, dy) = (far.X - origin.X, far.Y - origin.Y);
        var (column, row) = (reference.Column, reference.Row - 1);
        var square = new SKRect(origin.X + column * dx, origin.Y + row * dy, origin.X + (column + 1) * dx, origin.Y + (row + 1) * dy);
        var (across, down) = (column * 3 + (keypad - 1) % 3, row * 3 + 2 - (keypad - 1) / 3);
        var (tx, ty) = (dx / 3, dy / 3);
        var ninth = new SKRect(origin.X + across * tx, origin.Y + down * ty, origin.X + (across + 1) * tx, origin.Y + (down + 1) * ty);
        var line = PointerLine(scene, grid, reference.ToString(), ui);
        return new GridPointer(reference, square, ninth, line, !line.Contains(at) && !scene.PlateOver.Any(over => over.IntersectsWith(line)));
    }

    // The readout's line: under YOU's while a position is shown (tucked up a little, so the two read as one readout), else
    // in YOU's place, 8 DIP inside the strips. As wide as the widest reference of the map, so it doesn't change its width
    // as the pointer moves.
    private static SKRect PointerLine(MapScene scene, GridLayout grid, string reference, float ui)
    {
        using var small = new SKFont(TypefaceBold, YouSmall * ui);
        using var large = new SKFont(TypefaceBold, PointerLarge * ui);
        var widest = "W" + new string('8', scene.Grid.Rows.ToString(System.Globalization.CultureInfo.InvariantCulture).Length) + "-8";
        var width = YouPad * ui + small.MeasureText(MapTexts.MapGridPointer) + YouGap * ui
                    + Math.Max(large.MeasureText(reference), large.MeasureText(widest)) + YouPad * ui;
        var top = grid.You is { } you ? you.Box.Bottom - PointerTuck * ui : grid.Inner.Top + YouInset * ui;
        return SKRect.Create(grid.Inner.Left + YouInset * ui, top, width, PointerHigh * ui);
    }

    // POINTER small and muted, the reference a step under YOU's, in ink.
    private static void DrawPointerLine(SKCanvas canvas, GridPointer pointer, float ui)
    {
        using var small = new SKFont(TypefaceBold, YouSmall * ui);
        using var large = new SKFont(TypefaceBold, PointerLarge * ui);
        using var muted = new SKPaint { Color = Muted, IsAntialias = true };
        using var ink = new SKPaint { Color = Ink, IsAntialias = true };
        var line = pointer.Line;
        var baseline = line.MidY + PointerLarge * ui * 0.36f;
        var x = line.Left + YouPad * ui;
        canvas.DrawText(MapTexts.MapGridPointer, x, baseline, SKTextAlign.Left, small, muted);
        x += small.MeasureText(MapTexts.MapGridPointer) + YouGap * ui;
        canvas.DrawText(pointer.Reference.ToString(), x, baseline, SKTextAlign.Left, large, ink);
    }

    // The square and its ninth, inside the strips, each between its lines (a line's own pixel left alone, as the ticks
    // leave it): the square faint, the ninth a little more on top of it.
    private static void DrawPointerSquare(SKCanvas canvas, Camera camera, MapScene scene, GridLayout grid, GridPointer pointer, bool sheet)
    {
        // Between the lines, which lie on their pixel's middle (GridOf): from the pixel after one to the pixel before the next.
        static SKRect Between(SKRect r) => new(MathF.Floor(r.Left) + 1, MathF.Floor(r.Top) + 1, MathF.Floor(r.Right), MathF.Floor(r.Bottom));
        var (square, ninth) = (Between(pointer.Square), Between(pointer.Ninth));
        canvas.Save();
        canvas.ClipRect(grid.Inner);
        using var fill = new SKPaint { IsAntialias = false, BlendMode = sheet ? SKBlendMode.Plus : SKBlendMode.Overlay };
        fill.Color = Ink.WithAlpha(sheet ? SheetPointerSquareAlpha : PointerSquareAlpha);
        canvas.DrawRect(square, fill);
        fill.Color = Ink.WithAlpha(sheet ? SheetPointerNinthAlpha : PointerNinthAlpha);
        canvas.DrawRect(ninth, fill);
        if (!sheet && GroundShader(camera, scene) is { } ground)
        {
            using (ground)
            {
                using var floor = new SKPaint { IsAntialias = false, BlendMode = SKBlendMode.Plus, Color = Ink.WithAlpha(PointerFloorAlpha) };
                using (OnGround(floor, ground))
                    canvas.DrawRect(square, floor);
                using var ninthFloor = new SKPaint { IsAntialias = false, BlendMode = SKBlendMode.Plus, Color = Ink.WithAlpha(PointerNinthFloorAlpha) };
                using (OnGround(ninthFloor, ground))
                    canvas.DrawRect(ninth, ninthFloor);
            }
        }
        canvas.Restore();
    }
}
