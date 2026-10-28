using Shturmap.Game.Screenshots;
using SkiaSharp;

namespace Shturmap.Session.Tests;

// The real set of 2026-10-27 as a scored check (owner, 2026-10-28; docs/NEXT.md, "the readers' layout before other text
// engines"): the owner's own screenshots, 2560×1440, English, PvE, in the git-ignored tests\fixtures\ocr\real-2026-10-27,
// five of the GEAR tab in \desk (the desk's reading) and three of the raid's inventory screen in \raid (the glance's),
// each with its hand-written answers (DeskTruth: the items by the page's regions, the regions and their sizes, the counts
// and the found-in-raid marks). Skipped where the pictures or the catalogue aren't on this PC. The floors stand a little
// under what was measured after the layout round of 2026-10-28, as another Windows' recognition may read a letter
// otherwise; a name read wrong, a region made up or missing and a screen told wrong stay at what was measured.
public class DeskRealSetTests
{
    private static string Folder(string set) => Path.Combine(AppContext.BaseDirectory, "fixtures", "ocr", "real-2026-10-27", set);

    private static async Task<DeskScore> ScoreAsync(string set, bool raid)
    {
        var folder = Folder(set);
        var pictures = Directory.Exists(folder)
            ? Directory.GetFiles(folder, "*.png").Order(StringComparer.Ordinal).Where(p => DeskTruth.Beside(p) is not null).ToList()
            : [];
        if (pictures.Count == 0)
            Assert.Skip($"Real screenshots stay out of the repository: the real set isn't on this PC (tests\\fixtures\\ocr\\real-2026-10-27\\{set}).");
        var (data, catalogue) = await DeskRealPictureTests.CatalogueAsync();
        if (DeskReader.Create("en", DeskRealPictureTests.Paddle) is not { } reader)
        {
            Assert.Skip("This Windows has no text recognition language installed.");
            return new DeskScore();
        }
        var total = new DeskScore();
        foreach (var picture in pictures)
        {
            using var bitmap = SKBitmap.Decode(await File.ReadAllBytesAsync(picture, TestContext.Current.CancellationToken));
            using var bgra = bitmap.Copy(SKColorType.Bgra8888);
            var at = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var reading = raid
                ? await reader.ReadRaidAsync(bgra, catalogue, at, TestContext.Current.CancellationToken)
                : await reader.ReadAsync(bgra, catalogue, at, TestContext.Current.CancellationToken);
            total += DeskTruth.Beside(picture)!.Score(reading.Screen, reading.Page, data.ItemName, data.ItemShortName);
        }
        return total;
    }

    private static string Said(DeskScore s) =>
        $"{s.ItemsLine}; {s.RegionsLine}; {s.CountsLine}; {s.MarksLine}" + Environment.NewLine + string.Join(Environment.NewLine, s.Notes);

    // Measured on main 04beff4 (2026-10-28), before the round: the five pictures' 387 items 287 right, 43 wrong (the stash
    // of 04 read a row off: its first row, two AUGs' lower half, shows no line along its top), 17 one of N (2 not among
    // them), 20 nothing said, 18 no cell; regions 26 of 29 (the AVS rig 7x5 twice, the stash of 04 10x12), one made up
    // (a stash item's "Meds" taken for a window's title); counts 36 of 75 right, 8 wrong, 31 unread, 3 made up; marks 266
    // of 345 right, 23 wrong, 42 not looked for (slots, cells said nothing of), 14 no cell.
    [Fact]
    public async Task The_desk_reads_the_real_sets_gear_pictures_to_their_answers()
    {
        var s = await ScoreAsync("desk", raid: false);
        Assert.True(s.ScreensWrong == 0 && s.RegionsMadeUp <= 1 && s.RegionsMissing == 0, Said(s));
        Assert.True(s.Wrong <= 43, Said(s));
        Assert.True(s.Right >= 280, Said(s));
        Assert.True(s.RegionsRight >= 26, Said(s));
        Assert.True(s.CountsRight >= 34 && s.CountsWrong <= 8, Said(s));
        Assert.True(s.MarksRight >= 260 && s.MarksWrong <= 23, Said(s));
    }

    // Measured on main 04beff4 (2026-10-28), before the round: the three pictures' 127 items 60 right, none wrong, 10 one
    // of N, 5 nothing said, 52 no cell (the backpack's and the pouch's grids of r1 as two windows of no name; r2's backpack
    // and its LOOT crate's grid, the crate's name at the pane's head taken for a window's title); regions 15 of 22, 2
    // missing, 5 of another size, 4 made up; counts 16 of 44 right, 28 unread; marks 29 of 112 right, 39 not looked for
    // (slots), 44 no cell.
    [Fact]
    public async Task The_glance_reads_the_real_sets_raid_pictures_to_their_answers()
    {
        var s = await ScoreAsync("raid", raid: true);
        Assert.True(s.ScreensWrong == 0 && s.RegionsMadeUp <= 4 && s.RegionsMissing <= 2, Said(s));
        Assert.True(s.Wrong == 0, Said(s));
        Assert.True(s.Right >= 58, Said(s));
        Assert.True(s.RegionsRight >= 15, Said(s));
        Assert.True(s.CountsRight >= 15 && s.CountsWrong == 0, Said(s));
        Assert.True(s.MarksRight >= 28 && s.MarksWrong == 0, Said(s));
    }
}
