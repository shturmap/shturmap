namespace Spotter.Core.Quests;

/// <summary>What an objective asks the player to do. See docs/DESIGN.md §5.</summary>
public enum ObjectiveKind
{
    Kill,
    Visit,
    Retrieve,
    Place,
    Collect,
    Survive,

    /// <summary>Done outside the raid: hand-ins, builds, skills, trader levels.</summary>
    Trader,
}

public static class QuestTaxonomy
{
    // Tie-break order when a quest's in-raid objectives are split evenly between types.
    private static readonly ObjectiveKind[] Priority =
        [ObjectiveKind.Kill, ObjectiveKind.Retrieve, ObjectiveKind.Place, ObjectiveKind.Visit, ObjectiveKind.Survive, ObjectiveKind.Collect];

    /// <summary>The kind of a tarkov.dev objective <c>type</c>.</summary>
    public static ObjectiveKind Classify(string? objectiveType) => objectiveType switch
    {
        "shoot" => ObjectiveKind.Kill,
        "visit" => ObjectiveKind.Visit,
        "findQuestItem" => ObjectiveKind.Retrieve,
        "plantItem" or "plantQuestItem" or "mark" or "useItem" => ObjectiveKind.Place,
        "findItem" => ObjectiveKind.Collect,
        "extract" => ObjectiveKind.Survive,
        _ => ObjectiveKind.Trader,
    };

    public static bool InRaid(ObjectiveKind kind) => kind != ObjectiveKind.Trader;

    /// <summary>Kinds that count on any map when the objective names none.</summary>
    public static bool WorksAnywhere(ObjectiveKind kind) => kind is ObjectiveKind.Kill or ObjectiveKind.Collect or ObjectiveKind.Survive;

    /// <summary>A quest's kind: its most common in-raid objective kind, or Trader if it has none.</summary>
    public static ObjectiveKind QuestKind(IEnumerable<ObjectiveKind> objectiveKinds)
    {
        var counts = objectiveKinds.Where(InRaid).GroupBy(k => k).ToDictionary(g => g.Key, g => g.Count());
        if (counts.Count == 0)
            return ObjectiveKind.Trader;
        var most = counts.Values.Max();
        return Priority.First(k => counts.GetValueOrDefault(k) == most);
    }

    public static string Label(ObjectiveKind kind) => kind switch
    {
        ObjectiveKind.Kill => "Kill",
        ObjectiveKind.Visit => "Visit",
        ObjectiveKind.Retrieve => "Retrieve",
        ObjectiveKind.Place => "Place",
        ObjectiveKind.Collect => "Collect",
        ObjectiveKind.Survive => "Survive",
        _ => "Trader",
    };

    public static string Explanation(ObjectiveKind kind) => kind switch
    {
        ObjectiveKind.Kill => "Eliminate targets, sometimes with conditions",
        ObjectiveKind.Visit => "Go to a place",
        ObjectiveKind.Retrieve => "Pick up a quest item at a place",
        ObjectiveKind.Place => "Plant, mark or use an item at a place; bring the item",
        ObjectiveKind.Collect => "Find items in raid, on any map",
        ObjectiveKind.Survive => "Survive and extract",
        _ => "Done at a trader, outside the raid",
    };
}
