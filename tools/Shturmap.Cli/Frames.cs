using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Shturmap.Core;
using Shturmap.Core.Logs;
using Shturmap.Core.Maps;
using Shturmap.Data.Http;
using Shturmap.Data.TarkovDev;
using Shturmap.Map;
using SkiaSharp;

namespace Shturmap.Cli;

/// <summary>
/// <c>shturmap-cli frames</c>: a map's animations as frames drawn headless at fixed times, so a QA pass and the tests see
/// what an animation does over time without a window (owner, 2026-10-15: the QA pass must see animations frame by
/// frame; docs/DESIGN.md §8, "Developer aids"). A window snapshot takes seconds; a frame here takes milliseconds, and a
/// frame at t0 + 100 ms is the same every run. Next to the PNGs: frames.json, what the renderer drew of each symbol in
/// each frame (its trace, <see cref="SymbolTrace"/>), and summary.txt, what changed from one frame to the next.
/// </summary>
internal static class Frames
{
    // t0, the moment the list is read: a fixed time on the day every example uses, so the frames are the same every run.
    private static readonly DateTime Start = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Local);

    // The view's size in pixels: 1600 x 1000, or --viewport's (the map at the smallest window, 900 x 560, is about
    // 500 x 481). Set once per run, before anything is drawn.
    private static int Width = 1600;
    private static int Height = 1000;

    // What the view's glide does (MapView.AnimateView, the tour's 1.2 s): to three times the zoom, eased in and out.
    private const double GlideZoom = 3;

    // A symbol set off its place by more than this between two frames while the view glides has jumped: the view's own
    // motion moves its place, not its offset from it, and while it moves a symbol goes toward where the repel puts it a
    // step a frame (MapRenderer.RepelStep, 2 px), a neighbour's push on top.
    private const float Jump = 3;

    // The roll call's band (owner, 2026-10-15, from the design panel: option A, "Roll call, then fly-out"), as the panel
    // drew it: in the middle of the map column, two columns of three rows 22 px apart, the turns down the first column and
    // then the second; each row's triangle at its left, where its icon lifts off. A longer list (Customs' has eight) takes
    // a third column here; the window's band is the window's.
    private const float BandRowGap = 22;
    private const float BandColumn = 170;

    public static async Task RunAsync(List<string> args)
    {
        var options = new List<string>(args);
        var scene = Take(options, "--scene") ?? "list-read";
        if (scene is not ("list-read" or "whatsnew-extracts" or "list-callout"))
            throw new ArgumentException($"Unknown scene {scene}: list-read, whatsnew-extracts or list-callout");
        var callOut = scene == "list-callout";
        // The roll call runs to about 5.4 s, and its view zooms out by more than a tenth in a frame 100 ms apart, where the
        // window's frames hold symbols steady (MapRenderer.SetApart): 30 frames a second by default.
        var every = int.Parse(Take(options, "--every") ?? (callOut ? "33" : "100"), CultureInfo.InvariantCulture);
        var count = int.Parse(Take(options, "--count") ?? (callOut ? "180" : "36"), CultureInfo.InvariantCulture);
        if (every <= 0 || count is < 1 or > 2000)
            throw new ArgumentException("--every takes milliseconds above 0, --count 1 to 2000 frames");
        if (Take(options, "--viewport") is { } viewport)
        {
            var wh = viewport.Split('x', 'X');
            if (wh.Length != 2 || !int.TryParse(wh[0], NumberStyles.None, CultureInfo.InvariantCulture, out var w)
                               || !int.TryParse(wh[1], NumberStyles.None, CultureInfo.InvariantCulture, out var h) || w is < 200 or > 8000 || h is < 200 or > 8000)
                throw new ArgumentException("--viewport takes the view's size in pixels as WxH, 200 to 8000 each (500x481: the map at the smallest window)");
            (Width, Height) = (w, h);
        }
        WorldPoint? fixAt = null;
        if (options.IndexOf("--fix") is var f and >= 0)
        {
            if (f + 3 >= options.Count)
                throw new ArgumentException("--fix takes the world position's x, y and z (as a screenshot's name gives them)");
            var xyz = options.GetRange(f + 1, 3).Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
            fixAt = new WorldPoint(xyz[0], xyz[1], xyz[2]);
            options.RemoveRange(f, 4);
        }
        double? glideMs = null;
        if (options.IndexOf("--glide") is var g and >= 0)
        {
            glideMs = 1200;
            if (g + 1 < options.Count && double.TryParse(options[g + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var ms) && ms > 0)
            {
                glideMs = ms;
                options.RemoveAt(g + 1);
            }
            options.RemoveAt(g);
        }
        // The roll call moves the view as the window will, --glide or not: its length is the glide's.
        if (callOut)
            glideMs ??= MapScene.CallOutFitLength.TotalMilliseconds;
        if (options.Count != 2)
        {
            throw new ArgumentException("Use: shturmap-cli frames <map> <out folder> [--scene list-read|whatsnew-extracts|list-callout] [--every <ms>] [--count <n>] " +
                                        "[--fix <x> <y> <z>] [--glide [ms]] [--viewport <W>x<H>]");
        }
        var (mapName, output) = (options[0], Path.GetFullPath(options[1]));

        var paths = Shturmap.Session.AppPaths.Default;
        var http = new CachedHttp(CachedHttp.CreateClient(), paths.DataCache);
        var data = await new GameDataLoader(http).LoadAsync(GameMode.Pve, "en");
        var map = data.MapByNormalizedName(mapName) ?? throw new ArgumentException("Unknown map " + mapName);
        var definition = data.DefinitionFor(map.NormalizedName) ?? throw new InvalidOperationException("No map definition for " + mapName);
        // As render draws it: the SVG artwork, else tarkov.dev's tile render, else the sheet.
        MapArtwork? artwork = null;
        if (definition.SvgPath is { } svgUrl)
        {
            var svg = await new Shturmap.Data.Maps.ArtworkCache(new CachedHttp(CachedHttp.CreateClient(), paths.ArtworkCache)).GetSvgAsync(svgUrl);
            artwork = MapArtwork.Load(svg, definition, paths.PictureCache);
        }
        using var artworkScope = artwork;
        MapTiles? tiles = null;
        if (artwork is null && definition.TilePath is not null)
        {
            var tileHttp = new CachedHttp(CachedHttp.CreateClient(), paths.MapTileCache);
            tiles = new MapTiles(definition, (tile, ct) => Shturmap.Session.ArtworkProvider.FetchTileAsync(tileHttp, definition.Key, tile, ct));
        }
        using var tilesScope = tiles;

        var (shown, about) = scene == "whatsnew-extracts" ? WhatsNewExtracts(data, map.Id, definition, artwork, tiles) : ListRead(data, map, definition, artwork, tiles, fixAt);
        if (scene == "whatsnew-extracts" && fixAt is { } given)
        {
            shown.Player = new PlayerFix(given, null, Start);
            shown.Floor = FloorResolver.LayerFor(definition, given);
            about.Add(FormattableString.Invariant($"Player: --fix ({given.X:0.#}, {given.Y:0.#}, {given.Z:0.#}), which the preview itself hasn't got"));
        }
        // One fitted view for every frame, as the map opens; with --glide the view moves from it as AnimateView moves it.
        var camera = new Camera();
        camera.Resize(new SKSize(Width, Height));
        camera.Fit(shown.Projection.WorldRect, 24, Camera.LabelRoom);
        var from = (camera.Center, camera.Zoom);
        var to = from;
        var glideAt = 0.0;
        IReadOnlyList<ExitFlight> flights = [];
        if (callOut)
        {
            // The roll call: the band's rows called nearest the player first, each icon flying from its row; the view
            // starts as a raid's often is, three times the whole map's zoom on the position, and zooms out to frame the
            // list and the player as the window moves it.
            var calling = shown.ExitsToCall();
            var calls = calling.Select((m, i) => BandRow(i, calling.Count) is var row ? new ExitCall(m.Id, row.X, row.Y, i) : default).ToList();
            flights = shown.CallOutExits(calls, Start);
            var target = shown.Player is { } p ? shown.Projection.ToMap(p.Position) : Middle(shown);
            from = (target, camera.Zoom * GlideZoom);
            camera.Restore(from.Center, from.Zoom);
            var fit = camera.FittingAll(calling.Select(m => shown.Projection.ToMap(m.Position)).ToList(), PlayerAt(shown), Camera.LabelRoom, Camera.CallOutPadding);
            to = fit ?? from;
            glideAt = MapScene.CallOutFitAt.TotalMilliseconds;
            about.Add(FormattableString.Invariant($"Band: {calls.Count} rows, {BandRowGap} px apart in columns of three {BandColumn} px apart, in the middle of the view; each icon lifts off its row's triangle (the calls' From)"));
            about.Add(FormattableString.Invariant($"View: from {GlideZoom}x the whole map's zoom on the position (centre {from.Center.X:0.#}, {from.Center.Y:0.#}; zoom {from.Zoom:0.####}), at +{glideAt:0} ms it zooms out over {glideMs} ms to Camera.FittingAll with {Camera.CallOutPadding} px padding and the label room (centre {to.Center.X:0.#}, {to.Center.Y:0.#}; zoom {to.Zoom:0.####}), eased in and out as MapView.AnimateView"));
        }
        else
        {
            shown.RingExits(Start);
            if (glideMs is not null)
            {
                var target = shown.Player is { } p ? shown.Projection.ToMap(p.Position) : Middle(shown);
                to = (target, from.Zoom * GlideZoom);
                about.Add(FormattableString.Invariant($"View: glides from the whole map to {GlideZoom}x on {(shown.Player is null ? "the middle of the extracts on the list" : "the position")} over {glideMs} ms, eased in and out as MapView.AnimateView"));
            }
        }

        Directory.CreateDirectory(output);
        var frames = new List<FrameRecord>();
        for (var k = 0; k < count; k++)
        {
            var ms = (double)k * every;
            var time = Start + TimeSpan.FromMilliseconds(ms);
            if (glideMs is { } length)
            {
                var (center, zoom) = Glide(from, to, (ms - glideAt) / length);
                camera.Restore(center, zoom);
            }
            if (tiles is not null && definition.TilePath is { } basePath)
            {
                var a = camera.ToMap(new SKPoint(0, 0));
                var b = camera.ToMap(new SKPoint(Width, Height));
                var view = new MapRect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
                await tiles.LoadAsync(basePath, view, shown.Projection.WorldRect, camera.Zoom);
            }
            var trace = new List<SymbolTrace>();
            using var surface = SKSurface.Create(new SKImageInfo(Width, Height));
            MapRenderer.Render(surface.Canvas, camera, shown, 1, null, time, trace);
            var file = $"frame-{k:000}.png";
            using (var image = surface.Snapshot())
            using (var png = image.Encode(SKEncodedImageFormat.Png, 90))
                await File.WriteAllBytesAsync(Path.Combine(output, file), png.ToArray());
            frames.Add(Record(k, ms, file, camera, shown, trace, time));
        }

        var rings = shown.ExitsRinging.OrderBy(r => r.Value.At).ThenBy(r => r.Key, StringComparer.Ordinal)
            .Select(r => new RingRecord(r.Key, NameOf(shown, r.Key), Round((r.Value.At - Start).TotalMilliseconds, 1), r.Value.Listed)).ToList();
        var flown = flights.Select(f => Flown(f, shown, frames)).ToList();
        var checks = callOut ? CallOutChecks(shown, camera, flown, frames) : [];
        var json = new FramesRecord(map.NormalizedName, scene, every, count, Width, Height,
            shown.Player is { } fix ? new PositionRecord(fix.Position.X, fix.Position.Y, fix.Position.Z) : null,
            glideMs, new ListsRecord([.. Ids(shown, shown.ExitsListed)], [.. Ids(shown, shown.ExitsUnsure)], [.. Ids(shown, shown.ExitsNotListed)]),
            rings, frames)
        {
            GlideAtMs = glideMs is null ? null : glideAt,
            Flights = callOut ? flown : null,
        };
        await File.WriteAllTextAsync(Path.Combine(output, "frames.json"), JsonSerializer.Serialize(json, Json));
        var summary = Summary(map.Name, scene, every, glideMs, about, shown, rings, frames, flown, checks);
        await File.WriteAllTextAsync(Path.Combine(output, "summary.txt"), summary);
        Console.WriteLine(summary);
        Console.WriteLine($"{count} frames, frames.json and summary.txt in {output}");
    }

    // ---- the scenes ----

    // What's New's `extracts` preview (0.4.0's "Your extracts this raid"; MainWindow.WhatsNew: PreviewSceneAsync with
    // StageExtractList and pmc): a PMC raid's map, so without the Scav extracts (MapContent.InPmcRaid, the app's rule),
    // with no quests and no position, the example list's sets, rung out as it is set.
    private static (MapScene Scene, List<string> About) WhatsNewExtracts(GameData data, string mapId, MapDefinition definition, MapArtwork? artwork, MapTiles? tiles)
    {
        var content = MapContentBuilder.Build(data, mapId, [], new HashSet<string>()).InPmcRaid();
        var scene = new MapScene(definition, artwork, tiles)
            { Markers = content.Markers, Zones = content.Zones, Containers = content.Containers, QuestKeys = content.QuestKeys };
        var (listed, unsure, notListed) = ExampleExits(data, content);
        scene.ExitsListed = listed.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        scene.ExitsUnsure = unsure.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        scene.ExitsNotListed = notListed;
        return (scene, ["Scene: What's New's extracts preview (MainWindow.StageExtractList): a PMC raid's map, Scav extracts left out; no quests, no raid, no position"]);
    }

    // MainWindow.ExampleExits, as it stands (the app's code can't be referenced here): the extracts it names, one it
    // marks "??:??:??", and the rest, which it leaves out; Scav extracts have no state. Keep the two alike.
    private static (List<MapMarker> Listed, List<MapMarker> Unsure, HashSet<string> NotListed) ExampleExits(GameData data, MapContent content)
    {
        var exits = content.Markers.Where(m => m.Kind is MarkerKind.ExtractPmc or MarkerKind.ExtractShared).ToList();
        string[] names = ["Crossroads", "Trailer Park", "Old Gas Station", "RUAF Roadblock", "Smugglers' Boat", "Dorms V-Ex"];
        var listed = exits.Where(e => names.Any(n => MapContentBuilder.EnglishName(data, e).Contains(n, StringComparison.OrdinalIgnoreCase))).ToList();
        if (listed.Count < 3)
            listed = exits.Where((_, i) => i % 2 == 0).ToList();
        var unsure = exits.Where(e => !listed.Contains(e)).Take(1).ToList();
        var notListed = exits.Where(e => !listed.Contains(e) && !unsure.Contains(e)).Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        return (listed, unsure, notListed);
    }

    // A list read in a PMC raid, as a screenshot gives it: the map as the raid shows it (render's example quests, your
    // side's extracts only: GameSession's rule, MapContent.InPmcRaid), every other one of its extracts lit, two of the
    // rest "??:??:??", the others struck out; the position from the same screenshot pings. At a PMC spawn unless --fix
    // says where.
    private static (MapScene Scene, List<string> About) ListRead(GameData data, ApiMap map, MapDefinition definition, MapArtwork? artwork, MapTiles? tiles,
        WorldPoint? fix)
    {
        var active = data.Tasks.Values.Where(t => new[] { "audit", "dandies", "secret-message", "road-closed", "ballet-lover", "glory-to-cpsu", "revision-streets-of-tarkov" }
            .Contains(t.NormalizedName)).Select(t => t.Id).ToList();
        var content = MapContentBuilder.Build(data, map.Id, active, new HashSet<string>());
        if (content.Objectives.All(o => o.Places.Count == 0))
            content = MapContentBuilder.Build(data, map.Id, data.Tasks.Keys.ToList(), new HashSet<string>());
        var markers = content.InPmcRaid().Markers;
        var scene = new MapScene(definition, artwork, tiles)
            { Markers = markers, Zones = content.Zones, Containers = content.Containers, QuestKeys = content.QuestKeys, InRaid = true };
        var exits = markers.Where(m => m.Kind is MarkerKind.ExtractPmc or MarkerKind.ExtractShared).ToList();
        var listed = exits.Where((_, i) => i % 2 == 0).ToList();
        var rest = exits.Except(listed).ToList();
        var unsure = rest.Take(rest.Count >= 4 ? 2 : 1).ToList();
        scene.ExitsListed = listed.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        scene.ExitsUnsure = unsure.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        scene.ExitsNotListed = rest.Except(unsure).Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var about = new List<string> { "Scene: an extract list read in a PMC raid (render's example quests; Scav extracts left out, as a raid shows them)" };
        var spawn = (map.Spawns ?? []).FirstOrDefault(s => s.Position is not null && s.Categories?.Contains("player") == true
                                                         && s.Sides?.Any(side => side is "pmc" or "all") == true);
        var at = fix ?? spawn?.Position?.ToWorld() ?? (exits.Count > 0
            ? new WorldPoint(exits.Average(e => e.Position.X), exits.Average(e => e.Position.Y), exits.Average(e => e.Position.Z))
            : new WorldPoint(0, 0, 0));
        scene.Player = new PlayerFix(at, null, Start);
        scene.Floor = FloorResolver.LayerFor(definition, at);
        scene.PingSince = Start;
        var where = fix is not null ? "--fix" : spawn is not null ? "the map's first PMC spawn" : "the middle of the extracts";
        about.Add(FormattableString.Invariant($"Player: {where} ({at.X:0.#}, {at.Y:0.#}, {at.Z:0.#}), its fix taken at t0 and pinging"));
        return (scene, about);
    }

    // Where the band's row of a turn has its triangle, in the view's pixels: the turns go down a column of three, then the
    // next; the columns sit in the middle of the view.
    private static SKPoint BandRow(int turn, int rows)
    {
        var columns = Math.Max(1, (rows + 2) / 3);
        return new SKPoint(Width / 2f - columns * BandColumn / 2 + turn / 3 * BandColumn + 10, Height / 2f + (turn % 3 - 1) * BandRowGap);
    }

    private static MapPoint? PlayerAt(MapScene scene) => scene.Player is { } p ? scene.Projection.ToMap(p.Position) : null;

    // Where the glide goes without a position: the middle of the extracts on the list.
    private static MapPoint Middle(MapScene scene)
    {
        var on = scene.Markers.Where(m => scene.ExitsListed.Contains(m.Id) || scene.ExitsUnsure.Contains(m.Id)).Select(m => scene.Projection.ToMap(m.Position)).ToList();
        return on.Count == 0 ? scene.Projection.ToMap(new WorldPoint(0, 0, 0)) : new MapPoint(on.Average(p => p.X), on.Average(p => p.Y));
    }

    // MapView.AnimateView's ease: in and out, the zoom on a log scale.
    private static (MapPoint Center, double Zoom) Glide((MapPoint Center, double Zoom) from, (MapPoint Center, double Zoom) to, double t)
    {
        t = Math.Clamp(t, 0, 1);
        var e = t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
        var zoom = Math.Exp(Math.Log(from.Zoom) + (Math.Log(to.Zoom) - Math.Log(from.Zoom)) * e);
        return (new MapPoint(from.Center.X + (to.Center.X - from.Center.X) * e, from.Center.Y + (to.Center.Y - from.Center.Y) * e), zoom);
    }

    // ---- frames.json ----

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private sealed record FramesRecord(string Map, string Scene, int EveryMs, int Count, int Width, int Height, PositionRecord? Fix, double? GlideMs,
        ListsRecord Lists, IReadOnlyList<RingRecord> Rings, IReadOnlyList<FrameRecord> Frames)
    {
        /// <summary>When the view's glide starts, from t0: at once, or the roll call's zoom out at 1.2 s.</summary>
        public double? GlideAtMs { get; init; }

        /// <summary>The roll call's flights (list-callout), in their turns' order.</summary>
        public IReadOnlyList<FlightRecord>? Flights { get; init; }
    }

    /// <summary>A flight from the band to an extract's place (MapScene.ExitFlights): its turn, when it lifts off and lands
    /// (from t0), where from (the row's triangle) and where to (its symbol as drawn in the first frame at or after the
    /// landing; null when no frame is), in pixels.</summary>
    private sealed record FlightRecord(string Id, string Name, int Turn, bool Listed, double LiftOffMs, double LandingMs, PointRecord From, PointRecord? To,
        double? ToMs);

    private sealed record PositionRecord(double X, double Y, double Z);

    private sealed record ListsRecord(IReadOnlyList<string> Listed, IReadOnlyList<string> Unsure, IReadOnlyList<string> NotListed);

    /// <param name="TurnMs">When its turn comes, from t0 (MapScene.ExitsRinging).</param>
    private sealed record RingRecord(string Id, string Name, double TurnMs, bool Listed);

    /// <param name="Ringing">Whether an extract still rings out or waits for its turn (MapScene.ExitsArrivingAt).</param>
    private sealed record FrameRecord(int Index, double Ms, string File, ViewRecord View, bool Ringing, IReadOnlyList<ExitRecord> Exits, IReadOnlyList<SymbolRecord> Others)
    {
        /// <summary>Where the scale bar's left end is drawn, in pixels: at its place at the lower left, or stepped up from a
        /// symbol under it (MapRenderer.ScaleBarRise).</summary>
        public PointRecord? ScaleBar { get; init; }
    }

    /// <param name="Zoom">Pixels per map unit.</param>
    private sealed record ViewRecord(double CenterX, double CenterY, double Zoom);

    private sealed record PointRecord(double X, double Y);

    /// <summary>An extract or a transit as drawn (<see cref="SymbolTrace"/>): its centre and radius in pixels, its centre
    /// in map units, how far it stands from its own place in pixels (set off by the repel), the leader's end at that place
    /// where it has one, and the animation values the renderer drew it with; for one the roll call flies, how far its icon
    /// is on its way (0 to 1), where it is drawn and how large, and whether its place is in view yet (out of view, only the
    /// flight is drawn, toward the place's x, y); and whether its name was drawn bold (null where it wasn't drawn).</summary>
    private sealed record ExitRecord(string Id, string Name, MarkerKind Kind, ExitLook State, double X, double Y, double R, double MapX, double MapY,
        double OffX, double OffY, PointRecord? Leader, double? Arrival, double? Glow, IReadOnlyList<double> Echoes, double? Question)
    {
        public double? Flight { get; init; }

        public PointRecord? Flying { get; init; }

        public double? FlightScale { get; init; }

        public bool InView { get; init; } = true;

        public bool? Bold { get; init; }
    }

    /// <summary>Any other symbol as drawn: where, how large, and how far off its own place.</summary>
    private sealed record SymbolRecord(string Id, string Name, MarkerKind Kind, double X, double Y, double R, double MapX, double MapY,
        double OffX, double OffY, PointRecord? Leader);

    private static FrameRecord Record(int index, double ms, string file, Camera camera, MapScene scene, List<SymbolTrace> trace, DateTime time)
    {
        var exits = new List<ExitRecord>();
        var others = new List<SymbolRecord>();
        foreach (var t in trace)
        {
            var place = camera.ToScreen(scene.Projection.ToMap(t.Shown.Marker.Position));
            var map = camera.ToMap(t.At);
            var leader = t.Shown.Leader is { } l ? new PointRecord(Round(l.X), Round(l.Y)) : null;
            if (t.Look is { } look)
            {
                exits.Add(new ExitRecord(t.Id, t.Name, t.Kind, look, Round(t.At.X), Round(t.At.Y), Round(t.R), Round(map.X, 3), Round(map.Y, 3),
                    Round(t.At.X - place.X), Round(t.At.Y - place.Y), leader, t.Arrival is { } a ? Round(a, 4) : null, t.Glow is { } g ? Round(g, 4) : null,
                    t.Echoes.Select(e => Round(e, 4)).ToList(), t.Question is { } q ? Round(q, 4) : null)
                {
                    Flight = t.Flight is { } f ? Round(f, 4) : null,
                    Flying = t.Flying is { } at ? new PointRecord(Round(at.X), Round(at.Y)) : null,
                    FlightScale = t.FlightScale is { } s ? Round(s, 4) : null,
                    InView = t.InView,
                    Bold = t.Bold,
                });
            }
            else
            {
                others.Add(new SymbolRecord(t.Id, t.Name, t.Kind, Round(t.At.X), Round(t.At.Y), Round(t.R), Round(map.X, 3), Round(map.Y, 3),
                    Round(t.At.X - place.X), Round(t.At.Y - place.Y), leader));
            }
        }
        // The frame's own layout (MapRenderer.LayoutOf keeps it), as drawn.
        var bar = MapRenderer.LayoutOf(camera, scene, 1, time).Scale.Origin;
        return new FrameRecord(index, ms, file, new ViewRecord(Round(camera.Center.X, 3), Round(camera.Center.Y, 3), Round(camera.Zoom, 6)),
            scene.ExitsArrivingAt(time), exits, others) { ScaleBar = new PointRecord(Round(bar.X), Round(bar.Y)) };
    }

    private static double Round(double v, int digits = 2) => Math.Round(v, digits, MidpointRounding.AwayFromZero);

    private static string NameOf(MapScene scene, string id) => scene.Markers.FirstOrDefault(m => m.Id == id)?.Label ?? id;

    // Ids of a set in the data's order: a set's own order is no order.
    private static IEnumerable<string> Ids(MapScene scene, IReadOnlySet<string> set) => scene.Markers.Where(m => set.Contains(m.Id)).Select(m => m.Id);

    // ---- summary.txt ----

    private static string Summary(string mapName, string sceneName, int every, double? glideMs, List<string> about, MapScene scene,
        List<RingRecord> rings, List<FrameRecord> frames, List<FlightRecord> flights, List<string> callOutChecks)
    {
        var s = new StringBuilder();
        var first = frames[0];
        var callOut = sceneName == "list-callout";
        s.AppendLine(Fmt($"{mapName}, scene {sceneName}: {frames.Count} frames {every} ms apart from t0 (the moment the list is read: {(callOut ? "CallOutExits" : "RingExits")}), {Width} x {Height} px"));
        if (!callOut)
            s.AppendLine(Fmt($"View: fitted to the map (centre {first.View.CenterX:0.#}, {first.View.CenterY:0.#}; zoom {first.View.Zoom:0.####} px per map unit)"));
        foreach (var line in about)
            s.AppendLine(line);
        if (scene.Player is null)
            s.AppendLine("Player: none. The rings go in the data's order (MapScene.RingExits: nearest first only where there is a position)");
        // The roll call starts close in: what is in view by the end is what it framed.
        var exits = callOut ? frames[^1].Exits.Where(e => e.InView).ToList() : first.Exits;
        if (callOut)
            s.AppendLine("In view at the last frame, after the zoom out:");
        void Group(string title, ExitLook look) =>
            s.AppendLine($"{title} ({exits.Count(e => e.State == look)}): {string.Join(", ", exits.Where(e => e.State == look).Select(e => e.Name))}");
        Group("Lit, on the list", ExitLook.Listed);
        Group("\"??:??:??\", on the list", ExitLook.Unsure);
        Group("Struck out, not on the list", ExitLook.NotListed);
        Group("Plain, no state", ExitLook.Plain);
        Group("Transits", ExitLook.Transit);
        s.AppendLine("Turns (MapScene.ExitsRinging): " + string.Join("; ", rings.Select(r => Fmt($"+{r.TurnMs:0} ms {r.Name} ({(r.Listed ? "lit" : "??:??:??")})"))));
        if (callOut)
        {
            s.AppendLine("Flights (MapScene.ExitFlights), from the band's row to the symbol as drawn at the first frame at or after the landing:");
            foreach (var f in flights)
            {
                var to = f.To is { } at ? Fmt($"({at.X:0.#}, {at.Y:0.#}) at +{f.ToMs:0} ms") : "(no frame after it)";
                s.AppendLine(Fmt($"  turn {f.Turn}  {f.Name} ({(f.Listed ? "lit" : "??:??:??")}): lifts off +{f.LiftOffMs:0} ms from ({f.From.X:0.#}, {f.From.Y:0.#}), lands +{f.LandingMs:0} ms on {to}"));
            }
        }
        if (frames.FirstOrDefault(f => !f.Ringing) is { } over)
            s.AppendLine(Fmt($"The rings are over from frame {over.Index:000} (+{over.Ms:0} ms)."));
        else
            s.AppendLine("The rings are still going at the last frame.");
        s.AppendLine();

        var maxMove = (Px: 0.0, Name: "", Frame: 0);
        var still = new List<string>();
        var jumps = new List<string>();
        var leaders = new Dictionary<string, int>(StringComparer.Ordinal);
        Dictionary<string, (ExitRecord? Exit, SymbolRecord? Other)>? before = null;
        var wasFlying = new Dictionary<string, ExitRecord>(StringComparer.Ordinal);
        foreach (var frame in frames)
        {
            var now = Index(frame);
            var lines = new List<string>();
            // The roll call's icons: which lift off and which land between this frame and the one before.
            var flying = frame.Exits.Where(e => e.Flight is not null).ToDictionary(e => e.Id, StringComparer.Ordinal);
            foreach (var (id, e) in flying.Where(f => !wasFlying.ContainsKey(f.Key)))
                lines.Add(Fmt($"  lift-off {e.Name} at ({e.Flying!.X:0.#}, {e.Flying.Y:0.#}), {e.Flight:0.00} of its way{(e.InView ? "" : ", its place out of view")}"));
            foreach (var (id, e) in wasFlying.Where(f => !flying.ContainsKey(f.Key)))
                lines.Add($"  lands    {e.Name}");
            wasFlying = flying;
            if (glideMs is not null && before is not null)
            {
                var previous = frames[frame.Index - 1].View;
                if (previous != frame.View)
                    lines.Add(Fmt($"  view     zoom {previous.Zoom:0.####} -> {frame.View.Zoom:0.####} (x{frame.View.Zoom / previous.Zoom:0.###}), centre moved {Math.Sqrt(Math.Pow(frame.View.CenterX - previous.CenterX, 2) + Math.Pow(frame.View.CenterY - previous.CenterY, 2)):0.#} map units"));
            }
            foreach (var (id, (exit, other)) in now)
            {
                var name = exit?.Name ?? other!.Name;
                var (x, y, r, offX, offY, leader) = exit is not null ? (exit.X, exit.Y, exit.R, exit.OffX, exit.OffY, exit.Leader) : (other!.X, other.Y, other.R, other.OffX, other.OffY, other.Leader);
                if (before is null)
                {
                    if (exit is { Arrival: >= 0 })
                        lines.Add($"  turn     {name} ({(exit.State == ExitLook.Listed ? "lit" : "??:??:??")})");
                    continue;
                }
                if (!before.TryGetValue(id, out var was))
                {
                    lines.Add(Fmt($"  appears  {name} at ({x:0.#}, {y:0.#})"));
                    continue;
                }
                var (wx, wy, wr, wOffX, wOffY, wLeader) = was.Exit is { } e ? (e.X, e.Y, e.R, e.OffX, e.OffY, e.Leader) : (was.Other!.X, was.Other.Y, was.Other.R, was.Other.OffX, was.Other.OffY, was.Other.Leader);
                if (glideMs is null)
                {
                    var moved = Math.Sqrt((x - wx) * (x - wx) + (y - wy) * (y - wy));
                    if (moved > 0.01)
                        lines.Add(Fmt($"  moved    {name} {moved:0.##} px"));
                    if (exit is not null && moved > maxMove.Px)
                        maxMove = (moved, name, frame.Index);
                }
                else
                {
                    // The view moves every place alike; what a symbol does beyond that is its offset from its place.
                    var jumped = Math.Sqrt((offX - wOffX) * (offX - wOffX) + (offY - wOffY) * (offY - wOffY));
                    if (jumped > Jump)
                    {
                        var text = Fmt($"{name} ({(exit?.Kind ?? other!.Kind)}) {jumped:0.#} px off its place");
                        lines.Add($"  jump     {text}");
                        jumps.Add(Fmt($"frame {frame.Index:000}: {text}"));
                    }
                }
                if ((leader is null) != (wLeader is null))
                {
                    lines.Add($"  leader   {name} {(leader is null ? "loses its leader" : "gets a leader")}");
                    leaders[id] = leaders.GetValueOrDefault(id) + 1;
                }
                if (Math.Abs(r - wr) > 0.001)
                    lines.Add(Fmt($"  size     {name} r {wr:0.##} -> {r:0.##}"));
                if (exit is null || was.Exit is null)
                    continue;
                if (exit.State != was.Exit.State)
                    lines.Add($"  state    {name} {was.Exit.State} -> {exit.State}");
                if (exit.Bold != was.Exit.Bold)
                    lines.Add($"  name     {name} {Face(was.Exit.Bold)} -> {Face(exit.Bold)}");
                if (exit.Arrival is >= 0 && was.Exit.Arrival is null or < 0)
                    lines.Add($"  turn     {name} ({(exit.State == ExitLook.Listed ? "lit" : "??:??:??")})");
                if (exit.Arrival is null && was.Exit.Arrival is not null)
                    lines.Add($"  over     {name}");
                var drawn = Drawn(exit) != Drawn(was.Exit);
                if (drawn && exit.State is not (ExitLook.Listed or ExitLook.Unsure))
                    still.Add(Fmt($"frame {frame.Index:000}: {name} ({exit.State}) {Drawn(was.Exit)} -> {Drawn(exit)}"));
            }
            if (before is not null)
            {
                foreach (var (id, was) in before.Where(b => !now.ContainsKey(b.Key)))
                    lines.Add($"  gone     {was.Exit?.Name ?? was.Other!.Name}");
                var animating = frame.Exits.Where(e => e.InView && before.TryGetValue(e.Id, out var w) && w.Exit is not null && Drawn(e) != Drawn(w.Exit)).ToList();
                if (animating.Count > 0)
                    lines.Add("  drawn    " + string.Join("; ", animating.Select(e => $"{e.Name} {Drawn(e)}")));
            }
            else
            {
                var waiting = frame.Exits.Where(e => e.InView && e.Arrival is < 0).Select(e => e.Name).ToList();
                if (waiting.Count > 0)
                    lines.Add($"  waiting  {string.Join(", ", waiting)} (not lit before their turn)");
                var bold = frame.Exits.Where(e => e.InView && e.Bold == true).Select(e => e.Name).ToList();
                lines.Add($"  bold     {(bold.Count == 0 ? "no extract's name" : string.Join(", ", bold))}");
            }
            if (lines.Count > 0)
            {
                s.AppendLine(Fmt($"frame {frame.Index:000}  +{frame.Ms:0} ms"));
                foreach (var line in lines)
                    s.AppendLine(line);
            }
            before = now;
        }

        s.AppendLine();
        s.AppendLine("Checks");
        if (glideMs is null)
            s.AppendLine(Fmt($"  largest move of an extract's or transit's centre between two frames: {maxMove.Px:0.##} px{(maxMove.Px > 0 ? Fmt($" ({maxMove.Name}, frame {maxMove.Frame:000})") : "")}"));
        else
            s.AppendLine(Fmt($"  symbols set off their place by more than {Jump} px more or less from one frame to the next: {(jumps.Count == 0 ? "none" : jumps.Count.ToString(CultureInfo.InvariantCulture))}"));
        var twice = leaders.Where(l => l.Value > 1).Select(l => NameOf(scene, l.Key)).ToList();
        s.AppendLine(Fmt($"  leaders that came or went: {(leaders.Count == 0 ? "none" : Fmt($"{leaders.Values.Sum()}, {(twice.Count == 0 ? "none" : string.Join(", ", twice))} more than once"))}"));
        s.AppendLine($"  struck out, plain and transit symbols whose glow, echoes or \"?\" changed: {(still.Count == 0 ? "none" : string.Join("; ", still))}");
        // The bold comes with the lighting (owner, 2026-10-15): no name bold before its extract's turn, a struck-out one's never.
        var early = frames.SelectMany(f => f.Exits.Where(e => e.InView && e.Bold == true && (e.Arrival is < 0 || e.State == ExitLook.NotListed))
            .Select(e => Fmt($"frame {f.Index:000}: {e.Name} ({e.State})"))).ToList();
        s.AppendLine($"  names bold before their extract is lit, or struck out: {(early.Count == 0 ? "none" : string.Join("; ", early))}");
        // The scale bar's place is 66 px above the view's foot; a symbol under it moves it up (owner, 2026-10-15).
        var risen = frames.Where(f => f.ScaleBar is { } b && Height - 66 - b.Y > 0.01).ToList();
        var moves = frames.Skip(1).Count(f => Math.Abs(f.ScaleBar!.Y - frames[f.Index - 1].ScaleBar!.Y) > 0.01);
        s.AppendLine(risen.Count == 0 ? "  the scale bar: at its place at the lower left in every frame"
            : Fmt($"  the scale bar: stepped up from a symbol under it in {risen.Count} of {frames.Count} frames (from frame {risen[0].Index:000}), by up to {risen.Max(f => Height - 66 - f.ScaleBar!.Y):0.#} px; it changed place from one frame to the next {moves} times"));
        var last = frames[^1];
        var differ = new List<string>();
        foreach (var e in last.Exits.Where(e => e.InView))
        {
            if (first.Exits.FirstOrDefault(f => f.Id == e.Id && f.InView) is not { } f)
                differ.Add($"{e.Name}: not in frame 000");
            else if (glideMs is null && f with { Arrival = e.Arrival, Echoes = e.Echoes, Glow = e.Glow, Question = e.Question } != e)
                differ.Add($"{e.Name} ({e.State}): its place, size or state");
            else if (Drawn(f) != Drawn(e))
                differ.Add($"{e.Name} ({e.State}): {Drawn(f)} -> {Drawn(e)}");
        }
        s.AppendLine(Fmt($"  frame 000 against frame {last.Index:000}: {(differ.Count == 0 ? "no extract or transit differs" : string.Join("; ", differ))}"));
        foreach (var line in callOutChecks)
            s.AppendLine("  " + line);
        return s.ToString();
    }

    // A flight as the frames drew it: its times from t0, its row, and where it landed (its symbol in the first frame at or
    // after the landing, laid out in view).
    private static FlightRecord Flown(ExitFlight f, MapScene scene, List<FrameRecord> frames)
    {
        var landing = (f.Landing - Start).TotalMilliseconds;
        var after = frames.FirstOrDefault(fr => fr.Ms >= landing - 1e-6 && fr.Exits.Any(e => e.Id == f.MarkerId && e.InView));
        var at = after?.Exits.First(e => e.Id == f.MarkerId && e.InView);
        return new FlightRecord(f.MarkerId, NameOf(scene, f.MarkerId), f.Turn, f.Listed, Round((f.LiftOff - Start).TotalMilliseconds, 1), Round(landing, 1),
            new PointRecord(Round(f.FromX), Round(f.FromY)), at is null ? null : new PointRecord(at.X, at.Y), after?.Ms);
    }

    // The roll call's own checks (owner, 2026-10-15): every landing is its ring's start, its symbol unlit until then and
    // never lit while its icon flies; and the view it zooms out to holds every extract it called and the player, within
    // the padding and the label room.
    private static List<string> CallOutChecks(MapScene scene, Camera camera, List<FlightRecord> flights, List<FrameRecord> frames)
    {
        var checks = new List<string>();
        var off = new List<string>();
        foreach (var f in flights)
        {
            if (!scene.ExitsRinging.TryGetValue(f.Id, out var ring) || Math.Abs((ring.At - Start).TotalMilliseconds - f.LandingMs) > 0.05)
                off.Add($"{f.Name}: its ring doesn't start at its landing");
        }
        foreach (var frame in frames)
        {
            foreach (var e in frame.Exits)
            {
                if (e.Flight is not null && (e.Arrival is not < 0 || e.Glow is > 0 || e.Question is > 0 || e.Echoes.Count > 0))
                    off.Add(Fmt($"frame {frame.Index:000}: {e.Name} lit or ringing while it flies ({Drawn(e)})"));
                if (e.Flight is null && e.Arrival is < 0 && (e.Glow is > 0 || e.Question is > 0 || e.Echoes.Count > 0))
                    off.Add(Fmt($"frame {frame.Index:000}: {e.Name} lit before it lands ({Drawn(e)})"));
            }
        }
        checks.Add(Fmt($"every landing is a ring start, nothing lit before it lands: {(off.Count == 0 ? Fmt($"yes, {flights.Count} flights") : string.Join("; ", off))}"));

        // At the last frame's view: each called extract's place and its symbol as drawn, and the player.
        var right = Width - Math.Max(Camera.CallOutPadding, Math.Min(Camera.LabelRoom, Width * Camera.LabelRoomShare));
        var room = SKRect.Create(Camera.CallOutPadding - 0.5f, Camera.CallOutPadding - 0.5f, right - Camera.CallOutPadding + 1, Height - 2 * Camera.CallOutPadding + 1);
        var outside = new List<string>();
        var last = frames[^1];
        foreach (var f in flights)
        {
            var marker = scene.Markers.First(m => m.Id == f.Id);
            var place = camera.ToScreen(scene.Projection.ToMap(marker.Position));
            if (!room.Contains(place))
                outside.Add(Fmt($"{f.Name}'s place at ({place.X:0.#}, {place.Y:0.#})"));
            if (last.Exits.FirstOrDefault(e => e.Id == f.Id && e.InView) is not { } drawn)
                outside.Add($"{f.Name} not drawn");
            else if (drawn.X < 0 || drawn.X > Width || drawn.Y < 0 || drawn.Y > Height)
                outside.Add(Fmt($"{f.Name} drawn at ({drawn.X:0.#}, {drawn.Y:0.#})"));
        }
        if (PlayerAt(scene) is { } you && camera.ToScreen(you) is var player && !room.Contains(player))
            outside.Add(Fmt($"the player at ({player.X:0.#}, {player.Y:0.#})"));
        checks.Add(Fmt($"the fit at frame {last.Index:000} (+{last.Ms:0} ms) holds every called extract and the player within {Camera.CallOutPadding} px and the label room: {(outside.Count == 0 ? "yes" : string.Join("; ", outside))}"));
        return checks;
    }

    // What the renderer drew an extract with that moves: its glow, its echoes, its "?".
    private static string Drawn(ExitRecord e)
    {
        var parts = new List<string>();
        if (e.Glow is { } glow)
            parts.Add(Fmt($"glow {glow:0.00}"));
        if (e.Echoes.Count > 0)
            parts.Add("echoes " + string.Join("/", e.Echoes.Select(p => p.ToString("0.00", CultureInfo.InvariantCulture))));
        if (e.Question is { } question)
            parts.Add(Fmt($"? {question:0.00}"));
        return parts.Count == 0 ? "-" : string.Join(", ", parts);
    }

    private static string Fmt(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    // The face an extract's name was drawn in.
    private static string Face(bool? bold) => bold switch { true => "bold", false => "regular", null => "not drawn" };

    private static Dictionary<string, (ExitRecord? Exit, SymbolRecord? Other)> Index(FrameRecord frame)
    {
        var all = new Dictionary<string, (ExitRecord?, SymbolRecord?)>(StringComparer.Ordinal);
        // An extract the roll call flies to while its place is out of view isn't laid out: only its flight is drawn.
        foreach (var e in frame.Exits.Where(e => e.InView))
            all.TryAdd(e.Id, (e, null));
        foreach (var o in frame.Others)
            all.TryAdd(o.Id, (null, o));
        return all;
    }

    // An option's value, taken out of the arguments.
    private static string? Take(List<string> options, string name)
    {
        var at = options.IndexOf(name);
        if (at < 0)
            return null;
        if (at + 1 >= options.Count)
            throw new ArgumentException($"{name} needs a value");
        var value = options[at + 1];
        options.RemoveRange(at, 2);
        return value;
    }
}
