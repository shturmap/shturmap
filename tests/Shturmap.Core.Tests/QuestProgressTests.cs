using Shturmap.Core.Logs;
using Shturmap.Core.Quests;

namespace Shturmap.Core.Tests;

public class QuestProgressTests
{
    // c requires b, b requires a; d requires a OR its failure (not strict); e requires c.
    private static readonly Dictionary<string, QuestRequirement[]> Requirements = new()
    {
        ["b"] = [new("a", ["complete"])],
        ["c"] = [new("b", ["complete"])],
        ["d"] = [new("a", ["complete", "failed"])],
        ["e"] = [new("c", ["complete"])],
    };

    private static Dictionary<string, QuestStatus> Resolve(params QuestObservation[] observations) =>
        QuestProgress.Resolve(observations, id => Requirements.GetValueOrDefault(id, []), id => id.ToUpperInvariant());

    private static QuestObservation Obs(string id, QuestState state, ObservationSource source, int day) =>
        new(GameMode.Pve, id, state, source, new DateTime(2026, 9, day), $"{source}:{id}:{day}");

    [Fact]
    public void Newest_observation_wins()
    {
        var result = Resolve(
            Obs("c", QuestState.Active, ObservationSource.TasksScan, 29),
            Obs("c", QuestState.Completed, ObservationSource.Log, 30));
        Assert.Equal(QuestState.Completed, result["c"].State);
        Assert.Equal(ObservationSource.Log, result["c"].Source);
    }

    [Fact]
    public void An_import_or_scan_never_reopens_a_quest_the_log_saw_finished()
    {
        // TarkovEyes dates its whole file by its last save, which can be after the game logged the hand-in.
        var result = Resolve(
            Obs("c", QuestState.Active, ObservationSource.Log, 26),
            Obs("c", QuestState.Completed, ObservationSource.Log, 29),
            Obs("c", QuestState.Active, ObservationSource.Import, 30),
            Obs("b", QuestState.Failed, ObservationSource.Log, 28),
            Obs("b", QuestState.Active, ObservationSource.TasksScan, 30));
        Assert.Equal(QuestState.Completed, result["c"].State);
        Assert.Equal(QuestState.Failed, result["b"].State);
    }

    [Fact]
    public void A_later_log_start_reopens_a_failed_quest_and_an_import_can_still_complete_one()
    {
        var result = Resolve(
            Obs("b", QuestState.Failed, ObservationSource.Log, 27),
            Obs("b", QuestState.Active, ObservationSource.Log, 28),
            Obs("c", QuestState.Active, ObservationSource.Log, 26),
            Obs("c", QuestState.Completed, ObservationSource.Import, 30));
        Assert.Equal(QuestState.Active, result["b"].State);
        Assert.Equal(QuestState.Completed, result["c"].State);
    }

    [Fact]
    public void Prerequisites_of_an_active_quest_are_implied_complete()
    {
        var result = Resolve(Obs("c", QuestState.Active, ObservationSource.TasksScan, 30));
        Assert.Equal(QuestState.Completed, result["b"].State);
        Assert.Equal("C", result["b"].ImpliedBy);
        Assert.Equal(QuestState.Completed, result["a"].State); // transitively
        Assert.Equal("B", result["a"].ImpliedBy);
        Assert.False(result.ContainsKey("e"));
    }

    [Fact]
    public void Inference_never_overrides_an_observation_and_skips_either_or_requirements()
    {
        var result = Resolve(
            Obs("b", QuestState.Failed, ObservationSource.Log, 1),
            Obs("c", QuestState.Active, ObservationSource.Log, 2),
            Obs("d", QuestState.Active, ObservationSource.Log, 3));
        Assert.Equal(QuestState.Failed, result["b"].State);
        Assert.False(result.ContainsKey("a")); // b is failed, not completed, and d's requirement is not strict
    }
}
