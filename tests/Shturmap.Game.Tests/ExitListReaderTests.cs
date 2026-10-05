using Shturmap.Core.Screenshots;
using Shturmap.Game.Screenshots;
using SkiaSharp;

namespace Shturmap.Game.Tests;

// The extract list read from a screenshot's top right corner (owner, 2026-10-05). The first tests draw a picture of a
// list themselves, laid out as the game's at 2560×1440 (a green bar, rows under it, a right column), so they run on
// any PC whose Windows has text recognition for English. The last ones read real screenshots, which stay on the PC
// that took them (tests\fixtures\ocr, ignored by git) and are skipped elsewhere.
public sealed class ExitListReaderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("shturmap-exits-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static ExitListReader Reader()
    {
        var reader = ExitListReader.Create("en");
        if (reader is null)
            Assert.Skip("This Windows has no text recognition language installed.");
        return reader;
    }

    private static readonly ExitName[] Streets =
    [
        new("courtyard", ["Courtyard"]),
        new("taxi", ["Primorsky Ave Taxi V-Ex"]),
        new("crash", ["Crash Site"]),
        new("house", ["Damaged House"]),
        new("klimov", ["Klimov Street (Flare)"]),
        new("pinewood", ["Pinewood Basement (Co-op)"]),
        new("sewer", ["Sewer River"]),
    ];

    /// <summary>A picture with a list in its top right corner, in the game's layout scaled to the picture's height.</summary>
    private string Picture(string name, int width, int height, string header, params (string Label, string Name, string Right)[] rows)
    {
        var k = height / 1440f;
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(96, 104, 92)); // some scene
            float left = width - 768 * k, top = 5 * k;
            using var panel = new SKPaint { Color = new SKColor(14, 16, 14) };
            canvas.DrawRect(left, top, 768 * k, (74 + 70 * rows.Length) * k, panel);
            if (header.Length > 0)
            {
                using var green = new SKPaint { Color = new SKColor(124, 167, 14) };
                canvas.DrawRect(left, top, 573 * k, 74 * k, green);
            }
            using var face = SKTypeface.FromFamilyName("Bahnschrift", SKFontStyle.Bold) ?? SKTypeface.Default;
            using var big = new SKFont(face, 30 * k);
            using var small = new SKFont(face, 24 * k);
            using var dark = new SKPaint { Color = new SKColor(10, 12, 10), IsAntialias = true };
            using var light = new SKPaint { Color = new SKColor(205, 210, 205), IsAntialias = true };
            canvas.DrawText(header, left + 70 * k, top + 48 * k, SKTextAlign.Left, big, dark);
            canvas.DrawText("0:39:51", left + 610 * k, top + 48 * k, SKTextAlign.Left, big, light);
            for (var i = 0; i < rows.Length; i++)
            {
                var y = top + (74 + 70 * i + 46) * k;
                canvas.DrawText(rows[i].Label, left + 14 * k, y, SKTextAlign.Left, big, light);
                canvas.DrawText(rows[i].Name, left + 14 * k + big.MeasureText(rows[i].Label) + 14 * k, y, SKTextAlign.Left, small, light);
                canvas.DrawText(rows[i].Right, left + 605 * k, y, SKTextAlign.Left, big, light);
            }
        }
        var path = Path.Combine(_root, name);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    [Theory]
    [InlineData(2560, 1440)]
    [InlineData(1920, 1080)]
    [InlineData(3840, 2160)]
    [InlineData(3440, 1440)]
    public async Task A_list_in_the_corner_is_read_with_its_marks(int width, int height)
    {
        var path = Picture("list.png", width, height, ExitList.EnglishHeader,
            ("EXFIL01", "Courtyard", "??:??:??"), ("EXFIL02", "Primorsky Ave Taxi V-Ex", "??:??:??"), ("EXFIL03", "Crash Site", ""),
            ("EXFIL04", "Damaged House", ""), ("TRANSIT01", "Transit to Ground Zero", "0:00:50"));
        var reading = await Reader().ReadAsync(path, tries: 1, ct: TestContext.Current.CancellationToken);
        Assert.NotNull(reading);
        var found = ExitList.Match(reading, Streets);
        Assert.Equal(["courtyard", "crash", "house", "taxi"], found.Keys.Order());
        Assert.True(found["courtyard"]);
        Assert.True(found["taxi"]);
        Assert.False(found["crash"]);
        Assert.False(found["house"]);
        Assert.True(ExitList.IsList(reading, found.Count));
    }

    [Fact]
    public async Task A_picture_without_the_green_bar_has_no_list()
    {
        var path = Picture("none.png", 2560, 1440, "", ("EXFIL01", "Courtyard", ""), ("EXFIL02", "Crash Site", ""));
        Assert.Null(await Reader().ReadAsync(path, tries: 1, ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task What_is_no_picture_is_let_be()
    {
        var reader = Reader();
        var empty = Path.Combine(_root, "empty.png");
        File.WriteAllText(empty, "");
        var text = Path.Combine(_root, "text.png");
        File.WriteAllText(text, "picture");
        var wait = TimeSpan.FromMilliseconds(5);
        Assert.Null(await reader.ReadAsync(empty, tries: 2, wait, TestContext.Current.CancellationToken));
        Assert.Null(await reader.ReadAsync(text, tries: 2, wait, TestContext.Current.CancellationToken));
        Assert.Null(await reader.ReadAsync(Path.Combine(_root, "gone.png"), tries: 2, wait, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_picture_the_game_is_still_writing_is_read_once_it_is_whole()
    {
        var whole = Picture("whole.png", 2560, 1440, ExitList.EnglishHeader, ("EXFIL01", "Crash Site", ""), ("EXFIL02", "Damaged House", ""));
        var bytes = await File.ReadAllBytesAsync(whole, TestContext.Current.CancellationToken);
        var path = Path.Combine(_root, "written.png");
        // The game's way: the file is there, empty, and stays open while the picture is written.
        await using (var writing = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        {
            var read = Reader().ReadAsync(path, tries: 40, TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
            await Task.Delay(150, TestContext.Current.CancellationToken);
            await writing.WriteAsync(bytes.AsMemory(0, bytes.Length / 3), TestContext.Current.CancellationToken);
            await writing.FlushAsync(TestContext.Current.CancellationToken);
            await Task.Delay(150, TestContext.Current.CancellationToken);
            await writing.WriteAsync(bytes.AsMemory(bytes.Length / 3), TestContext.Current.CancellationToken);
            await writing.FlushAsync(TestContext.Current.CancellationToken);
            var reading = await read;
            Assert.NotNull(reading);
            Assert.Equal(["crash", "house"], ExitList.Match(reading, Streets).Keys.Order());
        }
    }

    [Theory]
    [InlineData("ge", "de")]
    [InlineData("en", "en")]
    [InlineData("ru", "ru")]
    [InlineData("ES-MX", "es")]
    [InlineData(null, null)]
    public void The_games_language_codes_become_windows_ones(string? game, string? windows) =>
        Assert.Equal(windows, ExitListReader.WindowsLanguage(game));

    // ---- real screenshots, where a PC has them ----

    private static string Real(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "ocr", name);
        if (!File.Exists(path))
            Assert.Skip(@"Real screenshots stay out of the repository: this one isn't on this PC (tests\fixtures\ocr).");
        return path;
    }

    [Fact]
    public async Task A_real_list_of_six_is_read()
    {
        var reading = await Reader().ReadAsync(Real("exits-streets-six.png"), tries: 1, ct: TestContext.Current.CancellationToken);
        Assert.NotNull(reading);
        var found = ExitList.Match(reading, Streets);
        Assert.Equal(["courtyard", "crash", "house", "klimov", "pinewood", "taxi"], found.Keys.Order());
        // The game's "??:??:??" stands beside the first two.
        Assert.Equal(["courtyard", "taxi"], found.Where(f => f.Value).Select(f => f.Key).Order());
    }

    [Theory]
    [InlineData("exits-streets-two-a.png")]
    [InlineData("exits-streets-two-b.png")]
    public async Task A_real_list_of_two_is_read(string name)
    {
        var reading = await Reader().ReadAsync(Real(name), tries: 1, ct: TestContext.Current.CancellationToken);
        Assert.NotNull(reading);
        var found = ExitList.Match(reading, Streets);
        Assert.Equal(["klimov", "pinewood"], found.Keys.Order());
        Assert.DoesNotContain(true, found.Values);
        Assert.True(ExitList.IsList(reading, found.Count));
    }

    [Theory]
    [InlineData("no-list-timer.png")]
    [InlineData("no-list-trees.png")]
    [InlineData("no-list-sky.png")]
    public async Task A_real_screenshot_without_the_list_has_none(string name) =>
        Assert.Null(await Reader().ReadAsync(Real(name), tries: 1, ct: TestContext.Current.CancellationToken));
}
