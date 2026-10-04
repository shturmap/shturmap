using Shturmap.Core;
using Shturmap.Data.TarkovDev;
using static Shturmap.Map.Tests.Fixtures;

namespace Shturmap.Map.Tests;

// While a quest is highlighted, the rest steps back by kind, never out of sight, and less in a raid (owner,
// 2026-10-03: at 28 % the other markers "can be barely made out anymore, but are still pretty important").
public class FocusTests
{
    private static readonly MarkerKind[] WaysOutAndBosses =
        [MarkerKind.ExtractPmc, MarkerKind.ExtractScav, MarkerKind.ExtractShared, MarkerKind.Transit, MarkerKind.BossSpawn];

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Ways_out_and_bosses_never_step_back(bool inRaid) =>
        Assert.All(WaysOutAndBosses, kind =>
        {
            var step = MapRenderer.StepBackOf(kind, inRaid);
            Assert.Equal(1f, step.Alpha);
            Assert.Equal(1f, step.Saturation);
        });

    [Fact]
    public void Nothing_steps_back_out_of_sight_and_colours_stay()
    {
        foreach (var kind in Enum.GetValues<MarkerKind>())
        foreach (var inRaid in new[] { true, false })
        {
            var step = MapRenderer.StepBackOf(kind, inRaid);
            Assert.True(step.Alpha >= 0.6f, $"{kind} in raid {inRaid}: {step.Alpha}");
            // Greying a quest marker would make it read as done (grey means done, after the raid).
            Assert.Equal(1f, step.Saturation);
        }
    }

    [Theory]
    [InlineData(MarkerKind.Objective)]
    [InlineData(MarkerKind.PossibleLocation)]
    [InlineData(MarkerKind.ScavSpawn)]
    public void Other_quests_step_back_less_in_a_raid_and_their_labels_more_than_their_symbols(MarkerKind kind)
    {
        var raid = MapRenderer.StepBackOf(kind, inRaid: true);
        var plan = MapRenderer.StepBackOf(kind, inRaid: false);
        Assert.True(raid.Alpha > plan.Alpha);
        Assert.True(raid.LabelAlpha < raid.Alpha);
        Assert.True(plan.LabelAlpha < plan.Alpha);
    }

    // ---- zones, hazards and the picks' doors follow the same table (the review of 2026-10-04: zones fell to about
    // 35 % whatever their markers kept, hazards had a measure of their own, and a pick's doors stepped back) ----

    private static MapZone Zone(string id, MarkerKind kind, string? group, double x) =>
        new(id, kind, [new WorldPoint(x, 0, 0), new WorldPoint(x + 20, 0, 0), new WorldPoint(x + 20, 0, 20), new WorldPoint(x, 0, 20)], group);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Another_quests_zone_steps_back_as_far_as_its_markers(bool inRaid)
    {
        var (_, scene) = TestView.Of([TestView.Quest("a", 0, 0, "A", group: "a"), TestView.Quest("b", 100, 0, "B", group: "b"),
            TestView.Quest("c", 200, 0, "C", group: "c")]);
        var (pointedAt, other, picked) = (Zone("zone:a:1", MarkerKind.Objective, "a", 0), Zone("zone:b:1", MarkerKind.Objective, "b", 100),
            Zone("zone:c:1", MarkerKind.Objective, "c", 200));
        scene.Zones = [pointedAt, other, picked];
        scene.InRaid = inRaid;
        scene.Kept = new HashSet<string> { "c" };

        // Nothing pointed at: every zone at full strength.
        Assert.Equal(1f, MapRenderer.ZoneStrength(scene, other));

        scene.Focus = new HashSet<string> { "a" };
        scene.Dim = 1;
        var markers = MapRenderer.StepBackOf(MarkerKind.Objective, inRaid).Alpha;
        Assert.Equal(inRaid ? 0.8f : 0.62f, markers);
        Assert.Equal(markers, MapRenderer.ZoneStrength(scene, other), 5);
        Assert.Equal(1f, MapRenderer.ZoneStrength(scene, pointedAt));
        Assert.Equal(1f, MapRenderer.ZoneStrength(scene, picked));
        // Half-way through the ease, half-way back.
        scene.Dim = 0.5f;
        Assert.Equal(1 - (1 - markers) / 2, MapRenderer.ZoneStrength(scene, other), 5);
    }

    [Theory]
    [InlineData(true, 0.75f, 0.6f)]
    [InlineData(false, 0.6f, 0.5f)]
    public void Hazard_areas_step_back_like_the_zones_locks_and_switches(bool inRaid, float alpha, float labels)
    {
        var step = MapRenderer.StepBackOf(MarkerKind.Hazard, inRaid);
        Assert.Equal(MapRenderer.StepBackOf(MarkerKind.Lock, inRaid), step);
        Assert.Equal((alpha, labels), (step.Alpha, step.LabelAlpha));

        var (_, scene) = TestView.Of([TestView.Quest("a", 0, 0, "A", group: "a")]);
        var minefield = Zone("minefield:1", MarkerKind.Hazard, MapContentBuilder.MinefieldGroup, 100);
        scene.Zones = [minefield];
        scene.InRaid = inRaid;
        Assert.Equal(1f, MapRenderer.ZoneStrength(scene, minefield));
        scene.Focus = new HashSet<string> { "a" };
        scene.Dim = 1;
        Assert.Equal(alpha, MapRenderer.ZoneStrength(scene, minefield), 5);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_picked_quests_doors_dont_step_back(bool inRaid)
    {
        var door = new MapMarker("lock:1", MarkerKind.Lock, new WorldPoint(40, 0, 0), "Dorm 114", MapContentBuilder.KeyGroup("key-1"));
        var otherDoor = new MapMarker("lock:2", MarkerKind.Lock, new WorldPoint(-40, 0, 0), "Dorm 220", MapContentBuilder.KeyGroup("key-2"));
        var (camera, scene) = TestView.Of([TestView.Quest("a", 0, 0, "A", group: "a"), TestView.Quest("b", 100, 0, "B", group: "b"), door, otherDoor]);
        scene.QuestKeys = new Dictionary<string, IReadOnlyList<string>> { ["a"] = [MapContentBuilder.KeyGroup("key-1")] };
        scene.InRaid = inRaid;
        scene.Kept = new HashSet<string> { "a" };
        scene.Focus = new HashSet<string> { "b" };
        scene.Dim = 1;

        var shown = MapRenderer.Layout(camera, scene, 1).Markers;
        var full = new MapRenderer.StepBack(1f, 1f, 1f);
        // The pick and the door of the key it needs: at full strength, their names too.
        Assert.Equal(full, MapRenderer.StepBackOf(scene, shown.Single(m => m.Marker.Group == "a")));
        Assert.Equal(full, MapRenderer.StepBackOf(scene, shown.Single(m => m.Marker == door)));
        // Any other door steps back as locks do.
        Assert.Equal(MapRenderer.StepBackOf(MarkerKind.Lock, inRaid), MapRenderer.StepBackOf(scene, shown.Single(m => m.Marker == otherDoor)));
    }

    // ---- the position's age, beside the marker (owner, 2026-10-03: "Put it next to the marker") ----

    [Theory]
    [InlineData(1.2, "1 MIN", false)]
    [InlineData(1.99, "1 MIN", false)]
    [InlineData(2, "2 MIN OLD", true)]
    [InlineData(7.5, "7 MIN OLD", true)]
    [InlineData(75, "1 H OLD", true)]
    public void The_age_tag_says_old_from_two_minutes(double minutes, string text, bool stale) =>
        Assert.Equal((text, stale), MapRenderer.AgeTag(TimeSpan.FromMinutes(minutes)));

    // ---- optional objectives (owner, 2026-10-03: they "might still be very relevant for a quest") ----

    [Fact]
    public void Optional_objectives_give_optional_markers()
    {
        var map = TestMap("customs");
        ApiObjective Objective(string id, bool optional) => new(id, "visit", "Locate the place", optional, ["map-1"],
            [new ApiZone(null, null, "map-1", At(id == "o1" ? 10 : 50, 0), null, null, null)], null, null, null, null, null, null, null, false);
        var quest = new ApiTask("q", "Quest", null, null, "map-1", null, false, false, null, null, null, false, null,
            [Objective("o1", false), Objective("o2", true)], null);
        var plain = With(map);
        var data = new GameData
        {
            Mode = plain.Mode, Language = "en", Maps = plain.Maps, Traders = plain.Traders, MapDefinitions = plain.MapDefinitions,
            CheckedAt = plain.CheckedAt, Tasks = new Dictionary<string, ApiTask> { ["q"] = quest },
        };
        var markers = MapContentBuilder.Build(data, "map-1", ["q"], new HashSet<string>()).Markers.Where(m => m.Group == "q").ToList();
        Assert.False(markers.Single(m => m.Id.StartsWith("objective:o1:", StringComparison.Ordinal)).Optional);
        Assert.True(markers.Single(m => m.Id.StartsWith("objective:o2:", StringComparison.Ordinal)).Optional);
    }

    [Fact]
    public void An_optional_marker_keeps_its_badge_clear_of_labels()
    {
        // Twenty labelled neighbours around one optional marker: none of their labels may cover its "OPT" badge,
        // which sits at the marker's upper left (10 px marker radius, the badge about 22 × 12 px around (-8.5, -8.5)).
        var markers = Enumerable.Range(0, 20)
            .Select(i => TestView.Quest($"n{i}", (i % 5) * 26 - 60, (i / 5) * 18 - 30, $"Neighbour {i}"))
            .Append(TestView.Quest("opt", 0, 0, "Optional") with { Optional = true })
            .ToList();
        var (camera, scene) = TestView.Of(markers);
        var layout = MapRenderer.Layout(camera, scene, 1);
        var badge = new SkiaSharp.SKRect(500 - 8.5f - 11, 500 - 8.5f - 6, 500 - 8.5f + 11, 500 - 8.5f + 6);
        Assert.All(layout.Labels, l => Assert.False(l.Box.IntersectsWith(badge), $"{l.Text} at {l.Box}"));
    }
}
