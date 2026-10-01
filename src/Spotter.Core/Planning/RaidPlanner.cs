using Spotter.Core.Quests;

namespace Spotter.Core.Planning;

/// <summary>An item a raid needs: a key (any one of the alternatives) or something to bring and use up.</summary>
/// <param name="Alternatives">Item ids; any one will do (keys often have alternatives).</param>
/// <param name="ForQuests">Quest ids that need it.</param>
public sealed record Requirement(RequirementKind Kind, IReadOnlyList<string> Alternatives, int Count, IReadOnlyList<string> ForQuests);

public enum RequirementKind
{
    Key,
    Bring,
}

/// <param name="Places">Positions per map id, where the objective has fixed places.</param>
/// <param name="Count">How many kills or items it asks for (1 when it's a single action).</param>
/// <param name="Keys">Key alternatives needed for this objective: each inner list is one way in.</param>
/// <param name="Bring">Items consumed: (item id, count).</param>
public sealed record PlanObjective(
    string Id,
    ObjectiveKind Kind,
    IReadOnlyList<string> MapIds,
    IReadOnlyDictionary<string, IReadOnlyList<WorldPoint>> Places,
    int Count,
    bool Optional,
    IReadOnlyList<IReadOnlyList<string>> Keys,
    IReadOnlyList<(string ItemId, int Count)> Bring);

/// <param name="NeededKeys">Keys the quest needs, per map id.</param>
public sealed record PlanQuest(string Id, string Name, IReadOnlyList<PlanObjective> Objectives, IReadOnlyDictionary<string, IReadOnlyList<string>> NeededKeys);

/// <summary>A map as the player picks it in the game; variants that share artwork (Ground Zero 21+) are one map.</summary>
public sealed record PlanMap(string Id, string Name, IReadOnlySet<string> MapIds, int RaidMinutes);

public sealed record QuestOnMap(PlanQuest Quest, IReadOnlyList<PlanObjective> Objectives);

/// <param name="RouteMeters">Walking distance through one place per located objective, nearest first.</param>
public sealed record MapPlan(
    PlanMap Map,
    double Score,
    IReadOnlyList<QuestOnMap> Finish,
    IReadOnlyList<QuestOnMap> Progress,
    IReadOnlyList<Requirement> Requirements,
    double RouteMeters)
{
    public const double WalkingSpeed = 3; // m/s, allowing for caution and detours

    public double WalkingMinutes => RouteMeters / WalkingSpeed / 60;
}

/// <summary>
/// Ranks maps for the next raid by what the active quests let you do there. See docs/DESIGN.md §7. Objective
/// progress is unknown, so counts above <see cref="OneRaidCount"/> are treated as more than one raid's work.
/// </summary>
public static class RaidPlanner
{
    public const int OneRaidCount = 3;

    public static IReadOnlyList<MapPlan> Rank(IEnumerable<PlanQuest> quests, IEnumerable<PlanMap> maps, int top = 4)
    {
        var questList = quests.ToList();
        return maps
            .Select(map => Plan(questList, map))
            .Where(plan => plan.Score > 0)
            .OrderByDescending(plan => plan.Score)
            .ThenBy(plan => plan.RouteMeters)
            .Take(top)
            .ToList();
    }

    public static MapPlan Plan(IReadOnlyList<PlanQuest> quests, PlanMap map)
    {
        var finish = new List<QuestOnMap>();
        var progress = new List<QuestOnMap>();
        double score = 0;

        foreach (var quest in quests)
        {
            var inRaid = quest.Objectives.Where(o => QuestTaxonomy.InRaid(o.Kind) && !o.Optional).ToList();
            var doable = inRaid.Where(o => IsDoable(o, map)).ToList();
            // Work that can be done on any map doesn't argue for this one; it is listed once (see AnyMap).
            var tied = doable.Where(o => IsTied(o, map)).ToList();
            if (tied.Count == 0)
                continue;
            score += tied.Sum(o => Places(o, map).Count > 0 ? 1 : 0.6) + (doable.Count - tied.Count) * 0.1;
            // Found-in-raid items depend on luck, and large kill counts take several raids.
            var finishable = doable.Count == inRaid.Count &&
                             doable.All(o => o.Kind != ObjectiveKind.FindInRaid && (o.Kind != ObjectiveKind.Elimination || o.Count <= OneRaidCount));
            if (finishable)
            {
                finish.Add(new QuestOnMap(quest, doable));
                score += 3;
            }
            else
            {
                progress.Add(new QuestOnMap(quest, doable));
            }
        }

        var involved = finish.Concat(progress).ToList();
        return new MapPlan(map, Math.Round(score, 2), finish, progress, Requirements(involved, map), RouteLength(involved, map));
    }

    /// <summary>Quests whose in-raid work can be done on any map (kills anywhere, found-in-raid items).</summary>
    public static IReadOnlyList<PlanQuest> AnyMap(IEnumerable<PlanQuest> quests) =>
        quests.Where(q =>
        {
            var inRaid = q.Objectives.Where(o => QuestTaxonomy.InRaid(o.Kind) && !o.Optional).ToList();
            return inRaid.Count > 0 && inRaid.All(o => o.MapIds.Count == 0 && o.Places.Count == 0 && QuestTaxonomy.WorksAnywhere(o.Kind));
        }).ToList();

    /// <summary>The objective names this map or has a place on it.</summary>
    public static bool IsTied(PlanObjective o, PlanMap map) => o.MapIds.Any(map.MapIds.Contains) || Places(o, map).Count > 0;

    public static bool IsDoable(PlanObjective o, PlanMap map) =>
        QuestTaxonomy.InRaid(o.Kind) &&
        (o.MapIds.Any(map.MapIds.Contains) || Places(o, map).Count > 0 || (o.MapIds.Count == 0 && QuestTaxonomy.WorksAnywhere(o.Kind)));

    public static IReadOnlyList<WorldPoint> Places(PlanObjective o, PlanMap map) =>
        o.Places.Where(p => map.MapIds.Contains(p.Key)).SelectMany(p => p.Value).ToList();

    private static List<Requirement> Requirements(List<QuestOnMap> involved, PlanMap map)
    {
        var keys = new Dictionary<string, (List<string> Alternatives, HashSet<string> Quests)>(StringComparer.Ordinal);
        var items = new Dictionary<string, (int Count, HashSet<string> Quests)>(StringComparer.Ordinal);

        void AddKey(IReadOnlyList<string> alternatives, string questId)
        {
            if (alternatives.Count == 0)
                return;
            var id = string.Join("|", alternatives.Order(StringComparer.Ordinal));
            if (!keys.TryGetValue(id, out var entry))
                keys[id] = entry = (alternatives.ToList(), new HashSet<string>());
            entry.Quests.Add(questId);
        }

        foreach (var (quest, objectives) in involved)
        {
            foreach (var (mapId, needed) in quest.NeededKeys)
            {
                if (map.MapIds.Contains(mapId))
                    foreach (var key in needed)
                        AddKey([key], quest.Id);
            }
            foreach (var objective in objectives)
            {
                foreach (var alternatives in objective.Keys)
                    AddKey(alternatives, quest.Id);
                foreach (var (itemId, count) in objective.Bring)
                {
                    if (!items.TryGetValue(itemId, out var entry))
                        entry = (0, new HashSet<string>());
                    entry.Quests.Add(quest.Id);
                    items[itemId] = (entry.Count + count, entry.Quests);
                }
            }
        }

        // A key listed alone also satisfies any alternative set that contains it.
        var singles = keys.Values.Where(k => k.Alternatives.Count == 1).Select(k => k.Alternatives[0]).ToHashSet(StringComparer.Ordinal);
        return keys.Values
            .Where(k => k.Alternatives.Count == 1 || !k.Alternatives.Any(singles.Contains))
            .Select(k => new Requirement(RequirementKind.Key, k.Alternatives, 1, k.Quests.ToList()))
            .Concat(items.Select(i => new Requirement(RequirementKind.Bring, [i.Key], i.Value.Count, i.Value.Quests.ToList())))
            .ToList();
    }

    private static double RouteLength(List<QuestOnMap> involved, PlanMap map)
    {
        var stops = new List<WorldPoint>();
        foreach (var objective in involved.SelectMany(q => q.Objectives))
        {
            var places = Places(objective, map);
            if (places.Count == 0)
                continue;
            // Of several possible places, take the one closest to the stops chosen so far.
            stops.Add(stops.Count == 0 ? places[0] : places.MinBy(p => stops.Min(s => s.HorizontalDistanceTo(p))));
        }
        if (stops.Count < 2)
            return 0;

        var remaining = new List<WorldPoint>(stops);
        var at = remaining[0];
        remaining.RemoveAt(0);
        double length = 0;
        while (remaining.Count > 0)
        {
            var next = remaining.MinBy(p => at.HorizontalDistanceTo(p));
            length += at.HorizontalDistanceTo(next);
            remaining.Remove(next);
            at = next;
        }
        return length;
    }
}
