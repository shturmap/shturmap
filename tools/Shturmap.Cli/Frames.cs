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

    // --notch turns the wheel this many notches in, each as MapView turns one (WheelZoom: 1.197 over 0.15 s).
    private const int Notches = 3;

    // --sway: each time back and forth, a glide in over 1.2 s (the tour's) and one back out, then the notches in and as many
    // out, 0.3 s apart, the last landing 0.15 s before the next time starts.
    private const double SwayGlide = 1200;
    private const double SwayNotch = 300;
    private const double SwayRound = 2 * SwayGlide + (2 * Notches + 1) * SwayNotch;

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
        // --grid off: without the squad grid (the setting unticked); on, as the app starts, by default.
        var grid = Take(options, "--grid") is not "off";
        // --quests all: every quest active, the busiest the map gets, instead of render's example quests.
        AllQuests = Take(options, "--quests") is "all";
        // --pointer: the mouse sweeps the view, a step a frame, as the map view tells the scene (MapScene.Pointer; owner,
        // 2026-10-26: the grid's square under it lit), so a frame's drawing time with it can be set against one without.
        var sweep = options.Remove("--pointer");
        var scene = Take(options, "--scene") ?? "list-read";
        if (scene is not ("list-read" or "whatsnew-extracts" or "list-callout" or "plan"))
            throw new ArgumentException($"Unknown scene {scene}: list-read, whatsnew-extracts, list-callout or plan");
        var callOut = scene == "list-callout";
        // --sway [n]: the view goes back and forth n times (owner, 2026-10-17: two symbols on one spot "get expanded every
        // time when the user pans or zooms the camera"), each a SwayRound: a glide in and one back out, then the wheel's
        // notches in and as many out.
        int? sway = null;
        if (options.IndexOf("--sway") is var sw and >= 0)
        {
            sway = 4;
            if (sw + 1 < options.Count && int.TryParse(options[sw + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var rounds))
            {
                sway = rounds;
                options.RemoveAt(sw + 1);
            }
            options.RemoveAt(sw);
            if (sway is < 1 or > 7)
                throw new ArgumentException("--sway takes how many times the view goes back and forth, 1 to 7");
        }
        // --toward <name>: where the view glides and the wheel turns, the place of the first symbol of that name, instead
        // of the position (the plan scene has none).
        var toward = Take(options, "--toward");
        double? notchMs = null;
        if (options.IndexOf("--notch") is var n and >= 0)
        {
            notchMs = 300;
            if (n + 1 < options.Count && double.TryParse(options[n + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var apart) && apart > 0)
            {
                notchMs = apart;
                options.RemoveAt(n + 1);
            }
            options.RemoveAt(n);
        }
        // --step <factor> [ms]: a zoom step of + and − (1.5, 0.667), the zoom buttons or a double-click (2), over 0.2 s as
        // MapView animates it (WheelZoom.Step); with [ms], the same again that much later, while or after the first runs.
        double? stepFactor = null;
        double? stepAgainMs = null;
        if (options.IndexOf("--step") is var st and >= 0)
        {
            if (st + 1 >= options.Count || !double.TryParse(options[st + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var factor) || factor <= 0 || factor == 1)
                throw new ArgumentException("--step takes the zoom's factor: 1.5 or 0.667 for + and − or the zoom buttons, 2 for a double-click; then, if given, the ms until it is pressed again");
            stepFactor = factor;
            options.RemoveAt(st + 1);
            if (st + 1 < options.Count && double.TryParse(options[st + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var again) && again > 0)
            {
                stepAgainMs = again;
                options.RemoveAt(st + 1);
            }
            options.RemoveAt(st);
        }
        // The roll call of Customs' eight rows runs to about 8 s (owner, 2026-10-16: lift-offs 0.4 s apart, flights of 1.5 s;
        // the last lands at 5.9 s and its rings end 2.1 s later): 260 frames run to 8.55 s. Its view zooms out by more than a
        // tenth in a frame 100 ms apart, where the window's frames hold symbols steady (MapRenderer.SetApart): 30 frames a
        // second by default. The wheel's notches take 0.15 s each, a step 0.2 s: 60 frames a second, until the last has
        // landed.
        var zooms = notchMs is not null || stepFactor is not null || sway is not null;
        var every = int.Parse(Take(options, "--every") ?? (callOut ? "33" : zooms ? "16" : "100"), CultureInfo.InvariantCulture);
        var count = int.Parse(Take(options, "--count")
            ?? (callOut ? "260"
                : sway is { } swayRounds ? ((int)(swayRounds * SwayRound / every) + 1).ToString(CultureInfo.InvariantCulture)
                : notchMs is { } apartMs ? ((int)((Notches - 1) * apartMs / every) + 160 / every + 6).ToString(CultureInfo.InvariantCulture)
                : stepFactor is not null ? ((int)((stepAgainMs ?? 0) / every) + 210 / every + 6).ToString(CultureInfo.InvariantCulture)
                : "36"),
            CultureInfo.InvariantCulture);
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
        if (notchMs is not null && (glideMs is not null || callOut))
            throw new ArgumentException("--notch turns the wheel at the map's whole view: not with --glide or list-callout, which move the view themselves");
        if (stepFactor is not null && (glideMs is not null || callOut || notchMs is not null))
            throw new ArgumentException("--step zooms at the map's whole view: not with --glide, --notch or list-callout, which move the view themselves");
        if (sway is not null && (glideMs is not null || callOut || notchMs is not null || stepFactor is not null))
            throw new ArgumentException("--sway moves the view itself: not with --glide, --notch, --step or list-callout");
        // The roll call moves the view as the window will, --glide or not: its length is the glide's.
        if (callOut)
            glideMs ??= MapScene.CallOutFitLength.TotalMilliseconds;
        if (options.Count != 2)
        {
            throw new ArgumentException("Use: shturmap-cli frames <map> <out folder> [--scene list-read|whatsnew-extracts|list-callout|plan] [--every <ms>] [--count <n>] " +
                                        "[--fix <x> <y> <z>] [--glide [ms] | --notch [ms] | --step <factor> [ms] | --sway [n]] [--toward <name>] [--viewport <W>x<H>] " +
                                        "[--grid off] [--quests all] [--pointer]");
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

        var (shown, about) = scene switch
        {
            "whatsnew-extracts" => WhatsNewExtracts(data, map.Id, definition, artwork, tiles),
            "plan" => Plan(data, map, definition, artwork, tiles, fixAt),
            _ => ListRead(data, map, definition, artwork, tiles, fixAt),
        };
        shown.ShowGrid = grid;
        about.Add(grid ? "Grid: on (the squad grid, as the app starts)" : "Grid: off (--grid off)");
        if (AllQuests)
            about.Add("Quests: every quest active (--quests all)");
        if (sweep)
            about.Add("Pointer: sweeps the view from its top left to its bottom right, a step a frame (--pointer; MapScene.Pointer)");
        if (scene == "whatsnew-extracts" && fixAt is { } given)
        {
            shown.Player = new PlayerFix(given, null, Start);
            shown.Floor = FloorResolver.LayerFor(definition, given);
            about.Add(FormattableString.Invariant($"Player: --fix ({given.X:0.#}, {given.Y:0.#}, {given.Z:0.#}), which the preview itself hasn't got"));
        }
        // One fitted view for every frame, as the map opens (inside the grid's strips while it shows, Camera.Inset); with
        // --glide the view moves from it as AnimateView moves it.
        var camera = new Camera { Inset = MapRenderer.GridInset(shown, 1) };
        camera.Resize(new SKSize(Width, Height));
        camera.Fit(shown.Projection.WorldRect, 24, Camera.LabelRoom);
        var from = (camera.Center, camera.Zoom);
        var to = from;
        var glideAt = 0.0;
        var pointer = new SKPoint(Width / 2f, Height / 2f);
        var wheel = new WheelZoom();
        // The wheel's notches or the steps, each at its time from t0, as MapView turns them (WheelZoom).
        var turns = new List<(double Ms, double Factor, TimeSpan Over)>();
        var turned = 0;
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
            if (scene != "plan")
                shown.RingExits(Start);
            // Where the view goes: the place of the symbol --toward names, or the position, or the middle of the extracts.
            MapPoint? towardAt = null;
            if (toward is not null)
            {
                var named = shown.Markers.FirstOrDefault(m => string.Equals(m.Label, toward, StringComparison.OrdinalIgnoreCase))
                            ?? throw new ArgumentException($"--toward: no symbol named {toward} on {map.Name} in this scene");
                towardAt = shown.Projection.ToMap(named.Position);
            }
            var where = toward is not null ? toward : shown.Player is null ? "the middle of the extracts on the list" : "the position";
            if (sway is { } rounds)
            {
                var target = towardAt ?? (shown.Player is { } p ? shown.Projection.ToMap(p.Position) : Middle(shown));
                to = (target, from.Zoom * GlideZoom);
                pointer = camera.ToScreen(target);
                for (var r = 0; r < rounds; r++)
                {
                    for (var k = 0; k < Notches; k++)
                    {
                        turns.Add((r * SwayRound + 2 * SwayGlide + k * SwayNotch, WheelZoom.FactorOf(120), WheelZoom.Notch));
                        turns.Add((r * SwayRound + 2 * SwayGlide + Notches * SwayNotch + SwayNotch + k * SwayNotch, WheelZoom.FactorOf(-120), WheelZoom.Notch));
                    }
                }
                turns.Sort((a, b) => a.Ms.CompareTo(b.Ms));
                about.Add(FormattableString.Invariant($"View: goes back and forth {rounds} times, {SwayRound} ms each: glides from the whole map to {GlideZoom}x on {where} over {SwayGlide} ms and back over as long, eased in and out as MapView.AnimateView, then the wheel turned {Notches} notches in and {Notches} out at it ({pointer.X:0}, {pointer.Y:0} px), {SwayNotch} ms apart, each over {WheelZoom.Notch.TotalMilliseconds} ms (WheelZoom)"));
            }
            if (glideMs is not null)
            {
                var target = towardAt ?? (shown.Player is { } p ? shown.Projection.ToMap(p.Position) : Middle(shown));
                to = (target, from.Zoom * GlideZoom);
                about.Add(FormattableString.Invariant($"View: glides from the whole map to {GlideZoom}x on {(shown.Player is null ? "the middle of the extracts on the list" : "the position")} over {glideMs} ms, eased in and out as MapView.AnimateView"));
            }
            if (notchMs is { } apart)
            {
                pointer = camera.ToScreen(shown.Player is { } p ? shown.Projection.ToMap(p.Position) : Middle(shown));
                turns.AddRange(Enumerable.Range(0, Notches).Select(k => (k * apart, WheelZoom.FactorOf(120), WheelZoom.Notch)));
                about.Add(FormattableString.Invariant($"View: the wheel turned {Notches} notches in at {(shown.Player is null ? "the middle of the extracts on the list" : "the position")} ({pointer.X:0}, {pointer.Y:0} px), {notchMs} ms apart from t0, each {WheelZoom.FactorOf(120):0.###}x over {WheelZoom.Notch.TotalMilliseconds} ms as MapView turns them (WheelZoom), the point under the pointer in place"));
            }
            if (stepFactor is { } factor)
            {
                // About the position, as a double-click on it zooms; + and − zoom about the view's middle, which only moves
                // where the zoom centres.
                pointer = camera.ToScreen(shown.Player is { } p ? shown.Projection.ToMap(p.Position) : Middle(shown));
                turns.Add((0, factor, WheelZoom.Step));
                if (stepAgainMs is { } again)
                    turns.Add((again, factor, WheelZoom.Step));
                about.Add(FormattableString.Invariant($"View: a step of {factor:0.###}x at t0{(stepAgainMs is null ? "" : $" and again at +{stepAgainMs} ms")} about {(shown.Player is null ? "the middle of the extracts on the list" : "the position")} ({pointer.X:0}, {pointer.Y:0} px), each over {WheelZoom.Step.TotalMilliseconds} ms as MapView zooms + and −, the zoom buttons and a double-click (WheelZoom), the point in place"));
            }
        }

        Directory.CreateDirectory(output);
        var frames = new List<FrameRecord>();
        // Each frame's drawing time (MapRenderer.Render alone, on the CPU, the trace included; not the PNG's encoding).
        var drawn = new List<double>();
        // The grid's keypad (0: ticks, 1: its lines in) and how many references each frame placed.
        var keypads = new List<(int Frame, float Keypad, int References)>();
        // --pointer: the frames whose pointer showed another square or ninth than the frame before (MapRenderer.PointerMoved):
        // the window draws only those for a move of the pointer.
        var pointerFrames = 0;
        for (var k = 0; k < count; k++)
        {
            if (sweep)
            {
                var t = count > 1 ? (float)k / (count - 1) : 0.5f;
                shown.Pointer = new SKPoint(Width * (0.04f + 0.92f * t), Height * (0.04f + 0.92f * t));
                if (MapRenderer.PointerMoved(camera, shown, 1))
                    pointerFrames++;
            }
            var ms = (double)k * every;
            var time = Start + TimeSpan.FromMilliseconds(ms);
            if (glideMs is { } length)
            {
                var (center, zoom) = Glide(from, to, (ms - glideAt) / length);
                camera.Restore(center, zoom);
            }
            // A sway's glides, in and back out; its notches are turns, below.
            if (sway is not null && ms % SwayRound is var inRound and <= 2 * SwayGlide && ms < sway * SwayRound)
            {
                wheel.Stop();
                var (center, zoom) = inRound <= SwayGlide ? Glide(from, to, inRound / SwayGlide) : Glide(to, from, (inRound - SwayGlide) / SwayGlide);
                camera.Restore(center, zoom);
            }
            // Each notch or step at its own time, between frames as a hand turns or presses it; each frame draws the zoom at
            // its time.
            for (; turned < turns.Count && turns[turned].Ms <= ms; turned++)
                wheel.Turn(camera.Zoom, turns[turned].Factor, camera.MinZoom, camera.MaxZoom, Start.AddMilliseconds(turns[turned].Ms), over: turns[turned].Over);
            if (wheel.Running)
                camera.ZoomAt(pointer, wheel.At(time) / camera.Zoom);
            if (tiles is not null && definition.TilePath is { } basePath)
            {
                var a = camera.ToMap(new SKPoint(0, 0));
                var b = camera.ToMap(new SKPoint(Width, Height));
                var view = new MapRect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
                await tiles.LoadAsync(basePath, view, shown.Projection.WorldRect, camera.Zoom);
            }
            var trace = new List<SymbolTrace>();
            using var surface = SKSurface.Create(new SKImageInfo(Width, Height));
            var drawing = System.Diagnostics.Stopwatch.StartNew();
            MapRenderer.Render(surface.Canvas, camera, shown, 1, null, time, trace);
            drawn.Add(drawing.Elapsed.TotalMilliseconds);
            if (shown.GridShown is { } gridShown)
                keypads.Add((k, gridShown.Keypad, gridShown.References.Count));
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
            NotchMs = notchMs,
            StepFactor = stepFactor,
            StepAgainMs = stepAgainMs,
            Sway = sway,
            Toward = toward,
            Flights = callOut ? flown : null,
        };
        await File.WriteAllTextAsync(Path.Combine(output, "frames.json"), JsonSerializer.Serialize(json, Json));
        var summary = Summary(map.Name, scene, every, glideMs is not null || zooms, about, shown, rings, frames, flown, checks, sway, toward);
        var sorted = drawn.Order().ToList();
        var timing = FormattableString.Invariant(
            $"Drawing (MapRenderer.Render, CPU raster, {Width}x{Height}): median {sorted[sorted.Count / 2]:0.00} ms, mean {drawn.Average():0.00} ms, slowest {sorted[^1]:0.00} ms over {drawn.Count} frames (the first {drawn[0]:0.00} ms)");
        summary += timing + Environment.NewLine;
        if (sweep)
            summary += FormattableString.Invariant($"Pointer: {pointerFrames} of {count} frames showed another square or ninth than the frame before (the window draws a frame for those only)") + Environment.NewLine;
        if (keypads.Count > 0)
        {
            // Each frame where the keypad's fade changed, and the references' count.
            var changes = keypads.Where((p, i) => i == 0 || Math.Abs(p.Keypad - keypads[i - 1].Keypad) > 1e-3 || i == keypads.Count - 1)
                .Select(p => FormattableString.Invariant($"frame {p.Frame}: {p.Keypad:0.00}"));
            summary += "Grid: keypad lines (0 ticks, 1 lines in) " + string.Join(", ", changes) +
                       FormattableString.Invariant($"; references {keypads.Min(p => p.References)} to {keypads.Max(p => p.References)} a frame") + Environment.NewLine;
        }
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
            { Markers = content.Markers, Zones = content.Zones, Containers = content.Containers, QuestKeys = content.QuestKeys, QuestBosses = content.QuestBosses };
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
        var content = ExampleContent(data, map);
        var markers = content.InPmcRaid().Markers;
        var scene = new MapScene(definition, artwork, tiles)
            { Markers = markers, Zones = content.Zones, Containers = content.Containers, QuestKeys = content.QuestKeys, QuestBosses = content.QuestBosses, InRaid = true };
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

    // --quests all: every quest active instead of render's example quests.
    private static bool AllQuests;

    // The map's content with render's example quests active, or every quest where none of those has a place on it (or
    // with --quests all).
    private static MapContent ExampleContent(GameData data, ApiMap map)
    {
        if (AllQuests)
            return MapContentBuilder.Build(data, map.Id, data.Tasks.Keys.ToList(), new HashSet<string>());
        var active = data.Tasks.Values.Where(t => new[] { "audit", "dandies", "secret-message", "road-closed", "ballet-lover", "glory-to-cpsu", "revision-streets-of-tarkov" }
            .Contains(t.NormalizedName)).Select(t => t.Id).ToList();
        var content = MapContentBuilder.Build(data, map.Id, active, new HashSet<string>());
        return content.Objectives.All(o => o.Places.Count == 0) ? MapContentBuilder.Build(data, map.Id, data.Tasks.Keys.ToList(), new HashSet<string>()) : content;
    }

    // The map as Plan shows it between raids: render's example quests and every extract (a PMC's, a Scav's, shared), no
    // list and no raid; a position only with --fix.
    private static (MapScene Scene, List<string> About) Plan(GameData data, ApiMap map, MapDefinition definition, MapArtwork? artwork, MapTiles? tiles,
        WorldPoint? fix)
    {
        var content = ExampleContent(data, map);
        var scene = new MapScene(definition, artwork, tiles)
            { Markers = content.Markers, Zones = content.Zones, Containers = content.Containers, QuestKeys = content.QuestKeys, QuestBosses = content.QuestBosses };
        var about = new List<string> { "Scene: the map as Plan shows it between raids (render's example quests, every extract: a PMC's, a Scav's, shared), no list" };
        if (fix is { } at)
        {
            scene.Player = new PlayerFix(at, null, Start);
            scene.Floor = FloorResolver.LayerFor(definition, at);
            about.Add(FormattableString.Invariant($"Player: --fix ({at.X:0.#}, {at.Y:0.#}, {at.Z:0.#}), its fix taken at t0"));
        }
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

        /// <summary>--notch: how far apart the wheel's notches are, from t0.</summary>
        public double? NotchMs { get; init; }

        /// <summary>--step: the zoom of the step at t0 (1.5 or 0.667 for + and −, 2 for a double-click).</summary>
        public double? StepFactor { get; init; }

        /// <summary>--step's [ms]: when the step is pressed again, from t0.</summary>
        public double? StepAgainMs { get; init; }

        /// <summary>--sway: how many times the view went back and forth, 4.5 s each (Frames.SwayRound).</summary>
        public int? Sway { get; init; }

        /// <summary>--toward: the symbol whose place the view glided to and the wheel turned at.</summary>
        public string? Toward { get; init; }

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

        /// <summary>Whether the scale bar kept the frame before's place because the view only moved (MapRenderer.ScaleBar.Held).</summary>
        public bool ScaleBarHeld { get; init; }

        /// <summary>How the symbols set apart on leaders were placed (MapRenderer.RepelRun): afresh, going on from the frame
        /// before while the view only moved, or held.</summary>
        public MapRenderer.RepelRun? Repel { get; init; }
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

    /// <summary>Any other symbol as drawn: where, how large, and how far off its own place; for one of a quest objective's
    /// places, the objective, and how many of its places it stands for (a cluster's count, MapRenderer.ShownMarkers).</summary>
    private sealed record SymbolRecord(string Id, string Name, MarkerKind Kind, double X, double Y, double R, double MapX, double MapY,
        double OffX, double OffY, PointRecord? Leader)
    {
        public string? Objective { get; init; }

        public int Count { get; init; } = 1;
    }

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
                    Round(t.At.X - place.X), Round(t.At.Y - place.Y), leader) { Objective = MapRenderer.ObjectiveOf(t.Shown.Marker), Count = t.Shown.Count });
            }
        }
        // As the frame drew it.
        var bar = scene.ScaleBarShown!;
        return new FrameRecord(index, ms, file, new ViewRecord(Round(camera.Center.X, 3), Round(camera.Center.Y, 3), Round(camera.Zoom, 6)),
            scene.ExitsArrivingAt(time), exits, others)
        {
            ScaleBar = new PointRecord(Round(bar.Origin.X), Round(bar.Origin.Y)),
            ScaleBarHeld = bar.Held,
            Repel = scene.RepelShown,
        };
    }

    private static double Round(double v, int digits = 2) => Math.Round(v, digits, MidpointRounding.AwayFromZero);

    private static string NameOf(MapScene scene, string id) => scene.Markers.FirstOrDefault(m => m.Id == id)?.Label ?? id;

    // Ids of a set in the data's order: a set's own order is no order.
    private static IEnumerable<string> Ids(MapScene scene, IReadOnlySet<string> set) => scene.Markers.Where(m => set.Contains(m.Id)).Select(m => m.Id);

    // ---- summary.txt ----

    private static string Summary(string mapName, string sceneName, int every, bool moves, List<string> about, MapScene scene,
        List<RingRecord> rings, List<FrameRecord> frames, List<FlightRecord> flights, List<string> callOutChecks, int? sway, string? toward)
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
        // The frames with a jump, and the jumps of a marker whose cluster merged or parted in that frame: it stands for
        // other places then, and may move with them.
        var jumpFrames = new SortedSet<int>();
        var clusterFrames = new SortedSet<int>();
        var clusterJumps = 0;
        var leaders = new Dictionary<string, int>(StringComparer.Ordinal);
        Dictionary<string, (ExitRecord? Exit, SymbolRecord? Other)>? before = null;
        Dictionary<string, string>? clustersBefore = null;
        var wasFlying = new Dictionary<string, ExitRecord>(StringComparer.Ordinal);
        foreach (var frame in frames)
        {
            var now = Index(frame);
            var clusters = Clusters(frame);
            var lines = new List<string>();
            // The roll call's icons: which lift off and which land between this frame and the one before.
            var flying = frame.Exits.Where(e => e.Flight is not null).ToDictionary(e => e.Id, StringComparer.Ordinal);
            foreach (var (id, e) in flying.Where(f => !wasFlying.ContainsKey(f.Key)))
                lines.Add(Fmt($"  lift-off {e.Name} at ({e.Flying!.X:0.#}, {e.Flying.Y:0.#}), {e.Flight:0.00} of its way{(e.InView ? "" : ", its place out of view")}"));
            foreach (var (id, e) in wasFlying.Where(f => !flying.ContainsKey(f.Key)))
                lines.Add($"  lands    {e.Name}");
            wasFlying = flying;
            if (moves && before is not null)
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
                if (!moves)
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
                        var parted = other is { Objective: not null } && ClusterOf(other) is var key
                                     && clusters.GetValueOrDefault(key) != clustersBefore!.GetValueOrDefault(key);
                        var text = Fmt($"{name} ({(exit?.Kind ?? other!.Kind)}) {jumped:0.#} px off its place{(parted ? ", as its cluster merges or parts" : "")}");
                        lines.Add($"  jump     {text}");
                        jumps.Add(Fmt($"frame {frame.Index:000}: {text}"));
                        if (parted)
                        {
                            clusterJumps++;
                            clusterFrames.Add(frame.Index);
                        }
                        else
                        {
                            jumpFrames.Add(frame.Index);
                        }
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
            clustersBefore = clusters;
        }

        s.AppendLine();
        s.AppendLine("Checks");
        if (!moves)
        {
            s.AppendLine(Fmt($"  largest move of an extract's or transit's centre between two frames: {maxMove.Px:0.##} px{(maxMove.Px > 0 ? Fmt($" ({maxMove.Name}, frame {maxMove.Frame:000})") : "")}"));
        }
        else
        {
            // Where a cluster merges or parts, its marker stands for other places and may move with them (the steady rule's
            // exception); the rest the rule holds to a step a frame.
            var others = jumps.Count - clusterJumps;
            s.AppendLine(Fmt($"  symbols set off their place by more than {Jump} px more or less from one frame to the next: {(jumps.Count == 0 ? "none" : jumps.Count.ToString(CultureInfo.InvariantCulture))}")
                + (clusterJumps == 0 ? "" : Fmt($"; {clusterJumps} as their cluster merges or parts, in {clusterFrames.Count} frames, at most {InARow(clusterFrames)} in a row"))
                + (others == 0 ? "" : Fmt($"; {(clusterJumps == 0 ? "" : Fmt($"the other {others} "))}in {jumpFrames.Count} frames, at most {InARow(jumpFrames)} in a row")));
            // A frame that zooms by a tenth or more isn't a move: it lays the symbols out afresh (MapRenderer's GlideMost).
            var zoomed = frames.Skip(1).Select(f => (f.Index, By: Math.Abs(Math.Log(f.View.Zoom / frames[f.Index - 1].View.Zoom)))).ToList();
            if (zoomed.Count > 0)
            {
                var most = zoomed.MaxBy(z => z.By);
                var afresh = zoomed.Count(z => z.By >= Math.Log(1.1));
                s.AppendLine(Fmt($"  the largest zoom from one frame to the next: x{Math.Exp(most.By):0.###} (frame {most.Index:000}); frames that zoomed by a tenth or more, laid out afresh: {(afresh == 0 ? "none" : afresh.ToString(CultureInfo.InvariantCulture))}"));
            }
        }
        // While the view only moves, the repel goes on from the frame before (MapRenderer.RepelView.GlidesTo); a frame laid
        // out afresh in a move is one the rule didn't hold.
        var placedAfresh = frames.Where(f => f.Repel == MapRenderer.RepelRun.Afresh).Select(f => Fmt($"{f.Index:000}")).ToList();
        s.AppendLine(Fmt($"  symbols on leaders placed afresh in {placedAfresh.Count} frames ({string.Join(", ", placedAfresh.Take(8))}{(placedAfresh.Count > 8 ? ", ..." : "")}), ")
            + Fmt($"going on from the frame before in {frames.Count(f => f.Repel == MapRenderer.RepelRun.Continued)}, as the frame before in {frames.Count(f => f.Repel == MapRenderer.RepelRun.Held)}"));
        var twice = leaders.Where(l => l.Value > 1).Select(l => NameOf(scene, l.Key)).ToList();
        s.AppendLine(Fmt($"  leaders that came or went: {(leaders.Count == 0 ? "none" : Fmt($"{leaders.Values.Sum()}, {(twice.Count == 0 ? "none" : string.Join(", ", twice))} more than once"))}"));
        // How far off its place a symbol stood (MapRenderer.LeaderMost bounds it); with --sway, time by time back and forth
        // (owner, 2026-10-17: two symbols on one spot "get expanded every time when the user pans or zooms the camera").
        static IEnumerable<(string Name, double Px, int Frame)> Offsets(FrameRecord f) =>
            f.Exits.Select(e => (e.Name, e.OffX, e.OffY)).Concat(f.Others.Select(o => (o.Name, o.OffX, o.OffY)))
                .Select(o => (o.Name, Math.Sqrt(o.OffX * o.OffX + o.OffY * o.OffY), f.Index));
        var farthest = frames.SelectMany(Offsets).DefaultIfEmpty().MaxBy(o => o.Px);
        s.AppendLine(Fmt($"  the farthest a symbol stood off its place: {farthest.Px:0.#} px ({farthest.Name}, frame {farthest.Frame:000})"));
        if (sway is { } rounds)
        {
            string Farthest(Func<string, bool> which) => string.Join(", ", frames.GroupBy(f => Math.Min((int)(f.Ms / SwayRound), rounds - 1)).Select(g =>
            {
                var far = g.SelectMany(Offsets).Where(o => which(o.Name)).DefaultIfEmpty().MaxBy(o => o.Px);
                return Fmt($"{g.Key + 1}: {far.Px:0.#} px ({far.Name}, frame {far.Frame:000})");
            }));
            s.AppendLine("  the same, time by time back and forth: " + Farthest(_ => true));
            if (toward is not null)
                s.AppendLine($"  the farthest one named {toward} stood off its place, time by time: " + Farthest(n => string.Equals(n, toward, StringComparison.OrdinalIgnoreCase)));
        }
        s.AppendLine($"  struck out, plain and transit symbols whose glow, echoes or \"?\" changed: {(still.Count == 0 ? "none" : string.Join("; ", still))}");
        // The bold comes with the lighting (owner, 2026-10-15): no name bold before its extract's turn, a struck-out one's never.
        var early = frames.SelectMany(f => f.Exits.Where(e => e.InView && e.Bold == true && (e.Arrival is < 0 || e.State == ExitLook.NotListed))
            .Select(e => Fmt($"frame {f.Index:000}: {e.Name} ({e.State})"))).ToList();
        s.AppendLine($"  names bold before their extract is lit, or struck out: {(early.Count == 0 ? "none" : string.Join("; ", early))}");
        // The scale bar's place is 66 px above the view's foot; a symbol under it moves it up (owner, 2026-10-15).
        var risen = frames.Where(f => f.ScaleBar is { } b && Height - 66 - b.Y > 0.01).ToList();
        var barMoves = frames.Skip(1).Count(f => Math.Abs(f.ScaleBar!.Y - frames[f.Index - 1].ScaleBar!.Y) > 0.01);
        var held = frames.Count(f => f.ScaleBarHeld);
        s.AppendLine((risen.Count == 0 ? "  the scale bar: at its place at the lower left in every frame"
            : Fmt($"  the scale bar: stepped up from a symbol under it in {risen.Count} of {frames.Count} frames (from frame {risen[0].Index:000}), by up to {risen.Max(f => Height - 66 - f.ScaleBar!.Y):0.#} px"))
            + Fmt($"; it changed place from one frame to the next {barMoves} times, and held its place while the view moved in {held} frames"));
        var last = frames[^1];
        var differ = new List<string>();
        foreach (var e in last.Exits.Where(e => e.InView))
        {
            if (first.Exits.FirstOrDefault(f => f.Id == e.Id && f.InView) is not { } f)
                differ.Add($"{e.Name}: not in frame 000");
            else if (!moves && f with { Arrival = e.Arrival, Echoes = e.Echoes, Glow = e.Glow, Question = e.Question } != e)
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
        var right = Width - Camera.RoomRight(Width, Camera.CallOutPadding, Camera.LabelRoom);
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

    // A quest marker's cluster: the places of one objective and one kind (MapRenderer.ShownMarkers).
    private static string ClusterOf(SymbolRecord s) => $"{s.Objective}|{s.Kind}";

    // Each cluster's markers in a frame, as one text: which marker stands for how many places.
    private static Dictionary<string, string> Clusters(FrameRecord frame) =>
        frame.Others.Where(o => o.Objective is not null).GroupBy(ClusterOf, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => string.Join(",", g.Select(o => Fmt($"{o.Id}:{o.Count}")).Order(StringComparer.Ordinal)), StringComparer.Ordinal);

    // The most frames in a row in a set of frame indices.
    private static int InARow(SortedSet<int> frames)
    {
        var (most, run, last) = (0, 0, int.MinValue);
        foreach (var k in frames)
        {
            run = k == last + 1 ? run + 1 : 1;
            most = Math.Max(most, run);
            last = k;
        }
        return most;
    }

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
