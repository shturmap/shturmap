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
