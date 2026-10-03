using System.Net;
using System.Text.Json.Nodes;
using Shturmap.Core.Logs;
using Shturmap.Data.Http;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Data.Tests;

// Kill targets, exit statuses and kill conditions for the plan's effort groups, read before translation so they mean
// the same in every language (2026-10-03).
public class ObjectiveFactsTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("shturmap-facts-").FullName;

    public void Dispose() => Directory.Delete(_folder, true);

    private static ObjectiveFacts? Facts(string json) => GameDataLoader.Facts(JsonNode.Parse(json)!.AsObject());

    // A kill objective as tarkov.dev writes one with nothing set.
    private const string PlainKill = """
        "type":"shoot","targetNames":["Savage"],"usingWeapon":[],"usingWeaponMods":[],"bodyParts":[],
        "distance":{"value":0,"compareMethod":">="},"wearing":[],"notWearing":[],"timeFromHour":0,"timeUntilHour":0,
        "playerHealthEffect":null,"enemyHealthEffect":null,"zones":[]
        """;

    [Fact]
    public void A_kill_with_nothing_set_has_no_conditions()
    {
        var facts = Facts("{" + PlainKill + "}")!;
        Assert.Equal(["Savage"], facts.Targets);
        Assert.Empty(facts.Conditions);
    }

    [Theory]
    [InlineData("\"usingWeapon\":[\"5a2a57cfc4a2826c6e06d44a\"]", "weapon")]
    [InlineData("\"usingWeaponMods\":[[\"5a33a8ebc4a282000c5a950d\"]]", "weapon mods")]
    [InlineData("\"bodyParts\":[\"Head\"]", "body parts")]
    [InlineData("\"distance\":{\"value\":40,\"compareMethod\":\">=\"}", "distance")]
    [InlineData("\"wearing\":[[{\"id\":\"x\"}]]", "wearing")]
    [InlineData("\"notWearing\":[{\"id\":\"x\"}]", "not wearing")]
    [InlineData("\"timeFromHour\":21,\"timeUntilHour\":4", "time of day")]
    [InlineData("\"playerHealthEffect\":{\"effects\":[\"Pain\"]}", "player health")]
    [InlineData("\"enemyHealthEffect\":{\"effects\":[\"Stun\"]}", "enemy health")]
    [InlineData("\"zones\":[{\"id\":\"z\"}]", "zone")]
    public void A_set_condition_is_named(string field, string condition) =>
        Assert.Equal([condition], GameDataLoader.Facts(PlainKillWith(field))!.Conditions);

    // The plain kill with some of its fields replaced.
    private static JsonObject PlainKillWith(string fields)
    {
        var kill = JsonNode.Parse("{" + PlainKill + "}")!.AsObject();
        foreach (var (name, value) in JsonNode.Parse("{" + fields + "}")!.AsObject())
            kill[name] = value?.DeepClone();
        return kill;
    }

    [Fact]
    public void No_distance_is_written_as_zero_or_null()
    {
        Assert.Empty(GameDataLoader.Facts(PlainKillWith("\"distance\":null"))!.Conditions);
        Assert.Empty(Facts("{" + PlainKill + "}")!.Conditions);
    }

    [Fact]
    public void Exit_statuses_are_kept_and_other_objectives_have_no_facts()
    {
        Assert.Equal(["ExpBonusSurvived", "marathon Name"], Facts("""{"type":"extract","exitStatus":["ExpBonusSurvived","marathon Name"]}""")!.ExitStatus);
        Assert.Null(Facts("""{"type":"visit","zones":[{"id":"z"}]}"""));
    }

    // tarkov.dev in miniature: one quest with a Scav kill and a "Survived" extract, and German texts for both keys.
    private sealed class FakeTarkovDev : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var name = request.RequestUri!.AbsolutePath.Split('/')[^1];
            var body = name switch
            {
                "maps.json" => "[]",
                "maps" => """{ "data": { "maps": {}, "mobs": {} } }""",
                "tasks" => """
                    { "translations": ["$.data.tasks.*.objectives[*].targetNames[*]", "$.data.tasks.*.objectives[*].exitStatus[*]"],
                      "data": { "questItems": {}, "tasks": { "t1": { "id": "t1", "name": "t1", "objectives": [
                        { "id": "o1", "type": "shoot", "targetNames": ["Savage"], "distance": {"value":0,"compareMethod":">="} },
                        { "id": "o2", "type": "extract", "exitStatus": ["ExpBonusSurvived"] } ] } } } }
                    """,
                "tasks_de" => """{ "data": { "Savage": "Scavs (de)", "ExpBonusSurvived": "Überlebt" } }""",
                "traders" => """{ "data": {} }""",
                _ => """{ "data": {} }""",
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }

    [Fact]
    public async Task Targets_and_exit_statuses_stay_the_games_keys_in_every_language()
    {
        var data = await new GameDataLoader(new CachedHttp(new HttpClient(new FakeTarkovDev()), _folder)).LoadAsync(GameMode.Pve, "ge",
            TestContext.Current.CancellationToken);
        Assert.Equal("de", data.Language);
        Assert.Equal(["Savage"], data.ObjectiveFacts["o1"].Targets);
        Assert.Equal(["ExpBonusSurvived"], data.ObjectiveFacts["o2"].ExitStatus);
    }
}
