using System.Globalization;
using Spotter.Core.Planning;
using Spotter.Core.Quests;
using Spotter.Data.TarkovDev;

namespace Spotter.Session;

/// <param name="Where">Map names, "any map", or empty for work at the trader.</param>
/// <param name="ItemId">An item the objective is about, to picture next to it.</param>
public sealed record CardObjective(ObjectiveKind Kind, string Text, string Where, string? ItemId);

/// <summary>A key or an item to take into the raid for this quest.</summary>
public sealed record CardNeed(RequirementKind Kind, string ItemId, string Text, string Where);

/// <summary>Everything the quest card shows: who, what, where, what to bring, and why Spotter thinks it's active.</summary>
/// <param name="Facts">"Prapor · from level 10 · needed for Kappa".</param>
/// <param name="Status">"Active · from the game log, 26 Sep": where the state came from (docs/DESIGN.md §4.6).</param>
/// <param name="Unlocks">"Unlocks Big Customer, Chemical - Part 2", or empty.</param>
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
    string Unlocks,
    string? WikiLink);

public static class QuestCards
{
    public static QuestCardView? Build(GameData data, IReadOnlyDictionary<string, QuestStatus> quests, string questId)
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
            .Select(t => t.Name)
            .Order(StringComparer.CurrentCulture)
            .ToList();

        return new QuestCardView(
            task.Id,
            task.Name,
            QuestTaxonomy.QuestKind(objectives.Select(o => QuestTaxonomy.Classify(o.Type))),
            task.Trader,
            string.Join(" · ", facts.Where(f => f.Length > 0)),
            StatusText(status),
            status?.State ?? QuestState.NotStarted,
            objectives.Select(o => Objective(data, o)).ToList(),
            Needs(data, task),
            unlocks.Count switch
            {
                0 => "",
                <= 4 => "Unlocks " + string.Join(", ", unlocks),
                _ => $"Unlocks {string.Join(", ", unlocks.Take(3))} and {unlocks.Count - 3} more",
            },
            task.WikiLink);
    }

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
            ObservationSource.TasksScan => $"from a Tasks screenshot, {day}",
            ObservationSource.Import => "imported from TarkovEyes",
            ObservationSource.Manual => "set by you",
            _ when status.ImpliedBy is { } later => $"because {later} needs it",
            _ => null,
        };
        return source is null ? state : $"{state} · {source}";
    }

    private static CardObjective Objective(GameData data, ApiObjective o)
    {
        var kind = QuestTaxonomy.Classify(o.Type);
        var mapIds = (o.Maps ?? [])
            .Concat((o.Zones ?? []).Select(z => z.Map))
            .Concat((o.PossibleLocations ?? []).Select(l => l.Map))
            .OfType<string>();
        var where = MapNames(data, mapIds);
        if (where.Length == 0 && QuestTaxonomy.WorksAnywhere(kind))
            where = "any map";
        var text = string.IsNullOrWhiteSpace(o.Description) ? QuestTaxonomy.Label(kind) : o.Description!;
        if (o.Optional)
            text += " (optional)";
        var item = o.Items?.FirstOrDefault() ?? o.QuestItem ?? o.MarkerItem ?? o.UseAny?.FirstOrDefault();
        return new CardObjective(kind, text, where, item);
    }

    // Variants drawn with the same artwork (Ground Zero and Ground Zero 21+) read as one map.
    private static string MapNames(GameData data, IEnumerable<string> mapIds) =>
        string.Join(", ", mapIds
            .Select(id => data.Maps.GetValueOrDefault(id))
            .OfType<ApiMap>()
            .GroupBy(m => data.DefinitionFor(m.NormalizedName)?.Key ?? m.NormalizedName)
            .Select(g => g.MinBy(m => m.Name.Length)!.Name)
            .Distinct()
            .Order(StringComparer.CurrentCulture));

    private static List<CardNeed> Needs(GameData data, ApiTask task)
    {
        var needs = new List<CardNeed>();
        void Add(RequirementKind kind, IReadOnlyList<string> alternatives, int count, IEnumerable<string> maps)
        {
            if (alternatives.Count == 0)
                return;
            var names = alternatives.Select(data.ItemName).Distinct().ToList();
            var text = names.Count <= 2 ? string.Join(" or ", names) : $"{names[0]} or {names.Count - 1} others";
            if (count > 1)
                text += $" ×{count}";
            var where = MapNames(data, maps);
            if (needs.Any(n => n.Text == text))
                return;
            needs.Add(new CardNeed(kind, alternatives[0], text, where.Length > 0 ? "on " + where : ""));
        }

        foreach (var (o, plan) in (task.Objectives ?? []).Zip(Planning.ToPlan(task).Objectives))
        {
            var maps = (o.Maps ?? []).Concat((o.Zones ?? []).Select(z => z.Map)).OfType<string>().ToList();
            foreach (var keys in plan.Keys)
                Add(RequirementKind.Key, keys, 1, maps);
            foreach (var (item, count) in plan.Bring)
                Add(RequirementKind.Bring, [item], count, maps);
        }
        foreach (var needed in task.NeededKeys ?? [])
            foreach (var key in needed.Keys ?? [])
                Add(RequirementKind.Key, [key], 1, needed.Map is null ? [] : [needed.Map]);
        return needs;
    }
}
