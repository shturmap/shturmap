using Shturmap.Core.Quests;

namespace Shturmap.Core.Planning;

/// <summary>
/// What a quest's work on a map involves, named by the activity rather than "easy" or "hard": the data says what an
/// objective asks, not how hard a player finds it (owner, 2026-10-03; docs/DESIGN.md §5, "Plan order").
/// </summary>
public enum EffortGroup
{
    /// <summary>Reach a place and act there: visit, mark, plant, use, pick up, leave through a named exit.</summary>
    GoThere = 1,

    /// <summary>Depends on luck or on getting out alive: items found in raid, extracting with "Survived", Scav kills.</summary>
    FindOrSurvive = 2,

    /// <summary>Real fights or demanding kills: PMCs, Rogues, Raiders, cultists, bosses and guards, kills with conditions.</summary>
    Fight = 3,
}

/// <summary>
/// A quest's effort on one map: its group, then its complexity as a tuple compared in this order: how many of its
/// objectives count there, how many kill conditions they set, and the largest kill count in buckets
/// (none 0, up to 5: 1, up to 15: 2, more: 3). No weights: the order says which counts first.
/// </summary>
public readonly record struct Effort(EffortGroup Group, int Steps, int Conditions, int KillBucket) : IComparable<Effort>
{
    public int CompareTo(Effort other) =>
        Group != other.Group ? Group.CompareTo(other.Group)
        : Steps != other.Steps ? Steps.CompareTo(other.Steps)
        : Conditions != other.Conditions ? Conditions.CompareTo(other.Conditions)
        : KillBucket.CompareTo(other.KillBucket);

    public override string ToString() => $"{(int)Group} {QuestEffort.Label(Group)} ({Steps} steps, {Conditions} conditions, kill bucket {KillBucket})";
}

/// <summary>
/// The plan's order (owner, 2026-10-03): by effort group, then complexity, then the quest giver in the game's trader
/// order, then the quest's name. Only tarkov.dev's structured fields count: the objective type, kill targets and
/// conditions, exit statuses, found in raid; never the description, so new quests sort by themselves.
/// </summary>
public static class QuestEffort
{
    // Kill targets doable on AI Scavs. "assaultGroup" is a Scav type: tarkov.dev's English dictionary translates it
    // "Scav" (as it does "Savage" "Scavs", and "Marksman" "Sniper", the sniper Scavs); "Any" includes Scavs.
    private static readonly HashSet<string> ScavTargets = new(StringComparer.Ordinal) { "Savage", "Marksman", "assaultGroup", "Any" };

    // Every other target the data uses today; they are fights. By tarkov.dev's English names: "AnyPmc" any PMC
    // operatives, "Bear" BEAR operatives, "ExUsec" Rogues, "PmcBot" Raiders, "blackDivision" and "pmcBotBlackDiv"
    // Black Div., "tagillaHelperAgro" Labyrinth Guard. Unknown names count as fights too (the safe side), and the audit
    // (shturmap-cli effort) lists them.
    private static readonly HashSet<string> FightTargets = new(StringComparer.Ordinal)
        { "AnyPmc", "Bear", "ExUsec", "PmcBot", "blackDivision", "pmcBotBlackDiv", "tagillaHelperAgro" };
    private static readonly string[] FightPrefixes = ["boss", "follower", "sectant", "infected"];

    // Objective types tarkov.dev uses: in the raid (with a group) and outside it (no group). Anything else is new, and the
    // audit flags it; QuestTaxonomy.Classify sends unknown types outside the raid.
    private static readonly HashSet<string> InRaidTypes = new(StringComparer.Ordinal)
        { "visit", "mark", "plantItem", "plantQuestItem", "useItem", "findQuestItem", "extract", "findItem", "experience", "shoot" };
    private static readonly HashSet<string> OutsideTypes = new(StringComparer.Ordinal)
        { "giveItem", "giveQuestItem", "buildWeapon", "traderLevel", "traderStanding", "taskStatus", "skill", "sellItem", "globalVariable", "dialogue", "playerLevel" };

    public static bool IsKnownType(string? type) => type is not null && (InRaidTypes.Contains(type) || OutsideTypes.Contains(type));

    public static bool IsKnownTarget(string target) => ScavTargets.Contains(target) || FightTargets.Contains(target) || FightPrefixes.Any(p => target.StartsWith(p, StringComparison.Ordinal));

    public static string Label(EffortGroup group) => group switch
    {
        EffortGroup.GoThere => "Go there",
        EffortGroup.FindOrSurvive => "Find or survive",
        _ => "Fight",
    };

    /// <summary>The group of one objective.</summary>
    public static EffortGroup GroupOf(PlanObjective o) => o.Type switch
    {
        "visit" or "mark" or "plantItem" or "plantQuestItem" or "useItem" or "findQuestItem" => EffortGroup.GoThere,
        // Leaving through a named exit is a trip; "Survived" also means getting out alive.
        "extract" => (o.ExitStatus ?? []).Contains("ExpBonusSurvived") ? EffortGroup.FindOrSurvive : EffortGroup.GoThere,
        // Items that must be found in raid depend on luck; ones that may be bought are a trip at most.
        "findItem" => o.FoundInRaid ? EffortGroup.FindOrSurvive : EffortGroup.GoThere,
        // Staying under a health effect in the raid: surviving.
        "experience" => EffortGroup.FindOrSurvive,
        "shoot" => KillGroup(o),
        // Built without a type (the planner's own tests): by its kind.
        _ => o.Kind switch
        {
            ObjectiveKind.Exploration or ObjectiveKind.Pickup or ObjectiveKind.Place => EffortGroup.GoThere,
            ObjectiveKind.FindInRaid or ObjectiveKind.Survive => EffortGroup.FindOrSurvive,
            _ => EffortGroup.Fight,
        },
    };

    private static EffortGroup KillGroup(PlanObjective o)
    {
        if (o.Conditions is { Count: > 0 })
            return EffortGroup.Fight;
        var targets = o.Targets ?? [];
        return targets.Count > 0 && targets.All(ScavTargets.Contains) ? EffortGroup.FindOrSurvive : EffortGroup.Fight;
    }

    /// <summary>A quest's effort on a map: its objectives there as the planner counts them.</summary>
    public static Effort Of(QuestOnMap quest)
    {
        var objectives = quest.Objectives;
        if (objectives.Count == 0)
            return new Effort(EffortGroup.GoThere, 0, 0, 0);
        var kills = objectives.Where(o => o.Kind == ObjectiveKind.Elimination).ToList();
        var most = kills.Count > 0 ? kills.Max(o => o.Count) : 0;
        var bucket = most switch
        {
            0 => 0,
            <= 5 => 1,
            <= 15 => 2,
            _ => 3,
        };
        return new Effort(objectives.Max(GroupOf), objectives.Count, kills.Sum(o => o.Conditions?.Count ?? 0), bucket);
    }

    /// <summary>Quests in the plan's order: effort, then trader, then name.</summary>
    public static IEnumerable<QuestOnMap> Order(IEnumerable<QuestOnMap> quests) =>
        quests.Select(q => (Quest: q, Effort: Of(q)))
            .OrderBy(x => x.Effort)
            .ThenBy(x => x.Quest.Quest.TraderOrder)
            .ThenBy(x => x.Quest.Quest.Name, StringComparer.CurrentCulture)
            .Select(x => x.Quest);
}
