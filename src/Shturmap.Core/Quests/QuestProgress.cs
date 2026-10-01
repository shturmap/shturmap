using Shturmap.Core.Logs;

namespace Shturmap.Core.Quests;

public enum QuestState
{
    NotStarted,
    Active,
    Completed,
    Failed,
}

/// <summary>
/// Where a stored observation came from. Only Log (and Manual) are used; TasksScan and Import rows from earlier
/// versions may still be in a database and are ignored (docs/DESIGN.md §8, "Quest progress").
/// </summary>
public enum ObservationSource
{
    Log,
    TasksScan,
    Manual,
    Import,
}

/// <summary>Something Shturmap learned about a quest, with where and when it learned it.</summary>
/// <param name="Evidence">What it came from (a notification id, a screenshot name, …); also used to avoid storing it twice.</param>
public sealed record QuestObservation(GameMode Mode, string QuestId, QuestState State, ObservationSource Source, DateTime At, string Evidence);

/// <param name="ImpliedBy">Set when the state was inferred: the name of a later quest that requires this one.</param>
public sealed record QuestStatus(string QuestId, QuestState State, ObservationSource? Source, DateTime? At, string? ImpliedBy = null)
{
    public bool IsImplied => ImpliedBy is not null;
}

/// <summary>A quest's prerequisite: which quest, and which states of it satisfy the requirement.</summary>
public sealed record QuestRequirement(string QuestId, IReadOnlyList<string> States);

public static class QuestProgress
{
    public static QuestState FromLog(QuestLogStatus status) => status switch
    {
        QuestLogStatus.Started => QuestState.Active,
        QuestLogStatus.Completed => QuestState.Completed,
        _ => QuestState.Failed,
    };

    /// <summary>
    /// Current state per quest. The newest observation wins, so a log event after a scan overrides it and a manual
    /// change holds until the game says otherwise; but once the log has seen a quest completed or failed, only a
    /// later log event can make it active again. Quests nobody observed are marked
    /// completed when an active or completed quest strictly requires them; that is inferred, not stored.
    /// </summary>
    /// <param name="requirements">Prerequisites of a quest; only those requiring "complete" alone are used for inference.</param>
    /// <param name="name">Quest name for the "implied by" note.</param>
    public static Dictionary<string, QuestStatus> Resolve(
        IEnumerable<QuestObservation> observations,
        Func<string, IEnumerable<QuestRequirement>> requirements,
        Func<string, string> name)
    {
        var result = new Dictionary<string, QuestStatus>(StringComparer.Ordinal);
        foreach (var o in observations.OrderBy(o => o.At).ThenBy(o => o.Source))
        {
            // The game's log is the only witness of a hand-in. An import (dated by its file, not by the quest) or
            // a misread scan must not reopen a quest the log saw completed or failed; only a later log event can.
            if (o.Source is ObservationSource.Import or ObservationSource.TasksScan && o.State == QuestState.Active &&
                result.GetValueOrDefault(o.QuestId) is { Source: ObservationSource.Log, State: QuestState.Completed or QuestState.Failed })
                continue;
            result[o.QuestId] = new QuestStatus(o.QuestId, o.State, o.Source, o.At);
        }

        var queue = new Queue<string>(result.Values.Where(s => s.State is QuestState.Active or QuestState.Completed).Select(s => s.QuestId));
        while (queue.Count > 0)
        {
            var questId = queue.Dequeue();
            foreach (var requirement in requirements(questId))
            {
                var strict = requirement.States.Count > 0 &&
                             requirement.States.All(s => s.Equals("complete", StringComparison.OrdinalIgnoreCase));
                if (!strict || result.ContainsKey(requirement.QuestId))
                    continue;
                result[requirement.QuestId] = new QuestStatus(requirement.QuestId, QuestState.Completed, null, null, name(questId));
                queue.Enqueue(requirement.QuestId);
            }
        }
        return result;
    }
}
