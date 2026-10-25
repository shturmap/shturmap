using System.Reflection;
using Shturmap.Core.Desk;
using Shturmap.Core.Operational;
using Shturmap.Game.Screenshots;
using SkiaSharp;

namespace Shturmap.Game.Tests;

// The readings log (owner, 2026-10-25: "Can't you write out logs of what was detected and link it to the screenshot so we
// can troubleshoot it afterwards?"; docs/DESIGN.md §8, "Developer aids", *Readings log*): one text file per menu screenshot
// read, named after it, in a folder that keeps the last 50. Drawn readings with made-up items; every file under a
// temporary folder.
public sealed class ReadingLogTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("shturmap-readings-").FullName;

    public void Dispose() => Directory.Delete(_folder, true);

    private const string Water = "000000000000000000000401", Wallet = "000000000000000000000402", Zorb = "000000000000000000000403",
        Headset = "000000000000000000000404";

    private static readonly Dictionary<string, string> Names = new()
    {
        [Water] = "Bottle of water (Water)",
        [Wallet] = "Wallet",
        [Zorb] = "Zorb case (Zorb)",
        [Headset] = "Zorbian headset (Zorbset)",
    };

    private static string Shot(string name = "2026-01-01[15-40] (1).png") => Path.Combine(Path.GetTempPath(), "Screenshots", name);

    private static ReadingLogEntry Entry(MenuReading? reading, string screenshot = "2026-01-01[15-40] (1).png") =>
        new(Shot(screenshot), new DateTimeOffset(2026, 1, 1, 15, 40, 12, TimeSpan.FromHours(1)))
        {
            Shturmap = "0.7.0+f5ffe43 (dev build, dev data folder) on Windows 11 (10.0.26200)",
            Mode = "Pve",
            Reading = reading,
            Ms = 1312,
            NameOf = id => Names.GetValueOrDefault(id),
        };

    // A GEAR tab as the desk's reader would see it: an earpiece in its slot, the backpack read too blank, the Zhuk's window,
    // the stash with a stack of water found in raid, a "one of 2", a label that fits nothing, one unread, one whose
    // footprint contradicts its label, and a cell under a window.
    private static (MenuReading Reading, DeskPage Page) Gear()
    {
        var earpiece = new DeskCell(0, 0, 1, 1, [Headset]) { Slot = "earpiece" };
        var water = new DeskCell(0, 0, 1, 2, [Water]) { Count = 2, FoundInRaid = true };
        var either = new DeskCell(3, 0, 1, 1, [Wallet, Zorb]) { FoundInRaid = false, Holds = DeskHolds.Contents };
        var nothing = new DeskCell(5, 0, 1, 1, []);
        var unread = new DeskCell(6, 0, 1, 1, []);
        var otherSize = new DeskCell(7, 0, 2, 1, []);
        var zhukItem = new DeskCell(0, 0, 1, 1, [Water]) { FoundInRaid = false };
        var page = new DeskPage(DateTimeOffset.MinValue,
        [
            new DeskRegion("loadout", DeskRegionKind.Loadout, 0, 0, [earpiece]) { CoveredSlots = ["armband"] },
            new DeskRegion("backpack", DeskRegionKind.Backpack, 5, 4, []) { Parts = [new DeskRect(0, 0, 5, 4)], Unread = 3 },
            new DeskRegion("window:zhuk", DeskRegionKind.ContainerWindow, 3, 2, [zhukItem]) { Title = "Zhuk", Parts = [new DeskRect(0, 0, 3, 2)] },
            new DeskRegion("stash", DeskRegionKind.Stash, 10, 12, [water, either, nothing, unread, otherSize])
            {
                Covered = [new DeskRect(8, 4, 1, 1)],
                Unread = 4,
            },
        ]);
        var layout = new DeskLayoutFound(DeskScreen.Gear,
            [new DeskSlotBox("earpiece", SKRectI.Create(100, 200, 168, 168)), new DeskSlotBox("armband", SKRectI.Create(300, 200, 168, 84))],
            [
                new DeskPlace(DeskRegionKind.Backpack, SKRectI.Create(700, 900, 168, 168), [SKRectI.Create(880, 900, 420, 336)]),
                new DeskPlace(DeskRegionKind.ContainerWindow, null, [SKRectI.Create(1500, 500, 252, 168)])
                {
                    Title = "Zhuk",
                    Area = SKRectI.Create(1490, 460, 272, 218),
                },
            ],
            new StashGrid(1800, 300, 10, 12, StashGridPlace.Stash),
            [SKRectI.Create(1490, 460, 272, 218), SKRectI.Create(2000, 1300, 560, 80)],
            1,
            [SKRectI.Create(880, 900, 420, 336), SKRectI.Create(10, 10, 84, 84), SKRectI.Create(1500, 500, 252, 168)])
        {
            Notes =
            [
                "headings EARPIECE \"EARPlECE\" (100,180 88x15); foot 1274",
                "\"ZORBNAME\" at (10,20) where a loadout heading stands: no title",
                "window \"Zhuk\" at (1500,470): magnifier read, close read, 15 px high",
                "frame (10,10 84x84) under no heading",
            ],
        };
        DeskCellLook Look(string region, int x, int y, int w, int h, DeskCell cell, string? label, double fit, bool contradicted = false, string? count = null) =>
            new(region, SKRectI.Create(x, y, w, h), cell, label, fit, contradicted, count);
        var cells = new List<DeskCellLook>
        {
            Look("loadout", 100, 200, 168, 168, earpiece, "Zorbset", 1.0),
            Look("window:zhuk", 1500, 500, 84, 84, zhukItem, "Water", 0.95),
            Look("stash", 1800, 300, 84, 168, water, "Water", 1.0, count: "2"),
            Look("stash", 2052, 300, 84, 84, either, "Zorb", 0.92),
            Look("stash", 2220, 300, 84, 84, nothing, "Zrbq", 0.41),
            Look("stash", 2304, 300, 84, 84, unread, null, 0),
            Look("stash", 2388, 300, 168, 84, otherSize, "Water", 1.0, contradicted: true),
        };
        var look = new DeskLook(2560, 1440, 84, layout, cells, page, 1, 151, 1290);
        var seen = new MenuSeen(2560, 1440) { TasksScreen = TaskScreen.None, Desk = look };
        var marks = new StashReading(false, [new StashGridReading(StashGridKind.Stash, [new StashItemSeen(Water, 2, true)])]);
        return (new MenuReading(null, marks, new DeskReading(DeskScreen.Gear, page, 1290)) { Seen = seen }, page);
    }

    private static string[] Written(string path) => File.ReadAllLines(path);

    [Fact]
    public void A_desk_reading_is_written_cell_by_cell_beside_its_screenshots_name()
    {
        var (reading, page) = Gear();
        var after = page with
        {
            Regions = page.Regions.Select(r => r.Key == "window:zhuk" ? r with { AttachedTo = new DeskAnchor("backpack", 0, 0, "backpack") } : r).ToList(),
        };
        var change = new DeskChange(after, new HashSet<string> { "stash", "loadout" }, false)
        {
            Refused = new HashSet<string> { "backpack" },
            Dropped = new HashSet<string> { "window:junk" },
        };
        var path = ReadingLog.Write(_folder, Entry(reading) with { Desk = change, MarksChanged = 1 });

        Assert.Equal(Path.Combine(_folder, "2026-01-01[15-40] (1).png.txt"), path);
        var lines = Written(path);
        var text = File.ReadAllText(path);
        // The head.
        Assert.Contains("Screenshot: 2026-01-01[15-40] (1).png", lines);
        Assert.Contains("Taken: 2026-01-01 15:40:12 +01:00", lines);
        Assert.Contains("Shturmap: 0.7.0+f5ffe43 (dev build, dev data folder) on Windows 11 (10.0.26200)", lines);
        Assert.Contains("Picture: 2560x1440 px", lines);
        Assert.Contains("Mode: Pve", lines);
        Assert.Contains("Read your stash, gear and tasks from menu screenshots: on", lines);
        Assert.Contains("Delete screenshots after reading: off", lines);
        // The Tasks screen told none; What You Have's items.
        Assert.Contains(lines, l => l.StartsWith("TASKS SCREEN: none", StringComparison.Ordinal));
        Assert.Contains($"  Bottle of water (Water) [{Water}], count 2, found in raid", lines);
        Assert.Contains("  marks changed: 1", lines);
        // The layout in pixels.
        Assert.Contains("DESK: the GEAR tab, read in 1290 ms (the layout 151 ms), a cell 84 px.", lines);
        Assert.Contains("  loadout slots: earpiece (100,200 168x168), armband (300,200 168x84)", lines);
        Assert.Contains("  backpack: slot (700,900 168x168); parts (880,900 420x336)", lines);
        Assert.Contains("  window \"Zhuk\": slot none; parts (1500,500 252x168); area (1490,460 272x218)", lines);
        Assert.Contains("  stash grid: at (1800,300), 10x12 cells", lines);
        Assert.Contains("  over, not read nor what lies under it: window (1490,460 272x218), toast (2000,1300 560x80)", lines);
        Assert.Contains("  frames: 3 found, 1 under no region", lines);
        Assert.Contains("  note: frame (10,10 84x84) under no heading", lines);
        // A window's title the page has stays; a word refused as one is said by its length only.
        Assert.Contains("  note: window \"Zhuk\" at (1500,470): magnifier read, close read, 15 px high", lines);
        Assert.Contains("  note: a word of 8 characters at (10,20) where a loadout heading stands: no title", lines);
        Assert.Contains("  note: headings EARPIECE \"EARPlECE\" (100,180 88x15); foot 1274", lines);
        Assert.DoesNotContain("ZORBNAME", text);
        // Each cell: its place, the label as read, its fit, what it matched.
        Assert.Contains("loadout: 1 cell (1 named, 0 one of several, 0 nothing said), slots covered: armband", lines);
        Assert.Contains($"  [earpiece] (100,200 168x168): label \"Zorbset\", fit 1.00: Zorbian headset (Zorbset) [{Headset}]", lines);
        Assert.Contains("stash 10x12: 5 cells (1 named, 1 one of several, 3 nothing said), 4 cells unread, covered by a window or a toast: 8,4 1x1", lines);
        Assert.Contains($"  0,0 1x2 (1800,300 84x168): label \"Water\", fit 1.00: Bottle of water (Water) [{Water}], count 2 (read \"2\"), found in raid", lines);
        Assert.Contains($"  3,0 1x1 (2052,300 84x84): label \"Zorb\", fit 0.92: one of 2: Wallet [{Wallet}]; Zorb case (Zorb) [{Zorb}], not found in raid, holds contents", lines);
        Assert.Contains("  5,0 1x1 (2220,300 84x84): label \"Zrbq\", fit 0.41: nothing: the label fits nothing", lines);
        Assert.Contains("  6,0 1x1 (2304,300 84x84): no label read: nothing", lines);
        Assert.Contains("  7,0 2x1 (2388,300 168x84): label \"Water\", fit 1.00, its footprint contradicts it: nothing: the items the label fits are of another size", lines);
        Assert.Contains(lines, l => l.StartsWith("window:zhuk \"Zhuk\" 3x2", StringComparison.Ordinal));
        Assert.Contains("CELLS: 4 regions, 7 cells: 3 named, 1 one of several, 3 nothing said; 1 shape no label fits (their cells unread).", lines);
        // What the page in hand did.
        Assert.Contains("THE PAGE IN HAND (Pve): changed loadout, stash.", lines);
        Assert.Contains("  kept the page's backpack: read too blank (0 cells read, 3 unread; the page's has 0)", lines);
        Assert.Contains("  dropped window:junk: a window attached to nothing, not shown by 3 readings in a row", lines);
        Assert.Contains("  window \"Zhuk\": attached to backpack [backpack]", lines);
        Assert.Contains(lines, l => l.StartsWith("  regions now: loadout 1 cell, read ", StringComparison.Ordinal));
        NothingOfThePicture(text);
    }

    // No pixel ever reaches the log: no long run that could be an encoded picture, nothing that isn't plain text.
    private static void NothingOfThePicture(string text)
    {
        Assert.DoesNotMatch("[A-Za-z0-9+/=]{48,}", text);
        Assert.DoesNotContain(text, c => char.IsControl(c) && c is not '\r' and not '\n');
        Assert.True(text.Length < 20_000, $"{text.Length} characters");
    }

    // What the log is made of holds no pixel: no picture, no bytes, no stream, at any depth of the readings it is given.
    [Fact]
    public void Nothing_the_log_is_given_can_hold_a_picture()
    {
        var pixels = new[] { typeof(SKBitmap), typeof(SKImage), typeof(SKPixmap), typeof(byte[]), typeof(Stream), typeof(Memory<byte>), typeof(ReadOnlyMemory<byte>) };
        var seen = new HashSet<Type>();
        var faults = new List<string>();
        void Walk(Type type, string path)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                type = type.GetGenericArguments()[0];
            if (pixels.Any(p => p.IsAssignableFrom(type)))
            {
                faults.Add(path);
                return;
            }
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTimeOffset) ||
                type == typeof(TimeSpan) || type == typeof(DateTime) || !seen.Add(type))
                return;
            if (type.IsGenericType)
            {
                foreach (var argument in type.GetGenericArguments())
                    Walk(argument, $"{path}<{argument.Name}>");
            }
            if (type.Namespace?.StartsWith("Shturmap", StringComparison.Ordinal) != true)
                return;
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                Walk(property.PropertyType, $"{path}.{property.Name}");
        }
        Walk(typeof(ReadingLogEntry), nameof(ReadingLogEntry));
        Assert.Empty(faults);
        Assert.Contains(typeof(DeskLook), seen);
        Assert.Contains(typeof(TaskRow), seen);
    }

    private static TaskRow Row(string name, string? location, string? time, TimeSpan? left, int votes)
    {
        var row = new TaskRow { Name = name, Location = location, Status = "active", TimeText = time, TimeLeft = left, TimeVotes = votes };
        return row;
    }

    [Fact]
    public void A_tasks_reading_is_written_row_by_row_with_its_bands_votes_and_what_the_set_did()
    {
        var kill = Row("Elimination", "Customs", "10:14:11", new TimeSpan(10, 14, 11), 0);
        kill.ProgressText = "00/0";
        kill.Progress = 0;
        kill.ProgressVotes = 3;
        kill.Objectives.Add(new TaskObjective("Eliminate Zorbs with a headshot", 0, 3));
        var exit = Row("Exit the location", "Zorbland", null, null, 0);
        var page = new TaskPage(TaskScreen.Table, [kill, exit], 17.5);
        var seen = new MenuSeen(2560, 1440)
        {
            TasksScreen = TaskScreen.Table,
            Bands = ["time band 1200,640: nothing read of 14 ways", "progress band 1500,400: 0 ×3 of 14 ways"],
        };
        var set = new OperationalTask("Elimination", "map-customs", [new OperationalObjective("Eliminate Zorbs with a headshot", 0, 3)], OperationalKind.Kill,
            0, new DateTimeOffset(2026, 1, 2, 1, 54, 23, TimeSpan.FromHours(1)), new DateTimeOffset(2026, 1, 1, 15, 40, 12, TimeSpan.FromHours(1)));
        var taken = new OperationalTaken { Rows = 1, Unplaced = ["Zorbland"], Dropped = 0, Set = [set] };
        var path = ReadingLog.Write(_folder, Entry(new MenuReading(page, null) { Seen = seen }) with { Tasks = taken });

        var lines = Written(path);
        Assert.Contains("TASKS SCREEN: the OPERATIONAL table, 2 rows.", lines);
        Assert.Contains("row 1: \"Elimination\" | location \"Customs\" | status \"active\" | time left \"10:14:11\" = 10:14:11 (the page's words) | progress \"00/0\" = 0 % (3 of 14 ways)", lines);
        Assert.Contains("  objective \"Eliminate Zorbs with a headshot\" 0/3", lines);
        Assert.Contains("row 2: \"Exit the location\" | location \"Zorbland\" | status \"active\" | time left unread | progress unread", lines);
        Assert.Contains("bands, each read 14 ways, the value most of them read taken:", lines);
        Assert.Contains("  time band 1200,640: nothing read of 14 ways", lines);
        Assert.Contains("  progress band 1500,400: 0 ×3 of 14 ways", lines);
        Assert.Contains("TAKEN: 1 row, the set replaced; 0 countdowns not taken by the checks; left out on no map the data knows: \"Zorbland\".", lines);
        Assert.Contains("the set (Pve) now: 1 task", lines);
        Assert.Contains("  \"Elimination\" on map-customs | Kill | expires 2026-01-02 01:54:23 +01:00 | progress 0 % | 1 objective | read 2026-01-01 15:40:12 +01:00", lines);
        // A Tasks screen goes to the tasks' reading alone.
        Assert.DoesNotContain(lines, l => l.StartsWith("DESK", StringComparison.Ordinal) || l.StartsWith("WHAT YOU HAVE", StringComparison.Ordinal));
        NothingOfThePicture(File.ReadAllText(path));
    }

    [Fact]
    public void A_traders_tab_is_written_with_its_header_its_objectives_and_its_lists_entries()
    {
        var task = Row("Exit the location", "Shoreline", "23:54:15", new TimeSpan(23, 54, 15), 5);
        task.Status = "active!";
        task.Objectives.Add(new TaskObjective("Survive and extract from the location", 0, 1));
        var other = Row("Elimination", null, "00:48:48", new TimeSpan(0, 48, 48), 4);
        var page = new TaskPage(TaskScreen.TraderScreen, [task, other], 17.5) { Selected = task };
        var taken = new OperationalTaken { Trader = true, Added = true, Refreshed = 1, Set = [] };
        var lines = ReadingLog.Lines(Entry(new MenuReading(page, null) { Seen = new MenuSeen(2560, 1440) { TasksScreen = TaskScreen.TraderScreen } }) with { Tasks = taken });

        Assert.Contains("TASKS SCREEN: a trader's TASKS tab, its header's task an operational one; 2 entries in its list's OPERATIONAL TASKS.", lines);
        Assert.Contains("header: \"Exit the location\" | location \"Shoreline\" | status \"active!\" | time left \"23:54:15\" = 23:54:15 (5 of 14 ways)", lines);
        Assert.Contains("  objective \"Survive and extract from the location\" 0/1", lines);
        Assert.Contains("entry: \"Elimination\" | time left \"00:48:48\" = 00:48:48 (4 of 14 ways)", lines);
        Assert.Contains("TAKEN: the task added to the set; 1 kept task's countdown refreshed by the list's entries; 0 countdowns not taken by the checks.", lines);

        // A screenshot older than the last table's leaves the set as it was.
        lines = ReadingLog.Lines(Entry(new MenuReading(page, null)) with { Tasks = taken with { Older = true } });
        Assert.Contains("TAKEN: nothing: the screenshot is older than the last Tasks table's, whose set stands.", lines);
    }

    [Fact]
    public void A_screenshot_that_gave_nothing_gets_a_short_file_saying_so()
    {
        var none = new MenuReading(null, null, new DeskReading(DeskScreen.None, null, 40)) { Seen = new MenuSeen(1920, 1080) { TasksScreen = TaskScreen.None } };
        var lines = Written(ReadingLog.Write(_folder, Entry(none)));
        Assert.Contains("Picture: 1920x1080 px", lines);
        Assert.Contains("Nothing was read from it: no Tasks screen, and neither the stash nor the GEAR tab.", lines);
        Assert.True(lines.Length < 15, string.Join(Environment.NewLine, lines));

        // A picture that couldn't be opened whole.
        lines = Written(ReadingLog.Write(_folder, Entry(null, "2026-01-01[15-41] (2).png")));
        Assert.Contains("The picture couldn't be opened whole (still being written after the tries, or gone): nothing read.", lines);
        Assert.DoesNotContain(lines, l => l.StartsWith("Picture:", StringComparison.Ordinal));
    }

    // The folder keeps the last 50 files by when they were written; a screenshot read again is written over, the newest.
    [Fact]
    public void The_folder_keeps_the_last_fifty_and_a_screenshot_read_again_is_written_over()
    {
        var start = DateTime.UtcNow.AddHours(-2);
        string Name(int n) => $"2026-01-01[15-40] ({n}).png";
        for (var n = 0; n < ReadingLog.Kept; n++)
        {
            var path = ReadingLog.Write(_folder, Entry(null, Name(n)));
            File.SetLastWriteTimeUtc(path, start.AddMinutes(n));
        }
        Assert.Equal(ReadingLog.Kept, Directory.GetFiles(_folder).Length);

        // One more: the oldest goes.
        ReadingLog.Write(_folder, Entry(null, Name(50)));
        Assert.Equal(ReadingLog.Kept, Directory.GetFiles(_folder).Length);
        Assert.False(File.Exists(ReadingLog.PathFor(_folder, Name(0))));
        Assert.True(File.Exists(ReadingLog.PathFor(_folder, Name(1))));

        // The second oldest read again: written over, now the newest, so the next to go is the third.
        var (reading, _) = Gear();
        ReadingLog.Write(_folder, Entry(reading, Name(1)));
        Assert.Contains("DESK: the GEAR tab", File.ReadAllText(ReadingLog.PathFor(_folder, Name(1))));
        ReadingLog.Write(_folder, Entry(null, Name(51)));
        Assert.Equal(ReadingLog.Kept, Directory.GetFiles(_folder).Length);
        Assert.True(File.Exists(ReadingLog.PathFor(_folder, Name(1))));
        Assert.False(File.Exists(ReadingLog.PathFor(_folder, Name(2))));
        // No temporary file is left behind.
        Assert.DoesNotContain(Directory.GetFiles(_folder), f => f.EndsWith(".tmp", StringComparison.Ordinal));
    }
}
