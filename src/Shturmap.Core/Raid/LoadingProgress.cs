using Shturmap.Core.Logs;

namespace Shturmap.Core.Raid;

/// <summary>
/// How far a raid's loading is, for one quiet line and a thin rule in the raid card: four stages a player can tell
/// apart, each started by a step in the log. Within a stage the rule creeps on by the stage's typical length (medians
/// of 49 loads in the owner's logs, 2026-08-15 to 2026-10-01: location loaded at 25 s, player spawned at 42 s, pooled
/// at 47 s, raid start at 71 s), and never past 90 % of the stage before the log says the next one began.
/// </summary>
public static class LoadingProgress
{
    public static readonly IReadOnlyList<(string Name, double TypicalSeconds)> Stages =
    [
        ("MAP", 25),
        ("RAID", 17),
        ("SPAWNING", 5),
        ("STARTING", 24),
    ];

    public static int StageOf(LoadingStep? step) => step switch
    {
        null or LoadingStep.MatchingCompleted => 0,
        LoadingStep.LocationLoaded or LoadingStep.GamePrepared or LoadingStep.GameCreated => 1,
        LoadingStep.PlayerSpawned => 2,
        _ => 3,
    };

    /// <summary>The stage's name and how full the rule is (0–1), for a raid that is loading.</summary>
    public static (string Stage, double Fill) At(RaidState raid, DateTime now)
    {
        var stage = StageOf(raid.LoadingStep);
        var since = raid.LoadingStageSince ?? raid.LoadingSince ?? now;
        var within = Math.Clamp((now - since).TotalSeconds / Stages[stage].TypicalSeconds, 0, 0.9);
        return (Stages[stage].Name, (stage + within) / Stages.Count);
    }
}
