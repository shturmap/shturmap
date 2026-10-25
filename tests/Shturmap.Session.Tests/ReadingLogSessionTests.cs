using System.Globalization;
using Shturmap.Core.Desk;
using Shturmap.Core.Logs;
using Shturmap.Data.TarkovDev;
using Shturmap.Game.Screenshots;

namespace Shturmap.Session.Tests;

// The readings log in the session (owner, 2026-10-25: "Can't you write out logs of what was detected and link it to the
// screenshot so we can troubleshoot it afterwards?"; docs/DESIGN.md §8, "Developer aids", *Readings log*): each menu
// screenshot read leaves a text file named after it in the data folder's "readings", with what the readings saw and what
// the session did with them; "Delete screenshots after reading" leaves it; nothing of it goes into the diagnostics a report
// carries. The readers are the test's own: they say what a picture "shows".
public class ReadingLogSessionTests
{
    private const string Water = "000000000000000000000401";

    private static ItemSources Sources(GameMode _) => new()
    {
        Items = new Dictionary<string, ApiItem>
        {
            [Water] = new(Water, ["provisions"], 1, 20_000, [], BasePrice: 10_000, Width: 1, Height: 2, LastOfferCount: 80),
        },
        Barters = Array.Empty<ApiBarter>().ToLookup(b => ""),
        Crafts = Array.Empty<ApiCraft>().ToLookup(c => ""),
        Stations = new Dictionary<string, string>(),
        FleaMarket = new ApiFleaMarket(15, true, 0.05, 0.05, false),
        PricesAt = DateTimeOffset.Now,
    };

    private static GameData Data(GameMode mode)
    {
        var data = SessionRig.Data(mode);
        return new GameData
        {
            Mode = data.Mode,
            Language = data.Language,
            Maps = data.Maps,
            Tasks = data.Tasks,
            Traders = data.Traders,
            ItemNames = new Dictionary<string, string> { [Water] = "Bottle of zorbwater" },
            ItemShortNames = new Dictionary<string, string> { [Water] = "Zorbwater" },
            MapDefinitions = data.MapDefinitions,
            CheckedAt = data.CheckedAt,
        };
    }

    private static SessionRig Rig(Func<DeskReading?>? desk = null, Func<TaskPage?>? tasks = null) =>
        new(configure: (paths, locations) => new GameSession(paths, locations)
        {
            GivenData = Data,
            GivenSources = Sources,
            BuildKind = "dev build",
            ScreenshotGrace = TimeSpan.FromMilliseconds(50),
            DeskReader = (_, _, _, _) => Task.FromResult<DeskReading?>(desk?.Invoke()),
            TasksReader = (_, _) => Task.FromResult<TaskPage?>(tasks?.Invoke() ?? new TaskPage(TaskScreen.None, [], 17.5)),
        });

    private static int _shots;

    // A screenshot the game names without a position (taken in the menus); its name.
    private static string MenuScreenshot(SessionRig rig)
    {
        var name = string.Format(CultureInfo.InvariantCulture, "{0:yyyy-MM-dd}[{0:HH-mm}] ({1}).png", DateTime.Now, Interlocked.Increment(ref _shots));
        File.WriteAllBytes(Path.Combine(rig.ScreenshotsFolder, name), []);
        return name;
    }

    private static string Readings(SessionRig rig) => Path.Combine(rig.AppFolder, ReadingLog.FolderName);

    private static async Task<string> LogOf(SessionRig rig, string screenshot)
    {
        var path = ReadingLog.PathFor(Readings(rig), screenshot);
        await rig.Until(() => File.Exists(path), "the readings log of " + screenshot);
        return File.ReadAllText(path);
    }

    [Fact]
    public async Task A_desk_reading_leaves_a_file_named_after_its_screenshot_which_deleting_the_screenshot_leaves()
    {
        var shows = new DeskReading(DeskScreen.Stash, new DeskPage(DateTimeOffset.Now,
            [new DeskRegion("stash", DeskRegionKind.Stash, 10, 12, [new DeskCell(0, 0, 1, 2, [Water]) { Count = 2, FoundInRaid = true }])]), 5);
        await using var rig = Rig(desk: () => shows);
        rig.Log("Session mode: Pve");
        await rig.StartAsync();
        await rig.Until(s => s.Data is not null && s.Sources is not null, "the data and the item file");
        await rig.Session.SetDeleteScreenshotsAsync(true);

        var shot = MenuScreenshot(rig);
        var text = await LogOf(rig, shot);
        Assert.Contains($"Screenshot: {shot}", text);
        Assert.Contains($"Shturmap: {GameSession.Version} (dev build, custom (--data) data folder) on Windows", text);
        Assert.Contains("Mode: Pve", text);
        Assert.Contains("Read your stash, gear and tasks from menu screenshots: on", text);
        Assert.Contains("Delete screenshots after reading: on", text);
        Assert.Contains("TASKS SCREEN: none", text);
        Assert.Contains("DESK: the stash, read in 5 ms.", text);
        Assert.Contains($"  0,0 1x2: label not known: Bottle of zorbwater (Zorbwater) [{Water}], count 2, found in raid", text);
        Assert.Contains("THE PAGE IN HAND (Pve): the first reading.", text);

        // The screenshot goes once read; its readings stay.
        await rig.Until(() => rig.Screenshots.Count == 0, "the screenshot deleted");
        Assert.True(File.Exists(ReadingLog.PathFor(Readings(rig), shot)));

        // Nothing of it in the diagnostics, which is what a report carries besides the player's words.
        var diagnostics = Diagnostics.Build(rig.Snapshot, GameSession.Version, Diagnostics.WindowsVersion(), "dev build", AppLog.Tail(Diagnostics.LogLines),
            DateTime.Now, null, "dev");
        Assert.DoesNotContain("zorbwater", diagnostics, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ReadingLog.FolderName, diagnostics, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(shot, diagnostics, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_tasks_table_leaves_its_rows_and_what_the_set_did()
    {
        var row = new TaskRow { Name = "Elimination", Location = "Customs", Status = "active", TimeText = "10:14:11", TimeLeft = new TimeSpan(10, 14, 11), TimeVotes = 4 };
        row.Objectives.Add(new TaskObjective("Eliminate Zorbs", 0, 3));
        var lost = new TaskRow { Name = "Exit the location", Location = "Zorbland", Status = "active" };
        await using var rig = Rig(tasks: () => new TaskPage(TaskScreen.Table, [row, lost], 17.5));
        rig.Log("Session mode: Pve");
        await rig.StartAsync();
        await rig.Until(s => s.Data is not null, "the data");

        var shot = MenuScreenshot(rig);
        var text = await LogOf(rig, shot);
        Assert.Contains("TASKS SCREEN: the OPERATIONAL table, 2 rows.", text);
        Assert.Contains("row 1: \"Elimination\" | location \"Customs\" | status \"active\" | time left \"10:14:11\" = 10:14:11 (4 of 14 ways)", text);
        Assert.Contains("  objective \"Eliminate Zorbs\" 0/3", text);
        Assert.Contains("TAKEN: 1 row, the set replaced; 0 countdowns not taken by the checks; left out on no map the data knows: \"Zorbland\".", text);
        Assert.Contains("the set (Pve) now: 1 task", text);
        Assert.Contains("  \"Elimination\" on map-customs | Kill | expires ", text);
        // The Tasks screen goes to the tasks' reading alone: no item grids.
        Assert.DoesNotContain("DESK", text);
    }

    [Fact]
    public async Task A_menu_screenshot_not_read_leaves_no_file()
    {
        await using var rig = Rig(desk: () => new DeskReading(DeskScreen.None, null, 5));
        rig.Log("Session mode: Pve");
        await rig.StartAsync();
        await rig.Until(s => s.Data is not null && s.Sources is not null, "the data and the item file");
        await rig.Session.SetReadStashAsync(false);

        var shot = MenuScreenshot(rig);
        await Task.Delay(800, TestContext.Current.CancellationToken);
        Assert.False(File.Exists(ReadingLog.PathFor(Readings(rig), shot)));

        // Read: a screenshot that showed none of the screens gets a short file saying so.
        await rig.Session.SetReadStashAsync(true);
        shot = MenuScreenshot(rig);
        var text = await LogOf(rig, shot);
        Assert.Contains("Nothing was read from it", text);
    }
}
