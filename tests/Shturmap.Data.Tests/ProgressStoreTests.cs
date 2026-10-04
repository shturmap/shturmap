using Shturmap.Core.Logs;
using Shturmap.Core.Quests;
using Shturmap.Data.Progress;

namespace Shturmap.Data.Tests;

public class ProgressStoreTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("shturmap-store-").FullName;

    public void Dispose() => Directory.Delete(_folder, true);

    [Fact]
    public void Settings_round_trip()
    {
        using var store = new ProgressStore(Path.Combine(_folder, "shturmap.db"));
        Assert.Null(store.GetSetting("crashReports"));
        store.SetSetting("crashReports", "always");
        Assert.Equal("always", store.GetSetting("crashReports"));
    }

    // The help panel closing with the window, on a first start, saved "help seen" after the session had closed the
    // store, and Shturmap crashed on exit (found by the release's delivery check, 2026-10-03).
    [Fact]
    public void A_setting_after_the_store_closed_is_dropped_not_a_crash()
    {
        var store = new ProgressStore(Path.Combine(_folder, "shturmap.db"));
        store.Dispose();
        store.SetSetting("help.seen", "1");
        Assert.Null(store.GetSetting("help.seen"));
    }

    // Closing runs on more than one path (the window closing, an uninstall): the second close, and whatever a
    // background step still asks afterwards, must find nothing to do rather than a closed database (review of
    // 2026-10-04, A36).
    [Fact]
    public void Closing_twice_and_using_it_afterwards_is_safe()
    {
        var path = Path.Combine(_folder, "shturmap.db");
        var store = new ProgressStore(path);
        var seen = new QuestObservation(GameMode.Pve, "quest", QuestState.Active, ObservationSource.Log, new DateTime(2026, 10, 4, 12, 0, 0), "log:1");
        Assert.Equal(1, store.Add([seen]));
        store.Dispose();
        store.Dispose();
        Assert.Equal(0, store.Add([seen with { Evidence = "log:2" }]));
        Assert.Empty(store.Load(GameMode.Pve));
        store.RemoveSetting("anything");

        // What was stored before the close is there for the next start.
        using var again = new ProgressStore(path);
        Assert.Equal("log:1", Assert.Single(again.Load(GameMode.Pve)).Evidence);
    }
}
