using Shturmap.Core.Logs;

namespace Shturmap.Core.Raid;

/// <summary>
/// What the game's log has confirmed of a raid's loading, for one quiet line and a segmented rule in the raid card.
/// Nothing is estimated (owner, 2026-10-02: the app shows only what it can read): the line names the last step the log
/// reported, and each segment lights only once its own step is in the log, so a skipped step stays dark.
/// </summary>
public static class LoadingProgress
{
    /// <summary>The steps the game logs while loading a raid, in their order, with what each one means once done.</summary>
    public static readonly IReadOnlyList<(LoadingStep Step, string Done)> Steps =
    [
        (LoadingStep.LocationLoaded, "MAP LOADED"),
        (LoadingStep.GamePrepared, "RAID PREPARED"),
        (LoadingStep.GameCreated, "RAID CREATED"),
        (LoadingStep.PlayerSpawned, "PLAYER SPAWNED"),
        (LoadingStep.GamePooled, "GAME POOLED"),
        (LoadingStep.GameRunning, "GAME RUNNING"),
    ];

    /// <summary>"LOADING · MAP" until the log reports the first step, then the last step it reported.</summary>
    public static string Text(RaidState raid)
    {
        foreach (var (step, done) in Steps)
            if (raid.LoadingStep == step)
                return "LOADING · " + done;
        return "LOADING · MAP";
    }

    /// <summary>For each of <see cref="Steps"/>, whether the log has reported it for this load.</summary>
    public static bool[] Done(RaidState raid) => Steps.Select(s => (raid.LoadingStepsSeen & (1 << (int)s.Step)) != 0).ToArray();
}
