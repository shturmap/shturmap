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
    private static string Folder(string set, string real = "real-2026-10-27") => Path.Combine(AppContext.BaseDirectory, "fixtures", "ocr", real, set);

    private static async Task<DeskScore> ScoreAsync(string set, bool raid, string real = "real-2026-10-27")
    {
        var folder = Folder(set, real);
        var pictures = Directory.Exists(folder)
            ? Directory.GetFiles(folder, "*.png").Order(StringComparer.Ordinal).Where(p => DeskTruth.Beside(p) is not null).ToList()
            : [];
        if (pictures.Count == 0)
            Assert.Skip($"Real screenshots stay out of the repository: the real set isn't on this PC (tests\\fixtures\\ocr\\{real}\\{set}).");
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
    // of 345 right, 23 wrong, 42 not looked for (slots, cells said nothing of), 14 no cell. After the round: 350 right, 0
    // wrong, 19 one of N and 1 not among them, 13 nothing said, 4 no cell; regions 29 of 29, none made up; counts 50 right, 5
    // wrong, 20 unread, none made up; marks 298 right, 6 wrong, 39 not looked for, 2 no cell.
    [Fact]
    public async Task The_desk_reads_the_real_sets_gear_pictures_to_their_answers()
    {
        var s = await ScoreAsync("desk", raid: false);
        Assert.True(s.ScreensWrong == 0 && s.RegionsMadeUp == 0 && s.RegionsMissing == 0 && s.RegionsRight == 29, Said(s));
        Assert.True(s.Wrong == 0, Said(s));
        Assert.True(s.Right >= 343, Said(s));
        Assert.True(s.CountsRight >= 47 && s.CountsWrong <= 5, Said(s));
        Assert.True(s.MarksRight >= 292 && s.MarksWrong <= 6, Said(s));
    }

    // Measured on main 04beff4 (2026-10-28), before the round: the three pictures' 127 items 60 right, none wrong, 10 one
    // of N, 5 nothing said, 52 no cell (the backpack's and the pouch's grids of r1 as two windows of no name; r2's backpack
    // and its LOOT crate's grid, the crate's name at the pane's head taken for a window's title); regions 15 of 22, 2
    // missing, 5 of another size, 4 made up; counts 16 of 44 right, 28 unread; marks 29 of 112 right, 39 not looked for
    // (slots), 44 no cell. After the round: 86 right, none wrong, 11 one of N, 8 nothing said, 22 no cell (r2's backpack
    // and special slots, their frames lost in the game's red low-health tint; its crate's faint grid read nine wide);
    // regions 18 of 22, 1 missing, 3 of another size, none made up; counts 20 right, 24 unread; marks 50 right, 41 not
    // looked for, 21 no cell. After the owner's pick (2) of the same evening, r3's Tri-Zip, its grid cut by the column's
    // foot, read as far as it shows and added to the answers (its slot and three items): 131 items 88 right, 1 wrong (its
    // AEM-5 suppressor's label read "AEM-\", which names the AEM textbook, and its mark not seen), 11 one of N; regions 19
    // of 23; counts 20 right; marks 51 right, 1 wrong.
    [Fact]
    public async Task The_glance_reads_the_real_sets_raid_pictures_to_their_answers()
    {
        var s = await ScoreAsync("raid", raid: true);
        Assert.True(s.ScreensWrong == 0 && s.RegionsMadeUp == 0 && s.RegionsMissing <= 1 && s.RegionsRight >= 19, Said(s));
        Assert.True(s.Wrong <= 1, Said(s));
        Assert.True(s.Right >= 85, Said(s));
        Assert.True(s.CountsRight >= 18 && s.CountsWrong == 0, Said(s));
        Assert.True(s.MarksRight >= 49 && s.MarksWrong <= 1, Said(s));
    }

    // The owner's play session of 2026-10-28 (evening), in the git-ignored tests\fixtures\ocr\real-2026-10-28: in-raid
    // screenshots answered by hand before the reader ran on them, seven at 2560×1440 (\raid-1440) and nine at 1920×1080
    // (\raid-1080, the first real pictures at that size), and four without an inventory (\none). The cut grids of the
    // player's own regions were added to the answers after the owner's pick (2) of the same evening: read as far as they
    // show, their slot and the items whole in view. Measured on main aedf76e, before the second layout round, on the
    // first answers: 189 items 154 right, 3 wrong, 11 one of N, 7 nothing said, 14 no cell; regions 31 of 39, 2 missing, 6
    // of another size, 2 made up (a window of no name from a rig's pocket, a window "LOOT"); counts 54 right, 2 wrong;
    // marks 103 right. After the round, with the cut grids' answers: 215 items 177 right, 3 wrong (a special slot's
    // Surv12 read as the Unheard beside it twice, an armour's short name), 14 one of N, 7 nothing said, 14 no cell; regions
    // 36 of 44, 2 missing (a09's loot rig under a tooltip, its loot backpack), 6 of another size (the special slots' fourth
    // cell and frames lost against the light world behind the column, two LOOT grids a column short), none made up;
    // counts 58 right, 3 wrong (the rig's "203/214", two magazines' "60/601"); marks 124 right, none wrong.
    [Fact]
    public async Task The_glance_reads_the_play_sessions_2560x1440_pictures_to_their_answers()
    {
        var s = await ScoreAsync("raid-1440", raid: true, "real-2026-10-28");
        Assert.True(s.ScreensWrong == 0 && s.RegionsMadeUp == 0 && s.RegionsMissing <= 2 && s.RegionsRight >= 36, Said(s));
        Assert.True(s.Wrong <= 3, Said(s));
        Assert.True(s.Right >= 172, Said(s));
        Assert.True(s.CountsRight >= 55 && s.CountsWrong <= 3, Said(s));
        Assert.True(s.MarksRight >= 120 && s.MarksWrong == 0, Said(s));
    }

    // Measured on main aedf76e, before the round, on the first answers: 296 items 174 right, 5 wrong, 15 one of N, 18
    // nothing said, 84 no cell (the LOOT pane never found, the AVS rig left out as cut); regions 22 of 43, 17 missing, 4
    // of another size, 2 made up; counts 42 right, 1 wrong; marks 116 right. After the round, with the cut grids' answers:
    // 343 items 265 right, 3 wrong (labels misread: "ComTac II", "Axe", and a special slot's Surv12 read as the Camera
    // beside it), 21 one of N, 17 nothing said, 37 no cell (b07's backpack, its grid unframed beside light items, left
    // out); regions 41 of 48, 1 missing, 6 of another size (three special slots' regions against the light world, two LOOT
    // grids a column short, b12's rig), none made up; counts 78 right, 1 wrong; marks 178 right, 2 wrong (a pocket's
    // Analgin read marked).
    [Fact]
    public async Task The_glance_reads_the_play_sessions_1920x1080_pictures_to_their_answers()
    {
        var s = await ScoreAsync("raid-1080", raid: true, "real-2026-10-28");
        Assert.True(s.ScreensWrong == 0 && s.RegionsMadeUp == 0 && s.RegionsMissing <= 1 && s.RegionsRight >= 41, Said(s));
        Assert.True(s.Wrong <= 3, Said(s));
        Assert.True(s.Right >= 258, Said(s));
        Assert.True(s.CountsRight >= 74 && s.CountsWrong <= 1, Said(s));
        Assert.True(s.MarksRight >= 172 && s.MarksWrong <= 2, Said(s));
    }

    // The same evening's four screenshots without an inventory (an extract list, the world): nothing is read there.
    [Fact]
    public async Task The_glance_reads_nothing_on_the_play_sessions_pictures_without_an_inventory()
    {
        var folder = Folder("none", "real-2026-10-28");
        var pictures = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.png").Order(StringComparer.Ordinal).ToList() : [];
        if (pictures.Count == 0)
            Assert.Skip("Real screenshots stay out of the repository: the real set isn't on this PC (tests\\fixtures\\ocr\\real-2026-10-28\\none).");
        var (_, catalogue) = await DeskRealPictureTests.CatalogueAsync();
        if (DeskReader.Create("en", DeskRealPictureTests.Paddle) is not { } reader)
        {
            Assert.Skip("This Windows has no text recognition language installed.");
            return;
        }
        foreach (var picture in pictures)
        {
            using var bitmap = SKBitmap.Decode(await File.ReadAllBytesAsync(picture, TestContext.Current.CancellationToken));
            using var bgra = bitmap.Copy(SKColorType.Bgra8888);
            var reading = await reader.ReadRaidAsync(bgra, catalogue, DateTimeOffset.MinValue, TestContext.Current.CancellationToken);
            Assert.True(reading.Page is null || reading.Page.Regions.Count == 0, $"{Path.GetFileName(picture)}: {reading.Screen}, {reading.Page?.Regions.Count} regions");
        }
    }
}
