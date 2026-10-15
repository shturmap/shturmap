namespace Shturmap.Map.Tests;

// The wheel's zoom, animated (owner, 2026-10-15; docs/DESIGN.md "Map drawing", *How far the view zooms*): a notch zooms
// by about a fifth over 0.15 s, eased, so its frames come under the rule that holds symbols steady while the view moves
// (less than a tenth more or less zoom a frame); notches in a row chain without a jump or a stop; with Windows' animation
// effects off a notch is at once.
public class WheelZoomTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Local);

    private const double Notch = 1.2;

    // A frame of the window's map: about 60 a second.
    private const int FrameMs = 16;

    // MapRenderer.RepelView's glide: a frame that zooms by less than a tenth only moves the view.
    private static readonly double Steady = Math.Log(1.1);

    private static double Step(double a, double b) => Math.Abs(Math.Log(b / a));

    [Fact]
    public void A_notch_is_about_a_fifth()
    {
        Assert.Equal(1.197, WheelZoom.FactorOf(120), 3);
        Assert.Equal(1 / WheelZoom.FactorOf(120), WheelZoom.FactorOf(-120), 9);
        Assert.Equal(TimeSpan.FromMilliseconds(150), WheelZoom.Notch);
    }

    [Fact]
    public void A_notch_from_z_reaches_z_times_its_step_in_0_15_s_eased_in_and_out()
    {
        var wheel = new WheelZoom();
        wheel.Turn(2, Notch, 0.5, 64, T0);
        Assert.Equal(2 * Notch, wheel.Target, 9);
        Assert.Equal(2, wheel.At(T0), 9);
        // Halfway through its time, halfway on the log scale; the first frame moves it little, the last little too.
        Assert.Equal(2 * Math.Sqrt(Notch), wheel.At(T0.AddMilliseconds(75)), 9);
        Assert.True(Step(2, wheel.At(T0.AddMilliseconds(FrameMs))) < 0.1 * Math.Log(Notch));
        Assert.True(Step(wheel.At(T0.AddMilliseconds(150 - FrameMs)), 2 * Notch) < 0.1 * Math.Log(Notch));
        Assert.False(wheel.Over(T0.AddMilliseconds(149)));
        Assert.True(wheel.Over(T0.AddMilliseconds(150)));
        Assert.Equal(2 * Notch, wheel.At(T0.AddMilliseconds(150)), 9);
        Assert.Equal(2 * Notch, wheel.At(T0.AddSeconds(5)), 9);

        // Every frame zooms by a few per cent, under the tenth that lays the symbols out afresh, even at 30 frames a
        // second; and a notch out is a notch in backwards.
        for (var ms = 0; ms < 150; ms += FrameMs)
            Assert.True(Step(wheel.At(T0.AddMilliseconds(ms)), wheel.At(T0.AddMilliseconds(ms + 2 * FrameMs))) < Steady / 1.5);
        var back = new WheelZoom();
        back.Turn(2 * Notch, 1 / Notch, 0.5, 64, T0);
        Assert.Equal(Step(2, wheel.At(T0.AddMilliseconds(40))), Step(back.At(T0.AddMilliseconds(40)), 2 * Notch), 9);
    }

    // The view at each frame, as MapView draws it: a notch is turned at the zoom shown.
    private static List<double> Frames(IReadOnlyList<(int Ms, double Factor)> notches, int untilMs, double zoom = 1)
    {
        var wheel = new WheelZoom();
        var shown = new List<double>();
        var next = 0;
        for (var ms = 0; ms <= untilMs; ms += 1)
        {
            while (next < notches.Count && notches[next].Ms == ms)
                wheel.Turn(zoom, notches[next++].Factor, 0.1, 64, T0.AddMilliseconds(ms));
            if (ms % FrameMs != 0)
                continue;
            if (wheel.Running)
                zoom = wheel.At(T0.AddMilliseconds(ms));
            shown.Add(zoom);
        }
        return shown;
    }

    [Fact]
    public void A_notch_during_another_sets_off_from_the_zoom_shown_and_goes_on_without_a_stop()
    {
        // Three notches 48 ms apart, at frames: the zoom never stands still or turns back on its way, never jumps, and
        // lands 0.15 s after the last on three notches' worth.
        var shown = Frames([(0, Notch), (48, Notch), (96, Notch)], 400);
        var steps = shown.Zip(shown.Skip(1), (a, b) => Math.Log(b / a)).ToList();
        Assert.All(steps.Take(15), s => Assert.True(s > 0, $"a frame stood still or went back: {s}"));
        Assert.All(steps, s => Assert.True(s < Steady, $"a frame zoomed by {Math.Exp(s):0.###}"));
        Assert.Equal(Math.Pow(Notch, 3), shown[^1], 9);
        Assert.Equal(Math.Pow(Notch, 3), shown[(96 + 150) / FrameMs + 1], 9);
        // Where a notch comes, the frame after it zooms at least as fast as the frame before it: it carries the speed it
        // had (from rest at each notch, the zoom would stall there).
        Assert.True(steps[3] >= steps[2], $"{steps[2]:0.####} then {steps[3]:0.####}");
        Assert.True(steps[6] >= steps[5], $"{steps[5]:0.####} then {steps[6]:0.####}");

        // The same turn at once (Windows' animation effects off) ends at the same zoom.
        var wheel = new WheelZoom();
        var zoom = 1.0;
        foreach (var ms in new[] { 0, 48, 96 })
        {
            wheel.Turn(zoom, Notch, 0.1, 64, T0.AddMilliseconds(ms), animate: false);
            zoom = wheel.At(T0.AddMilliseconds(ms));
        }
        Assert.Equal(shown[^1], zoom, 9);
    }

    [Fact]
    public void Notches_in_a_row_or_apart_never_pass_their_target()
    {
        // Quick ones (a spin of the wheel, every 20 ms), and one back the other way during a notch: the zoom goes there
        // and stops, never past it.
        var spin = Frames(Enumerable.Range(0, 6).Select(i => (i * 20, Notch)).ToList(), 600);
        Assert.All(spin.Zip(spin.Skip(1)), p => Assert.True(p.Second >= p.First));
        Assert.Equal(Math.Pow(Notch, 6), spin[^1], 9);
        var back = Frames([(0, Notch), (64, 1 / Notch)], 400);
        var turned = back.IndexOf(back.Max());
        Assert.True(back.Max() < Notch);
        Assert.All(back.Skip(turned).Zip(back.Skip(turned + 1)), p => Assert.True(p.Second <= p.First));
        Assert.Equal(1, back[^1], 9);
        Assert.True(back.Min() >= 1 - 1e-9);
    }

    [Fact]
    public void A_notch_keeps_to_the_zoom_limits_as_the_camera_does()
    {
        var wheel = new WheelZoom();
        wheel.Turn(60, Notch, 0.5, 64, T0);
        Assert.Equal(64, wheel.Target, 9);
        Assert.All(Enumerable.Range(0, 12), k => Assert.InRange(wheel.At(T0.AddMilliseconds(k * FrameMs)), 60 - 1e-9, 64 + 1e-9));
        // At the limit, a notch further does nothing; a view outside the limits moves toward them or stays.
        wheel.Turn(64, Notch, 0.5, 64, T0.AddSeconds(1));
        Assert.Equal(64, wheel.At(T0.AddSeconds(1.05)), 9);
        // (A view put back from another map: the map's change stopped the wheel.)
        wheel.Stop();
        wheel.Turn(100, Notch, 0.5, 64, T0.AddSeconds(2));
        Assert.Equal(100, wheel.Target, 9);
        wheel.Stop();
        wheel.Turn(100, 1 / Notch, 0.5, 64, T0.AddSeconds(3));
        Assert.Equal(100 / Notch, wheel.Target, 9);

        // One landed but not stopped yet (no frame since) drives the view still: the next sets off from where it landed.
        var landed = new WheelZoom();
        landed.Turn(2, Notch, 0.5, 64, T0);
        landed.Turn(2.1, Notch, 0.5, 64, T0.AddMilliseconds(170));
        Assert.Equal(2 * Notch * Notch, landed.Target, 9);

        // A notch at the limit while one lands there, at speed: it goes on to the limit, slowing, and not past it.
        var landing = new WheelZoom();
        landing.Turn(50, Notch, 0.5, 60, T0);
        landing.Turn(landing.At(T0.AddMilliseconds(112)), Notch, 0.5, 60, T0.AddMilliseconds(112));
        Assert.Equal(60, landing.Target, 9);
        Assert.All(Enumerable.Range(0, 20), k => Assert.True(landing.At(T0.AddMilliseconds(112 + k * 8)) <= 60 + 1e-9));
    }

    [Fact]
    public void With_the_animation_effects_off_a_notch_is_at_once()
    {
        var wheel = new WheelZoom();
        wheel.Turn(2, Notch, 0.5, 64, T0, animate: false);
        Assert.True(wheel.Running);
        Assert.True(wheel.Over(T0));
        Assert.Equal(2 * Notch, wheel.At(T0), 9);
        // Stopped (another map, a fit, a jump), it has nothing more to show.
        wheel.Stop();
        Assert.False(wheel.Running);
        Assert.True(wheel.Over(T0));
    }

    // ---- + and −, the zoom buttons and a double-click (owner, 2026-10-15, evening; docs/DESIGN.md "Map drawing", *The
    // keys, the buttons and a double-click zoom over 0.2 s*): half again (1.5) or twice (2) the zoom over 0.2 s, eased,
    // as a notch is; until then at once. ----

    private const double Key = 1.5;

    private const double DoubleClick = 2;

    // The largest zoom from one frame to the next of a step from rest, frames every frameMs, however they fall in its time
    // (log scale).
    private static double LargestFrame(double factor, double frameMs)
    {
        var zoom = new WheelZoom();
        zoom.Turn(1, factor, 0.1, 64, T0, over: WheelZoom.Step);
        var largest = 0.0;
        for (var phase = 0.0; phase < frameMs; phase += frameMs / 20)
        {
            for (var ms = phase; ms < 200 + frameMs; ms += frameMs)
                largest = Math.Max(largest, Step(zoom.At(T0.AddMilliseconds(ms - frameMs)), zoom.At(T0.AddMilliseconds(ms))));
        }
        return largest;
    }

    [Theory]
    [InlineData(Key)]
    [InlineData(1 / Key)]
    [InlineData(DoubleClick)]
    public void A_step_reaches_its_zoom_in_0_2_s_eased_in_and_out_halfway_at_0_1_s(double factor)
    {
        Assert.Equal(TimeSpan.FromMilliseconds(200), WheelZoom.Step);
        var zoom = new WheelZoom();
        zoom.Turn(2, factor, 0.1, 64, T0, over: WheelZoom.Step);
        Assert.Equal(2 * factor, zoom.Target, 9);
        Assert.Equal(2, zoom.At(T0), 9);
        Assert.Equal(2 * Math.Sqrt(factor), zoom.At(T0.AddMilliseconds(100)), 9);
        // Slow at its start and its end: the first and last frames move it by little.
        Assert.True(Step(2, zoom.At(T0.AddMilliseconds(FrameMs))) < 0.1 * Math.Abs(Math.Log(factor)));
        Assert.True(Step(zoom.At(T0.AddMilliseconds(200 - FrameMs)), 2 * factor) < 0.1 * Math.Abs(Math.Log(factor)));
        Assert.False(zoom.Over(T0.AddMilliseconds(199)));
        Assert.True(zoom.Over(T0.AddMilliseconds(200)));
        Assert.Equal(2 * factor, zoom.At(T0.AddMilliseconds(200)), 9);
        Assert.Equal(2 * factor, zoom.At(T0.AddSeconds(5)), 9);
    }

    [Fact]
    public void A_steps_frames_zoom_by_less_than_a_tenth_at_60_frames_a_second_and_not_all_at_30()
    {
        // At 60 frames a second (16.7 ms; the window's timer asks for one every 15 ms) every frame of + or − zooms by 5.2 %
        // at most, of a double-click by 9 %: under the tenth past which the symbols are laid out afresh.
        Assert.InRange(Math.Exp(LargestFrame(Key, 1000 / 60.0)), 1.05, 1.053);
        Assert.InRange(Math.Exp(LargestFrame(DoubleClick, 1000 / 60.0)), 1.09, 1.091);
        Assert.True(LargestFrame(DoubleClick, FrameMs) < Steady);
        Assert.True(LargestFrame(1 / DoubleClick, 1000 / 60.0) < Steady);
        // At 30 frames a second the middle frames zoom by more (docs/DESIGN.md says so): + and − by up to 10.6 %, a
        // double-click by up to 18.7 %; those frames lay the symbols out afresh, as a zoom at once did. Under a tenth, + and
        // − keep down to about 32 frames a second, a double-click down to about 55.
        Assert.InRange(Math.Exp(LargestFrame(Key, 1000 / 30.0)), 1.105, 1.107);
        Assert.InRange(Math.Exp(LargestFrame(DoubleClick, 1000 / 30.0)), 1.186, 1.188);
        Assert.True(LargestFrame(Key, 1000 / 32.0) < Steady);
        Assert.True(LargestFrame(DoubleClick, 1000 / 55.0) < Steady);
    }

    [Fact]
    public void A_step_pressed_again_goes_on_from_the_first_and_lands_0_2_s_after_the_second()
    {
        // + twice, 100 ms apart: one zoom to 2.25 times, landing at 300 ms, without a stop, a turn back or a jump.
        var zoom = new WheelZoom();
        var shown = new List<double>();
        var z = 1.0;
        for (var ms = 0; ms <= 400; ms++)
        {
            if (ms is 0 or 100)
                zoom.Turn(z, Key, 0.1, 64, T0.AddMilliseconds(ms), over: WheelZoom.Step);
            if (zoom.Running)
                z = zoom.At(T0.AddMilliseconds(ms));
            if (ms % FrameMs == 0)
                shown.Add(z);
        }
        var steps = shown.Zip(shown.Skip(1), (a, b) => Math.Log(b / a)).ToList();
        Assert.All(steps.Take(300 / FrameMs), s => Assert.True(s > 0, $"a frame stood still or went back: {s}"));
        Assert.All(steps, s => Assert.True(s < Steady, $"a frame zoomed by {Math.Exp(s):0.###}"));
        Assert.Equal(Key * Key, zoom.Target, 9);
        Assert.False(zoom.Over(T0.AddMilliseconds(299)));
        Assert.True(zoom.Over(T0.AddMilliseconds(300)));
        Assert.Equal(Key * Key, shown[^1], 9);
        // Where the second press comes, the frame after it zooms at least as fast as the one before: it carries the speed.
        Assert.True(steps[100 / FrameMs] >= steps[100 / FrameMs - 1]);

        // A wheel's notch, then + during it: + goes on from the notch and lands 0.2 s after it, on both together.
        var mixed = new WheelZoom();
        mixed.Turn(1, Notch, 0.1, 64, T0);
        mixed.Turn(mixed.At(T0.AddMilliseconds(60)), Key, 0.1, 64, T0.AddMilliseconds(60), over: WheelZoom.Step);
        Assert.Equal(Notch * Key, mixed.Target, 9);
        Assert.False(mixed.Over(T0.AddMilliseconds(259)));
        Assert.True(mixed.Over(T0.AddMilliseconds(260)));
        Assert.All(Enumerable.Range(0, 20), k => Assert.True(mixed.At(T0.AddMilliseconds(60 + k * 10)) <= mixed.At(T0.AddMilliseconds(70 + k * 10)) + 1e-12));

        // − during +: back the other way, never past where it ends.
        var back = new WheelZoom();
        back.Turn(1, Key, 0.1, 64, T0, over: WheelZoom.Step);
        back.Turn(back.At(T0.AddMilliseconds(80)), 1 / Key, 0.1, 64, T0.AddMilliseconds(80), over: WheelZoom.Step);
        Assert.Equal(1, back.Target, 9);
        Assert.All(Enumerable.Range(0, 30), k => Assert.True(back.At(T0.AddMilliseconds(80 + k * 8)) >= 1 - 1e-9));
        Assert.Equal(1, back.At(T0.AddMilliseconds(280)), 9);
    }

    [Fact]
    public void A_step_keeps_to_the_zoom_limits_and_with_the_effects_off_is_at_once()
    {
        var zoom = new WheelZoom();
        zoom.Turn(40, DoubleClick, 0.5, 64, T0, over: WheelZoom.Step);
        Assert.Equal(64, zoom.Target, 9);
        Assert.All(Enumerable.Range(0, 14), k => Assert.InRange(zoom.At(T0.AddMilliseconds(k * FrameMs)), 40 - 1e-9, 64 + 1e-9));
        var outward = new WheelZoom();
        outward.Turn(0.6, 1 / Key, 0.5, 64, T0, over: WheelZoom.Step);
        Assert.Equal(0.5, outward.Target, 9);
        Assert.All(Enumerable.Range(0, 14), k => Assert.InRange(outward.At(T0.AddMilliseconds(k * FrameMs)), 0.5 - 1e-9, 0.6 + 1e-9));

        var off = new WheelZoom();
        off.Turn(2, DoubleClick, 0.5, 64, T0, animate: false, over: WheelZoom.Step);
        Assert.True(off.Over(T0));
        Assert.Equal(4, off.At(T0), 9);
        off.Turn(off.At(T0.AddMilliseconds(50)), 1 / Key, 0.5, 64, T0.AddMilliseconds(50), animate: false, over: WheelZoom.Step);
        Assert.Equal(4 / Key, off.At(T0.AddMilliseconds(50)), 9);

        Assert.Throws<ArgumentOutOfRangeException>(() => new WheelZoom().Turn(1, Key, 0.5, 64, T0, over: TimeSpan.Zero));
    }
}
