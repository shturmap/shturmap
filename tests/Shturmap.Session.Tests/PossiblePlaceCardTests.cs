using Shturmap.Core.Logs;
using Shturmap.Core.Maps;
using Shturmap.Core.Quests;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Session.Tests;

// The quest's card says what the map's "?" means (owner, 2026-10-06: "It should be displayed in the raid tooltip to
// explain"), and only where there is a "?": several places the thing can be.
public class PossiblePlaceCardTests
{
    private const string Quest = "aaaaaaaaaaaaaaaaaaaaaa31";

    private static CardObjective Row(params ApiPosition[] places)
    {
        var objective = new ApiObjective("o-find", "findQuestItem", "Obtain the journal", false, ["map-streets"], null,
            [new ApiPossibleLocation("map-streets", [.. places])], 1, "journal", null, null, null, null, false);
        var task = new ApiTask(Quest, "Population Census", null, null, "map-streets", null, false, false, null, null, null, false, null, [objective], null);
        var data = new GameData
        {
            Mode = GameMode.Pve,
            Language = "en",
            Maps = new[] { new ApiMap("map-streets", "Streets of Tarkov", "streets-of-tarkov", "TarkovStreets", null, null, 50, [], [], [], [], []) }
                .ToDictionary(m => m.Id),
            Tasks = new Dictionary<string, ApiTask> { [Quest] = task },
            Traders = new Dictionary<string, ApiTrader>(),
            MapDefinitions = [new MapDefinition { Key = "streets-of-tarkov", Transform = [1, 0, 1, 0], Bounds = new WorldBox(-500, -500, 500, 500) }],
            CheckedAt = DateTimeOffset.Now,
        };
        var active = new Dictionary<string, QuestStatus> { [Quest] = new(Quest, QuestState.Active, ObservationSource.Log, DateTime.Now) };
        return Assert.Single(QuestCards.Build(data, active, Quest)!.Objectives);
    }

    [Fact]
    public void Several_places_say_how_many()
    {
        Assert.Equal("One of 3 places it can be", Row(new(10, 0, 20), new(300, 0, 40), new(-200, 0, 90)).Possible);
    }

    [Fact]
    public void One_place_says_nothing_of_it()
    {
        Assert.Equal("", Row(new ApiPosition(10, 0, 20)).Possible);
    }
}
