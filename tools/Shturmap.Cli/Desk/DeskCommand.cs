using System.Globalization;
using System.Text;
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
/// the hand-written truths beside the pictures (<c>truth-&lt;picture&gt;.txt</c>, the stash spike's format: "A 3,0 Marl
/// 214", the grid's letter biggest first). The pictures are the owner's own, in the git-ignored tests\fixtures\ocr; it
/// prints item names and counts, never a path's folder. The data comes from the cache: no request unless --online.
/// </summary>
internal static class DeskCommand
{
    // "--why": each cell's label as read, its fit, and a footprint's contradiction.
    private static bool Why;

    private sealed class Tally
    {
        public int Truths, Right, Wrong, OneOf, OneOfOther, Silent, Missing, Pictures;
    }

    public static async Task RunAsync(IReadOnlyList<string> args)
    {
        var online = false;
        var quiet = false;
        var audit = false;
        var mode = GameMode.Pve;
        var inputs = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--online": online = true; break;
                case "--quiet": quiet = true; break;
                case "--why": Why = true; break;
                case "--audit": audit = true; break;
                case "--mode": mode = args[++i] == "pvp" || args[i] == "regular" ? GameMode.Pvp : GameMode.Pve; break;
                default: inputs.Add(args[i]); break;
            }
        }
        if (inputs.Count == 0 && !audit)
        {
            Console.WriteLine("Use: shturmap-cli desk <png or folder>... [--quiet] [--online] [--mode pve|pvp]");
            Console.WriteLine("  each picture's page: regions, cells (name or \"one of N\", count, condition, figure and source, marks), the head line;");
            Console.WriteLine("  with truth-<picture>.txt beside a picture, the tally against it. --quiet: the tallies only.");
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
            var look = await reader.LookAsync(picture, catalogue, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), CancellationToken.None);
            totalMs += look.TotalMs;
            var named = look.Cells.Count(c => c.Cell.Ids.Count == 1);
            var unsure = look.Cells.Count(c => c.Cell.Ids.Count > 1);
            var silent = look.Cells.Count(c => c.Cell.Ids.Count == 0);
            var windows = look.Page?.Regions.Where(r => r.Kind == DeskRegionKind.ContainerWindow).ToList() ?? [];
            Console.WriteLine($"== {Path.GetFileName(file)}: {look.Layout.Screen}, {look.Page?.Regions.Count ?? 0} regions ({string.Join(", ", look.Page?.Regions.Select(r => r.Key) ?? [])}); " +
                              $"{look.Cells.Count} cells: named {named}, one of N {unsure}, nothing said {silent} ({look.Cells.Count(c => c.Contradicted)} by the footprint), odd {look.Odd}; " +
                              $"windows {windows.Count} ({windows.Count(x => x.AttachedTo is not null)} attached); frames unplaced {look.Layout.Unplaced}; layout {look.LayoutMs} ms, all {look.TotalMs} ms");
            if (look.Page is { } read)
            {
                var change = ledger.Apply(mode, read);
                Console.WriteLine($"   page in hand: {(change.First ? "first reading" : $"changed {(change.Changed.Count == 0 ? "nothing" : string.Join(", ", change.Changed.Order(StringComparer.Ordinal)))}")}" +
                                  (change.Refused.Count > 0 ? $"; kept the page's {string.Join(", ", change.Refused.Order(StringComparer.Ordinal))} (read too blank)" : ""));
            }
            if (look.Page is { } page)
            {
                var attached = DeskLedger.Attach(page);
                var sheet = DeskSheet.Of(attached, id => DeskData.Facts(sources, id), flea, keep);
                if (!quiet)
                    Print(sheet, data, sources, look);
                var t = sheet.Totals;
                Console.WriteLine($"   ON YOU {Money(t.OnYou)}{(t.OnYouPlus ? "+" : "")} ₽ · STASH {Money(t.Stash)}{(t.StashPlus ? "+" : "")} ₽ · KEEP {t.KeepNow} · {t.KeepNext} · {t.KeepLater}");
                foreach (var w in attached.Regions.Where(r => r.Kind == DeskRegionKind.ContainerWindow))
                    Console.WriteLine($"   window \"{w.Title}\": " + (w.AttachedTo is { } a ? $"attached to {a.Region} {a.Slot ?? $"{a.I},{a.J}"}" : $"attached to none ({w.SameName} of its name on the page)") +
                                      $", title names {w.TitleIds.Count} items");
            }
            var truthPath = Path.Combine(Path.GetDirectoryName(file)!, "truth-" + Path.GetFileNameWithoutExtension(file) + ".txt");
            if (File.Exists(truthPath))
                Score(look, picture, truthPath, data, tally);
        }
        if (files.Count > 1 && ledger.Page(mode) is { } merged)
        {
            var sheet = DeskSheet.Of(merged, id => DeskData.Facts(sources, id), flea, keep);
            var t = sheet.Totals;
            Console.WriteLine();
            Console.WriteLine($"The page in hand after {files.Count} pictures: " + string.Join(", ", sheet.Regions.Select(r => $"{r.Region.Key} {r.Cells.Count}")) +
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
        Console.WriteLine($"{files.Count} pictures, {(files.Count > 0 ? totalMs / files.Count : 0)} ms a picture" + (online ? "" : $"; requests refused: {refused}"));
    }

    private static void Print(DeskSheet sheet, GameData data, ItemSources sources, DeskLook look)
    {
        foreach (var region in sheet.Regions)
        {
            var r = region.Region;
            var head = r.Kind == DeskRegionKind.ContainerWindow ? $"{r.Title?.ToUpperInvariant()} (window" + (r.AttachedTo is { } a ? $" in {a.Region})" : ", attached to none)") : r.Key.ToUpperInvariant();
            Console.WriteLine($"   {head}: {r.Cols}×{r.Rows}, {r.Parts.Count} parts, {r.Cells.Count} cells, {region.Part}, total {Money(region.Total)}{(region.Plus ? "+" : "")}");
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
        foreach (var line in File.ReadAllLines(truthPath).Where(l => l.Trim().Length > 0))
        {
            var parts = line.Split(' ', 3);
            var cell = parts[1].Split(',');
            truth.Add((parts[0][0], int.Parse(cell[0], CultureInfo.InvariantCulture), int.Parse(cell[1], CultureInfo.InvariantCulture), parts[2].Trim()));
        }
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
        tally.Pictures++;
        tally.Truths += truth.Count;
        tally.Right += right;
        tally.Wrong += wrong;
        tally.OneOf += oneOf;
        tally.OneOfOther += other;
        tally.Silent += silent;
        tally.Missing += missing;
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
