using Shturmap.Core.Logs;
using Shturmap.Core.Quests;
using Shturmap.Data.Http;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Data.Tests;

// Every synopsis tarkov.dev's quests give, checked against the rules: no word that isn't in the text or the verb
// table, no condition left out, no cut mid-phrase. The quest texts are tarkov.dev's and BSG's, so they are not in the
// repository: this reads the app's own download cache and skips when there is none.
public class SynopsisDataTests
{
    private sealed class Offline : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("offline: the test reads the cache only");
    }

    private static async Task<GameData> Cached(GameMode mode)
    {
        var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shturmap", "cache", "tarkov-dev");
        var slug = GameData.Slug(mode);
        if (!File.Exists(Path.Combine(cache, slug + "_tasks.json")))
            Assert.Skip($"No tarkov.dev cache at {cache}: run Shturmap or shturmap-cli once to download it.");
        try
        {
            return await new GameDataLoader(new CachedHttp(new HttpClient(new Offline()), cache)).LoadAsync(mode, "en", TestContext.Current.CancellationToken);
        }
        catch (HttpRequestException e)
        {
            Assert.Skip("The tarkov.dev cache is incomplete: " + e.Message);
            throw;
        }
    }

    [Theory]
    [InlineData(GameMode.Pve)]
    [InlineData(GameMode.Pvp)]
    public async Task Every_quest_on_every_map_keeps_to_the_rules(GameMode mode)
    {
        var data = await Cached(mode);
        var allNames = data.Maps.Values.Select(m => m.Name).ToList();
        var problems = new List<string>();
        var lines = 0;
        foreach (var group in data.Maps.Values.GroupBy(m => data.DefinitionFor(m.NormalizedName)?.Key ?? m.NormalizedName))
        {
            var ids = group.Select(m => m.Id).ToHashSet();
            var here = group.Select(m => m.Name).ToList();
            foreach (var task in data.Tasks.Values)
            {
                // As the planner counts them: in-raid, not optional, tied to this map or doable on any.
                var objectives = (task.Objectives ?? [])
                    .Where(o => QuestTaxonomy.InRaid(QuestTaxonomy.Classify(o.Type)) && !o.Optional)
                    .Select(o => (O: o, Places: (o.Zones ?? []).Where(z => z.Map is not null && ids.Contains(z.Map)).Count() +
                                               (o.PossibleLocations ?? []).Where(l => l.Map is not null && ids.Contains(l.Map)).Count()))
                    .Where(x => x.Places > 0 || (x.O.Maps ?? []).Any(ids.Contains) || (x.O.Maps ?? []).Count == 0)
                    .Select(x => new SynopsisObjective(x.O.Description ?? "", Math.Max(1, x.O.Count ?? 1), x.O.Type, x.Places > 0))
                    .ToList();
                if (objectives.Count == 0)
                    continue;
                var line = QuestSynopsis.Of(objectives, here, allNames);
                lines++;
                problems.AddRange(QuestSynopsis.Problems(line).Select(p => $"{group.Key} · {task.Name}: {p}"));
            }
        }
        Assert.True(lines > 100, $"only {lines} quest lines: is the cache complete?");
        Assert.True(problems.Count == 0, string.Join("\n", problems.Take(40)));
    }
}
