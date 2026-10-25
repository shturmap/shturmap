using Shturmap.Core.Desk;
using Shturmap.Core.Logs;
using Shturmap.Data.Http;
using Shturmap.Data.Images;
using Shturmap.Data.TarkovDev;
using Shturmap.Game.Screenshots;
using SkiaSharp;

namespace Shturmap.Session.Tests;

// The stash desk's reading of the owner's own screenshots against the whole catalogue (docs/NEXT.md, the desk's step B):
// the GEAR tab's truth and the stash pages' three truths (hand-written, "A 3,0 Marl 214" a line: the grid, biggest
// first, the cell, the short name). The pictures stay on the PC that took them (tests\fixtures\ocr\desk and \stash,
// ignored by git) and the catalogue comes from the app's download cache, read without a request: skipped without either.
// A name read must never be another item's; how many are named is held to a floor below the measured (29 of 37 and 129
// of 148 on 2026-10-19), as the recognition's reading may differ by a letter on another Windows.
public class DeskRealPictureTests
{
    private static string Fixtures(string folder) => Path.Combine(AppContext.BaseDirectory, "fixtures", "ocr", folder);

    private sealed class Offline : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("offline: the test reads the cache only");
    }

    private static async Task<(GameData Data, DeskCatalogue Catalogue)> CatalogueAsync()
    {
        var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shturmap", "cache", "tarkov-dev");
        if (!File.Exists(Path.Combine(cache, "pve_items.json")))
            Assert.Skip("No tarkov.dev item file in the download cache: run Shturmap once in PvE to download it.");
        try
        {
            var loader = new GameDataLoader(new CachedHttp(new HttpClient(new Offline()), cache));
            var data = await loader.LoadAsync(GameMode.Pve, "en", TestContext.Current.CancellationToken);
            var sources = await loader.LoadSourcesAsync(GameMode.Pve, "en", TestContext.Current.CancellationToken);
            // A "one of N" cell's picture against its candidates' grid images, those the download cache holds (step 1c,
            // 2026-10-25): a name it settles must be as right as the label's.
            var art = new GameArt(new HttpClient(new Offline()), Path.Combine(Path.GetDirectoryName(cache)!, "game-art"));
            var icons = new DeskIcons(id => art.Cached(ArtKind.Grid, id) is { } png ? File.ReadAllBytes(png) : null);
            return (data, new DeskCatalogue(DeskData.Candidates(data, sources), icons));
        }
        catch (Exception e) when (e is HttpRequestException or IOException or NotDataException or System.Text.Json.JsonException)
        {
            Assert.Skip("The tarkov.dev cache is incomplete: " + e.Message);
            throw;
        }
    }

    private static List<(string Picture, List<(char Grid, int I, int J, string Name)> Cells)> Truths(string folder) =>
        !Directory.Exists(Fixtures(folder)) ? [] :
        Directory.GetFiles(Fixtures(folder), "truth-*.txt").Order(StringComparer.Ordinal)
            .Select(truth => (Path.Combine(Fixtures(folder), Path.GetFileNameWithoutExtension(truth)["truth-".Length..] + ".png"),
                File.ReadAllLines(truth).Where(l => l.Trim().Length > 0).Select(l =>
                {
                    var parts = l.Split(' ', 3);
                    var cell = parts[1].Split(',');
                    return (parts[0][0], int.Parse(cell[0]), int.Parse(cell[1]), parts[2].Trim());
                }).ToList()))
            .Where(t => File.Exists(t.Item1))
            .ToList();

    // A truth's grid is the biggest grid of the picture (the stash spike's "A"): here the region whose cells it names
    // most, at the row offset that names most (the truth's row 0 may be a row lower in the reading).
    private static (int Right, int Wrong, int Unsure) Score(DeskPage page, GameData data, List<(char Grid, int I, int J, string Name)> truth)
    {
        var best = (Right: 0, Wrong: 0, Unsure: 0, Hits: -1);
        foreach (var region in page.Regions)
        {
            for (var offset = 0; offset < Math.Max(1, region.Rows); offset++)
            {
                int right = 0, wrong = 0, unsure = 0;
                foreach (var t in truth)
                {
                    var cell = region.Cells.FirstOrDefault(c => !c.IsSlot && c.Rect.Contains(new DeskRect(t.I, t.J + offset, 1, 1)));
                    var names = cell?.Ids.Select(data.ItemShortName).ToList() ?? [];
                    if (names.Count == 1 && names[0] == t.Name)
                        right++;
                    else if (names.Count == 1)
                        wrong++;
                    else if (names.Contains(t.Name))
                        unsure++;
                }
                if (right + unsure > best.Hits)
                    best = (right, wrong, unsure, right + unsure);
            }
        }
        return (best.Right, best.Wrong, best.Unsure);
    }

    private static async Task<(int Right, int Wrong, int Unsure, int Items)> ReadAsync(string folder)
    {
        var truths = Truths(folder);
        if (truths.Count == 0)
            Assert.Skip($"Real screenshots stay out of the repository: none with a truth on this PC (tests\\fixtures\\ocr\\{folder}).");
        var (data, catalogue) = await CatalogueAsync();
        var reader = DeskReader.Create("en");
        if (reader is null)
            Assert.Skip("This Windows has no text recognition language installed.");
        int right = 0, wrong = 0, unsure = 0, items = 0;
        foreach (var (picture, cells) in truths)
        {
            using var bitmap = SKBitmap.Decode(await File.ReadAllBytesAsync(picture, TestContext.Current.CancellationToken));
            using var bgra = bitmap.Copy(SKColorType.Bgra8888);
            var reading = await reader.ReadAsync(bgra, catalogue, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), TestContext.Current.CancellationToken);
            Assert.NotNull(reading.Page);
            var (r, w, u) = Score(reading.Page, data, cells);
            right += r;
            wrong += w;
            unsure += u;
            items += cells.Count;
        }
        return (right, wrong, unsure, items);
    }

    [Fact]
    public async Task The_gear_tabs_stash_is_named_against_the_whole_catalogue_and_nothing_wrongly()
    {
        var (right, wrong, unsure, items) = await ReadAsync("desk");
        Assert.Equal(0, wrong);
        Assert.True(right >= 25, $"{right} of {items} named right, {unsure} one of several");
    }

    [Fact]
    public async Task The_stash_pages_are_named_against_the_whole_catalogue_and_nothing_wrongly()
    {
        var (right, wrong, unsure, items) = await ReadAsync("stash");
        Assert.Equal(0, wrong);
        Assert.True(right >= 115, $"{right} of {items} named right, {unsure} one of several");
    }

    // The layout round's test set (owner, 2026-10-25; docs/NEXT.md): the 13 menu screenshots of the owner's test of the
    // GEAR tab and the stash, and one more of the same evening with ARMBAND half under the Keys window, renamed in their
    // order without their time (00-gear-armband.png … 13-gear-keys.png in tests\fixtures\ocr\desk\session, ignored by git).
    // Beside each, the regions it should make (regions-<picture>.txt: its screen, then each region and the size of its grid
    // in view, "backpack 5x4"), and beside three, every item whose label shows (truth-<picture>.txt, by the page's regions:
    // "backpack 1,0 Triton", "loadout [earpiece] ComTac II"). With them since the owner's second test of 2026-10-25 (step
    // 1b), four more of that evening: the Beta 2 window over the backpack's grid with the game's tooltip over its rig
    // (14-gear-beta-window-tooltip.png), the CR MK2 (R) window over it (15-gear-beta-window.png, with a truth), that window
    // over the stash pane (16-gear-beta-rig-windows.png), and the column scrolled to its top with the rig
    // (17-gear-rig-top.png, with a truth). No screen told wrong, no region made up and none missing, no name wrong; the
    // sizes and the names right held to a floor under what was measured on 2026-10-25 (93 of 95 regions of their size, 269
    // of 338 items named), as another Windows' recognition may read a letter otherwise.
    [Fact]
    public async Task The_layout_rounds_test_set_makes_its_regions_and_nothing_else_and_names_nothing_wrongly()
    {
        var folder = Fixtures(Path.Combine("desk", "session"));
        var sets = Directory.Exists(folder)
            ? Directory.GetFiles(folder, "regions-*.txt").Order(StringComparer.Ordinal)
                .Select(path => (Name: Path.GetFileNameWithoutExtension(path)["regions-".Length..], Regions: path))
                .Where(s => File.Exists(Path.Combine(folder, s.Name + ".png"))).ToList()
            : [];
        if (sets.Count == 0)
            Assert.Skip("Real screenshots stay out of the repository: the layout round's test set isn't on this PC (tests\\fixtures\\ocr\\desk\\session).");
        var (data, catalogue) = await CatalogueAsync();
        var reader = DeskReader.Create("en");
        if (reader is null)
            Assert.Skip("This Windows has no text recognition language installed.");
        var faults = new List<string>();
        int regions = 0, sized = 0, items = 0, named = 0, wrong = 0;
        foreach (var (name, regionsPath) in sets)
        {
            using var bitmap = SKBitmap.Decode(await File.ReadAllBytesAsync(Path.Combine(folder, name + ".png"), TestContext.Current.CancellationToken));
            using var bgra = bitmap.Copy(SKColorType.Bgra8888);
            var reading = await reader.ReadAsync(bgra, catalogue, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), TestContext.Current.CancellationToken);
            var lines = Lines(regionsPath);
            var screen = lines.FirstOrDefault(l => l.StartsWith("screen ", StringComparison.Ordinal))?["screen ".Length..] ?? "None";
            if (reading.Screen.ToString() != screen)
                faults.Add($"{name}: screen {reading.Screen}, truth {screen}");
            var truth = lines.Where(l => !l.StartsWith("screen ", StringComparison.Ordinal)).Select(l => l.Split(' ', 2))
                .ToDictionary(p => p[0], p => p.Length > 1 ? p[1] : "", StringComparer.Ordinal);
            var read = reading.Page?.Regions ?? [];
            faults.AddRange(truth.Keys.Where(k => read.All(r => r.Key != k)).Select(k => $"{name}: {k} missing"));
            faults.AddRange(read.Where(r => !truth.ContainsKey(r.Key)).Select(r => $"{name}: {r.Key} made up"));
            regions += truth.Count;
            sized += read.Count(r => truth.TryGetValue(r.Key, out var size) && size == (r.Kind == DeskRegionKind.Loadout ? "" : $"{r.Cols}x{r.Rows}"));

            var cellsPath = Path.Combine(folder, "truth-" + name + ".txt");
            if (!File.Exists(cellsPath) || reading.Page is not { } page)
                continue;
            foreach (var line in Lines(cellsPath))
            {
                var parts = line.Split(' ', 3);
                var region = page.Region(parts[0]);
                DeskCell? cell;
                if (parts[1].StartsWith('['))
                    cell = region?.Cells.FirstOrDefault(c => c.Slot == parts[1].Trim('[', ']'));
                else
                {
                    var at = parts[1].Split(',');
                    cell = region?.Cells.FirstOrDefault(c => !c.IsSlot && c.Rect.Contains(new DeskRect(int.Parse(at[0]), int.Parse(at[1]), 1, 1)));
                }
                var names = cell?.Ids.Select(data.ItemShortName).ToList() ?? [];
                items++;
                if (names.Count == 1 && names[0] == parts[2])
                    named++;
                else if (names.Count == 1)
                    wrong++;
            }
        }
        Assert.Empty(faults);
        Assert.Equal(0, wrong);
        Assert.True(sized >= 91, $"{sized} of {regions} regions of their size");
        Assert.True(named >= 260, $"{named} of {items} items named right");
    }

    // A truth file's lines, "#" a comment.
    private static List<string> Lines(string path) =>
        File.ReadAllLines(path).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')).ToList();
}
