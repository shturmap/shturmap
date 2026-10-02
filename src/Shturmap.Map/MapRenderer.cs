using SkiaSharp;
using Shturmap.Core;
using Shturmap.Core.Maps;

namespace Shturmap.Map;

/// <summary>Draws a <see cref="MapScene"/> through a <see cref="Camera"/>. Markers keep a fixed screen size.</summary>
public static class MapRenderer
{
    // The app's palette (App.xaml, docs/DESIGN.md §4): muted gold for quests, the game's green for extracts.
    private static readonly SKColor Background = SKColor.Parse("#0b0c0b");
    private static readonly SKColor Amber = SKColor.Parse("#c9ad62");

    /// <summary>The quest amber, which nothing else on the map may resemble.</summary>
    public static SKColor QuestAmber => Amber;
    private static readonly SKColor Green = SKColor.Parse("#8da65e");
    private static readonly SKColor Teal = SKColor.Parse("#6f9a94");
    private static readonly SKColor Lime = SKColor.Parse("#b7b77a");
    private static readonly SKColor Violet = SKColor.Parse("#9c8cc4");
    private static readonly SKColor Player = SKColor.Parse("#e9e2c8");
    private static readonly SKColor Ink = SKColor.Parse("#d9d5c4");
    private static readonly SKColor Muted = SKColor.Parse("#8a8778");
    private static readonly SKColor Red = SKColor.Parse("#b8604a");

    /// <summary>
    /// The quest kept highlighted by a click (owner, 2026-10-01: gold among gold didn't stand out). Cyan is the one
    /// hue nothing else on the map uses, the artwork included, and it stays apart from gold with any colour vision.
    /// </summary>
    public static readonly SKColor Kept = SKColor.Parse("#3fd2e0");

    // Bahnschrift (ships with Windows), semi-condensed like the app's labels.
    private static readonly SKTypeface Typeface =
        SKTypeface.FromFamilyName("Bahnschrift", new SKFontStyle(SKFontStyleWeight.Normal, SKFontStyleWidth.SemiCondensed, SKFontStyleSlant.Upright)) ?? SKTypeface.Default;
    private static readonly SKTypeface TypefaceBold =
        SKTypeface.FromFamilyName("Bahnschrift", new SKFontStyle(SKFontStyleWeight.SemiBold, SKFontStyleWidth.SemiCondensed, SKFontStyleSlant.Upright)) ?? SKTypeface.Default;

    public static void Render(SKCanvas canvas, Camera camera, MapScene scene, float uiScale = 1)
    {
        canvas.Clear(Background);
        if (scene.Artwork is not null)
            DrawArtwork(canvas, camera, scene, scene.Artwork);
        else
            DrawSchematic(canvas, camera, scene, uiScale);

        var layout = Layout(camera, scene, uiScale);
        foreach (var name in layout.Names)
            DrawMapName(canvas, name, uiScale);
        foreach (var zone in scene.Zones)
            DrawZone(canvas, camera, scene, zone);
        DrawTrail(canvas, camera, scene, uiScale);
        DrawGuide(canvas, layout.Guide, uiScale);
        DrawSpawns(canvas, camera, scene, uiScale);

        // Markers step back when something else is pointed at (easing with the scene's Dim), all in one layer so
        // overlapping ones fade as one. A marker on another floor stays at full strength and carries an arrow to it
        // instead (owner, 2026-10-01: half-strength markers read as "not important", and a highlighted one must look
        // highlighted).
        var back = layout.Markers.Where(m => !m.Focused).ToList();
        var alpha = scene.ShownFocus.Count > 0 ? 1 - 0.72f * scene.Dim : 1f;
        if (alpha < 1 && back.Count > 0)
        {
            using var layer = new SKPaint { Color = SKColors.White.WithAlpha((byte)(255 * alpha)) };
            canvas.SaveLayer(layer);
        }
        foreach (var marker in back)
            DrawMarker(canvas, scene, marker, uiScale);
        foreach (var label in layout.Labels.Where(l => !l.Of.Focused))
            DrawLabel(canvas, label);
        if (alpha < 1 && back.Count > 0)
            canvas.Restore();
        foreach (var marker in layout.Markers.Where(m => m.Focused))
            DrawMarker(canvas, scene, marker, uiScale);
        foreach (var label in layout.Labels.Where(l => l.Of.Focused))
            DrawLabel(canvas, label);
        DrawGuidePlate(canvas, layout.Guide, uiScale);
        DrawScaleBar(canvas, layout.Scale, uiScale);
        DrawPlayer(canvas, camera, scene, uiScale);
    }

    // ---- layout: what is drawn where, before anything is drawn ----

    /// <summary>A marker as drawn in one frame.</summary>
    /// <param name="At">Screen position (pixels).</param>
    /// <param name="R">The marker's radius: its size and the distance its label keeps.</param>
    /// <param name="Selected">Kept highlighted or pointed at: drawn larger, its label bold.</param>
    /// <param name="Kept">A marker of the quest kept highlighted: drawn in the kept colour, ringed.</param>
    /// <param name="Focused">Drawn at full strength while something is in focus.</param>
    /// <param name="Floor">Floors above (+) or below (−) the one shown; 0 on it.</param>
    public sealed record ShownMarker(MapMarker Marker, SKPoint At, float R, SKColor Color, bool Selected, bool Kept, bool Focused, int Floor)
    {
        /// <summary>How far the symbol reaches from its centre, collar and ring included (pixels).</summary>
        public float Reach { get; init; }

        /// <summary>How many places of one objective this marker stands for (1, or a cluster's size).</summary>
        public int Count { get; init; } = 1;
    }

    /// <summary>A placed label: the marker's own, or a map name (rotated about its anchor).</summary>
    public sealed record PlacedLabel(string Text, SKRect Box, float Baseline, float Size, bool Bold, SKColor Color, ShownMarker Of);

    /// <summary>A map name that fits, rotated about its anchor.</summary>
    public sealed record PlacedName(string Text, SKPoint At, float Rotation, float Size, SKRect Box);

    /// <summary>Everything placed in one frame: markers in drawing order, their labels, the map's names, the guide and the scale.</summary>
    public sealed record MapLayout(IReadOnlyList<ShownMarker> Markers, IReadOnlyList<PlacedLabel> Labels, IReadOnlyList<PlacedName> Names,
        GuideLine? Guide, ScaleBar Scale);

    /// <summary>
    /// Places symbols first, then labels by priority (cartography review, 2026-10-02): the kept or pointed-at quest,
    /// bosses, quests, extracts and transits, snipers, and the map's own names last. Each marker label tries right,
    /// left, above and below its symbol and is dropped when all four are taken; no label covers a symbol. A selected
    /// label that finds no free place still shows on the right, giving way only to its own copies.
    /// </summary>
    public static MapLayout Layout(Camera camera, MapScene scene, float ui)
    {
        var markers = ShownMarkers(camera, scene, ui);
        var taken = new List<SKRect>();
        foreach (var m in markers)
        {
            if (m.Count > 1)
                taken.Add(CountBadgeBox(m, ui));
            taken.Add(Square(m.At, m.Reach));
            if (m.Floor != 0)
                taken.Add(FloorBadgeBox(m, ui));
        }
        if (scene.Player is { } player)
        {
            var at = Screen(camera, scene, player.Position);
            taken.Add(Square(at, (PlayerRing + 3) * ui));
            var age = DateTime.Now - player.At;
            if (age >= PlayerOld)
            {
                using var tagFont = new SKFont(TypefaceBold, 10.5f * ui);
                taken.Add(AgeTagBox(at, tagFont.MeasureText(AgeText(age)), ui));
            }
        }
        var guide = Guide(camera, scene, ui);
        if (guide?.Plate is not null)
            taken.Add(guide.PlateBox);
        var scale = Scale(camera, scene, ui);
        taken.Add(scale.Box);

        var labels = new List<PlacedLabel>();
        using var regular = new SKFont(Typeface, 11.5f * ui);
        using var bold = new SKFont(TypefaceBold, 13 * ui);
        foreach (var m in markers.Where(m => m.Marker.Label.Length > 0 && (scene.ShowLabels || m.Selected)).OrderBy(LabelRank).ThenByDescending(m => m.Count))
        {
            // A name is said once per neighbourhood: eight "Abandoned Cargo" labels in one block say no more than one.
            if (labels.Any(l => l.Text == m.Marker.Label && SKPoint.Distance(l.Of.At, m.At) < LabelRepeat * ui))
                continue;
            var font = m.Selected ? bold : regular;
            var width = font.MeasureText(m.Marker.Label);
            var gap = 4 * ui;
            SKRect? box = null;
            foreach (var candidate in LabelCandidates(m.At, m.Reach + gap, width, font.Size))
            {
                if (!taken.Any(t => t.IntersectsWith(candidate)))
                {
                    box = candidate;
                    break;
                }
            }
            if (box is null && m.Selected)
                box = LabelCandidates(m.At, m.Reach + gap, width, font.Size).First();
            if (box is not { } placed)
                continue;
            taken.Add(placed);
            var color = m.Selected ? m.Color : m.Marker.Kind == MarkerKind.BossSpawn ? Red : Ink;
            labels.Add(new PlacedLabel(m.Marker.Label, placed, placed.Top + font.Size * 1.1f, font.Size, m.Selected, color, m));
        }

        var names = new List<PlacedName>();
        if (scene.ShowLabels)
        {
            using var font = new SKFont(Typeface, 11 * ui);
            foreach (var label in scene.Definition.Labels.OrderByDescending(l => l.Size))
            {
                // A label with heights belongs to one floor and shows only with it, as on tarkov.dev (The Lab's rooms
                // would print over each other otherwise).
                if (label.Height is { } h && FloorOffset(scene, new WorldPoint(label.X, (h.Min + h.Max) / 2, label.Z)) != 0)
                    continue;
                var text = label.Text.ToUpperInvariant();
                var at = camera.ToScreen(scene.Projection.ToMap(label.X, label.Z));
                var width = font.MeasureText(text);
                var box = Rotated(SKRect.Create(at.X - width / 2, at.Y - font.Size, width, font.Size * 1.3f), at, (float)label.Rotation);
                if (taken.Any(t => t.IntersectsWith(box)))
                    continue;
                taken.Add(box);
                names.Add(new PlacedName(text, at, (float)label.Rotation, font.Size, box));
            }
        }
        return new MapLayout(markers, labels, names, guide, scale);
    }

    /// <summary>How near (pixels, before scaling) two markers with the same label may be before only one is labelled.</summary>
    public const float LabelRepeat = 250;

    // Label priority: what the player picked, then bosses, quests, ways out, snipers.
    private static int LabelRank(ShownMarker m) => m.Selected ? 0 : m.Marker.Kind switch
    {
        MarkerKind.BossSpawn => 1,
        MarkerKind.Objective or MarkerKind.PossibleLocation or MarkerKind.ObjectiveDone => 2,
        MarkerKind.ExtractPmc or MarkerKind.ExtractScav or MarkerKind.ExtractShared or MarkerKind.Transit => 3,
        _ => 4,
    };

    /// <summary>Where a marker label may go, in order: right, left, above and below the symbol.</summary>
    /// <param name="clearance">From the symbol's centre to the label's near edge.</param>
    public static IEnumerable<SKRect> LabelCandidates(SKPoint at, float clearance, float width, float size)
    {
        var height = size * 1.3f;
        var top = at.Y - size * 0.75f;
        yield return SKRect.Create(at.X + clearance, top, width, height);
        yield return SKRect.Create(at.X - clearance - width, top, width, height);
        yield return SKRect.Create(at.X - width / 2, at.Y - clearance - height, width, height);
        yield return SKRect.Create(at.X - width / 2, at.Y + clearance, width, height);
    }

    private static SKRect Square(SKPoint at, float half) => new(at.X - half, at.Y - half, at.X + half, at.Y + half);

    // The axis-aligned box around a rectangle rotated about a point.
    private static SKRect Rotated(SKRect box, SKPoint about, float degrees)
    {
        if (degrees == 0)
            return box;
        var rotation = SKMatrix.CreateRotationDegrees(degrees, about.X, about.Y);
        return rotation.MapRect(box);
    }

    /// <summary>
    /// The markers drawn in this view, in drawing order: places of one objective whose markers would overlap (closer
    /// than two marker widths) merge into one at the group's medoid, a real place, with their count; groups split as
    /// the view zooms in. Markers out of view are left out (their labels would take the place of the ones in view).
    /// </summary>
    public static List<ShownMarker> ShownMarkers(Camera camera, MapScene scene, float ui)
    {
        var view = SKRect.Create(0, 0, camera.Viewport.Width, camera.Viewport.Height);
        view.Inflate(40 * ui, 40 * ui);
        var shown = scene.Markers.Select((m, i) => (Index: i, Shown: Show(camera, scene, m, ui))).ToList();
        var result = new List<(int Index, ShownMarker Shown)>();
        foreach (var group in shown.GroupBy(s => ObjectiveOf(s.Shown.Marker) is { } objective ? $"{objective}|{s.Shown.Marker.Kind}" : "#" + s.Index))
        {
            foreach (var cluster in Clusters(group.ToList(), (a, b) => SKPoint.Distance(a.Shown.At, b.Shown.At) < 4 * a.Shown.R))
            {
                if (cluster.Count == 1)
                {
                    result.Add(cluster[0]);
                    continue;
                }
                var medoid = cluster.MinBy(c => cluster.Sum(o => SKPoint.Distance(c.Shown.At, o.Shown.At)))!;
                // A group can span floors: its arrow shows when any of its places is on another floor than the one shown.
                var floor = medoid.Shown.Floor != 0 ? medoid.Shown.Floor
                    : cluster.Select(c => c.Shown.Floor).Where(f => f != 0).GroupBy(f => f).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
                result.Add((medoid.Index, medoid.Shown with { Count = cluster.Count, Floor = floor }));
            }
        }
        return result
            .Where(r => view.Contains(r.Shown.At))
            .OrderBy(r => r.Shown.Focused ? 2 : r.Shown.Selected ? 1 : 0)
            .ThenBy(r => r.Index)
            .Select(r => r.Shown)
            .ToList();
    }

    // Single linkage: items closer than the test (directly or through others) end up in one cluster.
    private static List<List<T>> Clusters<T>(List<T> items, Func<T, T, bool> near)
    {
        var clusters = new List<List<T>>();
        foreach (var item in items)
        {
            var joined = clusters.Where(c => c.Any(o => near(item, o))).ToList();
            var merged = joined.SelectMany(c => c).Append(item).ToList();
            clusters.RemoveAll(joined.Contains);
            clusters.Add(merged);
        }
        return clusters;
    }

    /// <summary>The objective a quest marker belongs to (its id is "objective:&lt;objective id&gt;:&lt;n&gt;"), or null.</summary>
    public static string? ObjectiveOf(MapMarker m) =>
        m.Objective is not null && m.Id.StartsWith("objective:", StringComparison.Ordinal) && m.Id.LastIndexOf(':') is var end and > 10
            ? m.Id[10..end]
            : null;

    // The count badge sits at the marker's lower right; the upper right is the floor arrow's.
    private static SKRect CountBadgeBox(ShownMarker m, float ui)
    {
        using var font = new SKFont(TypefaceBold, 10 * ui);
        var width = Math.Max(font.MeasureText(m.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)) + 7 * ui, 14 * ui);
        var c = new SKPoint(m.At.X + m.R * 0.85f, m.At.Y + m.R * 0.85f);
        return SKRect.Create(c.X - width / 2, c.Y - 7 * ui, width, 14 * ui);
    }

    private static void DrawCountBadge(SKCanvas canvas, ShownMarker m, float ui)
    {
        var box = CountBadgeBox(m, ui);
        using var plate = new SKPaint { Color = Background, IsAntialias = true };
        using var edge = new SKPaint { Color = m.Color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f * ui };
        using var font = new SKFont(TypefaceBold, 10 * ui);
        using var paint = new SKPaint { Color = m.Color, IsAntialias = true };
        canvas.DrawRect(box, plate);
        canvas.DrawRect(box, edge);
        canvas.DrawText(m.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), box.MidX, box.MidY + font.Size * 0.36f, SKTextAlign.Center, font, paint);
    }

    private static ShownMarker Show(Camera camera, MapScene scene, MapMarker marker, float ui)
    {
        var at = Screen(camera, scene, marker.Position);
        var focused = IsFocused(scene, marker);
        var selected = IsSelected(scene, marker) || focused;
        // The kept quest's markers are drawn in their own colour and larger than anything pointed at.
        var kept = marker.Objective is not null && IsSelected(scene, marker);
        var color = kept ? Kept : ColorOf(marker.Kind);
        // Quest markers carry a type glyph, so they are drawn largest. Extracts and transits (level 2) are as large as
        // the boss diamond (level 3): a 15 px triangle or diamond.
        var r = marker switch
        {
            { Objective: not null } => kept ? 14f : selected ? 12f : 10f,
            { Kind: MarkerKind.ExtractPmc or MarkerKind.ExtractScav or MarkerKind.ExtractShared or MarkerKind.Transit } => selected ? 9.5f : 7.5f,
            _ => selected ? 8f : 6f,
        } * ui;
        var reach = marker.Kind switch
        {
            _ when kept => r + 7.5f * ui,
            MarkerKind.ObjectiveDone => r * 0.8f,
            MarkerKind.ExtractPmc or MarkerKind.ExtractScav or MarkerKind.ExtractShared => r * 1.2f,
            MarkerKind.BossSpawn => r * 1.25f,
            MarkerKind.SniperSpawn => 6.5f * ui,
            MarkerKind.ScavSpawn => 4 * ui,
            _ => r,
        } + MarkerCollar * ui;
        var inFocus = scene.ShownFocus.Count == 0 || focused;
        return new ShownMarker(marker, at, r, color, selected, kept, inFocus && scene.ShownFocus.Count > 0, FloorOffset(scene, marker.Position))
        {
            Reach = reach + 1 * ui,
        };
    }

    private static void DrawLabel(SKCanvas canvas, PlacedLabel label)
    {
        using var font = new SKFont(label.Bold ? TypefaceBold : Typeface, label.Size);
        var ui = label.Size / (label.Bold ? 13 : 11.5f);
        using var shadow = new SKPaint { Color = Background.WithAlpha(220), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 * ui };
        using var text = new SKPaint { Color = label.Color, IsAntialias = true };
        canvas.DrawText(label.Text, label.Box.Left, label.Baseline, SKTextAlign.Left, font, shadow);
        canvas.DrawText(label.Text, label.Box.Left, label.Baseline, SKTextAlign.Left, font, text);
    }

    // The map's own names: Ink at 59 % on a halo of the ground, so they read on light streets (1.4:1 without it,
    // 4.7:1 with it, cartography review 2026-10-02).
    private static void DrawMapName(SKCanvas canvas, PlacedName name, float ui)
    {
        using var font = new SKFont(Typeface, name.Size);
        using var halo = new SKPaint { Color = Background.WithAlpha(220), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 * ui };
        using var paint = new SKPaint { Color = Ink.WithAlpha(150), IsAntialias = true };
        canvas.Save();
        canvas.RotateDegrees(name.Rotation, name.At.X, name.At.Y);
        canvas.DrawText(name.Text, name.At.X, name.At.Y, SKTextAlign.Center, font, halo);
        canvas.DrawText(name.Text, name.At.X, name.At.Y, SKTextAlign.Center, font, paint);
        canvas.Restore();
    }

    private static bool IsSelected(MapScene scene, MapMarker m) =>
        scene.Selected is not null && (m.Id == scene.Selected || m.Group == scene.Selected);

    private static bool IsFocused(MapScene scene, MapMarker m) =>
        scene.ShownFocus.Contains(m.Id) || (m.Group is not null && scene.ShownFocus.Contains(m.Group));

    /// <summary>
    /// Whether a point is on a floor above (+1) or below (−1) the one shown, or on it (0). Floors without artwork
    /// of their own are drawn in the base layer, so they count as the ground.
    /// </summary>
    public static int FloorOffset(MapScene scene, WorldPoint p)
    {
        var stack = scene.FloorStack;
        if (stack.Count == 0)
            return 0;
        var layer = FloorResolver.LayerFor(scene.Definition, p);
        int IndexOf(MapLayer? l)
        {
            // The same layer object, not the same SvgLayer: maps without artwork have no SvgLayer on any floor.
            var i = l is null ? -1 : stack.ToList().FindIndex(s => s is not null && s == l);
            return i >= 0 ? i : stack.ToList().IndexOf(null);
        }
        var here = IndexOf(scene.Floor);
        var there = IndexOf(layer);
        // The stack lists the top floor first.
        return there == here ? 0 : there < here ? 1 : -1;
    }

    private static SKRect FloorBadgeBox(ShownMarker m, float ui)
    {
        var r = m.Marker.Kind == MarkerKind.ScavSpawn ? 4 * ui : m.R;
        return Square(new SKPoint(m.At.X + r * 0.8f, m.At.Y - r * 0.8f), 6 * ui);
    }

    // The other-floor arrow: a small dark disc at the marker's upper right with a chevron pointing up or down.
    private static void DrawFloorArrow(SKCanvas canvas, SKPoint at, float r, int offset, float ui)
    {
        var c = new SKPoint(at.X + r * 0.8f, at.Y - r * 0.8f);
        var size = 5.5f * ui;
        using var disc = new SKPaint { Color = Background, IsAntialias = true };
        using var rim = new SKPaint { Color = Player, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f * ui };
        canvas.DrawCircle(c, size, disc);
        canvas.DrawCircle(c, size, rim);
        var h = 2.6f * ui;
        var w = 3.2f * ui;
        using var chevron = offset > 0
            ? Polygon(new(c.X, c.Y - h), new(c.X + w, c.Y + h * 0.8f), new(c.X - w, c.Y + h * 0.8f))
            : Polygon(new(c.X, c.Y + h), new(c.X + w, c.Y - h * 0.8f), new(c.X - w, c.Y - h * 0.8f));
        using var fill = new SKPaint { Color = Player, IsAntialias = true };
        canvas.DrawPath(chevron, fill);
    }

    /// <summary>The marker under a screen point (pixels), nearest first, or null.</summary>
    public static MapMarker? HitTest(Camera camera, MapScene scene, SKPoint screen, float ui)
    {
        MapMarker? best = null;
        var bestDistance = float.MaxValue;
        foreach (var shown in ShownMarkers(camera, scene, ui))
        {
            var marker = shown.Marker;
            // Scav and sniper zones say nothing more on hover.
            if (marker.Kind is MarkerKind.ScavSpawn or MarkerKind.SniperSpawn)
                continue;
            var distance = SKPoint.Distance(shown.At, screen);
            var reach = (marker.Objective is not null ? 15f : 10f) * ui;
            if (distance <= reach && distance < bestDistance)
            {
                best = marker;
                bestDistance = distance;
            }
        }
        return best;
    }

    private static void DrawArtwork(SKCanvas canvas, Camera camera, MapScene scene, MapArtwork artwork)
    {
        canvas.Save();
        // Concat, not SetMatrix: a snapshot draws on a canvas already scaled for pixel density.
        var view = camera.Matrix;
        canvas.Concat(in view);
        canvas.Translate((float)scene.Placement.OffsetX, (float)scene.Placement.OffsetY);
        canvas.Scale((float)scene.Placement.Scale);
        using var recede = new SKPaint { ColorFilter = ArtworkColors.Filter };
        canvas.DrawPicture(artwork.Base, recede);
        if (artwork.Layer(scene.Floor?.SvgLayer) is { } floor)
        {
            using var dim = new SKPaint { Color = Background.WithAlpha(150) };
            canvas.DrawRect(artwork.ViewBox, dim);
            canvas.DrawPicture(floor, recede);
        }
        canvas.Restore();
    }

    // Maps without usable artwork (docs/DESIGN.md §3) get a sheet instead, drawn from data only: maps.json's bounds (the
    // extent tarkov.dev gives the map, not a traced outline) as a panel with a metric grid (10 m, every fifth line
    // stronger), so positions, distances and markers still read true. No walls: the data has none. The 10 m lines go
    // when they would crowd closer than 6 px. A caption in the sheet's corner says what it is.
    private static readonly SKColor SheetPanel = SKColor.Parse("#121311");
    private static readonly SKColor SheetEdge = SKColor.Parse("#45463f");
    private static readonly SKColor SheetMinor = SKColor.Parse("#1c1d1a");
    private static readonly SKColor SheetMajor = SKColor.Parse("#2a2b27");
    private const double SheetSpacing = 10;

    private static void DrawSchematic(SKCanvas canvas, Camera camera, MapScene scene, float ui)
    {
        var b = scene.Definition.Bounds;
        SKPoint At(double x, double z) => camera.ToScreen(scene.Projection.ToMap(x, z));
        using var sheet = Polygon(At(b.X1, b.Z1), At(b.X2, b.Z1), At(b.X2, b.Z2), At(b.X1, b.Z2));
        using var panel = new SKPaint { Color = SheetPanel, IsAntialias = true };
        canvas.DrawPath(sheet, panel);

        var step = SKPoint.Distance(At(0, 0), At(SheetSpacing, 0));
        canvas.Save();
        canvas.ClipPath(sheet, antialias: true);
        using var minor = new SKPaint { Color = SheetMinor, StrokeWidth = 1, Style = SKPaintStyle.Stroke };
        using var major = new SKPaint { Color = SheetMajor, StrokeWidth = 1, Style = SKPaintStyle.Stroke };
        foreach (var line in SchematicGrid.Lines(b, SheetSpacing, 5))
        {
            if (!line.Major && step < 6)
                continue;
            canvas.DrawLine(At(line.X1, line.Z1), At(line.X2, line.Z2), line.Major ? major : minor);
        }
        canvas.Restore();
        using var edge = new SKPaint { Color = SheetEdge, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        canvas.DrawPath(sheet, edge);

        var box = sheet.Bounds;
        using var font = new SKFont(TypefaceBold, 10 * ui);
        using var paint = new SKPaint { Color = Ink.WithAlpha(110), IsAntialias = true };
        // Without floor data (Labyrinth) every marker is drawn on one plane: say so rather than imply one floor.
        var caption = $"NO ARTWORK FOR THIS MAP · GRID {SheetSpacing:0} M" + (scene.FloorStack.Count == 0 ? " · NO FLOOR DATA" : "");
        canvas.DrawText(caption, box.Left + 10 * ui, box.Top + 18 * ui, SKTextAlign.Left, font, paint);
    }

    private static SKPoint Screen(Camera camera, MapScene scene, WorldPoint p) => camera.ToScreen(scene.Projection.ToMap(p));

    private static void DrawZone(SKCanvas canvas, Camera camera, MapScene scene, MapZone zone)
    {
        if (zone.Outline.Count < 3)
            return;
        using var path = Polygon(zone.Outline.Select(p => Screen(camera, scene, p)).ToArray());
        var kept = scene.Selected is not null && zone.Group == scene.Selected;
        var color = kept ? Kept : ColorOf(zone.Kind);
        var focused = zone.Group is not null && scene.ShownFocus.Contains(zone.Group);
        var selected = kept || focused;
        // Zones outside the focus ease back with the scene's Dim, like the markers.
        var fade = scene.ShownFocus.Count > 0 && !focused ? scene.Dim : 0f;
        using var fill = new SKPaint { Color = color.WithAlpha((byte)(selected ? 70 : 35 - 23 * fade)), IsAntialias = true };
        using var stroke = new SKPaint { Color = color.WithAlpha((byte)(selected ? 230 : 140 - 90 * fade)), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = selected ? 2 : 1.2f };
        canvas.DrawPath(path, fill);
        canvas.DrawPath(path, stroke);
    }

    private static void DrawTrail(SKCanvas canvas, Camera camera, MapScene scene, float ui)
    {
        if (scene.Trail.Count == 0)
            return;
        var points = scene.Trail.Select(p => Screen(camera, scene, p)).ToList();
        if (scene.Player is { } player)
            points.Add(Screen(camera, scene, player.Position));
        // In the player's sand: it is their path (teal is the Scav extracts').
        using var line = new SKPaint
        {
            Color = Player.WithAlpha(150), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f * ui,
            PathEffect = SKPathEffect.CreateDash([4 * ui, 4 * ui], 0),
        };
        using var dot = new SKPaint { Color = Player.WithAlpha(190), IsAntialias = true };
        for (var i = 1; i < points.Count; i++)
            canvas.DrawLine(points[i - 1], points[i], line);
        foreach (var p in points.Take(scene.Trail.Count))
            canvas.DrawCircle(p, 3 * ui, dot);
    }

    // Where the item the pointer is on lies loose: small open squares, like an empty inventory cell.
    private static void DrawSpawns(SKCanvas canvas, Camera camera, MapScene scene, float ui)
    {
        if (scene.Spawns.Count == 0)
            return;
        using var cell = new SKPaint { Color = Background.WithAlpha(200), IsAntialias = true };
        using var edge = new SKPaint { Color = Player, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f * ui };
        var half = 5f * ui;
        foreach (var spawn in scene.Spawns)
        {
            var at = Screen(camera, scene, spawn);
            var box = new SKRect(at.X - half, at.Y - half, at.X + half, at.Y + half);
            canvas.DrawRect(box, cell);
            canvas.DrawRect(box, edge);
        }
    }

    // A dashed line from the player to the kept quest's nearest place.
    private static void DrawGuide(SKCanvas canvas, GuideLine? guide, float ui)
    {
        if (guide is null)
            return;
        using var line = new SKPaint
        {
            Color = Kept.WithAlpha(220), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f * ui,
            PathEffect = SKPathEffect.CreateDash([6 * ui, 5 * ui], 0),
        };
        canvas.DrawLine(guide.From, guide.To, line);
    }

    /// <summary>The guide from the player to the kept quest's nearest place, and its distance on a plate.</summary>
    /// <param name="Plate">"69 m", with the fix's age once it is a minute old ("69 m · 4 MIN"); null when the line is too short to carry it.</param>
    public sealed record GuideLine(SKPoint From, SKPoint To, double Metres, string? Plate, SKRect PlateBox);

    private static GuideLine? Guide(Camera camera, MapScene scene, float ui)
    {
        if (scene.Player is not { } player || scene.Selected is null)
            return null;
        var targets = scene.Markers.Where(m => IsSelected(scene, m) && m.Kind != MarkerKind.ObjectiveDone).ToList();
        if (targets.Count == 0)
            return null;
        var nearest = targets.MinBy(m => player.Position.HorizontalDistanceTo(m.Position))!;
        var from = Screen(camera, scene, player.Position);
        var to = Screen(camera, scene, nearest.Position);
        // The number the card shows: horizontal metres to the nearest place, as old as the position.
        var metres = player.Position.HorizontalDistanceTo(nearest.Position);
        var age = DateTime.Now - player.At;
        var text = DistanceText(metres) + (age >= PlayerOld ? " · " + AgeText(age) : "");
        // On the part of the line in view, and only where the line is long enough to carry it clear of its ends.
        var view = SKRect.Create(0, 0, camera.Viewport.Width, camera.Viewport.Height);
        view.Inflate(-24 * ui, -24 * ui);
        using var font = new SKFont(TypefaceBold, 12 * ui);
        var width = font.MeasureText(text) + 12 * ui;
        var height = 18 * ui;
        if (Clip(from, to, view) is not var (a, b) || SKPoint.Distance(a, b) < width + 56 * ui)
            return new GuideLine(from, to, metres, null, SKRect.Empty);
        var mid = new SKPoint((a.X + b.X) / 2, (a.Y + b.Y) / 2);
        return new GuideLine(from, to, metres, text, SKRect.Create(mid.X - width / 2, mid.Y - height / 2, width, height));
    }

    /// <summary>A distance as the cards say it: "69 m", "1.2 km".</summary>
    public static string DistanceText(double metres) => metres < 1000 ? $"{metres:0} m" : $"{metres / 1000:0.0} km";

    // The part of a segment inside a rectangle (Liang–Barsky), or null.
    private static (SKPoint A, SKPoint B)? Clip(SKPoint a, SKPoint b, SKRect box)
    {
        float t0 = 0, t1 = 1, dx = b.X - a.X, dy = b.Y - a.Y;
        foreach (var (p, q) in new[] { (-dx, a.X - box.Left), (dx, box.Right - a.X), (-dy, a.Y - box.Top), (dy, box.Bottom - a.Y) })
        {
            if (p == 0)
            {
                if (q < 0)
                    return null;
                continue;
            }
            var t = q / p;
            if (p < 0)
                t0 = Math.Max(t0, t);
            else
                t1 = Math.Min(t1, t);
            if (t0 > t1)
                return null;
        }
        return (new SKPoint(a.X + t0 * dx, a.Y + t0 * dy), new SKPoint(a.X + t1 * dx, a.Y + t1 * dy));
    }

    private static void DrawGuidePlate(SKCanvas canvas, GuideLine? guide, float ui)
    {
        if (guide?.Plate is not { } text)
            return;
        var box = guide.PlateBox;
        using var plate = new SKPaint { Color = Background.WithAlpha(235), IsAntialias = true };
        using var edge = new SKPaint { Color = Kept, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 * ui };
        using var font = new SKFont(TypefaceBold, 12 * ui);
        using var paint = new SKPaint { Color = Kept, IsAntialias = true };
        canvas.DrawRect(box, plate);
        canvas.DrawRect(box, edge);
        canvas.DrawText(text, box.MidX, box.MidY + font.Size * 0.36f, SKTextAlign.Center, font, paint);
    }

    // ---- the scale bar ----

    /// <summary>The scale bar: its round length in metres, its length on screen, and where it stands.</summary>
    public sealed record ScaleBar(double Metres, float Pixels, SKPoint Origin, SKRect Box);

    private static readonly double[] ScaleSteps = [1, 2, 5, 10, 25, 50, 100, 200, 500, 1000, 2000];

    /// <summary>The longest round length (1, 2, 5, 10, 25, 50, 100, 200, 500 m …) that fits in a number of pixels.</summary>
    public static double ScaleBarMetres(double pixelsPerMetre, float maxPixels) =>
        ScaleSteps.Where(s => s * pixelsPerMetre <= maxPixels).DefaultIfEmpty(ScaleSteps[0]).Max();

    // Bottom left, above the wiki link and the credit line the window lays over that corner.
    private static ScaleBar Scale(Camera camera, MapScene scene, float ui)
    {
        var origin = Screen(camera, scene, new WorldPoint(0, 0, 0));
        var perMetre = (SKPoint.Distance(origin, Screen(camera, scene, new WorldPoint(100, 0, 0))) +
                        SKPoint.Distance(origin, Screen(camera, scene, new WorldPoint(0, 0, 100)))) / 200;
        var metres = ScaleBarMetres(perMetre, 120 * ui);
        var pixels = (float)(metres * perMetre);
        var at = new SKPoint(18 * ui, camera.Viewport.Height - 66 * ui);
        return new ScaleBar(metres, pixels, at, new SKRect(at.X - 6 * ui, at.Y - 20 * ui, at.X + pixels + 34 * ui, at.Y + 4 * ui));
    }

    private static void DrawScaleBar(SKCanvas canvas, ScaleBar bar, float ui)
    {
        var (x, y, w) = (bar.Origin.X, bar.Origin.Y, bar.Pixels);
        using var halo = new SKPaint { Color = Background.WithAlpha(200), IsAntialias = true, StrokeWidth = 4 * ui, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Square };
        using var line = new SKPaint { Color = Ink.WithAlpha(210), IsAntialias = true, StrokeWidth = 1.5f * ui, Style = SKPaintStyle.Stroke };
        foreach (var paint in new[] { halo, line })
        {
            canvas.DrawLine(x, y, x + w, y, paint);
            canvas.DrawLine(x, y - 5 * ui, x, y, paint);
            canvas.DrawLine(x + w / 2, y - 3 * ui, x + w / 2, y, paint);
            canvas.DrawLine(x + w, y - 5 * ui, x + w, y, paint);
        }
        using var font = new SKFont(Typeface, 10.5f * ui);
        using var textHalo = new SKPaint { Color = Background.WithAlpha(220), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 * ui };
        using var text = new SKPaint { Color = Ink.WithAlpha(210), IsAntialias = true };
        var half = bar.Metres / 2;
        var marks = new List<(string Text, float X)> { ("0", x), (DistanceText(bar.Metres), x + w) };
        if (half == Math.Floor(half))
            marks.Add(($"{half:0}", x + w / 2));
        foreach (var (label, at) in marks)
        {
            canvas.DrawText(label, at, y - 8 * ui, SKTextAlign.Center, font, textHalo);
            canvas.DrawText(label, at, y - 8 * ui, SKTextAlign.Center, font, text);
        }
    }

    /// <summary>How long one pulse of a focused marker takes; the map keeps redrawing while something is in focus.</summary>
    public static readonly TimeSpan PulsePeriod = TimeSpan.FromSeconds(1.4);

    // A ring that leaves the marker and fades, like a ping: motion is what the eye notices before anything else, so
    // the focused markers are found at once even among many.
    private static void DrawPulse(SKCanvas canvas, MapScene scene, SKPoint at, float r, SKColor color, float ui)
    {
        var t = (float)((DateTime.Now - scene.FocusSince).TotalSeconds % PulsePeriod.TotalSeconds / PulsePeriod.TotalSeconds);
        var fade = (1 - t) * (1 - t);
        using var ring = new SKPaint
        {
            Color = color.WithAlpha((byte)(220 * fade)), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f * ui,
        };
        canvas.DrawCircle(at, r + (3 + 26 * t) * ui, ring);
    }

    // Every symbol stands on a dark collar (3 px, the ground at 67 %), so it keeps an edge on light streets, where
    // amber, green and road are nearly as bright (cartography review, 2026-10-02: one collar rule for all symbols).
    private const float MarkerCollar = 3f;
    private const byte MarkerCollarAlpha = 170;

    private static void DrawCollar(SKCanvas canvas, SKPath shape, float ui)
    {
        using var collar = new SKPaint
        {
            Color = Background.WithAlpha(MarkerCollarAlpha), IsAntialias = true, Style = SKPaintStyle.StrokeAndFill,
            StrokeWidth = 2 * MarkerCollar * ui, StrokeJoin = SKStrokeJoin.Round,
        };
        canvas.DrawPath(shape, collar);
    }

    private static void DrawCollar(SKCanvas canvas, SKPoint at, float r, float ui)
    {
        using var collar = new SKPaint { Color = Background.WithAlpha(MarkerCollarAlpha), IsAntialias = true };
        canvas.DrawCircle(at, r + MarkerCollar * ui, collar);
    }

    // A collar for a hollow ring: a dark band under the ring and 3 px either side of it, the middle left open.
    private static void DrawRingCollar(SKCanvas canvas, SKPoint at, float r, float width, float ui)
    {
        using var collar = new SKPaint
        {
            Color = Background.WithAlpha(MarkerCollarAlpha), IsAntialias = true, Style = SKPaintStyle.Stroke,
            StrokeWidth = width + 2 * MarkerCollar * ui,
        };
        canvas.DrawCircle(at, r, collar);
    }

    private static SKPath Triangle(SKPoint at, float r) =>
        Polygon(new(at.X, at.Y - r * 1.2f), new(at.X + r * 1.1f, at.Y + r * 0.8f), new(at.X - r * 1.1f, at.Y + r * 0.8f));

    private static SKPath Diamond(SKPoint at, float r) =>
        Polygon(new(at.X, at.Y - r), new(at.X + r, at.Y), new(at.X, at.Y + r), new(at.X - r, at.Y));

    private static void DrawMarker(SKCanvas canvas, MapScene scene, ShownMarker shown, float ui)
    {
        var (marker, at, r, color) = (shown.Marker, shown.At, shown.R, shown.Color);
        var kept = shown.Kept;
        using var fill = new SKPaint { Color = color, IsAntialias = true };
        using var outline = new SKPaint { Color = Background, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 * ui };

        if (scene.Pulsing && IsFocused(scene, marker))
            DrawPulse(canvas, scene, at, r, color, ui);
        // The kept quest keeps a steady ring once it stops pulsing, so it is still found at a glance: a dark band,
        // then the colour, so it reads on light and dark artwork alike.
        if (kept)
        {
            using var band = new SKPaint { Color = Background.WithAlpha(200), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4.5f * ui };
            using var halo = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f * ui };
            canvas.DrawCircle(at, r + 5 * ui, band);
            canvas.DrawCircle(at, r + 5 * ui, halo);
        }

        switch (marker.Kind)
        {
            case MarkerKind.Objective or MarkerKind.PossibleLocation when marker.Objective is { } kind:
                if (marker.Kind == MarkerKind.PossibleLocation)
                {
                    // A possible location: hollow, so the eye reads "maybe here".
                    using var ring = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 * ui };
                    DrawRingCollar(canvas, at, r - ui, 2 * ui, ui);
                    canvas.DrawCircle(at, r - ui, ring);
                    Glyphs.Draw(canvas, kind, at, r * 1.05f, color);
                }
                else
                {
                    DrawCollar(canvas, at, r, ui);
                    canvas.DrawCircle(at, r, fill);
                    canvas.DrawCircle(at, r, outline);
                    Glyphs.Draw(canvas, kind, at, r * 1.05f, Background);
                }
                break;
            case MarkerKind.ExtractPmc or MarkerKind.ExtractScav or MarkerKind.ExtractShared:
                using (var tri = Triangle(at, r))
                {
                    DrawCollar(canvas, tri, ui);
                    canvas.DrawPath(tri, fill);
                    canvas.DrawPath(tri, outline);
                }
                // Shared by both sides: split down the middle, so it differs from your side's triangle in shape too
                // (green and khaki come within ΔE 7 with red-green colour blindness).
                if (marker.Kind == MarkerKind.ExtractShared)
                {
                    using var split = new SKPaint { Color = Background, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.2f * ui };
                    canvas.DrawLine(at.X, at.Y - r * 0.85f, at.X, at.Y + r * 0.8f, split);
                }
                break;
            case MarkerKind.SniperSpawn:
                // A hollow hexagon: a shape no other marker uses (a reticle would repeat the Elimination glyph).
                using (var hex = Hexagon(at, 6.5f * ui))
                using (var edge = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.6f * ui })
                {
                    DrawCollar(canvas, hex, ui);
                    canvas.DrawPath(hex, edge);
                    canvas.DrawCircle(at, 1.6f * ui, fill);
                }
                break;
            case MarkerKind.ScavSpawn:
                // A small open ring: there for whoever looks, not competing with quests and exits.
                using (var ring = new SKPaint { Color = color.WithAlpha(170), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f * ui })
                {
                    DrawRingCollar(canvas, at, 4 * ui, 1.5f * ui, ui);
                    canvas.DrawCircle(at, 4 * ui, ring);
                }
                break;
            case MarkerKind.BossSpawn:
                // A red diamond with a dark centre: not to be confused with the violet transit diamond.
                using (var plate = Diamond(at, r * 1.25f))
                {
                    DrawCollar(canvas, plate, ui);
                    canvas.DrawPath(plate, fill);
                    canvas.DrawPath(plate, outline);
                }
                using (var dot = new SKPaint { Color = Background, IsAntialias = true })
                    canvas.DrawCircle(at, r * 0.32f, dot);
                break;
            case MarkerKind.Transit:
                using (var diamond = Diamond(at, r))
                {
                    DrawCollar(canvas, diamond, ui);
                    canvas.DrawPath(diamond, fill);
                    canvas.DrawPath(diamond, outline);
                }
                break;
            case MarkerKind.ObjectiveDone:
                // Done: a smaller disc in muted ink with a check mark, which leaves green to the extracts.
                DrawCollar(canvas, at, r * 0.8f, ui);
                canvas.DrawCircle(at, r * 0.8f, fill);
                using (var check = new SKPaint { Color = Background, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 * ui, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round })
                using (var tick = new SKPathBuilder())
                {
                    var s = r * 0.8f;
                    tick.MoveTo(at.X - 0.42f * s, at.Y + 0.02f * s);
                    tick.LineTo(at.X - 0.1f * s, at.Y + 0.34f * s);
                    tick.LineTo(at.X + 0.45f * s, at.Y - 0.3f * s);
                    using var mark = tick.Detach();
                    canvas.DrawPath(mark, check);
                }
                break;
            default:
                DrawCollar(canvas, at, r, ui);
                canvas.DrawCircle(at, r, fill);
                canvas.DrawCircle(at, r, outline);
                break;
        }

        if (shown.Floor != 0)
            DrawFloorArrow(canvas, at, marker.Kind == MarkerKind.ScavSpawn ? 4 * ui : r, shown.Floor, ui);
        if (shown.Count > 1)
            DrawCountBadge(canvas, shown, ui);
    }

    // ---- the player's new position: a ping where it is, or an arrow at the edge when it is out of view ----

    /// <summary>
    /// Where the player is shown at the edge of the view (pixels) while their position is out of view, or null while
    /// it is in view: on the line from the view's centre toward the position, inset from the edge.
    /// </summary>
    public static SKPoint? EdgeOf(Camera camera, MapScene scene, float ui)
    {
        if (scene.Player is not { } player)
            return null;
        var at = Screen(camera, scene, player.Position);
        var (w, h) = (camera.Viewport.Width, camera.Viewport.Height);
        var margin = 6 * ui;
        if (at.X >= margin && at.X <= w - margin && at.Y >= margin && at.Y <= h - margin)
            return null;
        var inset = EdgeInset * ui;
        var center = new SKPoint(w / 2, h / 2);
        var d = at - center;
        var tx = Math.Abs(d.X) < 1e-3 ? float.MaxValue : Math.Max(0, w / 2 - inset) / Math.Abs(d.X);
        var ty = Math.Abs(d.Y) < 1e-3 ? float.MaxValue : Math.Max(0, h / 2 - inset) / Math.Abs(d.Y);
        var t = Math.Min(tx, ty);
        return new SKPoint(center.X + d.X * t, center.Y + d.Y * t);
    }

    private const float EdgeInset = 42;

    /// <summary>How near (pixels, before scaling) a click must be to the edge arrow to show the player.</summary>
    public const float EdgeReach = 24;

    // Rings leaving the marker like a sonar ping: motion and size the eye can't miss, gone after a few seconds.
    // Each ring is the player's sand on a dark band, so it reads on light and dark artwork alike.
    private static void DrawPing(SKCanvas canvas, SKPoint at, double seconds, bool animate, float ui, float from = 10)
    {
        using var band = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 9 * ui };
        using var ring = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4 * ui };
        // A steady bright ring hugging the marker for the whole ping, so even a still frame says "here, now".
        band.Color = Background.WithAlpha(170);
        ring.Color = Player;
        canvas.DrawCircle(at, (from + 6) * ui, band);
        canvas.DrawCircle(at, (from + 6) * ui, ring);
        if (!animate)
        {
            // Animation effects off: a second still ring for the same few seconds.
            canvas.DrawCircle(at, (from + 24) * ui, band);
            canvas.DrawCircle(at, (from + 24) * ui, ring);
            return;
        }
        const double each = 1.4, gap = 0.6;
        for (var i = 0; i < 3; i++)
        {
            var p = (seconds - i * gap) / each;
            if (p is < 0 or > 1)
                continue;
            var eased = 1 - Math.Pow(1 - p, 3);
            var r = (float)(from + 6 + 84 * eased) * ui;
            // Strong for most of the way out, then gone: a ring that is faint from the start is missed.
            var fade = (float)(1 - Math.Pow(p, 2.5));
            band.Color = Background.WithAlpha((byte)(150 * fade));
            ring.Color = Player.WithAlpha((byte)(255 * fade));
            canvas.DrawCircle(at, r, band);
            canvas.DrawCircle(at, r, ring);
        }
    }

    // The player out of view: a badge at the edge with an arrow pointing their way. While a new position pings it
    // is larger, pings itself and says so.
    private static void DrawEdge(SKCanvas canvas, Camera camera, MapScene scene, float ui)
    {
        if (EdgeOf(camera, scene, ui) is not { } edge || scene.Player is not { } player)
            return;
        var toward = Screen(camera, scene, player.Position) - edge;
        var angle = (float)(Math.Atan2(toward.Y, toward.X) * 180 / Math.PI);
        var pinging = scene.Pinging;
        var r = (pinging ? 17f : 13f) * ui;
        if (pinging)
            DrawPing(canvas, edge, (DateTime.Now - scene.PingSince!.Value).TotalSeconds, scene.Pulse, ui, 28);

        using var plate = new SKPaint { Color = Background.WithAlpha(230), IsAntialias = true };
        using var edgeLine = new SKPaint { Color = Player, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 * ui };
        using var fill = new SKPaint { Color = Player, IsAntialias = true };
        canvas.Save();
        canvas.RotateDegrees(angle, edge.X, edge.Y);
        using (var arrow = Polygon(new(edge.X + r + 15 * ui, edge.Y), new(edge.X + r + 2 * ui, edge.Y - 9 * ui), new(edge.X + r + 2 * ui, edge.Y + 9 * ui)))
        {
            using var arrowEdge = new SKPaint { Color = Background, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 * ui };
            canvas.DrawPath(arrow, arrowEdge);
            canvas.DrawPath(arrow, fill);
        }
        canvas.Restore();
        canvas.DrawCircle(edge, r, plate);
        canvas.DrawCircle(edge, r, edgeLine);
        canvas.DrawCircle(edge, r * 0.4f, fill);

        if (!pinging)
            return;
        // Said beside the badge, on the side toward the middle of the view, so it never runs off the edge.
        const string text = "YOUR NEW POSITION · PRESS F";
        using var font = new SKFont(TypefaceBold, 13 * ui);
        var width = font.MeasureText(text);
        var center = new SKPoint(camera.Viewport.Width / 2, camera.Viewport.Height / 2);
        var inward = center - edge;
        var length = Math.Max(1, inward.Length);
        var labelAt = new SKPoint(edge.X + inward.X / length * (r + 18 * ui), edge.Y + inward.Y / length * (r + 18 * ui));
        var left = inward.X >= 0 ? labelAt.X : labelAt.X - width;
        if (Math.Abs(inward.X) < Math.Abs(inward.Y) * 0.5f)
            left = labelAt.X - width / 2;
        left = Math.Clamp(left, 8 * ui, camera.Viewport.Width - width - 8 * ui);
        var baseline = labelAt.Y + font.Size * 0.35f;
        using var shadow = new SKPaint { Color = Background.WithAlpha(235), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4 * ui };
        using var label = new SKPaint { Color = Player, IsAntialias = true };
        canvas.DrawText(text, left, baseline, SKTextAlign.Left, font, shadow);
        canvas.DrawText(text, left, baseline, SKTextAlign.Left, font, label);
    }

    private static void DrawPlayer(SKCanvas canvas, Camera camera, MapScene scene, float ui)
    {
        if (scene.Player is not { } player)
            return;
        DrawEdge(canvas, camera, scene, ui);
        var at = Screen(camera, scene, player.Position);
        var age = DateTime.Now - player.At;
        if (scene.Pinging)
            DrawPing(canvas, at, (DateTime.Now - scene.PingSince!.Value).TotalSeconds, scene.Pulse, ui);

        // Full strength at any age (cartography review, 2026-10-02: fading it said "less important" and sank it below
        // the quest markers). The age is said instead: a steady ring, sand on a dark band like the kept quest's, turns
        // dashed once the position is a minute old, and a tag gives the minutes, as the top bar does.
        var old = age >= PlayerOld;
        using var glow = new SKPaint { Color = Player.WithAlpha(40), IsAntialias = true };
        using var band = new SKPaint { Color = Background.WithAlpha(200), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4.5f * ui };
        using var ring = new SKPaint
        {
            Color = Player, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 * ui,
            PathEffect = old ? SKPathEffect.CreateDash([3.5f * ui, 3 * ui], 0) : null,
        };
        canvas.DrawCircle(at, 16 * ui, glow);
        canvas.DrawCircle(at, PlayerRing * ui, band);
        canvas.DrawCircle(at, PlayerRing * ui, ring);

        // The facing is only true for a moment; after a minute the cone would mislead.
        if (player.YawDegrees is { } yaw && age < TimeSpan.FromSeconds(60))
        {
            var heading = (float)scene.Projection.ScreenHeadingDegrees(player.Position, yaw);
            canvas.Save();
            canvas.RotateDegrees(heading, at.X, at.Y);
            using var cone = new SKPaint
            {
                IsAntialias = true,
                Shader = SKShader.CreateRadialGradient(at, 70 * ui, [Player.WithAlpha(110), Player.WithAlpha(0)], SKShaderTileMode.Clamp),
            };
            using var wedgeBuilder = new SKPathBuilder();
            wedgeBuilder.MoveTo(at);
            wedgeBuilder.ArcTo(SKRect.Create(at.X - 70 * ui, at.Y - 70 * ui, 140 * ui, 140 * ui), -90 - 28, 56, false);
            wedgeBuilder.Close();
            using var wedge = wedgeBuilder.Detach();
            canvas.DrawPath(wedge, cone);
            // Outside the ring, so the ring stays whole.
            using var arrow = Polygon(new(at.X, at.Y - 23 * ui), new(at.X - 6 * ui, at.Y - 15 * ui), new(at.X + 6 * ui, at.Y - 15 * ui));
            using var arrowPaint = new SKPaint { Color = Player, IsAntialias = true };
            canvas.DrawPath(arrow, arrowPaint);
            canvas.Restore();
        }
        using var body = new SKPaint { Color = Player, IsAntialias = true };
        using var edge = new SKPaint { Color = Background, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f * ui };
        canvas.DrawCircle(at, 6.5f * ui, body);
        canvas.DrawCircle(at, 6.5f * ui, edge);
        if (!old)
            return;
        var text = AgeText(age);
        using var font = new SKFont(TypefaceBold, 10.5f * ui);
        var box = AgeTagBox(at, font.MeasureText(text), ui);
        using var plate = new SKPaint { Color = Background.WithAlpha(230), IsAntialias = true };
        using var paint = new SKPaint { Color = Player, IsAntialias = true };
        canvas.DrawRect(box, plate);
        canvas.DrawText(text, box.MidX, box.MidY + font.Size * 0.36f, SKTextAlign.Center, font, paint);
    }

    /// <summary>From when the player's ring is dashed and carries an age tag.</summary>
    public static readonly TimeSpan PlayerOld = TimeSpan.FromMinutes(1);

    private const float PlayerRing = 12;

    /// <summary>A fix's age as the map says it: "4 MIN", "2 H" (whole units, as the top bar).</summary>
    public static string AgeText(TimeSpan age) => age.TotalMinutes < 60 ? $"{(int)age.TotalMinutes} MIN" : $"{(int)age.TotalHours} H";

    // The age tag sits right of the ring.
    private static SKRect AgeTagBox(SKPoint at, float textWidth, float ui) =>
        SKRect.Create(at.X + (PlayerRing + 5) * ui, at.Y - 8 * ui, textWidth + 10 * ui, 16 * ui);

    private static SKPath Hexagon(SKPoint at, float r) =>
        Polygon(Enumerable.Range(0, 6).Select(i => new SKPoint(at.X + r * MathF.Cos(MathF.PI / 3 * i), at.Y + r * MathF.Sin(MathF.PI / 3 * i))).ToArray());

    private static SKPath Polygon(params SKPoint[] points)
    {
        using var builder = new SKPathBuilder();
        builder.AddPoly(points, true);
        return builder.Detach();
    }

    private static SKColor ColorOf(MarkerKind kind) => kind switch
    {
        MarkerKind.Objective or MarkerKind.PossibleLocation => Amber,
        MarkerKind.ObjectiveDone => Muted,
        MarkerKind.ExtractPmc => Green,
        MarkerKind.ExtractScav => Teal,
        MarkerKind.ExtractShared => Lime,
        MarkerKind.Transit => Violet,
        MarkerKind.BossSpawn => Red,
        _ => Ink,
    };
}
