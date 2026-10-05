namespace Shturmap.Core.Quests;

/// <summary>
/// What an objective asks the player to do. See docs/DESIGN.md §5. Names follow the game's own task types where one
/// fits (Elimination, Exploration, Pickup) and the community's terms otherwise (found in raid).
/// </summary>
public enum ObjectiveKind
{
    Elimination,
    Exploration,
    Pickup,
    Place,
    FindInRaid,
    Survive,

    /// <summary>Done outside the raid: hand-ins, builds, skills, trader levels.</summary>
    Trader,
}

public static class QuestTaxonomy
{
    // Tie-break order when a quest's in-raid objectives are split evenly between types.
    private static readonly ObjectiveKind[] Priority =
        [ObjectiveKind.Elimination, ObjectiveKind.Pickup, ObjectiveKind.Place, ObjectiveKind.Exploration, ObjectiveKind.Survive, ObjectiveKind.FindInRaid];

    /// <summary>The kind of a tarkov.dev objective <c>type</c>.</summary>
    public static ObjectiveKind Classify(string? objectiveType) => objectiveType switch
    {
        "shoot" => ObjectiveKind.Elimination,
        "visit" => ObjectiveKind.Exploration,
        "findQuestItem" => ObjectiveKind.Pickup,
        "plantItem" or "plantQuestItem" or "mark" or "useItem" => ObjectiveKind.Place,
        "findItem" => ObjectiveKind.FindInRaid,
        // tarkov.dev's "experience" is an in-raid health condition (e.g. stay under an effect), not player XP.
        "extract" or "experience" => ObjectiveKind.Survive,
        _ => ObjectiveKind.Trader,
    };

    public static bool InRaid(ObjectiveKind kind) => kind != ObjectiveKind.Trader;

    /// <summary>Kinds that count on any map when the objective names none.</summary>
    public static bool WorksAnywhere(ObjectiveKind kind) => kind is ObjectiveKind.Elimination or ObjectiveKind.FindInRaid or ObjectiveKind.Survive;

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
        ObjectiveKind.Elimination => "Elimination",
        ObjectiveKind.Exploration => "Exploration",
        ObjectiveKind.Pickup => "Pickup",
        ObjectiveKind.Place => "Place",
        ObjectiveKind.FindInRaid => "Find in raid",
        ObjectiveKind.Survive => "Survive",
        _ => "Trader",
    };

    public static string Explanation(ObjectiveKind kind) => kind switch
    {
        ObjectiveKind.Elimination => "Kill targets, maybe with conditions",
        ObjectiveKind.Exploration => "Go to a place",
        ObjectiveKind.Pickup => "Pick up a quest item",
        ObjectiveKind.Place => "Plant, mark or use an item you bring",
        ObjectiveKind.FindInRaid => "Find items in raid (FIR), any map",
        ObjectiveKind.Survive => "Survive and extract, or meet a condition",
        _ => "Hand over at a trader, after the raid",
    };
}
