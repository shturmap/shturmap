using Shturmap.Core;
using Shturmap.Core.Maps;
using SkiaSharp;
using static Shturmap.Map.Tests.TestView;

namespace Shturmap.Map.Tests;

// The roll call (owner, 2026-10-15, from the design panel: option A, "Roll call, then fly-out"; docs/DESIGN.md, "Map
// drawing"), seen frame by frame as shturmap-cli frames --scene list-callout draws it: a list just read, its extracts
// called from the band nearest the player first, each icon lifting off its row 0.4 s after the one before from 1.6 s,
// flying to its place in 1.5 s and landing as its ring starts (owner, 2026-10-16: "too fast"; until then 0.16 s apart and
// 0.9 s). The asserts read the renderer's trace (SymbolTrace); a few pixels show that the trace is what the picture shows.
public class ExitCallOutFramesTests
{
    // t0, the moment the list is read: the day every example uses.
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Local);

    // Every 20 ms to 6 s: each lift-off and landing falls on a frame.
    private static readonly TimeSpan Every = TimeSpan.FromMilliseconds(20);

    private const int Count = 300;

    // A time in whole milliseconds, to the tick: TimeSpan.FromSeconds(3.3) cuts its last tick off.
    private static TimeSpan Ms(long ms) => TimeSpan.FromTicks(ms * TimeSpan.TicksPerMillisecond);

    private static readonly SKColor Green = Palette.Sk(Palette.Green);

    private static MapMarker Exit(string id, MarkerKind kind, double x, double z) => new(id, kind, new WorldPoint(x, 0, z), id);

    // ExitRingFramesTests' made-up raid: three lit, two "??:??:??", two struck out, a Scav extract the list doesn't speak
    // of on the very place of a lit one, a transit, and a quest by a struck-out one, in the data's order, which isn't the
    // order of distance from the fix at the middle (500, 500 on screen; world x to the right, z up).
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

    // The band's rows below the middle, as the window would hand them: two columns of three, 22 px apart, the turns down
    // the first column, then the second; each row's triangle where its icon lifts off.
    private static SKPoint Row(int turn) => new(330 + turn / 3 * 170, 838 + turn % 3 * 22);

    // The calls the window makes: the roll call's order (MapScene.ExitsToCall) and each row's place.
    private static List<ExitCall> Calls(MapScene scene) =>
        scene.ExitsToCall().Select((m, i) => new ExitCall(m.Id, Row(i).X, Row(i).Y, i)).ToList();

    // Nearest the fix first, lit and "??:??:??" alike: near (60 m), unsure-a (72 m), mid (180 m), unsure-b (283 m), far (391 m).
    private static readonly string[] Called = ["extract:near", "extract:unsure-a", "extract:mid", "extract:unsure-b", "extract:far"];

    // What one frame drew of every symbol, by id.
    private static Dictionary<string, SymbolTrace> Frame(MapScene scene, Camera camera, DateTime at)
    {
        var trace = new List<SymbolTrace>();
        using (var bitmap = new SKBitmap(1000, 1000))
        using (var canvas = new SKCanvas(bitmap))
            MapRenderer.Render(canvas, camera, scene, 1, null, at, trace);
        return trace.ToDictionary(t => t.Id);
    }

    // The frames from t0, each drawn at its time; `before` runs before the k-th is drawn (to read another list).
    private static List<Dictionary<string, SymbolTrace>> Frames(MapScene scene, Camera camera, int count = Count, TimeSpan? every = null,
        Action<int, DateTime>? before = null)
    {
        var frames = new List<Dictionary<string, SymbolTrace>>();
        for (var k = 0; k < count; k++)
        {
            var at = T0 + (every ?? Every) * k;
            before?.Invoke(k, at);
            frames.Add(Frame(scene, camera, at));
        }
        return frames;
    }

    // The raid called out at t0, drawn every 20 ms: the frames most tests read, drawn once.
    private static readonly Lazy<(MapScene Scene, Camera Camera, List<Dictionary<string, SymbolTrace>> Frames)> CalledOut = new(() =>
    {
        var (camera, scene) = Raid();
        scene.CallOutExits(Calls(scene), T0);
        return (scene, camera, Frames(scene, camera));
    });

    private static SKBitmap Picture(MapScene scene, Camera camera, DateTime at)
    {
        var bitmap = new SKBitmap(1000, 1000);
        using var canvas = new SKCanvas(bitmap);
        MapRenderer.Render(canvas, camera, scene, 1, null, at);
        return bitmap;
    }

    [Fact]
    public void The_roll_call_goes_nearest_the_player_first_lit_and_unsure_alike()
    {
        var (_, scene) = Raid();
        Assert.Equal(Called, scene.ExitsToCall().Select(m => m.Id));
        // Without a position, in the data's order; never a struck-out one, nor the Scav extract the list doesn't speak of.
        var (_, alone) = Raid(fix: false);
        Assert.Equal(new[] { "extract:far", "extract:unsure-b", "extract:mid", "extract:near", "extract:unsure-a" }, alone.ExitsToCall().Select(m => m.Id));
    }

    // The design's words (docs/DESIGN.md, "Map drawing", *A list just read is called out*; owner, 2026-10-16: "too fast"):
    // from 1.6 s the icons lift off 0.4 s apart, the roll call's own turn, and each flies 1.5 s; a list after the cue still
    // rings its extracts at the rings' turn, 0.16 s apart.
    [Fact]
    public void The_icons_lift_off_0_4_s_apart_from_1_6_s_and_fly_1_5_s_while_rings_keep_their_0_16_s()
    {
        Assert.Equal((Ms(1600), Ms(400), Ms(1500), Ms(160)), (MapScene.CallOutLiftOff, MapScene.CallOutTurn, MapScene.FlightLength, MapRenderer.ExitTurn));
    }

    [Fact]
    public void Each_lifts_off_a_turn_after_the_one_before_from_1_6_s_and_flies_1_5_s()
    {
        var (scene, _, frames) = CalledOut.Value;
        for (var turn = 0; turn < Called.Length; turn++)
        {
            var id = Called[turn];
            var flight = scene.ExitFlights[id];
            var liftOff = 1600 + 400 * turn;
            Assert.Equal(turn, flight.Turn);
            Assert.Equal(T0 + Ms(liftOff), flight.LiftOff);
            Assert.Equal(T0 + Ms(liftOff + 1500), flight.Landing);
            // The frames say so: in the air from its lift-off's frame to the one before its landing's, never else.
            var flying = frames.Select((f, k) => (K: k, f[id].Flight)).Where(f => f.Flight is not null).ToList();
            Assert.Equal(liftOff / 20, flying[0].K);
            Assert.Equal((liftOff + 1500) / 20 - 1, flying[^1].K);
            Assert.Equal(75, flying.Count);
            foreach (var (k, p) in flying)
                Assert.Equal(liftOff, k * 20 - p!.Value * 1500, 6);
        }
        // Last landing at 4.7 s for five (far, lit); its rings go on 2.1 s, to 6.8 s, and the window draws until they are
        // over.
        Assert.True(scene.ExitsArrivingAt(T0 + Ms(6750)));
        Assert.False(scene.ExitsArrivingAt(T0 + Ms(6800)));
    }

    [Fact]
    public void The_icon_flies_an_arc_from_its_row_to_its_place_growing_with_a_lift()
    {
        var (scene, camera, frames) = CalledOut.Value;
        foreach (var id in Called)
        {
            var flight = scene.ExitFlights[id];
            var from = new SKPoint(flight.FromX, flight.FromY);
            var k = (int)Math.Round((flight.LiftOff - T0).TotalMilliseconds / 20);
            // Lift-off: on its row, at 0.8 of its size.
            Assert.Equal(0, frames[k][id].Flight);
            Assert.Equal(from, frames[k][id].Flying);
            Assert.Equal(0.8f, frames[k][id].FlightScale);
            // A moment before landing: on its symbol as drawn, at its size.
            var place = frames[k][id].At;
            var late = Frame(scene, camera, flight.Landing - TimeSpan.FromMilliseconds(1))[id];
            Assert.True(SKPoint.Distance(late.Flying!.Value, place) < 0.01f, $"{id} at {late.Flying} a moment before landing on {place}");
            Assert.Equal(1, late.FlightScale!.Value, 3);

            // Halfway: on the line's upper side, a tenth of the distance off it (its control point a fifth), and lifted
            // to 1.12 of its size. The upper side is the one its normal with the smaller y points to: left of a line that
            // runs right, right of one that runs left.
            var half = Frame(scene, camera, flight.LiftOff + MapScene.FlightLength / 2)[id];
            var at = half.Flying!.Value;
            var d = place - from;
            var cross = d.X * (at.Y - from.Y) - d.Y * (at.X - from.X);
            Assert.True(cross * Math.Sign(d.X) < 0, $"{id} halfway at {at}, below its line from {from} to {place}");
            Assert.Equal(1.12f, half.FlightScale!.Value, 3);
            var off = Math.Abs(cross) / d.Length;
            // Steeper than a fifth across for the way along, the arc flattens toward the line as the line turns upright,
            // half its way across off it (MapRenderer.FlightAt): mid's row lies right under it.
            if (Math.Abs(d.X) >= 0.2f * d.Length)
            {
                Assert.Equal(d.Length / 10, off, 1);
                var lineY = from.Y + (at.X - from.X) * d.Y / d.X;
                Assert.True(at.Y < lineY - 1, $"{id} halfway at {at}, the line at {lineY}");
            }
            else
            {
                Assert.Equal(Math.Abs(d.X) / 2, off, 1);
            }

            // Eased in and out: a quarter of its time in, less than a tenth of its way gone; at three quarters, more than
            // nine tenths.
            var quarter = Frame(scene, camera, flight.LiftOff + MapScene.FlightLength / 4)[id].Flying!.Value;
            var threeQuarters = Frame(scene, camera, flight.Landing - MapScene.FlightLength / 4)[id].Flying!.Value;
            Assert.True(SKPoint.Distance(quarter, from) < d.Length / 10, $"{id} {SKPoint.Distance(quarter, from)} px from its row a quarter in");
            Assert.True(SKPoint.Distance(threeQuarters, place) < d.Length / 10, $"{id} {SKPoint.Distance(threeQuarters, place)} px from its place three quarters in");
        }
        // The pure path the frames drew along: exact at both ends.
        var (start, scale0) = MapRenderer.FlightAt(new SKPoint(100, 900), new SKPoint(700, 300), 0);
        var (end, scale1) = MapRenderer.FlightAt(new SKPoint(100, 900), new SKPoint(700, 300), 1);
        Assert.Equal((new SKPoint(100, 900), 0.8f), (start, scale0));
        Assert.Equal((new SKPoint(700, 300), 1f), (end, scale1));
    }

    [Fact]
    public void It_lands_as_its_ring_starts_and_rings_as_a_list_rung_at_once_does()
    {
        var (scene, camera, _) = CalledOut.Value;
        var (ringCamera, rung) = Raid();
        rung.RingExits(T0);
        foreach (var id in Called)
        {
            var landing = scene.ExitFlights[id].Landing;
            Assert.Equal(landing, scene.ExitsRinging[id].At);
            // From its landing on, glow, echoes and "?" as RingExits rings it from its turn: tenth by tenth of a second.
            for (var tenth = 0; tenth <= 22; tenth++)
            {
                var dt = TimeSpan.FromMilliseconds(100 * tenth);
                var flown = Frame(scene, camera, landing + dt)[id];
                var rang = Frame(rung, ringCamera, rung.ExitsRinging[id].At + dt)[id];
                Assert.Null(flown.Flight);
                Assert.Equal(rang.Arrival, flown.Arrival);
                Assert.Equal(rang.Glow, flown.Glow);
                Assert.Equal(rang.Echoes, flown.Echoes);
                Assert.Equal(rang.Question, flown.Question);
                Assert.Equal(rang.At, flown.At);
            }
        }
    }

    [Fact]
    public void Its_own_symbol_waits_unlit_at_its_place_until_it_lands()
    {
        var (scene, camera, frames) = CalledOut.Value;
        foreach (var id in Called)
        {
            var landing = (int)Math.Round((scene.ExitFlights[id].Landing - T0).TotalMilliseconds / 20);
            for (var k = 0; k < landing; k++)
            {
                var t = frames[k][id];
                Assert.True(t.Arrival < 0, $"{id} rings at frame {k}, before its landing at {landing}");
                Assert.Empty(t.Echoes);
                if (t.Look == ExitLook.Listed)
                    Assert.Equal(0, t.Glow);
                else
                    Assert.Equal(0, t.Question);
                // It doesn't move either: the icon flies, the symbol waits at its place.
                Assert.Equal(frames[0][id].At, t.At);
            }
            Assert.Equal(0, frames[landing][id].Arrival);
            Assert.Null(frames[landing][id].Flight);
        }

        // The picture says so: no glow round the nearest while its icon is halfway (near its place the icon's own green
        // would count), its glow once it has landed.
        var near = scene.ExitFlights["extract:near"].Landing;
        var place = frames[0]["extract:near"].At;
        using var before = Picture(scene, camera, near - MapScene.FlightLength / 2);
        using var after = Picture(scene, camera, near + TimeSpan.FromMilliseconds(300));
        Assert.True(Glow(before, place) < 20 && Glow(after, place) > 100, $"{Glow(before, place)} glowing pixels before landing, {Glow(after, place)} after");
    }

    // The bold comes with the lighting (owner, 2026-10-15: until then every listed name was bold from the first frame):
    // a listed extract's name is in the regular face, as an unlit one's, until its icon lands, and bold from its landing
    // on; it keeps the bold name's box throughout, so no label moves as one lights. "??:??:??" and struck-out names are
    // never bold.
    [Fact]
    public void A_listed_name_is_regular_until_its_icon_lands_and_bold_from_then_on()
    {
        var (scene, camera, frames) = CalledOut.Value;
        Assert.All(frames[0].Values.Where(t => t.Look is not null), t => Assert.NotEqual(true, t.Bold));
        var lit = Called.Where(scene.ExitsListed.Contains).ToList();
        Assert.Equal(3, lit.Count);
        foreach (var id in lit)
        {
            var landing = (int)Math.Round((scene.ExitFlights[id].Landing - T0).TotalMilliseconds / 20);
            for (var k = 0; k < frames.Count; k++)
                Assert.True(frames[k][id].Bold == k >= landing, $"{id} {(frames[k][id].Bold is { } b ? b ? "bold" : "regular" : "not drawn")} at frame {k}, landing at {landing}");
        }
        foreach (var t in frames.SelectMany(f => f.Values.Where(t => t.Look is ExitLook.Unsure or ExitLook.NotListed or ExitLook.Plain or ExitLook.Transit)))
            Assert.NotEqual(true, t.Bold);

        // Its name's box is the same from the first frame to the last.
        string Boxes(DateTime at) => string.Join(" ", MapRenderer.LayoutOf(camera, scene, 1, at).Labels.Select(l => $"{l.Text} {l.Box}"));
        Assert.Equal(Boxes(T0), Boxes(T0 + Ms(5900)));

        // The picture says so: more ink in the nearest's name once it has landed than a moment before.
        var near = scene.ExitFlights["extract:near"].Landing;
        var box = SKRect.Inflate(MapRenderer.LayoutOf(camera, scene, 1, near).Labels.Single(l => l.Of.Marker.Id == "extract:near").Box, 2, 2);
        using var before = Picture(scene, camera, near - TimeSpan.FromMilliseconds(1));
        using var after = Picture(scene, camera, near + TimeSpan.FromMilliseconds(1500));
        Assert.True(Ink(after, box) > Ink(before, box) * 1.1, $"{Ink(before, box)} ink pixels in the name before landing, {Ink(after, box)} after");
    }

    // Pixels of the labels' ink in a box.
    private static int Ink(SKBitmap bitmap, SKRect box)
    {
        var ink = Palette.Sk(Palette.Ink);
        var count = 0;
        for (var y = (int)box.Top; y < box.Bottom; y++)
        {
            for (var x = (int)box.Left; x < box.Right; x++)
            {
                if (Near(bitmap.GetPixel(x, y), ink))
                    count++;
            }
        }
        return count;
    }

    [Fact]
    public void Struck_out_scav_and_transit_symbols_never_change_and_no_call_moves_them()
    {
        var (camera, scene) = Raid();
        // The window calls them too, by mistake: they never fly.
        var calls = Calls(scene).Concat(new ExitCall[] { new("extract:closed-a", 600, 860, 5), new("transit:t", 600, 882, 6),
            new("extract:twin", 770, 838, 7), new("extract:gone", 770, 860, 8) }).ToList();
        Assert.Equal(Called, scene.CallOutExits(calls, T0).Select(f => f.MarkerId));
        var frames = Frames(scene, camera);
        foreach (var id in new[] { "extract:closed-a", "extract:closed-b", "extract:twin", "transit:t" })
        {
            Assert.False(scene.ExitFlights.ContainsKey(id));
            var first = frames[0][id];
            foreach (var frame in frames)
            {
                var t = frame[id];
                Assert.Null(t.Flight);
                Assert.Null(t.Flying);
                Assert.Null(t.Arrival);
                Assert.Null(t.Glow);
                Assert.Empty(t.Echoes);
                Assert.Null(t.Question);
                Assert.Equal(first.At, t.At);
                Assert.Equal(first.Look, t.Look);
            }
        }
    }

    [Fact]
    public void A_later_list_adds_only_what_it_lit_anew_after_the_last_turn_and_the_same_list_again_changes_nothing()
    {
        string Drawn(SymbolTrace t) => FormattableString.Invariant(
            $"{t.Id} {t.At} {t.Look} {t.Arrival} {t.Glow} {string.Join("/", t.Echoes)} {t.Question} {t.Flight} {t.Flying} {t.FlightScale}");
        IReadOnlyList<ExitFlight> added = [];
        void Later(MapScene scene, int k, DateTime at)
        {
            // Half a second in, a second screenshot's list names closed-a too: the window calls the whole list again,
            // closed-a third by distance.
            if (k == 5)
            {
                scene.ExitsListed = new HashSet<string>(scene.ExitsListed) { "extract:closed-a" };
                scene.ExitsNotListed = new HashSet<string> { "extract:closed-b" };
                added = scene.CallOutExits(Calls(scene), at);
            }
        }
        var tenth = TimeSpan.FromMilliseconds(100);
        var (camera, scene) = Raid();
        scene.CallOutExits(Calls(scene), T0);
        var once = Frames(scene, camera, 60, tenth, (k, at) => Later(scene, k, at));

        // Only closed-a flies anew, after the last turn (far's, at 3.2 s): at 3.6 s, not at once and not before the others.
        var flight = Assert.Single(added);
        Assert.Equal(("extract:closed-a", 5, T0 + Ms(3600), true), (flight.MarkerId, flight.Turn, flight.LiftOff, flight.Listed));
        Assert.Equal(new ExitRing(T0 + Ms(5100), true), scene.ExitsRinging["extract:closed-a"]);
        // Its symbol turns from struck out to unlit as the list is read, and stays where it stood.
        Assert.Equal(ExitLook.NotListed, once[4]["extract:closed-a"].Look);
        Assert.Equal(ExitLook.Listed, once[5]["extract:closed-a"].Look);
        Assert.Equal(0, once[5]["extract:closed-a"].Glow);
        Assert.Equal(once[4]["extract:closed-a"].At, once[5]["extract:closed-a"].At);

        // What flew already carries on as it would have without the later list.
        var (camera2, alone) = Raid();
        alone.CallOutExits(Calls(alone), T0);
        var without = Frames(alone, camera2, 60, tenth);
        for (var k = 0; k < 60; k++)
        {
            foreach (var id in Called)
                Assert.Equal(Drawn(without[k][id]), Drawn(once[k][id]));
        }

        // The same list again, a second in: nothing changes, in any frame.
        var (camera3, again) = Raid();
        again.CallOutExits(Calls(again), T0);
        var twice = Frames(again, camera3, 60, tenth, (k, at) =>
        {
            Later(again, k, at);
            if (k == 10)
                Assert.Empty(again.CallOutExits(Calls(again), at));
        });
        for (var k = 0; k < 60; k++)
            Assert.Equal(once[k].Values.Where(t => t.Look is not null).Select(Drawn).Order(), twice[k].Values.Where(t => t.Look is not null).Select(Drawn).Order());

        // One more at 4.5 s, after every icon has left its row (closed-a last, at 3.6 s) and while the rings go on: it lifts
        // off then, never in the past.
        again.ExitsListed = new HashSet<string>(again.ExitsListed) { "extract:closed-b" };
        again.ExitsNotListed = new HashSet<string>();
        var last = Assert.Single(again.CallOutExits(Calls(again), T0 + Ms(4500)));
        Assert.Equal(("extract:closed-b", 6, T0 + Ms(4500)), (last.MarkerId, last.Turn, last.LiftOff));
    }

    [Fact]
    public void Once_all_is_over_a_list_calls_afresh()
    {
        var (_, scene) = Raid();
        scene.CallOutExits(Calls(scene), T0);
        var later = T0 + Ms(10_000);
        Assert.False(scene.ExitsArrivingAt(later));
        // The player looked again: every extract flies again, from that moment's turns.
        var again = scene.CallOutExits(Calls(scene), later);
        Assert.Equal(Called, again.Select(f => f.MarkerId));
        Assert.Equal(later + MapScene.CallOutLiftOff, scene.ExitFlights["extract:near"].LiftOff);
    }

    // A list that comes once the roll call's cue has gone (the window rings it, MapScene.RingExits): only what it lit or
    // marked anew rings, where it stands, a turn apart, nearest the player first, lit and "??:??:??" alike; the same list
    // again changes nothing (owner, 2026-10-15, from the design panel: option A; docs/DESIGN.md, "Map drawing"). Until
    // then a list after the rings rang every extract again.
    [Fact]
    public void After_a_roll_call_a_later_list_rings_only_what_it_lit_or_marked_anew_where_it_stands()
    {
        var (camera, scene) = Raid();
        scene.CallOutExits(Calls(scene), T0);
        // What the roll call called, as the window keeps it for the raid (Rules.RollCallRecord).
        var record = Record(scene);
        var later = T0 + Ms(10_000);
        Assert.False(scene.ExitsArrivingAt(later));
        var called = scene.ExitsRinging.ToDictionary(r => r.Key, r => r.Value);

        // The same list again: nothing rings, nothing flies.
        scene.RingExits(later, record);
        Assert.Equal(called, scene.ExitsRinging);
        Assert.False(scene.ExitsArrivingAt(later));

        // A third screenshot's list lights closed-a (102 m away) and marks closed-b "??:??:??" (361 m).
        scene.ExitsListed = new HashSet<string>(scene.ExitsListed) { "extract:closed-a" };
        scene.ExitsUnsure = new HashSet<string>(scene.ExitsUnsure) { "extract:closed-b" };
        scene.ExitsNotListed = new HashSet<string>();
        var standing = Frame(scene, camera, later);
        scene.RingExits(later, record);
        Assert.Equal(new ExitRing(later, true), scene.ExitsRinging["extract:closed-a"]);
        Assert.Equal(new ExitRing(later + MapRenderer.ExitTurn, false), scene.ExitsRinging["extract:closed-b"]);
        foreach (var id in Called)
            Assert.Equal(called[id], scene.ExitsRinging[id]);
        // Rung where they stand: nothing flies, and the others rest lit.
        Assert.DoesNotContain("extract:closed-a", scene.ExitFlights.Keys);
        Assert.DoesNotContain("extract:closed-b", scene.ExitFlights.Keys);
        var ringing = Frame(scene, camera, later + Ms(300));
        Assert.NotEmpty(ringing["extract:closed-a"].Echoes);
        Assert.Null(ringing["extract:closed-a"].Flying);
        Assert.Equal(standing["extract:closed-a"].At, ringing["extract:closed-a"].At);
        Assert.Equal(0.14, ringing["extract:closed-b"].Arrival!.Value, 6);
        foreach (var id in Called)
        {
            Assert.Null(ringing[id].Arrival);
            Assert.Empty(ringing[id].Echoes);
        }

        // And again, with the record the window keeps by now: nothing changes.
        record = Record(scene);
        var rung = scene.ExitsRinging.ToDictionary(r => r.Key, r => r.Value);
        scene.RingExits(later + Ms(500), record);
        Assert.Equal(rung, scene.ExitsRinging);
    }

    // A look at another map in the raid and back makes the raid map's scene anew, with nothing ringing or flown on it. The
    // roll call's record is the window's, kept for the raid (coordinator, 2026-10-15): until then it was the scene's, so it
    // went with the scene, and the next list got a whole roll call again. With the record, the new scene rings only what the
    // list lit or marked anew.
    [Fact]
    public void A_scene_made_anew_in_the_raid_rings_only_what_a_later_list_lit_or_marked_anew()
    {
        var (_, before) = Raid();
        before.CallOutExits(Calls(before), T0);
        var record = Record(before);

        var (_, again) = Raid();
        var later = T0 + Ms(30_000);
        // The same list: nothing rings.
        again.RingExits(later, record);
        Assert.Empty(again.ExitsRinging);
        // A list that lights closed-a anew: it alone rings, where it stands.
        again.ExitsListed = new HashSet<string>(again.ExitsListed) { "extract:closed-a" };
        again.ExitsNotListed = new HashSet<string> { "extract:closed-b" };
        again.RingExits(later, record);
        Assert.Equal(["extract:closed-a"], again.ExitsRinging.Keys);
        Assert.Equal(new ExitRing(later, true), again.ExitsRinging["extract:closed-a"]);
        Assert.Empty(again.ExitFlights);
    }

    [Fact]
    public void Without_a_roll_call_in_the_raid_a_list_after_the_rings_rings_them_all_again()
    {
        var (_, scene) = Raid();
        scene.CallOutExits(Calls(scene), T0);
        // The raid is over and the next one on the same map has no list yet: the window clears the three sets, and its
        // record is the last raid's, so the next list gets none (Rules.RollCallRecord).
        scene.ExitsNotListed = new HashSet<string>();
        scene.ExitsListed = new HashSet<string>();
        scene.ExitsUnsure = new HashSet<string>();
        // Without a roll call, rings that are over ring again with the next list, as before.
        var later = T0 + Ms(10_000);
        scene.ExitsListed = new HashSet<string> { "extract:far", "extract:mid", "extract:near" };
        scene.RingExits(later);
        Assert.Equal(later, scene.ExitsRinging["extract:near"].At);
        Assert.Empty(scene.ExitFlights);
    }

    // The roll call's extracts as the window records them for the raid: each, lit or "??:??:??".
    private static Dictionary<string, bool> Record(MapScene scene) =>
        scene.ExitsToCall().ToDictionary(m => m.Id, m => scene.ExitsListed.Contains(m.Id), StringComparer.Ordinal);

    [Fact]
    public void With_animation_effects_off_they_are_simply_lit_and_nothing_flies()
    {
        var (camera, scene) = Raid();
        scene.Pulse = false;
        Assert.Empty(scene.CallOutExits(Calls(scene), T0));
        Assert.Empty(scene.ExitFlights);
        Assert.False(scene.ExitsArrivingAt(T0));
        foreach (var frame in Frames(scene, camera, 40, TimeSpan.FromMilliseconds(100)))
        {
            foreach (var id in Called)
            {
                var t = frame[id];
                Assert.Null(t.Flight);
                Assert.Null(t.Flying);
                Assert.Null(t.Arrival);
                Assert.Empty(t.Echoes);
                if (t.Look == ExitLook.Listed)
                    Assert.Equal(1, t.Glow);
                else
                    Assert.Equal(1, t.Question);
                // Its name bold from the first moment, as it is lit.
                Assert.Equal(t.Look == ExitLook.Listed, t.Bold);
            }
        }
        // Lit from the first moment.
        using var picture = Picture(scene, camera, T0);
        Assert.True(Glow(picture, Frame(scene, camera, T0)["extract:near"].At) > 100);
    }

    [Fact]
    public void The_icon_flies_over_every_symbol_and_under_the_player()
    {
        var (camera, scene) = Raid();
        var quest = Frame(scene, camera, T0)["objective:q:1"].At;
        var you = new SKPoint(500, 500);
        // Two rows placed on purpose: the first lifts off on the quest's disc, the second on the player.
        scene.CallOutExits([new ExitCall("extract:near", quest.X, quest.Y, 0), new ExitCall("extract:unsure-a", you.X, you.Y, 1)], T0);
        using var still = Picture(scene, camera, T0);
        using var first = Picture(scene, camera, T0 + MapScene.CallOutLiftOff);
        using var second = Picture(scene, camera, T0 + MapScene.CallOutLiftOff + MapScene.CallOutTurn);
        // Over the quest: the triangle's green where the disc was.
        Assert.False(Near(still.GetPixel((int)quest.X, (int)quest.Y), Green), $"{still.GetPixel((int)quest.X, (int)quest.Y)} at the quest before");
        Assert.True(Near(first.GetPixel((int)quest.X, (int)quest.Y), Green), $"{first.GetPixel((int)quest.X, (int)quest.Y)} at the quest as the icon lifts off on it");
        // Under the player: the player's sand stays.
        var sand = Palette.Sk(Palette.Sand);
        Assert.True(Near(second.GetPixel(500, 500), sand), $"{second.GetPixel(500, 500)} at the player as the icon lifts off under it");
        Assert.Equal(you, Frame(scene, camera, T0 + MapScene.CallOutLiftOff + MapScene.CallOutTurn)["extract:unsure-a"].Flying);
    }

    [Fact]
    public void An_icon_whose_place_is_out_of_view_flies_toward_where_it_is()
    {
        // Close in on the position, as a raid's view often is when the list is read: far (300, -250) is out of view.
        var (camera, scene) = Raid();
        camera.Restore(new MapPoint(0, 0), 4);
        scene.CallOutExits(Calls(scene), T0);
        var flight = scene.ExitFlights["extract:far"];
        var place = camera.ToScreen(scene.Projection.ToMap(new WorldPoint(300, 0, -250)));
        Assert.False(SKRect.Create(0, 0, 1000, 1000).Contains(place), $"{place}");
        var quarter = Frame(scene, camera, flight.LiftOff + MapScene.FlightLength / 4)["extract:far"];
        Assert.False(quarter.InView);
        Assert.Equal(ExitLook.Listed, quarter.Look);
        Assert.Equal(place, quarter.At);
        Assert.Equal(0.25, quarter.Flight!.Value, 9);
        Assert.True(quarter.Arrival < 0);
        // On its way from its row toward the place.
        var from = new SKPoint(flight.FromX, flight.FromY);
        Assert.True(SKPoint.Distance(quarter.Flying!.Value, place) < SKPoint.Distance(from, place));
        // In view, it is the symbol's own record, as ever.
        Assert.True(Frame(scene, camera, flight.LiftOff + MapScene.FlightLength / 4)["extract:near"].InView);
    }

    private static bool Near(SKColor a, SKColor b) => Math.Abs(a.Red - b.Red) + Math.Abs(a.Green - b.Green) + Math.Abs(a.Blue - b.Blue) < 40;

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
}
