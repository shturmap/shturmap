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
}
