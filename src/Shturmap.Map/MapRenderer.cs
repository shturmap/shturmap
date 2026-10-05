using SkiaSharp;
using Shturmap.Core;
using Shturmap.Core.Maps;

namespace Shturmap.Map;

/// <summary>Draws a <see cref="MapScene"/> through a <see cref="Camera"/>. Markers keep a fixed screen size.</summary>
public static partial class MapRenderer
{
    // The design system's palette (Palette, docs/DESIGN.md §4): muted gold for quests, the game's green for extracts.
    private static readonly SKColor Background = Palette.Sk(Palette.Ground);
    private static readonly SKColor Amber = Palette.Sk(Palette.Amber);

    /// <summary>The quest amber, which nothing else on the map may resemble.</summary>
    public static SKColor QuestAmber => Amber;
    private static readonly SKColor Green = Palette.Sk(Palette.Green);
    private static readonly SKColor Teal = Palette.Sk(Palette.Teal);
    private static readonly SKColor Lime = Palette.Sk(Palette.Khaki);
    private static readonly SKColor Violet = Palette.Sk(Palette.Violet);
    private static readonly SKColor Player = Palette.Sk(Palette.Sand);
    private static readonly SKColor Ink = Palette.Sk(Palette.Ink);
    private static readonly SKColor Muted = Palette.Sk(Palette.Muted);
    private static readonly SKColor Red = Palette.Sk(Palette.Red);

    /// <summary>
    /// The quest kept highlighted by a click (owner, 2026-10-01: gold among gold didn't stand out). Cyan is the one
    /// hue nothing else on the map uses, the artwork included, and it stays apart from gold with any colour vision.
    /// </summary>
    /// <summary>The first pick's colour, and that of a pick nobody gave a number.</summary>
    public static readonly SKColor Kept = Palette.Sk(Palette.Pick1);

    /// <summary>The picks' colours, in the order picks take them (<see cref="MapScene.PickSlots"/>); the first is <see cref="Kept"/>.</summary>
    public static readonly IReadOnlyList<SKColor> PickColors =
    [
        Kept, Palette.Sk(Palette.Pick2), Palette.Sk(Palette.Pick3), Palette.Sk(Palette.Pick4),
        Palette.Sk(Palette.Pick5), Palette.Sk(Palette.Pick6), Palette.Sk(Palette.Pick7), Palette.Sk(Palette.Pick8),
    ];

    /// <summary>The colour of a picked quest: everything of a pick is drawn in it (marker, ring, badges, name, zone, chevron, guide).</summary>
    public static SKColor PickColor(MapScene scene, string? quest) =>
        PickColors[quest is not null && scene.PickSlots.TryGetValue(quest, out var slot) ? ((slot % PickColors.Count) + PickColors.Count) % PickColors.Count : 0];

    // Bahnschrift (ships with Windows), semi-condensed like the app's labels.
    private static readonly SKTypeface Typeface =
        SKTypeface.FromFamilyName("Bahnschrift", new SKFontStyle(SKFontStyleWeight.Normal, SKFontStyleWidth.SemiCondensed, SKFontStyleSlant.Upright)) ?? SKTypeface.Default;
    private static readonly SKTypeface TypefaceBold =
        SKTypeface.FromFamilyName("Bahnschrift", new SKFontStyle(SKFontStyleWeight.SemiBold, SKFontStyleWidth.SemiCondensed, SKFontStyleSlant.Upright)) ?? SKTypeface.Default;

    /// <param name="stepBack">How markers outside the focus step back; <see cref="StepBackOf"/> unless a developer
    /// render compares alternatives.</param>
    public static void Render(SKCanvas canvas, Camera camera, MapScene scene, float uiScale = 1, Func<MarkerKind, bool, StepBack>? stepBack = null)
    {
        canvas.Clear(Background);
        // The zoom limit is the map's that is drawn, not the one's fitted last (a preview leaves its own behind).
        camera.LimitTo(scene.Projection.WorldRect, 24 * uiScale);
        // Read once: a tile arriving on its own thread may end the sheet in the middle of a frame.
        var sheet = scene.IsSheet;
        if (scene.Artwork is not null)
        {
            DrawArtwork(canvas, camera, scene, scene.Artwork);
        }
        else if (!sheet && scene.Tiles is { } tiles)
        {
            DrawTiles(canvas, camera, scene, tiles);
        }
        else
        {
            DrawSchematic(canvas, camera, scene, uiScale);
            // The sheet stands in for a render that couldn't be had (offline): keep asking for this view's tiles, so
            // the render comes by itself once they can be had, without a restart (the review of 2026-10-04).
            if (scene.Tiles is { } missing)
                AskForTiles(camera, scene, missing);
        }
        if (sheet)
            DrawContainers(canvas, camera, scene, uiScale);

        var layout = LayoutOf(camera, scene, uiScale);
        foreach (var name in layout.Names)
            DrawMapName(canvas, name, uiScale);
        var hazardLabels = new List<(string Text, SKPoint At)>();
        using (var ground = GroundShader(camera, scene))
        {
            foreach (var zone in scene.Zones)
                DrawZone(canvas, camera, scene, zone, uiScale, hazardLabels, ground, stepBack);
        }
        // Over every hatch, and once per group of neighbouring areas (border zones overlap along an edge).
        var labelled = new List<SKPoint>();
        var screen = new SKRect(0, 0, camera.Viewport.Width, camera.Viewport.Height);
        foreach (var (text, at) in hazardLabels.Where(l => screen.Contains(l.At)))
        {
            if (labelled.Any(p => SKPoint.Distance(p, at) < 260 * uiScale))
                continue;
            labelled.Add(at);
            DrawHazardLabel(canvas, text, at, uiScale, HazardLabelStrength(scene, stepBack));
        }
        DrawTrail(canvas, camera, scene, uiScale);
        DrawGuide(canvas, layout.Guide, uiScale);
        DrawSpawns(canvas, camera, scene, uiScale);
        // The extracts on the player's list this raid are lit, under every symbol, so a glow never covers one.
        foreach (var marker in layout.Markers.Where(m => m.Listed))
            DrawListedGlow(canvas, marker);
        DrawUnderlay(canvas, scene, layout.Markers, uiScale);
        void Draw(ShownMarker marker) => DrawMarker(canvas, scene, marker, uiScale);

        // Markers outside the focus step back while something is highlighted (easing with the scene's Dim), each kind
        // by its own measure (StepBackOf), one layer per measure so overlapping ones fade as one. A marker on another
        // floor stays at full strength and carries an arrow to it instead (owner, 2026-10-01: half-strength markers
        // read as "not important", and a highlighted one must look highlighted).
        // Picks never step back: they are the plan for this raid, as much as the ways out (owner, 2026-10-03), and
        // the doors of the keys they need are part of them. They are level 1, so they get a pass of their own after
        // everything that steps back: grouped by measure with the ways out, which don't step back in a raid either,
        // they were drawn first there and lay under other quests' markers (the review of 2026-10-04).
        // Where symbols share a place, the one that matters more lies on top: the layers that step back furthest are
        // drawn first, and within a layer the markers go by their labels' priority, least first (the review of
        // 2026-10-04, C6: a Scav spawn's ring was drawn over the boss's octagon of the same spawn zone, which then
        // read as a red ring).
        var dim = scene.ShownFocus.Count > 0 ? scene.Dim : 0f;
        StepBack Measure(ShownMarker m) => StepBackOf(scene, m, stepBack);
        foreach (var group in layout.Markers.Where(m => !m.Focused && !IsPick(scene, m)).GroupBy(Measure).OrderBy(g => g.Key.Alpha))
        {
            using var layer = new StepBackLayer(canvas, group.Key.Alpha, group.Key.Saturation, dim);
            foreach (var marker in group.OrderByDescending(LabelRank))
                Draw(marker);
        }
        foreach (var marker in layout.Markers.Where(m => !m.Focused && IsPick(scene, m)))
            Draw(marker);
        foreach (var group in layout.Labels.Where(l => !l.Of.Focused).GroupBy(l => Measure(l.Of).LabelAlpha))
        {
            using var layer = new StepBackLayer(canvas, group.Key, 1, dim);
            foreach (var label in group)
                DrawLabel(canvas, label);
        }
        foreach (var marker in layout.Markers.Where(m => m.Focused))
            Draw(marker);
        foreach (var label in layout.Labels.Where(l => l.Of.Focused))
            DrawLabel(canvas, label);
        DrawGuidePlate(canvas, layout.Guide, uiScale);
        foreach (var chevron in layout.Chevrons)
        {
            // What is pointed at pulses where it is in view; out of view, its chevron does (owner, 2026-10-04: "if a
            // quest marker is outside of the current viewport, it should probably indicate that").
            if (chevron.Pointed && scene.Pulsing)
                DrawPulse(canvas, scene, chevron.At, 7 * uiScale, chevron.Color, uiScale);
            DrawChevron(canvas, chevron, uiScale);
        }
        DrawScaleBar(canvas, layout.Scale, uiScale);
        DrawPlayer(canvas, camera, scene, uiScale);
    }

    // ---- stepping back: what is outside the focus while something is highlighted ----

    /// <summary>How far a marker outside the focus steps back once the dimming is complete.</summary>
    /// <param name="Alpha">Its opacity, 1 for full strength.</param>
    /// <param name="Saturation">How much of its colour it keeps, 1 for all.</param>
    /// <param name="LabelAlpha">Its label's opacity: labels step back further than their symbols.</param>
    public readonly record struct StepBack(float Alpha, float Saturation, float LabelAlpha);

    // Not stepping back at all: picks.
    private static readonly StepBack Full = new(1f, 1f, 1f);

    /// <summary>
    /// How far each kind steps back while something else is highlighted (owner, 2026-10-03: at 28 % the other markers
    /// could barely be made out, "but are still pretty important", above all in a raid). Ways out (your side's
    /// extracts and transits) and bosses never step back: they matter at a glance whatever is highlighted. Other
    /// quests' markers fade to about two thirds while planning and much less in a raid, where a quest is often kept
    /// highlighted all raid; spawn rings fade like them, and so do the hazard areas (level 4, with locks and
    /// switches). Labels step back further than symbols, so the highlighted quest's names stand out without hiding
    /// where everything else is. A zone steps back by the measure of its kind's markers (<see cref="ZoneStrength"/>).
    /// </summary>
    public static StepBack StepBackOf(MarkerKind kind, bool inRaid) => kind switch
    {
        MarkerKind.ExtractPmc or MarkerKind.ExtractScav or MarkerKind.ExtractShared or MarkerKind.Transit or MarkerKind.BossSpawn
            => new(1f, 1f, inRaid ? 1f : 0.7f),
        MarkerKind.ScavSpawn or MarkerKind.SniperSpawn or MarkerKind.Lock or MarkerKind.Switch or MarkerKind.Hazard
            => inRaid ? new(0.75f, 1f, 0.6f) : new(0.6f, 1f, 0.5f),
        _ => inRaid ? new(0.8f, 1f, 0.6f) : new(0.62f, 1f, 0.45f),
    };

    // A picked quest's marker, or the door of a key a picked quest needs: part of the pick.
    private static bool IsPick(MapScene scene, ShownMarker m) => m.Kept || IsKeptKey(scene, m.Marker);

    /// <summary>
    /// How far a marker steps back while something else is pointed at: not at all for the picks and for the doors of the
    /// keys they need, which read as part of the pick (the review of 2026-10-04: the doors stepped back like any
    /// lock), else by its kind.
    /// </summary>
    /// <param name="byKind"><see cref="StepBackOf(MarkerKind, bool)"/> unless a developer render compares alternatives.</param>
    public static StepBack StepBackOf(MapScene scene, ShownMarker marker, Func<MarkerKind, bool, StepBack>? byKind = null) =>
        IsPick(scene, marker) ? Full : (byKind ?? StepBackOf)(marker.Marker.Kind, scene.InRaid);

    /// <summary>
    /// How strongly a zone is drawn while something is pointed at, 1 for full strength: a picked or pointed-at
    /// quest's zone at full strength, any other by the measure of its kind's markers, eased with the scene's Dim (the
    /// review of 2026-10-04: other quests' zones fell to about 35 % where their markers keep 62 % and, in a raid, 80 %).
    /// </summary>
    public static float ZoneStrength(MapScene scene, MapZone zone, Func<MarkerKind, bool, StepBack>? byKind = null)
    {
        if (scene.ShownFocus.Count == 0)
            return 1;
        if (zone.Group is not null && (scene.Kept.Contains(zone.Group) || scene.ShownFocus.Contains(zone.Group)))
            return 1;
        return 1 - (1 - (byKind ?? StepBackOf)(zone.Kind, scene.InRaid).Alpha) * scene.Dim;
    }

    // A hazard area's name steps back as its kind's labels do.
    private static float HazardLabelStrength(MapScene scene, Func<MarkerKind, bool, StepBack>? byKind) =>
        scene.ShownFocus.Count == 0 ? 1 : 1 - (1 - (byKind ?? StepBackOf)(MarkerKind.Hazard, scene.InRaid).LabelAlpha) * scene.Dim;

    // A layer that steps what is drawn into it back by a measure, eased with the dim; no layer when it changes nothing.
    private readonly ref struct StepBackLayer
    {
        private readonly SKCanvas? _canvas;

        public StepBackLayer(SKCanvas canvas, float alpha, float saturation, float dim)
        {
            var a = 1 - (1 - alpha) * dim;
            var s = 1 - (1 - saturation) * dim;
            if (a >= 0.999f && s >= 0.999f)
                return;
            using var filter = s < 0.999f ? ArtworkColors.Recede(s, 1 - 0.1f * (1 - s)) : null;
            using var paint = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(255 * a)), ColorFilter = filter };
            canvas.SaveLayer(paint);
            _canvas = canvas;
        }

        public void Dispose() => _canvas?.Restore();
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

        /// <summary>
        /// The very thing pointed at, which pulses while the pointer is on it: every marker in focus, or with one
        /// objective pointed at (<see cref="MapScene.FocusObjective"/>) only that objective's places. The rest of the
        /// focus is lit and holds still.
        /// </summary>
        public bool Pointed { get; init; }

        /// <summary>An extract the game's own list didn't name this raid (<see cref="MapScene.ExitsNotListed"/>): hollow.</summary>
        public bool NotListed { get; init; }

        /// <summary>An extract on the player's list this raid (<see cref="MapScene.ExitsListed"/>): lit.</summary>
        public bool Listed { get; init; }

        /// <summary>Its true place, while it stands on an opened stack's ring (<see cref="MapScene.FanAt"/>): a hairline
        /// joins the two.</summary>
        public SKPoint? Home { get; init; }

        /// <summary>The middle of the opened stack it stands in (the place of the symbol the pointer rested on).</summary>
        public SKPoint FanHub { get; init; }

        /// <summary>A listed extract the game marked "??:??:??" (<see cref="MapScene.ExitsUnsure"/>): a "?" at its corner.</summary>
        public bool Unsure { get; init; }

        /// <summary>Drawn beside its true place, for a neighbour of the same rank that would cover it or be covered
        /// (<see cref="SideBySide"/>): <see cref="At"/> is where the symbol stands, a few pixels off.</summary>
        public bool Beside { get; init; }
    }

    /// <summary>A placed label: the marker's own, or a map name (rotated about its anchor).</summary>
    public sealed record PlacedLabel(string Text, SKRect Box, float Baseline, float Size, bool Bold, SKColor Color, ShownMarker Of);

    /// <summary>A map name that fits, rotated about its anchor.</summary>
    public sealed record PlacedName(string Text, SKPoint At, float Rotation, float Size, SKRect Box)
    {
        /// <summary>A landmark's name (tarkov.dev size 90 and up): semi-bold, letter-spaced.</summary>
        public bool Landmark { get; init; }
    }

    /// <summary>Everything placed in one frame: markers in drawing order, their labels, the map's names, the guide and the scale.</summary>
    public sealed record MapLayout(IReadOnlyList<ShownMarker> Markers, IReadOnlyList<PlacedLabel> Labels, IReadOnlyList<PlacedName> Names,
        GuideLine? Guide, ScaleBar Scale)
    {
        /// <summary>Where the highlighted quest's places out of view lie: chevrons at the edge.</summary>
        public IReadOnlyList<EdgeChevron> Chevrons { get; init; } = [];
    }

    /// <summary>What a layout is made from, apart from the scene's own data, which <see cref="MapScene.LayoutVersion"/> counts.</summary>
    /// <param name="Fading">Whether the last focus is still drawn while its dimming fades out (<see cref="MapScene.ShownFocus"/>).</param>
    /// <param name="Age">The age tag beside the player and on the guide's plate: the one part that follows the clock.</param>
    internal readonly record struct LayoutKey(int Scene, MapPoint Center, double Zoom, SKSize Viewport, float Ui, bool Fading, bool Sheet, string Age);

    /// <summary>
    /// The layout for a frame: the last frame's while nothing it is made from changed, else a new one
    /// (<see cref="Layout"/>). While a pointed-at quest pulses or a new position pings, the map is drawn again about 60
    /// times a second and nothing in it moves; placing every marker and label anew for each of those frames was most
    /// of a frame's work (the review of 2026-10-04, A17). The view, the display's scale and the scene's data decide
    /// the layout; the clock only through the player's age tag, which changes with the minute.
    /// </summary>
    public static MapLayout LayoutOf(Camera camera, MapScene scene, float ui)
    {
        var age = scene.Player is { } player && Shturmap.Core.Logs.WallClock.Elapsed(player.At, DateTime.Now) is var old && old >= PlayerOld ? AgeTag(old).Text : "";
        var key = new LayoutKey(scene.LayoutVersion, camera.Center, camera.Zoom, camera.Viewport, ui, scene.Dim > 0, scene.IsSheet, age);
        if (scene.LastLayout is { } last && last.Key == key)
            return last.Layout;
        var layout = Layout(camera, scene, ui);
        scene.LastLayout = (key, layout);
        return layout;
    }

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
            if (ShowsOptional(m))
                taken.Add(OptionalBadgeBox(m, ui));
            if (ShowsPossible(m))
                taken.Add(PossibleBadgeBox(m, ui));
            taken.Add(Square(m.At, m.Reach));
            if (m.Floor != 0)
                taken.Add(FloorBadgeBox(m, ui));
        }
        if (scene.Player is { } player)
        {
            var at = Screen(camera, scene, player.Position);
            taken.Add(Square(at, (PlayerRing + 3) * ui));
            var age = Shturmap.Core.Logs.WallClock.Elapsed(player.At, DateTime.Now);
            if (age >= PlayerOld)
            {
                var (text, stale) = AgeTag(age);
                using var tagFont = new SKFont(TypefaceBold, AgeTagSize(stale) * ui);
                taken.Add(AgeTagBox(at, tagFont.MeasureText(text), stale, ui));
            }
        }
        // After every symbol: the guide's plate gives way to them. The guide ends, and the chevrons count, where the
        // symbols are drawn: a marker set beside its place (SideBySide) is there for them too.
        var drawnAt = markers.Where(m => m.Count == 1).GroupBy(m => m.Marker.Id).ToDictionary(g => g.Key, g => g.First().At, StringComparer.Ordinal);
        var guide = Guide(camera, scene, ui, taken, drawnAt);
        if (guide?.Plate is not null)
            taken.Add(guide.PlateBox);
        var scale = Scale(camera, scene, ui);
        var chevrons = Chevrons(camera, scene, ui, drawnAt);
        taken.AddRange(chevrons.Select(c => Square(c.At, 10 * ui)));
        taken.Add(scale.Box);

        var labels = new List<PlacedLabel>();
        using var regular = new SKFont(Typeface, 11.5f * ui);
        using var bold = new SKFont(TypefaceBold, 13 * ui);
        // A lock's key and a switch's name are said close up, or for the one pointed at.
        var landmarkLabels = ZoomOverOverview(camera, scene, ui) >= LandmarkLabelFromZoom;
        foreach (var m in markers.Where(m => m.Marker.Label.Length > 0 && m.Marker.Kind != MarkerKind.ObjectiveDone && (scene.ShowLabels || m.Selected)
                                             && (m.Selected || landmarkLabels || !IsLandmark(m.Marker.Kind)))
                     .OrderBy(LabelRank).ThenByDescending(m => m.Count))
        {
            // A name is said once per neighbourhood: eight "Abandoned Cargo" labels in one block say no more than one.
            if (labels.Any(l => l.Text == m.Marker.Label && SKPoint.Distance(l.Of.At, m.At) < LabelRepeat * ui))
                continue;
            // An extract on the player's list this raid says its name in bold, as what is picked or pointed at does.
            var font = m.Selected || m.Listed ? bold : regular;
            var width = font.MeasureText(m.Marker.Label);
            var gap = 4 * ui;
            SKRect? box = null;
            var candidates = LabelCandidates(m.At, m.Reach + gap, width, font.Size);
            // One of a pair set side by side has its neighbour on one side and, as often as not, that one's name
            // below: its own may stand a line further down, where the two read as the pair's caption.
            if (m.Beside)
            {
                // A little further than one line: the neighbour's symbol may be the taller, and its name the lower.
                var below = candidates.Last();
                candidates = candidates.Append(SKRect.Create(below.Left, below.Bottom + 3 * ui, below.Width, below.Height));
            }
            foreach (var candidate in candidates)
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
            // Ink for every label at rest, a boss's too: its red octagon is the danger sign, and red type on the dark
            // ground read worse than any other label (the review of 2026-10-04). What is picked or pointed at takes its
            // marker's colour.
            // An extract that isn't on the player's list this raid steps back, name and symbol (MapScene.ExitsNotListed).
            var color = m.Selected ? m.Color : m.NotListed ? Muted : Ink;
            labels.Add(new PlacedLabel(m.Marker.Label, placed, placed.Top + font.Size * 1.1f, font.Size, m.Selected || m.Listed, color, m));
        }

        var names = new List<PlacedName>();
        if (scene.ShowLabels)
        {
            var zoom = ZoomOverOverview(camera, scene, ui);
            var screen = SKRect.Create(0, 0, camera.Viewport.Width, camera.Viewport.Height);
            using var street = new SKFont(Typeface, 11 * ui);
            using var landmark = new SKFont(TypefaceBold, 12 * ui);
            foreach (var label in scene.Definition.Labels.OrderByDescending(l => l.Size ?? StreetSize))
            {
                // A label with heights belongs to one floor and shows only with it, as on tarkov.dev (The Lab's rooms
                // would print over each other otherwise).
                if (label.Height is { } h && FloorOffset(scene, new WorldPoint(label.X, (h.Min + h.Max) / 2, label.Z)) != 0)
                    continue;
                var tier = NameTier(label.Size);
                if (zoom < tier.FromZoom)
                    continue;
                var text = label.Text.ToUpperInvariant();
                var at = camera.ToScreen(scene.Projection.ToMap(label.X, label.Z));
                var font = tier.Landmark ? landmark : street;
                var width = NameWidth(font, text, tier.Landmark ? NameTracking * ui : 0);
                var box = Rotated(SKRect.Create(at.X - width / 2, at.Y - font.Size, width, font.Size * 1.3f), at, (float)label.Rotation);
                if (!box.IntersectsWith(screen) || taken.Any(t => t.IntersectsWith(box)))
                    continue;
                taken.Add(box);
                names.Add(new PlacedName(text, at, (float)label.Rotation, font.Size, box) { Landmark = tier.Landmark });
            }
        }
        return new MapLayout(markers, labels, names, guide, scale) { Chevrons = chevrons };
    }

    /// <summary>How near (pixels, before scaling) two markers with the same label may be before only one is labelled.</summary>
    public const float LabelRepeat = 250;

    // Label priority: what the player picked, then bosses, quests, ways out, snipers, locks and switches.
    // An extract on the player's list this raid is placed with the bosses, ahead of other ways out and of quests: at
    // the raid's end it is the name to find.
    private static int LabelRank(ShownMarker m) => m.Selected ? 0 : m.Listed ? 1 : RestRank(m.Marker.Kind);

    /// <summary>A kind's rank at rest, whatever is picked or pointed at: bosses, quests, ways out, Scav and sniper
    /// zones, locks and switches. Symbols of one rank are set side by side where they would cover each other
    /// (<see cref="SideBySide"/>); of two ranks, the one that matters more lies on top.</summary>
    public static int RestRank(MarkerKind kind) => kind switch
    {
        MarkerKind.BossSpawn => 1,
        MarkerKind.Objective or MarkerKind.PossibleLocation or MarkerKind.ObjectiveDone => 2,
        MarkerKind.ExtractPmc or MarkerKind.ExtractScav or MarkerKind.ExtractShared or MarkerKind.Transit => 3,
        MarkerKind.Lock or MarkerKind.Switch => 5,
        _ => 4,
    };

    /// <summary>Half a symbol's width at rest (pixels): the size it is drawn at when nothing is picked or pointed at.</summary>
    public static float RestHalf(MapMarker marker, float ui) => marker switch
    {
        { Kind: MarkerKind.ObjectiveDone } => 8f,
        { Objective: not null } => 10f,
        // A triangle is a little wider than its radius, a diamond exactly as wide.
        { Kind: MarkerKind.ExtractPmc or MarkerKind.ExtractScav or MarkerKind.ExtractShared } => 7.5f * 1.1f,
        { Kind: MarkerKind.Transit } => 7.5f,
        { Kind: MarkerKind.BossSpawn } => 6f * 1.2f,
        { Kind: MarkerKind.SniperSpawn } => 6.5f,
        { Kind: MarkerKind.ScavSpawn } => 4f,
        { Kind: MarkerKind.Lock or MarkerKind.Switch } => 5.5f,
        _ => 6f,
    } * ui;

    // Three symbols in a row settle in two or three passes; more than that is a crowd no pass count sorts out.
    private const int SideBySidePasses = 4;

    /// <summary>
    /// Where symbols stand that would cover each other: two of the same rank (two ways out, two quests' places, a
    /// lock beside a lock) closer than their widths allow are set side by side, each moved by half of what is missing
    /// and never further from its true place than its own width. Before, the one drawn later covered the other: on
    /// Streets a transit's diamond lay over an extract's triangle at the same spot (the review of 2026-10-04, C6).
    /// They part along the line between their true places, so as the view zooms in and those draw apart, each
    /// symbol comes straight back to its own; where they share a place, left and right, the earlier one on the left.
    /// Symbols of different rank aren't moved: the one that matters more lies on top (Render). The sizes are the
    /// symbols' at rest (<see cref="RestHalf"/>), so nothing moves because the pointer is on it.
    /// </summary>
    /// <param name="symbols">Each symbol's true place on screen, half its width at rest, and its rank.</param>
    /// <param name="gap">The room kept between two symbols' edges: their dark collars.</param>
    public static SKPoint[] SideBySide(IReadOnlyList<(SKPoint At, float Half, int Rank)> symbols, float gap)
    {
        var at = symbols.Select(s => s.At).ToArray();
        for (var pass = 0; pass < SideBySidePasses; pass++)
        {
            var moved = false;
            for (var i = 0; i < symbols.Count; i++)
            {
                for (var j = i + 1; j < symbols.Count; j++)
                {
                    if (symbols[i].Rank != symbols[j].Rank)
                        continue;
                    var want = symbols[i].Half + symbols[j].Half + gap;
                    var apart = SKPoint.Distance(at[i], at[j]);
                    if (apart >= want - 0.01f)
                        continue;
                    var line = symbols[j].At - symbols[i].At;
                    var along = line.Length < 0.5f ? new SKPoint(1, 0) : new SKPoint(line.X / line.Length, line.Y / line.Length);
                    var push = (want - apart) / 2;
                    at[i] = Within(new SKPoint(at[i].X - along.X * push, at[i].Y - along.Y * push), symbols[i].At, 2 * symbols[i].Half);
                    at[j] = Within(new SKPoint(at[j].X + along.X * push, at[j].Y + along.Y * push), symbols[j].At, 2 * symbols[j].Half);
                    moved = true;
                }
            }
            if (!moved)
                break;
        }
        return at;
    }

    // A point no further from its origin than a reach.
    private static SKPoint Within(SKPoint p, SKPoint origin, float reach)
    {
        var d = p - origin;
        return d.Length <= reach ? p : new SKPoint(origin.X + d.X / d.Length * reach, origin.Y + d.Y / d.Length * reach);
    }

    /// <summary>Locks and switches: level-4 landmarks from tarkov.dev's data (docs/DESIGN.md, "Map drawing").</summary>
    public static bool IsLandmark(MarkerKind kind) => kind is MarkerKind.Lock or MarkerKind.Switch;

    /// <summary>
    /// From which zoom (a multiple of the overview, as for the map's names) locks and switches show on a map with
    /// artwork: Streets has 63 locks, Customs 36, Reserve 34, too many for the overview. On a sheet they show at any
    /// zoom (they are among the few things it has). What is pointed at shows at any zoom.
    /// </summary>
    public const double LandmarkFromZoom = 1.5;

    /// <summary>From which zoom their labels (the key's short name, the switch's name) show unless pointed at.</summary>
    public const double LandmarkLabelFromZoom = 2.5;

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
    /// the view zooms in. Markers out of view are left out (their labels would take the place of the ones in view),
    /// and before places are merged: a group that runs out of view keeps a marker for its places in view, at one of
    /// them. Merged first, the whole group went when its middle place was out of view (the review of 2026-10-04).
    /// Last, symbols of one rank that would still cover each other are set side by side (<see cref="SideBySide"/>):
    /// their <see cref="ShownMarker.At"/> is where they are drawn, and <see cref="ShownMarker.Beside"/> says so.
    /// </summary>
    public static List<ShownMarker> ShownMarkers(Camera camera, MapScene scene, float ui)
    {
        var view = SKRect.Create(0, 0, camera.Viewport.Width, camera.Viewport.Height);
        view.Inflate(40 * ui, 40 * ui);
        // Locks and switches are level 4: from a zoom on maps with artwork, and on the floor shown; the ones pointed at
        // show anyway, with their floor arrow (The Lab's other floors would otherwise cover the sheet with arrows).
        var landmarks = scene.IsSheet || ZoomOverOverview(camera, scene, ui) >= LandmarkFromZoom;
        var shown = scene.Markers.Select((m, i) => (Index: i, Shown: Show(camera, scene, m, ui)))
            .Where(s => view.Contains(s.Shown.At))
            .Where(s => !IsLandmark(s.Shown.Marker.Kind) || s.Shown.Selected || (landmarks && s.Shown.Floor == 0))
            .ToList();
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
        // Symbols of one rank that would cover each other stand side by side (SideBySide). In the data's order, not
        // the drawing order below: which of two goes left must not turn on what is pointed at.
        result.Sort((a, b) => a.Index.CompareTo(b.Index));
        var places = SideBySide(result.Select(r => (r.Shown.At, RestHalf(r.Shown.Marker, ui), RestRank(r.Shown.Marker.Kind))).ToList(), MarkerCollar * ui);
        var list = result
            .Select((r, i) => (r.Index, Shown: places[i] == r.Shown.At ? r.Shown : r.Shown with { At = places[i], Beside = true }))
            .OrderBy(r => r.Shown.Focused ? 2 : r.Shown.Selected ? 1 : 0)
            .ThenBy(r => r.Index)
            .Select(r => r.Shown)
            .ToList();
        if (scene.FanAt is { } fan)
            FanOut(list, fan, scene.FanProgress, ui);
        return list;
    }

    // A symbol's body, without the pick's ring: what a neighbour must keep clear of.
    private static float Body(ShownMarker m, float ui) => m.Reach - 1 * ui - (m.Kept ? 7.5f * ui : 0);

    // The symbols that touch one: the stack an opened fan stands on its ring.
    private static List<int> StackOf(IReadOnlyList<ShownMarker> list, int f, float ui) =>
        Enumerable.Range(0, list.Count).Where(i => SKPoint.Distance(list[i].At, list[f].At) < list[f].Reach + Body(list[i], ui)).ToList();

    /// <summary>
    /// Whether resting the pointer on this marker opens its stack: another symbol lies well over it, centres closer
    /// than 60 % of their bodies together, so one of them is mostly hidden. Symbols that only touch can each be
    /// pointed at as they are, and their cards open as ever.
    /// </summary>
    public static bool Stacked(IReadOnlyList<ShownMarker> markers, string markerId, float ui)
    {
        var list = markers.ToList();
        var f = list.FindIndex(m => m.Marker.Id == markerId && m.Home is null);
        return f >= 0 && list.Where((m, i) => i != f && m.Home is null)
            .Any(m => SKPoint.Distance(m.At, list[f].At) < 0.6f * (Body(m, ui) + Body(list[f], ui)));
    }

    /// <summary>The opened stack's plate, where the pointer keeps it open: its middle and radius; null with none.</summary>
    public static (SKPoint Hub, float Radius)? FanPlate(IReadOnlyList<ShownMarker> markers, float ui)
    {
        var moved = markers.Where(m => m.Home is not null).ToList();
        if (moved.Count == 0)
            return null;
        var hub = moved[0].FanHub;
        return (hub, moved.Max(m => SKPoint.Distance(m.At, hub) + m.Reach) + 3 * ui);
    }

    // The symbols that touch the one the pointer rests on, and it, stand on a ring around its place, each joined to its
    // own place by a hairline (owner, 2026-10-05, from the overlap panel: "Go for F"). They go out along the ring as
    // the stack opens (progress 0 to 1, eased out).
    private static void FanOut(List<ShownMarker> list, string fanAt, float progress, float ui)
    {
        var f = list.FindIndex(m => m.Marker.Id == fanAt);
        if (f < 0)
            return;
        var hub = list[f].At;
        // Round the ring in the order the symbols' own places lie around the hub, from the first of them, so the
        // hairlines don't cross; one at the hub itself goes last.
        float? AngleOf(int i) => (list[i].At - hub).Length < 0.5f ? null : MathF.Atan2(list[i].At.Y - hub.Y, list[i].At.X - hub.X);
        var stack = StackOf(list, f, ui).OrderBy(i => AngleOf(i) ?? float.MaxValue).ToList();
        if (stack.Count < 2)
            return;
        var widest = stack.Max(i => list[i].Reach);
        var gap = MarkerCollar * ui;
        var radius = Math.Max(widest * 1.7f + gap, stack.Count * (2 * widest + gap) / (2 * MathF.PI));
        var start = AngleOf(stack[0]) ?? -MathF.PI / 2;
        var eased = 1 - MathF.Pow(1 - Math.Clamp(progress, 0, 1), 3);
        for (var k = 0; k < stack.Count; k++)
        {
            var angle = start + k * 2 * MathF.PI / stack.Count;
            var i = stack[k];
            var home = list[i].At;
            var ring = new SKPoint(hub.X + radius * MathF.Cos(angle), hub.Y + radius * MathF.Sin(angle));
            list[i] = list[i] with
            {
                At = new SKPoint(home.X + (ring.X - home.X) * eased, home.Y + (ring.Y - home.Y) * eased),
                Home = home,
                FanHub = hub,
            };
        }
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

    // An optional objective's marker says so in a small badge at its upper left (owner, 2026-10-03: optional places
    // "might still be very relevant for a quest"). Words, because no shape, colour or ring on the map is free to mean
    // "optional" (one symbol, one meaning); the upper right is the floor arrow's, the lower right the count's.
    private const string OptionalTag = "OPT";

    private static bool ShowsOptional(ShownMarker m) => m.Marker.Optional && m.Marker.Kind is MarkerKind.Objective or MarkerKind.PossibleLocation;

    private static SKRect OptionalBadgeBox(ShownMarker m, float ui)
    {
        using var font = new SKFont(TypefaceBold, 8.5f * ui);
        var width = font.MeasureText(OptionalTag) + 6 * ui;
        var c = new SKPoint(m.At.X - m.R * 0.85f, m.At.Y - m.R * 0.85f);
        return SKRect.Create(c.X - width / 2, c.Y - 6 * ui, width, 12 * ui);
    }

    private static void DrawOptionalBadge(SKCanvas canvas, ShownMarker m, float ui)
    {
        var box = OptionalBadgeBox(m, ui);
        using var plate = new SKPaint { Color = Background, IsAntialias = true };
        using var edge = new SKPaint { Color = m.Color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.1f * ui };
        using var font = new SKFont(TypefaceBold, 8.5f * ui);
        using var paint = new SKPaint { Color = m.Color, IsAntialias = true };
        canvas.DrawRect(box, plate);
        canvas.DrawRect(box, edge);
        canvas.DrawText(OptionalTag, box.MidX, box.MidY + font.Size * 0.36f, SKTextAlign.Center, font, paint);
    }

    // One of the places the thing can be: a "?" at the marker's lower left (the floor is upper right, the count
    // lower right, "OPT" upper left).
    private const string PossibleTag = "?";

    // An extract the game's list marked "??:??:??" carries it too: maybe open (MapScene.ExitsUnsure).
    private static bool ShowsPossible(ShownMarker m) => m.Marker.Kind == MarkerKind.PossibleLocation || m.Unsure;

    private static SKRect PossibleBadgeBox(ShownMarker m, float ui)
    {
        var c = new SKPoint(m.At.X - m.R * 0.85f, m.At.Y + m.R * 0.85f);
        return SKRect.Create(c.X - 5.5f * ui, c.Y - 6 * ui, 11 * ui, 12 * ui);
    }

    private static void DrawPossibleBadge(SKCanvas canvas, ShownMarker m, float ui)
    {
        var box = PossibleBadgeBox(m, ui);
        using var plate = new SKPaint { Color = Background, IsAntialias = true };
        using var edge = new SKPaint { Color = m.Color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.1f * ui };
        using var font = new SKFont(TypefaceBold, 10 * ui);
        using var paint = new SKPaint { Color = m.Color, IsAntialias = true };
        canvas.DrawRect(box, plate);
        canvas.DrawRect(box, edge);
        canvas.DrawText(PossibleTag, box.MidX, box.MidY + font.Size * 0.36f, SKTextAlign.Center, font, paint);
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
        // A done objective is no place to go to any more: it keeps its quiet look (a small muted disc with a check),
        // at its rest size and without a name, also while its quest is picked or pointed at (owner, 2026-10-04: an
        // objective the player ticked as done).
        var done = marker.Kind == MarkerKind.ObjectiveDone;
        // With one objective pointed at, the quest's other places stay lit at their rest size; the doors of its keys
        // belong to no objective and keep the pointed-at look (at rest they wouldn't show from far out at all).
        var pointed = IsPointed(scene, marker);
        var selected = !done && (IsSelected(scene, marker) || (marker.Objective is null ? focused : pointed));
        // Picked quests' markers are drawn in their own colour, with a ring.
        var kept = marker.Objective is not null && !done && IsSelected(scene, marker);
        // A lock a picked quest needs the key of is part of the pick: the picks' colour, without their ring.
        var color = kept ? PickColor(scene, marker.Group) : IsKeptKey(scene, marker) ? PickColor(scene, scene.PickOfKey(marker.Group!)) : ColorOf(marker.Kind);
        // Quest markers carry a type glyph, so they are drawn largest. Extracts and transits (level 2) are as large as
        // the boss octagon (level 3): a 15 px triangle or diamond. Every symbol keeps its size when picked or pointed
        // at; its colour, ring and pulse say it (owner, 2026-10-05, from the overlap panel: "Go for F"). Until then a
        // pick grew to 14 px and anything pointed at by a fifth, over neighbours set apart for their rest size.
        var r = marker switch
        {
            { Objective: not null } => 10f,
            { Kind: MarkerKind.ExtractPmc or MarkerKind.ExtractScav or MarkerKind.ExtractShared or MarkerKind.Transit } => 7.5f,
            { Kind: MarkerKind.Lock or MarkerKind.Switch } => 5.5f,
            _ => 6f,
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
            Pointed = pointed && !done,
            NotListed = scene.ExitsNotListed.Contains(marker.Id),
            Listed = scene.ExitsListed.Contains(marker.Id),
            Unsure = scene.ExitsUnsure.Contains(marker.Id),
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

    // ---- the map's own names, by tarkov.dev's label size ----

    /// <summary>The size tarkov.dev's street names have; names without a size are treated as streets.</summary>
    private const double StreetSize = 80;

    /// <summary>The extra space between a landmark name's letters (pixels, before scaling).</summary>
    private const float NameTracking = 1.2f;

    /// <summary>
    /// How a map name of a tarkov.dev label size is set (cartography review, 2026-10-02): landmarks (90 and up) in
    /// 12 px semi-bold spaced caps, streets (80, or no size) always, shops (65–70) from 1.5 times the overview zoom,
    /// the smallest (60) from 2.5 times.
    /// </summary>
    public static (bool Landmark, double FromZoom) NameTier(double? size) => size switch
    {
        null => (false, 0),
        >= 85 => (true, 0),
        >= 75 => (false, 0),
        >= 65 => (false, 1.5),
        _ => (false, 2.5),
    };

    /// <summary>The zoom as a multiple of the zoom that shows the whole map in this view.</summary>
    public static double ZoomOverOverview(Camera camera, MapScene scene, float ui)
    {
        var rect = scene.Projection.WorldRect;
        var padding = 24 * ui;
        var overview = Math.Min((camera.Viewport.Width - 2 * padding) / Math.Max(rect.Width, 1e-6), (camera.Viewport.Height - 2 * padding) / Math.Max(rect.Height, 1e-6));
        return camera.Zoom / overview;
    }

    private static float NameWidth(SKFont font, string text, float tracking) =>
        tracking == 0 ? font.MeasureText(text) : text.Sum(c => font.MeasureText(c.ToString())) + tracking * Math.Max(0, text.Length - 1);

    // The map's own names: Ink at 59 % on a halo of the ground, so they read on light streets (1.4:1 without it,
    // 4.7:1 with it, cartography review 2026-10-02).
    private static void DrawMapName(SKCanvas canvas, PlacedName name, float ui)
    {
        using var font = new SKFont(name.Landmark ? TypefaceBold : Typeface, name.Size);
        using var halo = new SKPaint { Color = Background.WithAlpha(220), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 * ui };
        using var paint = new SKPaint { Color = Ink.WithAlpha(150), IsAntialias = true };
        canvas.Save();
        canvas.RotateDegrees(name.Rotation, name.At.X, name.At.Y);
        if (!name.Landmark)
        {
            canvas.DrawText(name.Text, name.At.X, name.At.Y, SKTextAlign.Center, font, halo);
            canvas.DrawText(name.Text, name.At.X, name.At.Y, SKTextAlign.Center, font, paint);
        }
        else
        {
            // Spaced caps, letter by letter: Skia has no letter spacing.
            var tracking = NameTracking * ui;
            foreach (var paintOf in new[] { halo, paint })
            {
                var x = name.At.X - NameWidth(font, name.Text, tracking) / 2;
                foreach (var c in name.Text)
                {
                    var letter = c.ToString();
                    canvas.DrawText(letter, x, name.At.Y, SKTextAlign.Left, font, paintOf);
                    x += font.MeasureText(letter) + tracking;
                }
            }
        }
        canvas.Restore();
    }

    // A marker of a picked quest (or a picked marker): the kept look.
    private static bool IsSelected(MapScene scene, MapMarker m) =>
        scene.Kept.Count > 0 && (scene.Kept.Contains(m.Id) || (m.Group is not null && scene.Kept.Contains(m.Group)) || IsKeptKey(scene, m));

    // A lock whose key a picked quest needs on this map (MapScene.KeptKeys).
    private static bool IsKeptKey(MapScene scene, MapMarker m) =>
        m.Kind == MarkerKind.Lock && m.Group is not null && scene.KeptKeys.Contains(m.Group);

    private static bool IsFocused(MapScene scene, MapMarker m) =>
        scene.ShownFocus.Contains(m.Id) || (m.Group is not null && scene.ShownFocus.Contains(m.Group));

    // The very thing pointed at: everything in focus, or with one objective pointed at (MapScene.FocusObjective) only
    // that objective's places. It pulses; the rest of the focus is lit and holds still.
    private static bool IsPointed(MapScene scene, MapMarker m) =>
        IsFocused(scene, m) && (scene.ShownFocusObjective is not { } objective || ObjectiveOf(m) == objective);

    // A zone's objective (its id is "zone:<objective id>:<n>"), or null.
    private static string? ObjectiveOf(MapZone z) =>
        z.Id.StartsWith("zone:", StringComparison.Ordinal) && z.Id.LastIndexOf(':') is var end and > 5 ? z.Id[5..end] : null;

    /// <summary>
    /// How many floors above (+) or below (−) the one shown a point is, or 0 on it, counted in the map's floor list
    /// (read data). Floors without artwork of their own are drawn in the base layer, so they count as the ground.
    /// </summary>
    public static int FloorOffset(MapScene scene, WorldPoint p)
    {
        var stack = scene.FloorStack;
        if (stack.Count == 0)
            return 0;
        var layer = FloorResolver.LayerFor(scene.Definition, p);
        // A floor without artwork of its own isn't in the floor list: its building's floors in height order say which
        // way it is from the floor shown. Customs' 4th floor in the oil rig is drawn in the base layer, and counted as
        // the ground it read as below the 2nd floor (owner, 2026-10-05: a camera "7m up but the icon showing down").
        if (layer is not null && !stack.Contains(layer)
            && FloorResolver.Ladder(scene.Definition, p).ToList() is var ladder
            && ladder.IndexOf(scene.Floor) is var shown and >= 0 && ladder.IndexOf(layer) is var at and >= 0)
            return shown - at;
        int IndexOf(MapLayer? l)
        {
            // The same layer object, not the same SvgLayer: maps without artwork have no SvgLayer on any floor.
            var i = l is null ? -1 : stack.ToList().FindIndex(s => s is not null && s == l);
            return i >= 0 ? i : stack.ToList().IndexOf(null);
        }
        // The stack lists the top floor first.
        return IndexOf(scene.Floor) - IndexOf(layer);
    }

    // The floor arrow sits at the marker's upper right (the lower right is the cluster badge's); beside small symbols
    // it moves out so they stay visible.
    private static SKPoint FloorBadgeCenter(ShownMarker m, float ui)
    {
        var r = m.Marker.Kind switch
        {
            MarkerKind.ScavSpawn => 4 * ui,
            MarkerKind.SniperSpawn => 6.5f * ui,
            MarkerKind.BossSpawn => m.R * 1.25f,
            _ => m.R,
        };
        var d = r < 8 * ui ? r + 3 * ui : r * 0.8f;
        return new SKPoint(m.At.X + d, m.At.Y - d);
    }

    private static SKRect FloorBadgeBox(ShownMarker m, float ui)
    {
        var c = FloorBadgeCenter(m, ui);
        return Math.Abs(m.Floor) > 1 ? SKRect.Create(c.X - 6 * ui, c.Y - 6.5f * ui, 18 * ui, 13 * ui) : Square(c, 6 * ui);
    }

    // The other-floor arrow: a small dark disc with a chevron pointing up or down; two or more floors away, a small
    // plate with the chevron and the number of floors. In the marker's own colour, like its count and OPT badges: a
    // marker's badges are the marker's (the review of 2026-10-04: it was the player's sand, which says "you").
    private static void DrawFloorArrow(SKCanvas canvas, ShownMarker m, float ui)
    {
        var c = FloorBadgeCenter(m, ui);
        var offset = m.Floor;
        using var plate = new SKPaint { Color = Background, IsAntialias = true };
        using var rim = new SKPaint { Color = m.Color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f * ui };
        using var fill = new SKPaint { Color = m.Color, IsAntialias = true };
        var count = Math.Abs(offset);
        if (count > 1)
        {
            var box = FloorBadgeBox(m, ui);
            canvas.DrawRect(box, plate);
            canvas.DrawRect(box, rim);
            c = new SKPoint(box.Left + 5.5f * ui, box.MidY);
            using var font = new SKFont(TypefaceBold, 9.5f * ui);
            canvas.DrawText(count.ToString(System.Globalization.CultureInfo.InvariantCulture), box.Right - 5 * ui, box.MidY + font.Size * 0.36f, SKTextAlign.Center, font, fill);
        }
        else
        {
            canvas.DrawCircle(c, 5.5f * ui, plate);
            canvas.DrawCircle(c, 5.5f * ui, rim);
        }
        var h = (count > 1 ? 2.3f : 2.6f) * ui;
        var w = (count > 1 ? 2.8f : 3.2f) * ui;
        using var chevron = offset > 0
            ? Polygon(new(c.X, c.Y - h), new(c.X + w, c.Y + h * 0.8f), new(c.X - w, c.Y + h * 0.8f))
            : Polygon(new(c.X, c.Y + h), new(c.X + w, c.Y - h * 0.8f), new(c.X - w, c.Y - h * 0.8f));
        canvas.DrawPath(chevron, fill);
    }

    /// <summary>The marker under a screen point (pixels), nearest first, or null.</summary>
    public static MapMarker? HitTest(Camera camera, MapScene scene, SKPoint screen, float ui)
    {
        MapMarker? best = null;
        var bestDistance = float.MaxValue;
        // The markers of the frame on screen (LayoutOf): a pointer moving over a still map places nothing anew.
        foreach (var shown in LayoutOf(camera, scene, ui).Markers)
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

    // tarkov.dev's tile render (The Lab, Labyrinth, Icebreaker; docs/DESIGN.md §3): the base layer's tiles, and on another
    // floor that floor's tiles over the base, dimmed as for SVG floors. The same treatment as SVG artwork (the recede
    // filter), baked into each tile when it is decoded (MapTiles). Tiles are drawn without anti-aliasing, so neighbours
    // meet on whole pixels with no seam.
    private static void DrawTiles(SKCanvas canvas, Camera camera, MapScene scene, MapTiles tiles)
    {
        if (scene.Definition.TilePath is not { } basePath)
            return;
        var view = ViewOf(camera);
        var bounds = scene.Projection.WorldRect;
        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
        using var plain = new SKPaint();
        void Layer(string template)
        {
            canvas.Save();
            var matrix = camera.Matrix;
            canvas.Concat(in matrix);
            // Inside the map's bounds only: the renders run on to whole tiles, past their own edge (Labyrinth's border
            // line, its opaque background).
            canvas.ClipRect(new SKRect((float)bounds.Left, (float)bounds.Top, (float)bounds.Right, (float)bounds.Bottom));
            tiles.Draw(template, view, bounds, camera.Zoom, (image, source, target) =>
                canvas.DrawImage(image, source, new SKRect((float)target.Left, (float)target.Top, (float)target.Right, (float)target.Bottom), sampling, plain));
            canvas.Restore();
        }
        Layer(basePath);
        if (scene.Floor?.TilePath is { } floorPath && floorPath != basePath)
        {
            using var dim = new SKPaint { Color = Background.WithAlpha(150) };
            canvas.DrawRect(canvas.LocalClipBounds, dim);
            Layer(floorPath);
        }
        // What this frame drew stays in memory; a floor no longer shown may go.
        tiles.Shows(basePath, scene.Floor?.TilePath);
    }

    // The part of the map a camera shows, in map units.
    private static MapRect ViewOf(Camera camera)
    {
        var corner1 = camera.ToMap(new SKPoint(0, 0));
        var corner2 = camera.ToMap(new SKPoint(camera.Viewport.Width, camera.Viewport.Height));
        return new MapRect(Math.Min(corner1.X, corner2.X), Math.Min(corner1.Y, corner2.Y), Math.Max(corner1.X, corner2.X), Math.Max(corner1.Y, corner2.Y));
    }

    // While the sheet stands in: the tiles DrawTiles would draw are asked for, none drawn. A tile that failed waits
    // out MapTiles.RetryAfter before it is asked for again, so a frame costs no request by itself.
    private static void AskForTiles(Camera camera, MapScene scene, MapTiles tiles)
    {
        if (scene.Definition.TilePath is not { } basePath)
            return;
        var view = ViewOf(camera);
        var bounds = scene.Projection.WorldRect;
        tiles.Ask(basePath, view, bounds, camera.Zoom);
        if (scene.Floor?.TilePath is { } floorPath && floorPath != basePath)
            tiles.Ask(floorPath, view, bounds, camera.Zoom);
        tiles.Shows(basePath, scene.Floor?.TilePath);
    }

    // Maps without usable artwork (docs/DESIGN.md §3) get a sheet instead, drawn from data only: maps.json's bounds (the
    // extent tarkov.dev gives the map, not a traced outline) as a panel with a metric grid (10 m, every fifth line
    // stronger), so positions, distances and markers still read true. No walls: the data has none. The 10 m lines go
    // when they would crowd closer than 6 px. A caption in the sheet's corner says what it is.
    private static readonly SKColor SheetPanel = Palette.Sk(Palette.SheetPanel);
    private static readonly SKColor SheetEdge = Palette.Sk(Palette.LineStrong);
    private static readonly SKColor SheetMinor = Palette.Sk(Palette.SheetMinor);
    private static readonly SKColor SheetMajor = Palette.Sk(Palette.Line);
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

    /// <summary>A minefield or border-sniper zone from the data is left out over artwork that draws them itself; traps
    /// always show.</summary>
    public static bool HazardShown(MapZone zone, MapArtwork? artwork) => zone.Group switch
    {
        MapContentBuilder.MinefieldGroup => artwork is not { ShowsMinefields: true },
        MapContentBuilder.SniperZoneGroup => artwork is not { ShowsSniperZones: true },
        _ => true,
    };

    /// <summary>Hazard areas get a name from this zoom over the overview (like landmarks): "SNIPER ZONE".</summary>
    public static string? HazardLabel(MapZone zone) => zone.Group == MapContentBuilder.SniperZoneGroup ? "SNIPER ZONE" : null;

    /// <param name="hazardLabels">Collects hazard areas' names ("SNIPER ZONE", from <see cref="LandmarkFromZoom"/>) to draw
    /// after every zone; null draws none.</param>
    /// <param name="ground">The artwork's ground in screen space (<see cref="GroundShader"/>) that keeps a hazard to the
    /// drawn map; null draws it whole.</param>
    private static void DrawZone(SKCanvas canvas, Camera camera, MapScene scene, MapZone zone, float ui = 1,
        List<(string Text, SKPoint At)>? hazardLabels = null, SKShader? ground = null, Func<MarkerKind, bool, StepBack>? stepBack = null)
    {
        if (zone.Outline.Count < 3)
            return;
        var points = zone.Outline.Select(p => Screen(camera, scene, p)).ToArray();
        using var path = Polygon(points);
        if (zone.Kind == MarkerKind.Hazard)
        {
            if (!HazardShown(zone, scene.Artwork))
                return;
            DrawHazard(canvas, path, ZoneStrength(scene, zone, stepBack), ui, ground);
            // Named where it lies on the drawn map, not out in the empty space past its edge.
            var centre = new SKPoint(points.Average(p => p.X), points.Average(p => p.Y));
            if (hazardLabels is not null && HazardLabel(zone) is { } label && ZoomOverOverview(camera, scene, ui) >= LandmarkFromZoom
                && (scene.Artwork is not { } artwork || artwork.OnGround(ToViewBox(camera, scene, centre))))
                hazardLabels.Add((label, centre));
            return;
        }
        // A done objective's zone stays muted and quiet, also while its quest is picked or pointed at.
        var done = zone.Kind == MarkerKind.ObjectiveDone;
        var kept = !done && zone.Group is not null && scene.Kept.Contains(zone.Group);
        var color = kept ? PickColor(scene, zone.Group) : ColorOf(zone.Kind);
        // With one objective pointed at, only its own zone takes the pointed-at look; the quest's others stay lit.
        var focused = zone.Group is not null && scene.ShownFocus.Contains(zone.Group)
            && (scene.ShownFocusObjective is not { } objective || ObjectiveOf(zone) == objective);
        var selected = !done && (kept || focused);
        // Zones outside the focus ease back with the scene's Dim, by the measure of their markers (StepBackOf).
        var strength = ZoneStrength(scene, zone, stepBack);
        using var fill = new SKPaint { Color = color.WithAlpha((byte)Math.Round(selected ? 70 : 35 * strength)), IsAntialias = true };
        using var stroke = new SKPaint { Color = color.WithAlpha((byte)Math.Round(selected ? 230 : 140 * strength)), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = selected ? 2 : 1.2f };
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

    /// <summary>How strongly a lock's or switch's glyph is drawn when not pointed at (level 4).</summary>
    private const byte LandmarkAlpha = 200;

    // On a sheet, where loot containers stand: faint dots on the floor shown, so rooms and corridors show from real
    // points where no artwork draws them (owner, 2026-10-03). Never on artwork, where they would be clutter.
    private static void DrawContainers(SKCanvas canvas, Camera camera, MapScene scene, float ui)
    {
        if (scene.Containers.Count == 0)
            return;
        using var dot = new SKPaint { Color = Ink.WithAlpha(70), IsAntialias = true };
        var r = 1.6f * ui;
        var view = SKRect.Create(0, 0, camera.Viewport.Width, camera.Viewport.Height);
        foreach (var container in scene.Containers)
        {
            var at = Screen(camera, scene, container);
            if (view.Contains(at) && FloorOffset(scene, container) == 0)
                canvas.DrawCircle(at, r, dot);
        }
    }

    // A hazard area's name, set like a street name (Ink at 59 % on a halo of the ground), centred on the area.
    private static void DrawHazardLabel(SKCanvas canvas, string text, SKPoint at, float ui, float strength = 1)
    {
        using var font = new SKFont(Typeface, 10.5f * ui);
        using var halo = new SKPaint { Color = Background.WithAlpha(220), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 * ui };
        using var paint = new SKPaint { Color = Ink.WithAlpha((byte)Math.Round(150 * strength)), IsAntialias = true };
        canvas.DrawText(text, at.X, at.Y + 4 * ui, SKTextAlign.Center, font, halo);
        canvas.DrawText(text, at.X, at.Y + 4 * ui, SKTextAlign.Center, font, paint);
    }

    // A hazard tarkov.dev outlines (traps, minefields, border-sniper zones): a thin ink outline, hatched, an area style
    // nothing else uses. Thin and sparse, so the artwork reads through it (owner, 2026-10-03, on Customs' minefields: "big
    // white rectangles"), and over artwork only where it draws the map (GroundShader).
    // Its strength is 1, or less while something else is pointed at (ZoneStrength).
    private static void DrawHazard(SKCanvas canvas, SKPath path, float strength, float ui, SKShader? ground = null)
    {
        using var hatch = new SKPaint { Color = Ink.WithAlpha((byte)Math.Round(80 * strength)), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.8f * ui };
        using var edge = new SKPaint { Color = Ink.WithAlpha((byte)Math.Round(120 * strength)), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 * ui };
        using var hatchOnGround = OnGround(hatch, ground);
        using var edgeOnGround = OnGround(edge, ground);
        canvas.Save();
        canvas.ClipPath(path, antialias: true);
        // What is left to draw on: the area's part in view.
        foreach (var (from, to) in HatchLines(path.Bounds, canvas.LocalClipBounds, 5 * ui))
            canvas.DrawLine(from, to, hatch);
        canvas.Restore();
        canvas.DrawPath(path, edge);
    }

    /// <summary>
    /// The hatch of an area: lines at 45°, <paramref name="step"/> apart, counted from the corner of the area's
    /// <paramref name="box"/> so they keep their places as the view moves, and only the ones that cross the part
    /// <paramref name="seen"/>, each cut to it. Zoomed in, a border zone's box is thousands of pixels wide and high,
    /// and every one of its lines was drawn across the whole box for each frame, nearly all of it outside the window
    /// (the review of 2026-10-04, A37).
    /// </summary>
    public static IEnumerable<(SKPoint From, SKPoint To)> HatchLines(SKRect box, SKRect seen, float step)
    {
        if (step <= 0 || !seen.IntersectsWith(box))
            yield break;
        seen.Intersect(box);
        // A line is the points with x + y = c; the first runs through the box's upper left corner.
        var first = box.Left + box.Top;
        var from = Math.Max(0, (int)Math.Ceiling((seen.Left + seen.Top - first) / step));
        var to = (int)Math.Floor((seen.Right + seen.Bottom - first) / step);
        for (var k = from; k <= to; k++)
        {
            var c = first + k * step;
            yield return (new SKPoint(c - seen.Bottom, seen.Bottom), new SKPoint(c - seen.Top, seen.Top));
        }
    }

    // A paint's colour taken through the ground's alpha (a shader in place of the colour); null: as it is.
    private static SKShader? OnGround(SKPaint paint, SKShader? ground)
    {
        if (ground is null)
            return null;
        using var color = SKShader.CreateColor(paint.Color);
        paint.Shader = SKShader.CreateBlend(SKBlendMode.DstIn, color, ground);
        paint.Color = SKColors.Black;
        return paint.Shader;
    }

    /// <summary>
    /// The artwork's <see cref="MapArtwork.Ground"/> placed on the screen as the artwork is drawn (DrawArtwork), transparent
    /// past its edge; null without artwork (tiles and sheets keep their hazards whole: they lie inside the render).
    /// </summary>
    private static SKShader? GroundShader(Camera camera, MapScene scene)
    {
        if (scene.Artwork is not { Ground: { } ground } artwork)
            return null;
        var box = artwork.ViewBox;
        var place = scene.Placement;
        var matrix = camera.Matrix
            .PreConcat(SKMatrix.CreateTranslation((float)place.OffsetX, (float)place.OffsetY))
            .PreConcat(SKMatrix.CreateScale((float)place.Scale, (float)place.Scale))
            .PreConcat(SKMatrix.CreateTranslation(box.Left, box.Top))
            .PreConcat(SKMatrix.CreateScale(box.Width / ground.Width, box.Height / ground.Height));
        return ground.ToShader(SKShaderTileMode.Decal, SKShaderTileMode.Decal, new SKSamplingOptions(SKFilterMode.Linear), matrix);
    }

    // A screen point in the artwork's own coordinates (its viewBox), the inverse of DrawArtwork's placement.
    private static SKPoint ToViewBox(Camera camera, MapScene scene, SKPoint screen)
    {
        var map = camera.ToMap(screen);
        var place = scene.Placement;
        return new SKPoint((float)((map.X - place.OffsetX) / place.Scale), (float)((map.Y - place.OffsetY) / place.Scale));
    }

    // Where the item the pointer is on lies loose: small open squares, like an empty inventory cell. In ink, as the
    // padlocks of a key pointed at and the container dots are: what the map's data has, neither a quest's nor the
    // player's (the review of 2026-10-04: they were the player's sand).
    private static void DrawSpawns(SKCanvas canvas, Camera camera, MapScene scene, float ui)
    {
        if (scene.Spawns.Count == 0)
            return;
        using var cell = new SKPaint { Color = Background.WithAlpha(200), IsAntialias = true };
        using var edge = new SKPaint { Color = Ink, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f * ui };
        var half = 5f * ui;
        foreach (var spawn in scene.Spawns)
        {
            var at = Screen(camera, scene, spawn);
            var box = new SKRect(at.X - half, at.Y - half, at.X + half, at.Y + half);
            canvas.DrawRect(box, cell);
            canvas.DrawRect(box, edge);
        }
    }

    // A dashed line from the player to the nearest place of the picked quests.
    private static void DrawGuide(SKCanvas canvas, GuideLine? guide, float ui)
    {
        if (guide is null)
            return;
        using var line = new SKPaint
        {
            Color = guide.Color.WithAlpha(220), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f * ui,
            PathEffect = SKPathEffect.CreateDash([6 * ui, 5 * ui], 0),
        };
        canvas.DrawLine(guide.From, guide.To, line);
    }

    /// <summary>The guide from the player to the nearest place of the picked quests, and its distance on a plate.</summary>
    /// <param name="Plate">"69 m"; null when the line is too short to carry it. The distance alone: the position's age stood beside it until 2026-10-04 ("69 m · 4 MIN") and read as the minutes it takes to get there.</param>
    /// <param name="Color">The colour of the pick it leads to.</param>
    public sealed record GuideLine(SKPoint From, SKPoint To, double Metres, string? Plate, SKRect PlateBox, SKColor Color);

    /// <param name="symbols">The boxes of the symbols placed in this frame: the plate stands on none of them.</param>
    /// <param name="drawnAt">Where this frame's markers are drawn, by id: the line ends on the symbol, also when it
    /// stands beside its place (<see cref="SideBySide"/>).</param>
    private static GuideLine? Guide(Camera camera, MapScene scene, float ui, IReadOnlyList<SKRect> symbols, IReadOnlyDictionary<string, SKPoint> drawnAt)
    {
        if (scene.Player is not { } player || scene.Kept.Count == 0)
            return null;
        // The guide leads to the picked quests' places, never to a door they need (a means, not a goal).
        var targets = scene.Markers.Where(m => m.Objective is not null && IsSelected(scene, m) && m.Kind != MarkerKind.ObjectiveDone).ToList();
        if (targets.Count == 0)
            return null;
        var nearest = targets.MinBy(m => player.Position.HorizontalDistanceTo(m.Position))!;
        var from = Screen(camera, scene, player.Position);
        var to = drawnAt.TryGetValue(nearest.Id, out var drawn) ? drawn : Screen(camera, scene, nearest.Position);
        // The number the card shows: horizontal metres to the nearest place, as old as the position.
        var metres = player.Position.HorizontalDistanceTo(nearest.Position);
        // The distance and nothing else (owner, 2026-10-04: "The 'minutes' numbers when showing distances on the map
        // are completely off. Either be precise or scrap them"). The minutes were the position's age; beside a
        // distance they read as the time to get there, which nobody can know. The age is said at the player's marker.
        var text = DistanceText(metres);
        // On the part of the line in view, and only where the line is long enough to carry it clear of its ends.
        var view = SKRect.Create(0, 0, camera.Viewport.Width, camera.Viewport.Height);
        view.Inflate(-24 * ui, -24 * ui);
        using var font = new SKFont(TypefaceBold, 12 * ui);
        var width = font.MeasureText(text) + 12 * ui;
        var height = 18 * ui;
        if (Clip(from, to, view) is not var (a, b) || SKPoint.Distance(a, b) < width + 56 * ui)
            return new GuideLine(from, to, metres, null, SKRect.Empty, PickColor(scene, nearest.Group));
        return new GuideLine(from, to, metres, text, PlateBox(a, b, width, height, symbols, ui), PickColor(scene, nearest.Group));
    }

    // Where the plate stands on the line's part in view: at its middle, or, where that would cover a symbol (it stood
    // on a boss marker; the review of 2026-10-04), slid along the line to the nearest place that covers none, toward
    // the place before toward the player. It keeps the middle's distance from the line's ends, and the middle itself
    // when no place is free.
    private static SKRect PlateBox(SKPoint a, SKPoint b, float width, float height, IReadOnlyList<SKRect> symbols, float ui)
    {
        var length = SKPoint.Distance(a, b);
        SKRect At(float along)
        {
            var t = along / length;
            return SKRect.Create(a.X + (b.X - a.X) * t - width / 2, a.Y + (b.Y - a.Y) * t - height / 2, width, height);
        }
        bool Free(SKRect box) => !symbols.Any(symbol => symbol.IntersectsWith(box));
        var middle = At(length / 2);
        if (Free(middle))
            return middle;
        var room = (length - width - 56 * ui) / 2;
        for (var slide = 6 * ui; slide <= room; slide += 6 * ui)
        {
            foreach (var box in new[] { At(length / 2 + slide), At(length / 2 - slide) })
            {
                if (Free(box))
                    return box;
            }
        }
        return middle;
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
        using var edge = new SKPaint { Color = guide.Color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 * ui };
        using var font = new SKFont(TypefaceBold, 12 * ui);
        using var paint = new SKPaint { Color = guide.Color, IsAntialias = true };
        canvas.DrawRect(box, plate);
        canvas.DrawRect(box, edge);
        canvas.DrawText(text, box.MidX, box.MidY + font.Size * 0.36f, SKTextAlign.Center, font, paint);
    }

    // ---- the highlighted quest's places out of view ----

    /// <summary>A chevron at the edge of the view pointing toward places out of view, with how many lie that way.</summary>
    /// <param name="Degrees">The direction it points, clockwise from the right.</param>
    /// <param name="Pointed">Places of what is pointed at: drawn larger, and pulsing as its markers in view do.</param>
    public sealed record EdgeChevron(SKPoint At, float Degrees, int Count, SKColor Color, bool Pointed = false);

    /// <summary>How far in from the edge of the view the chevrons sit (pixels, before scaling).</summary>
    private const float ChevronInset = 18;

    // The picked quests' places out of view, and the pointed-at quest's while the pointer is on it: one chevron per
    // direction (places whose edge points lie within 56 px merge), in the quest's colour. The same vocabulary as the
    // player's edge badge, smaller and without a plate: the player is level 1.
    // A place counts as in view where its symbol is drawn: one set beside its place at the very edge (SideBySide) gets
    // no chevron while its symbol shows, and one while it doesn't.
    private static List<EdgeChevron> Chevrons(Camera camera, MapScene scene, float ui, IReadOnlyDictionary<string, SKPoint> drawnAt)
    {
        var (w, h) = (camera.Viewport.Width, camera.Viewport.Height);
        var center = new SKPoint(w / 2, h / 2);
        var inset = ChevronInset * ui;
        var points = new List<(SKPoint Edge, SKPoint Direction, SKColor Color, bool Pointed)>();
        foreach (var marker in scene.Markers)
        {
            if (marker.Objective is null || marker.Kind == MarkerKind.ObjectiveDone || !(IsPointed(scene, marker) || IsSelected(scene, marker)))
                continue;
            var at = drawnAt.TryGetValue(marker.Id, out var drawn) ? drawn : Screen(camera, scene, marker.Position);
            if (at.X >= 0 && at.X <= w && at.Y >= 0 && at.Y <= h)
                continue;
            var d = at - center;
            var tx = Math.Abs(d.X) < 1e-3 ? float.MaxValue : Math.Max(0, w / 2 - inset) / Math.Abs(d.X);
            var ty = Math.Abs(d.Y) < 1e-3 ? float.MaxValue : Math.Max(0, h / 2 - inset) / Math.Abs(d.Y);
            var t = Math.Min(tx, ty);
            var length = Math.Max(1e-3f, d.Length);
            points.Add((new SKPoint(center.X + d.X * t, center.Y + d.Y * t), new SKPoint(d.X / length, d.Y / length),
                IsSelected(scene, marker) ? PickColor(scene, marker.Group) : ColorOf(marker.Kind), IsPointed(scene, marker)));
        }
        return Clusters(points, (a, b) => a.Color == b.Color && a.Pointed == b.Pointed && SKPoint.Distance(a.Edge, b.Edge) < 56 * ui)
            .Select(c =>
            {
                var at = new SKPoint(c.Average(p => p.Edge.X), c.Average(p => p.Edge.Y));
                var direction = new SKPoint(c.Sum(p => p.Direction.X), c.Sum(p => p.Direction.Y));
                return new EdgeChevron(at, (float)(Math.Atan2(direction.Y, direction.X) * 180 / Math.PI), c.Count, c[0].Color, c[0].Pointed);
            })
            .ToList();
    }

    private static void DrawChevron(SKCanvas canvas, EdgeChevron chevron, float ui)
    {
        var at = chevron.At;
        // Half as large again for what is pointed at: the small chevron went unseen.
        var scale = ui;
        if (chevron.Pointed)
            ui *= 1.5f;
        using var halo = new SKPaint { Color = Background.WithAlpha(220), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 * ui, StrokeJoin = SKStrokeJoin.Round };
        using var fill = new SKPaint { Color = chevron.Color, IsAntialias = true };
        canvas.Save();
        canvas.RotateDegrees(chevron.Degrees, at.X, at.Y);
        using (var arrow = Polygon(new(at.X + 6 * ui, at.Y), new(at.X - 4 * ui, at.Y - 6.5f * ui), new(at.X - 1.5f * ui, at.Y), new(at.X - 4 * ui, at.Y + 6.5f * ui)))
        {
            canvas.DrawPath(arrow, halo);
            canvas.DrawPath(arrow, fill);
        }
        canvas.Restore();
        if (chevron.Count < 2)
            return;
        // The count sits inward of the chevron, toward the middle of the view.
        var radians = chevron.Degrees * Math.PI / 180;
        var label = new SKPoint(at.X - (float)Math.Cos(radians) * 14 * ui, at.Y - (float)Math.Sin(radians) * 14 * ui);
        using var font = new SKFont(TypefaceBold, (chevron.Pointed ? 12.5f : 10.5f) * scale);
        using var textHalo = new SKPaint { Color = Background.WithAlpha(220), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 * scale };
        var count = chevron.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        canvas.DrawText(count, label.X, label.Y + font.Size * 0.36f, SKTextAlign.Center, font, textHalo);
        canvas.DrawText(count, label.X, label.Y + font.Size * 0.36f, SKTextAlign.Center, font, fill);
    }

    // ---- the scale bar ----

    /// <summary>The scale bar: its round length in metres, its length on screen, and where it stands.</summary>
    public sealed record ScaleBar(double Metres, float Pixels, SKPoint Origin, SKRect Box);

    private static readonly double[] ScaleSteps = [1, 2, 5, 10, 25, 50, 100, 200, 500, 1000, 2000];

    /// <summary>The longest round length (1, 2, 5, 10, 25, 50, 100, 200, 500 m …) that fits in a number of pixels.</summary>
    public static double ScaleBarMetres(double pixelsPerMetre, float maxPixels) =>
        ScaleSteps.Where(s => s * pixelsPerMetre <= maxPixels).DefaultIfEmpty(ScaleSteps[0]).Max();

    // Bottom left, above the wiki link and the credit line the window lays over that corner. Its metres are measured
    // the way the bar lies, along the screen's horizontal: a render stretched along one axis (Icebreaker, 1.75×) has
    // no one scale, and the mean of both axes made the bar 27 % wrong there (the review of 2026-10-04).
    private static ScaleBar Scale(Camera camera, MapScene scene, float ui)
    {
        var at = new SKPoint(18 * ui, camera.Viewport.Height - 66 * ui);
        var from = scene.Projection.ToWorld(camera.ToMap(at));
        var to = scene.Projection.ToWorld(camera.ToMap(new SKPoint(at.X + 100, at.Y)));
        var metresIn100 = Math.Sqrt((to.X - from.X) * (to.X - from.X) + (to.Z - from.Z) * (to.Z - from.Z));
        var perMetre = metresIn100 > 0 ? 100 / metresIn100 : 1;
        var metres = ScaleBarMetres(perMetre, 120 * ui);
        var pixels = (float)(metres * perMetre);
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

    // Rings that leave the marker and fade, like a ping: motion is what the eye notices before anything else, so
    // the focused markers are found at once even among many. Two rings half a period apart, so one is always well
    // out; each starts 4.5 px wide and travels 38 px, on a dark band so it shows on light streets too (owner,
    // 2026-10-04: "I really like it, but the effect is too subtle"; it was one ring, 2.5 px wide, that travelled
    // 26 px and faded with the square of the way).
    private static void DrawPulse(SKCanvas canvas, MapScene scene, SKPoint at, float r, SKColor color, float ui)
    {
        var phase = (DateTime.Now - scene.FocusSince).TotalSeconds % PulsePeriod.TotalSeconds / PulsePeriod.TotalSeconds;
        for (var i = 0; i < 2; i++)
        {
            var t = (float)((phase + i * 0.5) % 1);
            var fade = (float)Math.Pow(1 - t, 1.5);
            var width = (4.5f - 2 * t) * ui;
            using var band = new SKPaint
            {
                Color = Background.WithAlpha((byte)(150 * fade)), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = width + 2.5f * ui,
            };
            using var ring = new SKPaint
            {
                Color = color.WithAlpha((byte)(255 * fade)), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = width,
            };
            canvas.DrawCircle(at, r + (4 + 34 * t) * ui, band);
            canvas.DrawCircle(at, r + (4 + 34 * t) * ui, ring);
        }
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

    // An extract on the player's list this raid is lit (owner, 2026-10-05: "highlighted better on the map so it can be
    // seen that they are active"): a steady glow in its own colour around the triangle. A glow, since a ring is a
    // pick's and a pulse the pointer's; steady, since nothing moves for long on a second monitor.
    private const float ListedGlowReach = 2.8f;

    internal static void DrawListedGlow(SKCanvas canvas, ShownMarker m)
    {
        var radius = m.R * ListedGlowReach;
        using var shader = SKShader.CreateRadialGradient(m.At, radius,
            [m.Color.WithAlpha(210), m.Color.WithAlpha(120), m.Color.WithAlpha(0)], [0.3f, 0.55f, 1f], SKShaderTileMode.Clamp);
        using var paint = new SKPaint { Shader = shader, IsAntialias = true };
        canvas.DrawCircle(m.At, radius, paint);
    }

    // What lies under every symbol (owner, 2026-10-05, from the overlap panel: "Go for F"): a pick's ring and a
    // pointed-at symbol's pulse, around symbols that keep their rest size, so neither covers a neighbour; and where a
    // stack stands opened, its dark plate and the hairlines from each symbol to its own place.
    private static void DrawUnderlay(SKCanvas canvas, MapScene scene, IReadOnlyList<ShownMarker> markers, float ui)
    {
        foreach (var m in markers.Where(m => m.Kept))
            DrawPickRing(canvas, m, ui);
        foreach (var m in markers.Where(m => m.Pointed && scene.Pulsing))
            DrawPulse(canvas, scene, m.At, m.R, m.Color, ui);
        if (FanPlate(markers, ui) is not { } plate)
            return;
        using (var ground = new SKPaint { Color = Background.WithAlpha((byte)(190 * Math.Clamp(scene.FanProgress, 0, 1))), IsAntialias = true })
        using (var edge = new SKPaint { Color = Palette.Sk(Palette.LineStrong), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 * ui })
        {
            canvas.DrawCircle(plate.Hub, plate.Radius, ground);
            canvas.DrawCircle(plate.Hub, plate.Radius, edge);
        }
        foreach (var m in markers.Where(m => m.Home is not null))
        {
            using var under = new SKPaint { Color = Background.WithAlpha(170), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 * ui, StrokeCap = SKStrokeCap.Round };
            using var line = new SKPaint { Color = m.Color.WithAlpha(220), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.3f * ui, StrokeCap = SKStrokeCap.Round };
            using var dot = new SKPaint { Color = m.Color, IsAntialias = true };
            canvas.DrawLine(m.Home!.Value, m.At, under);
            canvas.DrawLine(m.Home!.Value, m.At, line);
            canvas.DrawCircle(m.Home!.Value, 2.4f * ui, under);
            canvas.DrawCircle(m.Home!.Value, 2.2f * ui, dot);
        }
    }

    // A picked quest's marker carries a steady ring, so it is found at a glance: a dark band, then the colour, so it
    // reads on light and dark artwork alike.
    private static void DrawPickRing(SKCanvas canvas, ShownMarker m, float ui)
    {
        using var band = new SKPaint { Color = Background.WithAlpha(200), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4.5f * ui };
        using var halo = new SKPaint { Color = m.Color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f * ui };
        canvas.DrawCircle(m.At, m.R + 5 * ui, band);
        canvas.DrawCircle(m.At, m.R + 5 * ui, halo);
    }

    private static SKPath Triangle(SKPoint at, float r) =>
        Polygon(new(at.X, at.Y - r * 1.2f), new(at.X + r * 1.1f, at.Y + r * 0.8f), new(at.X - r * 1.1f, at.Y + r * 0.8f));

    private static SKPath Diamond(SKPoint at, float r) =>
        Polygon(new(at.X, at.Y - r), new(at.X + r, at.Y), new(at.X, at.Y + r), new(at.X - r, at.Y));

    private static void DrawMarker(SKCanvas canvas, MapScene scene, ShownMarker shown, float ui)
    {
        var (marker, at, r, color) = (shown.Marker, shown.At, shown.R, shown.Color);
        using var fill = new SKPaint { Color = color, IsAntialias = true };
        using var outline = new SKPaint { Color = Background, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 * ui };
        // A pick's ring and a pointed-at symbol's pulse lie under every symbol (DrawUnderlay).

        switch (marker.Kind)
        {
            case MarkerKind.Objective or MarkerKind.PossibleLocation when marker.Objective is { } kind:
                // Every quest marker is the type's dark glyph on the marker's colour (owner, 2026-10-04: "it should
                // always be a black icon surrounded by the marker color"). A possible location was a hollow ring with
                // a coloured glyph until then, and read as another kind of marker, not as "maybe here": it says so
                // with a "?" at its corner now (DrawPossibleBadge).
                DrawCollar(canvas, at, r, ui);
                canvas.DrawCircle(at, r, fill);
                canvas.DrawCircle(at, r, outline);
                Glyphs.Draw(canvas, kind, at, r * 1.05f, Background);
                break;
            case MarkerKind.ExtractPmc or MarkerKind.ExtractScav or MarkerKind.ExtractShared:
                using (var tri = Triangle(at, r))
                {
                    DrawCollar(canvas, tri, ui);
                    if (shown.NotListed)
                    {
                        // Not on the player's list this raid (read from their screenshot; owner, 2026-10-05): the
                        // triangle's outline alone, on the collar's dark, so the solid ones are the ways out.
                        using var ground = new SKPaint { Color = Background.WithAlpha(215), IsAntialias = true };
                        using var edge = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.6f * ui, StrokeJoin = SKStrokeJoin.Round };
                        canvas.DrawPath(tri, ground);
                        canvas.DrawPath(tri, edge);
                        break;
                    }
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
                // A solid red octagon: a shape no other marker has, and all red (owner, 2026-10-04, from the review:
                // the red diamond with a dark centre was mostly collar and centre, weaker than a quest's disc, and it
                // shared its outline with the violet transit diamond).
                using (var plate = Octagon(at, r * 1.2f))
                {
                    DrawCollar(canvas, plate, ui);
                    canvas.DrawPath(plate, fill);
                    canvas.DrawPath(plate, outline);
                }
                break;
            case MarkerKind.Transit:
                using (var diamond = Diamond(at, r))
                {
                    DrawCollar(canvas, diamond, ui);
                    canvas.DrawPath(diamond, fill);
                    canvas.DrawPath(diamond, outline);
                }
                break;
            case MarkerKind.Lock or MarkerKind.Switch:
                // A glyph on a dark collar, no plate: quiet, and a shape no other marker has (a padlock, a power
                // symbol). The key itself is the key glyph in BRING; the padlock is where it opens.
                DrawCollar(canvas, at, r, ui);
                Glyphs.Draw(canvas, marker.Kind == MarkerKind.Lock ? Glyphs.Lock : Glyphs.Switch, at, r * 1.9f,
                    shown.Selected ? color : color.WithAlpha(LandmarkAlpha));
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
            DrawFloorArrow(canvas, shown, ui);
        if (shown.Count > 1)
            DrawCountBadge(canvas, shown, ui);
        if (ShowsOptional(shown))
            DrawOptionalBadge(canvas, shown, ui);
        if (ShowsPossible(shown))
            DrawPossibleBadge(canvas, shown, ui);
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
    // is larger, pings itself and says so. Not while the view glides to the position (Follow my position).
    private static void DrawEdge(SKCanvas canvas, Camera camera, MapScene scene, float ui)
    {
        if (scene.Gliding || EdgeOf(camera, scene, ui) is not { } edge || scene.Player is not { } player)
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
        var age = Shturmap.Core.Logs.WallClock.Elapsed(player.At, DateTime.Now);
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

        // The facing is only true for a moment; past that the cone would mislead. As long as the cards say directions
        // relative to it (Facing.Fresh).
        if (player.YawDegrees is { } yaw && age < Shturmap.Core.Navigation.Facing.Fresh)
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
        // Past PlayerStale the tag says so on its own, larger and framed in sand: it took over from the big
        // "POSITION 7 MIN OLD" over the map (owner, 2026-10-03: "Put it next to the marker").
        var (text, stale) = AgeTag(age);
        using var font = new SKFont(TypefaceBold, AgeTagSize(stale) * ui);
        var box = AgeTagBox(at, font.MeasureText(text), stale, ui);
        using var plate = new SKPaint { Color = Background.WithAlpha((byte)(stale ? 240 : 230)), IsAntialias = true };
        using var paint = new SKPaint { Color = Player, IsAntialias = true };
        canvas.DrawRect(box, plate);
        if (stale)
        {
            using var frame = new SKPaint { Color = Player, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f * ui };
            canvas.DrawRect(box, frame);
        }
        canvas.DrawText(text, box.MidX, box.MidY + font.Size * 0.36f, SKTextAlign.Center, font, paint);
    }

    /// <summary>From when the player's ring is dashed and carries an age tag.</summary>
    public static readonly TimeSpan PlayerOld = TimeSpan.FromMinutes(1);

    /// <summary>
    /// From when a position is too old to trust at a glance: the age tag then says "OLD", larger and framed (the same
    /// two minutes the big banner over the map used before it was removed, owner, 2026-10-03).
    /// </summary>
    public static readonly TimeSpan PlayerStale = TimeSpan.FromMinutes(2);

    private const float PlayerRing = 12;

    /// <summary>A fix's age as the map says it: "4 MIN", "2 H" (whole units, as the top bar).</summary>
    public static string AgeText(TimeSpan age) => age.TotalMinutes < 60 ? $"{(int)age.TotalMinutes} MIN" : $"{(int)age.TotalHours} H";

    /// <summary>The tag beside the player once the position is a minute old: "1 MIN OLD", and from <see cref="PlayerStale"/>
    /// larger and framed ("7 MIN OLD"). Always with "OLD": bare minutes on a map read as a time to get somewhere
    /// (owner, 2026-10-04).</summary>
    public static (string Text, bool Stale) AgeTag(TimeSpan age) => (AgeText(age) + " OLD", age >= PlayerStale);

    private static float AgeTagSize(bool stale) => stale ? 12.5f : 10.5f;

    // The age tag sits right of the ring.
    private static SKRect AgeTagBox(SKPoint at, float textWidth, bool stale, float ui)
    {
        var height = (stale ? 20 : 16) * ui;
        return SKRect.Create(at.X + (PlayerRing + 5) * ui, at.Y - height / 2, textWidth + (stale ? 12 : 10) * ui, height);
    }

    private static SKPath Hexagon(SKPoint at, float r) =>
        Polygon(Enumerable.Range(0, 6).Select(i => new SKPoint(at.X + r * MathF.Cos(MathF.PI / 3 * i), at.Y + r * MathF.Sin(MathF.PI / 3 * i))).ToArray());

    // Flat on top, like a stop sign.
    private static SKPath Octagon(SKPoint at, float r) =>
        Polygon(Enumerable.Range(0, 8).Select(i => new SKPoint(at.X + r * MathF.Cos(MathF.PI / 4 * i + MathF.PI / 8), at.Y + r * MathF.Sin(MathF.PI / 4 * i + MathF.PI / 8))).ToArray());

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
