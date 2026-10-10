using SkiaSharp;
using Shturmap.Core;
using Shturmap.Core.Maps;
using Shturmap.Core.Quests;

namespace Shturmap.Map;

/// <summary>A symbol the help panel explains.</summary>
public enum LegendSymbol
{
    Player,
    PlayerOutOfView,
    Ping,
    KeptQuest,
    KeptLock,
    Guide,
    OutOfView,
    PointedOutOfView,
    Objective,
    PossibleLocation,
    Cluster,
    Joined,
    Leader,
    Optional,
    Done,
    Completed,
    QuestZone,
    Extract,
    SharedExtract,
    ExtractListed,
    ExtractNotListed,
    Transit,
    Boss,
    Sniper,
    Scav,
    Lock,
    Switch,
    Hazard,
    OtherFloor,
    Trail,
    Replay,
    LooseItem,
    Containers,
    Sheet,
}

/// <summary>
/// The help panel's legend: every symbol on the map, drawn by <see cref="MapRenderer"/> itself so the legend can't
/// drift from the map, ordered by the four levels of docs/DESIGN.md ("Map drawing"), each row naming shape and colour.
/// </summary>
public static class MapLegend
{
    public sealed record Row(LegendSymbol Symbol, string Text);

    /// <summary>
    /// The symbols that are on a map as it is shown now, for the help panel to list first (owner, 2026-10-04, from the
    /// review: 25 rows made help 2,350 px tall, most of them for things the map on screen doesn't have). A symbol
    /// counts when the scene holds what it stands for: a lock when the map has locks, the guide line when a pick has
    /// a place here and there is a position, the sheet when there is no artwork. The rest stay one click away.
    /// </summary>
    public static IReadOnlySet<LegendSymbol> On(MapScene scene)
    {
        var on = new HashSet<LegendSymbol>();
        void If(bool there, LegendSymbol symbol)
        {
            if (there)
                on.Add(symbol);
        }

        var kinds = scene.Markers.Select(m => m.Kind).ToHashSet();
        var places = scene.Markers.Where(m => m.Objective is not null).ToList();
        var picked = places.Any(m => m.Group is { } quest && scene.Kept.Contains(quest));
        If(scene.Player is not null, LegendSymbol.Player);
        If(scene.Player is not null, LegendSymbol.PlayerOutOfView);
        // A position on this map: the next one pings here.
        If(scene.Player is not null, LegendSymbol.Ping);
        If(picked, LegendSymbol.KeptQuest);
        If(scene.Markers.Any(m => m.Kind == MarkerKind.Lock && m.Group is not null && scene.KeptKeys.Contains(m.Group)), LegendSymbol.KeptLock);
        If(picked && scene.Player is not null, LegendSymbol.Guide);
        // Chevrons point to places out of view: cyan to a pick's, so only where a pick has a place; gold to those of
        // the quest pointed at, and any quest with a place here can be pointed at.
        If(picked, LegendSymbol.OutOfView);
        If(places.Count > 0, LegendSymbol.PointedOutOfView);
        If(kinds.Contains(MarkerKind.Objective), LegendSymbol.Objective);
        If(kinds.Contains(MarkerKind.PossibleLocation), LegendSymbol.PossibleLocation);
        // Marker ids are "objective:<id>:<n>": two places of one objective can merge into a cluster.
        If(places.GroupBy(m => m.Id[..Math.Max(0, m.Id.LastIndexOf(':'))]).Any(g => g.Count() > 1), LegendSymbol.Cluster);
        // Two objectives of one quest on one spot (MapRenderer.OneSpot).
        If(places.Where(m => m.Kind == MarkerKind.Objective).GroupBy(m => m.Group).Any(g => g.Any(a => g.Any(b => b.Id != a.Id
            && MapRenderer.ObjectiveOf(a) != MapRenderer.ObjectiveOf(b) && MapRenderer.OneSpot(a.Position, b.Position)))), LegendSymbol.Joined);
        // Any two symbols that are set apart can crowd one spot at some zoom (MapRenderer.Repel).
        If(scene.Markers.Count(m => MapRenderer.Repels(m.Kind)) > 1, LegendSymbol.Leader);
        If(places.Any(m => m.Optional), LegendSymbol.Optional);
        If(kinds.Contains(MarkerKind.ObjectiveDone), LegendSymbol.Done);
        // Any quest with a place here can be completed, and its places then ring out.
        If(places.Count > 0, LegendSymbol.Completed);
        If(scene.Zones.Any(z => z.Kind is MarkerKind.Objective or MarkerKind.ObjectiveDone), LegendSymbol.QuestZone);
        If(kinds.Contains(MarkerKind.ExtractPmc) || kinds.Contains(MarkerKind.ExtractScav), LegendSymbol.Extract);
        If(kinds.Contains(MarkerKind.ExtractShared), LegendSymbol.SharedExtract);
        // Once a screenshot showed the game's own extract list this raid.
        If(scene.ExitsListed.Count > 0, LegendSymbol.ExtractListed);
        If(scene.ExitsNotListed.Count > 0 || scene.ExitsUnsure.Count > 0, LegendSymbol.ExtractNotListed);
        If(kinds.Contains(MarkerKind.Transit), LegendSymbol.Transit);
        If(kinds.Contains(MarkerKind.BossSpawn), LegendSymbol.Boss);
        If(kinds.Contains(MarkerKind.SniperSpawn), LegendSymbol.Sniper);
        If(kinds.Contains(MarkerKind.ScavSpawn), LegendSymbol.Scav);
        If(kinds.Contains(MarkerKind.Lock), LegendSymbol.Lock);
        If(kinds.Contains(MarkerKind.Switch), LegendSymbol.Switch);
        If(scene.Zones.Any(z => z.Kind == MarkerKind.Hazard), LegendSymbol.Hazard);
        If(scene.FloorStack.Count > 1, LegendSymbol.OtherFloor);
        If(scene.Trail.Count > 0, LegendSymbol.Trail);
        // While the raid replay plays on this map.
        If(scene.Replay is not null, LegendSymbol.Replay);
        If(scene.Spawns.Count > 0, LegendSymbol.LooseItem);
        If(scene.IsSheet && scene.Containers.Count > 0, LegendSymbol.Containers);
        If(scene.IsSheet, LegendSymbol.Sheet);
        return on;
    }

    /// <summary>A swatch's size in device-independent pixels.</summary>
    public const float SwatchWidth = 56, SwatchHeight = 34;

    /// <summary>
    /// Every symbol with its line, in the language in use: looked up at each read, so a read after the language
    /// changed has the new one (docs/DESIGN.md §8, "Texts").
    /// </summary>
    public static IReadOnlyList<Row> Rows =>
    [
        // By the four levels (docs/DESIGN.md "Map drawing"): first what levels 1 and 2 always draw, then what marks
        // those or shows only at times, then levels 3 and 4. Help lists the first twelve the map has (owner, 2026-10-09:
        // the extracts came after the ping and "Moved off a crowded spot", behind help's link).
        new(LegendSymbol.Player, MapTexts.LegendPlayer),
        new(LegendSymbol.KeptQuest, MapTexts.LegendKeptQuest),
        new(LegendSymbol.Guide, MapTexts.LegendGuide),
        new(LegendSymbol.OutOfView, MapTexts.LegendOutOfView),
        new(LegendSymbol.Objective, MapTexts.LegendObjective),
        new(LegendSymbol.PossibleLocation, MapTexts.LegendPossibleLocation),
        new(LegendSymbol.Cluster, MapTexts.LegendCluster),
        new(LegendSymbol.Extract, MapTexts.LegendExtract),
        new(LegendSymbol.SharedExtract, MapTexts.LegendSharedExtract),
        new(LegendSymbol.ExtractListed, MapTexts.LegendExtractListed),
        new(LegendSymbol.ExtractNotListed, MapTexts.LegendExtractNotListed),
        new(LegendSymbol.Transit, MapTexts.LegendTransit),
        new(LegendSymbol.PlayerOutOfView, MapTexts.LegendPlayerOutOfView),
        new(LegendSymbol.KeptLock, MapTexts.LegendKeptLock),
        new(LegendSymbol.Joined, MapTexts.LegendJoined),
        new(LegendSymbol.Optional, MapTexts.LegendOptional),
        new(LegendSymbol.Done, MapTexts.LegendDone),
        new(LegendSymbol.QuestZone, MapTexts.LegendQuestZone),
        new(LegendSymbol.Leader, MapTexts.LegendLeader),
        new(LegendSymbol.PointedOutOfView, MapTexts.LegendPointedOutOfView),
        new(LegendSymbol.Ping, MapTexts.LegendPing),
        new(LegendSymbol.Completed, MapTexts.LegendCompleted),
        new(LegendSymbol.Boss, MapTexts.LegendBoss),
        new(LegendSymbol.Sniper, MapTexts.LegendSniper),
        new(LegendSymbol.Scav, MapTexts.LegendScav),
        new(LegendSymbol.Lock, MapTexts.LegendLock),
        new(LegendSymbol.Switch, MapTexts.LegendSwitch),
        new(LegendSymbol.Hazard, MapTexts.LegendHazard),
        new(LegendSymbol.OtherFloor, MapTexts.LegendOtherFloor),
        new(LegendSymbol.Trail, MapTexts.LegendTrail),
        new(LegendSymbol.Replay, MapTexts.LegendReplay),
        new(LegendSymbol.LooseItem, MapTexts.LegendLooseItem),
        new(LegendSymbol.Containers, MapTexts.LegendContainers),
        new(LegendSymbol.Sheet, MapTexts.LegendSheet),
    ];

    /// <summary>Draws one swatch into a new bitmap, <paramref name="scale"/> device pixels per DIP, on a transparent ground.</summary>
    public static SKBitmap Draw(LegendSymbol symbol, float scale)
    {
        var bitmap = new SKBitmap(new SKImageInfo((int)Math.Ceiling(SwatchWidth * scale), (int)Math.Ceiling(SwatchHeight * scale), SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        MapRenderer.DrawSwatch(canvas, symbol, new SKSize(bitmap.Width, bitmap.Height), scale);
        return bitmap;
    }
}

public static partial class MapRenderer
{
    // A sheet where one world metre is one DIP, centred on the swatch: x to the right, z upward.
    private static readonly MapDefinition SwatchSheet = new()
    {
        Key = "legend",
        Transform = [1, 0, 1, 0],
        Bounds = new WorldBox(-100, -100, 100, 100),
    };

    /// <summary>Draws a legend swatch with the map's own drawing code (see <see cref="MapLegend"/>).</summary>
    internal static void DrawSwatch(SKCanvas canvas, LegendSymbol symbol, SKSize size, float ui)
    {
        var scene = new MapScene(SwatchSheet, null) { Pulse = false };
        var camera = new Camera();
        camera.Resize(size);
        camera.Restore(new MapPoint(0, 0), ui);
        var center = new SKPoint(size.Width / 2, size.Height / 2);
        WorldPoint At(double x, double z) => new(x, 0, z);
        MapMarker Quest(MarkerKind kind, double x = 0) => new($"objective:legend:{x}", kind, At(x, 0), "", "legend", ObjectiveKind.Exploration);
        void Marker(MapMarker marker, float scale = 1, Func<ShownMarker, ShownMarker>? change = null)
        {
            var shown = Show(camera, scene, marker, ui * scale);
            shown = change?.Invoke(shown) ?? shown;
            // A pick's ring lies under the symbols on the map (DrawUnderlay); here it is the one symbol.
            if (shown.Kept)
                DrawPickRing(canvas, shown, ui * scale);
            DrawMarker(canvas, scene, shown, ui * scale);
        }

        switch (symbol)
        {
            case LegendSymbol.Player:
                scene.Player = new PlayerFix(At(0, 0), null, DateTime.Now);
                DrawPlayer(canvas, camera, scene, ui * 0.8f);
                break;
            case LegendSymbol.PlayerOutOfView:
                // Far to the right: the edge badge stands in the middle of a view this small and points that way.
                scene.Player = new PlayerFix(At(1000, 0), null, DateTime.Now);
                DrawEdge(canvas, camera, scene, ui * 0.7f);
                break;
            case LegendSymbol.Ping:
            {
                // The two still rings a ping shows with animation effects off, around the player's disc.
                var u = ui * 0.4f;
                DrawPing(canvas, center, 0, animate: false, u);
                using var body = new SKPaint { Color = Player, IsAntialias = true };
                canvas.DrawCircle(center, 6.5f * u, body);
                break;
            }
            case LegendSymbol.KeptQuest:
                scene.Kept = new HashSet<string> { "legend" };
                Marker(Quest(MarkerKind.Objective), 0.85f);
                break;
            case LegendSymbol.KeptLock:
                // The door of a key a picked quest needs: the lock's group is the key's, which the quest names.
                scene.QuestKeys = new Dictionary<string, IReadOnlyList<string>> { ["legend"] = ["key:legend"] };
                scene.Kept = new HashSet<string> { "legend" };
                Marker(new MapMarker("lock", MarkerKind.Lock, At(0, 0), "", "key:legend"), 1.3f);
                break;
            case LegendSymbol.Guide:
            {
                var u = ui * 0.8f;
                using var font = new SKFont(TypefaceBold, 12 * u);
                var text = DistanceText(69);
                var width = font.MeasureText(text) + 12 * u;
                var plate = SKRect.Create(center.X - width / 2, center.Y - 9 * u, width, 18 * u);
                var guide = new GuideLine(new SKPoint(2 * ui, center.Y), new SKPoint(size.Width - 2 * ui, center.Y), 69, text, plate, Kept);
                DrawGuide(canvas, guide, u);
                DrawGuidePlate(canvas, guide, u);
                break;
            }
            case LegendSymbol.OutOfView:
                DrawChevron(canvas, new EdgeChevron(new SKPoint(center.X + 10 * ui, center.Y), 0, 2, Kept), ui);
                break;
            case LegendSymbol.PointedOutOfView:
                DrawChevron(canvas, new EdgeChevron(new SKPoint(center.X + 10 * ui, center.Y), 0, 2, Amber, Pointed: true), ui);
                break;
            case LegendSymbol.Objective:
                Marker(Quest(MarkerKind.Objective));
                break;
            case LegendSymbol.PossibleLocation:
                Marker(Quest(MarkerKind.PossibleLocation));
                break;
            case LegendSymbol.Cluster:
                Marker(Quest(MarkerKind.PossibleLocation), change: m => m with { Count = 5 });
                break;
            case LegendSymbol.Joined:
                Marker(Quest(MarkerKind.Objective, -9), 0.85f, m => m with { Joined = [m.Marker, Quest(MarkerKind.Objective, -9) with { Id = "objective:legend-2:1" }] });
                break;
            case LegendSymbol.Leader:
            {
                // A quest's place at the dot, its symbol set off to the right as on a crowded spot.
                var place = camera.ToScreen(new MapPoint(-15, 0));
                var shown = Show(camera, scene, Quest(MarkerKind.Objective, 7), ui * 0.85f);
                DrawLeader(canvas, place, shown.At, shown.Color, ui * 0.85f);
                DrawMarker(canvas, scene, shown, ui * 0.85f);
                break;
            }
            case LegendSymbol.Optional:
                Marker(Quest(MarkerKind.Objective) with { Optional = true });
                break;
            case LegendSymbol.Completed:
                // A moment into its leaving: the check popping, the first ring on its way out.
                DrawCompletedPlace(canvas, center, 0.12, ui * 0.6f);
                break;
            case LegendSymbol.Done:
                Marker(Quest(MarkerKind.ObjectiveDone));
                break;
            case LegendSymbol.QuestZone:
                DrawZone(canvas, camera, scene, new MapZone("legend", MarkerKind.Objective, [At(-20, -11), At(18, -13), At(22, 11), At(-16, 13)]));
                break;
            case LegendSymbol.Extract:
                Marker(new MapMarker("pmc", MarkerKind.ExtractPmc, At(-11, 0), ""));
                Marker(new MapMarker("scav", MarkerKind.ExtractScav, At(11, 0), ""));
                break;
            case LegendSymbol.ExtractListed:
            {
                // A little smaller than on the map, so the whole glow fits the swatch.
                var u = ui * 0.75f;
                var shown = Show(camera, scene, new MapMarker("listed", MarkerKind.ExtractPmc, At(0, 0), ""), u) with { Listed = true };
                DrawListedGlow(canvas, shown);
                DrawMarker(canvas, scene, shown, u);
                break;
            }
            case LegendSymbol.ExtractNotListed:
                Marker(new MapMarker("not-listed", MarkerKind.ExtractPmc, At(-11, 0), ""), change: m => m with { NotListed = true });
                Marker(new MapMarker("unsure", MarkerKind.ExtractPmc, At(11, 0), ""), change: m => m with { Unsure = true });
                break;
            case LegendSymbol.SharedExtract:
                Marker(new MapMarker("shared", MarkerKind.ExtractShared, At(0, 0), ""));
                break;
            case LegendSymbol.Transit:
                Marker(new MapMarker("transit", MarkerKind.Transit, At(0, 0), ""));
                break;
            case LegendSymbol.Boss:
                Marker(new MapMarker("boss", MarkerKind.BossSpawn, At(0, 0), ""));
                break;
            case LegendSymbol.Sniper:
                Marker(new MapMarker("sniper", MarkerKind.SniperSpawn, At(0, 0), ""));
                break;
            case LegendSymbol.Scav:
                Marker(new MapMarker("scav", MarkerKind.ScavSpawn, At(0, 0), ""));
                break;
            case LegendSymbol.Lock:
                Marker(new MapMarker("lock", MarkerKind.Lock, At(0, 0), ""), 1.3f);
                break;
            case LegendSymbol.Switch:
                Marker(new MapMarker("switch", MarkerKind.Switch, At(0, 0), ""), 1.3f);
                break;
            case LegendSymbol.Hazard:
                DrawZone(canvas, camera, scene, new MapZone("hazard", MarkerKind.Hazard, [At(-14, -8), At(14, -8), At(14, 8), At(-14, 8)]), ui);
                break;
            case LegendSymbol.Containers:
                scene.Containers = [At(-18, -6), At(-10, -6), At(-2, -6), At(6, -6), At(14, -6), At(-18, 6), At(-6, 6), At(14, 6), At(14, 0)];
                DrawContainers(canvas, camera, scene, ui);
                break;
            case LegendSymbol.OtherFloor:
                Marker(Quest(MarkerKind.Objective, -14), 0.85f, m => m with { Floor = 1 });
                Marker(Quest(MarkerKind.Objective, 6), 0.85f, m => m with { Floor = -2 });
                break;
            case LegendSymbol.Trail:
                scene.Trail = [At(-24, -6), At(-4, 8)];
                scene.Player = new PlayerFix(At(22, -4), null, DateTime.Now);
                DrawTrail(canvas, camera, scene, ui);
                break;
            case LegendSymbol.Replay:
            {
                // The pen from a raid's start to its end, faint and thin to bright and wide, through three positions.
                SKPoint[] points = [new(5 * ui, size.Height - 9 * ui), new(size.Width * 0.45f, 10 * ui), new(size.Width - 5 * ui, size.Height * 0.55f)];
                double[] times = [0, 0.45, 1];
                DrawReplayPen(canvas, [(points[0], points[1], times[0], times[1]), (points[1], points[2], times[1], times[2])], ui);
                DrawReplayDots(canvas, points, times, ui);
                break;
            }
            case LegendSymbol.LooseItem:
                scene.Spawns = [At(0, 0)];
                DrawSpawns(canvas, camera, scene, ui);
                break;
            case LegendSymbol.Sheet:
            {
                var box = SKRect.Create(4 * ui, 3 * ui, size.Width - 8 * ui, size.Height - 6 * ui);
                using var panel = new SKPaint { Color = SheetPanel };
                using var minor = new SKPaint { Color = SheetMinor, StrokeWidth = 1, Style = SKPaintStyle.Stroke };
                using var major = new SKPaint { Color = SheetMajor, StrokeWidth = 1, Style = SKPaintStyle.Stroke };
                using var edge = new SKPaint { Color = SheetEdge, StrokeWidth = 1, Style = SKPaintStyle.Stroke };
                canvas.DrawRect(box, panel);
                for (var i = 1; box.Left + i * 6 * ui < box.Right; i++)
                    canvas.DrawLine(box.Left + i * 6 * ui, box.Top, box.Left + i * 6 * ui, box.Bottom, i % 5 == 0 ? major : minor);
                for (var i = 1; box.Top + i * 6 * ui < box.Bottom; i++)
                    canvas.DrawLine(box.Left, box.Top + i * 6 * ui, box.Right, box.Top + i * 6 * ui, i % 5 == 0 ? major : minor);
                canvas.DrawRect(box, edge);
                break;
            }
        }
    }
}
