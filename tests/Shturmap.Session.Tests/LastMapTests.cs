using Shturmap.Core.Logs;
using Shturmap.Core.Raid;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Session.Tests;

// The map on screen is remembered (owner, 2026-10-06: "remember the last played and selected map and return to that one
// a) when a raid ends and b) remember the last open map when the app closes and re-open it").
public class LastMapTests
{
    private const string Lobby = "PrepareSelectedProfileLocally ProfileId:000000000000000000000003 AccountId:0";

    // A quest with a place on Customs, so that Plan suggests Customs for the next raid.
    private const string Drive = "aaaaaaaaaaaaaaaaaaaaaa21";

    private static GameData WithACustomsQuest(GameMode mode)
    {
        var data = SessionRig.Data(mode);
        var pickup = new ApiObjective("o-pick", "findQuestItem", "Locate and obtain the hard drive", false, ["map-customs"],
            [new ApiZone("o-pick-zone", null, "map-customs", new ApiPosition(10, 0, 10), null, null, null)], null, 1, "hard-drive", null, null, null, null, false);
        var quest = new ApiTask(Drive, "Saving the Scientist", null, null, "map-customs", null, false, false, null, null, null, false, null, [pickup], null);
        return new GameData
        {
            Mode = data.Mode, Language = data.Language, Maps = data.Maps, Traders = data.Traders, MapDefinitions = data.MapDefinitions,
            CheckedAt = data.CheckedAt, Tasks = new Dictionary<string, ApiTask>(data.Tasks) { [Drive] = quest },
        };
    }

    private static string QuestStarted(string quest, DateTime at) => $$"""
        {
          "type": "new_message",
          "eventId": "test-event-{{quest}}",
          "dialogId": "54cb50c76803fa8b248b4571",
          "message": {
            "_id": "test-message-{{quest}}",
            "type": 10,
            "dt": {{new DateTimeOffset(at).ToUnixTimeSeconds()}},
            "text": "quest",
            "templateId": "{{quest}} description"
          }
        }
        """;

    [Fact]
    public async Task A_restart_opens_the_map_last_on_screen_not_the_best_suggestion()
    {
        await using var rig = new SessionRig(configure: (paths, locations) => new GameSession(paths, locations) { GivenData = WithACustomsQuest });
        rig.Notification("ChatMessageReceived", QuestStarted(Drive, DateTime.Now.AddMinutes(-10)), DateTime.Now.AddMinutes(-10));
        await rig.StartAsync();
        // At a first start, the best suggestion for the next raid.
        var first = await rig.Until(s => s.Data is not null && s.Map is not null && s.Plan.Count > 0, "a map and a plan");
        Assert.Equal(("customs", "customs"), (first.Plan[0].NormalizedName, first.Map?.NormalizedName));
        await rig.Session.SelectMapAsync("woods");

        await rig.RestartAsync();
        await rig.StartAsync();
        var again = await rig.Until(s => s.Data is not null && s.Map is not null && s.Plan.Count > 0, "the map after a restart");
        Assert.Equal("woods", again.Map?.NormalizedName);
    }

    [Fact]
    public async Task A_raid_ends_on_its_own_map_also_after_a_look_at_another_and_a_restart_opens_it()
    {
        await using var rig = new SessionRig();
        rig.RaidUpToItsStart("maps/customs_preset.bundle", "bigmap", DateTime.Now.AddMinutes(-5));
        await rig.StartAsync();
        await rig.Until(s => s.Raid.Phase == RaidPhase.InRaid && s.RaidMap?.NormalizedName == "customs", "the raid on Customs");
        // A look at Woods, and the raid ends without a position since.
        await rig.Session.SelectMapAsync("woods");
        rig.Log(Lobby);
        var over = await rig.Until(s => s.Raid.Phase == RaidPhase.Menu, "the raid's end");
        Assert.Equal("customs", over.Map?.NormalizedName);

        await rig.RestartAsync();
        await rig.StartAsync();
        Assert.Equal("customs", (await rig.Until(s => s.Data is not null && s.Map is not null, "the map after a restart")).Map?.NormalizedName);
    }

    [Fact]
    public async Task A_raid_read_back_at_start_does_not_take_the_place_of_the_map_last_on_screen()
    {
        await using var rig = new SessionRig();
        await rig.StartAsync();
        await rig.Until(s => s.Map is not null, "a map");
        await rig.Session.SelectMapAsync("woods");
        await rig.RestartAsync();

        // A raid on Customs in the log, over before the app starts again: read back, it is older than the map it closed on.
        rig.RaidUpToItsStart("maps/customs_preset.bundle", "bigmap", DateTime.Now.AddMinutes(-20));
        rig.Log(Lobby, DateTime.Now.AddMinutes(-2));
        await rig.StartAsync();
        var started = await rig.Until(s => s.Data is not null && s.Map is not null && s.LastRaid is not null, "the map and the last raid after a restart");
        Assert.Equal("woods", started.Map?.NormalizedName);
        Assert.Equal("Customs", started.LastRaid?.MapName);
    }

    [Fact]
    public async Task A_raid_still_running_at_start_shows_its_own_map()
    {
        await using var rig = new SessionRig();
        await rig.StartAsync();
        await rig.Until(s => s.Map is not null, "a map");
        await rig.Session.SelectMapAsync("woods");
        await rig.RestartAsync();

        rig.RaidUpToItsStart("maps/customs_preset.bundle", "bigmap", DateTime.Now.AddMinutes(-5));
        await rig.StartAsync();
        var raid = await rig.Until(s => s.Raid.Phase == RaidPhase.InRaid && s.RaidMap is not null, "the raid on Customs");
        Assert.Equal("customs", raid.Map?.NormalizedName);
    }
}
