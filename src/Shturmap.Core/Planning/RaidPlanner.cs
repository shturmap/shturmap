using Shturmap.Core.Quests;

namespace Shturmap.Core.Planning;

/// <summary>An item a raid needs: a key (any one of the alternatives) or something to bring and use up.</summary>
/// <param name="Alternatives">Item ids; any one will do (keys often have alternatives).</param>
/// <param name="ForQuests">Quest ids that need it.</param>
/// <param name="Exit">For <see cref="RequirementKind.Exit"/>: the exit it is for, by its name ("Klimov Street (Flare)").</param>
/// <param name="Enter">For <see cref="RequirementKind.Entry"/>: the map it lets you into ("The Lab").</param>
public sealed record Requirement(RequirementKind Kind, IReadOnlyList<string> Alternatives, int Count, IReadOnlyList<string> ForQuests,
    string? Exit = null, string? Enter = null);

public enum RequirementKind
{
    Key,
    Bring,

    /// <summary>Gear to wear while doing the objective (kills while wearing a helmet, a beanie).</summary>
    Wear,

    /// <summary>A weapon to kill with: any one of the alternatives (a class arrives as all its members).</summary>
    Weapon,

    /// <summary>Mods the weapon must carry for the kills (a suppressor, a scope): fitted, not used up.</summary>
    WeaponMods,

    /// <summary>What the exit a quest names takes to leave through it: a flare, climbing gear, money.</summary>
    Exit,

    /// <summary>What it takes to enter the map at all (The Lab's keycard): for no quest, always first.</summary>
    Entry,
}

/// <param name="Places">Positions per map id, where the objective has fixed places.</param>
/// <param name="Count">How many kills or items it asks for (1 when it's a single action).</param>
/// <param name="Keys">Key alternatives needed for this objective: each inner list is one way in.</param>
/// <param name="Bring">Items consumed: (item id, count).</param>
/// <param name="Wear">Gear to wear: each inner list is a set worn together; any set will do.</param>
/// <param name="Type">tarkov.dev's objective type ("visit", "shoot", …), for its effort group (<see cref="QuestEffort"/>).</param>
/// <param name="Targets">A kill objective's targets, as the game's keys ("Savage", "AnyPmc", "bossKnight").</param>
/// <param name="ExitStatus">An extract objective's accepted exit statuses, as the game's keys ("ExpBonusSurvived").</param>
/// <param name="Conditions">A kill objective's set conditions by name ("weapon", "distance", "zone", …).</param>
/// <param name="FoundInRaid">A find objective whose items must be found in raid.</param>
/// <param name="Weapons">A kill objective's weapons: any one will do.</param>
/// <param name="Mods">A kill objective's weapon mods: each inner list is a set fitted together; any set will do.</param>
/// <param name="NotWearing">Gear a kill objective forbids: a note on the objective, nothing to bring.</param>
/// <param name="Exit">The exit an extract objective names, by its name, when leaving there takes items.</param>
/// <param name="ExitItems">What that exit takes: (item id, count), all of them (climbing gear is two items).</param>
public sealed record PlanObjective(
    string Id,
    ObjectiveKind Kind,
    IReadOnlyList<string> MapIds,
    IReadOnlyDictionary<string, IReadOnlyList<WorldPoint>> Places,
    int Count,
    bool Optional,
    IReadOnlyList<IReadOnlyList<string>> Keys,
    IReadOnlyList<(string ItemId, int Count)> Bring,
    IReadOnlyList<IReadOnlyList<string>>? Wear = null,
    string? Type = null,
    IReadOnlyList<string>? Targets = null,
    IReadOnlyList<string>? ExitStatus = null,
    IReadOnlyList<string>? Conditions = null,
    bool FoundInRaid = false,
    IReadOnlyList<string>? Weapons = null,
    IReadOnlyList<IReadOnlyList<string>>? Mods = null,
    IReadOnlyList<string>? NotWearing = null,
    string? Exit = null,
    IReadOnlyList<(string ItemId, int Count)>? ExitItems = null);

/// <param name="NeededKeys">Keys the quest needs, per map id.</param>
/// <param name="TraderOrder">The quest giver's place in the trader list as tarkov.dev gives it (the game's own order),
/// for the plan's order; quests of unknown traders come last.</param>
public sealed record PlanQuest(string Id, string Name, IReadOnlyList<PlanObjective> Objectives, IReadOnlyDictionary<string, IReadOnlyList<string>> NeededKeys,
    int TraderOrder = int.MaxValue);

/// <summary>A map as the player picks it in the game; variants that share artwork (Ground Zero 21+) are one map.</summary>
/// <param name="EntryItems">What it takes to enter it at all, as item ids (The Lab's keycard; tarkov.dev's accessKeys).</param>
public sealed record PlanMap(string Id, string Name, IReadOnlySet<string> MapIds, int RaidMinutes, IReadOnlyList<string>? EntryItems = null);

public sealed record QuestOnMap(PlanQuest Quest, IReadOnlyList<PlanObjective> Objectives);

/// <summary>Why a quest only progresses on a map, as the planner sees it (<see cref="RaidPlanner.WhyProgress"/>).</summary>
/// <param name="Here">Its raid objectives that can be done on this map.</param>
/// <param name="InRaid">All its raid objectives (not optional).</param>
/// <param name="FoundInRaid">One of those here wants items found in raid.</param>
/// <param name="Kills">The largest kill count here above <see cref="RaidPlanner.OneRaidCount"/>, or 0.</param>
public sealed record ProgressFacts(int Here, int InRaid, bool FoundInRaid, int Kills);

/// <param name="RouteMeters">Distance through one place per located objective, nearest first: only to break ties
/// between maps of equal score. Never shown; a time or distance estimate in the UI misled more than it helped (owner,
/// 2026-10-03).</param>
public sealed record MapPlan(
    PlanMap Map,
    double Score,
    IReadOnlyList<QuestOnMap> Finish,
    IReadOnlyList<QuestOnMap> Progress,
    IReadOnlyList<Requirement> Requirements,
    double RouteMeters);

/// <summary>
/// Ranks maps for the next raid by what the active quests let you do there. See docs/DESIGN.md §7. Objective
/// progress is unknown, so counts above <see cref="OneRaidCount"/> are treated as more than one raid's work.
/// </summary>
public static class RaidPlanner
{
    public const int OneRaidCount = 3;

    /// <param name="top">How many maps are suggested.</param>
    /// <param name="picks">The quests picked for the coming raid. Every map with a pick comes first, most picks first,
    /// then in the planner's order, whatever its rank: the player's plan before the planner's (owner, 2026-10-03). The
    /// rest fill up to <paramref name="top"/>; maps with picks are all shown, also beyond it.</param>
    public static IReadOnlyList<MapPlan> Rank(IEnumerable<PlanQuest> quests, IEnumerable<PlanMap> maps, int top = 4, IReadOnlySet<string>? picks = null)
    {
        var questList = quests.ToList();
        var ranked = maps
            .Select(map => Plan(questList, map))
            .Where(plan => plan.Score > 0)
            .OrderByDescending(plan => plan.Score)
            .ThenBy(plan => plan.RouteMeters)
            .ToList();
        if (picks is not { Count: > 0 })
            return ranked.Take(top).ToList();
        // Every map is ranked before the cut: a pick on the map ranked fifth would otherwise never show.
        var picked = ranked
            .Select((plan, rank) => (Plan: plan, Rank: rank, Picks: plan.Finish.Concat(plan.Progress).Count(q => picks.Contains(q.Quest.Id))))
            .Where(x => x.Picks > 0)
            .OrderByDescending(x => x.Picks)
            .ThenBy(x => x.Rank)
            .Select(x => x.Plan)
            .ToList();
        var first = picked.Select(p => p.Map.Id).ToHashSet(StringComparer.Ordinal);
        return [.. picked, .. ranked.Where(p => !first.Contains(p.Map.Id)).Take(Math.Max(0, top - picked.Count))];
    }

    /// <summary>
    /// The facts behind a quest being in PROGRESS rather than COMPLETE: the same tests as <see cref="Plan"/>'s
    /// "finishable" (objectives elsewhere, found-in-raid items, kill counts above <see cref="OneRaidCount"/>).
    /// </summary>
    public static ProgressFacts WhyProgress(QuestOnMap quest) => new(
        quest.Objectives.Count,
        quest.Quest.Objectives.Count(o => QuestTaxonomy.InRaid(o.Kind) && !o.Optional),
        quest.Objectives.Any(o => o.Kind == ObjectiveKind.FindInRaid),
        quest.Objectives.Where(o => o.Kind == ObjectiveKind.Elimination && o.Count > OneRaidCount).Select(o => o.Count).DefaultIfEmpty(0).Max());

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

        // Each section in effort order: going somewhere, then finding or surviving, then fighting (owner, 2026-10-03).
        finish = QuestEffort.Order(finish).ToList();
        progress = QuestEffort.Order(progress).ToList();
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
        var wear = new Dictionary<string, (List<string> Items, HashSet<string> Quests)>(StringComparer.Ordinal);
        var weapons = new Dictionary<string, (List<string> Items, HashSet<string> Quests)>(StringComparer.Ordinal);
        var mods = new Dictionary<string, (List<string> Items, HashSet<string> Quests)>(StringComparer.Ordinal);
        var exits = new Dictionary<string, (string Item, int Count, string Exit, HashSet<string> Quests)>(StringComparer.Ordinal);

        // One row per thing to take, listing every quest it serves: the same weapons, mods or exit item merge.
        static void Merge(Dictionary<string, (List<string> Items, HashSet<string> Quests)> rows, IReadOnlyList<string> things, string questId)
        {
            if (things.Count == 0)
                return;
            var id = string.Join("|", things.Order(StringComparer.Ordinal));
            if (!rows.TryGetValue(id, out var entry))
                rows[id] = entry = (things.ToList(), new HashSet<string>());
            entry.Quests.Add(questId);
        }

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
                // Gear is worn, not used up: one requirement per objective's choice of sets.
                if (objective.Wear is { Count: > 0 } sets)
                    Merge(wear, sets.SelectMany(s => s).Distinct().ToList(), quest.Id);
                // A weapon to kill with (any one of them), and the mods it must carry (fitted, so like gear).
                if (objective.Weapons is { Count: > 0 } guns)
                    Merge(weapons, guns.Distinct().ToList(), quest.Id);
                if (objective.Mods is { Count: > 0 } modSets)
                    Merge(mods, modSets.SelectMany(s => s).Distinct().ToList(), quest.Id);
                // What the exit a quest names takes (Cease Fire!: a red flare for Klimov Street): one row per item.
                if (objective.Exit is { } exit)
                {
                    foreach (var (itemId, count) in objective.ExitItems ?? [])
                    {
                        var id = itemId + "|" + exit;
                        if (!exits.TryGetValue(id, out var entry))
                            exits[id] = entry = (itemId, count, exit, new HashSet<string>());
                        entry.Quests.Add(quest.Id);
                    }
                }
            }
        }

        // A key listed alone also satisfies any alternative set that contains it.
        var singles = keys.Values.Where(k => k.Alternatives.Count == 1).Select(k => k.Alternatives[0]).ToHashSet(StringComparer.Ordinal);
        // What it takes to enter the map comes first: without it there is no raid (owner, 2026-10-03, from the map audit).
        var entryItems = (map.EntryItems ?? []).Distinct().Select(id => new Requirement(RequirementKind.Entry, [id], 1, [], Enter: map.Name));
        return entryItems.Concat(keys.Values
            .Where(k => k.Alternatives.Count == 1 || !k.Alternatives.Any(singles.Contains))
            .Select(k => new Requirement(RequirementKind.Key, k.Alternatives, 1, k.Quests.ToList()))
            .Concat(items.Select(i => new Requirement(RequirementKind.Bring, [i.Key], i.Value.Count, i.Value.Quests.ToList())))
            .Concat(exits.Values.Select(e => new Requirement(RequirementKind.Exit, [e.Item], e.Count, e.Quests.ToList(), e.Exit)))
            .Concat(wear.Values.Select(w => new Requirement(RequirementKind.Wear, w.Items, 1, w.Quests.ToList())))
            .Concat(weapons.Values.Select(w => new Requirement(RequirementKind.Weapon, w.Items, 1, w.Quests.ToList())))
            .Concat(mods.Values.Select(m => new Requirement(RequirementKind.WeaponMods, m.Items, 1, m.Quests.ToList()))))
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
