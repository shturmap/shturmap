using SkiaSharp;
using static Shturmap.Map.Tests.TestView;

namespace Shturmap.Map.Tests;

// The linked highlight breathes (owner, 2026-10-17: it "really helps pre-attentively highlight elements … However, the
// pre-attentiveness goes away directly after the highlight was done … the highlights are a bit too faint"; from a panel
// of four, "B"; docs/DESIGN.md §4, *Motion*): while the pointer lights a linked set, its places' halos breathe in phase
// and the rest steps back a little further; the breath stops to a static look (a ring on each collar, the rest further
// back) when a click holds it, when the pointer rests 6 s, and with the animation effects off. One quest pointed at keeps
// its rings.
public class BreathTests
{
    // The day every example uses.
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Local);

    private static readonly IReadOnlySet<string> Ragman = new HashSet<string> { "quest-a", "quest-b" };

    private static DateTime At(double seconds) => T0 + TimeSpan.FromSeconds(seconds);

    // ---- the curve ----

    [Theory]
    [InlineData(0, 0.92f, 0.85f)]
    [InlineData(0.5, 1f, 0.925f)]
    [InlineData(1, 1.08f, 1f)]
    [InlineData(1.5, 1f, 0.925f)]
    [InlineData(2, 0.92f, 0.85f)]
    public void The_breath_is_an_eased_sine_back_and_forth_over_two_seconds(double seconds, float scale, float opacity)
    {
        var (s, o) = LinkedBreath.At(TimeSpan.FromSeconds(seconds));
        Assert.Equal(scale, s, 4);
        Assert.Equal(opacity, o, 4);
    }

    [Fact]
    public void It_starts_once_the_set_has_lit()
    {
        var breath = new LinkedBreath();
        breath.Light(Ragman, T0);
        // Through the 0.18 s ease-in it stands at its start; then its curve runs.
        Assert.Equal(LinkedBreath.At(TimeSpan.Zero), breath.Frame(At(0.1)));
        Assert.Equal(1f, breath.Frame(At(0.18 + 0.5)).Scale, 4);
        Assert.Equal(1.08f, breath.Frame(At(0.18 + 1)).Scale, 4);
        Assert.Equal(0f, breath.Settle(At(1)));
        Assert.True(breath.Running(At(1)));
    }

    [Fact]
    public void The_same_set_again_goes_on_breathing_and_another_starts_anew()
    {
        var breath = new LinkedBreath();
        breath.Light(Ragman, T0);
        breath.Light(new HashSet<string> { "quest-b", "quest-a" }, At(1));
        Assert.Equal(T0, breath.Since);
        breath.Light(new HashSet<string> { "boss-kaban" }, At(2));
        Assert.Equal(At(2), breath.Since);
        breath.Light(null, At(3));
        Assert.False(breath.Lit);
        Assert.False(breath.Running(At(3)));
    }

    // ---- the stops ----

    [Fact]
    public void A_click_that_holds_the_set_stops_the_breath_over_the_ease()
    {
        var breath = new LinkedBreath();
        breath.Light(Ragman, T0);
        breath.Hold(true, At(1));
        Assert.Equal(0.5f, breath.Settle(At(1.09)), 2);
        Assert.Equal(1f, breath.Settle(At(1.18)));
        Assert.False(breath.Running(At(1.2)));
        // Held, a move of the pointer doesn't start it.
        Assert.False(breath.Moved(At(10)));
        Assert.Equal(1f, breath.Settle(At(10)));
        // Let go under the pointer, it breathes again, easing back in from the static look.
        breath.Hold(false, At(11));
        Assert.Equal(1f, breath.Settle(At(11)));
        Assert.Equal(0f, breath.Settle(At(11.18)));
        Assert.True(breath.Running(At(11.2)));
    }

    [Fact]
    public void A_set_held_before_the_pointer_lit_it_never_breathes()
    {
        var breath = new LinkedBreath();
        breath.Light(Ragman, T0);
        breath.Hold(true, T0);
        Assert.Equal(1f, breath.Settle(T0));
        Assert.False(breath.Running(T0));
    }

    [Fact]
    public void After_six_seconds_of_a_still_pointer_the_breath_stops_and_a_move_starts_it_again()
    {
        var breath = new LinkedBreath();
        breath.Light(Ragman, T0);
        Assert.Equal(0f, breath.Settle(At(5.9)));
        Assert.Equal(0.5f, breath.Settle(At(6.09)), 2);
        Assert.Equal(1f, breath.Settle(At(6.18)));
        Assert.False(breath.Running(At(6.2)));
        // A move after it stopped starts it again, from the start of its curve after the ease.
        Assert.True(breath.Moved(At(12)));
        Assert.Equal(At(12), breath.Since);
        Assert.Equal(1f, breath.Settle(At(12)));
        Assert.Equal(0f, breath.Settle(At(12.18)));
        Assert.Equal(LinkedBreath.At(TimeSpan.Zero), breath.Frame(At(12.18)));
        Assert.True(breath.Running(At(13)));
    }

    [Fact]
    public void A_move_before_the_six_seconds_puts_the_stop_off()
    {
        var breath = new LinkedBreath();
        breath.Light(Ragman, T0);
        Assert.False(breath.Moved(At(4)));
        Assert.Equal(0f, breath.Settle(At(9.9)));
        Assert.Equal(1f, breath.Settle(At(10.18)));
    }

    [Fact]
    public void With_the_animation_effects_off_it_never_starts()
    {
        var breath = new LinkedBreath { Effects = false };
        breath.Light(Ragman, T0);
        Assert.Equal(1f, breath.Settle(T0));
        Assert.Equal(1f, breath.Settle(At(1)));
        Assert.False(breath.Running(T0));
        Assert.False(breath.Moved(At(7)));
    }

    // ---- the strengths ----

    [Theory]
    [InlineData(false, 0f, 0.55f, 0.4f)]
    [InlineData(false, 1f, 0.4f, 0.3f)]
    [InlineData(true, 0f, 0.7f, 0.5f)]
    [InlineData(true, 1f, 0.6f, 0.45f)]
    public void A_set_steps_the_rest_back_further_breathing_and_further_still_when_static(bool inRaid, float settle, float alpha, float labels)
    {
        var step = MapRenderer.StepBackOf(inRaid, settle);
        Assert.Equal(alpha, step.Alpha, 4);
        Assert.Equal(labels, step.LabelAlpha, 4);
        Assert.Equal(1f, step.Saturation);
        // Never out of sight: the owner found 28 % too dark (2026-10-03).
        Assert.True(step.LabelAlpha > 0.28f);
    }

    private static (Camera Camera, MapScene Scene) Customs() => Of([
        Quest("a", -200, 0, "Ragman's A"),
        Quest("b", 200, 0, "Ragman's B"),
        Quest("c", 0, -200, "Prapor's C"),
        new MapMarker("extract:e", MarkerKind.ExtractPmc, new Shturmap.Core.WorldPoint(0, 0, 200), "Exit"),
    ]);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_map_takes_the_sets_measures_and_one_quest_pointed_at_keeps_its_own(bool inRaid)
    {
        var (_, scene) = Customs();
        scene.InRaid = inRaid;
        scene.Dim = 1;

        // One quest pointed at: the plain table, as before.
        scene.Focus = new HashSet<string> { "quest-c" };
        Assert.False(scene.ShowsSet);
        Assert.Equal(MapRenderer.StepBackOf(MarkerKind.Objective, inRaid), MapRenderer.MeasuresAt(scene, At(1))(MarkerKind.Objective, inRaid));

        // A set the pointer lights, breathing: a little further; the ways out keep theirs.
        scene.Breath.Light(Ragman, T0);
        scene.LinkedSet = true;
        scene.Focus = Ragman;
        Assert.True(scene.ShowsSet);
        Assert.Equal(MapRenderer.StepBackOf(inRaid, 0), MapRenderer.MeasuresAt(scene, At(1))(MarkerKind.Objective, inRaid));
        Assert.Equal(MapRenderer.StepBackOf(inRaid, 0), MapRenderer.MeasuresAt(scene, At(1))(MarkerKind.Lock, inRaid));
        Assert.Equal(MapRenderer.StepBackOf(MarkerKind.ExtractPmc, inRaid), MapRenderer.MeasuresAt(scene, At(1))(MarkerKind.ExtractPmc, inRaid));

        // The pointer still for 6 s: static, further back.
        Assert.Equal(MapRenderer.StepBackOf(inRaid, 1), MapRenderer.MeasuresAt(scene, At(7))(MarkerKind.Objective, inRaid));

        // Held, nothing pointed at: static from the start.
        scene.Breath.Light(null, At(8));
        scene.LinkedSet = false;
        scene.Focus = new HashSet<string>();
        scene.Held = Ragman;
        Assert.True(scene.ShowsSet);
        Assert.Equal(1f, scene.SettleAt(At(8)));
        Assert.Equal(MapRenderer.StepBackOf(inRaid, 1), MapRenderer.MeasuresAt(scene, At(8))(MarkerKind.Objective, inRaid));
    }

    // ---- what the renderer draws ----

    // One frame at a time, as shturmap-cli frames draws it: the trace says what the frame drew of each symbol.
    private static Dictionary<string, SymbolTrace> Frame(Camera camera, MapScene scene, DateTime at, SKBitmap? into = null)
    {
        using var bitmap = into is null ? new SKBitmap(1000, 1000) : null;
        var trace = new List<SymbolTrace>();
        using (var canvas = new SKCanvas(into ?? bitmap!))
            MapRenderer.Render(canvas, camera, scene, 1, null, at, trace);
        return trace.ToDictionary(t => t.Id);
    }

    [Fact]
    public void One_quest_pointed_at_still_rings_and_doesnt_breathe()
    {
        var (camera, scene) = Customs();
        scene.Breath.Light(null, T0);
        scene.Focus = new HashSet<string> { "quest-a" };
        scene.Dim = 1;
        Assert.True(scene.Pulsing);
        Assert.False(scene.BreathingAt(At(1)));
        var frame = Frame(camera, scene, At(1));
        Assert.Null(frame["objective:a:1"].Halo);
        Assert.Null(frame["objective:a:1"].Ring);

        // The same quest as a trader's one quest, a set: it breathes, and doesn't ring.
        scene.Breath.Light(new HashSet<string> { "quest-a" }, T0);
        scene.LinkedSet = true;
        Assert.False(scene.Pulsing);
        Assert.True(scene.BreathingAt(At(1)));
        Assert.NotNull(Frame(camera, scene, At(1))["objective:a:1"].Halo);
    }

    [Fact]
    public void A_lit_symbols_halo_changes_between_two_frames_a_second_apart_while_breathing_and_not_when_static()
    {
        var (camera, scene) = Customs();
        scene.ShowLabels = false;
        scene.Breath.Light(Ragman, T0);
        scene.LinkedSet = true;
        scene.Focus = Ragman;
        scene.Dim = 1;

        // Breathing: at the start of its curve and half a cycle on, out and in.
        using var first = new SKBitmap(1000, 1000);
        using var second = new SKBitmap(1000, 1000);
        var (low, high) = (Frame(camera, scene, At(0.18), first), Frame(camera, scene, At(1.18), second));
        var (a, b) = (low["objective:a:1"], high["objective:a:1"]);
        Assert.NotNull(a.Halo);
        Assert.NotNull(b.Halo);
        Assert.Equal(LinkedBreath.HighScale / LinkedBreath.LowScale, b.Halo!.Value.Radius / a.Halo!.Value.Radius, 3);
        Assert.True(b.Halo.Value.Alpha > a.Halo.Value.Alpha);
        // About three times the symbol's radius at its widest.
        Assert.Equal(3 * b.R, b.Halo.Value.Radius, 0);
        // Both lit places breathe alike, in phase; the rest has none, and no lit symbol rings its collar yet.
        Assert.Equal(b.Halo, high["objective:b:1"].Halo);
        Assert.Null(high["objective:c:1"].Halo);
        Assert.Null(b.Ring);
        // The picture shows it: just inside the wider halo's edge, above the symbol, the gold is there only when it is wide.
        var edge = new SKPoint(b.At.X, b.At.Y - (a.Halo.Value.Radius + b.Halo.Value.Radius) / 2);
        Assert.NotEqual(first.GetPixel((int)edge.X, (int)edge.Y), second.GetPixel((int)edge.X, (int)edge.Y));

        // Still for 6 s: the static look, the same a second apart, a ring on each lit collar and no halo.
        var (still, later) = (Frame(camera, scene, At(7)), Frame(camera, scene, At(8)));
        Assert.Null(still["objective:a:1"].Halo);
        Assert.Equal(1f, still["objective:a:1"].Ring);
        Assert.Equal(still["objective:a:1"].Ring, later["objective:a:1"].Ring);
        Assert.Null(later["objective:a:1"].Halo);
        Assert.Null(later["objective:c:1"].Ring);
    }

    [Fact]
    public void A_held_focus_stands_in_the_static_look_without_motion()
    {
        var (camera, scene) = Customs();
        scene.Held = Ragman;
        scene.Dim = 1;
        Assert.True(scene.ShowsSet);
        Assert.False(scene.BreathingAt(At(1)));
        var (one, two) = (Frame(camera, scene, At(1)), Frame(camera, scene, At(2)));
        Assert.Null(one["objective:a:1"].Halo);
        Assert.Equal(1f, one["objective:a:1"].Ring);
        Assert.Equal(one["objective:b:1"].Ring, two["objective:b:1"].Ring);
        Assert.Null(two["objective:a:1"].Halo);
    }
}
