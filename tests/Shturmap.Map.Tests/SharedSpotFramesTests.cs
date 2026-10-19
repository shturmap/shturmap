using Shturmap.Core;
using Shturmap.Core.Maps;
using SkiaSharp;
using static Shturmap.Map.Tests.TestView;

namespace Shturmap.Map.Tests;

// Symbols on one spot settle once and stay put however often the view moves (owner, 2026-10-17, with a screenshot of
// Woods: "There is a bug when two items are close to each other and their repel-lines are parallel they get expanded every
// time when the user pans or zooms the camera"; docs/DESIGN.md, "Map drawing"). tarkov.dev gives Woods' two Outskirts, a
// PMC's and a Scav's on one spot, one id. The repel's placement went on from frame to frame by the markers' ids, so both
// went on from the first one's at every frame of a move: they pushed each other out along one line, up to 7.4 px a frame,
// and after a few glides and notches one stood across the view on a leader (shturmap-cli frames woods --scene plan --sway 4
// --toward Outskirts: 1434 px off its place after the first time back and forth, 5699 px after the fourth).
public class SharedSpotFramesTests
{
    // t0: the day every example uses.
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Local);

    // How often the view goes back and forth.
    private const int Times = 6;

    // A frame every 16 ms; a glide of 1.2 s.
    private const int EveryMs = 16;
    private const int GlideFrames = 75;

    // A symbol's offset from its place may change by this much from one frame to the next (GlideFramesTests' Steady).
    private const float Steady = 3;

    // A spot with two extracts that share one made-up id, a PMC's and a Scav's (the second `apart` metres east), with a
    // transit on it too where there are three; six quests' places round it, 8 to 18 m off, so that the view's zoom changes
    // the distances between the places and the repel goes on from frame to frame while it moves. One metre is one pixel at
    // the start.
    private static (Camera Camera, MapScene Scene) Spot(int count, double apart)
    {
        var markers = new List<MapMarker>
        {
            new("extract:spot", MarkerKind.ExtractPmc, new WorldPoint(0, 0, 0), "Spot"),
            new("extract:spot", MarkerKind.ExtractScav, new WorldPoint(apart, 0, 0), "Spot"),
        };
        if (count == 3)
            markers.Add(new MapMarker("transit:spot", MarkerKind.Transit, new WorldPoint(0, 0, 0), "Transit"));
        for (var i = 0; i < 6; i++)
            markers.Add(Quest($"round{i}", (8 + 2 * i) * Math.Cos(i * 1.1), (8 + 2 * i) * Math.Sin(i * 1.1), $"Round {i}"));
        return Of(markers);
    }

    // Each of the spot's symbols (by id and kind: two share an id) as the frame draws it: how far off its own place.
    private static Dictionary<string, SKPoint> Frame(Camera camera, MapScene scene, DateTime at) =>
        MapRenderer.ShownMarkers(camera, scene, 1, at).Where(m => m.Marker.Id.EndsWith(":spot", StringComparison.Ordinal))
            .ToDictionary(m => $"{m.Marker.Id}|{m.Marker.Kind}", m => m.At - camera.ToScreen(scene.Projection.ToMap(m.Marker.Position)));

    // MapView.AnimateView's ease: in and out, the zoom on a log scale.
    private static (MapPoint Center, double Zoom) Glide((MapPoint Center, double Zoom) from, (MapPoint Center, double Zoom) to, double t)
    {
        var e = t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
        return (new MapPoint(from.Center.X + (to.Center.X - from.Center.X) * e, from.Center.Y + (to.Center.Y - from.Center.Y) * e),
            Math.Exp(Math.Log(from.Zoom) + (Math.Log(to.Zoom) - Math.Log(from.Zoom)) * e));
    }

    [Theory]
    [InlineData(2, 0.0)]
    [InlineData(2, 1.5)]
    [InlineData(3, 0.0)]
    public void Symbols_on_one_spot_settle_once_and_stay_put_however_often_the_view_moves(int count, double apart)
    {
        var (camera, scene) = Spot(count, apart);
        var now = T0;
        // Each frame's offsets, and which time back and forth it belongs to.
        var frames = new List<(int Time, Dictionary<string, SKPoint> Off)>();
        void Draw(int time)
        {
            frames.Add((time, Frame(camera, scene, now)));
            now = now.AddMilliseconds(EveryMs);
        }
        var start = (new MapPoint(0, 0), 1.0);
        camera.Restore(start.Item1, start.Item2);
        Draw(0);
        var pointer = camera.ToScreen(new MapPoint(10, -5));
        for (var time = 1; time <= Times; time++)
        {
            // A glide in to 2.5 times the zoom off to one side, and back out.
            var near = (new MapPoint(20, -10), 2.5);
            for (var k = 1; k <= GlideFrames; k++)
            {
                var (center, zoom) = Glide(start, near, k / (double)GlideFrames);
                camera.Restore(center, zoom);
                Draw(time);
            }
            for (var k = 1; k <= GlideFrames; k++)
            {
                var (center, zoom) = Glide(near, start, k / (double)GlideFrames);
                camera.Restore(center, zoom);
                Draw(time);
            }
            // Three of the wheel's notches in and three out, at the pointer, as MapView turns them.
            var wheel = new WheelZoom();
            foreach (var delta in new[] { 120, 120, 120, -120, -120, -120 })
            {
                wheel.Turn(camera.Zoom, WheelZoom.FactorOf(delta), camera.MinZoom, camera.MaxZoom, now);
                while (true)
                {
                    var landed = wheel.Over(now);
                    camera.ZoomAt(pointer, wheel.At(now) / camera.Zoom);
                    Draw(time);
                    if (landed)
                        break;
                }
            }
            // A pan off to one side and back.
            for (var k = 0; k < 30; k++)
            {
                camera.Pan(4, -3);
                Draw(time);
            }
            for (var k = 0; k < 30; k++)
            {
                camera.Pan(-4, 3);
                Draw(time);
            }
        }
        Assert.All(frames, f => Assert.Equal(count, f.Off.Count));

        // Bounded: none stands further off its place than a leader's least length (the extract's half width, the collar and
        // LeaderLeast: 21 px) and what the quests round it push, two and a half times that in all; with the placement gone on
        // from the first one's, the two ran out to LeaderMost within the first glide.
        var least = MapRenderer.RestHalf(scene.Markers[0], 1) + 3 + MapRenderer.LeaderLeast;
        var farthest = frames.SelectMany(f => f.Off.Values).Max(o => o.Length);
        Assert.True(farthest <= 2.5f * least, $"{farthest:0.0} px off its place, {2.5f * least:0.0} at most");

        // Steady: from one frame to the next, none moves off its place by more than a step and a neighbour's push.
        for (var k = 1; k < frames.Count; k++)
        {
            foreach (var (key, off) in frames[k].Off)
            {
                var moved = SKPoint.Distance(off, frames[k - 1].Off[key]);
                Assert.True(moved <= Steady, $"{key} moved {moved:0.0} px off its place from frame {k - 1} to {k}");
            }
        }

        // Settled: the first time back and forth goes on from the fresh layout at the start; from the second on, each takes
        // them no further off their places than the one before, and ends, the view back where it started, where the one
        // before left them: the same moves draw the same frames.
        float Farthest(int time) => frames.Where(f => f.Time == time).SelectMany(f => f.Off.Values).Max(o => o.Length);
        Dictionary<string, SKPoint> End(int time) => frames.Last(f => f.Time == time).Off;
        for (var time = 2; time <= Times; time++)
        {
            Assert.True(Farthest(time) <= Farthest(time - 1) + 0.01f, $"time {time}: {Farthest(time):0.00} px off its place, the time before {Farthest(time - 1):0.00}");
            if (time < 3)
                continue;
            foreach (var (key, off) in End(time))
            {
                var from = SKPoint.Distance(off, End(time - 1)[key]);
                Assert.True(from <= 0.01f, $"time {time}: {key} ends {from:0.00} px from where the time before left it");
            }
        }
    }

    // Placed afresh in a view packed tighter than its symbols fit, the search for a free spot sent one far across it (up
    // to 360 px in the whole of Customs at the smallest window); none stands further off its place than LeaderMost.
    [Fact]
    public void No_symbol_stands_further_off_its_place_than_the_most()
    {
        // Eighty quests' places within three metres of one spot: three pixels at this zoom.
        var markers = Enumerable.Range(0, 80).Select(i => Quest($"packed{i}", 3 * Math.Cos(i * 2.4), 3 * Math.Sin(i * 2.4), $"Packed {i}")).ToList();
        var (camera, scene) = Of(markers);
        var shown = MapRenderer.ShownMarkers(camera, scene, 1, T0);
        var offsets = shown.Select(m => SKPoint.Distance(m.At, camera.ToScreen(scene.Projection.ToMap(m.Marker.Position)))).ToList();
        Assert.Equal(80, shown.Count);
        Assert.All(offsets, o => Assert.True(o <= MapRenderer.LeaderMost + 0.01f, $"{o:0.0} px off its place"));
        // The crowd still opens: some stand off on leaders, and far.
        Assert.True(offsets.Max() > MapRenderer.LeaderMost / 2, $"the farthest {offsets.Max():0.0} px off its place");
    }
}
