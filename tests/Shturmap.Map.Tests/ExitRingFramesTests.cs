using Shturmap.Core;
using SkiaSharp;
using static Shturmap.Map.Tests.TestView;

namespace Shturmap.Map.Tests;

// A list just read rings its extracts out (owner, 2026-10-14; docs/DESIGN.md, "Map drawing"), seen frame by frame as
// shturmap-cli frames draws it (owner, 2026-10-15: the QA pass must see animations frame by frame): 36 frames 100 ms
// apart, each drawn at its own time with the renderer's trace (SymbolTrace), which records what the frame drew of each
// symbol. The asserts read the trace; a few pixels show that the trace is what the picture shows.
public class ExitRingFramesTests
{
    // t0, the moment the list is read: the day every example uses.
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Local);

    private static readonly TimeSpan Every = TimeSpan.FromMilliseconds(100);

    private const int Count = 36;

    private static MapMarker Exit(string id, MarkerKind kind, double x, double z) => new(id, kind, new WorldPoint(x, 0, z), id);

    // A made-up raid: three lit, two "??:??:??", two struck out, a Scav extract the list doesn't speak of on the very
    // place of a lit one (Customs has several such pairs, which the repel sets side by side), a transit, and a quest by
    // a struck-out one. In the data's order, which isn't the order of distance from the fix at the middle.
    private static (Camera Camera, MapScene Scene) Raid(bool fix = true)
    {
        var (camera, scene) = Of(
        [
            Exit("extract:far", MarkerKind.ExtractPmc, 300, -250),
            Exit("extract:unsure-b", MarkerKind.ExtractShared, 200, 200),
            Exit("extract:mid", MarkerKind.ExtractPmc, -150, 100),
            Exit("extract:twin", MarkerKind.ExtractScav, -150, 100),
            Exit("extract:closed-b", MarkerKind.ExtractPmc, -300, -200),
            Exit("extract:near", MarkerKind.ExtractPmc, 60, 0),
            Exit("extract:unsure-a", MarkerKind.ExtractPmc, -40, -60),
            Exit("extract:closed-a", MarkerKind.ExtractPmc, 100, 20),
            new MapMarker("transit:t", MarkerKind.Transit, new WorldPoint(0, 0, 300), "Transit"),
            Quest("q", 110, 30, "Quest"),
        ]);
        if (fix)
            scene.Player = new PlayerFix(new WorldPoint(0, 0, 0), null, T0);
        scene.ExitsListed = new HashSet<string> { "extract:far", "extract:mid", "extract:near" };
        scene.ExitsUnsure = new HashSet<string> { "extract:unsure-a", "extract:unsure-b" };
        scene.ExitsNotListed = new HashSet<string> { "extract:closed-a", "extract:closed-b" };
        return (camera, scene);
    }

    // The frames from t0, each drawn at its time; `before` runs before the k-th is drawn (to read another list).
    private static List<Dictionary<string, SymbolTrace>> Frames(MapScene scene, Camera camera, Action<int, DateTime>? before = null)
    {
        scene.RingExits(T0);
        var frames = new List<Dictionary<string, SymbolTrace>>();
        using var bitmap = new SKBitmap(1000, 1000);
        for (var k = 0; k < Count; k++)
        {
            var at = T0 + Every * k;
            before?.Invoke(k, at);
            var trace = new List<SymbolTrace>();
            using (var canvas = new SKCanvas(bitmap))
                MapRenderer.Render(canvas, camera, scene, 1, null, at, trace);
            frames.Add(trace.Where(t => t.Look is not null).ToDictionary(t => t.Id));
        }
        return frames;
    }

    private static SKBitmap Picture(MapScene scene, Camera camera, DateTime at)
    {
        var bitmap = new SKBitmap(1000, 1000);
        using var canvas = new SKCanvas(bitmap);
        MapRenderer.Render(canvas, camera, scene, 1, null, at);
        return bitmap;
    }

    // When an extract's turn came, from t0, as the frames show it: the first frame it rings in, less how far into its
    // ring that frame is.
    private static double? TurnMs(List<Dictionary<string, SymbolTrace>> frames, string id) =>
        frames.Select((f, k) => f[id].Arrival is { } a && a >= 0 ? k * Every.TotalMilliseconds - a * 1000 : (double?)null).FirstOrDefault(t => t is not null);

    [Fact]
    public void Only_the_lit_and_the_unsure_ones_ring()
    {
        var (camera, scene) = Raid();
        var frames = Frames(scene, camera);
        foreach (var id in new[] { "extract:far", "extract:mid", "extract:near", "extract:unsure-a", "extract:unsure-b" })
            Assert.Contains(frames, f => f[id].Arrival is >= 0);
        // The struck-out ones, the Scav extract the list doesn't speak of and the transit never change: no glow, no echo,
        // no "?", the same size and place in every frame.
        foreach (var id in new[] { "extract:closed-a", "extract:closed-b", "extract:twin", "transit:t" })
        {
            var first = frames[0][id];
            Assert.Equal(id == "transit:t" ? ExitLook.Transit : id == "extract:twin" ? ExitLook.Plain : ExitLook.NotListed, first.Look);
            foreach (var frame in frames)
            {
                var t = frame[id];
                Assert.Null(t.Arrival);
                Assert.Null(t.Glow);
                Assert.Empty(t.Echoes);
                Assert.Null(t.Question);
                Assert.Equal(first.At, t.At);
                Assert.Equal(first.R, t.R);
                Assert.Equal(first.Look, t.Look);
            }
        }
    }

    [Fact]
    public void The_lit_ones_ring_nearest_the_fix_first_then_the_unsure_ones_a_turn_apart()
    {
        var (camera, scene) = Raid();
        var frames = Frames(scene, camera);
        // Lit: near (60 m), mid (180 m), far (390 m); then "??:??:??": unsure-a (72 m, nearer than all but one lit), unsure-b.
        string[] order = ["extract:near", "extract:mid", "extract:far", "extract:unsure-a", "extract:unsure-b"];
        for (var i = 0; i < order.Length; i++)
            Assert.Equal(i * MapRenderer.ExitTurn.TotalMilliseconds, TurnMs(frames, order[i])!.Value, 3);
        // Before its turn a lit one isn't lit yet: at 100 ms the nearest swells in, the next waits unlit.
        Assert.True(frames[1]["extract:near"].Glow > 1, $"{frames[1]["extract:near"].Glow}");
        Assert.Equal(0, frames[1]["extract:mid"].Glow);
        Assert.Equal(0, frames[4]["extract:unsure-a"].Question);
        Assert.True(frames[5]["extract:unsure-a"].Question > 0);

        // The picture says so: green glow around the nearest at 100 ms, none yet around the farthest.
        using var at100 = Picture(scene, camera, T0 + Every);
        var near = Glow(at100, frames[1]["extract:near"].At);
        var far = Glow(at100, frames[1]["extract:far"].At);
        Assert.True(near > 100 && far < 20, $"{near} glowing pixels around the nearest, {far} around the farthest");
    }

    [Fact]
    public void No_extract_moves_between_frames()
    {
        var (camera, scene) = Raid();
        var frames = Frames(scene, camera);
        for (var k = 1; k < Count; k++)
        {
            foreach (var (id, t) in frames[k])
            {
                var moved = SKPoint.Distance(t.At, frames[k - 1][id].At);
                Assert.True(moved <= 0.5f, $"{id} moved {moved} px from frame {k - 1} to {k}");
            }
        }
        // The pair on one place stands side by side, the lit one and the Scav one alike.
        Assert.NotEqual(frames[0]["extract:mid"].At, frames[0]["extract:twin"].At);
    }

    [Fact]
    public void A_later_list_while_they_ring_adds_only_what_it_lit_anew_and_the_same_list_again_changes_nothing()
    {
        string Extract(SymbolTrace t) => FormattableString.Invariant($"{t.Id} {t.At} {t.Look} {t.Arrival} {t.Glow} {string.Join("/", t.Echoes)} {t.Question}");
        void Later(MapScene scene, int k, DateTime at)
        {
            // Half a second in, a second screenshot's list names closed-a too.
            if (k == 5)
            {
                scene.ExitsListed = new HashSet<string>(scene.ExitsListed) { "extract:closed-a" };
                scene.ExitsNotListed = new HashSet<string> { "extract:closed-b" };
                scene.RingExits(at);
            }
        }
        var (camera, scene) = Raid();
        var once = Frames(scene, camera, (k, at) => Later(scene, k, at));
        var (camera2, again) = Raid();
        var twice = Frames(again, camera2, (k, at) =>
        {
            Later(again, k, at);
            // And at a second, a third screenshot with the same list.
            if (k == 10)
                again.RingExits(at);
        });

        // What rang carries on from where it was: the nearest is k/10 s into its ring in every frame.
        for (var k = 0; k < 21; k++)
            Assert.Equal(k * 0.1, once[k]["extract:near"].Arrival!.Value, 6);
        // What is lit anew rings after the last turn (unsure-b's, 640 ms), not at once; it doesn't move as it lights.
        Assert.Equal(800, TurnMs(once, "extract:closed-a")!.Value, 3);
        Assert.Equal(ExitLook.NotListed, once[4]["extract:closed-a"].Look);
        Assert.Equal(ExitLook.Listed, once[5]["extract:closed-a"].Look);
        Assert.Equal(once[4]["extract:closed-a"].At, once[5]["extract:closed-a"].At);
        // The same list again changes nothing, in any frame.
        for (var k = 0; k < Count; k++)
            Assert.Equal(once[k].Values.Select(Extract).Order(), twice[k].Values.Select(Extract).Order());
    }

    [Fact]
    public void Without_a_fix_they_ring_in_the_datas_order_the_same_every_run()
    {
        // No position (What's New's preview has none): the lit ones in the order the scene's markers come in, then the
        // "??:??:??" ones in theirs (MapScene.RingExits, Near). Not by distance from anything.
        var (camera, scene) = Raid(fix: false);
        var frames = Frames(scene, camera);
        string[] order = ["extract:far", "extract:mid", "extract:near", "extract:unsure-b", "extract:unsure-a"];
        for (var i = 0; i < order.Length; i++)
            Assert.Equal(i * MapRenderer.ExitTurn.TotalMilliseconds, TurnMs(frames, order[i])!.Value, 3);
        var (camera2, again) = Raid(fix: false);
        var other = Frames(again, camera2);
        for (var k = 0; k < Count; k++)
        {
            foreach (var (id, t) in frames[k])
            {
                Assert.Equal(t.At, other[k][id].At);
                Assert.Equal(t.Arrival, other[k][id].Arrival);
                Assert.Equal(t.Glow, other[k][id].Glow);
                Assert.Equal(t.Question, other[k][id].Question);
            }
        }
    }

    [Fact]
    public void The_first_and_the_last_frame_differ_only_in_the_glow_and_the_question_marks()
    {
        var (camera, scene) = Raid();
        var frames = Frames(scene, camera);
        var (first, last) = (frames[0], frames[^1]);
        Assert.Equal(first.Keys.Order(), last.Keys.Order());
        foreach (var (id, a) in first)
        {
            var b = last[id];
            Assert.Equal(a.At, b.At);
            Assert.Equal(a.R, b.R);
            Assert.Equal(a.Look, b.Look);
            switch (a.Look)
            {
                case ExitLook.Listed:
                    // Unlit before its turn, or (the first) at its very start; lit at the end, every ring over.
                    Assert.Equal(0, a.Glow);
                    Assert.Equal(1, b.Glow);
                    Assert.Null(b.Arrival);
                    Assert.Empty(b.Echoes);
                    break;
                case ExitLook.Unsure:
                    Assert.Equal(0, a.Question);
                    Assert.Equal(1, b.Question);
                    Assert.Null(b.Arrival);
                    break;
                default:
                    Assert.Null(a.Glow ?? b.Glow);
                    Assert.Null(a.Question ?? b.Question);
                    Assert.Empty(a.Echoes);
                    Assert.Empty(b.Echoes);
                    break;
            }
        }

        // The pictures differ only where the trace says something differs: around the lit ones (their glow and the
        // first one's echo) and in the unsure ones' triangles (their "?").
        using var at0 = Picture(scene, camera, T0);
        using var atLast = Picture(scene, camera, T0 + Every * (Count - 1));
        var lit = first.Values.Where(t => t.Look == ExitLook.Listed).ToList();
        var unsure = first.Values.Where(t => t.Look == ExitLook.Unsure).ToList();
        var stray = 0;
        var differing = 0;
        for (var y = 0; y < 1000; y++)
        {
            for (var x = 0; x < 1000; x++)
            {
                var (p, q) = (at0.GetPixel(x, y), atLast.GetPixel(x, y));
                if (Math.Abs(p.Red - q.Red) + Math.Abs(p.Green - q.Green) + Math.Abs(p.Blue - q.Blue) <= 6)
                    continue;
                differing++;
                var point = new SKPoint(x, y);
                if (!lit.Any(t => SKPoint.Distance(point, t.At) <= t.R * 3.4f + 2) && !unsure.Any(t => SKPoint.Distance(point, t.At) <= t.R * 1.4f))
                    stray++;
            }
        }
        Assert.True(differing > 200, $"{differing} pixels differ: the glow should be there at the end and not at the start");
        Assert.Equal(0, stray);
        // The "?" is in the picture as the trace says: not yet at t0, there at the end.
        var mark = first["extract:unsure-a"].At;
        Assert.True(Dark(atLast, mark) > Dark(at0, mark) + 6, $"{Dark(atLast, mark)} dark pixels with the ?, {Dark(at0, mark)} before");
    }

    // Pixels tinted green 13 to 18 px from a symbol's centre: its glow's, past its triangle and collar.
    private static int Glow(SKBitmap bitmap, SKPoint at)
    {
        var tinted = 0;
        for (var y = (int)at.Y - 20; y <= at.Y + 20; y++)
        {
            for (var x = (int)at.X - 20; x <= at.X + 20; x++)
            {
                var d = SKPoint.Distance(new SKPoint(x, y), at);
                var c = bitmap.GetPixel(x, y);
                if (d is >= 13 and <= 18 && c.Green > c.Red + 4 && c.Green > c.Blue + 8)
                    tinted++;
            }
        }
        return tinted;
    }

    // Dark pixels (the ground's) in the middle of a triangle, where a "?" stands.
    private static int Dark(SKBitmap bitmap, SKPoint at)
    {
        var count = 0;
        for (var x = (int)at.X - 3; x <= at.X + 3; x++)
        {
            for (var y = (int)at.Y - 3; y <= at.Y + 4; y++)
            {
                var c = bitmap.GetPixel(x, y);
                if (c.Red + c.Green + c.Blue < 120)
                    count++;
            }
        }
        return count;
    }
}
