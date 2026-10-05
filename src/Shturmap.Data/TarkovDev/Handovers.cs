using System.Runtime.CompilerServices;

namespace Shturmap.Data.TarkovDev;

/// <summary>
/// Hand-overs that only finish what another objective of the same quest got (owner, 2026-10-05: "many quests have a
/// thing where you have to get something or pick up something and then at a later step you need to hand it off after
/// the raid … It should not be AI derived per quest, it should be detectable from the quest text or layout itself").
/// "Locate and obtain the scientist's hard drive" then "Hand over the hard drive": the second is no step of its own in
/// a raid, so it is shown as a mark on the first (docs/DESIGN.md, "Quest cards", *Hand-overs*).
/// Paired by the data's ids, never by words, so it holds in every game language:
/// <list type="bullet">
/// <item><c>giveQuestItem</c> with the <c>findQuestItem</c> of the same quest item;</item>
/// <item><c>giveItem</c> with the <c>findItem</c> of the same set of items (in any order: tarkov.dev lists them in a
/// different order for some quests), the same count and the same found-in-raid.</item>
/// </list>
/// Each objective pairs once, in the quest's order (two quests list the hand-over before the find). A hand-over of
/// something no objective of the quest gets (money, a letter from an earlier quest, "any found in raid medicine
/// items") is the quest's work at the trader and stays its own line. Where the find is optional and its hand-over isn't
/// (the game counts the finding as optional where the items may be bought: A Bitter Victory, Reserve Expert), the line
/// reads as required (<see cref="Optional"/>): what is handed over has to be got. <c>shturmap-cli handovers</c> lists the
/// pairs and the near misses after a tarkov.dev update.
/// </summary>
public static class Handovers
{
    /// <summary>A quest's pairs: the hand-over by the objective that gets the thing, and the other way round.</summary>
    public sealed class Pairing
    {
        internal Dictionary<string, ApiObjective> HandoverOf { get; } = new(StringComparer.Ordinal);

        internal Dictionary<string, ApiObjective> GetOf { get; } = new(StringComparer.Ordinal);

        /// <summary>Every pair, in the quest's order of the objectives that get the thing.</summary>
        public IReadOnlyList<(ApiObjective Get, ApiObjective Handover)> Pairs { get; internal set; } = [];
    }

    private static readonly ConditionalWeakTable<ApiTask, Pairing> Cache = new();

    /// <summary>The quest's pairs, worked out once per loaded quest.</summary>
    public static Pairing Of(ApiTask task) => Cache.GetValue(task, Pair);

    /// <summary>The hand-over that finishes what this objective gets, or null.</summary>
    public static ApiObjective? HandoverOf(ApiTask task, string objectiveId) => Of(task).HandoverOf.GetValueOrDefault(objectiveId);

    /// <summary>The objective whose thing this hand-over gives, or null when it is a step of its own.</summary>
    public static ApiObjective? GetOf(ApiTask task, string objectiveId) => Of(task).GetOf.GetValueOrDefault(objectiveId);

    /// <summary>This objective is a hand-over shown as a mark on the objective that gets its thing.</summary>
    public static bool Folds(ApiTask task, string objectiveId) => Of(task).GetOf.ContainsKey(objectiveId);

    /// <summary>
    /// Whether an objective reads as optional ("(optional)", OPT on the map): as the data says, except that one whose
    /// hand-over folds into it is optional only when the hand-over is too.
    /// </summary>
    public static bool Optional(ApiTask task, ApiObjective objective) =>
        objective.Optional && HandoverOf(task, objective.Id)?.Optional != false;

    /// <summary>The objective's type is one that gives something to the trader.</summary>
    public static bool IsHandover(string? type) => type is "giveQuestItem" or "giveItem";

    /// <summary>
    /// Whether a hand-over gives what this objective gets, by the rule above. Public for the CLI's audit, which also
    /// lists what nearly pairs.
    /// </summary>
    public static bool Gives(ApiObjective handover, ApiObjective get) =>
        (handover.Type, get.Type) switch
        {
            ("giveQuestItem", "findQuestItem") => handover.QuestItem is { } item && item == get.QuestItem,
            ("giveItem", "findItem") => handover.Items is { Count: > 0 } items && get.Items is { Count: > 0 } found &&
                                        items.ToHashSet(StringComparer.Ordinal).SetEquals(found) &&
                                        Math.Max(1, handover.Count ?? 1) == Math.Max(1, get.Count ?? 1) &&
                                        handover.FoundInRaid == get.FoundInRaid,
            _ => false,
        };

    private static Pairing Pair(ApiTask task)
    {
        var pairing = new Pairing();
        var objectives = task.Objectives ?? [];
        foreach (var handover in objectives.Where(o => IsHandover(o.Type)))
        {
            var get = objectives.FirstOrDefault(o => !pairing.HandoverOf.ContainsKey(o.Id) && Gives(handover, o));
            if (get is null)
                continue;
            pairing.HandoverOf[get.Id] = handover;
            pairing.GetOf[handover.Id] = get;
        }
        pairing.Pairs = objectives.Where(o => pairing.HandoverOf.ContainsKey(o.Id)).Select(o => (o, pairing.HandoverOf[o.Id])).ToList();
        return pairing;
    }
}
