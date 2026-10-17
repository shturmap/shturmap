using Shturmap.Core;
using static Shturmap.Map.Tests.TestView;

namespace Shturmap.Map.Tests;

// A trader's held focus (owner, 2026-10-17, the trader strip under the MAP list): the trader's quests are drawn as the
// focus is while nothing is pointed at, the rest stepping back by kind, in a raid too, but they hold still; what the
// pointer is on goes before it while it lasts.
public class HeldFocusTests
{
    // Ragman's quests "a" and "b", Prapor's "c".
    private static (Camera Camera, MapScene Scene) Customs() => Of([
        Quest("a", -200, 0, "Ragman's A", group: "a"),
        Quest("b", 200, 0, "Ragman's B", group: "b"),
        Quest("c", 0, -200, "Prapor's C", group: "c"),
    ]);

    private static MapRenderer.ShownMarker Place(IReadOnlyList<MapRenderer.ShownMarker> shown, string group) => shown.Single(m => m.Marker.Group == group);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_held_focus_lights_its_quests_and_steps_the_others_back_without_a_pulse(bool inRaid)
    {
        var (camera, scene) = Customs();
        scene.InRaid = inRaid;
        scene.Held = new HashSet<string> { "a", "b" };
        scene.Dim = 1;
        Assert.True(scene.HasHighlight);
        Assert.False(scene.Pulsing);
        var shown = MapRenderer.Layout(camera, scene, 1).Markers;
        Assert.True(Place(shown, "a").Focused && Place(shown, "a").Selected);
        Assert.True(Place(shown, "b").Focused);
        Assert.False(Place(shown, "c").Focused);
    }

    [Fact]
    public void What_the_pointer_is_on_goes_before_the_held_focus_and_hands_back_to_it()
    {
        var (camera, scene) = Customs();
        scene.Held = new HashSet<string> { "a", "b" };
        scene.Focus = new HashSet<string> { "c" };
        scene.Dim = 1;
        Assert.True(scene.Pulsing);
        var pointed = MapRenderer.Layout(camera, scene, 1).Markers;
        Assert.True(Place(pointed, "c").Focused);
        Assert.False(Place(pointed, "a").Focused);

        // The pointer leaves: the held focus is back at once, nothing fades out between.
        scene.Focus = new HashSet<string>();
        Assert.Equal(["a", "b"], scene.ShownFocus.Order());
        Assert.False(scene.Pulsing);
        Assert.True(scene.HasHighlight);
    }

    [Fact]
    public void Let_go_the_held_focus_fades_out_as_a_pointed_one_does()
    {
        var (_, scene) = Customs();
        scene.Held = new HashSet<string> { "a" };
        scene.Dim = 1;
        scene.Held = new HashSet<string>();
        Assert.False(scene.HasHighlight);
        // While the dim eases back, what was held stays drawn as the focus; then nothing is.
        Assert.Equal(["a"], scene.ShownFocus);
        scene.Dim = 0;
        Assert.Empty(scene.ShownFocus);
    }

    [Fact]
    public void A_held_focus_with_nothing_on_this_map_steps_nothing_back()
    {
        var (_, scene) = Customs();
        scene.Held = new HashSet<string> { "elsewhere" };
        scene.Dim = 1;
        Assert.False(scene.HasHighlight);
        Assert.Empty(scene.ShownFocus.Intersect(["a", "b", "c"]));
    }

    [Fact]
    public void Picks_keep_their_strength_under_a_held_focus()
    {
        var (camera, scene) = Customs();
        scene.Kept = new HashSet<string> { "c" };
        scene.Held = new HashSet<string> { "a" };
        scene.Dim = 1;
        var shown = MapRenderer.Layout(camera, scene, 1).Markers;
        Assert.Equal(1f, MapRenderer.StepBackOf(scene, Place(shown, "c")).Alpha);
        Assert.True(MapRenderer.StepBackOf(scene, Place(shown, "b")).Alpha < 1f);
    }
}
