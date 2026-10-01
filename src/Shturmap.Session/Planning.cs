using Shturmap.Core;
using Shturmap.Core.Planning;
using Shturmap.Core.Quests;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Session;

/// <summary>Something a raid needs, ready to show: "Dorm room 114 key", "MS2000 Marker ×3", for which quests.</summary>
/// <param name="ItemId">The item to picture (the first of the alternatives).</param>
public sealed record RequirementView(RequirementKind Kind, string Text, string ForQuests, string ItemId, IReadOnlyList<string> QuestIds);

public sealed record PlanQuestView(string QuestId, string Name, ObjectiveKind Kind, string? TraderId = null);

/// <summary>One suggested map for the next raid.</summary>
public sealed record MapPlanView(
    string NormalizedName,
    string MapName,
    IReadOnlyList<PlanQuestView> Finish,
    IReadOnlyList<PlanQuestView> Progress,
    IReadOnlyList<RequirementView> Requirements,
    int WalkingMinutes,
    int RaidMinutes,
    IReadOnlyList<string> Bosses);

/// <summary>Turns tarkov.dev quests and maps into the planner's terms and its results into display text.</summary>
public static class Planning
{
    public static IReadOnlyList<MapPlanView> Suggest(GameData data, IEnumerable<string> activeQuestIds)
    {
        var plans = RaidPlanner.Rank(Quests(data, activeQuestIds), Maps(data));
        return plans.Select(p => ToView(data, p)).ToList();
    }

    /// <summary>The plan for one map (whether or not it ranks), e.g. for the bring-list when a raid loads.</summary>
    public static MapPlanView? PlanFor(GameData data, IEnumerable<string> activeQuestIds, string normalizedName)
    {
        var id = data.MapByNormalizedName(normalizedName)?.Id;
        var map = Maps(data).FirstOrDefault(m => id is not null && m.MapIds.Contains(id));
        return map is null ? null : ToView(data, RaidPlanner.Plan(Quests(data, activeQuestIds), map));
    }

    /// <summary>Active quests whose in-raid work fits any map.</summary>
    public static IReadOnlyList<PlanQuestView> AnyMap(GameData data, IEnumerable<string> activeQuestIds) =>
        RaidPlanner.AnyMap(Quests(data, activeQuestIds)).Select(q => QuestView(data, q)).ToList();

    private static List<PlanQuest> Quests(GameData data, IEnumerable<string> ids) =>
        ids.Select(id => data.Tasks.GetValueOrDefault(id)).OfType<ApiTask>().Select(ToPlan).ToList();

    /// <summary>"Kaban 75%": a boss and its spawn chance, without the locale's space before the percent sign.</summary>
    public static string BossText((string Name, double Chance) boss) => $"{boss.Name} {Math.Round(boss.Chance * 100):0}%";

    /// <summary>The planner's view of one quest (also used for the per-objective requirement hints).</summary>
    public static PlanQuest ToPlan(ApiTask task) => new(
        task.Id,
        task.Name,
        (task.Objectives ?? []).Select(ToPlan).ToList(),
        (task.NeededKeys ?? []).Where(k => k.Map is not null)
            .GroupBy(k => k.Map!)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.SelectMany(k => k.Keys ?? []).Distinct().ToList()));

    private static PlanObjective ToPlan(ApiObjective o)
    {
        var kind = QuestTaxonomy.Classify(o.Type);
        var places = new Dictionary<string, List<WorldPoint>>(StringComparer.Ordinal);
        foreach (var zone in o.Zones ?? [])
        {
            if (zone.Map is not null && zone.Position is not null)
                Add(places, zone.Map, zone.Position.ToWorld());
        }
        foreach (var location in o.PossibleLocations ?? [])
        {
            if (location.Map is not null)
                foreach (var p in location.Positions ?? [])
                    Add(places, location.Map, p.ToWorld());
        }

        var count = Math.Max(1, o.Count ?? 1);
        (string, int)[] bring = o.Type switch
        {
            "plantItem" when o.Items is [var item, ..] => [(item, count)],
            "plantQuestItem" when o.QuestItem is { } questItem => [(questItem, count)],
            "mark" when o.MarkerItem is { } marker => [(marker, 1)],
            "useItem" when o.UseAny is [var usable, ..] => [(usable, count)],
            _ => [],
        };
        return new PlanObjective(o.Id, kind, o.Maps ?? [],
            places.ToDictionary(p => p.Key, p => (IReadOnlyList<WorldPoint>)p.Value),
            count, o.Optional,
            (o.RequiredKeys ?? []).Where(k => k.Count > 0).Select(k => (IReadOnlyList<string>)k).ToList(),
            bring);
    }

    private static void Add(Dictionary<string, List<WorldPoint>> places, string map, WorldPoint p)
    {
        if (!places.TryGetValue(map, out var list))
            places[map] = list = [];
        // tarkov.dev lists a zone once per map variant; keep each spot once.
        if (!list.Any(q => q.HorizontalDistanceTo(p) < 0.5))
            list.Add(p);
    }

    /// <summary>Maps as the player picks them: variants drawn with the same artwork are one choice.</summary>
    public static IReadOnlyList<PlanMap> Maps(GameData data) =>
        data.Maps.Values
            .Select(m => (Map: m, Definition: data.DefinitionFor(m.NormalizedName)))
            .Where(m => m.Definition is not null)
            .GroupBy(m => m.Definition!.Key)
            .Select(g =>
            {
                var primary = g.FirstOrDefault(m => m.Map.NormalizedName == g.Key).Map ?? g.First().Map;
                return new PlanMap(primary.Id, primary.Name, g.Select(m => m.Map.Id).ToHashSet(), primary.RaidDuration ?? 0);
            })
            .ToList();

    private static MapPlanView ToView(GameData data, MapPlan plan) => new(
        data.Maps.TryGetValue(plan.Map.Id, out var map) ? map.NormalizedName : plan.Map.Id,
        plan.Map.Name,
        plan.Finish.Select(q => QuestView(data, q.Quest)).ToList(),
        plan.Progress.Select(q => QuestView(data, q.Quest)).ToList(),
        plan.Requirements.Select(r => RequirementText(data, r)).ToList(),
        (int)Math.Ceiling(plan.WalkingMinutes),
        plan.Map.RaidMinutes,
        data.BossesOn(plan.Map.Id).Select(BossText).ToList());

    private static PlanQuestView QuestView(GameData data, PlanQuest q) =>
        new(q.Id, q.Name, QuestTaxonomy.QuestKind(q.Objectives.Select(o => o.Kind)), data.Tasks.GetValueOrDefault(q.Id)?.Trader);

    public static RequirementView RequirementText(GameData data, Requirement r)
    {
        var names = r.Alternatives.Select(data.ItemName).Distinct().ToList();
        var text = names.Count <= 2 ? string.Join(" or ", names) : $"{names[0]} or {names.Count - 1} others";
        if (r.Count > 1)
            text += $" ×{r.Count}";
        var quests = string.Join(", ", r.ForQuests.Select(id => data.Tasks.GetValueOrDefault(id)?.Name ?? id));
        return new RequirementView(r.Kind, text, quests, r.Alternatives[0], r.ForQuests.ToList());
    }

    /// <summary>"Key: X · Bring: Y" for one objective on one map, or null if it needs nothing.</summary>
    /// <param name="hasPlace">Quest-level keys are shown only on objectives with a place, where the key is used.</param>
    public static string? Needs(GameData data, ApiTask task, ApiObjective objective, IReadOnlySet<string> mapIds, bool hasPlace)
    {
        var plan = ToPlan(objective);
        var parts = new List<string>();
        var keys = plan.Keys.Select(k => string.Join(" or ", k.Select(data.ItemName))).ToList();
        if (hasPlace)
        {
            keys.AddRange((task.NeededKeys ?? []).Where(k => k.Map is not null && mapIds.Contains(k.Map))
                .SelectMany(k => k.Keys ?? []).Select(data.ItemName));
        }
        if (keys.Count > 0)
            parts.Add("Key: " + string.Join(", ", keys.Distinct()));
        if (plan.Bring.Count > 0)
            parts.Add("Bring: " + string.Join(", ", plan.Bring.Select(b => data.ItemName(b.ItemId) + (b.Count > 1 ? $" ×{b.Count}" : ""))));
        return parts.Count > 0 ? string.Join(" · ", parts) : null;
    }
}
