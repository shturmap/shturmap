using System.Globalization;
using Shturmap.Core.Planning;
using Shturmap.Core.Quests;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Session;

/// <param name="Where">Map names, "any map", or empty for work at the trader.</param>
/// <param name="ItemId">An item the objective is about, to picture next to it.</param>
/// <param name="Live">In a raid on this objective's map: how far and which way from the last fix ("121 m · NE"); empty
/// for an objective ticked as done.</param>
/// <param name="Done">The player ticked it as done (<see cref="ObjectiveTicks"/>).</param>
/// <param name="Ticked">Where "done" comes from, in the place of <paramref name="Live"/>: "Done · ticked by you, 4 Oct".</param>
/// <param name="Tickable">Its tick box shows: the quest is active, or the objective is ticked (a tick can always be
/// taken back).</param>
/// <param name="Handover">What its hand-over mark's tooltip says ("Hand over ×3 to Therapist"), when a hand-over of the
/// quest gives what this objective gets (<see cref="Handovers"/>); empty otherwise.</param>
public sealed record CardObjective(string QuestId, string ObjectiveId, ObjectiveKind Kind, string Text, string Where, string? ItemId, string Live,
    bool Done = false, string Ticked = "", bool Tickable = false, string Handover = "")
{
    public IReadOnlyList<string> QuestIds => [QuestId];

    /// <summary>How many of what it gets go to the trader after the raid; 0 without a hand-over.</summary>
    public int HandoverCount { get; init; }

    /// <summary>The trader it goes to (the quest's), pictured in the hand-over mark; null without a hand-over.</summary>
    public string? HandoverTraderId { get; init; }

    /// <summary>What the map's "?" means for it: "One of 4 places it can be"; empty where it has one place, or is done.</summary>
    public string Possible { get; init; } = "";
}

/// <summary>A key or an item to take into the raid for this quest.</summary>
/// <param name="Where">What it is for and where: "to plant · on Streets of Tarkov".</param>
/// <param name="Source">The easiest way to get it ("Prapor LL1 · 18,936 ₽"), or empty.</param>
public sealed record CardNeed(string QuestId, RequirementKind Kind, string ItemId, string Text, string Where, string Source = "")
{
    public IReadOnlyList<string> QuestIds => [QuestId];

    /// <summary>
    /// Every item the row stands for ("A or B", gear worn together, a weapon class): it is each of them for the linked
    /// highlight, though it pictures <see cref="ItemId"/>, the first. One item for a plain row.
    /// </summary>
    public IReadOnlyList<string> Alternatives { get; init; } = [ItemId];
}

/// <summary>A quest named on a card (e.g. one this quest unlocks), to point at for its own card.</summary>
public sealed record CardQuest(string QuestId, string Name, ObjectiveKind Kind, string? TraderId);

/// <summary>Everything the quest card shows: who, what, where, what to bring, and why Shturmap thinks it's active.</summary>
/// <param name="Facts">"Prapor · from level 10 · needed for Kappa".</param>
/// <param name="Status">"Active · from the game log, 26 Sep": where the state came from (docs/DESIGN.md §4.6).</param>
/// <param name="UnlocksMore">"and 3 more" when not all unlocked quests are listed, or empty.</param>
public sealed record QuestCardView(
    string QuestId,
    string Name,
    ObjectiveKind Kind,
    string? TraderId,
    string Facts,
    string Status,
    QuestState State,
    IReadOnlyList<CardObjective> Objectives,
    IReadOnlyList<CardNeed> Needs,
    IReadOnlyList<CardQuest> Unlocks,
    string UnlocksMore,
    string? WikiLink)
{
    public IReadOnlyList<string> QuestIds => [QuestId];
}

public static class QuestCards
{
    private const int ShownUnlocks = 5;

    /// <param name="live">Distance and direction for an objective id, in a raid on its map; null or empty otherwise.</param>
    /// <param name="sources">Where items come from, for one line under each thing to bring.</param>
    /// <param name="ticks">The objectives the player ticked as done, each with its day (<see cref="ObjectiveTicks"/>):
    /// they read as done and say why, and BRING lists only what the open objectives need.</param>
    public static QuestCardView? Build(GameData data, IReadOnlyDictionary<string, QuestStatus> quests, string questId,
        Func<string, string?>? live = null, ItemSources? sources = null, IReadOnlyDictionary<string, DateOnly>? ticks = null)
    {
        if (!data.Tasks.TryGetValue(questId, out var task))
            return null;
        var status = quests.GetValueOrDefault(questId);
        var objectives = task.Objectives ?? [];

        var facts = new List<string> { data.TraderName(task.Trader) };
        if (task.MinPlayerLevel is > 1 and var level)
            facts.Add($"from level {level}");
        if (task.KappaRequired)
            facts.Add("needed for Kappa");
        if (task.LightkeeperRequired)
            facts.Add("needed for Lightkeeper");

        var unlocks = data.Tasks.Values
            .Where(t => t.TaskRequirements?.Any(r => r.Task == questId) == true)
            .OrderBy(t => t.Name, StringComparer.CurrentCulture)
            .Select(t => Reference(data, t))
            .ToList();

        return new QuestCardView(
            task.Id,
            task.Name,
            KindOf(task),
            task.Trader,
            string.Join(" · ", facts.Where(f => f.Length > 0)),
            StatusText(status),
            status?.State ?? QuestState.NotStarted,
            // A hand-over of what another objective gets is that objective's mark, not a row of its own.
            objectives.Where(o => !Handovers.Folds(task, o.Id))
                .Select(o => Objective(data, task, o, live?.Invoke(o.Id) ?? "", status?.State == QuestState.Active, ticks)).ToList(),
            Needs(data, task, sources, quests, ticks),
            unlocks.Take(ShownUnlocks).ToList(),
            unlocks.Count > ShownUnlocks ? $"and {unlocks.Count - ShownUnlocks} more" : "",
            task.WikiLink);
    }

    internal static CardQuest Reference(GameData data, ApiTask task) => new(task.Id, task.Name, KindOf(task), task.Trader);

    internal static ObjectiveKind KindOf(ApiTask task) =>
        QuestTaxonomy.QuestKind((task.Objectives ?? []).Select(o => QuestTaxonomy.Classify(o.Type)));

    private static string StatusText(QuestStatus? status)
    {
        if (status is null)
            return "Not started";
        var state = status.State switch
        {
            QuestState.Active => "Active",
            QuestState.Completed => "Completed",
            QuestState.Failed => "Failed",
            _ => "Not started",
        };
        var day = status.At?.ToString("d MMM", CultureInfo.CurrentCulture);
        var source = status.Source switch
        {
            ObservationSource.Log => $"from the game log, {day}",
            _ when status.ImpliedBy is { } later => $"because {later} needs it",
            _ => null,
        };
        return source is null ? state : $"{state} · {source}";
    }

    /// <summary>"Done · ticked by you, 4 Oct": where an objective's "done" comes from (docs/DESIGN.md §4, "Says why").</summary>
    public static string TickedText(DateOnly day) =>
        day == default ? "Done · ticked by you" : $"Done · ticked by you, {day.ToString("d MMM", CultureInfo.CurrentCulture)}";

    private static CardObjective Objective(GameData data, ApiTask task, ApiObjective o, string live, bool active, IReadOnlyDictionary<string, DateOnly>? ticks)
    {
        var kind = QuestTaxonomy.Classify(o.Type);
        var where = MapNames(data, MapIds(o));
        if (where.Length == 0 && QuestTaxonomy.WorksAnywhere(kind))
            where = "any map";
        var text = string.IsNullOrWhiteSpace(o.Description) ? QuestTaxonomy.Label(kind) : o.Description!;
        if (SaysWhere(data, text, where))
            where = "";
        if (Handovers.Optional(task, o))
            text += " (optional)";
        var item = ItemOf(o);
        var handover = HandoverText(data, task, o.Id);
        var count = HandoverCount(task, o.Id);
        // A ticked objective isn't measured any more: where its distance stood, it says that it is done and why. Its
        // hand-over mark stays: what was got still goes to the trader.
        var row = ticks is not null && ticks.TryGetValue(o.Id, out var day)
            ? new CardObjective(task.Id, o.Id, kind, text, where, item, "", Done: true, Ticked: TickedText(day), Tickable: true, Handover: handover)
            : new CardObjective(task.Id, o.Id, kind, text, where, item, live, Tickable: active, Handover: handover);
        // Where its thing can be at several places, the card says what the map's "?" on each of them means.
        var places = Shturmap.Map.MapContentBuilder.PlacesItCanBe(o);
        return row with
        {
            HandoverCount = count,
            HandoverTraderId = count > 0 ? task.Trader : null,
            Possible = places > 1 && !row.Done ? $"One of {places} places it can be" : "",
        };
    }

    /// <summary>The item an objective is about, to picture beside it: what it finds, picks up, marks, uses or wears.</summary>
    public static string? ItemOf(ApiObjective o) =>
        o.Items?.FirstOrDefault() ?? o.QuestItem ?? o.MarkerItem ?? o.UseAny?.FirstOrDefault()
        ?? o.Wearing?.FirstOrDefault()?.FirstOrDefault()?.Id ?? o.UsingWeapon?.FirstOrDefault();

    /// <summary>
    /// How many of what an objective gets go to the quest's trader after the raid, when a hand-over of the quest gives
    /// it (<see cref="Handovers"/>); 0 otherwise.
    /// </summary>
    public static int HandoverCount(ApiTask task, string objectiveId) =>
        Handovers.HandoverOf(task, objectiveId) is { } handover ? Math.Max(1, handover.Count ?? 1) : 0;

    /// <summary>
    /// What the hand-over mark on an objective says in its tooltip, when a hand-over of the quest gives what it gets
    /// (<see cref="Handovers"/>): "Hand over ×3 to Therapist". Empty otherwise.
    /// </summary>
    public static string HandoverText(GameData data, ApiTask task, string objectiveId)
    {
        var count = HandoverCount(task, objectiveId);
        return count == 0 ? "" : $"Hand over{(count > 1 ? $" ×{count}" : "")} to {data.TraderName(task.Trader)}";
    }

    /// <summary>
    /// Whether an objective's own text already says where it is: then the line of map names under it would say the
    /// same again ("… on Streets of Tarkov" over "Streets of Tarkov"; the review of 2026-10-04, C2) and is left out.
    /// It does when the sentence names one of the maps as a place, alone or in a list ("on Woods, Ground Zero, or
    /// Customs": <see cref="QuestSynopsis.MapList"/>, which doesn't take "at Factory gate" for the map), and every
    /// name the line would show stands in the text. A text that names only some of its maps keeps the whole line.
    /// The pattern knows English prepositions: in another game language both lines stay.
    /// </summary>
    internal static bool SaysWhere(GameData data, string text, string where)
    {
        var shown = where.Split(", ", StringSplitOptions.RemoveEmptyEntries);
        if (shown.Length == 0 || !shown.All(name => text.Contains(name, StringComparison.OrdinalIgnoreCase)))
            return false;
        return QuestSynopsis.MapList(shown, data.Maps.Values.Select(m => m.Name).ToList())?.IsMatch(text) == true;
    }

    internal static IEnumerable<string> MapIds(ApiObjective o) =>
        (o.Maps ?? [])
            .Concat((o.Zones ?? []).Select(z => z.Map))
            .Concat((o.PossibleLocations ?? []).Select(l => l.Map))
            .OfType<string>();

    // Variants drawn with the same artwork (Ground Zero and Ground Zero 21+) read as one map.
    internal static string MapNames(GameData data, IEnumerable<string> mapIds) =>
        string.Join(", ", mapIds
            .Select(id => data.Maps.GetValueOrDefault(id))
            .OfType<ApiMap>()
            .GroupBy(m => data.DefinitionFor(m.NormalizedName)?.Key ?? m.NormalizedName)
            .Select(g => g.MinBy(m => m.Name.Length)!.Name)
            .Distinct()
            .Order(StringComparer.CurrentCulture));

    // One thing the quest takes, gathered over its objectives.
    private sealed class Need(RequirementKind kind, IReadOnlyList<string> alternatives)
    {
        public RequirementKind Kind => kind;

        public IReadOnlyList<string> Alternatives => alternatives;

        public int Count { get; set; }

        public List<string> Maps { get; } = [];

        public List<string> Purposes { get; } = [];
    }

    // One row per thing to take, as Plan's BRING counts it (RaidPlanner's requirements): an item used up is added up
    // across the quest's objectives, and a key or item needed on two maps names both. One row per objective, dropped
    // when its text was already there, lost both (the review of 2026-10-04: three "mark" objectives read "MS2000
    // Marker" where Plan said "×3", and a key needed on two maps named the first).
    // An objective ticked as done needs nothing brought any more, and a quest-level key counts only for a map where an
    // open objective is left.
    private static List<CardNeed> Needs(GameData data, ApiTask task, ItemSources? sources, IReadOnlyDictionary<string, QuestStatus> quests,
        IReadOnlyDictionary<string, DateOnly>? ticks = null)
    {
        bool Done(ApiObjective o) => ticks?.ContainsKey(o.Id) == true;
        var anyDone = (task.Objectives ?? []).Any(Done);
        var openMaps = (task.Objectives ?? []).Where(o => !Done(o)).SelectMany(MapIds).ToHashSet(StringComparer.Ordinal);
        var rows = new List<Need>();
        var byKey = new Dictionary<string, Need>(StringComparer.Ordinal);
        void Add(RequirementKind kind, IReadOnlyList<string> alternatives, int count, IEnumerable<string> maps, string purpose)
        {
            if (alternatives.Count == 0)
                return;
            // The same thing again: the same items for the same kind of use. An exit's items are per exit, and what is
            // brought to be used up is one row whatever it is done with ("to plant and to mark").
            var key = $"{kind}:{string.Join("|", alternatives.Order(StringComparer.Ordinal))}{(kind == RequirementKind.Exit ? ":" + purpose : "")}";
            if (!byKey.TryGetValue(key, out var need))
            {
                byKey[key] = need = new Need(kind, alternatives);
                rows.Add(need);
            }
            // Used up each time it is planted or marked: the counts add up. A key, gear and a weapon are taken once,
            // and an exit asks the same whichever objective names it.
            need.Count = kind == RequirementKind.Bring ? need.Count + count : Math.Max(need.Count, count);
            need.Maps.AddRange(maps.Where(m => !need.Maps.Contains(m)).Distinct().ToList());
            if (!need.Purposes.Contains(purpose))
                need.Purposes.Add(purpose);
        }

        foreach (var (o, plan) in (task.Objectives ?? []).Zip(Planning.ToPlan(task, data).Objectives))
        {
            if (Done(o))
                continue;
            var maps = (o.Maps ?? []).Concat((o.Zones ?? []).Select(z => z.Map)).OfType<string>().ToList();
            foreach (var keys in plan.Keys)
                Add(RequirementKind.Key, keys, 1, maps, "key");
            foreach (var (item, count) in plan.Bring)
                Add(RequirementKind.Bring, [item], count, maps, o.Type switch
                {
                    "plantItem" => "to plant",
                    "mark" => "to mark",
                    "useItem" => "to use",
                    _ => "to bring",
                });
            if (plan.Wear is { Count: > 0 } wear)
                Add(RequirementKind.Wear, wear.SelectMany(s => s).Distinct().ToList(), 1, maps, "to wear");
            if (plan.Weapons is { Count: > 0 } weapons)
                Add(RequirementKind.Weapon, weapons, 1, maps, "to use");
            if (plan.Mods is { Count: > 0 } mods)
                Add(RequirementKind.WeaponMods, mods.SelectMany(s => s).Distinct().ToList(), 1, maps, "to fit");
            foreach (var (item, count) in plan.ExitItems ?? [])
                Add(RequirementKind.Exit, [item], count, maps, "to leave through " + plan.Exit);
        }
        // A key an objective names is that objective's row above, with its alternatives ("306 or 308"); the quest's own
        // list repeats those keys one by one, so only a key no objective names is added from it (Planning.QuestOnlyKeys;
        // the review of 2026-10-04, A33: "306 or 308" stood beside "306" and "308").
        var named = (task.Objectives ?? []).SelectMany(o => o.RequiredKeys ?? []).SelectMany(k => k).ToHashSet(StringComparer.Ordinal);
        foreach (var needed in task.NeededKeys ?? [])
        {
            if (anyDone && needed.Map is not null && !openMaps.Contains(needed.Map))
                continue;
            foreach (var key in (needed.Keys ?? []).Where(k => !named.Contains(k)))
                Add(RequirementKind.Key, [key], 1, needed.Map is null ? [] : [needed.Map], "key");
        }

        var needs = new List<CardNeed>();
        foreach (var need in rows)
        {
            var names = need.Alternatives.Select(data.ItemName).Distinct().ToList();
            var text = need.Kind switch
            {
                RequirementKind.Wear or RequirementKind.WeaponMods => Planning.GearText(data, need.Alternatives),
                RequirementKind.Weapon => Planning.WeaponText(data, sources, need.Alternatives),
                _ => names.Count <= 2 ? string.Join(" or ", names) : $"{names[0]} or {names.Count - 1} others",
            };
            if (need.Count > 1)
                text += " ×" + need.Count.ToString("N0", CultureInfo.CurrentCulture);
            var where = MapNames(data, need.Maps);
            var why = string.Join(" · ", new[] { string.Join(" and ", need.Purposes), where.Length > 0 ? "on " + where : "" }.Where(p => p.Length > 0));
            // Two lists that read the same (a weapon class given twice with one preset more) stay one row.
            if (!needs.Any(n => n.Kind == need.Kind && n.Text == text && n.Where == why))
            {
                needs.Add(new CardNeed(task.Id, need.Kind, need.Alternatives[0], text, why, ItemCards.BestOf(data, sources, need.Alternatives, quests))
                {
                    Alternatives = need.Alternatives.Distinct(StringComparer.Ordinal).ToList(),
                });
            }
        }
        return needs;
    }
}
