using Shturmap.Core;
using Shturmap.Core.Logs;
using Shturmap.Core.Planning;
using Shturmap.Core.Quests;
using Shturmap.Data.Http;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Session.Tests;

// The Plan card's rows in effort order, with a hairline where a later group starts (owner, 2026-10-03).
public class PlanOrderTests
{
    private static readonly PlanMap Customs = new("customs", "Customs", new HashSet<string> { "customs" }, 35);

    private static GameData Empty() => new()
    {
        Mode = GameMode.Pve,
        Language = "en",
        Maps = new Dictionary<string, ApiMap>(),
        Tasks = new Dictionary<string, ApiTask>(),
        Traders = new Dictionary<string, ApiTrader>(),
        MapDefinitions = [],
        CheckedAt = DateTimeOffset.Now,
    };

    private static QuestOnMap Quest(string name, string type, params string[] targets) =>
        new(new PlanQuest(name, name, [], new Dictionary<string, IReadOnlyList<string>>()),
        [
            new PlanObjective(name + "1", QuestTaxonomy.Classify(type), ["customs"], new Dictionary<string, IReadOnlyList<WorldPoint>>(), 1, false, [], [],
                Type: type, Targets: targets),
        ]);

    [Fact]
    public void A_hairline_starts_each_later_group_and_never_the_first_row()
    {
        var rows = Planning.Rows(Empty(), [Quest("A", "visit"), Quest("B", "mark"), Quest("C", "shoot", "Savage"), Quest("D", "shoot", "AnyPmc"), Quest("E", "shoot", "bossKnight")], Customs);
        Assert.Equal([false, false, true, true, false], rows.Select(r => r.StartsGroup));
        Assert.Equal([EffortGroup.GoThere, EffortGroup.GoThere, EffortGroup.FindOrSurvive, EffortGroup.Fight, EffortGroup.Fight], rows.Select(r => r.Group));
    }

    [Fact]
    public void A_section_of_one_group_has_no_hairline() =>
        Assert.All(Planning.Rows(Empty(), [Quest("D", "shoot", "AnyPmc"), Quest("E", "shoot", "bossKnight")], Customs), r => Assert.False(r.StartsGroup));

    // Every quest row tarkov.dev's data gives, on every map, from the app's own download cache (the quests are
    // tarkov.dev's and BSG's, so not in the repository; skipped without a cache).
    private sealed class Offline : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("offline: the test reads the cache only");
    }

    [Theory]
    [InlineData(GameMode.Pve)]
    [InlineData(GameMode.Pvp)]
    public async Task Every_plan_row_has_a_group_and_each_section_is_in_effort_order(GameMode mode)
    {
        var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shturmap", "cache", "tarkov-dev");
        if (!File.Exists(Path.Combine(cache, GameData.Slug(mode) + "_tasks.json")))
            Assert.Skip($"No tarkov.dev cache at {cache}: run Shturmap or shturmap-cli once to download it.");
        GameData data;
        try
        {
            data = await new GameDataLoader(new CachedHttp(new HttpClient(new Offline()), cache)).LoadAsync(mode, "en", TestContext.Current.CancellationToken);
        }
        catch (HttpRequestException e)
        {
            Assert.Skip("The tarkov.dev cache is incomplete: " + e.Message);
            throw;
        }

        var quests = data.Tasks.Values.Select(t => Planning.ToPlan(t, data)).ToList();
        var rows = 0;
        var unknown = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var map in Planning.Maps(data))
        {
            var plan = RaidPlanner.Plan(quests, map);
            foreach (var section in new[] { plan.Finish, plan.Progress })
            {
                var efforts = section.Select(QuestEffort.Of).ToList();
                Assert.All(efforts, e => Assert.True(Enum.IsDefined(e.Group)));
                for (var i = 1; i < efforts.Count; i++)
                    Assert.True(efforts[i - 1].CompareTo(efforts[i]) <= 0, $"{map.Name}: {section[i - 1].Quest.Name} before {section[i].Quest.Name}");
                rows += section.Count;
                unknown.UnionWith(section.SelectMany(q => q.Objectives).SelectMany(o => o.Targets ?? []).Where(t => !QuestEffort.IsKnownTarget(t)));
            }
        }
        Assert.True(rows > 100, $"only {rows} rows: is the cache complete?");
        // A kill target the rules don't know counts as a fight (the safe side); name it in QuestEffort when one turns up.
        TestContext.Current.SendDiagnosticMessage(unknown.Count == 0 ? "No unknown kill targets." : "Unknown kill targets: " + string.Join(", ", unknown));
        Assert.True(data.ObjectiveFacts.Count > 100, "no objective facts read: did tarkov.dev rename targetNames or exitStatus?");
    }
}
