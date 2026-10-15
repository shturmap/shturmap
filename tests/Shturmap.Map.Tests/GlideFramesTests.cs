using Shturmap.Core;
using Shturmap.Core.Maps;
using Shturmap.Core.Quests;
using SkiaSharp;
using static Shturmap.Map.Tests.TestView;

namespace Shturmap.Map.Tests;

// Symbols set apart on leaders hold steady while the view moves (owner, 2026-10-15: "weird movements" in the tour's
// 1.2 s view move and every Follow glide; docs/DESIGN.md, "Map drawing"), seen frame by frame as shturmap-cli frames
// --glide draws it: the view glides as MapView.AnimateView moves it, from far out to three times the zoom on the
// position, and a frame is drawn every 16 ms with the renderer's trace (SymbolTrace). Until then the repel laid a crowd
// out anew at every frame's zoom, and settled it one way at one zoom and another way at the next: on Customs symbols
// flipped up to 120 px from side to side and leaders came and went 61 times in 1.2 s.
public class GlideFramesTests
{
    // t0, the glide's start: the day every example uses.
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Local);

    private const double GlideMs = 1200;

    private const int EveryMs = 16;

    // 0 to 1200 ms: the glide's first frame to its last.
    private const int Count = 76;

    // A symbol's offset from its own place may change by this much from one frame to the next: a step toward where the
    // repel puts it (MapRenderer.RepelStep) and a push from a neighbour doing the same.
    private const float Steady = 3;

    private static MapMarker Place(string objective, int n, double x, double z) =>
        new($"objective:{objective}:{n}", MarkerKind.Objective, new WorldPoint(x, 0, z), "Three in a row", "quest-row", ObjectiveKind.Exploration);

    // A made-up crowd round the position: fourteen quests' places on a spiral out to 26 m, a way out and a transit on
    // one spot, a boss's spawn, and one objective's three places in a row east of it, 30 m and 40 m apart, which are one
    // marker far out and part as the view zooms in (their markers 40 px apart: at 1.0 and 1.33 times the zoom).
    private static (Camera Camera, MapScene Scene) Crowd()
    {
        var markers = new List<MapMarker>();
        for (var i = 0; i < 14; i++)
        {
            var (r, a) = (3 + 1.8 * i, i * 2.4);
            markers.Add(Quest($"q{i}", r * Math.Cos(a), r * Math.Sin(a), $"Quest {i}"));
        }
        markers.Add(new MapMarker("extract:gate", MarkerKind.ExtractPmc, new WorldPoint(6, 0, -4), "Gate"));
        markers.Add(new MapMarker("transit:lab", MarkerKind.Transit, new WorldPoint(6, 0, -4), "Transit"));
        markers.Add(new MapMarker("boss:spawn", MarkerKind.BossSpawn, new WorldPoint(-10, 0, 8), "Boss"));
        markers.AddRange([Place("row", 1, 40, 0), Place("row", 2, 70, 0), Place("row", 3, 110, 0)]);
        var (camera, scene) = Of(markers);
        scene.Player = new PlayerFix(new WorldPoint(0, 0, 0), null, T0);
        return (camera, scene);
    }

    // What a frame drew of a symbol: how far off its own place (pixels), whether on a leader, and the cluster it stands for.
    private sealed record Drawn(SKPoint Off, bool Leader, int Count, string Cluster);

    private static Dictionary<string, Drawn> Frame(Camera camera, MapScene scene, DateTime at)
    {
        var trace = new List<SymbolTrace>();
        using (var bitmap = new SKBitmap(1000, 1000))
        using (var canvas = new SKCanvas(bitmap))
            MapRenderer.Render(canvas, camera, scene, 1, null, at, trace);
        return trace.ToDictionary(t => t.Id, t => new Drawn(t.At - camera.ToScreen(scene.Projection.ToMap(t.Shown.Marker.Position)), t.Shown.Leader is not null,
            t.Shown.Count, MapRenderer.ObjectiveOf(t.Shown.Marker) ?? t.Id));
    }

    // MapView.AnimateView's ease, as shturmap-cli frames --glide has it: in and out, the zoom on a log scale.
    private static (MapPoint Center, double Zoom) Glide((MapPoint Center, double Zoom) from, (MapPoint Center, double Zoom) to, double t)
    {
        t = Math.Clamp(t, 0, 1);
        var e = t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
        var zoom = Math.Exp(Math.Log(from.Zoom) + (Math.Log(to.Zoom) - Math.Log(from.Zoom)) * e);
        return (new MapPoint(from.Center.X + (to.Center.X - from.Center.X) * e, from.Center.Y + (to.Center.Y - from.Center.Y) * e), zoom);
    }

    // The glide's frames: from 0.6 times the zoom, off to one side, to three times that on the position.
    private static List<Dictionary<string, Drawn>> Frames(Camera camera, MapScene scene)
    {
        var target = scene.Projection.ToMap(scene.Player!.Position);
        var (from, to) = ((new MapPoint(target.X + 60, target.Y + 30), 0.6), (target, 1.8));
        var frames = new List<Dictionary<string, Drawn>>();
        for (var k = 0; k < Count; k++)
        {
            var (center, zoom) = Glide(from, to, k * EveryMs / GlideMs);
            camera.Restore(center, zoom);
            frames.Add(Frame(camera, scene, T0.AddMilliseconds(k * EveryMs)));
        }
        return frames;
    }

    // The frames at which a cluster's places merged or parted: what it stands for differs from the frame before.
    private static HashSet<(int Frame, string Cluster)> ClusterChanges(List<Dictionary<string, Drawn>> frames)
    {
        static Dictionary<string, string> Members(Dictionary<string, Drawn> frame) =>
            frame.GroupBy(d => d.Value.Cluster).ToDictionary(g => g.Key, g => string.Join(",", g.Select(d => $"{d.Key}:{d.Value.Count}").Order()));
        var changes = new HashSet<(int, string)>();
        for (var k = 1; k < frames.Count; k++)
        {
            var (was, now) = (Members(frames[k - 1]), Members(frames[k]));
            foreach (var cluster in was.Keys.Union(now.Keys))
            {
                if (was.GetValueOrDefault(cluster) != now.GetValueOrDefault(cluster))
                    changes.Add((k, cluster));
            }
        }
        return changes;
    }

    [Fact]
    public void No_symbol_jumps_or_changes_side_while_the_view_glides()
    {
        var (camera, scene) = Crowd();
        var frames = Frames(camera, scene);
        var (parted, leaders) = Steadily(frames);
        // The row of one objective's places parts as the view zooms in, at a few frames, never three in a row.
        var at = parted.Select(p => p.Frame).Distinct().Order().ToList();
        Assert.InRange(at.Count, 1, 4);
        Assert.DoesNotContain(at, k => at.Contains(k + 1) && at.Contains(k + 2));

        // The crowd is one: far out many stand off on leaders, and as it opens some come back to their places.
        Assert.True(frames[0].Count(f => f.Value.Leader) >= 5, $"{frames[0].Count(f => f.Value.Leader)} leaders at the start");
        Assert.Contains(leaders, l => frames[0].TryGetValue(l.Key, out var first) && first.Leader && !frames[^1][l.Key].Leader);
    }

    // A wheel's notches as MapView animates them (owner, 2026-10-15; WheelZoom): each zooms by a fifth over 0.15 s at the
    // pointer, so its frames come under the steady rule as a glide's do. Three notches 0.3 s apart, then a quick turn of
    // three 48 ms apart, from far out off to one side, as the glide above starts: about as far in as the glide goes.
    [Fact]
    public void No_symbol_jumps_or_changes_side_while_the_wheel_zooms()
    {
        var (camera, scene) = Crowd();
        var target = scene.Projection.ToMap(scene.Player!.Position);
        camera.Restore(new MapPoint(target.X + 60, target.Y + 30), 0.6);
        var pointer = camera.ToScreen(new MapPoint(target.X + 10, target.Y - 5));
        int[] notches = [0, 300, 600, 900, 948, 996];
        var wheel = new WheelZoom();
        var frames = new List<Dictionary<string, Drawn>>();
        for (var ms = 0; ms <= 1300; ms++)
        {
            var now = T0.AddMilliseconds(ms);
            if (notches.Contains(ms))
                wheel.Turn(camera.Zoom, WheelZoom.FactorOf(120), camera.MinZoom, camera.MaxZoom, now);
            if (ms % EveryMs != 0)
                continue;
            if (wheel.Running)
                camera.ZoomAt(pointer, wheel.At(now) / camera.Zoom);
            frames.Add(Frame(camera, scene, now));
        }
        Assert.Equal(0.6 * Math.Pow(WheelZoom.FactorOf(120), 6), camera.Zoom, 6);
        var (_, leaders) = Steadily(frames);
        Assert.True(frames[0].Count(f => f.Value.Leader) >= 5, $"{frames[0].Count(f => f.Value.Leader)} leaders at the start");
        Assert.NotEmpty(leaders);
    }

    // From one frame to the next no symbol's offset from its place changes by more than a step, but where its cluster
    // merges or parts; none's leader comes or goes more than once, and none on a leader swings to the other side of its
    // place more than once. The frames at which a cluster changed, and how often each symbol's leader came or went.
    private static (HashSet<(int Frame, string Cluster)> Parted, Dictionary<string, int> Leaders) Steadily(List<Dictionary<string, Drawn>> frames)
    {
        var parted = ClusterChanges(frames);
        var leaders = new Dictionary<string, int>();
        var flips = new Dictionary<string, int>();
        for (var k = 1; k < frames.Count; k++)
        {
            foreach (var (id, now) in frames[k])
            {
                if (!frames[k - 1].TryGetValue(id, out var was))
                    continue;
                // Where its own cluster merges or parts, the marker stands for other places: it may move with them.
                if (!parted.Contains((k, now.Cluster)))
                {
                    var moved = SKPoint.Distance(now.Off, was.Off);
                    Assert.True(moved <= Steady, $"{id} moved {moved:0.0} px off its place from frame {k - 1} to {k}");
                }
                if (now.Leader != was.Leader)
                    leaders[id] = leaders.GetValueOrDefault(id) + 1;
                // On a leader in both, it stays on its side of its place: its leader never swings round by more than a
                // quarter turn from one frame to the next.
                if (now.Leader && was.Leader && now.Off.X * was.Off.X + now.Off.Y * was.Off.Y < 0)
                    flips[id] = flips.GetValueOrDefault(id) + 1;
            }
        }
        Assert.All(leaders, l => Assert.True(l.Value <= 1, $"{l.Key}'s leader came or went {l.Value} times"));
        Assert.All(flips, f => Assert.True(f.Value <= 1, $"{f.Key} changed sides {f.Value} times"));
        return (parted, leaders);
    }

    [Fact]
    public void Once_the_view_is_still_nothing_moves()
    {
        var (camera, scene) = Crowd();
        var frames = Frames(camera, scene);
        // Ten more frames at the glide's end: the clock goes on (the position pings), the view doesn't.
        for (var k = 1; k <= 10; k++)
        {
            var still = Frame(camera, scene, T0.AddMilliseconds(GlideMs + k * EveryMs));
            Assert.Equal(frames[^1].Keys.Order(), still.Keys.Order());
            foreach (var (id, d) in still)
                Assert.Equal(frames[^1][id], d);
        }
    }

    // The scale bar steps up from a symbol under it (owner, 2026-10-15), and while the view only moves it keeps its place,
    // as the symbols on leaders go on from theirs: placed afresh in every frame, it went up and back 59 times in this
    // 1.2 s glide's frames on Streets at the smallest window, as symbols streamed under it. The first frame drawn with the
    // view still places it afresh.
    [Fact]
    public void The_scale_bar_holds_its_place_while_the_view_glides_and_settles_once_it_is_still()
    {
        // Quests 40 m apart over the lower left quarter of the sheet: as the view zooms in toward the position they
        // stream out past the bar's corner.
        var grid = new List<MapMarker>();
        for (var x = -480; x <= 0; x += 40)
        {
            for (var z = -480; z <= 0; z += 40)
                grid.Add(Quest($"g{x}:{z}", x, z, ""));
        }
        (Camera Camera, MapScene Scene) Grid()
        {
            var (c, s) = Of(grid);
            s.Player = new PlayerFix(new WorldPoint(0, 0, 0), null, T0);
            return (c, s);
        }
        var (camera, scene) = Grid();
        var target = scene.Projection.ToMap(scene.Player!.Position);
        var (from, to) = ((new MapPoint(target.X + 60, target.Y + 30), 0.6), (target, 1.8));
        // Where it stands in each frame, and where a fresh layout of that frame's view puts it.
        var drawn = new List<SKPoint>();
        var fresh = new List<SKPoint>();
        for (var k = 0; k <= Count; k++)
        {
            // Frame Count is the first drawn with the view still, at the glide's end.
            var (center, zoom) = Glide(from, to, Math.Min(k * EveryMs, GlideMs) / GlideMs);
            camera.Restore(center, zoom);
            var at = T0.AddMilliseconds(k * EveryMs);
            Frame(camera, scene, at);
            drawn.Add(scene.ScaleBarShown!.Origin);
            var (alone, aloneScene) = Grid();
            alone.Restore(center, zoom);
            fresh.Add(MapRenderer.Layout(alone, aloneScene, 1, at).Scale.Origin);
        }
        static int Changes(List<SKPoint> places) => places.Skip(1).Where((p, i) => SKPoint.Distance(p, places[i]) > 0.01f).Count();

        // Placed afresh, it would go up and back as the grid passes under it.
        Assert.True(Changes(fresh) >= 4, $"{Changes(fresh)} changes of place placed afresh: the grid doesn't reach the bar");
        // It holds: from the glide's first frame to its last, where the first frame put it; it changes at most once, as the
        // view comes to rest, and stands where a fresh layout of the still view puts it.
        Assert.Equal(fresh[0], drawn[0]);
        Assert.All(drawn.Take(Count), p => Assert.Equal(drawn[0], p));
        Assert.True(Changes(drawn) <= 1, $"{Changes(drawn)} changes of place");
        Assert.Equal(fresh[^1], drawn[^1]);
        Assert.False(scene.ScaleBarShown!.Held);

        // A zoom at once by more than a tenth (a double-click, + or −) isn't a move: it is placed afresh at once.
        camera.ZoomAt(new SKPoint(500, 500), 0.5);
        var later = T0.AddMilliseconds((Count + 1) * EveryMs);
        Frame(camera, scene, later);
        var (zoomed, zoomedScene) = Grid();
        zoomed.Restore(camera.Center, camera.Zoom);
        Assert.Equal(MapRenderer.Layout(zoomed, zoomedScene, 1, later).Scale.Origin, scene.ScaleBarShown!.Origin);
        Assert.False(scene.ScaleBarShown!.Held);
    }

    [Fact]
    public void A_wheels_notch_or_a_new_position_lays_the_crowd_out_as_a_still_view_shows_it()
    {
        // After the glide, a fifth more zoom at once (a wheel's notch with Windows' animation effects off; with them on,
        // it takes 0.15 s, above) lays the crowd out as a map opened at that view does.
        var (camera, scene) = Crowd();
        Frames(camera, scene);
        camera.ZoomAt(new SKPoint(500, 500), 1.2);
        var notched = Frame(camera, scene, T0.AddMilliseconds(GlideMs));
        var (fresh, freshScene) = Crowd();
        fresh.Restore(camera.Center, camera.Zoom);
        Assert.Equal(Frame(fresh, freshScene, T0.AddMilliseconds(GlideMs)), notched);

        // And so does a new position at a still view.
        var (glided, moved) = Crowd();
        Frames(glided, moved);
        moved.Player = new PlayerFix(new WorldPoint(4, 0, 2), null, T0.AddMilliseconds(GlideMs));
        var (again, againScene) = Crowd();
        againScene.Player = moved.Player;
        again.Restore(glided.Center, glided.Zoom);
        Assert.Equal(Frame(again, againScene, T0.AddMilliseconds(GlideMs)), Frame(glided, moved, T0.AddMilliseconds(GlideMs)));
    }
}
