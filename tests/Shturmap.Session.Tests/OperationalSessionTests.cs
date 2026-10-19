using System.Globalization;
using Shturmap.Core.Operational;
using Shturmap.Game.Screenshots;

namespace Shturmap.Session.Tests;

// The daily and weekly operational tasks from a menu screenshot of the Tasks screen (owner, 2026-10-19: from a panel of
// four, B): in the stash's path (one tick, the menus only), a picture of the table goes to the tasks' reading alone,
// its rows land on their maps and in operational.json, the notice says how many, a trader's tasks go to neither
// reading. The readers are the test's own: they say what a picture "shows", so no picture is needed.
public class OperationalSessionTests
{
    private static TaskRow Row(string name, string location, TimeSpan? left, params TaskObjective[] objectives)
    {
        var row = new TaskRow { Name = name, Location = location, Status = "active", TimeLeft = left, TimeVotes = left is null ? 0 : 4 };
        row.Objectives.AddRange(objectives);
        return row;
    }

    private static TaskPage Table(params TaskRow[] rows) => new(TaskScreen.Table, rows, 17.5);

    private static SessionRig Rig(Func<TaskPage?> shows, List<string>? stashAsked = null) =>
        new(configure: (paths, locations) => new GameSession(paths, locations)
        {
            GivenData = SessionRig.Data,
            TasksReader = (_, _) => Task.FromResult(shows()),
            StashReader = (path, _, _) =>
            {
                stashAsked?.Add(path);
                return Task.FromResult<StashReading?>(new StashReading(false, []));
            },
        });

    private static int _shots;

    // A screenshot the game names without a position: taken in the menus.
    private static void MenuScreenshot(SessionRig rig)
    {
        var name = string.Format(CultureInfo.InvariantCulture, "{0:yyyy-MM-dd}[{0:HH-mm}] ({1}).png", DateTime.Now, Interlocked.Increment(ref _shots));
        File.WriteAllBytes(Path.Combine(rig.ScreenshotsFolder, name), []);
    }

    [Fact]
    public async Task The_table_is_read_onto_its_maps_kept_in_the_file_and_said_in_a_notice()
    {
        var stashAsked = new List<string>();
        await using var rig = Rig(() => Table(
            Row("Elimination", "Customs", new TimeSpan(10, 14, 11), new TaskObjective("Eliminate Zorbs with a headshot from a distance of more than 50m", 0, 3)),
            Row("Find and transfer", "Any location", TimeSpan.FromDays(2)),
            Row("Exit the location", "Zorbland", new TimeSpan(8, 33, 19)),
            Row("Exit the location", "Ground Zero", new TimeSpan(8, 33, 19))), stashAsked);
        rig.Log("Session mode: Pve");
        await rig.StartAsync();
        await rig.Until(s => s.Data is not null, "the data");

        MenuScreenshot(rig);
        var s = await rig.Until(s => s.Operational.Count > 0, "the operational tasks");
        // A location the data names no map for is left out; a variant is its map (Ground Zero 21+ counts as Ground Zero
        // in Plan; the rig's data has only the variant, which then is its own map).
        Assert.Equal(["Elimination", "Find and transfer", "Exit the location"], s.Operational.Select(t => t.Task.Name));
        Assert.Equal("customs", s.Operational[0].MapKey);
        Assert.Equal("map-customs", s.Operational[0].Task.MapId);
        Assert.Null(s.Operational[1].MapKey);
        Assert.True(s.Operational[1].Task.AnyMap);
        Assert.NotNull(s.Operational[2].MapKey);
        Assert.Equal(OperationalKind.Kill, s.Operational[0].Task.Kind);
        Assert.NotNull(s.OperationalReadAt);
        // The stash's reading never saw the picture.
        Assert.Empty(stashAsked);
        await rig.Until(() => rig.Notices.Any(n => n == "Operational tasks read: 3."), "the notice");
        // The maps with operational work and none of your quests join the list's end.
        Assert.Contains(s.OperationalPlans, p => p.NormalizedName == "customs");

        var file = File.ReadAllText(Path.Combine(rig.AppFolder, OperationalLedger.FileName));
        Assert.Contains("Eliminate Zorbs", file, StringComparison.Ordinal);
        Assert.DoesNotContain("Zorbland", file, StringComparison.Ordinal);

        // A restart brings them back from the file.
        await rig.RestartAsync();
        await rig.StartAsync();
        await rig.Until(s => s.Operational.Count == 3, "the tasks after a restart");
    }

    [Fact]
    public async Task A_traders_tasks_screen_goes_to_neither_reading()
    {
        var stashAsked = new List<string>();
        await using var rig = Rig(() => new TaskPage(TaskScreen.TraderScreen, [], 17.5), stashAsked);
        rig.Log("Session mode: Pve");
        await rig.StartAsync();
        await rig.Until(s => s.Data is not null, "the data");
        MenuScreenshot(rig);
        await Task.Delay(800, TestContext.Current.CancellationToken);
        Assert.Empty(rig.Session.Snapshot.Operational);
        Assert.Empty(stashAsked);
    }

    [Fact]
    public async Task Unticked_no_menu_screenshot_is_read_for_tasks_either()
    {
        var asked = 0;
        await using var rig = Rig(() =>
        {
            Interlocked.Increment(ref asked);
            return Table(Row("Elimination", "Customs", new TimeSpan(10, 14, 11)));
        });
        rig.Log("Session mode: Pve");
        await rig.StartAsync();
        await rig.Until(s => s.Data is not null, "the data");
        await rig.Session.SetReadStashAsync(false);
        MenuScreenshot(rig);
        await Task.Delay(800, TestContext.Current.CancellationToken);
        Assert.Equal(0, asked);
        Assert.Empty(rig.Session.Snapshot.Operational);
    }

    [Fact]
    public async Task Made_up_tasks_for_snapshots_never_reach_the_file()
    {
        await using var rig = Rig(() => null);
        rig.Log("Session mode: Pve");
        await rig.StartAsync();
        await rig.Until(s => s.Data is not null, "the data");
        Assert.Null(await rig.Session.ReadExampleTasksAsync(3, ended: false));
        var s = await rig.Until(s => s.Operational.Count > 0, "the example tasks");
        Assert.Contains(s.Operational, t => t.Task.AnyMap && t.Task.Kind == OperationalKind.HandOver);
        Assert.False(File.Exists(Path.Combine(rig.AppFolder, OperationalLedger.FileName)));
    }
}
