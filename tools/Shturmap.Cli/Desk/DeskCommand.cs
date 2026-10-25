using System.Globalization;
using System.Text;
using Shturmap.App.Rules;
using Shturmap.Core.Desk;
using Shturmap.Core.Logs;
using Shturmap.Core.Quests;
using Shturmap.Data.Http;
using Shturmap.Data.TarkovDev;
using Shturmap.Game.Screenshots;
using Shturmap.Session;
using SkiaSharp;

namespace Shturmap.Cli.Desk;

/// <summary>
/// <c>shturmap-cli desk &lt;png|folder&gt;</c>: the stash desk's reading of menu screenshots, headless (docs/DESIGN.md §2,
/// "For the stash desk"): each picture's page as text (its regions in reading order, each cell's item or "one of N", count,
/// printed condition, figure with its source and marks, keep notch), the head line, and with a folder the tally against
/// the hand-written truths beside the pictures (<c>truth-&lt;picture&gt;.txt</c>: the stash spike's format, "A 3,0 Marl
/// 214", the grid's letter biggest first, or by the page's regions, "backpack 1,0 Triton" and "loadout [earpiece] ComTac
/// II", 2026-10-25) and the regions each should make (<c>regions-&lt;picture&gt;.txt</c>: "screen Gear", "loadout",
/// "backpack 5x6"). The pictures are the owner's own, in the git-ignored tests\fixtures\ocr; it prints item names and
/// counts, never a path's folder. The data comes from the cache: no request unless --online.
/// </summary>
internal static class DeskCommand
{
    // "--why": each cell's label as read, its fit, and a footprint's contradiction; the layout as found.
    private static bool Why;

    // "--lattice": the stash grid's lines between its cells as the item finder sees them (a measuring aid).
    private static bool Lattice;

    private sealed class Tally
    {
        public int Truths, Right, Wrong, OneOf, OneOfOther, Silent, Missing, Pictures;
        public int RegionPictures, Regions, RegionsRight, RegionsMissing, RegionsWrongSize, RegionsExtra, ScreensWrong;
    }

    public static async Task RunAsync(IReadOnlyList<string> args)
    {
        var online = false;
        var quiet = false;
        bool? raid = null;
        var audit = false;
        (double Width, double Height)? plate = null;
        var mode = GameMode.Pve;
        var inputs = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--online": online = true; break;
                case "--quiet": quiet = true; break;
                case "--why": Why = true; break;
                case "--raid": raid = true; break;
                case "--menus": raid = false; break;
                case "--lattice": Lattice = true; break;
                case "--audit": audit = true; break;
                case "--plate" when i + 1 < args.Count && args[i + 1].Split('x') is [var w, var h]
                                    && double.TryParse(w, CultureInfo.InvariantCulture, out var pw) && double.TryParse(h, CultureInfo.InvariantCulture, out var ph):
                    plate = (pw, ph);
                    i++;
                    break;
                case "--mode": mode = args[++i] == "pvp" || args[i] == "regular" ? GameMode.Pvp : GameMode.Pve; break;
                default: inputs.Add(args[i]); break;
            }
        }
        if (inputs.Count == 0 && !audit)
        {
            Console.WriteLine("Use: shturmap-cli desk <png or folder>... [--quiet] [--online] [--mode pve|pvp] [--raid | --menus] [--plate <w>x<h>] [--why] [--lattice]");
            Console.WriteLine("  each picture's page: regions, cells (name or \"one of N\", count, condition, figure and source, marks), the head line;");
            Console.WriteLine("  with truth-<picture>.txt beside a picture, the tally against it (and footprints-<picture>.txt, the items' footprints).");
            Console.WriteLine("  A truth's line: \"A 3,0 <short name>\" (grid A, the biggest the stash's lines give, its cell), or by the page's");
            Console.WriteLine("  regions \"<region> <i>,<j> <short name>\" (its grid's top left cell, compartments side by side as the page has");
            Console.WriteLine("  them) and \"<region> [<slot>] <short name>\" (a slot: loadout [earpiece], backpack [backpack]); \"#\" a comment.");
            Console.WriteLine("  With regions-<picture>.txt beside it, the regions the reading should make: \"screen Gear\" (None, Gear, Stash),");
            Console.WriteLine("  then a line a region, \"loadout\" or \"<region> <cols>x<rows>\" (the whole rows in view; 0x0, none of its grid).");
            Console.WriteLine("  --quiet: the tallies only. A raid's inventory screen (by recognition, or --raid: as the in-raid glance reads it,");
            Console.WriteLine("  the world's pictures said none) prints its LOOT regions and is never taken into the page in hand.");
            Console.WriteLine("  --plate <w>x<h>: a raid's LOOT regions laid out as the in-raid glance's plate lays them out in a room that wide and");
            Console.WriteLine("  high under its head, in DIP (the app log's \"room\"): the cell size, each region whole or cut short, and the ALSO line.");
            return;
        }
        var files = inputs.SelectMany(p => Directory.Exists(p) ? Directory.GetFiles(p, "*.png").Order(StringComparer.Ordinal).ToArray() : [p]).ToList();

        var paths = AppPaths.Default;
        var refused = 0;
        var http = online ? CachedHttp.CreateClient() : CachedHttp.CreateClient(new NoRequests(() => Interlocked.Increment(ref refused)));
        var loader = new GameDataLoader(new CachedHttp(http, paths.DataCache));
        var data = await loader.LoadAsync(mode, "en");
        var sources = await loader.LoadSourcesAsync(mode, "en");
        var catalogue = DeskData.Catalogue(data, sources);
        var flea = DeskData.Flea(sources);
        // No quest history here: every quest counts as not started, so NEXT is the quests open to start, LATER the rest.
        var keep = DeskData.Keep(data, new Dictionary<string, QuestStatus>(), sources);
        Console.WriteLine($"Catalogue: {catalogue.Count} items; prices of {sources.PricesAt.ToLocalTime():yyyy-MM-dd HH:mm}; flea fee {flea.OfferFeeRate:0.###}/{flea.RequirementFeeRate:0.###}, " +
                          $"found in raid {(flea.FoundInRaidRequired ? "required" : "not required")}; {(online ? "online" : $"cache only, {refused} requests refused")}");
        if (audit)
            Console.WriteLine(AuditLine(data, sources, paths.DataCache));
        if (inputs.Count == 0)
            return;

        var reader = DeskReader.Create("en");
        if (reader is null)
        {
            Console.WriteLine("This Windows has no text recognition language: no reading.");
            return;
        }
        var tally = new Tally();
        var totalMs = 0L;
        // The pictures in order through one page in hand, as the session takes them (the fold's changed regions, the
        // windows attached across pictures).
        var ledger = new DeskLedger();
        foreach (var file in files)
        {
            using var picture = StashPicture.Decode(await File.ReadAllBytesAsync(file));
            if (picture is null)
            {
                Console.WriteLine($"== {Path.GetFileName(file)}: not a picture");
                continue;
            }
            var at = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var look = raid == true && !await reader.ShowsTabsAsync(picture, CancellationToken.None)
                ? new DeskLook(picture.Width, picture.Height, 0, new DeskLayoutFound(DeskScreen.None, [], [], null, [], 0, []), [], null, 0, 0, 0)
                : await reader.LookAsync(picture, catalogue, at, CancellationToken.None, raid);
            totalMs += look.TotalMs;
            if (Lattice && look.Layout.Stash is { } latticeGrid)
                PrintLattice(picture, latticeGrid, look.Pitch);
            if (Why && look.Layout.Screen is not (DeskScreen.None or DeskScreen.Trading))
                PrintLayout(picture, look);
            var named = look.Cells.Count(c => c.Cell.Ids.Count == 1);
            var unsure = look.Cells.Count(c => c.Cell.Ids.Count > 1);
            var silent = look.Cells.Count(c => c.Cell.Ids.Count == 0);
            var windows = look.Page?.Regions.Where(r => r.Kind == DeskRegionKind.ContainerWindow).ToList() ?? [];
            Console.WriteLine($"== {Path.GetFileName(file)}: {look.Layout.Screen}, {look.Page?.Regions.Count ?? 0} regions ({string.Join(", ", look.Page?.Regions.Select(r => r.Key) ?? [])}); " +
                              $"{look.Cells.Count} cells: named {named}, one of N {unsure}, nothing said {silent} ({look.Cells.Count(c => c.Contradicted)} by the footprint), odd {look.Odd}; " +
                              $"windows {windows.Count} ({windows.Count(x => x.AttachedTo is not null)} attached); frames unplaced {look.Layout.Unplaced}; layout {look.LayoutMs} ms, all {look.TotalMs} ms");
            // A raid's reading is published for the glance and let go: never the page in hand.
            if (look.Page is { } read && look.Layout.Screen != DeskScreen.Raid)
            {
                var change = ledger.Apply(mode, read);
                Console.WriteLine($"   page in hand: {(change.First ? "first reading" : $"changed {(change.Changed.Count == 0 ? "nothing" : string.Join(", ", change.Changed.Order(StringComparer.Ordinal)))}")}" +
                                  (change.Refused.Count > 0 ? $"; kept the page's {string.Join(", ", change.Refused.Order(StringComparer.Ordinal))} (read too blank)" : "") +
                                  (change.Kept.Count > 0 ? $"; kept the page's {string.Join(", ", change.Kept.Order(StringComparer.Ordinal).Select(k => $"{k} grid ({KeptWhy(read.Region(k))})"))}" : "") +
                                  (change.Dropped.Count > 0 ? $"; dropped {string.Join(", ", change.Dropped.Order(StringComparer.Ordinal))} (attached to nothing, not shown by {DeskLedger.MissedToDrop} readings)" : ""));
                if (Why)
                {
                    Console.WriteLine("   page: " + string.Join("; ", change.Page.InReadingOrder().Select(r =>
                        $"{r.Key} {r.Cols}x{r.Rows} {r.Parts.Count} parts {r.Cells.Count} cells{(r.Missed > 0 ? $" missed {r.Missed}" : "")}" +
                        (read.Region(r.Key) is { Partial: true } p ? $" (read in part: {string.Join(" ", p.Parts.Select(x => $"{x.I},{x.J} {x.W}x{x.H}"))})" : ""))));
                }
            }
            if (look.Page is { } page)
            {
                var attached = DeskLedger.Attach(page);
                var sheet = DeskSheet.Of(attached, id => DeskData.Facts(sources, id), flea, keep);
                if (!quiet)
                    Print(sheet, data, sources, look);
                var t = sheet.Totals;
                Console.WriteLine($"   ON YOU {Money(t.OnYou)}{(t.OnYouPlus ? "+" : "")} ₽ · STASH {Money(t.Stash)}{(t.StashPlus ? "+" : "")} ₽" +
                                  (look.Layout.Screen == DeskScreen.Raid ? $" · LOOT {Money(t.Loot)}{(t.LootPlus ? "+" : "")} ₽" : "") +
                                  $" · KEEP {t.KeepNow} · {t.KeepNext} · {t.KeepLater}");
                if (look.Layout.Loot is { } loot)
                {
                    Console.WriteLine($"   {loot.Title}: " + string.Join("; ", sheet.Regions.Where(r => r.Region.IsLoot).Select(r => RegionTally(r.Region))));
                    if (Why)
                    {
                        Console.WriteLine($"   pane {loot.Pane}; slots {string.Join(", ", loot.Slots.Select(x => $"{x.Key} {x.Box}"))}");
                        foreach (var part in loot.Parts)
                            Console.WriteLine($"   part {part.Kind} slot {part.SlotBox} {part.Search}{(part.Cut ? " cut" : "")}: {string.Join(", ", part.Parts.Select(x => x.ToString()))}");
                    }
                }
                if (look.Layout.Screen == DeskScreen.Raid)
                    Console.WriteLine("   ON YOU: " + string.Join("; ", sheet.Regions.Where(r => r.Region.OnYou).Select(r => RegionTally(r.Region))));
                if (plate is { } room && look.Layout.Screen == DeskScreen.Raid)
                    PrintPlate(sheet, data, room.Width, room.Height);
                foreach (var w in attached.Regions.Where(r => r.Kind == DeskRegionKind.ContainerWindow))
                    Console.WriteLine($"   window \"{w.Title}\": " + (w.AttachedTo is { } a ? $"attached to {a.Region} {a.Slot ?? $"{a.I},{a.J}"}" : $"attached to none ({w.SameName} of its name on the page)") +
                                      $", title names {w.TitleIds.Count} items");
            }
            var truthPath = Path.Combine(Path.GetDirectoryName(file)!, "truth-" + Path.GetFileNameWithoutExtension(file) + ".txt");
            if (File.Exists(truthPath))
                Score(look, picture, truthPath, data, tally);
            var regionsPath = Path.Combine(Path.GetDirectoryName(file)!, "regions-" + Path.GetFileNameWithoutExtension(file) + ".txt");
            if (File.Exists(regionsPath))
                ScoreRegions(look, regionsPath, tally);
        }
        if (files.Count > 1 && ledger.Page(mode) is { } merged)
        {
            var sheet = DeskSheet.Of(merged, id => DeskData.Facts(sources, id), flea, keep);
            var t = sheet.Totals;
            Console.WriteLine();
            Console.WriteLine($"The page in hand after {files.Count} pictures: " +
                              string.Join(", ", sheet.Regions.Select(r => $"{r.Region.Key}{(r.Region.Cols > 0 ? $" {r.Region.Cols}×{r.Region.Rows}" : "")} {r.Cells.Count}")) +
                              $"; ON YOU {Money(t.OnYou)}{(t.OnYouPlus ? "+" : "")} ₽ · STASH {Money(t.Stash)}{(t.StashPlus ? "+" : "")} ₽ · KEEP {t.KeepNow} · {t.KeepNext} · {t.KeepLater}");
            foreach (var w in merged.Regions.Where(r => r.Kind == DeskRegionKind.ContainerWindow))
                Console.WriteLine($"   window \"{w.Title}\": " + (w.AttachedTo is { } a ? $"attached to {a.Region} {a.Slot ?? $"{a.I},{a.J}"}" : $"attached to none ({w.SameName} of its name on the page)"));
        }
        if (tally.Pictures > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"Against {tally.Pictures} truths ({tally.Truths} items): right {tally.Right}, wrong {tally.Wrong}, one of N {tally.OneOf} (the truth among them) + " +
                              $"{tally.OneOfOther} (not among them), nothing said {tally.Silent}, no cell read there {tally.Missing}");
        }
        if (tally.RegionPictures > 0)
        {
            Console.WriteLine($"Regions against {tally.RegionPictures} truths ({tally.Regions} regions): right {tally.RegionsRight}, missing {tally.RegionsMissing}, " +
                              $"of another size {tally.RegionsWrongSize}, made up {tally.RegionsExtra}; screens told wrong {tally.ScreensWrong}");
        }
        Console.WriteLine($"{files.Count} pictures, {(files.Count > 0 ? totalMs / files.Count : 0)} ms a picture" + (online ? "" : $"; requests refused: {refused}"));
    }

    // "--why": the layout as found, in pixels: the stash, the loadout's slot boxes, the grids the lines give (a window's has
    // two-pixel lines), the framed compartments, and each region's place (its slot box, compartments, a window's title and
    // area), with what the middle column left out.
    private static void PrintLayout(SKBitmap picture, DeskLook look)
    {
        static string Box(SKRectI r) => $"({r.Left},{r.Top} {r.Width}x{r.Height})";
        var layout = look.Layout;
        var gray = StashGray.Of(picture);
        var lines = new StashLines(gray);
        var grids = StashGrids.Find(lines, gray, picture.Width, picture.Height, look.Pitch);
        Console.WriteLine($"   layout at pitch {look.Pitch}: stash {(layout.Stash is { } s ? $"({s.X0},{s.Y0}) {s.Cols}x{s.Rows}" : "none")}; " +
                          $"slots {string.Join(", ", layout.Slots.Select(x => $"{x.Key} {Box(x.Box)}"))}");
        Console.WriteLine("   grids: " + string.Join(", ", grids.Select(g =>
        {
            var r = SKRectI.Create(g.X0, g.Y0, g.Cols * look.Pitch, g.Rows * look.Pitch);
            return $"{g.Place} {Box(r)} {g.Cols}x{g.Rows} line {DeskLayout.LineWidth(gray, r, look.Pitch)}";
        })));
        Console.WriteLine("   frames: " + string.Join(", ", layout.Frames.Select(Box)));
        foreach (var place in layout.Regions)
        {
            Console.WriteLine($"   place {place.Kind}{(place.Title is { } t ? $" \"{t}\"" : "")}: slot {(place.SlotBox is { } b ? Box(b) : "none")}, " +
                              $"parts {string.Join(" ", place.Parts.Select(Box))}{(place.Area is { } a ? $", area {Box(a)}" : "")}{(place.Cut ? ", cut" : "")}");
        }
        foreach (var note in layout.Notes)
            Console.WriteLine($"   note: {note}");
    }

    // Per row: each cell's spread once the gradient is out (E: read as empty), and for the line at its right and below: the
    // median height it stands out by, and the share (%) of its rows standing out by half the grid's frames' level or more.
    private static void PrintLattice(SKBitmap picture, StashGrid grid, int pitch)
    {
        var gray = StashGray.Of(picture);
        var lines = new StashLines(gray);
        var scale = Math.Min(picture.Width / 1920.0, picture.Height / 1080.0);
        var high = gray.HighPass((int)Math.Round(4 * scale));
        var m = pitch / 8;
        var all = new List<double>();
        for (var j = 0; j < grid.Rows; j++)
        {
            for (var i = 0; i < grid.Cols; i++)
            {
                int x = grid.X0 + i * pitch, y = grid.Y0 + j * pitch;
                if (i + 1 < grid.Cols && lines.Vertical(x + pitch, y + 4, y + pitch - 4) >= 0.9)
                    all.Add(Strong(gray, x + pitch, y + 4, y + pitch - 4, true, 0).Median);
                if (j + 1 < grid.Rows && lines.Horizontal(y + pitch, x + 4, x + pitch - 4) >= 0.9)
                    all.Add(Strong(gray, y + pitch, x + 4, x + pitch - 4, false, 0).Median);
            }
        }
        all.Sort();
        var level = all.Count > 0 ? all[all.Count / 2] : 0;
        Console.WriteLine($"   lattice of the stash at ({grid.X0},{grid.Y0}), {grid.Cols}x{grid.Rows}, frames' level {level:0}: per cell spread | right median,strong% / below median,strong%");
        for (var j = 0; j < grid.Rows; j++)
        {
            var row = new StringBuilder($"   r{j,2}:");
            for (var i = 0; i < grid.Cols; i++)
            {
                int x = grid.X0 + i * pitch, y = grid.Y0 + j * pitch;
                var spread = high.Spread(x + m, y + m, x + pitch - m, y + pitch - m);
                var (rm, rs) = Strong(gray, x + pitch, y + 4, y + pitch - 4, true, level / 2);
                var (bm, bs) = Strong(gray, y + pitch, x + 4, x + pitch - 4, false, level / 2);
                row.Append(CultureInfo.InvariantCulture, $" {spread,4:0.0}{(spread < 3.5 ? "E" : " ")}|{rm,3:0},{rs,3:0}/{bm,3:0},{bs,3:0}");
            }
            Console.WriteLine(row);
        }
    }

    // A line's median height and the share of its rows at least min high, at the column (row) of the best median.
    private static (double Median, double Strong) Strong(StashGray g, int at, int from, int to, bool vertical, double min)
    {
        var best = (Median: double.MinValue, Strong: 0.0);
        for (var c = at - 2; c <= at + 2; c++)
        {
            var hs = new List<double>();
            for (var t = from; t < to; t++)
            {
                if (vertical && c >= 3 && c + 4 < g.W && t >= 0 && t < g.H)
                    hs.Add(Math.Max(Math.Max(g[c - 1, t], g[c, t]), Math.Max(g[c + 1, t], g[c + 2, t])) - Math.Max(g[c - 3, t], g[c + 4, t]));
                else if (!vertical && c >= 3 && c + 4 < g.H && t >= 0 && t < g.W)
                    hs.Add(Math.Max(Math.Max(g[t, c - 1], g[t, c]), Math.Max(g[t, c + 1], g[t, c + 2])) - Math.Max(g[t, c - 3], g[t, c + 4]));
            }
            if (hs.Count == 0)
                continue;
            hs.Sort();
            var median = hs[hs.Count / 2];
            if (median > best.Median)
                best = (median, 100.0 * hs.Count(h => h >= min) / hs.Count);
        }
        return best;
    }

    // "--plate <w>x<h>": the in-raid glance's plate in a room this wide and high under its head, as the app lays it out
    // (GlancePlateRules.Layout; the app log's "room"): the cell size, each region's head, whole or cut short, with the items it
    // draws (its slots, and its grid's cells in the rows drawn), and the ALSO line.
    private static void PrintPlate(DeskSheet sheet, GameData data, double width, double height)
    {
        var regions = sheet.Regions.Where(r => r.Part == DeskPart.Loot).ToList();
        var laid = GlancePlateRules.Layout(regions, sheet.Page, width, height);
        Console.WriteLine($"   plate in {width:0}x{height:0} DIP: {laid.Shown.Count} regions at {laid.Cell:0} DIP, {laid.CutShortCount} cut short, " +
                          $"{laid.Cut.Count} in the ALSO line; it takes {laid.Width:0}x{laid.Height:0} DIP");
        for (var i = 0; i < laid.Shown.Count; i++)
        {
            var region = laid.Shown[i];
            var all = DeskPageRules.Grid(region.Region).Rows;
            var drawn = region.Cells.Where(c => c.Cell.IsSlot || c.Cell.J < laid.Rows[i]).Select(c => c.Cell.Ids.Count switch
            {
                0 => "?",
                1 => data.ItemShortName(c.Cell.Ids[0]),
                _ => "? " + data.ItemShortName(c.Cell.Ids[0]),
            });
            Console.WriteLine($"     column {laid.Columns[i] + 1}: {GlancePlateRules.HeadLine(region.Region, sheet.Page, laid.CutShort(i)).Replace('\u00A0', ' ')}" +
                              (all > 0 ? $" (rows {laid.Rows[i]} of {all})" : "") + ": " + string.Join(", ", drawn));
        }
        if (laid.Cut.Count > 0)
        {
            var (word, names) = GlancePlateRules.CutLine(laid.Cut, sheet.Page);
            Console.WriteLine($"     {word} · {string.Join(" · ", names)}");
        }
    }

    // Why a region read in part kept the page's grid, as the logs say it (ReadingLog.KeptWhy).
    private static string KeptWhy(DeskRegion? region) => ReadingLog.KeptWhy(region);

    // A region's tally: its cells named, one of N, unread, and a LOOT part's search state.
    private static string RegionTally(DeskRegion r) =>
        $"{r.Key} {r.Cells.Count(c => c.Ids.Count == 1)}/{r.Cells.Count(c => c.Ids.Count > 1)}/{r.Cells.Count(c => c.Ids.Count == 0)}" +
        (r.Search != DeskSearch.Searched ? $" {r.Search.ToString().ToUpperInvariant()}" : "") + (r.Cut ? " cut" : "") +
        (r.CoveredSlots.Count > 0 || r.Covered.Count > 0 ? " covered" : "") + (r.Partial ? " partial" : "");

    private static void Print(DeskSheet sheet, GameData data, ItemSources sources, DeskLook look)
    {
        foreach (var region in sheet.Regions)
        {
            var r = region.Region;
            var head = r.Kind == DeskRegionKind.ContainerWindow ? $"{r.Title?.ToUpperInvariant()} (window" + (r.AttachedTo is { } a ? $" in {a.Region})" : ", attached to none)")
                : r.IsLoot ? $"{r.Title} {r.Key.ToUpperInvariant()}" + (r.Search != DeskSearch.Searched ? $" ({r.Search.ToString().ToUpperInvariant()})" : "") + (r.Cut ? " (cut)" : "")
                : r.Key.ToUpperInvariant();
            Console.WriteLine($"   {head}: {r.Cols}×{r.Rows}, {r.Parts.Count} parts, {r.Cells.Count} cells, {region.Part}, total {Money(region.Total)}{(region.Plus ? "+" : "")}" +
                              (Why && r.Covered.Count > 0 ? $"; covered {string.Join(" ", r.Covered.Select(c => $"{c.I},{c.J}{(c.W * c.H > 1 ? $" {c.W}x{c.H}" : "")}"))}" : ""));
            foreach (var c in region.Cells)
            {
                var cell = c.Cell;
                var line = new StringBuilder("     ");
                line.Append(cell.IsSlot ? $"[{cell.Slot}]" : $"[{cell.I},{cell.J} {cell.W}×{cell.H}]");
                line.Append(' ');
                line.Append(cell.Ids.Count switch
                {
                    0 => "?",
                    1 => data.ItemShortName(cell.Ids[0]),
                    _ => $"one of {cell.Ids.Count}: " + string.Join(", ", cell.Ids.Take(6).Select(data.ItemShortName)) + (cell.Ids.Count > 6 ? ", …" : ""),
                });
                if (cell.Count > 1)
                    line.Append($" ×{cell.Count}");
                if (cell.Print is { } print)
                    line.Append($" {print}");
                if (cell.FoundInRaid == true)
                    line.Append(" FIR");
                if (c.Figure is { } f)
                {
                    var source = f.Unsure ? "range" : f.Source == DeskSource.Flea ? "flea" : f.Trader is { } trader ? data.TraderName(trader) : "trader";
                    var value = f.Unsure ? $"{Money(f.Value)}–{Money(f.High)}" : Money(f.Value);
                    line.Append($" = {(f.Estimate ? "~" : "")}{value}{(f.Plus ? "+" : "")} ({source}; tint {f.Tint}{(f.Arrow == DeskArrow.None ? "" : f.Arrow == DeskArrow.Up ? " ↑" : " ↓")}" +
                                (f.Full != f.Value && !f.Unsure ? $"; full {Money(f.Full)}" : "") + (f.NoFlea != DeskNoFlea.None ? $"; no flea: {f.NoFlea}" : "") + ")");
                }
                else if (cell.Ids.Count > 0)
                    line.Append(" = no figure");
                if (c.Unseen && c.Figure is null)
                    line.Append(" +");
                if (c.Keep != DeskKeepLevel.None)
                    line.Append($" KEEP {c.Keep.ToString().ToLowerInvariant()}");
                if (Why && look.Cells.FirstOrDefault(l => l.Region == r.Key && l.Cell.I == cell.I && l.Cell.J == cell.J && l.Cell.Slot == cell.Slot) is { } seen)
                    line.Append($"   | label \"{seen.Label}\" fit {seen.Fit:0.00}" + (seen.Contradicted ? " FOOTPRINT" : "") + (seen.CountText is { } ct ? $" count \"{ct}\"" : ""));
                Console.WriteLine(line);
            }
        }
    }

    /// <summary>
    /// The stash desk's line of the <c>bring</c> audit (docs/UPDATES.md): what its values assume of the item file. The
    /// catalogue (items with a short name, presets left out); NO PRICE, the items with neither a last low nor a trader's
    /// price (the cell shows no figure: the secure containers, quest keys and the like); NO FOOTPRINT, items without width
    /// and height (the tie-break can't run: a cell of theirs says nothing); ONE OF N, the flea-priced items that aren't
    /// guns sharing their short name and footprint with another (the "?" cells the art might one day tell apart); the
    /// flea's rules (a fee rate, the found-in-raid rule or the level changed: the figures move); the hideout's needs; and
    /// the price histories the cards fetched into the cache (step C), read as the card reads them: UNREAD is a file whose
    /// points' shape changed (the card's band says "No price history at hand"), FEW one with under a day's points in its
    /// last 30 days (no 2 h points: the band would be days). Cache only, nothing asked.
    /// </summary>
    public static string AuditLine(GameData data, ItemSources sources, string? cacheFolder = null)
    {
        var candidates = DeskData.Candidates(data, sources).ToList();
        var facts = candidates.Select(c => (Candidate: c, Facts: DeskData.Facts(sources, c.ItemId))).ToList();
        var noPrice = facts.Count(f => f.Facts is not { } x || (x.LastLow is not > 0 && x.Traders.Count == 0));
        var noFootprint = candidates.Count(c => sources.Items.GetValueOrDefault(c.ItemId) is not { Width: > 0, Height: > 0 });
        var priced = facts.Where(f => f.Facts is { NoFlea: false, LastLow: > 0 } x && (x.Kind & DeskItemKind.Gun) == 0).ToList();
        var shared = priced.GroupBy(f => (Core.Screenshots.ExitList.Fold(f.Candidate.ShortName), Math.Min(f.Candidate.W, f.Candidate.H), Math.Max(f.Candidate.W, f.Candidate.H)))
            .Where(g => g.Count() > 1).Sum(g => g.Count());
        var flea = DeskData.Flea(sources);
        return $"DESK: {candidates.Count} items with a short name; NO PRICE {noPrice}; NO FOOTPRINT {noFootprint}; " +
               $"ONE OF N {shared} of {priced.Count} flea-priced (not guns) share short name and footprint; " +
               $"flea fee {flea.OfferFeeRate:0.###}/{flea.RequirementFeeRate:0.###}, found in raid {(flea.FoundInRaidRequired ? "required" : "not required")}, " +
               $"from level {flea.MinPlayerLevel}{(sources.FleaMarket is null ? " (NO FLEA RULES in the file: the rates of 2026-10-19)" : "")}; " +
               $"hideout needs {sources.HideoutNeeds.Count} ({sources.HideoutNeeds.Select(n => n.ItemId).Distinct().Count()} items)" +
               (cacheFolder is null ? "" : "; " + HistoryAudit(cacheFolder, data.Mode));
    }

    // The price histories in the cache for the mode: how many, how many read, how many with too few recent points.
    private static string HistoryAudit(string cacheFolder, Core.Logs.GameMode mode)
    {
        var files = Directory.Exists(cacheFolder)
            ? Directory.GetFiles(cacheFolder, $"{GameData.Slug(mode)}_prices_*.json").Where(f => !f.EndsWith(".meta.json", StringComparison.Ordinal)).ToList()
            : [];
        if (files.Count == 0)
            return "HISTORY none cached (a card fetches one when it first shows an item)";
        var read = files.Select(f => DeskHistory.Parse(File.ReadAllText(f))).ToList();
        var unread = read.Count(h => h is null || h.Points.Count == 0);
        var few = read.Count(h => h is { Points.Count: > 0 } x && x.Recent(DeskHistory.Band).Count < 12);
        var points = read.OfType<DeskHistory>().Where(h => h.Points.Count > 0).Select(h => h.Recent(DeskHistory.Band).Count).DefaultIfEmpty(0).Average();
        return $"HISTORY {files.Count} cached, {points:0} points in their last 30 days on average; UNREAD {unread}; FEW {few}";
    }

    private static string Money(double value) => value switch
    {
        >= 1_000_000 => (value / 1_000_000).ToString("0.00", CultureInfo.InvariantCulture) + " M",
        >= 1_000 => (value / 1_000).ToString("0", CultureInfo.InvariantCulture) + "k",
        _ => value.ToString("0", CultureInfo.InvariantCulture),
    };

    // The truth's cells by the stash spike's grids (StashGrids, biggest first), each found by its middle on the page: the
    // truth's row 0 may be a row lower in the reading (the offset that matches most names is the truth's, as the spike's).
    private static void Score(DeskLook look, SKBitmap picture, string truthPath, GameData data, Tally tally)
    {
        var truth = new List<(char Grid, int I, int J, string Name)>();
        var byRegion = new List<(string Region, string? Slot, int I, int J, string Name)>();
        foreach (var line in File.ReadAllLines(truthPath).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')))
        {
            var parts = line.Split(' ', 3);
            if (parts[0].Length == 1 && char.IsAsciiLetterUpper(parts[0][0]))
            {
                var cell = parts[1].Split(',');
                truth.Add((parts[0][0], int.Parse(cell[0], CultureInfo.InvariantCulture), int.Parse(cell[1], CultureInfo.InvariantCulture), parts[2].Trim()));
            }
            else if (parts[1].StartsWith('['))
                byRegion.Add((parts[0], parts[1].Trim('[', ']'), 0, 0, parts[2].Trim()));
            else
            {
                var cell = parts[1].Split(',');
                byRegion.Add((parts[0], null, int.Parse(cell[0], CultureInfo.InvariantCulture), int.Parse(cell[1], CultureInfo.InvariantCulture), parts[2].Trim()));
            }
        }
        if (byRegion.Count > 0)
            ScoreByRegion(look, byRegion, data, tally);
        if (truth.Count == 0)
            return;
        var gray = StashGray.Of(picture);
        var lines = new StashLines(gray);
        var grids = StashGrids.Find(lines, gray, picture.Width, picture.Height, look.Pitch).Where(g => g.Place != StashGridPlace.Message).ToList();
        int right = 0, wrong = 0, oneOf = 0, other = 0, silent = 0, missing = 0;
        foreach (var group in truth.GroupBy(t => t.Grid))
        {
            var index = group.Key - 'A';
            if (index < 0 || index >= grids.Count)
            {
                missing += group.Count();
                continue;
            }
            var grid = grids[index];
            DeskCellLook? At(int i, int j)
            {
                int x = grid.X0 + i * look.Pitch + look.Pitch / 2, y = grid.Y0 + j * look.Pitch + look.Pitch / 2;
                return look.Cells.FirstOrDefault(c => !c.Cell.IsSlot && c.Pixels.Contains(x, y));
            }
            var offset = Enumerable.Range(0, Math.Max(1, grid.Rows))
                .Select(d => (d, Hits: group.Count(t => At(t.I, t.J + d) is { } c && c.Cell.Ids.Any(id => data.ItemShortName(id) == t.Name))))
                .OrderByDescending(x => x.Hits).ThenBy(x => x.d).First().d;
            foreach (var t in group)
            {
                var found = At(t.I, t.J + offset);
                var names = found?.Cell.Ids.Select(data.ItemShortName).ToList() ?? [];
                if (found is null)
                    missing++;
                else if (names.Count == 0)
                    silent++;
                else if (names.Count == 1 && names[0] == t.Name)
                    right++;
                else if (names.Count == 1)
                {
                    wrong++;
                    Console.WriteLine($"   WRONG {t.Grid} {t.I},{t.J}: read {names[0]} (label \"{found.Label}\"), truth {t.Name}");
                }
                else if (names.Contains(t.Name))
                    oneOf++;
                else
                {
                    other++;
                    Console.WriteLine($"   NOT AMONG {t.Grid} {t.I},{t.J}: one of {names.Count} ({string.Join(", ", names.Distinct().Take(4))}), truth {t.Name}");
                }
            }
        }
        Console.WriteLine($"   truth: {truth.Count} items: right {right}, wrong {wrong}, one of N {oneOf} + {other} not among them, nothing said {silent}, no cell read {missing}");
        var footprintPath = Path.Combine(Path.GetDirectoryName(truthPath)!, "footprints-" + Path.GetFileName(truthPath)["truth-".Length..]);
        if (File.Exists(footprintPath))
            ScoreFootprints(look, truth, grids, footprintPath, data);
        tally.Pictures++;
        tally.Truths += truth.Count;
        tally.Right += right;
        tally.Wrong += wrong;
        tally.OneOf += oneOf;
        tally.OneOfOther += other;
        tally.Silent += silent;
        tally.Missing += missing;
    }

    // A truth by the page's regions (2026-10-25): each line's cell found in the reading's region of that key, a slot by its
    // key, a grid's cell by the cell that takes its top left; the same tally as the grids' truth.
    private static void ScoreByRegion(DeskLook look, List<(string Region, string? Slot, int I, int J, string Name)> truth, GameData data, Tally tally)
    {
        int right = 0, wrong = 0, oneOf = 0, other = 0, silent = 0, missing = 0;
        foreach (var t in truth)
        {
            var region = look.Page?.Region(t.Region);
            var found = region?.Cells.FirstOrDefault(c => t.Slot is not null ? c.Slot == t.Slot : !c.IsSlot && c.Rect.Contains(new DeskRect(t.I, t.J, 1, 1)));
            var names = found?.Ids.Select(data.ItemShortName).ToList() ?? [];
            var where = t.Slot is not null ? $"{t.Region} [{t.Slot}]" : $"{t.Region} {t.I},{t.J}";
            if (found is null)
            {
                missing++;
                Console.WriteLine($"   NO CELL {where}: truth {t.Name}");
            }
            else if (names.Count == 0)
                silent++;
            else if (names.Count == 1 && names[0] == t.Name)
                right++;
            else if (names.Count == 1)
            {
                wrong++;
                Console.WriteLine($"   WRONG {where}: read {names[0]}, truth {t.Name}");
            }
            else if (names.Contains(t.Name))
                oneOf++;
            else
            {
                other++;
                Console.WriteLine($"   NOT AMONG {where}: one of {names.Count} ({string.Join(", ", names.Distinct().Take(4))}), truth {t.Name}");
            }
        }
        Console.WriteLine($"   truth by region: {truth.Count} items: right {right}, wrong {wrong}, one of N {oneOf} + {other} not among them, nothing said {silent}, no cell read {missing}");
        tally.Pictures++;
        tally.Truths += truth.Count;
        tally.Right += right;
        tally.Wrong += wrong;
        tally.OneOf += oneOf;
        tally.OneOfOther += other;
        tally.Silent += silent;
        tally.Missing += missing;
    }

    // The regions a picture should make (regions-<picture>.txt, 2026-10-25): its screen, then each region's key and the size
    // of its grid in view; a region made that the truth hasn't is made up (a window of a misread heading, a region's grid
    // taken for a window). A key listed twice counts once, by its first line, and is said (code review, 2026-10-25: it threw).
    private static void ScoreRegions(DeskLook look, string path, Tally tally)
    {
        var lines = File.ReadAllLines(path).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')).ToList();
        var screen = lines.FirstOrDefault(l => l.StartsWith("screen ", StringComparison.Ordinal))?["screen ".Length..].Trim() ?? "None";
        var truth = new Dictionary<string, string>(StringComparer.Ordinal);
        var repeated = new List<string>();
        foreach (var p in lines.Where(l => !l.StartsWith("screen ", StringComparison.Ordinal)).Select(l => l.Split(' ', 2)))
        {
            if (!truth.TryAdd(p[0], p.Length > 1 ? p[1].Trim() : ""))
                repeated.Add(p[0]);
        }
        var read = look.Page?.Regions ?? [];
        string Size(DeskRegion r) => r.Kind == DeskRegionKind.Loadout ? "" : $"{r.Cols}x{r.Rows}";
        var right = truth.Count(t => read.FirstOrDefault(r => r.Key == t.Key) is { } r && Size(r) == t.Value);
        var missing = truth.Keys.Where(k => read.All(r => r.Key != k)).ToList();
        var wrongSize = truth.Where(t => read.FirstOrDefault(r => r.Key == t.Key) is { } r && Size(r) != t.Value).Select(t => $"{t.Key} {Size(read.First(r => r.Key == t.Key))} (truth {t.Value})").ToList();
        var extra = read.Where(r => !truth.ContainsKey(r.Key)).Select(r => $"{r.Key} {Size(r)}").ToList();
        var screenRight = look.Layout.Screen.ToString() == screen;
        Console.WriteLine($"   regions: {right} of {truth.Count} right" + (screenRight ? "" : $"; SCREEN {look.Layout.Screen}, truth {screen}") +
                          (missing.Count > 0 ? $"; missing {string.Join(", ", missing)}" : "") + (wrongSize.Count > 0 ? $"; of another size {string.Join(", ", wrongSize)}" : "") +
                          (extra.Count > 0 ? $"; MADE UP {string.Join(", ", extra)}" : "") +
                          (repeated.Count > 0 ? $"; the truth lists {string.Join(", ", repeated)} again, its first line taken" : ""));
        tally.RegionPictures++;
        tally.Regions += truth.Count;
        tally.RegionsRight += right;
        tally.RegionsMissing += missing.Count;
        tally.RegionsWrongSize += wrongSize.Count;
        tally.RegionsExtra += extra.Count;
        tally.ScreensWrong += screenRight ? 0 : 1;
    }

    // Against the items' footprints as the picture shows them (footprints-<picture>.txt beside the truth, "A 0,7 2x1"; one by
    // one where not listed): an item is read whole when one cell stands on its footprint, else it is split (cut into pieces,
    // or joined with another); a whole cell names it right, as one of N, wrongly, or says nothing.
    private static void ScoreFootprints(DeskLook look, List<(char Grid, int I, int J, string Name)> truth, List<StashGrid> grids, string path, GameData data)
    {
        var footprints = new Dictionary<(char, int, int), (int W, int H)>();
        foreach (var line in File.ReadAllLines(path).Where(l => l.Trim().Length > 0))
        {
            var parts = line.Split(' ');
            var cell = parts[1].Split(',');
            var size = parts[2].Split('x');
            footprints[(parts[0][0], int.Parse(cell[0], CultureInfo.InvariantCulture), int.Parse(cell[1], CultureInfo.InvariantCulture))] =
                (int.Parse(size[0], CultureInfo.InvariantCulture), int.Parse(size[1], CultureInfo.InvariantCulture));
        }
        int right = 0, oneOf = 0, wrong = 0, silent = 0, split = 0, pieces = 0;
        var splits = new List<string>();
        foreach (var group in truth.GroupBy(t => t.Grid))
        {
            var index = group.Key - 'A';
            if (index < 0 || index >= grids.Count)
                continue;
            var grid = grids[index];
            var p = look.Pitch;
            SKRectI Rect(int i, int j, int w, int h) => SKRectI.Create(grid.X0 + i * p, grid.Y0 + j * p, w * p, h * p);
            bool Near(SKRectI a, SKRectI b) => Math.Abs(a.Left - b.Left) <= p / 4 && Math.Abs(a.Top - b.Top) <= p / 4 && Math.Abs(a.Right - b.Right) <= p / 4 && Math.Abs(a.Bottom - b.Bottom) <= p / 4;
            var stashCells = look.Cells.Where(c => !c.Cell.IsSlot).ToList();
            (int W, int H) Size((char Grid, int I, int J, string Name) t) => footprints.TryGetValue((t.Grid, t.I, t.J), out var f) ? f : (1, 1);
            var offset = Enumerable.Range(0, Math.Max(1, grid.Rows))
                .Select(d => (d, Hits: group.Count(t => stashCells.Any(c => Near(c.Pixels, Rect(t.I, t.J + d, Size(t).W, Size(t).H))))))
                .OrderByDescending(x => x.Hits).ThenBy(x => x.d).First().d;
            foreach (var t in group)
            {
                var (fw, fh) = Size(t);
                var area = Rect(t.I, t.J + offset, fw, fh);
                var whole = stashCells.FirstOrDefault(c => Near(c.Pixels, area));
                if (whole is null)
                {
                    split++;
                    var inside = stashCells.Where(c => SKRectI.Intersect(c.Pixels, area) is { IsEmpty: false } x && x.Width * x.Height * 2 > p * p).ToList();
                    pieces += inside.Count(c => c.Cell.Ids.Count == 0);
                    splits.Add($"{t.Name} {fw}x{fh} in {inside.Count} cells");
                    continue;
                }
                var names = whole.Cell.Ids.Select(data.ItemShortName).ToList();
                if (names.Count == 0)
                    silent++;
                else if (names.Count == 1 && names[0] == t.Name)
                    right++;
                else if (names.Count > 1 && names.Contains(t.Name))
                    oneOf++;
                else
                    wrong++;
            }
        }
        Console.WriteLine($"   footprints: {truth.Count} items: read whole {truth.Count - split} (right {right}, one of N {oneOf}, wrong {wrong}, nothing said {silent}), " +
                          $"split {split} ({pieces} \"?\" pieces)" + (splits.Count > 0 ? $": {string.Join(", ", splits)}" : ""));
    }

    // The transport of a cache-only run: every request refused, so the data comes from the cache (CachedHttp's offline path).
    private sealed class NoRequests(Action refused) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            refused();
            return Task.FromException<HttpResponseMessage>(new HttpRequestException("desk: cache only"));
        }
    }
}
