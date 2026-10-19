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
    /// <param name="now">The frame's time, which every animation and the player's age are drawn at; the clock's when
    /// null. Read once, so a whole frame shows one moment, and a frame drawn at a given time is the same every run
    /// (owner, 2026-10-15: the QA pass must see animations frame by frame; <c>shturmap-cli frames</c>).</param>
    /// <param name="trace">Filled with what the frame drew of each symbol (<see cref="SymbolTrace"/>), for that look;
    /// null in the app.</param>
    public static void Render(SKCanvas canvas, Camera camera, MapScene scene, float uiScale = 1, Func<MarkerKind, bool, StepBack>? stepBack = null,
        DateTime? now = null, ICollection<SymbolTrace>? trace = null)
    {
        var time = now ?? DateTime.Now;
        // A linked set shown steps the rest back further than one thing pointed at, by the frame's settle (MeasuresAt).
        stepBack ??= MeasuresAt(scene, time);
        canvas.Clear(Background);
        // The zoom limit is the map's that is drawn, not the one's fitted last (a preview leaves its own behind).
        camera.LimitTo(scene.Projection.WorldRect, 24 * uiScale, Camera.LabelRoom * uiScale);
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

        var layout = LayoutOf(camera, scene, uiScale, time);
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
        DrawGuide(canvas, layout.ExitGuide, uiScale);
        DrawSpawns(canvas, camera, scene, uiScale);
        // The extracts on the player's list this raid are lit, under every symbol, so a glow never covers one; a list
        // just read rings them out.
        // What a trace records of a symbol: its place and size from the layout, its animation from the drawing below.
        var traced = trace is null ? null : new Dictionary<ShownMarker, SymbolTrace>(ReferenceEqualityComparer.Instance);
        DrawExitsLit(canvas, scene, layout.Markers, uiScale, time, traced);
        // A linked set's places: halos that breathe, or in the static look a ring on each collar (SetLookAt).
        var setLook = SetLookAt(scene, time);
        DrawUnderlay(canvas, scene, layout.Markers, uiScale, time, setLook, stepBack, traced);
        void Draw(ShownMarker marker) => DrawMarker(canvas, scene, marker, uiScale, time, traced);

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
                DrawLabel(canvas, scene, label, time, traced);
        }
        // Under the lit symbols, so their badges lie over it: the ring just outside each collar.
        DrawCollarRings(canvas, layout.Markers, setLook, uiScale, traced);
        foreach (var marker in layout.Markers.Where(m => m.Focused))
            Draw(marker);
        foreach (var label in layout.Labels.Where(l => l.Of.Focused))
            DrawLabel(canvas, scene, label, time, traced);
        DrawGuidePlate(canvas, layout.Guide, uiScale);
        DrawGuidePlate(canvas, layout.ExitGuide, uiScale);
        foreach (var chevron in layout.Chevrons)
        {
            // What is pointed at pulses where it is in view; out of view, its chevron does (owner, 2026-10-04: "if a
            // quest marker is outside of the current viewport, it should probably indicate that").
            if (chevron.Pointed && scene.Pulsing)
                DrawPulse(canvas, scene, chevron.At, 7 * uiScale, chevron.Color, uiScale, time);
            // A linked set's place out of view breathes at its chevron, as its places in view do.
            else if (chevron.Pointed && setLook.Halo > 0)
                DrawHalo(canvas, chevron.At, 7 * uiScale, chevron.Color, setLook);
            DrawChevron(canvas, chevron, uiScale);
        }
        DrawScaleBar(canvas, layout.Scale, uiScale);
        DrawLeaving(canvas, camera, scene, uiScale, time);
        // The roll call's icons on their way from the band to their places: over every symbol, under the player.
        var flyingOutOfView = DrawFlights(canvas, camera, scene, layout.Markers, uiScale, time, traced);
        DrawPlayer(canvas, camera, scene, uiScale, time);
        DrawReplay(canvas, camera, scene, uiScale);
        if (trace is not null)
        {
            foreach (var marker in layout.Markers)
            {
                if (traced!.TryGetValue(marker, out var drawn))
                    trace.Add(drawn);
            }
            foreach (var flying in flyingOutOfView ?? [])
                trace.Add(flying);
        }
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
    /// extracts and transits) and bosses never step back: they matter at a glance whatever is highlighted (a boss rests
    /// stepped back already, hollow at <see cref="BossRestAlpha"/>, until it is lit: <see cref="ShownMarker.Resting"/>). Other
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

    /// <summary>
    /// How far the rest steps back while a linked set is shown (<see cref="MapScene.ShowsSet"/>; owner, 2026-10-17, from a
    /// panel of four: "B"): a little further while the set breathes (<paramref name="settle"/> 0), planning 55 % (labels
    /// 40 %), in a raid 70 % (labels 50 %), and further in its static look (1: a held focus, the pointer still, Windows'
    /// animation effects off), planning 40 % (labels 30 %), in a raid 60 % (labels 45 %), eased between as the breath
    /// settles. Never out of sight: the owner found 28 % too dark (2026-10-03). For every kind that steps back at all; the
    /// ways out and the bosses keep their measure (<see cref="StepBackOf(MarkerKind, bool)"/>), and one thing pointed at
    /// keeps the plain table.
    /// </summary>
    public static StepBack StepBackOf(bool inRaid, float settle)
    {
        var (breathing, still) = inRaid
            ? (new StepBack(0.7f, 1f, 0.5f), new StepBack(0.6f, 1f, 0.45f))
            : (new StepBack(0.55f, 1f, 0.4f), new StepBack(0.4f, 1f, 0.3f));
        var s = Math.Clamp(settle, 0, 1);
        return new(breathing.Alpha + (still.Alpha - breathing.Alpha) * s, 1f, breathing.LabelAlpha + (still.LabelAlpha - breathing.LabelAlpha) * s);
    }

    /// <summary>
    /// The measures a frame at <paramref name="now"/> steps the rest back by: the plain table
    /// (<see cref="StepBackOf(MarkerKind, bool)"/>), or while a linked set is shown its own, from breathing to static as
    /// the breath settles (<see cref="StepBackOf(bool, float)"/>, <see cref="MapScene.SettleAt"/>).
    /// </summary>
    public static Func<MarkerKind, bool, StepBack> MeasuresAt(MapScene scene, DateTime now)
    {
        if (!scene.ShowsSet)
            return StepBackOf;
        var settle = scene.SettleAt(now);
        return (kind, inRaid) => StepBackOf(kind, inRaid) is { Alpha: < 1f } ? StepBackOf(inRaid, settle) : StepBackOf(kind, inRaid);
    }

    // A picked quest's marker, or the door of a key a picked quest needs: part of the pick.
    private static bool IsPick(MapScene scene, ShownMarker m) => m.Kept || IsKeptKey(scene, m.Marker);

    /// <summary>
    /// How far a marker steps back while something else is pointed at: not at all for the picks and for the doors of the
    /// keys they need, which read as part of the pick (the review of 2026-10-04: the doors stepped back like any
    /// lock), else by its kind.
    /// </summary>
    /// <param name="byKind">The frame's measures (<see cref="MeasuresAt"/>, now's when null), or a developer render's
    /// alternatives.</param>
    public static StepBack StepBackOf(MapScene scene, ShownMarker marker, Func<MarkerKind, bool, StepBack>? byKind = null) =>
        IsPick(scene, marker) ? Full : (byKind ?? MeasuresAt(scene, DateTime.Now))(marker.Marker.Kind, scene.InRaid);

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
        return 1 - (1 - (byKind ?? MeasuresAt(scene, DateTime.Now))(zone.Kind, scene.InRaid).Alpha) * scene.Dim;
    }

    // A hazard area's name steps back as its kind's labels do.
    private static float HazardLabelStrength(MapScene scene, Func<MarkerKind, bool, StepBack>? byKind) =>
        scene.ShownFocus.Count == 0 ? 1 : 1 - (1 - (byKind ?? MeasuresAt(scene, DateTime.Now))(MarkerKind.Hazard, scene.InRaid).LabelAlpha) * scene.Dim;

    // A layer that steps what is drawn into it back by a measure, eased with the dim; no layer when it changes nothing.
    private readonly ref struct StepBackLayer
    {
        private readonly SKCanvas? _canvas;

        /// <param name="bounds">Where what is drawn into it lies, when that is known: a layer that size, not the canvas's.</param>
        public StepBackLayer(SKCanvas canvas, float alpha, float saturation, float dim, SKRect? bounds = null)
        {
            var a = 1 - (1 - alpha) * dim;
            var s = 1 - (1 - saturation) * dim;
            if (a >= 0.999f && s >= 0.999f)
                return;
            using var filter = s < 0.999f ? ArtworkColors.Recede(s, 1 - 0.1f * (1 - s)) : null;
            using var paint = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(255 * a)), ColorFilter = filter };
            if (bounds is { } limit)
                canvas.SaveLayer(limit, paint);
            else
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

        /// <summary>A boss's zone at rest, neither pointed at nor targeted by a picked, pointed-at or held quest: a hollow
        /// octagon at <see cref="BossRestAlpha"/>, named only where it is the boss's likeliest (<see cref="MapMarker.RestLabel"/>),
        /// in the map's own spaced caps in muted ink (owner, 2026-10-17, from a panel of four: "A").</summary>
        public bool Resting { get; init; }

        /// <summary>An extract the game's own list didn't name this raid (<see cref="MapScene.ExitsNotListed"/>): struck out.</summary>
        public bool NotListed { get; init; }

        /// <summary>An extract on the player's list this raid (<see cref="MapScene.ExitsListed"/>): lit.</summary>
        public bool Listed { get; init; }

        /// <summary>A listed extract the game marked "??:??:??" (<see cref="MapScene.ExitsUnsure"/>): a "?" in its triangle.</summary>
        public bool Unsure { get; init; }

        /// <summary>Drawn off its true place, for a neighbour that would cover it or be covered (<see cref="Repel"/>,
        /// <see cref="SideBySide"/>): <see cref="At"/> is where the symbol stands.</summary>
        public bool Beside { get; init; }

        /// <summary>Its true place, where it was set so far from it that it no longer covers it (<see cref="Repel"/>): a
        /// leader joins the two, with a dot at the place.</summary>
        public SKPoint? Leader { get; init; }

        /// <summary>The objectives of its quest on its spot it stands for, itself first (<see cref="JoinSpots"/>); empty for one.</summary>
        public IReadOnlyList<MapMarker> Joined { get; init; } = [];

        /// <summary>A switch or lock lit as a step of the gated extract pointed at (<see cref="MapMarker.SwitchSteps"/>):
        /// that extract, whose colour it takes and whose name its label adds ("Mall Main Power Switch · Saferoom Exfil";
        /// owner, 2026-10-17). Null otherwise.</summary>
        public MapMarker? StepOf { get; init; }
    }

    /// <summary>A placed label: the marker's own, or a map name (rotated about its anchor). <see cref="Text"/> is as drawn: a
    /// picked or pointed-at name may be trimmed with "…" (<see cref="LitNameMost"/>).</summary>
    public sealed record PlacedLabel(string Text, SKRect Box, float Baseline, float Size, bool Bold, SKColor Color, ShownMarker Of)
    {
        /// <summary>Which of its places it took (<see cref="LabelCandidates"/>): 0 right of its symbol, 1 left, 2 above, 3
        /// below, 4 a line further down. A picked or pointed-at name keeps it while the view only moves.</summary>
        public int Side { get; init; }

        /// <summary>The extra space between its letters (pixels): a resting boss's name is set in the map's spaced caps
        /// (<see cref="ShownMarker.Resting"/>); 0 for every other label.</summary>
        public float Tracking { get; init; }

        /// <summary>A listed extract's name as it is drawn until a list just read lights it (<see cref="MapScene.ExitsRinging"/>):
        /// in the regular face, where a regular name would stand on the same side; <see cref="Box"/> is its bold name's,
        /// kept from the first frame. Null for every other label.</summary>
        public UnlitLabel? Unlit { get; init; }
    }

    /// <summary>A listed extract's name before its turn lights it: in the regular face, its box and baseline.</summary>
    public sealed record UnlitLabel(SKRect Box, float Baseline, float Size);

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

        /// <summary>How the symbols set apart on leaders were placed (<see cref="RepelRun"/>).</summary>
        public RepelRun Repel { get; init; }

        /// <summary>While a gated extract is pointed at, the guide from it to the first step of what leaving there takes,
        /// in its colour, with the distance on the plate (<see cref="ExitGuideOf"/>); null otherwise.</summary>
        public GuideLine? ExitGuide { get; init; }
    }

    /// <summary>How a layout placed the symbols set apart on leaders (<see cref="Repel"/>): afresh, as a still view shows
    /// them; going on from the frame before, as while the view only moves (docs/DESIGN.md, "Map drawing", *Steady while the
    /// view moves*); or as the last placement, which still held (a pan of the same symbols, a pick, the pointer).</summary>
    public enum RepelRun
    {
        Afresh,
        Continued,
        Held,
    }

    /// <summary>What a layout is made from, apart from the scene's own data, which <see cref="MapScene.LayoutVersion"/> counts.</summary>
    /// <param name="Fading">Whether the last focus is still drawn while its dimming fades out (<see cref="MapScene.ShownFocus"/>).</param>
    /// <param name="Age">The age tag beside the player and on the guide's plate: the one part that follows the clock.</param>
    /// <param name="Language">The language in use (<see cref="UiLanguage.Code"/>): the badges' and tags' words, and so their
    /// sizes, follow it.</param>
    internal readonly record struct LayoutKey(int Scene, MapPoint Center, double Zoom, SKSize Viewport, float Ui, bool Fading, bool Sheet, string Age, string Language);

    /// <summary>
    /// The layout for a frame: the last frame's while nothing it is made from changed, else a new one
    /// (<see cref="Layout"/>). While a pointed-at quest pulses or a new position pings, the map is drawn again about 60
    /// times a second and nothing in it moves; placing every marker and label anew for each of those frames was most
    /// of a frame's work (the review of 2026-10-04, A17). The view, the display's scale and the scene's data decide
    /// the layout; the clock only through the player's age tag, which changes with the minute. One whose scale bar held
    /// its place while the view moved (<see cref="ScaleBar.Held"/>) is laid out again in the first frame drawn with the
    /// view still, where the bar settles; the symbols keep their places (the repel's last placement holds).
    /// </summary>
    /// <param name="now">The frame's time (<see cref="Render"/>'s); the clock's when null.</param>
    public static MapLayout LayoutOf(Camera camera, MapScene scene, float ui, DateTime? now = null)
    {
        var time = now ?? DateTime.Now;
        var age = scene.Player is { } player && Shturmap.Core.Logs.WallClock.Elapsed(player.At, time) is var old && old >= PlayerOld ? AgeTag(old).Text : "";
        var key = new LayoutKey(scene.LayoutVersion, camera.Center, camera.Zoom, camera.Viewport, ui, scene.Dim > 0, scene.IsSheet, age, UiLanguage.Code);
        if (scene.LastLayout is { } last && last.Key == key && !last.Layout.Scale.Held)
        {
            scene.RepelShown = RepelRun.Held;
            return last.Layout;
        }
        var layout = Layout(camera, scene, ui, time);
        scene.LastLayout = (key, layout);
        scene.RepelShown = layout.Repel;
        return layout;
    }

    /// <summary>
    /// Places symbols first, then labels by priority (cartography review, 2026-10-02): the kept or pointed-at quest,
    /// bosses, quests, extracts and transits, snipers, and the map's own names last. Each marker label tries right,
    /// left, above and below its symbol and is dropped when all four are taken; no label covers a symbol, and none starts
    /// past the map's left edge (<see cref="Inward"/>). A selected label that finds no free place still shows on the
    /// right, giving way only to its own copies; it never runs past the map's right edge either (<see cref="LitNameMost"/>).
    /// The scale bar steps aside from the symbols first (<see cref="Scale"/>).
    /// </summary>
    /// <param name="now">The frame's time (<see cref="Render"/>'s): the player's age tag takes room. The clock's when null.</param>
    public static MapLayout Layout(Camera camera, MapScene scene, float ui, DateTime? now = null)
    {
        var time = now ?? DateTime.Now;
        var markers = ShownMarkers(camera, scene, ui, time);
        var repel = scene.LastRepelRun;
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
            if (HasTab(m))
                taken.Add(SKRect.Inflate(TabBox(m, ui), MarkerCollar * ui, MarkerCollar * ui));
            if (m.Floor != 0)
                taken.Add(FloorBadgeKeepOut(m, ui));
            if (ShowsSwitchBadge(m))
                taken.Add(SwitchBadgeKeepOut(m, ui));
            // No label covers a leader or the dot at its end (Repel).
            if (m.Leader is { } place)
            {
                taken.Add(Square(place, 3.5f * ui));
                var length = SKPoint.Distance(place, m.At);
                for (var d = 4 * ui; d < length; d += 5 * ui)
                    taken.Add(Square(new SKPoint(place.X + (m.At.X - place.X) * d / length, place.Y + (m.At.Y - place.Y) * d / length), 1.5f * ui));
            }
        }
        if (scene.Player is { } player)
        {
            var at = Screen(camera, scene, player.Position);
            taken.Add(Square(at, (PlayerRing + 3) * ui));
            var age = Shturmap.Core.Logs.WallClock.Elapsed(player.At, time);
            if (age >= PlayerOld)
            {
                var (text, stale) = AgeTag(age);
                using var tagFont = new SKFont(TypefaceBold, AgeTagSize(stale) * ui);
                taken.Add(AgeTagBox(at, tagFont.MeasureText(text), stale, ui));
            }
        }
        // After every symbol: the guide's plate gives way to them. The guide ends, and the chevrons count, where the
        // symbols are drawn: a marker set off its place (Repel, SideBySide) is there for them too.
        var drawnAt = markers.Where(m => m.Count == 1).GroupBy(m => m.Marker.Id).ToDictionary(g => g.Key, g => g.First().At, StringComparer.Ordinal);
        var guide = Guide(camera, scene, ui, taken, drawnAt);
        if (guide?.Plate is not null)
            taken.Add(guide.PlateBox);
        var exitGuide = ExitGuideOf(camera, scene, ui, taken, drawnAt);
        if (exitGuide?.Plate is not null)
            taken.Add(exitGuide.PlateBox);
        var chevrons = Chevrons(camera, scene, ui, drawnAt);
        taken.AddRange(chevrons.Select(c => Square(c.At, 10 * ui)));
        // The scale bar steps aside from the symbols the repel sets apart, the player's ring and the chevrons (not from the
        // receded locks, switches and Scav zones' rings); the labels then give way to it.
        var symbols = markers.Where(m => Repels(m.Marker.Kind)).Select(m => Square(m.At, m.Reach)).Concat(chevrons.Select(c => Square(c.At, 10 * ui))).ToList();
        if (scene.Player is { } you)
            symbols.Add(Square(Screen(camera, scene, you.Position), (PlayerRing + 3) * ui));
        var scale = Scale(camera, scene, ui, symbols, time);
        taken.Add(scale.Box);

        var labels = new List<PlacedLabel>();
        using var regular = new SKFont(Typeface, 11.5f * ui);
        using var bold = new SKFont(TypefaceBold, 13 * ui);
        // A resting boss's name: the map's own street names' face and size, in spaced caps (BossRestNameSize).
        using var resting = new SKFont(Typeface, BossRestNameSize * ui);
        var restTracking = NameTracking * ui;
        var gap = 4 * ui;
        // A side beside a label placed before it on its line keeps LabelGap from it, so two names there read as two.
        var labelGap = LabelGap * ui;
        bool Free(SKRect box) =>
            !taken.Any(t => t.IntersectsWith(box)) && !labels.Any(l => OnOneLine(l.Box, box) && SKRect.Inflate(l.Box, labelGap, 0).IntersectsWith(box));
        // Where a picked or pointed-at name ends at most, LabelEdge in from the map's right edge, and how wide it moves in
        // from there at most (LitNameMost).
        var litEdge = camera.Viewport.Width - LabelEdge * ui;
        var litMost = Math.Min(LitNameMost * ui, camera.Viewport.Width * LitNameShare);
        // Where a marker's label may go, in order (null where it may not: past the map's left edge, or past right).
        List<SKRect?> Candidates(ShownMarker m, SKFont font, string text, float right = float.PositiveInfinity, float tracking = 0)
        {
            var width = NameWidth(font, text, tracking);
            var candidates = LabelCandidates(m.At, m.Reach + gap, width, font.Size).ToList();
            // A marker with a tab says its name after the tab: "◉×2 Gratitude".
            if (HasTab(m))
                candidates[0] = SKRect.Create(TabBox(m, ui).Right + MarkerCollar * ui + gap, candidates[0].Top, width, candidates[0].Height);
            // One of a pair set side by side has its neighbour on one side and, as often as not, that one's name
            // below: its own may stand a line further down, where the two read as the pair's caption.
            if (m.Beside)
            {
                // A little further than one line: the neighbour's symbol may be the taller, and its name the lower.
                var below = candidates[^1];
                candidates.Add(SKRect.Create(below.Left, below.Bottom + 3 * ui, below.Width, below.Height));
            }
            return candidates.Select((c, side) => Inward(OffBadges(m, c, side, ui), m.At, LabelEdge * ui, right)).ToList();
        }
        // While the view only moves (the scale bar's rule, Scale), a picked or pointed-at name keeps the side it took in
        // the layout before (the rule Steady while the view moves): on its right it is trimmed at the edge rather than sent
        // to its left in the middle of a move. The first layout with the view still places it afresh.
        int? HeldSide(ShownMarker m) =>
            scale.Held && scene.LastLayout?.Layout.Labels.FirstOrDefault(l => l.Of.Selected && l.Of.Marker.Id == m.Marker.Id) is { } before ? before.Side : null;
        // A lock's key and a switch's name are said close up, or for the one pointed at.
        var landmarkLabels = ZoomOverOverview(camera, scene, ui) >= LandmarkLabelFromZoom;
        foreach (var m in markers.Where(m => LabelOf(m).Length > 0 && m.Marker.Kind != MarkerKind.ObjectiveDone && (scene.ShowLabels || m.Selected)
                                             && (m.Selected || landmarkLabels || !IsLandmark(m.Marker.Kind)))
                     .OrderBy(LabelRank).ThenByDescending(m => m.Count))
        {
            // A name is said once per neighbourhood: eight "Abandoned Cargo" labels in one block say no more than one.
            if (labels.Any(l => LabelOf(l.Of) == LabelOf(m) && SKPoint.Distance(l.Of.At, m.At) < LabelRepeat * ui))
                continue;
            // An extract on the player's list this raid says its name in bold, as what is picked or pointed at does. A
            // resting boss says its name and chance in the map's own spaced caps, in muted ink (BossRestNameSize).
            var font = m.Resting ? resting : m.Selected || m.Listed ? bold : regular;
            var text = m.Resting ? UiLanguage.Upper(LabelOf(m)) : LabelOf(m);
            var tracking = m.Resting ? restTracking : 0;
            var candidates = Candidates(m, font, text, tracking: tracking);
            // A picked or pointed-at name doesn't run past the map's right edge (LitNameMost): where its right does, it
            // moves in, trimmed with "…" to LitNameMost if longer, to its left or above or below its symbol, slid in.
            if (m.Selected)
            {
                if (candidates[0] is { } whole && whole.Right > litEdge && Trimmed(font, text, litMost) is { } most)
                    text = most;
                candidates = Candidates(m, font, text, litEdge);
            }
            SKRect? box = null;
            var side = -1;
            if (m.Selected && HeldSide(m) is { } held && held < candidates.Count)
            {
                box = candidates[held] ?? (held == 0 ? Candidates(m, font, text)[0] : null);
                side = box is null ? -1 : held;
            }
            if (box is null)
            {
                side = candidates.FindIndex(c => c is { } free && Free(free));
                box = side >= 0 ? candidates[side] : null;
            }
            // A lit step finding no free place gives way as an unlit name does: at the Saferoom the last two stand at the
            // extract's door, and on its right they covered one another and the extract's name.
            if (box is null && m.Selected && m.StepOf is null)
            {
                box = OffBadges(m, LabelCandidates(m.At, m.Reach + gap, font.MeasureText(text), font.Size).First(), 0, ui);
                side = 0;
            }
            if (box is not { } placed)
                continue;
            // On its right past the edge (with no other side free, or kept there while the view moves), a lit name is
            // trimmed with "…" to end at it: never cut.
            if (m.Selected && placed.Right > litEdge)
            {
                if (Trimmed(font, text, litEdge - placed.Left) is not { } fits)
                    continue;
                text = fits;
                placed = SKRect.Create(placed.Left, placed.Top, font.MeasureText(fits), placed.Height);
            }
            taken.Add(placed);
            // Ink for every label, a lit boss's too: its red octagon is the danger sign, and red type on the dark ground
            // read worse than any other label (the review of 2026-10-04). What is picked or pointed at takes its marker's
            // colour.
            // An extract that isn't on the player's list this raid steps back, name and symbol (MapScene.ExitsNotListed), and
            // so does a boss at rest.
            var color = m.Selected ? m.Color : m.NotListed || m.Resting ? Muted : Ink;
            var label = new PlacedLabel(text, placed, placed.Top + font.Size * 1.1f, font.Size, m.Selected || m.Listed, color, m) { Side = side, Tracking = tracking };
            // Until a list just read lights it, a listed extract's name is in the regular face, as an unlit one's, on the
            // same side (owner, 2026-10-15): the bold name's box is kept from the first frame, so nothing moves as it lights.
            if (m.Listed && !m.Selected && side >= 0 && Candidates(m, regular, text)[side] is { } unlit)
                label = label with { Unlit = new UnlitLabel(unlit, unlit.Top + regular.Size * 1.1f, regular.Size) };
            labels.Add(label);
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
                // tarkov.dev's map labels (maps.json) are English in every language, so English rules: the language in
                // use's would write an English "i" as Turkish "İ".
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
        return new MapLayout(markers, labels, names, guide, scale) { Chevrons = chevrons, Repel = repel, ExitGuide = exitGuide };
    }

    /// <summary>How near (pixels, before scaling) two markers with the same label may be before only one is labelled.</summary>
    public const float LabelRepeat = 250;

    /// <summary>
    /// The room a marker label keeps on its left and right from a label placed before it on its line (pixels, before
    /// scaling; QA pass of 2026-10-19, F1): a side closer to one is not free, and the next is tried. Until then only an
    /// overlap took a side, so two names could stand end to end and read as one: at 150 % German, "Transit zu
    /// Interchange" and "Taxi in der Primorsky-Straße" on one line, about 2 px apart. 8 is more than twice a word space
    /// of the labels' 11.5 px face (3 px), so the gap reads as one between names, not between words. Names on lines of
    /// their own (<see cref="OnOneLine"/>) read as two however close they end.
    /// </summary>
    public const float LabelGap = 8;

    /// <summary>
    /// Whether two labels' boxes stand on one line: they share more than half the shorter one's height. A box is 1.3 times
    /// its text's size, so two names a line apart may share its leading and still read as two lines (Customs at 100 %:
    /// "Rite of Passage" under its symbol, 14 px below "Capturing Outposts" and ending 2 px before it).
    /// </summary>
    public static bool OnOneLine(SKRect a, SKRect b) =>
        Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top) > Math.Min(a.Height, b.Height) / 2;

    // ---- bosses at rest (owner, 2026-10-17: "I feel like the red dots for enemy bosses are too distracting when trying to
    // parse the quest icons"; from a panel of four, "A": stepped back until it matters) ----

    /// <summary>How strongly a resting boss's octagon is drawn, collar, outline and floor badge alike.</summary>
    public const float BossRestAlpha = 0.7f;

    /// <summary>The width of a resting boss's octagon outline (pixels, before scaling): hollow, no fill.</summary>
    public const float BossRestStroke = 1.5f;

    /// <summary>The size of a resting boss's name (pixels, before scaling): the map's street names', in spaced caps.</summary>
    public const float BossRestNameSize = 11;

    // What a marker's label says as it is drawn: a resting boss its rest label (MapMarker.RestLabel), a step of the gated
    // extract pointed at its name and the extract's (ShownMarker.StepOf), anything else its label.
    private static string LabelOf(ShownMarker m) =>
        m.Resting ? m.Marker.RestLabel ?? m.Marker.Label
        : m.StepOf is { } exit ? MapTexts.MarkerStepOfExit(exit: exit.Label, step: m.Marker.Label)
        : m.Marker.Label;

    // Label priority: what the player picked, then bosses, quests, ways out, snipers, locks and switches.
    // An extract on the player's list this raid is placed with the bosses, ahead of other ways out and of quests: at
    // the raid's end it is the name to find. One the list leaves out comes with the Scav and sniper zones', after the
    // quests' and the other ways out's. The steps a gated extract pointed at lights come after the extract's own name
    // (ShownMarker.StepOf): at the Saferoom its last two steps stand within 5 m of the door.
    private static int LabelRank(ShownMarker m) =>
        m.Selected ? (m.StepOf is null ? 0 : 1) : m.Listed ? 1 : m.NotListed ? 4 : RestRank(m.Marker.Kind);

    /// <summary>A kind's rank at rest, whatever is picked or pointed at: bosses, quests, ways out, Scav and sniper
    /// zones, locks and switches. Where symbols would cover each other, the lesser one yields (<see cref="Repel"/>), and
    /// where they still share a spot (locks under a quest), the one that matters more lies on top.</summary>
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
    /// Where the receded symbols stand that would cover each other (locks, switches, Scav zones' rings; the rest is
    /// <see cref="Repel"/>'s): two of the same rank closer than their widths allow are set side by side, each moved by
    /// half of what is missing and never further from its true place than its own width (the review of 2026-10-04,
    /// C6, for every symbol until 2026-10-06). They part along the line between their true places, so as the view
    /// zooms in and those draw apart, each symbol comes straight back to its own; where they share a place, left and
    /// right, the earlier one on the left. Symbols of different rank aren't moved: the one that matters more lies on
    /// top (Render). The sizes are the symbols' at rest (<see cref="RestHalf"/>), so nothing moves because the pointer
    /// is on it.
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

    private const int RepelPasses = 80;
    private const int RepelSettlePasses = 30;

    // How strongly a symbol is drawn back toward its place on each pass.
    private const float RepelPull = 0.12f;

    /// <summary>How far beyond its own half width no symbol comes to another's place (pixels, before scaling; ggrepel's
    /// point padding): the dot at the end of a leader, with its dark rim, and a little air.</summary>
    public const float PlaceClear = 7;

    /// <summary>How far beyond its own edge a symbol that has to leave its place stands from it (pixels, before
    /// scaling), so its leader is long enough to follow.</summary>
    public const float LeaderLeast = 10;

    /// <summary>
    /// How far from its place a symbol stands at most, centre to place (pixels, before scaling; owner, 2026-10-17: one of
    /// Woods' two Outskirts stood across the view on a leader). Twelve times <see cref="LeaderLeast"/>, about five times
    /// the least a symbol that leaves its place stands off it (21 to 23 px): a fresh layout of the whole of Customs, the
    /// busiest map, at 1600 × 1000 sets none further than 110 px, so in a view that has room for its crowd it holds
    /// nothing back. Where the view packs a crowd tighter than its symbols fit (the whole map at the smallest window), the
    /// search for a free spot sent one up to 360 px across the view; within the bound it is pushed only off its
    /// neighbours' bodies, their collars may meet.
    /// </summary>
    public const float LeaderMost = 120;

    // A symbol pushed off its place by up to this share of its half width is only nudged: it keeps to its place, without
    // a leader. Further, it has to leave it and stands a leader off it.
    private const float NudgedMost = 0.6f;

    /// <summary>
    /// While the view moves (the repel going on from the last placement), a symbol that stands off on a leader goes back
    /// onto its place only once the passes bring it within this share of the nudge that sends one off: a quarter less,
    /// so one whose crowd draws apart loses its leader once, not on and off from frame to frame.
    /// </summary>
    public const float LeaderKept = 0.75f;

    /// <summary>While the view moves, how far a symbol goes from one frame to the next toward where the repel puts it
    /// (pixels, before scaling): it keeps its side, and a leader comes or goes over a few frames, not in one.</summary>
    public const float RepelStep = 2;

    /// <summary>
    /// Where symbols stand that would cover each other, as ggrepel sets labels apart (owner, 2026-10-06: "Whenever there is
    /// overplotting going on, it automatically draws a line between the datapoint and its annotation … it should not be
    /// the case that I need to interact with the map with the mouse in order to see the spots properly"; chosen from a
    /// panel of five ways drawn by the real map on four Streets spots: "Overlap: D"). Each pass pushes every two symbols
    /// that overlap apart along the line between them, keeps every symbol off the others' places, then pulls each a
    /// little back toward its own; the last passes only push, so none is left covering another or another's place.
    /// Before them, a symbol that had to leave its place (more than half its width off it) is set off it by
    /// <paramref name="minLine"/> beyond its edge, so its leader can be followed; one only nudged keeps to its place and
    /// needs no leader. Where ranks differ, the lesser symbol takes the whole step and the one that matters more keeps its
    /// place (<see cref="RestRank"/>); of one rank, each takes half. Symbols sharing one place start a hair apart round
    /// it, the first to the left, so a crowd opens in every direction. Whatever is still wedged at the end goes to the
    /// nearest free spot. Positions depend only on the symbols' places, sizes and ranks at rest, never on what is picked
    /// or pointed at, so nothing moves with the pointer.
    /// </summary>
    /// <param name="symbols">Each symbol's true place on screen, half its width at rest, its rank, and whether it takes part.</param>
    /// <param name="gap">The room kept between two symbols' edges: their dark collars.</param>
    /// <param name="placeClear">Above 0: how far every symbol keeps from the places of the others (but locks' and switches'),
    /// beyond its own half width, so the dot at the end of each leader shows.</param>
    /// <param name="minLine">Above 0: how far beyond its own edge a symbol that has left its place stands from it.</param>
    /// <param name="fixedAround">What no symbol may cover and that never moves itself: the player's marker and its age tag,
    /// as circles (centre, half width).</param>
    /// <param name="maxLine">Above 0: how far from its place a symbol stands at most, centre to place (<see cref="LeaderMost"/>).</param>
    public static SKPoint[] Repel(IReadOnlyList<(SKPoint At, float Half, int Rank, bool Moves)> symbols, float gap, float placeClear = 0, float minLine = 0,
        IReadOnlyList<(SKPoint At, float Half)>? fixedAround = null, float maxLine = 0) =>
        Repel(symbols, gap, placeClear, minLine, maxLine, fixedAround, null, 0, out _);

    /// <summary>A symbol as a placement left it (<see cref="Repel"/>): how far off its place the passes had settled it before
    /// any was set a leader off its place (<paramref name="Settled"/>), how far off it it was drawn
    /// (<paramref name="Shown"/>), whether it had to leave its place (<paramref name="Off"/>), and whether it was drawn on a
    /// leader (<paramref name="Led"/>, which the layout decides).</summary>
    internal readonly record struct Stood(SKPoint Settled, SKPoint Shown, bool Off, bool Led = false);

    /// <summary>
    /// <see cref="Repel"/> afresh, or, while the view moves, from where the last placement left the symbols (owner,
    /// 2026-10-15: "weird movements" in the tour's view move and every Follow glide; docs/DESIGN.md, "Map drawing").
    /// Afresh, 80 passes settle a crowd one way at one zoom and as often another way at the next: in a crowd the passes
    /// swing back and forth and never come to rest, so in a 1.2 s glide on Customs symbols flipped up to 120 px from side
    /// to side, the last step's search for a free spot sent some 60 px off and back every other frame, and leaders came
    /// and went 61 times. From the last placement, the passes go on where they had got to, one a frame, so the crowd
    /// opens and closes with the view instead of being laid out anew each frame; a symbol that stood off on a leader
    /// keeps it until its place is clearly free (<see cref="LeaderKept"/>); and where it is drawn goes at most
    /// <paramref name="step"/> a frame toward where that puts it, so it keeps its side and a leader comes or goes over a few
    /// frames. The push-only passes see that none covers another or the player
    /// (keeping off the others' places is the passes' part, which the symbols follow), within the step too, and past it
    /// only so far as keeps their bodies apart (QA pass 3, 2026-10-15: the whole map at the smallest window, where the
    /// view packs a crowd tighter than its symbols fit). One that comes into the placement makes way for those already
    /// standing, and only it is searched a free spot for.
    /// </summary>
    /// <param name="last">Each symbol as the last placement left it (null for one it didn't have), while the view moves;
    /// null for a fresh start.</param>
    /// <param name="step">How far a symbol the last placement had is drawn from where it was, at most (pixels).</param>
    /// <param name="stood">Each symbol as this placement leaves it, for the next.</param>
    internal static SKPoint[] Repel(IReadOnlyList<(SKPoint At, float Half, int Rank, bool Moves)> symbols, float gap, float placeClear, float minLine,
        float maxLine, IReadOnlyList<(SKPoint At, float Half)>? fixedAround, IReadOnlyList<Stood?>? last, float step, out Stood[] stood)
    {
        var n = symbols.Count;
        var at = symbols.Select(s => s.At).ToArray();
        var obstacles = fixedAround ?? [];
        stood = new Stood[n];
        if (n == 0 || (n < 2 && obstacles.Count == 0))
            return at;
        // A crowd at one place: the k-th of it starts a hair off, round the place by the golden angle, the first to the
        // left (as side by side had it). One the last placement had starts where the passes had settled it.
        for (var i = 0; i < n; i++)
        {
            if (last?[i] is { } was && symbols[i].Moves)
            {
                at[i] = symbols[i].At + was.Settled;
                continue;
            }
            var k = 0;
            for (var j = 0; j < i; j++)
            {
                if (SKPoint.Distance(symbols[j].At, symbols[i].At) < 0.01f)
                    k++;
            }
            if (k > 0)
            {
                var angle = MathF.PI + k * 2.39996f;
                at[i] = new SKPoint(at[i].X + 0.5f * MathF.Cos(angle), at[i].Y + 0.5f * MathF.Sin(angle));
            }
            else if (Enumerable.Range(i + 1, n - i - 1).Any(j => SKPoint.Distance(symbols[j].At, symbols[i].At) < 0.01f))
            {
                at[i] = new SKPoint(at[i].X - 0.5f, at[i].Y);
            }
        }
        // While the view moves, the passes go on from where they were, one a frame.
        var passes = last is null ? RepelPasses : 1;
        var widest = symbols.Where(s => s.Moves).Select(s => s.Half).DefaultIfEmpty(0).Max();
        var order = Enumerable.Range(0, n).Where(i => symbols[i].Moves).ToArray();
        // The places kept in sight, by x, for a sweep: those of every symbol taking part but locks and switches (level 4).
        var places = placeClear <= 0 ? [] : order.Where(i => symbols[i].Rank < 5).OrderBy(i => symbols[i].At.X).ToArray();
        var placeXs = places.Select(i => symbols[i].At.X).ToArray();
        bool OnPlace(int i, SKPoint p, int j) => j != i && SKPoint.Distance(p, symbols[j].At) < symbols[i].Half + placeClear - 0.01f;
        // While the view moves, the push-only passes keep the symbols off each other, not off each other's places: that the
        // passes see to (the one a frame), and the symbols follow them a step a frame. And one that comes into the
        // placement (into view, or out of a cluster that parts) makes way for those already standing, whatever its rank,
        // so they don't jump aside for it.
        var follows = last is not null;
        bool New(int i) => follows && last![i] is null;
        (float, float) Yields(int i, int j) => New(i) == New(j) ? (0.5f, 0.5f) : New(i) ? (1f, 0f) : (0f, 1f);
        // Where ranks differ, the lesser symbol takes the whole step; of one rank, each half.
        (float, float) Ranks(int i, int j) => symbols[i].Rank == symbols[j].Rank ? (0.5f, 0.5f) : symbols[i].Rank < symbols[j].Rank ? (0f, 1f) : (1f, 0f);
        // Every two symbols closer than their halves and room together pushed apart along the line between them, by what
        // is missing, each by its share; a sweep along x, as only symbols closer than the two widest together can overlap.
        bool PushApart(float room, Func<int, int, (float, float)> shares)
        {
            Array.Sort(order, (a, b) => at[a].X.CompareTo(at[b].X));
            var moved = false;
            for (var a = 0; a < order.Length; a++)
            {
                var i = order[a];
                for (var b = a + 1; b < order.Length; b++)
                {
                    var j = order[b];
                    if (at[j].X - at[i].X >= 2 * widest + room)
                        break;
                    var want = symbols[i].Half + symbols[j].Half + room;
                    var line = at[j] - at[i];
                    var apart = line.Length;
                    if (apart >= want - 0.01f)
                        continue;
                    var along = apart < 0.001f ? new SKPoint(1, 0) : new SKPoint(line.X / apart, line.Y / apart);
                    var (shareI, shareJ) = shares(i, j);
                    var push = want - apart;
                    at[i] = new SKPoint(at[i].X - along.X * push * shareI, at[i].Y - along.Y * push * shareI);
                    at[j] = new SKPoint(at[j].X + along.X * push * shareJ, at[j].Y + along.Y * push * shareJ);
                    moved = true;
                }
            }
            return moved;
        }
        // Off what never moves (the player and its age tag), room kept, by the whole step; from the very middle, to the
        // left, away from the age tag.
        bool OffWhatStays(float room)
        {
            var moved = false;
            foreach (var i in order)
            {
                foreach (var (centre, half) in obstacles)
                {
                    var want = symbols[i].Half + half + room;
                    var line = at[i] - centre;
                    var apart = line.Length;
                    if (apart >= want - 0.01f)
                        continue;
                    var away = apart > 0.001f ? new SKPoint(line.X / apart, line.Y / apart)
                        : SKPoint.Distance(symbols[i].At, centre) > 0.001f ? Unit(symbols[i].At - centre) : new SKPoint(-1, 0);
                    at[i] = new SKPoint(at[i].X + away.X * (want - apart), at[i].Y + away.Y * (want - apart));
                    moved = true;
                }
            }
            return moved;
        }
        // No further from its place than the most (LeaderMost), whatever pushed it; after each pass, so the passes after it
        // open the crowd within it.
        void HoldIn()
        {
            if (maxLine <= 0)
                return;
            foreach (var i in order)
                at[i] = Within(at[i], symbols[i].At, maxLine);
        }
        var off = new bool[n];
        for (var pass = 0; pass < passes + RepelSettlePasses; pass++)
        {
            // Before the last passes: a symbol that had to leave its place (more than half its width off it) stands off
            // it by its leader's least length; one only nudged stays where it is. While the view moves, one that stood
            // off keeps standing off until it would be back within less (LeaderKept), and each is drawn at most a step
            // from where it was toward where that puts it.
            if (pass == passes)
            {
                foreach (var i in order)
                {
                    var settled = at[i] - symbols[i].At;
                    stood[i] = stood[i] with { Settled = settled };
                    var nudged = symbols[i].Half * NudgedMost * (last?[i] is { Off: true } ? LeaderKept : 1);
                    off[i] = minLine > 0 && settled.Length > nudged;
                    var least = symbols[i].Half + gap + minLine;
                    if (off[i] && settled.Length < least)
                        at[i] = new SKPoint(symbols[i].At.X + settled.X / settled.Length * least, symbols[i].At.Y + settled.Y / settled.Length * least);
                    if (last?[i] is { } was)
                        at[i] = symbols[i].At + Within(at[i] - symbols[i].At, was.Shown, step);
                }
            }
            var moved = PushApart(gap, follows && pass >= passes ? Yields : Ranks);
            // Off the others' places, by the whole step: a place doesn't move.
            if (places.Length > 0 && !(follows && pass >= passes))
            {
                foreach (var i in order)
                {
                    var reach = symbols[i].Half + placeClear;
                    var from = Array.BinarySearch(placeXs, at[i].X - reach);
                    for (var b = from < 0 ? ~from : from; b < places.Length && placeXs[b] <= at[i].X + reach; b++)
                    {
                        var j = places[b];
                        if (!OnPlace(i, at[i], j))
                            continue;
                        var line = at[i] - symbols[j].At;
                        var apart = line.Length;
                        // On the very place: out the way its own place lies from it, or to the left.
                        var away = apart > 0.001f ? new SKPoint(line.X / apart, line.Y / apart)
                            : SKPoint.Distance(symbols[i].At, symbols[j].At) > 0.001f ? Unit(symbols[i].At - symbols[j].At) : new SKPoint(-1, 0);
                        var push = reach - apart;
                        at[i] = new SKPoint(at[i].X + away.X * push, at[i].Y + away.Y * push);
                        moved = true;
                    }
                }
            }
            moved |= OffWhatStays(gap);
            HoldIn();
            if (pass < passes)
            {
                foreach (var i in order)
                    at[i] = new SKPoint(at[i].X + (symbols[i].At.X - at[i].X) * RepelPull, at[i].Y + (symbols[i].At.Y - at[i].Y) * RepelPull);
            }
            else if (!moved)
            {
                break;
            }
        }
        if (follows)
        {
            // While the view moves, pushes and all, each symbol the last placement had is drawn at most a step from where
            // it was: in a crowd the view packs tighter than its symbols fit, the push-only passes moved standing symbols
            // up to 9.6 px a frame (QA pass 3, 2026-10-15: the roll call's zoom out at the smallest window, 7.7 % a frame
            // at 30 frames a second). Past the step a push takes one only as far as keeps its body off another's and off
            // the player: for a few frames neighbours may stand in each other's collars, and the passes of the next
            // frames open them again as the view slows.
            foreach (var i in order)
            {
                if (last![i] is { } was)
                    at[i] = symbols[i].At + Within(at[i] - symbols[i].At, was.Shown, step);
            }
            for (var pass = 0; pass < RepelSettlePasses; pass++)
            {
                var moved = PushApart(0, Yields);
                moved |= OffWhatStays(0);
                HoldIn();
                if (!moved)
                    break;
            }
        }
        // A lesser symbol can be wedged between two that keep their places, pushed back and forth for good: whatever
        // still covers another goes to the nearest place round it where it covers none, the least important first. While
        // the view moves, only one new to the placement does, and makes way for those already standing; one that stood
        // stays where the passes left it, and the next frame's passes go on from there: in a crowd the view packs tighter
        // than its symbols fit (the whole map at the smallest window), the nearest free spot can lie across it, and the
        // search sent symbols that only touched a neighbour's collar up to 144 px off and back (QA pass 3, 2026-10-15).
        bool Clear(int i, SKPoint p) => order.All(j => j == i || SKPoint.Distance(p, at[j]) >= symbols[i].Half + symbols[j].Half + gap - 0.01f)
                                        && (follows || places.All(j => !OnPlace(i, p, j)))
                                        && obstacles.All(o => SKPoint.Distance(p, o.At) >= symbols[i].Half + o.Half + gap - 0.01f);
        var searched = new bool[n];
        foreach (var i in order.Where(i => !follows || New(i)).OrderByDescending(i => symbols[i].Rank).ThenBy(i => i))
        {
            if (Clear(i, at[i]))
                continue;
            searched[i] = true;
            var from = at[i];
            // Never further from its place than the most (LeaderMost): past it, no spot on the ring is near enough.
            var within = maxLine > 0 ? maxLine + SKPoint.Distance(from, symbols[i].At) : float.MaxValue;
            for (var radius = 1f; radius < 40 * (symbols[i].Half + gap) && radius <= within && !Clear(i, at[i]); radius += 1)
            {
                // Round the ring from the side its own place lies on, so it stays as near to it as it can.
                var toward = MathF.Atan2(symbols[i].At.Y - from.Y, symbols[i].At.X - from.X);
                for (var k = 0; k < 24; k++)
                {
                    var angle = toward + (k % 2 == 0 ? 1 : -1) * ((k + 1) / 2) * MathF.PI / 12;
                    var p = new SKPoint(from.X + radius * MathF.Cos(angle), from.Y + radius * MathF.Sin(angle));
                    if (maxLine > 0 && SKPoint.Distance(p, symbols[i].At) > maxLine)
                        continue;
                    if (Clear(i, p))
                    {
                        at[i] = p;
                        break;
                    }
                }
            }
        }
        // Where the search found no spot within the most for one (a crowd the view packs tighter than its symbols fit), what
        // still covers another is pushed only so far as keeps their bodies apart, within the most, as a move's frames do:
        // left covering, the next frame of a move pushed it at once (up to 22 px in the whole of Customs at the smallest
        // window).
        var wedged = maxLine > 0 && order.Any(i => searched[i] && !Clear(i, at[i]));
        for (var pass = 0; wedged && pass < RepelSettlePasses; pass++)
        {
            var moved = PushApart(0, follows ? Yields : Ranks);
            moved |= OffWhatStays(0);
            HoldIn();
            if (!moved)
                break;
        }
        // Afresh or going on, whatever pushed it: over a neighbour rather than on a leader across the view.
        HoldIn();
        foreach (var i in order)
        {
            // From the last placement, one whose place is free again is drawn back to within a hair of it (12 % a frame):
            // it goes onto it, where a fresh start leaves it.
            if (last is not null && SKPoint.Distance(at[i], symbols[i].At) < 0.05f)
                at[i] = symbols[i].At;
            // The next passes go on from where it is drawn where that is the other side of its place from where they had
            // it (the push-only passes took it round the player or past a neighbour), or where the search put it: going
            // on from their own side, it would cross over its place to get back. As far off its place as the passes had
            // it, not at its leader's end, or nothing would bring it back.
            var (shown, settled) = (at[i] - symbols[i].At, stood[i].Settled);
            if (searched[i])
                settled = shown;
            else if (shown.X * settled.X + shown.Y * settled.Y < 0)
                settled = new SKPoint(shown.X / shown.Length * settled.Length, shown.Y / shown.Length * settled.Length);
            if (maxLine > 0)
                settled = Within(settled, new SKPoint(0, 0), maxLine);
            stood[i] = new Stood(settled, shown, off[i]);
        }
        return at;
    }

    private static SKPoint Unit(SKPoint v) => new(v.X / v.Length, v.Y / v.Length);


    /// <summary>Whether a kind is set apart by <see cref="Repel"/>: all but the receded level 4 and the Scav zones' rings.</summary>
    public static bool Repels(MarkerKind kind) => kind is not (MarkerKind.ScavSpawn or MarkerKind.Lock or MarkerKind.Switch);

    /// <summary>Half a symbol's width as the repel sees it: its body at rest, and a little more where a badge stands
    /// out at a corner (a count, "OPT", "?": <see cref="BadgeRoom"/>; a floor arrow or a switch badge, the larger discs:
    /// <see cref="FloorBadgeRoom"/>), so no neighbour is pushed under a badge.</summary>
    public static float RepelHalf(ShownMarker m, float ui) =>
        RestHalf(m.Marker, ui) + (m.Floor != 0 || ShowsSwitchBadge(m) ? FloorBadgeRoom : m.Count > 1 || ShowsOptional(m) || ShowsPossible(m) ? BadgeRoom : 0) * ui
        + JoinedExtra(m, ui);

    /// <summary>How much wider a symbol with a corner badge counts for the repel (pixels, before scaling).</summary>
    public const float BadgeRoom = 3;

    /// <summary>
    /// The same for a floor arrow: the 3 DIP every badge had, and 3 more since its disc grew (owner, 2026-10-16: from a
    /// radius of 5.5 DIP to 7, its rim from 1.2 to 1.5, its centre 1 DIP further out each way). Along the diagonal it
    /// reaches 3.1 DIP further from its symbol than it did, so a neighbour set as close as the repel allows comes no
    /// further into the larger disc than it came into the smaller one.
    /// </summary>
    public const float FloorBadgeRoom = 6;

    /// <summary>How far a symbol must stand from its place before a leader joins the two: clear of its own body, so
    /// the dot at the place shows. Closer, it still covers its place and needs none.</summary>
    public static float LeaderFrom(MapMarker marker, float ui) => RestHalf(marker, ui) + (MarkerCollar + 2) * ui;

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

    /// <summary>How far in from the map's left edge a marker label starts at least (pixels, before scaling): its halo whole.</summary>
    public const float LabelEdge = 2;

    /// <summary>
    /// A label candidate kept whole at the map's left edge (owner, 2026-10-15: at the smallest window "ansit to
    /// Interchange" on Streets): one above or below its symbol that would start past <paramref name="edge"/> slides right
    /// to start there, still over or under the symbol; one beside it can't move without leaving its symbol or covering
    /// it, so it is not a place for the label (null), and the next side is tried. A label none of whose sides is left is
    /// dropped, as one whose four sides are taken is. The right edge keeps its rule: the label room on the right
    /// (<see cref="Camera.LabelRoom"/>), and at the smallest window a long name may lose its last letters there; a picked
    /// or pointed-at name's is kept whole at <paramref name="right"/> the same way (<see cref="LitNameMost"/>).
    /// </summary>
    /// <param name="at">The symbol's centre, where it is drawn.</param>
    /// <param name="edge">The least left of a label (pixels).</param>
    /// <param name="right">The most right of a label (pixels): the map's right edge for a picked or pointed-at name, none
    /// for the others.</param>
    public static SKRect? Inward(SKRect candidate, SKPoint at, float edge, float right = float.PositiveInfinity)
    {
        if (candidate.Left >= edge && candidate.Right <= right)
            return candidate;
        var overOrUnder = candidate.Left < at.X && candidate.Right > at.X;
        if (!overOrUnder || at.X < edge || at.X > right)
            return null;
        candidate.Offset(candidate.Left < edge ? edge - candidate.Left : right - candidate.Right, 0);
        return candidate;
    }

    /// <summary>
    /// The widest a picked or pointed-at name moves in from the map's right edge (pixels, before scaling; owner,
    /// 2026-10-15, E3: at full size the tour's German picked quest read "Lieferung aus der Vergan" at the edge, past
    /// the 150 DIP the view keeps there, <see cref="Camera.LabelRoom"/>). Where its right side would run past the edge,
    /// the name goes left of its symbol, or above or below it slid in to end at the edge (<see cref="Inward"/>), whole up
    /// to this width or <see cref="LitNameShare"/> of the map's, whichever is less, and trimmed with "…" to it beyond.
    /// 260 holds every quest name in tarkov.dev's data whole but one in the bold label face (October 2026: English's
    /// longest 251, German's 256 and 307, "Weg des Überlebenskünstlers - Ungeschützt, aber gefährlich"; the tour's 166).
    /// One with no other side free stays on its right, trimmed with "…" at the edge, never cut (<see cref="Trimmed"/>).
    /// </summary>
    public const float LitNameMost = 260;

    /// <summary>The share of the map's width a picked or pointed-at name moved in from its right edge takes at most
    /// (<see cref="LitNameMost"/>): at the smallest window, the map about 500 DIP wide, 200 DIP, which hold 95 % of the
    /// German quest names and 97 % of the English whole.</summary>
    public const float LitNameShare = 0.4f;

    /// <summary>
    /// <paramref name="text"/> as it fits in <paramref name="width"/> pixels in <paramref name="font"/>: whole, or its
    /// longest start that fits with "…" after it (a dash, comma or space before the "…" left out); null where not even its
    /// first letter does.
    /// </summary>
    public static string? Trimmed(SKFont font, string text, float width)
    {
        if (font.MeasureText(text) <= width)
            return text;
        for (var n = text.Length - 1; n > 0; n--)
        {
            if (char.IsLowSurrogate(text[n]))
                continue;
            var start = text[..n].TrimEnd(' ', '-', '–', '—', ',', ':', '·');
            if (start.Length > 0 && font.MeasureText(start + "…") <= width)
                return start + "…";
        }
        return null;
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
    /// Last, symbols that would still cover each other are set apart (<see cref="Repel"/>; the receded ones side by side,
    /// <see cref="SideBySide"/>): their <see cref="ShownMarker.At"/> is where they are drawn, <see cref="ShownMarker.Beside"/>
    /// says so, and <see cref="ShownMarker.Leader"/> holds the place of one set so far off it that a leader joins them.
    /// </summary>
    /// <param name="now">The frame's time (<see cref="Render"/>'s): no symbol covers the player's age tag. The clock's when null.</param>
    public static List<ShownMarker> ShownMarkers(Camera camera, MapScene scene, float ui, DateTime? now = null)
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
        // In the data's order, not the drawing order below: which of two goes left must not turn on what is pointed at.
        JoinSpots(result, ui);
        result.Sort((a, b) => a.Index.CompareTo(b.Index));
        var (places, led) = SetApart(camera, scene, result.Select(r => r.Shown).ToList(), ui, now ?? DateTime.Now);
        return result
            .Select((r, i) => (r.Index, Shown: places[i] == r.Shown.At ? r.Shown : r.Shown with
            {
                At = places[i],
                Beside = true,
                Leader = led[i] ? r.Shown.At : null,
            }))
            .OrderBy(r => r.Shown.Focused ? 2 : r.Shown.Selected ? 1 : 0)
            .ThenBy(r => r.Index)
            .Select(r => r.Shown)
            .ToList();
    }

    // Where each symbol stands: quests, bosses and ways out set apart on leaders (Repel), the receded locks, switches
    // and Scav zones' rings side by side among themselves, under the rest (their leaders were a web of ink between the
    // quests). The repel is most of a layout's work, and a pan lays the map out for every frame: while what it is given
    // is the same (a pan moves every place alike; a pick, the pointer or a list read moves none), the last offsets hold.
    // While the view moves (a glide, a pinch, a wheel's notch over 0.15 s, a pan that brings symbols into view or takes
    // them out of it), the repel goes on from the last placement, so nothing jumps from one frame to the next (owner,
    // 2026-10-15: "weird movements"; Repel). Anything else (the map opened, a zoom at once: a double-click, + or −, a
    // notch with Windows' animation effects off; a new position, other symbols at the same view) lays them out afresh,
    // as a still view always shows them.
    private static (SKPoint[] At, bool[] Led) SetApart(Camera camera, MapScene scene, List<ShownMarker> markers, float ui, DateTime now)
    {
        var places = SideBySide(markers.Select(m => (m.At, RestHalf(m.Marker, ui), RestRank(m.Marker.Kind))).ToList(), MarkerCollar * ui);
        var (player, ageTag) = PlayerObstacles(camera, scene, ui, now);
        var symbols = markers.Select(m => (m.At, RepelHalf(m, ui), RestRank(m.Marker.Kind), Repels(m.Marker.Kind))).ToArray();
        var ids = RepelKeys(markers);
        if (scene.LastRepel is not { } last || !last.Holds(ids, symbols, player))
        {
            var view = new RepelView(camera.Zoom, camera.Center, ui, scene.Player, ageTag);
            var from = scene.LastRepel is { } before && before.View.GlidesTo(view) ? before.From(ids) : null;
            // Zooming out, the view packs the crowd tighter.
            var packs = from is not null && camera.Zoom < scene.LastRepel!.View.Zoom;
            var repelled = Repel(symbols, MarkerCollar * ui, PlaceClear * ui, LeaderLeast * ui, LeaderMost * ui, player, from, RepelStep * ui, out var stood);
            var offsets = repelled.Select((p, i) => p - markers[i].At).ToArray();
            // A leader joins one that stands clear of its place's dot. While the view moves, one drawn a step at a time
            // passes that distance, and may stay near it: its leader comes once it is clearly past, and goes once it is
            // clearly back. While the view zooms out, not before the passes no longer have it off its place (Stood.Off): a
            // crowd pushed about as the view packs it can take one back over its place's dot for a few frames while it still
            // has to stand off, and its leader went and came again (QA pass 3, 2026-10-15: 8 symbols' leaders more than once
            // in the roll call's zoom out at the smallest window).
            for (var i = 0; i < stood.Length; i++)
            {
                var slack = from?[i] is { } was ? (was.Led ? -LeaderSlack : LeaderSlack) * ui : 0;
                var off = SKPoint.Distance(markers[i].At + offsets[i], markers[i].At);
                stood[i] = stood[i] with
                {
                    Led = symbols[i].Item4 && (off > LeaderFrom(markers[i].Marker, ui) + slack || (packs && from?[i] is { Led: true } && stood[i].Off)),
                };
            }
            last = new RepelPlacement(ids, symbols, [.. player], offsets, stood, view);
            scene.LastRepel = last;
            scene.LastRepelRun = from is null ? RepelRun.Afresh : RepelRun.Continued;
        }
        else
        {
            scene.LastRepelRun = RepelRun.Held;
        }
        var led = new bool[places.Length];
        for (var i = 0; i < places.Length; i++)
        {
            if (!Repels(markers[i].Marker.Kind))
                continue;
            places[i] = markers[i].At + last.Offsets[i];
            led[i] = last.Led(i);
        }
        return (places, led);
    }

    // Each symbol's key from one placement to the next: its marker's id and kind, and, where even both are another's too,
    // how many such came before it. tarkov.dev gives two extracts one id (Woods' two Outskirts, a PMC's and a Scav's, less
    // than a metre apart; its two UN Roadblocks): keyed by the id alone, both went on from the first's placement at every
    // frame of a move, pushed each other out along one line by 7.4 px a frame and never settled, so a few glides sent one
    // across the view on a leader (owner, 2026-10-17: "they get expanded every time when the user pans or zooms the camera").
    private static string[] RepelKeys(List<ShownMarker> markers)
    {
        var seen = new Dictionary<string, int>(markers.Count, StringComparer.Ordinal);
        return [.. markers.Select(m =>
        {
            var key = m.Marker.Id + "|" + m.Marker.Kind;
            var n = seen[key] = seen.GetValueOrDefault(key) + 1;
            return n == 1 ? key : key + "#" + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
        })];
    }

    // How much further than it takes for a leader one drawn a step at a time while the view moves must go before its
    // leader comes, and how much nearer before it goes (pixels, before scaling).
    private const float LeaderSlack = 2.5f;

    /// <summary>The view a placement was made for, and what else the repel turns on that doesn't show in its input's
    /// shape: the player's fix and its age tag.</summary>
    internal readonly record struct RepelView(double Zoom, MapPoint Center, float Ui, PlayerFix? Player, string AgeTag)
    {
        // A frame of a glide or a pinch zooms by a few per cent at most (the tour's 1.2 s glide to three times the zoom:
        // up to 4 % a frame at 60 frames a second; a wheel's notch, a fifth over 0.15 s: up to 3 %, and a quick turn
        // under a tenth up to a notch every 30 ms; + and −, half again over 0.2 s: up to 5 %; a double-click, twice over
        // 0.2 s: up to 9 %, WheelZoom). With the animation effects off a double-click zooms by 100 % at once, + and − by
        // 50 %, a notch by 20 %.
        private const double GlideMost = 1.1;

        /// <summary>Whether the view only moved from this one to <paramref name="next"/>, as a frame of a glide does: it
        /// panned or zoomed by less than a tenth, with the same position and display scale.</summary>
        public bool GlidesTo(RepelView next) =>
            (next.Zoom != Zoom || next.Center != Center) && Math.Abs(Math.Log(next.Zoom / Zoom)) < Math.Log(GlideMost)
            && next.Ui == Ui && Equals(next.Player, Player) && next.AgeTag == AgeTag;
    }

    /// <summary>
    /// The last repel (<see cref="Repel"/>): what it was given, the symbols by their markers' ids, the view, and where it
    /// left each. It holds while it would be given the same again, and the next starts from it while the view moves.
    /// </summary>
    internal sealed class RepelPlacement(string[] ids, (SKPoint At, float Half, int Rank, bool Moves)[] symbols, (SKPoint At, float Half)[] around,
        SKPoint[] offsets, Stood[] stood, RepelView view)
    {
        /// <summary>How far it set each symbol off its place, in the order it was given them.</summary>
        public SKPoint[] Offsets { get; } = offsets;

        /// <summary>Whether the i-th symbol is drawn on a leader.</summary>
        public bool Led(int i) => stood[i].Led;

        public RepelView View { get; } = view;

        // Places a pan moved alike are the same: compared from the first symbol's place, to a hundredth of a pixel (a
        // pan's sums round differently in the last bits).
        private const float Same = 0.01f;

        /// <summary>Whether a repel would be given what this one was: the same symbols with the same sizes and ranks, their
        /// places the same distances apart, and what they keep clear of where it was from them.</summary>
        public bool Holds(string[] nowIds, (SKPoint At, float Half, int Rank, bool Moves)[] now, List<(SKPoint At, float Half)> nowAround)
        {
            if (nowIds.Length != ids.Length || now.Length != symbols.Length || nowAround.Count != around.Length)
                return false;
            if (now.Length == 0)
                return true;
            var (origin, was) = (now[0].At, symbols[0].At);
            bool Near(SKPoint a, SKPoint b) => Math.Abs(a.X - origin.X - (b.X - was.X)) <= Same && Math.Abs(a.Y - origin.Y - (b.Y - was.Y)) <= Same;
            for (var i = 0; i < now.Length; i++)
            {
                if (!string.Equals(nowIds[i], ids[i], StringComparison.Ordinal) || now[i].Half != symbols[i].Half || now[i].Rank != symbols[i].Rank
                    || now[i].Moves != symbols[i].Moves || !Near(now[i].At, symbols[i].At))
                {
                    return false;
                }
            }
            for (var i = 0; i < around.Length; i++)
            {
                if (nowAround[i].Half != around[i].Half || !Near(nowAround[i].At, around[i].At))
                    return false;
            }
            return true;
        }

        /// <summary>Each of these symbols as this placement left it, by its marker's id; null for one it didn't have.</summary>
        public Stood?[] From(string[] nowIds)
        {
            var by = new Dictionary<string, Stood>(ids.Length, StringComparer.Ordinal);
            for (var i = 0; i < ids.Length; i++)
                by.TryAdd(ids[i], stood[i]);
            return [.. nowIds.Select(id => by.TryGetValue(id, out var was) ? was : (Stood?)null)];
        }
    }

    // What no symbol may cover (owner, 2026-10-06: "the player icon is also an icon - if you are right next to a
    // objective the player icon is drawn on top of the quest icon"): the player's ring with its dark band, and the age
    // tag beside it, as circles along it. The player is level 1 and never moves for a symbol; the facing cone and the
    // glow are see-through and hide nothing. With the tag's words, for the repel's memo: they change with the minute.
    private static (List<(SKPoint At, float Half)> Circles, string AgeTag) PlayerObstacles(Camera camera, MapScene scene, float ui, DateTime now)
    {
        if (scene.Player is not { } player)
            return ([], "");
        var at = Screen(camera, scene, player.Position);
        var circles = new List<(SKPoint, float)> { (at, (PlayerRing + 2.5f) * ui) };
        if (PlayerAgeTag(camera, scene, ui, now) is not { } tag)
            return (circles, "");
        var (box, text) = tag;
        var r = box.Height / 2;
        for (var x = box.Left + r; ; x += r)
        {
            circles.Add((new SKPoint(Math.Min(x, box.Right - r), box.MidY), r + 1 * ui));
            if (x >= box.Right - r)
                break;
        }
        return (circles, text);
    }

    /// <summary>The player's age tag, where it stands and what it says, once the position is a minute old; else null.</summary>
    /// <param name="now">The frame's time (<see cref="Render"/>'s); the clock's when null.</param>
    internal static (SKRect Box, string Text)? PlayerAgeTag(Camera camera, MapScene scene, float ui, DateTime? now = null)
    {
        if (scene.Player is not { } player || Shturmap.Core.Logs.WallClock.Elapsed(player.At, now ?? DateTime.Now) is var age && age < PlayerOld)
            return null;
        var (text, stale) = AgeTag(age);
        using var font = new SKFont(TypefaceBold, AgeTagSize(stale) * ui);
        return (AgeTagBox(Screen(camera, scene, player.Position), font.MeasureText(text), stale, ui), text);
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
    private static string OptionalTag => MapTexts.MarkerOptionalBadge;

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

    // An extract the game's list marked "??:??:??" (MapScene.ExitsUnsure) has its "?" in its triangle (DrawExtract).
    private static bool ShowsPossible(ShownMarker m) => m.Marker.Kind == MarkerKind.PossibleLocation;

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
        // A switch or lock that is a step of a gated extract pointed at (its marker, its rows): in the extract's colour,
        // named for it (owner, 2026-10-17, from a panel of three: "A").
        var stepOf = focused && IsLandmark(marker.Kind) && scene.ExitsOfSteps.TryGetValue(marker.Id, out var exits)
            ? exits.FirstOrDefault(e => IsFocused(scene, e))
            : null;
        if (stepOf is not null)
            color = ColorOf(stepOf.Kind);
        var inFocus = scene.ShownFocus.Count == 0 || focused;
        return new ShownMarker(marker, at, r, color, selected, kept, inFocus && scene.ShownFocus.Count > 0, FloorOffset(scene, marker.Position))
        {
            StepOf = stepOf,
            Reach = reach + 1 * ui,
            Pointed = pointed && !done,
            // A boss is lit while pointed at (in the lines, on the map) and while a quest that targets it is pointed at or
            // held (in the focus) or picked; else it rests stepped back.
            Resting = marker.Kind == MarkerKind.BossSpawn && !selected && !IsKeptBoss(scene, marker),
            NotListed = scene.ExitsNotListed.Contains(marker.Id),
            Listed = scene.ExitsListed.Contains(marker.Id),
            Unsure = scene.ExitsUnsure.Contains(marker.Id),
        };
    }

    /// <param name="now">The frame's time: a listed extract's name is bold from the moment a list just read lights it (<see cref="PlacedLabel.Unlit"/>).</param>
    /// <param name="traced">What a developer's trace records of the frame's symbols (<see cref="Render"/>), or null: the face its name was drawn in.</param>
    private static void DrawLabel(SKCanvas canvas, MapScene scene, PlacedLabel label, DateTime now, Dictionary<ShownMarker, SymbolTrace>? traced = null)
    {
        var (box, baseline, size, bold) = label.Unlit is { } unlit && ArrivalOf(scene, label.Of.Marker, now) < 0
            ? (unlit.Box, unlit.Baseline, unlit.Size, false)
            : (label.Box, label.Baseline, label.Size, label.Bold);
        if (traced is not null && traced.TryGetValue(label.Of, out var trace))
            trace.Bold = bold;
        using var font = new SKFont(bold ? TypefaceBold : Typeface, size);
        var ui = size / (label.Tracking > 0 ? BossRestNameSize : bold ? 13 : 11.5f);
        using var shadow = new SKPaint { Color = Background.WithAlpha(220), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 * ui };
        using var text = new SKPaint { Color = label.Color, IsAntialias = true };
        if (label.Tracking > 0)
        {
            // Spaced caps, letter by letter, as the map's landmarks are: Skia has no letter spacing.
            foreach (var paint in new[] { shadow, text })
            {
                var x = box.Left;
                foreach (var c in label.Text)
                {
                    var letter = c.ToString();
                    canvas.DrawText(letter, x, baseline, SKTextAlign.Left, font, paint);
                    x += font.MeasureText(letter) + label.Tracking;
                }
            }
            return;
        }
        canvas.DrawText(label.Text, box.Left, baseline, SKTextAlign.Left, font, shadow);
        canvas.DrawText(label.Text, box.Left, baseline, SKTextAlign.Left, font, text);
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

    /// <summary>The zoom as a multiple of the zoom that shows the whole map in this view (as "show the whole map" does,
    /// with the label room on the right).</summary>
    public static double ZoomOverOverview(Camera camera, MapScene scene, float ui) =>
        camera.Zoom / camera.Overview(scene.Projection.WorldRect, 24 * ui, Camera.LabelRoom * ui);

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

    // A boss a picked quest targets on this map (MapScene.KeptBosses).
    private static bool IsKeptBoss(MapScene scene, MapMarker m) =>
        m.Kind == MarkerKind.BossSpawn && m.Group is not null && scene.KeptBosses.Contains(m.Group);

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

    // The other-floor badge's sizes (DIP; owner, 2026-10-16, docs/DESIGN.md, "Visual language"): the disc's radius and
    // its rim; the caret's half width and half height between its line's centres, and its stroke; two or more floors
    // away the plate, where the caret and the digit stand, each centred this far in from its end, and the digit's size.
    internal const float FloorDisc = 7, FloorRim = 1.5f;
    internal const float FloorCaretHalfWidth = 3.4f, FloorCaretHalfHeight = 1.4f, FloorCaretStroke = 2.2f;
    internal const float FloorPlateWidth = 22, FloorPlateHeight = 15, FloorPlateCaretIn = 6.75f, FloorPlateDigitIn = 5.25f, FloorDigit = 11;

    // The floor arrow sits at the marker's upper right (the lower right is the cluster badge's); beside small symbols
    // it moves out so they stay visible. 1 DIP further out each way than the smaller disc of before 2026-10-16 stood,
    // so the larger one covers no more of its own symbol.
    internal static SKPoint FloorBadgeCenter(ShownMarker m, float ui)
    {
        var r = m.Marker.Kind switch
        {
            MarkerKind.ScavSpawn => 4 * ui,
            MarkerKind.SniperSpawn => 6.5f * ui,
            MarkerKind.BossSpawn => m.R * 1.25f,
            _ => m.R,
        };
        var d = (r < 8 * ui ? r + 3 * ui : r * 0.8f) + 1 * ui;
        // Above a tab (MapJoin), not on it.
        return new SKPoint(m.At.X + d, m.At.Y - d - (HasTab(m) ? 4 * ui : 0));
    }

    // The disc's square (with half a DIP of its rim), or the plate, whose caret stands where the disc's would.
    internal static SKRect FloorBadgeBox(ShownMarker m, float ui)
    {
        var c = FloorBadgeCenter(m, ui);
        return Math.Abs(m.Floor) > 1
            ? SKRect.Create(c.X - FloorPlateCaretIn * ui, c.Y - FloorPlateHeight / 2 * ui, FloorPlateWidth * ui, FloorPlateHeight * ui)
            : Square(c, (FloorDisc + 0.5f) * ui);
    }

    /// <summary>How far a name keeps off a floor badge (DIP; QA, 2026-10-16: at 3× on Interchange the picked "Minute of
    /// Fame" started 0 to 1 px from its ⌃2 plate's rim and read "⌃2 Minute of Fame"; with the smaller plate of before
    /// 2026-10-16 there had been about 6 px).</summary>
    internal const float FloorBadgeClear = 3;

    // Where no name goes for a floor badge (Layout's taken boxes): its box and FloorBadgeClear around it. The badge is
    // drawn, and the repel counts it, as before.
    internal static SKRect FloorBadgeKeepOut(ShownMarker m, float ui) =>
        SKRect.Inflate(FloorBadgeBox(m, ui), FloorBadgeClear * ui, FloorBadgeClear * ui);

    // A side of a marker's name (LabelCandidates' order: right, left, above, below, then a pair's further below) clear of
    // its own floor badge but inside its FloorBadgeClear moves out along that side by what it lacks, as a name after a
    // tab does, rather than giving the side up: a name right of a one-floor disc stood 1.5 DIP off its square, and
    // keeping the margin by the taken boxes alone sent every such name to the disc's left. A side over the badge itself
    // stays where it is, for the taken boxes to rule out. A side within a switch badge's keep-out moves out past it
    // whether or not it is over the badge itself: at an extract's lower left the badge reaches into the sides left of
    // and below it, and in The Lab, where the floor arrow takes the right of every gated extract, its name had no side left.
    internal static SKRect OffBadges(ShownMarker m, SKRect candidate, int side, float ui)
    {
        if (m.Floor != 0 && !candidate.IntersectsWith(FloorBadgeBox(m, ui)))
            candidate = MovedOut(candidate, side, FloorBadgeKeepOut(m, ui));
        if (ShowsSwitchBadge(m))
            candidate = MovedOut(candidate, side, SwitchBadgeKeepOut(m, ui));
        return candidate;
    }

    // A name's side moved out along that side past a badge's keep-out, where it reaches into it.
    private static SKRect MovedOut(SKRect candidate, int side, SKRect keep)
    {
        if (!candidate.IntersectsWith(keep))
            return candidate;
        return side switch
        {
            0 => SKRect.Create(keep.Right, candidate.Top, candidate.Width, candidate.Height),
            1 => SKRect.Create(keep.Left - candidate.Width, candidate.Top, candidate.Width, candidate.Height),
            2 => SKRect.Create(candidate.Left, keep.Top - candidate.Height, candidate.Width, candidate.Height),
            _ => SKRect.Create(candidate.Left, keep.Bottom, candidate.Width, candidate.Height),
        };
    }

    // ---- the switch badge (owner, 2026-10-17, from a panel of three: "A", a badge on the extract and the link on pointing) ----

    /// <summary>Whether a marker carries the switch badge: an extract a switch gates (<see cref="MapMarker.SwitchSteps"/>).</summary>
    internal static bool ShowsSwitchBadge(ShownMarker m) =>
        m.Marker.SwitchSteps is not null && m.Marker.Kind is MarkerKind.ExtractPmc or MarkerKind.ExtractScav or MarkerKind.ExtractShared;

    /// <summary>The switch badge's disc and rim (DIP): the floor arrow's, ⌀ 14 with a 1.5 rim (the roll call's rows draw it too).</summary>
    public const float SwitchBadgeDisc = FloorDisc, SwitchBadgeRim = FloorRim;

    /// <summary>The power symbol's size in the switch badge (DIP, the font's size: about 8 DIP of ink in the disc's 12.5).</summary>
    public const float SwitchGlyph = 9.5f;

    // The switch badge at the symbol's lower left (the floor arrow is at its upper right, a count at its lower right), as
    // far out as the floor arrow stands, mirrored.
    internal static SKPoint SwitchBadgeCenter(ShownMarker m, float ui)
    {
        var d = (m.R < 8 * ui ? m.R + 3 * ui : m.R * 0.8f) + 1 * ui;
        return new SKPoint(m.At.X - d, m.At.Y + d);
    }

    // The disc's square, with half a DIP of its rim.
    internal static SKRect SwitchBadgeBox(ShownMarker m, float ui) => Square(SwitchBadgeCenter(m, ui), (SwitchBadgeDisc + 0.5f) * ui);

    // Where no name goes for a switch badge: its box and FloorBadgeClear around it, as for the floor arrow.
    internal static SKRect SwitchBadgeKeepOut(ShownMarker m, float ui) =>
        SKRect.Inflate(SwitchBadgeBox(m, ui), FloorBadgeClear * ui, FloorBadgeClear * ui);

    /// <summary>
    /// The switch badge: a dark disc, as large as the floor arrow's and with its rim, in the extract's colour, holding the
    /// power symbol the switches have on the map (<see cref="Glyphs.Switch"/>) in that colour: leaving there takes a switch
    /// first. At rest, at every zoom; struck out with its extract where the list leaves it out.
    /// </summary>
    internal static void DrawSwitchBadge(SKCanvas canvas, ShownMarker m, float ui)
    {
        var c = SwitchBadgeCenter(m, ui);
        var color = m.NotListed ? m.Color.WithAlpha(ClosedAlpha) : m.Color;
        using var plate = new SKPaint { Color = Background, IsAntialias = true };
        using var rim = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = SwitchBadgeRim * ui };
        canvas.DrawCircle(c, SwitchBadgeDisc * ui, plate);
        canvas.DrawCircle(c, SwitchBadgeDisc * ui, rim);
        Glyphs.Draw(canvas, Glyphs.Switch, c, SwitchGlyph * ui, color);
    }

    // The other-floor arrow: a dark disc with a bold caret pointing up or down; two or more floors away, a plate with
    // the caret and the number of floors. In the marker's own colour, like its count and OPT badges: a marker's badges
    // are the marker's (the review of 2026-10-04: it was the player's sand, which says "you"). The caret is a stroked
    // chevron, about 9 × 5 DIP outside (owner, 2026-10-16, from a beta tester: hard to read on small second screens;
    // until then a filled triangle of about 6 × 5 DIP, which at 100 % ran into a blob where up and down differed only
    // in which edge was flat: an open V keeps its shape at that size).
    internal static void DrawFloorArrow(SKCanvas canvas, ShownMarker m, float ui)
    {
        var c = FloorBadgeCenter(m, ui);
        var offset = m.Floor;
        using var plate = new SKPaint { Color = Background, IsAntialias = true };
        using var rim = new SKPaint { Color = m.Color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = FloorRim * ui };
        var count = Math.Abs(offset);
        if (count > 1)
        {
            var box = FloorBadgeBox(m, ui);
            canvas.DrawRect(box, plate);
            canvas.DrawRect(box, rim);
            c = new SKPoint(box.Left + FloorPlateCaretIn * ui, box.MidY);
            using var fill = new SKPaint { Color = m.Color, IsAntialias = true };
            using var font = new SKFont(TypefaceBold, FloorDigit * ui);
            canvas.DrawText(count.ToString(System.Globalization.CultureInfo.InvariantCulture), box.Right - FloorPlateDigitIn * ui, box.MidY + font.Size * 0.36f,
                SKTextAlign.Center, font, fill);
        }
        else
        {
            canvas.DrawCircle(c, FloorDisc * ui, plate);
            canvas.DrawCircle(c, FloorDisc * ui, rim);
        }
        // The point up (above) or down (below), the arms' ends the other way.
        var (w, h) = (FloorCaretHalfWidth * ui, (offset > 0 ? 1 : -1) * FloorCaretHalfHeight * ui);
        using var stroke = new SKPaint
        {
            Color = m.Color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = FloorCaretStroke * ui,
            StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round,
        };
        using var caret = new SKPathBuilder();
        caret.MoveTo(c.X - w, c.Y + h);
        caret.LineTo(c.X, c.Y - h);
        caret.LineTo(c.X + w, c.Y + h);
        using var path = caret.Detach();
        canvas.DrawPath(path, stroke);
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
        if (best is not null)
            return best;
        // A marker's tab (MapJoin) is the marker.
        if (LayoutOf(camera, scene, ui).Markers.FirstOrDefault(m => HasTab(m) && SKRect.Inflate(TabBox(m, ui), 2 * ui, 2 * ui).Contains(screen)) is { } tabbed)
            return tabbed.Marker;
        // The dot at the end of a leader is its symbol's place: pointing at it points at the symbol.
        return LayoutOf(camera, scene, ui).Markers
            .Where(m => m.Leader is { } place && SKPoint.Distance(place, screen) <= 6 * ui)
            .MinBy(m => SKPoint.Distance(m.Leader!.Value, screen))?.Marker;
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
        var spacing = Math.Round(SheetSpacing);
        var caption = scene.FloorStack.Count == 0 ? MapTexts.MapSheetCaptionNoFloors(spacing: spacing) : MapTexts.MapSheetCaption(spacing: spacing);
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
    public static string? HazardLabel(MapZone zone) => zone.Group == MapContentBuilder.SniperZoneGroup ? MapTexts.MapSniperZone : null;

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

    // A dashed line from the player to the nearest place of the picked quests, or from a gated extract pointed at to its
    // first switch (ExitGuideOf).
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

    /// <summary>The guide from the player to the nearest place of the picked quests (or from a gated extract pointed at to its
    /// first switch, <see cref="ExitGuideOf"/>), and its distance on a plate.</summary>
    /// <param name="Plate">"69 m"; null when the line is too short to carry it. The distance alone: the position's age stood beside it until 2026-10-04 ("69 m · 4 MIN") and read as the minutes it takes to get there.</param>
    /// <param name="Color">The colour of the pick it leads to (of the extract it leads from).</param>
    public sealed record GuideLine(SKPoint From, SKPoint To, double Metres, string? Plate, SKRect PlateBox, SKColor Color);

    /// <param name="symbols">The boxes of the symbols placed in this frame: the plate stands on none of them.</param>
    /// <param name="drawnAt">Where this frame's markers are drawn, by id: the line ends on the symbol, also when it
    /// stands off its place (<see cref="Repel"/>).</param>
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
        return GuideBetween(camera, from, to, player.Position.HorizontalDistanceTo(nearest.Position), PickColor(scene, nearest.Group), symbols, ui);
    }

    /// <summary>
    /// The guide's second use (owner, 2026-10-17, from a panel of three: "A"): while a gated extract is pointed at (its
    /// symbol, its row in the Raid rail; <see cref="MapMarker.SwitchSteps"/>), the same dashed line from it to the first
    /// step of what leaving there takes, in the extract's colour, with the horizontal distance between the two on the
    /// plate. Pointing at that first switch draws it too, since it lights its extract. Null otherwise.
    /// </summary>
    /// <param name="symbols">The boxes taken in this frame: the plate stands on none of them.</param>
    /// <param name="drawnAt">Where this frame's markers are drawn, by id: the line runs between the symbols as drawn.</param>
    internal static GuideLine? ExitGuideOf(Camera camera, MapScene scene, float ui, IReadOnlyList<SKRect> symbols, IReadOnlyDictionary<string, SKPoint> drawnAt)
    {
        if (scene.ShownFocus.Count == 0)
            return null;
        foreach (var exit in scene.Markers)
        {
            if (exit.SwitchSteps is not [var first, ..] || !scene.ShownFocus.Contains(exit.Id) || !scene.ShownFocus.Contains(first)
                || scene.Markers.FirstOrDefault(m => m.Id == first) is not { } step)
                continue;
            var from = drawnAt.TryGetValue(exit.Id, out var a) ? a : Screen(camera, scene, exit.Position);
            var to = drawnAt.TryGetValue(step.Id, out var b) ? b : Screen(camera, scene, step.Position);
            return GuideBetween(camera, from, to, exit.Position.HorizontalDistanceTo(step.Position), ColorOf(exit.Kind), symbols, ui);
        }
        return null;
    }

    // A guide between two points with its distance on a plate where the line is long enough to carry it.
    private static GuideLine GuideBetween(Camera camera, SKPoint from, SKPoint to, double metres, SKColor color, IReadOnlyList<SKRect> symbols, float ui)
    {
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
            return new GuideLine(from, to, metres, null, SKRect.Empty, color);
        return new GuideLine(from, to, metres, text, PlateBox(a, b, width, height, symbols, ui), color);
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

    /// <summary>A distance as the cards say it: "69 m", "1.2 km", in the language in use ("1,2 km" in German).</summary>
    public static string DistanceText(double metres) => metres < 1000
        ? MapTexts.DistanceMetres(metres: metres.ToString("0", UiLanguage.Culture))
        : MapTexts.DistanceKilometres(kilometres: (metres / 1000).ToString("0.0", UiLanguage.Culture));

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
    // A place counts as in view where its symbol is drawn: one set off its place at the very edge (Repel) gets
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
    public sealed record ScaleBar(double Metres, float Pixels, SKPoint Origin, SKRect Box)
    {
        /// <summary>Whether it kept the place of the frame before because the view only moved (<see cref="Scale"/>): the
        /// window then draws one more frame, in which, with the view still, it settles.</summary>
        public bool Held { get; init; }
    }

    /// <summary>Where the scale bar stood in the last layout: how far above its own place, for which view.</summary>
    internal readonly record struct ScaleBarPlacement(RepelView View, SKSize Viewport, float Rise);

    private static readonly double[] ScaleSteps = [1, 2, 5, 10, 25, 50, 100, 200, 500, 1000, 2000];

    /// <summary>The longest round length (1, 2, 5, 10, 25, 50, 100, 200, 500 m …) that fits in a number of pixels.</summary>
    public static double ScaleBarMetres(double pixelsPerMetre, float maxPixels) =>
        ScaleSteps.Where(s => s * pixelsPerMetre <= maxPixels).DefaultIfEmpty(ScaleSteps[0]).Max();

    /// <summary>How far above its place at the lower left the scale bar goes at most to step aside from a symbol (pixels,
    /// before scaling): four times its own height. Where that finds no free place it stays at its own.</summary>
    public const float ScaleBarRise = 96;

    // Bottom left, above the wiki link and the credit line the window lays over that corner. Its metres are measured
    // the way the bar lies, along the screen's horizontal: a render stretched along one axis (Icebreaker, 1.75×) has
    // no one scale, and the mean of both axes made the bar 27 % wrong there (the review of 2026-10-04).
    // A symbol under it moves it up by what it takes (owner, 2026-10-15: at the smallest window it was drawn over a lit
    // extract on Streets, whose name then had no room): it stands 2 px above the highest of the symbols it would cover,
    // again until it covers none, and at its own place where that takes it more than ScaleBarRise up. The labels then
    // give way to it (Layout).
    // While the view only moves (the repel's rule, RepelView.GlidesTo: a pan, or less than a tenth more or less zoom, with
    // the same position, display scale and age tag), it keeps the place of the layout before (owner, 2026-10-15: placed
    // afresh in every frame, it went up and back 30 times in the roll call's 1.2 s zoom out on Streets at the smallest
    // window, as symbols passed under it). The first layout with the view still places it afresh (LayoutOf lays a
    // frame whose bar was held out again; the window draws that frame, MapView).
    /// <param name="symbols">What it steps aside from: the symbols' boxes as drawn, collars included (pixels).</param>
    /// <param name="now">The frame's time: the player's age tag is part of the view the rule compares.</param>
    private static ScaleBar Scale(Camera camera, MapScene scene, float ui, IReadOnlyList<SKRect> symbols, DateTime now)
    {
        var at = new SKPoint(18 * ui, camera.Viewport.Height - 66 * ui);
        var from = scene.Projection.ToWorld(camera.ToMap(at));
        var to = scene.Projection.ToWorld(camera.ToMap(new SKPoint(at.X + 100, at.Y)));
        var metresIn100 = Math.Sqrt((to.X - from.X) * (to.X - from.X) + (to.Z - from.Z) * (to.Z - from.Z));
        var perMetre = metresIn100 > 0 ? 100 / metresIn100 : 1;
        var metres = ScaleBarMetres(perMetre, 120 * ui);
        var pixels = (float)(metres * perMetre);
        SKRect Box(float y) => new(at.X - 6 * ui, y - 20 * ui, at.X + pixels + 34 * ui, y + 4 * ui);
        var view = new RepelView(camera.Zoom, camera.Center, ui, scene.Player, PlayerAgeTag(camera, scene, ui, now) is { } tag ? tag.Text : "");
        if (scene.LastScaleBar is { } last && last.Viewport == camera.Viewport && last.View.GlidesTo(view))
        {
            scene.LastScaleBar = last with { View = view };
            var held = at.Y - last.Rise;
            return new ScaleBar(metres, pixels, new SKPoint(at.X, held), Box(held)) { Held = true };
        }
        var y = at.Y;
        while (symbols.Where(s => s.IntersectsWith(Box(y))).Select(s => s.Top).DefaultIfEmpty(float.NaN).Min() is var top && !float.IsNaN(top))
        {
            y = top - (2 + 4) * ui;
            if (at.Y - y > ScaleBarRise * ui)
            {
                y = at.Y;
                break;
            }
        }
        scene.LastScaleBar = new ScaleBarPlacement(view, camera.Viewport, at.Y - y);
        return new ScaleBar(metres, pixels, new SKPoint(at.X, y), Box(y));
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
            marks.Add((half.ToString("0", UiLanguage.Culture), x + w / 2));
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
    private static void DrawPulse(SKCanvas canvas, MapScene scene, SKPoint at, float r, SKColor color, float ui, DateTime now)
    {
        var phase = (now - scene.FocusSince).TotalSeconds % PulsePeriod.TotalSeconds / PulsePeriod.TotalSeconds;
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

    // What lies under every symbol (owner, 2026-10-05, from the overlap panel: "Go for F"): a pick's ring and a
    // pointed-at symbol's pulse, around symbols that keep their rest size, so neither covers a neighbour; and the
    // leaders of symbols set off their places (Repel), stepping back with their symbols while something else is
    // pointed at; under them all, a linked set's breathing halos.
    private static void DrawUnderlay(SKCanvas canvas, MapScene scene, IReadOnlyList<ShownMarker> markers, float ui, DateTime now, SetLook look,
        Func<MarkerKind, bool, StepBack>? stepBack, Dictionary<ShownMarker, SymbolTrace>? traced)
    {
        var dim = scene.ShownFocus.Count > 0 ? scene.Dim : 0f;
        if (look.Halo > 0)
        {
            foreach (var m in markers.Where(m => m.Pointed))
                DrawHalo(canvas, m.At, m.R, m.Color, look, traced is null ? null : Traced(traced, m));
        }
        foreach (var m in markers.Where(m => m.Leader is not null))
            DrawLeader(canvas, m.Leader!.Value, m.At, m.Color, ui, m.Focused || IsPick(scene, m) ? 1 : 1 - (1 - StepBackOf(scene, m, stepBack).Alpha) * dim);
        foreach (var m in markers.Where(m => m.Kept))
            DrawPickRing(canvas, m, ui);
        foreach (var m in markers.Where(m => m.Pointed && scene.Pulsing))
            DrawPulse(canvas, scene, m.At, m.R, m.Color, ui, now);
    }

    /// <summary>
    /// How a frame draws the lit places of a linked set (owner, 2026-10-17, from a panel of four: "B"; docs/DESIGN.md
    /// §4, *Motion*): while it breathes, a halo under each (<see cref="Halo"/> its weight, with the breath's
    /// <see cref="Scale"/> and <see cref="Opacity"/>), and in its static look a ring on each collar (<see cref="Ring"/>
    /// its weight); the two trade places as the breath settles, and both ease in and out with the scene's Dim. All 0
    /// when no set is shown.
    /// </summary>
    internal readonly record struct SetLook(float Halo, float Scale, float Opacity, float Ring);

    internal static SetLook SetLookAt(MapScene scene, DateTime now)
    {
        if (!scene.ShowsSet || scene.ShownFocus.Count == 0)
            return default;
        var settle = scene.SettleAt(now);
        var (scale, opacity) = scene.Breath.Frame(now);
        return new(scene.Dim * (1 - settle), scale, opacity, scene.Dim * settle);
    }

    /// <summary>The breath's halo at scale 1, in times the symbol's radius: about three times at its widest (1.08).</summary>
    public const float HaloReach = 2.8f;

    // The halo's colour in its middle, at the breath's full opacity: a glow, not a disc.
    private const float HaloPeak = 0.6f;

    // A linked set's lit place while it breathes: a soft radial glow in the symbol's own colour under it, swelling and
    // fading with the breath (LinkedBreath.At), the same at every place and in phase with the rows' glyphs.
    private static void DrawHalo(SKCanvas canvas, SKPoint at, float r, SKColor color, SetLook look, SymbolTrace? trace = null)
    {
        var radius = HaloReach * r * look.Scale;
        var alpha = HaloPeak * look.Opacity * look.Halo;
        if (trace is not null)
            trace.Halo = (radius, alpha);
        var a = (byte)Math.Round(255 * Math.Clamp(alpha, 0, 1));
        using var shader = SKShader.CreateRadialGradient(at, radius, [color.WithAlpha(a), color.WithAlpha((byte)(a * 0.55f)), color.WithAlpha(0)],
            [0f, 0.45f, 1f], SKShaderTileMode.Clamp);
        using var paint = new SKPaint { Shader = shader, IsAntialias = true };
        canvas.DrawCircle(at, radius, paint);
    }

    /// <summary>The static look's ring on a lit symbol's collar, in DIP.</summary>
    public const float CollarRingWidth = 1.5f;

    // A linked set's lit places in its static look (held, the pointer still, the animation effects off): a ring of the
    // symbol's own colour just outside its collar, which takes the halo's place without moving.
    private static void DrawCollarRings(SKCanvas canvas, IReadOnlyList<ShownMarker> markers, SetLook look, float ui, Dictionary<ShownMarker, SymbolTrace>? traced)
    {
        if (look.Ring <= 0)
            return;
        foreach (var m in markers.Where(m => m.Pointed))
        {
            // Reach is the symbol, its collar and 1 DIP more: the ring lies on the collar's outer edge.
            var radius = m.Reach - 1 * ui + CollarRingWidth / 2 * ui;
            using var ring = new SKPaint
            {
                Color = m.Color.WithAlpha((byte)Math.Round(255 * Math.Clamp(look.Ring, 0, 1))), IsAntialias = true, Style = SKPaintStyle.Stroke,
                StrokeWidth = CollarRingWidth * ui,
            };
            canvas.DrawCircle(m.At, radius, ring);
            if (traced is not null)
                Traced(traced, m).Ring = look.Ring;
        }
    }

    /// <summary>A leader from a symbol's true place to where it is drawn: a hairline in its colour on a dark band, and a
    /// dot on a dark rim at the place (the opened stack's hairline of 2026-10-05, now always drawn).</summary>
    /// <param name="strength">1 at full strength; less while it steps back with its symbol.</param>
    internal static void DrawLeader(SKCanvas canvas, SKPoint place, SKPoint at, SKColor color, float ui, float strength = 1)
    {
        using var under = new SKPaint { Color = Background.WithAlpha((byte)(170 * strength)), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 * ui, StrokeCap = SKStrokeCap.Round };
        using var line = new SKPaint { Color = color.WithAlpha((byte)(220 * strength)), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.3f * ui, StrokeCap = SKStrokeCap.Round };
        using var dot = new SKPaint { Color = color.WithAlpha((byte)(255 * strength)), IsAntialias = true };
        using var rim = new SKPaint { Color = Background.WithAlpha((byte)(200 * strength)), IsAntialias = true };
        canvas.DrawLine(place, at, under);
        canvas.DrawLine(place, at, line);
        canvas.DrawCircle(place, 3.4f * ui, rim);
        canvas.DrawCircle(place, 2.2f * ui, dot);
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

    /// <param name="now">The frame's time: a "??:??:??" extract's "?" pops in as a list just read reaches it.</param>
    /// <param name="traced">What a developer's trace records of the frame's symbols (<see cref="Render"/>), or null.</param>
    private static void DrawMarker(SKCanvas canvas, MapScene scene, ShownMarker shown, float ui, DateTime now,
        Dictionary<ShownMarker, SymbolTrace>? traced = null)
    {
        var (marker, at, r, color) = (shown.Marker, shown.At, shown.R, shown.Color);
        using var fill = new SKPaint { Color = color, IsAntialias = true };
        using var outline = new SKPaint { Color = Background, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 * ui };
        var trace = traced is null ? null : Traced(traced, shown);
        // A pick's ring and a pointed-at symbol's pulse lie under every symbol (DrawUnderlay).
        // A resting boss is drawn whole at BossRestAlpha, its floor badge too, as one layer the size of the symbol and its
        // badge.
        using var rest = new StepBackLayer(canvas, shown.Resting ? BossRestAlpha : 1, 1, 1, Square(at, 40 * ui));

        switch (marker.Kind)
        {
            case MarkerKind.Objective when HasTab(shown):
                DrawTabbed(canvas, shown, ui);
                break;
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
                DrawExtract(canvas, scene, shown, ui, now, trace);
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
            case MarkerKind.BossSpawn when shown.Resting:
                // At rest a hollow octagon on its dark collar, the whole at 70 % (owner, 2026-10-17: the red dots were
                // "too distracting when trying to parse the quest icons"; from a panel of four, "A"). Its place and shape
                // stay, so the eye still finds where the boss spawns; the solid octagon is kept for when it matters.
                using (var plate = Octagon(at, r * 1.2f))
                using (var edge = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = BossRestStroke * ui, StrokeJoin = SKStrokeJoin.Round })
                {
                    DrawCollar(canvas, plate, ui);
                    canvas.DrawPath(plate, edge);
                }
                break;
            case MarkerKind.BossSpawn:
                // Lit, a solid red octagon: a shape no other marker has, and all red (owner, 2026-10-04, from the review:
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
                if (trace is not null)
                    trace.Look = ExitLook.Transit;
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
        if (ShowsSwitchBadge(shown))
            DrawSwitchBadge(canvas, shown, ui);
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
    private static void DrawEdge(SKCanvas canvas, Camera camera, MapScene scene, float ui, DateTime now)
    {
        if (scene.Gliding || EdgeOf(camera, scene, ui) is not { } edge || scene.Player is not { } player)
            return;
        var toward = Screen(camera, scene, player.Position) - edge;
        var angle = (float)(Math.Atan2(toward.Y, toward.X) * 180 / Math.PI);
        var pinging = scene.PingingAt(now);
        var r = (pinging ? 17f : 13f) * ui;
        if (pinging)
            DrawPing(canvas, edge, (now - scene.PingSince!.Value).TotalSeconds, scene.Pulse, ui, 28);

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
        var text = MapTexts.MapNewPosition;
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

    private static void DrawPlayer(SKCanvas canvas, Camera camera, MapScene scene, float ui, DateTime now)
    {
        if (scene.Player is not { } player)
            return;
        DrawEdge(canvas, camera, scene, ui, now);
        var at = Screen(camera, scene, player.Position);
        var age = Shturmap.Core.Logs.WallClock.Elapsed(player.At, now);
        if (scene.PingingAt(now))
            DrawPing(canvas, at, (now - scene.PingSince!.Value).TotalSeconds, scene.Pulse, ui);

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
    public static string AgeText(TimeSpan age) => age.TotalMinutes < 60
        ? MapTexts.PlayerAgeMinutes(minutes: (int)age.TotalMinutes)
        : MapTexts.PlayerAgeHours(hours: (int)age.TotalHours);

    /// <summary>The tag beside the player once the position is a minute old: "1 MIN OLD", and from <see cref="PlayerStale"/>
    /// larger and framed ("7 MIN OLD"). Always with "OLD": bare minutes on a map read as a time to get somewhere
    /// (owner, 2026-10-04).</summary>
    public static (string Text, bool Stale) AgeTag(TimeSpan age) => (age.TotalMinutes < 60
        ? MapTexts.PlayerAgeMinutesOld(minutes: (int)age.TotalMinutes)
        : MapTexts.PlayerAgeHoursOld(hours: (int)age.TotalHours), age >= PlayerStale);

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
