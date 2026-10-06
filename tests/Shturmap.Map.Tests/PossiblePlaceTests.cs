using Shturmap.Data.TarkovDev;
using static Shturmap.Map.Tests.Fixtures;

namespace Shturmap.Map.Tests;

// "Maybe here" only where a thing has several places (owner, 2026-10-06: "Why does the Population Census have a
// question mark in the icon?"): tarkov.dev lists every quest item's spot as a possible location, also where it is the
// only one.
public class PossiblePlaceTests
{
    private static MapContent Content(params ApiPosition[] places)
    {
        var map = TestMap("streets-of-tarkov");
        var objective = new ApiObjective("o1", "findQuestItem", "Obtain the journal", false, ["map-1"], null,
            [new ApiPossibleLocation("map-1", [.. places])], 1, "journal", null, null, null, null, false);
        var quest = new ApiTask("q", "Population Census", null, null, "map-1", null, false, false, null, null, null, false, null, [objective], null);
        var plain = With(map);
        var data = new GameData
        {
            Mode = plain.Mode, Language = "en", Maps = plain.Maps, Traders = plain.Traders, MapDefinitions = plain.MapDefinitions,
            CheckedAt = plain.CheckedAt, Tasks = new Dictionary<string, ApiTask> { ["q"] = quest },
        };
        return MapContentBuilder.Build(data, "map-1", ["q"], new HashSet<string>());
    }

    [Fact]
    public void One_place_is_a_place_like_any_other_without_a_question_mark()
    {
        var marker = Assert.Single(Content(At(10, 20)).Markers, m => m.Group == "q");
        Assert.Equal(MarkerKind.Objective, marker.Kind);
    }

    [Fact]
    public void Several_places_are_each_maybe_here()
    {
        var markers = Content(At(10, 20), At(300, 40), At(-200, 90)).Markers.Where(m => m.Group == "q").ToList();
        Assert.Equal(3, markers.Count);
        Assert.All(markers, m => Assert.Equal(MarkerKind.PossibleLocation, m.Kind));
    }
}
