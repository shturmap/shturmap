using System.Globalization;
using Shturmap.Core.Logs;
using Shturmap.Core.Operational;
using Shturmap.Data.TarkovDev;
using Shturmap.Game.Screenshots;

namespace Shturmap.Session.Tests;

// The daily and weekly operational tasks from a menu screenshot of the Tasks screen (owner, 2026-10-19: from a panel of
// four, B): in the stash's path (one tick, the menus only), a picture of the table goes to the tasks' reading alone,
// its rows land on their maps and in operational.json, the notice says how many; a trader's TASKS tab (owner,
// 2026-10-25) goes to the tasks' reading alone too, its one task onto its map, its list's countdowns onto the tasks kept.
// The readers are the test's own: they say what a picture "shows", so no picture is needed.
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
    public async Task The_table_is_read_onto_its_maps_kept_in_the_file_and_called_out_in_a_cue()
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
        // One cue for the three, no notice (owner, 2026-10-19: "the same animation as the other quests").
        var cue = Assert.Single(rig.Cues, c => c.Kind == CueKind.TasksRead);
        Assert.False(cue.Joins);
        Assert.Equal(["Elimination", "Find and transfer", "Exit the location"], cue.Tasks!.Select(t => t.Task.Name));
        Assert.Equal("customs", cue.Tasks![0].MapKey);
        Assert.DoesNotContain(rig.Notices, n => n.StartsWith("Operational task", StringComparison.Ordinal));
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

    // A player takes a few screenshots in a row (the table, then a row expanded, then another page): one reading, one cue
    // (owner, 2026-10-19: "Be mindful of multiple missions started at the same time, we might want to have them in one
    // animation included"). The same set again brings no cue.
    [Fact]
    public async Task A_run_of_screenshots_calls_one_cue_and_the_same_set_again_none()
    {
        var page = Table(Row("Elimination", "Customs", new TimeSpan(10, 14, 11)), Row("Find and transfer", "Any location", TimeSpan.FromDays(2)));
        await using var rig = Rig(() => page);
        rig.Log("Session mode: Pve");
        await rig.StartAsync();
        await rig.Until(s => s.Data is not null, "the data");

        MenuScreenshot(rig);
        await rig.Until(s => s.Operational.Count == 2, "the operational tasks");
        Assert.Single(rig.Cues, c => c.Kind == CueKind.TasksRead);

        // Seconds later the same table with the kill expanded: its objectives arrive, no second cue.
        page = Table(Row("Elimination", "Customs", new TimeSpan(10, 14, 11), new TaskObjective("Eliminate Zorbs", 0, 3)),
            Row("Find and transfer", "Any location", TimeSpan.FromDays(2)));
        MenuScreenshot(rig);
        await rig.Until(s => s.Operational.Any(t => t.Task.Objectives.Count > 0), "the expanded row's objectives");
        Assert.Single(rig.Cues, c => c.Kind == CueKind.TasksRead);

        // A page with a task the cue hasn't listed: it joins the cue, which lists all three.
        page = Table(Row("Exit the location", "Customs", new TimeSpan(8, 33, 19)));
        MenuScreenshot(rig);
        await rig.Until(s => s.Operational.Any(t => t.Task.Name == "Exit the location"), "the other page");
        var joins = rig.Cues.Where(c => c.Kind == CueKind.TasksRead).ToList();
        Assert.Equal(2, joins.Count);
        Assert.True(joins[1].Joins);
        Assert.Equal(["Elimination", "Find and transfer", "Exit the location"], joins[1].Tasks!.Select(t => t.Task.Name));
        Assert.Single(joins[1].Tasks!, t => t.Task.Objectives.Count > 0);
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

    // A trader's TASKS tab (owner, 2026-10-25): the list's entries, each a name and a countdown, and the header's task when
    // it is one of them (the same row, with the header's location and status).
    private static TaskPage TraderTab(string? selected, string? location, params (string Name, TimeSpan? Left)[] entries)
    {
        var rows = entries.Select(e => new TaskRow { Name = e.Name, TimeLeft = e.Left, TimeVotes = e.Left is null ? 0 : 4 }).ToList();
        var task = rows.FirstOrDefault(r => r.Name == selected);
        if (task is not null)
            (task.Location, task.Status) = (location, "active");
        return new TaskPage(TaskScreen.TraderScreen, rows, 17.5) { Selected = task };
    }

    [Fact]
    public async Task A_traders_task_view_puts_its_task_on_its_map_with_the_cue_and_in_the_file()
    {
        var stashAsked = new List<string>();
        await using var rig = Rig(() => TraderTab("Exit the location", "Customs", ("Quenvik", new TimeSpan(8, 0, 0)), ("Exit the location", new TimeSpan(23, 54, 15))),
            stashAsked);
        rig.Log("Session mode: Pve");
        await rig.StartAsync();
        await rig.Until(s => s.Data is not null, "the data");

        MenuScreenshot(rig);
        var s = await rig.Until(s => s.Operational.Count > 0, "the trader's task");
        var shown = Assert.Single(s.Operational);
        Assert.Equal(("Exit the location", "map-customs", "customs"), (shown.Task.Name, shown.Task.MapId, shown.MapKey));
        Assert.Equal(OperationalKind.Survive, shown.Task.Kind);
        Assert.NotNull(shown.Task.ExpiresAt);
        // No table was read: no "As of".
        Assert.Null(s.OperationalReadAt);
        Assert.Empty(stashAsked);
        var cue = Assert.Single(rig.Cues, c => c.Kind == CueKind.TasksRead);
        Assert.Equal(["Exit the location"], cue.Tasks!.Select(t => t.Task.Name));
        Assert.Contains("Exit the location", File.ReadAllText(Path.Combine(rig.AppFolder, OperationalLedger.FileName)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_traders_tab_that_only_refreshes_countdowns_calls_no_cue_and_a_story_quests_adds_nothing()
    {
        var page = Table(Row("Elimination", "Customs", null));
        await using var rig = Rig(() => page);
        rig.Log("Session mode: Pve");
        await rig.StartAsync();
        await rig.Until(s => s.Data is not null, "the data");
        MenuScreenshot(rig);
        await rig.Until(s => s.Operational.Count == 1, "the table");
        Assert.Single(rig.Cues, c => c.Kind == CueKind.TasksRead);

        // A story quest's tab: the list's entry refreshes the kept task's countdown; the other entry adds nothing.
        page = TraderTab("Zorbs on Parade", "Customs", ("Elimination", new TimeSpan(8, 0, 0)), ("Quenvik", new TimeSpan(5, 0, 0)));
        MenuScreenshot(rig);
        var s = await rig.Until(s => s.Operational.Single().Task.ExpiresAt is not null, "the refreshed countdown");
        Assert.Equal(["Elimination"], s.Operational.Select(t => t.Task.Name));
        Assert.Single(rig.Cues, c => c.Kind == CueKind.TasksRead);
        Assert.DoesNotContain(rig.Notices, n => n.StartsWith("Operational task", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_traders_task_on_no_known_map_is_left_out()
    {
        await using var rig = Rig(() => TraderTab("Exit the location", "Zorbland", ("Exit the location", new TimeSpan(23, 54, 15))));
        rig.Log("Session mode: Pve");
        await rig.StartAsync();
        await rig.Until(s => s.Data is not null, "the data");
        MenuScreenshot(rig);
        await Task.Delay(800, TestContext.Current.CancellationToken);
        Assert.Empty(rig.Session.Snapshot.Operational);
        Assert.DoesNotContain(rig.Cues, c => c.Kind == CueKind.TasksRead);
    }

    [Fact]
    public async Task Unticked_no_trader_screenshot_is_opened_either()
    {
        var asked = 0;
        await using var rig = Rig(() =>
        {
            Interlocked.Increment(ref asked);
            return TraderTab("Exit the location", "Customs", ("Exit the location", new TimeSpan(23, 54, 15)));
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

    private static TaskObjective HandOver(string item) => new($"Hand over the found in raid item: \"{item}\"", null, null);

    // Made-up item ids (24 characters, as the game's).
    private const string Zorb = "000000000000000000000a01", Fan = "000000000000000000000a02";

    // The rig's data in German: the catalogue's names are German, and one German name is another item's English one.
    private static GameData German(GameMode mode)
    {
        var data = SessionRig.Data(mode);
        return new GameData
        {
            Mode = mode, Language = "de", Maps = data.Maps, Tasks = data.Tasks, Traders = data.Traders,
            MapDefinitions = data.MapDefinitions, CheckedAt = data.CheckedAt,
            ItemNames = new Dictionary<string, string> { [Zorb] = "Zorblaufwerk", [Fan] = "Zorb drive" },
            EnglishItemNames = new Dictionary<string, string> { [Zorb] = "Zorb drive", [Fan] = "Zorb fan" },
        };
    }

    // The game is read in English: a hand-over's quoted names are matched against the items' English names whatever the
    // app's language (QA, 2026-10-19: with the app in German the item cells and FIND IN RAID's rows were gone).
    [Fact]
    public async Task A_hand_overs_items_are_found_by_their_english_names_in_a_german_catalogue()
    {
        await using var rig = new SessionRig(configure: (paths, locations) => new GameSession(paths, locations)
        {
            GivenData = German,
            TasksReader = (_, _) => Task.FromResult<TaskPage?>(Table(Row("Find and transfer", "Any location",
                TimeSpan.FromDays(2), HandOver("Zorb drive"), HandOver("Zorb fan"), HandOver("Zorblaufwerk")))),
            StashReader = (_, _, _) => Task.FromResult<StashReading?>(new StashReading(false, [])),
        });
        rig.Log("Session mode: Pve");
        await rig.StartAsync();
        await rig.Until(s => s.Data is not null, "the data");
        MenuScreenshot(rig);
        var s = await rig.Until(s => s.Operational.Count > 0, "the operational tasks");
        // Each by its English name; a German name read from the English game is no item's.
        Assert.Equal([("Zorb drive", Zorb), ("Zorb fan", Fan), ("Zorblaufwerk", null)],
            s.Operational[0].Items.Select(i => (i.Name, i.ItemId)));
    }

    // Made-up tasks (a script's "tasks", "--operational") go the reading's way from where it hands its rows over, the cue
    // too, so a snapshot can picture it; the file never has them.
    [Fact]
    public async Task Made_up_tasks_for_snapshots_call_the_cue_and_never_reach_the_file()
    {
        await using var rig = Rig(() => null);
        rig.Log("Session mode: Pve");
        await rig.StartAsync();
        await rig.Until(s => s.Data is not null, "the data");
        Assert.Null(await rig.Session.ReadExampleTasksAsync(3, ended: false));
        var s = await rig.Until(s => s.Operational.Count > 0, "the example tasks");
        Assert.Contains(s.Operational, t => t.Task.AnyMap && t.Task.Kind == OperationalKind.HandOver);
        Assert.Equal(3, Assert.Single(rig.Cues, c => c.Kind == CueKind.TasksRead).Tasks!.Count);
        Assert.False(File.Exists(Path.Combine(rig.AppFolder, OperationalLedger.FileName)));
    }
}
