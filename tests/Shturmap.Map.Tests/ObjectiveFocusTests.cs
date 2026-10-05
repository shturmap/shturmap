using Shturmap.Core;
using static Shturmap.Map.Tests.TestView;

namespace Shturmap.Map.Tests;

// The linked highlight down to the objective (the review of 2026-10-04): pointing at one objective's line keeps its
// whole quest lit and marks the objective within it. On the map only that objective's places pulse and take the
// pointed-at size; the quest's other places stay lit and hold still.
public class ObjectiveFocusTests
{
    // Quest "a" with two objectives ("plant" in two places, "find"), and another quest's place.
    private static (Camera Camera, MapScene Scene) Streets() => Of([
        Quest("plant", -200, 0, "Plant here", group: "a"),
        new MapMarker("objective:plant:2", MarkerKind.Objective, new WorldPoint(-200, 0, 200), "Or here", "a", Shturmap.Core.Quests.ObjectiveKind.Exploration),
        Quest("find", 200, 0, "Find it", group: "a"),
        Quest("other", 0, -200, "Elsewhere", group: "b"),
    ]);

    private static MapRenderer.ShownMarker Place(IReadOnlyList<MapRenderer.ShownMarker> shown, string id) => shown.Single(m => m.Marker.Id == id);

    [Fact]
    public void Pointing_at_a_quest_every_place_of_it_pulses()
    {
        var (camera, scene) = Streets();
        scene.Focus = new HashSet<string> { "a" };
        scene.Dim = 1;
        var shown = MapRenderer.Layout(camera, scene, 1).Markers;
        Assert.All(shown.Where(m => m.Marker.Group == "a"), m => Assert.True(m.Pointed && m.Focused && m.Selected && m.R == 10));
        Assert.False(Place(shown, "objective:other:1").Pointed);
    }

    [Fact]
    public void Pointing_at_an_objective_only_its_places_pulse_and_the_quests_others_stay_lit_and_still()
    {
        var (camera, scene) = Streets();
        scene.Focus = new HashSet<string> { "a" };
        scene.FocusObjective = "plant";
        scene.Dim = 1;
        var layout = MapRenderer.Layout(camera, scene, 1);
        var shown = layout.Markers;

        // Both places of the objective: their names bold and the pulse, at their rest size (owner, 2026-10-05: "Go for F").
        foreach (var id in new[] { "objective:plant:1", "objective:plant:2" })
        {
            var place = Place(shown, id);
            Assert.True(place.Pointed);
            Assert.True(place.Selected);
            Assert.Equal(10, place.R);
            Assert.True(layout.Labels.Single(l => l.Of == place).Bold);
        }

        // The quest's other objective: still in the focus (full strength, drawn over what stepped back), but at its
        // rest size, its name as at rest, and no pulse.
        var other = Place(shown, "objective:find:1");
        Assert.True(other.Focused);
        Assert.False(other.Pointed);
        Assert.False(other.Selected);
        Assert.Equal(10, other.R);
        Assert.False(layout.Labels.Single(l => l.Of == other).Bold);

        // Another quest steps back as with any focus.
        var elsewhere = Place(shown, "objective:other:1");
        Assert.False(elsewhere.Focused);
        Assert.False(elsewhere.Pointed);
    }

    [Fact]
    public void An_objective_with_no_place_here_leaves_its_quest_lit_and_nothing_pulsing()
    {
        // "Kill 5 Scavs" has no marker: pointing at its line still lights the quest's places, and none of them pulses.
        var (camera, scene) = Streets();
        scene.Focus = new HashSet<string> { "a" };
        scene.FocusObjective = "kills";
        scene.Dim = 1;
        var shown = MapRenderer.Layout(camera, scene, 1).Markers;
        Assert.All(shown.Where(m => m.Marker.Group == "a"), m => Assert.True(m.Focused && !m.Pointed && m.R == 10));
    }

    [Fact]
    public void A_done_place_of_the_objective_pointed_at_keeps_its_quiet_look()
    {
        var (camera, scene) = Of([Quest("plant", 0, 0, "Plant here", MarkerKind.ObjectiveDone, group: "a"), Quest("find", 200, 0, "Find it", group: "a")]);
        scene.Focus = new HashSet<string> { "a" };
        scene.FocusObjective = "plant";
        scene.Dim = 1;
        var done = MapRenderer.Layout(camera, scene, 1).Markers.Single(m => m.Marker.Kind == MarkerKind.ObjectiveDone);
        Assert.False(done.Pointed);
        Assert.Equal(10, done.R);
    }

    [Fact]
    public void The_door_of_the_quests_key_keeps_its_look_and_holds_still()
    {
        // A lock belongs to no objective: with an objective pointed at it stays shown and named (at rest it wouldn't
        // show from far out), but only the objective's own places pulse.
        var door = new MapMarker("lock:1", MarkerKind.Lock, new WorldPoint(60, 0, 60), "Dorm 114", MapContentBuilder.KeyGroup("key-1"));
        var (camera, scene) = Of([Quest("plant", -200, 0, "Plant here", group: "a"), door]);
        scene.QuestKeys = new Dictionary<string, IReadOnlyList<string>> { ["a"] = [MapContentBuilder.KeyGroup("key-1")] };
        scene.Focus = new HashSet<string> { "a" };
        scene.Dim = 1;
        var whole = MapRenderer.Layout(camera, scene, 1).Markers.Single(m => m.Marker == door);
        Assert.True(whole.Pointed && whole.Selected);

        scene.FocusObjective = "plant";
        var within = MapRenderer.Layout(camera, scene, 1).Markers.Single(m => m.Marker == door);
        Assert.True(within.Selected);
        Assert.True(within.Focused);
        Assert.False(within.Pointed);
        Assert.Equal(whole.R, within.R);
    }

    [Fact]
    public void While_the_highlight_fades_out_the_objectives_places_keep_their_emphasis_and_every_symbol_its_size()
    {
        var (camera, scene) = Streets();
        scene.Focus = new HashSet<string> { "a" };
        scene.FocusObjective = "plant";
        scene.Dim = 1;
        // The pointer left: the focus is gone, the dimming still easing back.
        scene.Focus = new HashSet<string>();
        scene.FocusObjective = null;
        scene.Dim = 0.5f;
        var shown = MapRenderer.Layout(camera, scene, 1).Markers;
        Assert.True(Place(shown, "objective:plant:1").Selected);
        Assert.Equal(10, Place(shown, "objective:plant:1").R);
        Assert.Equal(10, Place(shown, "objective:find:1").R);
        Assert.False(scene.Pulsing);
        // Faded out: everything at rest.
        scene.Dim = 0;
        Assert.All(MapRenderer.Layout(camera, scene, 1).Markers, m => Assert.Equal(10, m.R));
    }

    [Fact]
    public void Moving_to_another_objective_of_the_same_quest_starts_the_pulse_anew()
    {
        var (_, scene) = Streets();
        scene.Focus = new HashSet<string> { "a" };
        scene.FocusObjective = "plant";
        var since = scene.FocusSince;
        Thread.Sleep(20);
        // The same quest again (the next line of its block): the focus set is the same, the objective another.
        scene.Focus = new HashSet<string> { "a" };
        Assert.Equal(since, scene.FocusSince);
        scene.FocusObjective = "find";
        Assert.True(scene.FocusSince > since);
    }

    [Fact]
    public void Only_the_chevrons_of_the_objective_pointed_at_show_where_its_places_lie_out_of_view()
    {
        // Both objectives' places are out of view, in different directions.
        var (camera, scene) = Of([Quest("plant", 900, 0, "Plant here", group: "a"), Quest("find", -900, 0, "Find it", group: "a")]);
        scene.Focus = new HashSet<string> { "a" };
        scene.Dim = 1;
        Assert.Equal(2, MapRenderer.Layout(camera, scene, 1).Chevrons.Count);
        scene.FocusObjective = "plant";
        var chevron = Assert.Single(MapRenderer.Layout(camera, scene, 1).Chevrons);
        Assert.True(chevron.At.X > 500, "the chevron points to the objective's place, on the right");
    }

    [Fact]
    public void Only_the_zone_of_the_objective_pointed_at_takes_the_pointed_at_look()
    {
        static MapZone Zone(string objective, double x) => new($"zone:{objective}:1", MarkerKind.Objective,
            [new WorldPoint(x, 0, 100), new WorldPoint(x + 40, 0, 100), new WorldPoint(x + 40, 0, 140), new WorldPoint(x, 0, 140)], "a");
        var (camera, scene) = Streets();
        scene.Zones = [Zone("plant", -100), Zone("find", 60)];
        scene.Focus = new HashSet<string> { "a" };
        scene.FocusObjective = "plant";
        scene.Dim = 1;
        scene.Pulse = false;
        // Both zones are the quest's and stay at full strength.
        Assert.All(scene.Zones, z => Assert.Equal(1f, MapRenderer.ZoneStrength(scene, z)));

        using var bitmap = new SkiaSharp.SKBitmap(new SkiaSharp.SKImageInfo(1000, 1000, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul));
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        MapRenderer.Render(canvas, camera, scene);
        // Inside each zone, clear of the sheet's grid lines: the pointed-at objective's is filled twice as strongly.
        var pointed = bitmap.GetPixel(500 - 100 + 17, 500 - 117);
        var other = bitmap.GetPixel(500 + 60 + 17, 500 - 117);
        Assert.True(pointed.Red > other.Red + 10, $"pointed-at zone {pointed}, the quest's other zone {other}");

        // Pointing at the quest as a whole, both take that look.
        scene.FocusObjective = null;
        MapRenderer.Render(canvas, camera, scene);
        var whole = bitmap.GetPixel(500 + 60 + 17, 500 - 117);
        Assert.True(Math.Abs(whole.Red - pointed.Red) <= 3, $"with the whole quest pointed at {whole}, the pointed-at zone before {pointed}");
    }
}
