using Shturmap.Core;
using Shturmap.Data.TarkovDev;
using static Shturmap.Map.Tests.Fixtures;

namespace Shturmap.Map.Tests;

// A picked or pointed-at quest lights the doors of the keys it needs on its map (owner, 2026-10-03: "For the Golden
// Swag key the trailer park portable cabin marker is not highlighted when the quest is highlighted as goal quest").
public class QuestKeyTests
{
    private static readonly string Here = MapContentBuilder.KeyGroup("k-here");
    private static readonly string Objective = MapContentBuilder.KeyGroup("k-objective");
    private static readonly string Elsewhere = MapContentBuilder.KeyGroup("k-elsewhere");
    private static readonly string Unneeded = MapContentBuilder.KeyGroup("k-unneeded");

    private static MapMarker Lock(string group, double x) => new($"lock:{group}", MarkerKind.Lock, new WorldPoint(x, 0, 0), "Door", group);

    [Fact]
    public void A_quest_brings_the_keys_it_needs_on_this_map_and_no_other()
    {
        var map = TestMap("customs");
        var objective = new ApiObjective("o1", "findQuestItem", "Locate the lighter", false, ["map-1"],
            [new ApiZone(null, null, "map-1", At(10, 0), null, null, null)], null, null, null, null, null, null, [["k-objective"]], false);
        var quest = new ApiTask("q", "Golden Swag", null, null, "map-1", null, false, false, null, null, null, false, null, [objective],
            [new ApiNeededKeys("map-1", ["k-here"]), new ApiNeededKeys("map-2", ["k-elsewhere"])]);
        var plain = With(map);
        var data = new GameData
        {
            Mode = plain.Mode, Language = "en", Maps = plain.Maps, Traders = plain.Traders, MapDefinitions = plain.MapDefinitions,
            CheckedAt = plain.CheckedAt, Tasks = new Dictionary<string, ApiTask> { ["q"] = quest },
        };
        var keys = MapContentBuilder.Build(data, "map-1", ["q"], new HashSet<string>()).QuestKeys;
        Assert.Equal([Here, Objective], keys["q"].Order());
        Assert.DoesNotContain(Elsewhere, keys["q"]);
    }

    private static (Camera Camera, MapScene Scene) Picked(params MapMarker[] markers)
    {
        var (camera, scene) = TestView.Of(markers);
        scene.QuestKeys = new Dictionary<string, IReadOnlyList<string>> { ["quest-a"] = [Here] };
        return (camera, scene);
    }

    [Fact]
    public void A_picked_quest_lights_the_locks_of_its_keys_in_the_picks_colour()
    {
        var (camera, scene) = Picked(TestView.Quest("a", 0, 0, "Golden Swag"), Lock(Here, 60), Lock(Unneeded, -60));
        scene.Kept = new HashSet<string> { "quest-a" };
        var shown = MapRenderer.ShownMarkers(camera, scene, 1);
        var needed = shown.Single(m => m.Marker.Group == Here);
        Assert.True(needed.Selected);
        Assert.Equal(MapRenderer.Kept, needed.Color);
        var other = shown.Single(m => m.Marker.Group == Unneeded);
        Assert.False(other.Selected);
        Assert.NotEqual(MapRenderer.Kept, other.Color);
        // The door's name shows with it, as a pointed-at lock's does.
        Assert.Contains(MapRenderer.Layout(camera, scene, 1).Labels, l => l.Text == "Door" && l.Of.Marker.Group == Here);
    }

    [Fact]
    public void Pointing_at_a_quest_lights_the_locks_of_its_keys()
    {
        var (camera, scene) = Picked(TestView.Quest("a", 0, 0, "Golden Swag"), Lock(Here, 60), Lock(Unneeded, -60));
        scene.Focus = new HashSet<string> { "quest-a" };
        Assert.Contains(Here, scene.ShownFocus);
        var shown = MapRenderer.ShownMarkers(camera, scene, 1);
        Assert.True(shown.Single(m => m.Marker.Group == Here).Selected);
        Assert.False(shown.Single(m => m.Marker.Group == Unneeded).Selected);
    }

    [Fact]
    public void Keys_set_after_the_pick_still_light_its_locks()
    {
        // The content (and with it the keys) can arrive after the picks: the scene catches up.
        var (camera, scene) = TestView.Of([TestView.Quest("a", 0, 0, "Golden Swag"), Lock(Here, 60)]);
        scene.Kept = new HashSet<string> { "quest-a" };
        scene.Focus = new HashSet<string> { "quest-a" };
        scene.QuestKeys = new Dictionary<string, IReadOnlyList<string>> { ["quest-a"] = [Here] };
        Assert.Contains(Here, scene.KeptKeys);
        Assert.Contains(Here, scene.ShownFocus);
        Assert.True(MapRenderer.ShownMarkers(camera, scene, 1).Single(m => m.Marker.Group == Here).Selected);
    }

    [Fact]
    public void The_guide_leads_to_the_quests_place_never_to_a_door_it_needs()
    {
        // The door is 10 m from the player, the quest's place 150 m: the guide still goes to the place.
        var (camera, scene) = Picked(TestView.Quest("a", 150, 0, "Golden Swag"), Lock(Here, 10));
        scene.Kept = new HashSet<string> { "quest-a" };
        scene.Player = new PlayerFix(new WorldPoint(0, 0, 0), 0, DateTime.Now);
        var guide = MapRenderer.Layout(camera, scene, 1).Guide;
        Assert.NotNull(guide);
        Assert.Equal(150, guide.Metres, 1);
    }
}
