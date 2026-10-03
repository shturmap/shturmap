using Shturmap.Core;
using Shturmap.Core.Planning;
using Shturmap.Core.Quests;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Session;

/// <summary>Something a raid needs, ready to show: "Dorm room 114 key", "MS2000 Marker ×3", for which quests.</summary>
/// <param name="ItemId">The item to picture (the first of the alternatives).</param>
/// <param name="Why">What it is for, then for which quests: "to mark, for Revision", "key for Ballet Lover".</param>
public sealed record RequirementView(RequirementKind Kind, string Text, string ForQuests, string ItemId, IReadOnlyList<string> QuestIds, string Why = "");

/// <param name="Synopsis">What it asks on the plan's map in a few words (<see cref="Planning.Synopsis"/>), or empty.</param>
/// <param name="Group">Its effort group on the plan's map (<see cref="QuestEffort"/>).</param>
/// <param name="StartsGroup">The first row of a later effort group in its section: a hairline goes above it.</param>
/// <param name="Note">For a PROGRESS row, why it only progresses here, in a few words (<see cref="Planning.ProgressNote"/>); else empty.</param>
public sealed record PlanQuestView(string QuestId, string Name, ObjectiveKind Kind, string? TraderId = null, string Synopsis = "",
    EffortGroup Group = EffortGroup.GoThere, bool StartsGroup = false, string Note = "");

/// <summary>One suggested map for the next raid.</summary>
public sealed record MapPlanView(
    string NormalizedName,
    string MapName,
    IReadOnlyList<PlanQuestView> Finish,
    IReadOnlyList<PlanQuestView> Progress,
    IReadOnlyList<RequirementView> Requirements,
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

    /// <summary>
    /// The facts under a map's name in Plan: the raid's length and its bosses with their chances. No walking time: it
    /// was an estimate, for quests the line didn't name, and players rarely just walk (owner, 2026-10-03).
    /// </summary>
    public static string FactsLine(MapPlanView plan) =>
        string.Join(" · ", new[] { plan.RaidMinutes > 0 ? $"{plan.RaidMinutes} min raid" : null }.Concat(plan.Bosses).OfType<string>());

    /// <summary>Active quests whose in-raid work fits any map.</summary>
    public static IReadOnlyList<PlanQuestView> AnyMap(GameData data, IEnumerable<string> activeQuestIds) =>
        RaidPlanner.AnyMap(Quests(data, activeQuestIds)).Select(q => QuestView(data, q)).ToList();

    private static List<PlanQuest> Quests(GameData data, IEnumerable<string> ids) =>
        ids.Select(id => data.Tasks.GetValueOrDefault(id)).OfType<ApiTask>().Select(t => ToPlan(t, data)).ToList();

    /// <summary>"Kaban 75%": a boss and its spawn chance, without the locale's space before the percent sign.</summary>
    public static string BossText((string Name, double Chance) boss) => $"{boss.Name} {Math.Round(boss.Chance * 100):0}%";

    /// <summary>The planner's view of one quest (also used for the per-objective requirement hints).</summary>
    public static PlanQuest ToPlan(ApiTask task) => ToPlan(task, null);

    /// <summary>The planner's view of one quest with what orders it in a plan: its objectives' kill targets,
    /// conditions and exit statuses, and its trader's place in tarkov.dev's trader list (the game's own order).</summary>
    public static PlanQuest ToPlan(ApiTask task, GameData? data)
    {
        var traderOrder = data is not null && task.Trader is { } trader && data.Traders.Keys.ToList().IndexOf(trader) is var at and >= 0 ? at : int.MaxValue;
        return new(
            task.Id,
            task.Name,
            (task.Objectives ?? []).Select(o => ToPlan(o, data?.ObjectiveFacts.GetValueOrDefault(o.Id))).ToList(),
            (task.NeededKeys ?? []).Where(k => k.Map is not null)
                .GroupBy(k => k.Map!)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.SelectMany(k => k.Keys ?? []).Distinct().ToList()),
            traderOrder);
    }

    private static PlanObjective ToPlan(ApiObjective o, ObjectiveFacts? facts = null)
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
            bring,
            (o.Wearing ?? []).Select(set => (IReadOnlyList<string>)set.Select(i => i.Id).ToList()).Where(set => set.Count > 0).ToList(),
            o.Type,
            facts?.Targets ?? [],
            facts?.ExitStatus ?? [],
            facts?.Conditions ?? [],
            o.FoundInRaid);
    }

    /// <summary>
    /// Gear for a wear condition, short: "Bomber beanie / RayBench Hipster Reserve sunglasses", or "PACA Soft Armor /
    /// 2 others". Neutral about "and" or "or": the data's sets don't always match the quest's wording, which the
    /// objective text says anyway.
    /// </summary>
    public static string GearText(GameData data, IEnumerable<string> items)
    {
        var names = items.Select(data.ItemName).Distinct().ToList();
        return names.Count <= 2 ? string.Join(" / ", names) : $"{names[0]} / {names.Count - 1} others";
    }

    /// <summary>What an item is brought for, from the objectives that use it: "to plant", "to mark", "to use".</summary>
    private static string Purpose(GameData data, Requirement r)
    {
        if (r.Kind == RequirementKind.Wear)
            return "to wear";
        var item = r.Alternatives[0];
        var uses = r.ForQuests.Select(id => data.Tasks.GetValueOrDefault(id)).OfType<ApiTask>()
            .SelectMany(t => t.Objectives ?? [])
            .Select(o => o.Type switch
            {
                "plantItem" when o.Items?.Contains(item) == true => "to plant",
                "plantQuestItem" when o.QuestItem == item => "to plant",
                "mark" when o.MarkerItem == item => "to mark",
                "useItem" when o.UseAny?.Contains(item) == true => "to use",
                _ => null,
            })
            .OfType<string>()
            .Distinct()
            .ToList();
        return uses.Count > 0 ? string.Join(" and ", uses) : "to bring";
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
        Rows(data, plan.Finish, plan.Map),
        Rows(data, plan.Progress, plan.Map, progress: true),
        plan.Requirements.Select(r => RequirementText(data, r)).ToList(),
        plan.Map.RaidMinutes,
        data.BossesOn(plan.Map.Id).Select(BossText).ToList());

    /// <summary>
    /// What a quest asks on a map in a few words: its objectives there as the planner counts them, shortened by
    /// <see cref="QuestSynopsis"/>. Null when the data isn't in English: the rules are written for English texts.
    /// </summary>
    public static SynopsisLine? Synopsis(GameData data, QuestOnMap quest, PlanMap map)
    {
        if (data.Language != "en" || !data.Tasks.TryGetValue(quest.Quest.Id, out var task))
            return null;
        var objectives = quest.Objectives
            .Select(o => (task.Objectives ?? []).FirstOrDefault(a => a.Id == o.Id) is { } api
                ? new SynopsisObjective(api.Description ?? "", Math.Max(1, api.Count ?? 1), api.Type, RaidPlanner.Places(o, map).Count > 0)
                : null)
            .OfType<SynopsisObjective>();
        var here = map.MapIds.Select(id => data.Maps.GetValueOrDefault(id)?.Name).OfType<string>().ToList();
        return QuestSynopsis.Of(objectives, here, data.Maps.Values.Select(m => m.Name).ToList());
    }

    /// <summary>
    /// Why a PROGRESS row can't be finished on this map, in a few words from the planner's facts (owner, 2026-10-03:
    /// a PROGRESS quest is as much this raid's work as a COMPLETE one, so its row isn't dimmed; it says why instead):
    /// "2 of 5 objectives here", "needs items found in raid", "25 kills in all". Empty when none applies.
    /// </summary>
    public static string ProgressNote(QuestOnMap quest)
    {
        var facts = RaidPlanner.WhyProgress(quest);
        var parts = new List<string>();
        if (facts.Here < facts.InRaid)
            parts.Add($"{facts.Here} of {facts.InRaid} objectives here");
        if (facts.FoundInRaid)
            parts.Add("needs items found in raid");
        if (facts.Kills > 0)
            parts.Add($"{facts.Kills} kills in all");
        return string.Join(" · ", parts);
    }

    /// <summary>One section's rows in the plan's order, each with its effort group; a later group's first row starts a
    /// new group (the hairline between groups). PROGRESS rows also carry why they only progress (a note).</summary>
    public static IReadOnlyList<PlanQuestView> Rows(GameData data, IEnumerable<QuestOnMap> section, PlanMap map, bool progress = false)
    {
        var rows = new List<PlanQuestView>();
        foreach (var q in section)
        {
            var group = QuestEffort.Of(q).Group;
            rows.Add(QuestView(data, q.Quest) with
            {
                Synopsis = Synopsis(data, q, map)?.Text ?? "",
                Group = group,
                StartsGroup = rows.Count > 0 && rows[^1].Group != group,
                Note = progress ? ProgressNote(q) : "",
            });
        }
        return rows;
    }

    private static PlanQuestView QuestView(GameData data, PlanQuest q) =>
        new(q.Id, q.Name, QuestTaxonomy.QuestKind(q.Objectives.Select(o => o.Kind)), data.Tasks.GetValueOrDefault(q.Id)?.Trader);

    public static RequirementView RequirementText(GameData data, Requirement r)
    {
        var names = r.Alternatives.Select(data.ItemName).Distinct().ToList();
        var text = r.Kind == RequirementKind.Wear ? GearText(data, r.Alternatives)
            : names.Count <= 2 ? string.Join(" or ", names) : $"{names[0]} or {names.Count - 1} others";
        if (r.Count > 1)
            text += $" ×{r.Count}";
        var quests = string.Join(", ", r.ForQuests.Select(id => data.Tasks.GetValueOrDefault(id)?.Name ?? id));
        // Says why it is on the list (the study log: gear cards were opened over and over to find out).
        var why = r.Kind == RequirementKind.Key ? $"key for {quests}" : $"{Purpose(data, r)}, for {quests}";
        return new RequirementView(r.Kind, text, quests, r.Alternatives[0], r.ForQuests.ToList(), why);
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
        if (plan.Wear is { Count: > 0 } wear)
            parts.Add("Wear: " + GearText(data, wear.SelectMany(s => s)));
        return parts.Count > 0 ? string.Join(" · ", parts) : null;
    }
}
