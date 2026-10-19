using Shturmap.Core.Desk;
using Shturmap.Core.Logs;
using Shturmap.Data.Http;
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
            return (data, DeskData.Catalogue(data, sources));
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
}
