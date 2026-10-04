using Shturmap.Core;
using Shturmap.Core.Planning;
using Shturmap.Core.Quests;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Session;

/// <summary>Something a raid needs, ready to show: "Dorm room 114 key", "MS2000 Marker ×3", for which quests.</summary>
/// <param name="ItemId">The item to picture (the first of the alternatives).</param>
/// <param name="Why">What it is for, then for which quests: "to mark, for Revision", "key for Ballet Lover",
/// "to use, for Wet Job", "to leave through Klimov Street (Flare), for Cease Fire!".</param>
/// <param name="Alternatives">Every item that will do (a weapon class's members), for naming the class once the item
/// categories are loaded (<see cref="Planning.WeaponText"/>).</param>
public sealed record RequirementView(RequirementKind Kind, string Text, string ForQuests, string ItemId, IReadOnlyList<string> QuestIds, string Why = "",
    IReadOnlyList<string>? Alternatives = null);

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
    /// <param name="picks">The quests picked for the coming raid: maps with picks come first, most picks first, the
    /// player's plan before the planner's (owner, 2026-10-03), also a map the planner ranks below its best four
    /// (<see cref="RaidPlanner.Rank"/>); otherwise the planner's order.</param>
    public static IReadOnlyList<MapPlanView> Suggest(GameData data, IEnumerable<string> activeQuestIds, IReadOnlySet<string>? picks = null) =>
        RaidPlanner.Rank(Quests(data, activeQuestIds), Maps(data), picks: picks).Select(p => ToView(data, p)).ToList();

    /// <summary>A plan card's quest sections with the picks taken out to their own group on top (<see cref="Sections"/>).</summary>
    /// <param name="Picks">The picked quests on this map: those that can be completed first, then those that only
    /// progress (with their note), each in the planner's effort order.</param>
    public sealed record PlanSections(IReadOnlyList<PlanQuestView> Picks, IReadOnlyList<PlanQuestView> Finish, IReadOnlyList<PlanQuestView> Progress);

    /// <summary>
    /// The quests of a plan card for the rail: the picks first, in a group of their own, then COMPLETE and PROGRESS
    /// without them (owner, 2026-10-03: the quests you want to tackle first). The effort hairlines are drawn anew for
    /// what is left; the picks, a short list chosen by hand, get none. Without picks, the sections as planned.
    /// </summary>
    public static PlanSections Sections(MapPlanView plan, IReadOnlySet<string> picks)
    {
        if (picks.Count == 0)
            return new(Array.Empty<PlanQuestView>(), plan.Finish, plan.Progress);
        static IReadOnlyList<PlanQuestView> Regroup(IEnumerable<PlanQuestView> rows)
        {
            var list = new List<PlanQuestView>();
            foreach (var row in rows)
                list.Add(row with { StartsGroup = list.Count > 0 && list[^1].Group != row.Group });
            return list;
        }
        var picked = plan.Finish.Where(q => picks.Contains(q.QuestId))
            .Concat(plan.Progress.Where(q => picks.Contains(q.QuestId)))
            .Select(q => q with { StartsGroup = false })
            .ToList();
        return new(picked, Regroup(plan.Finish.Where(q => !picks.Contains(q.QuestId))), Regroup(plan.Progress.Where(q => !picks.Contains(q.QuestId))));
    }

    /// <summary>A BRING row in the rail's order (<see cref="BringOrder"/>).</summary>
    /// <param name="ForPicks">It serves at least one picked quest.</param>
    /// <param name="StartsOthers">The first row that serves no pick, after rows that do: a hairline goes above it.</param>
    public sealed record BringRow(RequirementView Row, bool ForPicks, bool StartsOthers);

    /// <summary>What to bring, with what the picks need first and a hairline before the rest; otherwise as planned.
    /// What it takes to enter the map comes before everything: without it there is no raid.</summary>
    public static IReadOnlyList<BringRow> BringOrder(IReadOnlyList<RequirementView> rows, IReadOnlySet<string> picks)
    {
        var entry = rows.Where(r => r.Kind == RequirementKind.Entry).ToList();
        var forPicks = rows.Where(r => r.Kind != RequirementKind.Entry && r.QuestIds.Any(picks.Contains)).ToList();
        var others = rows.Where(r => r.Kind != RequirementKind.Entry && !r.QuestIds.Any(picks.Contains)).ToList();
        return entry.Select(r => new BringRow(r, false, false))
            .Concat(forPicks.Select(r => new BringRow(r, true, false)))
            .Concat(others.Select((r, i) => new BringRow(r, false, i == 0 && forPicks.Count > 0)))
            .ToList();
    }

    /// <summary>The kit reminder while a raid loads (<see cref="Kit"/>).</summary>
    /// <param name="Main">What to check first: what it takes to get in (the entry item) or out (the exits quests name:
    /// a flare, climbing gear, money), then what the picks need; without picks on this map, all of it.</param>
    /// <param name="More">With picks on this map, what the other quests need: "also useful", quieter.</param>
    public sealed record KitList(IReadOnlyList<RequirementView> Main, IReadOnlyList<RequirementView> More)
    {
        public static KitList Empty { get; } = new([], []);

        public int Count => Main.Count + More.Count;

        public IEnumerable<RequirementView> All => Main.Concat(More);
    }

    /// <summary>
    /// What to bring for a map, as a reminder while its raid loads and matching can still be cancelled (owner,
    /// 2026-10-03: "it could still be a good reminder what to bring … with icon previews"): every active quest's BRING
    /// row, what gets you in or out first, then the picks' rows, then the rest. Shturmap knows no inventory, so it is a
    /// list to check, never a claim that something is missing.
    /// </summary>
    public static KitList Kit(MapPlanView? plan, IReadOnlySet<string> picks)
    {
        if (plan is null)
            return KitList.Empty;
        var rows = plan.Requirements;
        var essential = rows.Where(r => r.Kind == RequirementKind.Entry).Concat(rows.Where(r => r.Kind == RequirementKind.Exit)).ToList();
        var rest = rows.Where(r => r.Kind is not (RequirementKind.Entry or RequirementKind.Exit)).ToList();
        var picksHere = plan.Finish.Concat(plan.Progress).Select(q => q.QuestId).Where(picks.Contains).ToHashSet();
        if (picksHere.Count == 0)
            return new([.. essential, .. rest], []);
        return new([.. essential, .. rest.Where(r => r.QuestIds.Any(picksHere.Contains))], [.. rest.Where(r => !r.QuestIds.Any(picksHere.Contains))]);
    }

    /// <summary>The kit while the raid loads, and nothing otherwise: before loading the Plan card's BRING says it, once
    /// the raid starts the raid card's BRING does; a load cancelled goes back to the menus. A Scav (known early from a
    /// server-hosted raid's setup) brings nothing for quests, whose objectives don't count for it.</summary>
    public static KitList KitWhileLoading(Core.Raid.RaidState raid, MapPlanView? plan, IReadOnlySet<string> picks) =>
        raid.Phase == Core.Raid.RaidPhase.Loading && raid.Side != Core.Raid.RaidSide.Scav ? Kit(plan, picks) : KitList.Empty;

    /// <summary>The big cue's row of item pictures: the first <paramref name="shown"/> items in the kit's order, and
    /// how many more there are ("+3").</summary>
    public static (IReadOnlyList<RequirementView> Shown, int More) CueKit(KitList kit, int shown = 6)
    {
        var all = kit.All.ToList();
        return (all.Take(shown).ToList(), Math.Max(0, all.Count - shown));
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
            (task.Objectives ?? []).Select(o => ToPlan(o, data?.ObjectiveFacts.GetValueOrDefault(o.Id), data)).ToList(),
            (task.NeededKeys ?? []).Where(k => k.Map is not null)
                .GroupBy(k => k.Map!)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.SelectMany(k => k.Keys ?? []).Distinct().ToList()),
            traderOrder);
    }

    private static PlanObjective ToPlan(ApiObjective o, ObjectiveFacts? facts = null, GameData? data = null)
    {
        // The exit an extract objective names, and what leaving there takes (owner, 2026-10-03: Cease Fire! didn't say
        // a flare was needed). The objective names the exit by the game's internal name; so do the map's extracts.
        var exit = data is not null && facts?.Exit is { } exitKey ? ExitOf(data, o, exitKey) : null;
        IReadOnlyList<(string ItemId, int Count)> exitItems = exit is null || data is null ? [] : ExtractRules.Items(data, exit);
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
            o.FoundInRaid,
            (o.UsingWeapon ?? []).Distinct().ToList(),
            (o.UsingWeaponMods ?? []).Where(set => set.Count > 0).Select(set => (IReadOnlyList<string>)set.Distinct().ToList()).ToList(),
            (o.NotWearing ?? []).Select(i => i.Id).Distinct().ToList(),
            exitItems.Count > 0 ? exit!.Name ?? facts!.Exit : null,
            exitItems);
    }

    // The extract an objective's exit name stands for: on the objective's own maps first, then on any map.
    private static ApiExtract? ExitOf(GameData data, ApiObjective o, string exitKey)
    {
        var maps = (o.Maps ?? []).Select(id => data.Maps.GetValueOrDefault(id)).OfType<ApiMap>().Concat(data.Maps.Values);
        return maps.SelectMany(m => m.Extracts ?? [])
            .FirstOrDefault(e => string.Equals(data.ExtractKeys.GetValueOrDefault(e.Id), exitKey, StringComparison.Ordinal));
    }

    /// <summary>
    /// A kill objective's weapons, in as few words as the data allows (owner, 2026-10-03: what to bring for a quest must
    /// be clear). tarkov.dev arrives with a class as all its members, so the weapons are grouped by their item category,
    /// biggest group first, and said in at most three parts:
    /// <list type="bullet">
    /// <item>a group that is most of its category, or too many weapons to name (more than ten), by its category: "Any
    /// sniper rifle", "Shotgun (13 of 16 kinds)", "Assault rifle (15 of 53 kinds)"; the rest weapon by weapon: "Any
    /// sniper rifle or MP-18 7.62x54R single-shot rifle";</item>
    /// <item>if that takes more than three parts and the weapons span several categories, every group of two or more by
    /// its category: "Any handgun or revolver (3 of 5 kinds)";</item>
    /// <item>otherwise, and for a few weapons of one category, the weapons themselves, so there is a gun to name:
    /// "AK-12 assault rifle", "Colt M4A1 or 5 others".</item>
    /// </list>
    /// "Any" only when the list is the whole category; a count says how many kinds of it will do. Without the item
    /// categories (they load after the rest), the list form.
    /// </summary>
    public static string WeaponText(GameData data, ItemSources? sources, IReadOnlyList<string> weapons)
    {
        var distinct = weapons.Distinct().ToList();
        if (distinct.Count > 2 && sources is not null && ClassText(data, sources, distinct) is { } named)
            return named;
        var names = distinct.Select(data.ItemName).Distinct().ToList();
        return names.Count <= 2 ? string.Join(" or ", names) : $"{names[0]} or {names.Count - 1} others";
    }

    // A group is named by its category when it holds at least three quarters of the category's members, or when it has
    // more weapons than are worth naming one by one.
    private const double ClassShare = 0.75;
    private const int MostNamed = 10;
    private const int MostParts = 3;

    private static string? ClassText(GameData data, ItemSources sources, List<string> weapons)
    {
        var groups = weapons.GroupBy(id => sources.Items.GetValueOrDefault(id)?.Categories?.FirstOrDefault())
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => (Category: g.Key, Weapons: g.ToList(), Members: g.Key is null ? 0 : sources.Members[g.Key].Count()))
            .ToList();

        // Read in this order: whole categories ("any handgun"), then parts of one ("revolver (3 of 5 kinds)"), then
        // single weapons; within each, the bigger group first.
        string? Parts(Func<int, int, bool> byCategory)
        {
            var whole = new List<string>();
            var some = new List<string>();
            var single = new List<string>();
            foreach (var (category, list, members) in groups)
            {
                if (category is not null && list.Count >= 2 && byCategory(list.Count, members))
                {
                    var name = Lower(data.ItemName(category));
                    if (list.Count >= members)
                        whole.Add($"any {name}");
                    else
                        some.Add($"{name} ({list.Count} of {members} kinds)");
                }
                else
                {
                    single.AddRange(list.Select(data.ItemName));
                }
                if (whole.Count + some.Count + single.Count > MostParts)
                    return null;
            }
            return whole.Count + some.Count > 0 ? Capital(string.Join(" or ", whole.Concat(some).Concat(single))) : null;
        }

        return Parts((count, members) => count >= ClassShare * members || count > MostNamed)
               ?? (groups.Count > 1 ? Parts((_, _) => true) : null);
    }

    /// <summary>
    /// The gear a kill objective forbids, short: the item categories it falls in when the categories are loaded
    /// ("armor, headwear"), else "6B43 Zabralo-Sh body armor (EMR) / 54 others".
    /// </summary>
    public static string WithoutText(GameData data, ItemSources? sources, IReadOnlyList<string> items)
    {
        if (sources is not null)
        {
            var kinds = items.Select(id => sources.Items.GetValueOrDefault(id)?.Categories?.FirstOrDefault())
                .OfType<string>().Distinct().Select(c => Lower(data.ItemName(c))).ToList();
            if (kinds.Count is > 0 and <= 3)
                return string.Join(", ", kinds);
        }
        return GearText(data, items);
    }

    // "Sniper rifle" → "sniper rifle"; a name that is all capitals ("SMG") stays as it is.
    private static string Lower(string name) => name.Length > 1 && char.IsLower(name[1]) ? char.ToLowerInvariant(name[0]) + name[1..] : name;

    private static string Capital(string name) => name.Length > 0 ? char.ToUpperInvariant(name[0]) + name[1..] : name;

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

    /// <summary>What an item is brought for, from the objectives that use it: "to plant", "to mark", "to use", "to fit",
    /// "to wear", "to leave through Klimov Street (Flare)".</summary>
    private static string Purpose(GameData data, Requirement r)
    {
        switch (r.Kind)
        {
            case RequirementKind.Wear:
                return "to wear";
            case RequirementKind.Weapon:
                return "to use";
            case RequirementKind.WeaponMods:
                return "to fit";
            case RequirementKind.Exit:
                return "to leave through " + r.Exit;
            case RequirementKind.Entry:
                return "to enter " + r.Enter;
        }
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
                return new PlanMap(primary.Id, primary.Name, g.Select(m => m.Map.Id).ToHashSet(), primary.RaidDuration ?? 0,
                    g.SelectMany(m => m.Map.AccessKeys ?? []).Distinct().ToList());
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

    /// <param name="sources">The item categories, to name a weapon class (<see cref="WeaponText"/>); may be null.</param>
    public static RequirementView RequirementText(GameData data, Requirement r, ItemSources? sources = null)
    {
        var names = r.Alternatives.Select(data.ItemName).Distinct().ToList();
        var text = r.Kind switch
        {
            RequirementKind.Wear or RequirementKind.WeaponMods => GearText(data, r.Alternatives),
            RequirementKind.Weapon => WeaponText(data, sources, r.Alternatives),
            _ => names.Count <= 2 ? string.Join(" or ", names) : $"{names[0]} or {names.Count - 1} others",
        };
        if (r.Count > 1)
            text += " ×" + r.Count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture);
        var quests = string.Join(", ", r.ForQuests.Select(id => data.Tasks.GetValueOrDefault(id)?.Name ?? id));
        // Says why it is on the list (the study log: gear cards were opened over and over to find out).
        var why = r.Kind == RequirementKind.Key ? $"key for {quests}" : r.ForQuests.Count == 0 ? Purpose(data, r) : $"{Purpose(data, r)}, for {quests}";
        return new RequirementView(r.Kind, text, quests, r.Alternatives[0], r.ForQuests.ToList(), why, r.Alternatives.ToList());
    }

    /// <summary>"Key: X · Bring: Y · Use: Z · Without: armor" for one objective on one map, or null if it needs nothing.</summary>
    /// <param name="hasPlace">Quest-level keys are shown only on objectives with a place, where the key is used.</param>
    /// <param name="sources">The item categories, to name a weapon class and forbidden gear; may be null.</param>
    public static string? Needs(GameData data, ApiTask task, ApiObjective objective, IReadOnlySet<string> mapIds, bool hasPlace,
        ItemSources? sources = null)
    {
        var plan = ToPlan(objective, data.ObjectiveFacts.GetValueOrDefault(objective.Id), data);
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
        if (plan.Weapons is { Count: > 0 } weapons)
            parts.Add("Use: " + WeaponText(data, sources, weapons));
        if (plan.Mods is { Count: > 0 } mods)
            parts.Add("Fit: " + GearText(data, mods.SelectMany(s => s).Distinct()));
        if (plan.ExitItems is { Count: > 0 } exitItems)
            parts.Add("Bring: " + string.Join(", ", exitItems.Select(b => data.ItemName(b.ItemId) + (b.Count > 1 ? " ×" + b.Count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture) : ""))));
        // Gear the kills forbid isn't something to bring: it is said here, on the objective, only (owner, 2026-10-03).
        if (plan.NotWearing is { Count: > 0 } without)
            parts.Add("Without: " + WithoutText(data, sources, without));
        return parts.Count > 0 ? string.Join(" · ", parts) : null;
    }
}
