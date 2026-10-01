using Spotter.Core.Logs;
using Spotter.Core.Quests;

namespace Spotter.Core.Tests;

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
    public void Manual_holds_until_the_game_says_otherwise()
    {
        var result = Resolve(
            Obs("c", QuestState.Active, ObservationSource.Log, 28),
            Obs("c", QuestState.NotStarted, ObservationSource.Manual, 29));
        Assert.Equal(QuestState.NotStarted, result["c"].State);
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
