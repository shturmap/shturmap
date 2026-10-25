using System.Diagnostics;
using System.Globalization;
using System.Text;
using Shturmap.Core.Desk;
using Shturmap.Core.Logs;
using Shturmap.Core.Screenshots;
using Shturmap.Data.Http;
using Shturmap.Data.TarkovDev;
using Shturmap.Game.Screenshots;
using Shturmap.Session;
using SkiaSharp;

namespace Shturmap.Cli.Ocr;

/// <summary>
/// <c>shturmap-cli ocr-compare --fallback &lt;png|folder&gt;... --paddle &lt;onnx&gt; &lt;dict&gt;</c>: the spike of 2026-10-25 that
/// measures "Windows first, Paddle only where Windows reads nothing" on the desk's own geometry. Every picture with a
/// truth beside it (the desk command's two formats: the stash spike's "A 3,0 Marl" and the regions' "loadout [earpiece]
/// ComTac II" / "rig 0,0 6L31") is read by the desk as the app reads it (<see cref="DeskReader"/>, Windows' loop), and
/// each truth item's cell is found as the desk command finds it. Three columns: the desk's naming (Windows' loop); Paddle
/// alone on the cell's label band (<see cref="DeskReader.LabelRect"/>, the band 0.2 of a cell high from three pixels
/// below the top, native size in a 4 px dark margin), matched as the desk matches (the slot's kind, the cell's width, the
/// cell's footprint); and the fallback: the desk's naming, Paddle's where the desk named nothing (no item, or no cell for
/// a slot whose box the layout found). Paddle also reads every other cell the desk left unread and every empty slot box on
/// the page: what it names there is outside the truth (a truth lists every item whose label shows), so a wrong. Built
/// with <c>-p:OcrSpike=true</c> only; nothing of it runs in the app. It prints item names, never a path's folder.
/// </summary>
internal static class OcrFallback
{
    private enum Outcome { Right, OneOf, NotAmong, Wrong, Unread, NoCell }

    private sealed record Item(string Page, string Where, string Truth, DeskCellLook? Look, SKRectI? Box, bool Slot, string? SlotKey, int W, int H, bool SpecialKind);

    private sealed record Said(Outcome Outcome, string Text, string Names, double Ms);

    public static async Task RunAsync(IReadOnlyList<string> args)
    {
        Console.OutputEncoding = Encoding.UTF8;
#if !OCR_SPIKE
        Console.WriteLine("This build has no ONNX Runtime: build the CLI with -p:OcrSpike=true for --fallback.");
        await Task.CompletedTask;
#else
        var threads = 1;
        string? model = null, dict = null;
        var inputs = new List<string>();
        var cellsOut = false;
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--threads": threads = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--paddle": (model, dict) = (args[i + 1], args[i + 2]); i += 2; break;
                case "--cells": cellsOut = true; break;
                default: inputs.Add(args[i]); break;
            }
        }
        var tasks = inputs.Remove("--tasks");
        if (inputs.Count == 0 || model is null || dict is null)
        {
            Console.WriteLine("Use: shturmap-cli ocr-compare --fallback <png or folder>... --paddle <model.onnx> <dict.txt> [--threads <n>] [--cells] [--tasks]");
            Console.WriteLine("  --tasks: the operational pictures' countdown and objective count bands (truth-<picture>.txt, the tasks command's).");
            return;
        }
        if (tasks)
        {
            await TasksAsync(inputs, model, dict, threads);
            return;
        }
        var files = inputs.SelectMany(p => Directory.Exists(p) ? Directory.GetFiles(p, "*.png").Order(StringComparer.Ordinal).ToArray() : [p])
            .Where(f => File.Exists(Path.Combine(Path.GetDirectoryName(f)!, "truth-" + Path.GetFileNameWithoutExtension(f) + ".txt"))).ToList();

        var paths = AppPaths.Default;
        var http = CachedHttp.CreateClient(new NoRequests());
        var loader = new GameDataLoader(new CachedHttp(http, paths.DataCache));
        var data = await loader.LoadAsync(GameMode.Pve, "en");
        var sources = await loader.LoadSourcesAsync(GameMode.Pve, "en");
        var catalogue = DeskData.Catalogue(data, sources);
        Console.WriteLine($"Catalogue: {catalogue.Count} items (cache only)");
        var labels = StashReader.Create("en");
        if (labels is null)
        {
            Console.WriteLine("This Windows has no text recognition language: no reading.");
            return;
        }
        var desk = new DeskReader(labels);
        var load = Stopwatch.StartNew();
        using var rec = new OcrSpike.PaddleRec(model, dict, threads);
        Console.WriteLine($"Paddle {rec.Name}: {rec.Classes} classes, loaded in {load.ElapsedMilliseconds} ms, {threads} thread(s)");

        var items = new List<Item>();
        var windows = new Dictionary<Item, Said>();
        var paddle = new Dictionary<Item, Said>();
        var windowsCellMs = new Dictionary<Item, double>();
        var sameLabel = 0;
        var outside = new List<string>();
        var outsideNamed = 0;
        var outsideRead = 0;
        var pageRows = new List<string>();
        double deskMsAll = 0, fallbackExtraMsAll = 0;
        var pictures = 0;
        var warmed = false;
        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            using var picture = StashPicture.Decode(await File.ReadAllBytesAsync(file));
            if (picture is null)
                continue;
            pictures++;
            if (!warmed)
            {
                using var warm = Crop(picture, SKRectI.Create(0, 0, 80, 16), 20);
                rec.Read(warm);
                warmed = true;
            }
            var sw = Stopwatch.StartNew();
            var look = await desk.LookAsync(picture, catalogue, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), CancellationToken.None);
            var deskMs = sw.Elapsed.TotalMilliseconds;
            deskMsAll += deskMs;
            var pitch = look.Pitch;
            var mine = Truth(Path.Combine(Path.GetDirectoryName(file)!, "truth-" + name + ".txt"), name, look, picture, data);
            items.AddRange(mine);
            var extraMs = 0.0;
            foreach (var it in mine)
            {
                windows[it] = Desk(it, data);
                paddle[it] = it.Box is { } box ? Paddle(rec, picture, box, pitch, it, catalogue, data) : new Said(Outcome.NoCell, "", "", 0);
                if (windows[it].Outcome is Outcome.Unread or Outcome.NoCell)
                    extraMs += paddle[it].Ms;
                // Windows' loop on this cell alone, as the desk reads it, for its time a cell.
                if (it.Box is { } b && it.Look is not null)
                {
                    var t = Stopwatch.StartNew();
                    var (label, _, _) = await labels.ReadLabelAsync(picture, DeskReader.LabelRect(picture, b, pitch, it.Slot ? 6 : 0), pitch,
                        text => catalogue.Match.Match(text, it.Slot ? (int)Math.Round(b.Width / (double)pitch) : it.W, KindOf(it)), DeskMatch.Enough,
                        CancellationToken.None, it.Slot ? null : Confirm);
                    windowsCellMs[it] = t.Elapsed.TotalMilliseconds;
                    if ((label ?? "") == (it.Look.Label ?? ""))
                        sameLabel++;
                }
            }
            // Every other cell the desk left unread, and every slot box with no cell: the fallback reads them too.
            var taken = mine.Where(i => i.Look is not null).Select(i => i.Look!).ToHashSet(ReferenceEqualityComparer.Instance);
            var takenBoxes = mine.Where(i => i.Box is not null).Select(i => i.Box!.Value).ToHashSet();
            var others = new List<(string Where, SKRectI Box, bool Slot, string? Key, int W, int H, bool Special)>();
            foreach (var c in look.Cells.Where(c => c.Cell.Ids.Count == 0 && !taken.Contains(c)))
                others.Add(($"{c.Region} {(c.Cell.IsSlot ? $"[{c.Cell.Slot}]" : $"{c.Cell.I},{c.Cell.J} {c.Cell.W}×{c.Cell.H}")}", c.Pixels, c.Cell.IsSlot, c.Cell.Slot,
                    c.Cell.W, c.Cell.H, look.Page?.Region(c.Region)?.Kind == DeskRegionKind.SpecialSlots));
            var slotted = look.Cells.Where(c => c.Cell.IsSlot).Select(c => c.Pixels).ToHashSet();
            foreach (var s in look.Layout.Slots.Where(s => !slotted.Contains(s.Box) && !takenBoxes.Contains(s.Box) && !Under(s.Box, look.Layout.Over)))
                others.Add(($"loadout [{s.Key}] (no cell)", s.Box, true, s.Key, 1, 1, false));
            foreach (var p in look.Layout.Regions.Where(p => p.SlotBox is { } sb && !slotted.Contains(sb) && !takenBoxes.Contains(sb) && !Under(sb, look.Layout.Over)))
                others.Add(($"{DeskRegion.KeyOf(p.Kind)} [{DeskRegion.KeyOf(p.Kind)}] (no cell)", p.SlotBox!.Value, true, DeskRegion.KeyOf(p.Kind), 1, 1, false));
            foreach (var o in others)
            {
                var probe = new Item(name, o.Where, "", null, o.Box, o.Slot, o.Key, o.W, o.H, o.Special);
                var said = Paddle(rec, picture, o.Box, pitch, probe, catalogue, data);
                extraMs += said.Ms;
                outsideRead++;
                if (said.Outcome is not (Outcome.Unread or Outcome.NoCell))
                {
                    outsideNamed++;
                    outside.Add($"{name} {o.Where}: Paddle \"{said.Text}\" names {said.Names}");
                }
            }
            fallbackExtraMsAll += extraMs;
            pageRows.Add($"| {name} | {mine.Count} | {look.Cells.Count} | {others.Count + mine.Count(i => windows[i].Outcome is Outcome.Unread or Outcome.NoCell && i.Box is not null)} | {deskMs:0} | {extraMs:0} | {deskMs + extraMs:0} |");
        }

        var fallback = items.ToDictionary(i => i, i => windows[i].Outcome is Outcome.Unread or Outcome.NoCell && paddle[i].Outcome != Outcome.NoCell ? paddle[i] : windows[i]);
        Console.WriteLine($"{items.Count} truth items on {pictures} pictures; {items.Count(i => i.Look is null)} with no desk cell ({items.Count(i => i.Look is null && i.Box is not null)} of them slots whose box the layout found); " +
                          $"Windows' loop on the cell alone gave the desk's label for {sameLabel} of {windowsCellMs.Count}");
        Console.WriteLine();
        Console.WriteLine("| Column | right | one of N (truth among) | wrong | one of N (truth not among) | unread | no cell | ms a cell (median / mean) |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|");
        void Row(string label, Dictionary<Item, Said> said, Func<Item, double?> ms)
        {
            var o = items.Select(i => said[i].Outcome).ToList();
            var times = items.Select(ms).OfType<double>().Order().ToList();
            Console.WriteLine($"| {label} | {o.Count(x => x == Outcome.Right)} | {o.Count(x => x == Outcome.OneOf)} | {o.Count(x => x == Outcome.Wrong)} | {o.Count(x => x == Outcome.NotAmong)} | " +
                              $"{o.Count(x => x == Outcome.Unread)} | {o.Count(x => x == Outcome.NoCell)} | {(times.Count > 0 ? times[times.Count / 2] : 0):0.0} / {(times.Count > 0 ? times.Average() : 0):0.0} |");
        }
        Row("Windows' loop alone (the desk as it reads)", windows, i => windowsCellMs.TryGetValue(i, out var m) ? m : null);
        Row($"{rec.Name} alone (band)", paddle, i => paddle[i].Outcome == Outcome.NoCell ? null : paddle[i].Ms);
        Row("Fallback: Windows, then Paddle where unread", fallback, i => (windowsCellMs.TryGetValue(i, out var m) ? m : 0) +
                                                                        (windows[i].Outcome is Outcome.Unread or Outcome.NoCell && paddle[i].Outcome != Outcome.NoCell ? paddle[i].Ms : 0));

        Console.WriteLine();
        Console.WriteLine("Per page (right / one of N / wrong+not among / unread+no cell):");
        foreach (var page in items.Select(i => i.Page).Distinct())
        {
            var p = items.Where(i => i.Page == page).ToList();
            string Of(Dictionary<Item, Said> s) => $"{p.Count(i => s[i].Outcome == Outcome.Right)}/{p.Count(i => s[i].Outcome == Outcome.OneOf)}/" +
                                                   $"{p.Count(i => s[i].Outcome is Outcome.Wrong or Outcome.NotAmong)}/{p.Count(i => s[i].Outcome is Outcome.Unread or Outcome.NoCell)}";
            Console.WriteLine($"   {page} ({p.Count}): Windows {Of(windows)}; Paddle {Of(paddle)}; fallback {Of(fallback)}");
        }

        Console.WriteLine();
        Console.WriteLine("What the fallback changed (Windows unread, Paddle's reading taken):");
        foreach (var i in items.Where(i => windows[i].Outcome is Outcome.Unread or Outcome.NoCell && paddle[i].Outcome != Outcome.NoCell))
            Console.WriteLine($"   {(fallback[i].Outcome == Outcome.Right ? "FIXED" : fallback[i].Outcome is Outcome.Wrong or Outcome.NotAmong ? "WRONG" : fallback[i].Outcome == Outcome.OneOf ? "ONE OF N" : "still unread")} " +
                              $"{i.Page} {i.Where} {i.Truth}: Windows \"{windows[i].Text}\" → Paddle \"{paddle[i].Text}\"{(paddle[i].Names.Length > 0 ? $" ({paddle[i].Names})" : "")}");
        Console.WriteLine();
        Console.WriteLine("Where Windows and Paddle alone differ (the cell named otherwise):");
        foreach (var i in items.Where(i => windows[i].Outcome != paddle[i].Outcome && !(windows[i].Outcome is Outcome.Unread or Outcome.NoCell)))
            Console.WriteLine($"   {i.Page} {i.Where} {i.Truth}: Windows {windows[i].Outcome} \"{windows[i].Text}\" ({windows[i].Names}); Paddle {paddle[i].Outcome} \"{paddle[i].Text}\" ({paddle[i].Names})");
        Console.WriteLine();
        Console.WriteLine($"Outside the truth: Paddle read {outsideRead} other cells the desk left unread and empty slot boxes; it named something on {outsideNamed}:");
        foreach (var line in outside)
            Console.WriteLine("   " + line);
        Console.WriteLine();
        Console.WriteLine("Per picture (the desk's whole reading, then Paddle on what the fallback reads):");
        Console.WriteLine("| Picture | truth items | desk cells | crops Paddle reads | desk ms | fallback's extra ms | fallback ms |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        foreach (var row in pageRows)
            Console.WriteLine(row);
        Console.WriteLine($"Mean a picture: desk {deskMsAll / Math.Max(1, pictures):0} ms, fallback's extra {fallbackExtraMsAll / Math.Max(1, pictures):0} ms ({threads} Paddle thread(s))");
        if (cellsOut)
        {
            Console.WriteLine();
            foreach (var i in items)
                Console.WriteLine($"   {i.Page} {i.Where} {i.Truth}: Windows {windows[i].Outcome} \"{windows[i].Text}\"; Paddle {paddle[i].Outcome} \"{paddle[i].Text}\" ({paddle[i].Names}); {paddle[i].Ms:0.0} ms");
        }
#endif
    }

#if OCR_SPIKE
    /// <summary>
    /// The task bands: each truth countdown's band (a table row's, a trader's list entry's) and each truth objective's
    /// line band, read four ways: the 14-way vote as TasksReader votes (its ways, the value most of them parse; without its
    /// days-split second vote); what the tasks reader took in the end (the page's words first, then the vote and the
    /// days-split); Paddle on the band at its own size; and the fallback, the vote, then Paddle where no way parsed.
    /// </summary>
    private static async Task TasksAsync(List<string> inputs, string model, string dict, int threads)
    {
        var ocr = TasksReader.Create();
        if (ocr is null)
        {
            Console.WriteLine("This Windows has no English text recognition: no reading.");
            return;
        }
        using var rec = new OcrSpike.PaddleRec(model, dict, threads);
        Console.WriteLine($"Paddle {rec.Name}, {threads} thread(s); the vote of {TasksReader.Ways.Count} ways");
        var files = inputs.SelectMany(p => Directory.Exists(p) ? Directory.GetFiles(p, "*.png").Order(StringComparer.Ordinal).ToArray() : [p])
            .Where(f => File.Exists(Path.Combine(Path.GetDirectoryName(f)!, "truth-" + Path.GetFileNameWithoutExtension(f) + ".txt"))).ToList();
        var rows = new List<(string Kind, string Page, string What, string Truth, string? Vote, string? Production, string? Paddle, string PaddleText, double VoteMs, double PaddleMs)>();
        var warmed = false;
        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            using var picture = StashPicture.Decode(await File.ReadAllBytesAsync(file));
            if (picture is null)
                continue;
            if (!warmed)
            {
                using var warm = Crop(picture, SKRectI.Create(0, 0, 80, 16), 4);
                rec.Read(warm);
                warmed = true;
            }
            var truth = TaskTruth.Load(Path.Combine(Path.GetDirectoryName(file)!, "truth-" + name + ".txt"));
            var page = await ocr.ReadPageAsync(picture, 1);
            var bands = new List<(string Kind, string What, TaskBand Band, string Truth, string? Production)>();
            if (truth.Screen == "trader" && page.Screen == TaskScreen.TraderScreen)
            {
                for (var i = 0; i < truth.Entries.Count && i < page.Rows.Count; i++)
                    bands.Add(("countdown", $"entry {i} {truth.Entries[i].Name}", page.Rows[i].TimeBand, Hms(truth.Entries[i].Time),
                        page.Rows[i].TimeLeft is { } left ? TaskTable.Hms(left) : null));
                if (truth.Task is { } task)
                {
                    var wanted = truth.Objectives.Where(o => TaskTruth.Same(o.Row, task.Name)).ToList();
                    var said = page.Selected?.Objectives ?? [];
                    for (var i = 0; i < wanted.Count && i < page.ObjectiveBands.Count; i++)
                        bands.Add(("count", $"objective {i} of {task.Name}", page.ObjectiveBands[i], wanted[i].Count,
                            said.ElementAtOrDefault(i) is { } o ? TaskTruth.Count(o) : null));
                }
            }
            else if (truth.Screen == "table" && page.Screen == TaskScreen.Table)
            {
                var matched = new HashSet<TaskRow>();
                for (var index = 0; index < truth.Rows.Count; index++)
                {
                    var t = truth.Rows[index];
                    var row = page.Rows.FirstOrDefault(r => !matched.Contains(r) && TaskTruth.Same(r.Name, t.Name));
                    if (row is null && page.Rows.ElementAtOrDefault(index) is { } byOrder && !matched.Contains(byOrder))
                        row = byOrder;
                    if (row is null)
                        continue;
                    matched.Add(row);
                    bands.Add(("countdown", $"row {t.Name}", row.TimeBand, Hms(t.Time), row.TimeLeft is { } left ? TaskTable.Hms(left) : null));
                    var wanted = truth.Objectives.Where(o => TaskTruth.Same(o.Row, t.Name)).ToList();
                    for (var i = 0; i < wanted.Count && i < row.ObjectiveBands.Count; i++)
                        bands.Add(("count", $"objective {i} of {t.Name}", row.ObjectiveBands[i], wanted[i].Count,
                            row.Objectives.ElementAtOrDefault(i) is { } o ? TaskTruth.Count(o) : null));
                }
            }
            foreach (var (kind, what, band, want, production) in bands)
            {
                Func<string, string?> parse = kind == "countdown"
                    ? t => TaskTable.TimeLeft(t) is { } v ? TaskTable.Hms(v) : null
                    : t => TaskTable.CountValue(TaskTable.CountOf(t)) is { } v ? $"{v.Done}/{v.Of}" : null;
                var sw = Stopwatch.StartNew();
                var votes = new List<string>();
                foreach (var way in TasksReader.Ways)
                {
                    if (parse(await ocr.ReadBandAsync(picture, band, way, CancellationToken.None)) is { } value)
                        votes.Add(value);
                }
                var vote = votes.GroupBy(v => v).OrderByDescending(g => g.Count()).ThenBy(g => votes.IndexOf(g.Key)).FirstOrDefault()?.Key;
                var voteMs = sw.Elapsed.TotalMilliseconds;
                var rect = SKRectI.Intersect(SKRectI.Create(band.X, band.Y, band.W, band.H), SKRectI.Create(0, 0, picture.Width, picture.Height));
                sw.Restart();
                string text;
                using (var block = StashPicture.Block(picture, rect, rect.Width, rect.Height))
                using (var framed = StashPicture.Framed(block, 4, block.GetPixel(1, 1)))
                    text = rec.Read(framed);
                var paddleMs = sw.Elapsed.TotalMilliseconds;
                rows.Add((kind, name, what, want, vote, production, parse(text), text, voteMs, paddleMs));
            }
        }

        // A truth count "-" is a line without one: reading none is right there.
        static string Score(string? said, string truth) => said is null ? (truth == "-" ? "right" : "unread") : said == truth ? "right" : "wrong";
        Console.WriteLine();
        Console.WriteLine("| Bands | Column | right | wrong | unread | ms a band (mean) |");
        Console.WriteLine("|---|---|---|---|---|---|");
        foreach (var kind in new[] { "countdown", "count" })
        {
            var mine = rows.Where(r => r.Kind == kind).ToList();
            void Row(string column, Func<(string Kind, string Page, string What, string Truth, string? Vote, string? Production, string? Paddle, string PaddleText, double VoteMs, double PaddleMs), string?> said, string ms)
            {
                var s = mine.Select(r => Score(said(r), r.Truth)).ToList();
                Console.WriteLine($"| {kind} ({mine.Count}) | {column} | {s.Count(x => x == "right")} | {s.Count(x => x == "wrong")} | {s.Count(x => x == "unread")} | {ms} |");
            }
            Row("Windows, the 14-way vote", r => r.Vote, $"{(mine.Count > 0 ? mine.Average(r => r.VoteMs) : 0):0}");
            Row("Windows, the tasks reader as it reads (words, vote, days-split)", r => r.Production, "");
            Row($"{rec.Name} on the band", r => r.Paddle, $"{(mine.Count > 0 ? mine.Average(r => r.PaddleMs) : 0):0.0}");
            Row("Fallback: the vote, then Paddle where no way read", r => r.Vote ?? r.Paddle, "");
            Row("Fallback: the reader, then Paddle where it took none", r => r.Production ?? r.Paddle, "");
        }
        Console.WriteLine();
        foreach (var r in rows)
            Console.WriteLine($"   {r.Kind} {r.Page} {r.What}: truth {r.Truth}; vote {r.Vote ?? "none"}; reader {r.Production ?? "none"}; Paddle {r.Paddle ?? "none"} (\"{r.PaddleText}\")" +
                              (r.Vote is null ? $" → fallback {Score(r.Paddle, r.Truth).ToUpperInvariant()}" : ""));
    }

    // A countdown as a truth writes it, as Hms writes it: "6 day(s) 23:07:57" is "167:07:57".
    private static string Hms(string truth) => TaskTable.TimeLeft(truth) is { } t ? TaskTable.Hms(t) : truth;

    // The truth's items, each with the desk's cell for it (the desk command's Score and ScoreByRegion) and the box to read.
    private static List<Item> Truth(string path, string page, DeskLook look, SKBitmap picture, GameData data)
    {
        var grid = new List<(char Grid, int I, int J, string Name)>();
        var items = new List<Item>();
        foreach (var line in File.ReadAllLines(path).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')))
        {
            var parts = line.Split(' ', 3);
            if (parts[0].Length == 1 && char.IsAsciiLetterUpper(parts[0][0]))
            {
                var c = parts[1].Split(',');
                grid.Add((parts[0][0], int.Parse(c[0], CultureInfo.InvariantCulture), int.Parse(c[1], CultureInfo.InvariantCulture), parts[2].Trim()));
                continue;
            }
            string region = parts[0], name = parts[2].Trim();
            string? slot = parts[1].StartsWith('[') ? parts[1].Trim('[', ']') : null;
            int ti = 0, tj = 0;
            if (slot is null)
            {
                var c = parts[1].Split(',');
                (ti, tj) = (int.Parse(c[0], CultureInfo.InvariantCulture), int.Parse(c[1], CultureInfo.InvariantCulture));
            }
            var r = look.Page?.Region(region);
            var found = r?.Cells.FirstOrDefault(c => slot is not null ? c.Slot == slot : !c.IsSlot && c.Rect.Contains(new DeskRect(ti, tj, 1, 1)));
            var cellLook = found is null ? null : look.Cells.FirstOrDefault(l => ReferenceEquals(l.Cell, found)) ?? look.Cells.FirstOrDefault(l => l.Region == region && l.Cell == found);
            SKRectI? box = cellLook?.Pixels;
            if (box is null && slot is not null)
            {
                if (look.Layout.Slots.FirstOrDefault(s => s.Key == slot) is { } s)
                    box = s.Box;
                else if (look.Layout.Regions.FirstOrDefault(p => p.SlotBox is not null && DeskRegion.KeyOf(p.Kind) == slot) is { } p)
                    box = p.SlotBox;
            }
            var where = slot is not null ? $"{region} [{slot}]" : $"{region} {ti},{tj}";
            items.Add(new Item(page, where, name, cellLook, box, slot is not null, slot, found?.W ?? 1, found?.H ?? 1, r?.Kind == DeskRegionKind.SpecialSlots));
        }
        if (grid.Count > 0)
        {
            var gray = StashGray.Of(picture);
            var lines = new StashLines(gray);
            var grids = StashGrids.Find(lines, gray, picture.Width, picture.Height, look.Pitch).Where(g => g.Place != StashGridPlace.Message).ToList();
            foreach (var group in grid.GroupBy(t => t.Grid))
            {
                var index = group.Key - 'A';
                if (index < 0 || index >= grids.Count)
                {
                    items.AddRange(group.Select(t => new Item(page, $"{t.Grid} {t.I},{t.J}", t.Name, null, null, false, null, 1, 1, false)));
                    continue;
                }
                var g = grids[index];
                DeskCellLook? At(int i, int j)
                {
                    int x = g.X0 + i * look.Pitch + look.Pitch / 2, y = g.Y0 + j * look.Pitch + look.Pitch / 2;
                    return look.Cells.FirstOrDefault(c => !c.Cell.IsSlot && c.Pixels.Contains(x, y));
                }
                var offset = Enumerable.Range(0, Math.Max(1, g.Rows))
                    .Select(d => (d, Hits: group.Count(t => At(t.I, t.J + d) is { } c && c.Cell.Ids.Any(id => data.ItemShortName(id) == t.Name))))
                    .OrderByDescending(x => x.Hits).ThenBy(x => x.d).First().d;
                foreach (var t in group)
                {
                    var c = At(t.I, t.J + offset);
                    items.Add(new Item(page, $"{t.Grid} {t.I},{t.J}", t.Name, c, c?.Pixels, false, null, c?.Cell.W ?? 1, c?.Cell.H ?? 1,
                        c is not null && look.Page?.Region(c.Region)?.Kind == DeskRegionKind.SpecialSlots));
                }
            }
        }
        return items;
    }

    private static DeskSlotKind KindOf(Item it) => it.Slot ? DeskSlots.KindOf(it.SlotKey) : it.SpecialKind ? DeskSlotKind.Special : DeskSlotKind.Any;

    private static bool Confirm(string label) => ExitList.Fold(label).Length <= 3;

    private static bool Under(SKRectI box, IReadOnlyList<SKRectI> over) =>
        over.Any(o => SKRectI.Intersect(o, box) is { IsEmpty: false } i && (long)i.Width * i.Height * 4 > (long)box.Width * box.Height);

    // The desk's own naming of the item's cell.
    private static Said Desk(Item it, GameData data)
    {
        if (it.Look is null)
            return new Said(Outcome.NoCell, "", "", 0);
        return new Said(Score(it.Look.Cell.Ids, it.Truth, data), it.Look.Label ?? "", string.Join(", ", it.Look.Cell.Ids.Select(data.ItemShortName).Distinct().Take(4)), 0);
    }

    // Paddle on the cell's label band, matched as the desk matches: the slot's kind, the cell's width, its footprint.
    private static Said Paddle(OcrSpike.PaddleRec rec, SKBitmap picture, SKRectI box, int pitch, Item it, DeskCatalogue catalogue, GameData data)
    {
        var label = DeskReader.LabelRect(picture, box, pitch, it.Slot ? 6 : 0);
        var band = SKRectI.Create(label.Left + 2, label.Top + 3, Math.Max(4, label.Width - 4), (int)Math.Round(0.2 * pitch));
        band = SKRectI.Intersect(band, SKRectI.Create(0, 0, picture.Width, picture.Height));
        var sw = Stopwatch.StartNew();
        string text;
        using (var image = Crop(picture, band, 4))
            text = rec.Read(image);
        var ms = sw.Elapsed.TotalMilliseconds;
        var cells = it.Slot ? (int)Math.Round(box.Width / (double)pitch) : it.W;
        var (fit, best) = catalogue.Match.Match(text, cells, KindOf(it));
        if (fit < DeskMatch.Enough || best.Count == 0)
            return new Said(Outcome.Unread, text, "", ms);
        var named = !it.Slot && !it.SpecialKind ? Sized(best, it.W, it.H) : best;
        if (named.Count == 0)
            return new Said(Outcome.Unread, text, "footprint contradicts " + string.Join(", ", best.Select(b => b.ShortName).Distinct().Take(3)), ms);
        var ids = named.Select(c => c.ItemId).Distinct(StringComparer.Ordinal).ToList();
        return new Said(it.Truth.Length == 0 ? Outcome.Wrong : Score(ids, it.Truth, data), text, string.Join(", ", ids.Select(data.ItemShortName).Distinct().Take(4)), ms);
    }

    // DeskReader's Sized: the candidates of the cell's footprint either way round, else the guns it can hold.
    private static List<DeskCandidate> Sized(IReadOnlyList<DeskCandidate> candidates, int w, int h)
    {
        var exact = candidates.Where(c => (c.W == w && c.H == h) || (c.W == h && c.H == w)).ToList();
        return exact.Count > 0 ? exact
            : candidates.Where(c => c.Holds == DeskHolds.Mods && ((c.W <= w && c.H <= h) || (c.W <= h && c.H <= w))).ToList();
    }

    private static Outcome Score(IReadOnlyList<string> ids, string truth, GameData data)
    {
        var names = ids.Select(data.ItemShortName).ToList();
        if (names.Count == 0)
            return Outcome.Unread;
        if (ids.Distinct(StringComparer.Ordinal).Count() == 1)
            return names[0] == truth ? Outcome.Right : Outcome.Wrong;
        return names.Contains(truth) ? Outcome.OneOf : Outcome.NotAmong;
    }

    private static SKBitmap Crop(SKBitmap picture, SKRectI rect, int margin)
    {
        using var block = StashPicture.Block(picture, rect, rect.Width, rect.Height, StashPicture.Sampling);
        return StashPicture.Framed(block, margin, new SKColor(24, 29, 33));
    }
#endif

    private sealed class NoRequests : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("ocr-compare: cache only"));
    }
}
