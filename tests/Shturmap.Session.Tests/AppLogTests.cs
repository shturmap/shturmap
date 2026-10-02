namespace Shturmap.Session.Tests;

// The app log is a short support trail: the profile path masked, at most 1 MB a day, a week kept (owner, 2026-10-03).
public class AppLogTests : IDisposable
{
    private const string Profile = @"C:\Users\Jane Doe";
    private readonly string _folder = Directory.CreateTempSubdirectory("shturmap-log-").FullName;

    public void Dispose() => Directory.Delete(_folder, true);

    [Theory]
    [InlineData(@"Game found: Steam at C:\Users\Jane Doe\Games\EFT", @"Game found: Steam at %USERPROFILE%\Games\EFT")]
    [InlineData(@"Screenshots: C:\Users\Jane Doe\OneDrive\Documents\Escape from Tarkov\Screenshots", @"Screenshots: %USERPROFILE%\OneDrive\Documents\Escape from Tarkov\Screenshots")]
    [InlineData(@"c:\users\JANE DOE\appdata\local\Shturmap", @"%USERPROFILE%\appdata\local\Shturmap")]
    [InlineData("file:///C:/Users/Jane Doe/AppData/x.svg", "file:///%USERPROFILE%/AppData/x.svg")]
    [InlineData(@"at Foo() in C:\Users\Jane Doe\source\a.cs:line 3 and C:\Users\Jane Doe.", @"at Foo() in %USERPROFILE%\source\a.cs:line 3 and %USERPROFILE%.")]
    [InlineData(@"C:\Users\Jane Doen\x and C:\Users\Jane Doe.old\y", @"C:\Users\Jane Doen\x and C:\Users\Jane Doe.old\y")]
    public void The_profile_path_is_masked(string text, string masked) => Assert.Equal(masked, LogFile.Mask(text, Profile));

    [Fact]
    public void Written_lines_are_masked_and_levelled()
    {
        var log = new LogFile(_folder, Profile);
        var now = new DateTime(2026, 10, 3, 12, 0, 0);
        log.Write(now, LogLevel.Warn, @"Logs folder not found under C:\Users\Jane Doe\Games");
        var line = Assert.Single(File.ReadAllLines(log.PathFor(now)));
        Assert.Equal(@"2026-10-03 12:00:00.000 WARN Logs folder not found under %USERPROFILE%\Games", line);
    }

    [Fact]
    public void A_full_day_says_so_once_and_stops_until_the_next()
    {
        var log = new LogFile(_folder, Profile, capBytes: 400);
        var day = new DateTime(2026, 10, 3, 9, 0, 0);
        for (var i = 0; i < 50; i++)
            log.Write(day.AddSeconds(i), LogLevel.Info, $"Line {i} with some words to fill the day's file");
        var lines = File.ReadAllLines(log.PathFor(day));
        Assert.EndsWith("WARN Today's log reached 0 KB; nothing more is written until tomorrow.", lines[^1]);
        Assert.Single(lines, l => l.Contains("nothing more", StringComparison.Ordinal));
        Assert.True(lines.Length < 10);

        var next = day.AddDays(1);
        log.Write(next, LogLevel.Info, "A new day");
        Assert.Single(File.ReadAllLines(log.PathFor(next)));
    }

    [Fact]
    public void A_day_already_full_at_start_stays_full()
    {
        var day = new DateTime(2026, 10, 3, 9, 0, 0);
        var first = new LogFile(_folder, Profile, capBytes: 300);
        for (var i = 0; i < 20; i++)
            first.Write(day, LogLevel.Info, $"Line {i} of the first run");
        var length = new FileInfo(first.PathFor(day)).Length;
        new LogFile(_folder, Profile, capBytes: 300).Write(day, LogLevel.Info, "After a restart");
        Assert.True(new FileInfo(first.PathFor(day)).Length - length < 120);
    }

    [Fact]
    public void A_week_is_kept_and_the_spotter_logs_go()
    {
        foreach (var name in new[] { "shturmap-2026-09-25.log", "shturmap-2026-09-26.log", "shturmap-2026-10-03.log", "spotter-2026-09-30.log", "notes.txt" })
            File.WriteAllText(Path.Combine(_folder, name), "x");
        new LogFile(_folder, Profile).Prune(new DateTime(2026, 10, 3, 8, 0, 0));
        Assert.Equal(["notes.txt", "shturmap-2026-09-26.log", "shturmap-2026-10-03.log"],
            Directory.GetFiles(_folder).Select(Path.GetFileName).Order());
    }

    [Fact]
    public void The_tail_is_the_last_lines_masked()
    {
        var day = new DateTime(2026, 10, 3, 9, 0, 0);
        // A line written before masking existed.
        File.WriteAllText(Path.Combine(_folder, "shturmap-2026-10-03.log"), @"2026-10-03 08:00:00.000 INFO C:\Users\Jane Doe\old" + Environment.NewLine);
        var log = new LogFile(_folder, Profile);
        for (var i = 0; i < 5; i++)
            log.Write(day, LogLevel.Info, $"Line {i}");
        Assert.Equal(["Line 3", "Line 4"], log.Tail(day, 2).Select(l => l[(l.LastIndexOf("INFO ", StringComparison.Ordinal) + 5)..]));
        Assert.Contains(@"%USERPROFILE%\old", log.Tail(day, 10)[0]);
    }
}
