using System.Globalization;
using Shturmap.Core.Planning;
using Shturmap.Core.Quests;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Session;

/// <param name="Where">Map names, "any map", or empty for work at the trader.</param>
/// <param name="ItemId">An item the objective is about, to picture next to it.</param>
/// <param name="Live">In a raid on this objective's map: how far and which way from the last fix ("121 m · NE").</param>
public sealed record CardObjective(string QuestId, string ObjectiveId, ObjectiveKind Kind, string Text, string Where, string? ItemId, string Live)
{
    public IReadOnlyList<string> QuestIds => [QuestId];
}

/// <summary>A key or an item to take into the raid for this quest.</summary>
/// <param name="Where">What it is for and where: "to plant · on Streets of Tarkov".</param>
/// <param name="Source">The easiest way to get it ("Prapor LL1 · 18,936 ₽"), or empty.</param>
public sealed record CardNeed(string QuestId, RequirementKind Kind, string ItemId, string Text, string Where, string Source = "")
{
    public IReadOnlyList<string> QuestIds => [QuestId];
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
    public static QuestCardView? Build(GameData data, IReadOnlyDictionary<string, QuestStatus> quests, string questId,
        Func<string, string?>? live = null, ItemSources? sources = null)
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
            objectives.Select(o => Objective(data, task, o, live?.Invoke(o.Id) ?? "")).ToList(),
            Needs(data, task, sources),
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
            ObservationSource.Manual => "set by you",
            _ when status.ImpliedBy is { } later => $"because {later} needs it",
            _ => null,
        };
        return source is null ? state : $"{state} · {source}";
    }

    private static CardObjective Objective(GameData data, ApiTask task, ApiObjective o, string live)
    {
        var kind = QuestTaxonomy.Classify(o.Type);
        var where = MapNames(data, MapIds(o));
        if (where.Length == 0 && QuestTaxonomy.WorksAnywhere(kind))
            where = "any map";
        var text = string.IsNullOrWhiteSpace(o.Description) ? QuestTaxonomy.Label(kind) : o.Description!;
        if (o.Optional)
            text += " (optional)";
        var item = o.Items?.FirstOrDefault() ?? o.QuestItem ?? o.MarkerItem ?? o.UseAny?.FirstOrDefault()
            ?? o.Wearing?.FirstOrDefault()?.FirstOrDefault()?.Id ?? o.UsingWeapon?.FirstOrDefault();
        return new CardObjective(task.Id, o.Id, kind, text, where, item, live);
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

    private static List<CardNeed> Needs(GameData data, ApiTask task, ItemSources? sources)
    {
        var needs = new List<CardNeed>();
        void Add(RequirementKind kind, IReadOnlyList<string> alternatives, int count, IEnumerable<string> maps, string purpose)
        {
            if (alternatives.Count == 0)
                return;
            var names = alternatives.Select(data.ItemName).Distinct().ToList();
            var text = kind switch
            {
                RequirementKind.Wear or RequirementKind.WeaponMods => Planning.GearText(data, alternatives),
                RequirementKind.Weapon => Planning.WeaponText(data, sources, alternatives),
                _ => names.Count <= 2 ? string.Join(" or ", names) : $"{names[0]} or {names.Count - 1} others",
            };
            if (count > 1)
                text += " ×" + count.ToString("N0", CultureInfo.CurrentCulture);
            var where = MapNames(data, maps);
            if (needs.Any(n => n.Text == text))
                return;
            var why = string.Join(" · ", new[] { purpose, where.Length > 0 ? "on " + where : "" }.Where(p => p.Length > 0));
            needs.Add(new CardNeed(task.Id, kind, alternatives[0], text, why, ItemCards.BestOf(data, sources, alternatives)));
        }

        foreach (var (o, plan) in (task.Objectives ?? []).Zip(Planning.ToPlan(task, data).Objectives))
        {
            var maps = (o.Maps ?? []).Concat((o.Zones ?? []).Select(z => z.Map)).OfType<string>().ToList();
            foreach (var keys in plan.Keys)
                Add(RequirementKind.Key, keys, 1, maps, "key");
            foreach (var (item, count) in plan.Bring)
                Add(RequirementKind.Bring, [item], count, maps, o.Type switch
                {
                    "plantItem" or "plantQuestItem" => "to plant",
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
        foreach (var needed in task.NeededKeys ?? [])
            foreach (var key in needed.Keys ?? [])
                Add(RequirementKind.Key, [key], 1, needed.Map is null ? [] : [needed.Map], "key");
        return needs;
    }
}
