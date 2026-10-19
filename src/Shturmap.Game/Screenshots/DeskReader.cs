using System.Diagnostics;
using Shturmap.Core.Desk;
using SkiaSharp;

namespace Shturmap.Game.Screenshots;

/// <summary>The catalogue the stash desk names cells from: every item with a short name, matched once per data load.</summary>
public sealed class DeskCatalogue(IEnumerable<DeskCandidate> candidates)
{
    internal DeskMatch Match { get; } = new(candidates);

    public int Count => Match.Count;
}

/// <summary>What one screenshot showed for the stash desk: the screen, and the page of its regions (null when the screen
/// is none of the desk's, or the trading screen); how long the reading took.</summary>
public sealed record DeskReading(DeskScreen Screen, DeskPage? Page, long Ms);

/// <summary>One cell as the reading saw it, for measuring (shturmap-cli desk): its region, where it stands in pixels, the
/// cell made of it, the label read, how well it fit, and whether the footprint contradicted the label.</summary>
internal sealed record DeskCellLook(string Region, SKRectI Pixels, DeskCell Cell, string? Label, double Fit, bool Contradicted, string? CountText);

/// <summary>Everything one look saw: the layout, the cells, the page, the odd shapes and the cells left covered.</summary>
internal sealed record DeskLook(int Width, int Height, int Pitch, DeskLayoutFound Layout, IReadOnlyList<DeskCellLook> Cells, DeskPage? Page,
    int Odd, long LayoutMs, long TotalMs);

/// <summary>
/// The stash desk's reading of a menu screenshot (owner, 2026-10-19; docs/DESIGN.md §2, "For the stash desk"): every grid
/// of the player's own stash or GEAR tab, against the whole catalogue. The screen and its regions come from the headings,
/// read once over the whole picture, and the compartments' frames (<see cref="DeskLayout"/>); each cell's label is read
/// enlarged as the stash reader reads it (<see cref="StashReader"/>, Windows' own recognition, on the PC) and named only
/// when it fits a short name at 0.9 (<see cref="DeskMatch"/>); a label that fits several items alike is "one of N", the
/// item file's footprint leaving those of the cell's size (either way round); a slot has no footprint. Counts, a printed
/// condition ("46/50") and the found-in-raid mark are read as the stash reader reads them. What a window or a toast covers
/// isn't read. Nothing of the picture is kept: the page holds item ids.
/// </summary>
public sealed class DeskReader
{
    private const int LeastWidth = 1280;
    private const int LeastHeight = 720;

    private readonly StashReader _labels;

    public DeskReader(StashReader labels) => _labels = labels;

    /// <summary>A reader in the game's language (as the stash reader's), or null when Windows has no recognition.</summary>
    public static DeskReader? Create(string? gameLanguage) => StashReader.Create(gameLanguage) is { } labels ? new DeskReader(labels) : null;

    public string LanguageTag => _labels.LanguageTag;

    /// <summary>What a decoded picture shows for the desk, its page taken at <paramref name="at"/> (the screenshot's time).</summary>
    public async Task<DeskReading> ReadAsync(SKBitmap picture, DeskCatalogue catalogue, DateTimeOffset at, CancellationToken ct = default)
    {
        var look = await LookAsync(picture, catalogue, at, ct);
        return new DeskReading(look.Layout.Screen, look.Page, look.TotalMs);
    }

    internal async Task<DeskLook> LookAsync(SKBitmap bitmap, DeskCatalogue catalogue, DateTimeOffset at, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        int w = bitmap.Width, h = bitmap.Height;
        var scale = Math.Min(w / 1920.0, h / 1080.0);
        var pitch = (int)Math.Round(StashTemplate.BaseCell * scale);
        var none = new DeskLayoutFound(DeskScreen.None, [], [], null, [], 0, []);
        if (w < LeastWidth || h < LeastHeight)
            return new DeskLook(w, h, pitch, none, [], null, 0, 0, sw.ElapsedMilliseconds);
        var gray = StashGray.Of(bitmap);
        var lines = new StashLines(gray);
        var words = await DeskWords.ReadAsync(_labels.Engine, bitmap, ct);
        var layout = DeskLayout.Find(words, lines, gray, w, h, pitch);
        var layoutMs = sw.ElapsedMilliseconds;
        if (layout.Screen is DeskScreen.None or DeskScreen.Trading)
            return new DeskLook(w, h, pitch, layout, [], null, 0, layoutMs, sw.ElapsedMilliseconds);

        var highPassed = gray.HighPass((int)Math.Round(4 * scale));
        var looks = new List<DeskCellLook>();
        var regions = new List<DeskRegion>();
        var odd = 0;

        // The loadout's slots.
        if (layout.Slots.Count > 0)
        {
            var cells = new List<DeskCell>();
            var covered = new List<string>();
            foreach (var slot in layout.Slots)
            {
                if (Under(slot.Box, layout.Over))
                {
                    covered.Add(slot.Key);
                    continue;
                }
                var order = DeskSlots.Order(slot.Key);
                if (await SlotAsync(bitmap, slot.Box, pitch, catalogue, ct) is not { } read)
                    continue;
                var cell = read.Cell with { I = order, J = 0, Slot = slot.Key, Holds = DeskSlots.IsWeapon(slot.Key) ? DeskHolds.Mods : read.Cell.Holds };
                cells.Add(cell);
                looks.Add(read with { Region = "loadout", Cell = cell });
            }
            regions.Add(new DeskRegion("loadout", DeskRegionKind.Loadout, DeskSlots.Loadout.Count, 1, cells.OrderBy(c => c.I).ToList()) { CoveredSlots = covered });
        }

        // The middle column's regions, the windows.
        foreach (var place in layout.Regions)
        {
            var key = place.Kind == DeskRegionKind.ContainerWindow ? DeskRegion.WindowKey(place.Title ?? "", regions.Count(r => r.Kind == DeskRegionKind.ContainerWindow && r.Title == place.Title))
                : DeskRegion.KeyOf(place.Kind);
            var cells = new List<DeskCell>();
            var coveredSlots = new List<string>();
            if (place.SlotBox is { } box)
            {
                var slotKey = DeskRegion.KeyOf(place.Kind);
                if (Under(box, layout.Over))
                    coveredSlots.Add(slotKey);
                else if (await SlotAsync(bitmap, box, pitch, catalogue, ct) is { } read)
                {
                    var cell = read.Cell with { I = 0, J = 0, Slot = slotKey };
                    cells.Add(cell);
                    looks.Add(read with { Region = key, Cell = cell });
                }
            }
            var over = place.Area is { } own ? layout.Over.Where(o => o != own).ToList() : layout.Over.ToList();
            var (grid, parts, covered, gridOdd) = await GridAsync(bitmap, key, place.Parts, over, lines, highPassed, pitch, catalogue, looks, ct);
            odd += gridOdd;
            cells.AddRange(grid);
            var cols = parts.Count > 0 ? parts.Max(p => p.I + p.W) : 0;
            var rows = parts.Count > 0 ? parts.Max(p => p.J + p.H) : 0;
            var region = new DeskRegion(key, place.Kind, cols, rows, cells) { Parts = parts, Covered = covered, CoveredSlots = coveredSlots };
            if (place.Kind == DeskRegionKind.ContainerWindow)
                region = region with { Title = place.Title, TitleIds = catalogue.Match.Titled(place.Title ?? "").Select(c => c.ItemId).ToList() };
            // A container's slot whose grid wasn't found (under a window, out of view): its grid's cells stay as they were.
            if (place.SlotBox is not null && place.Parts.Count == 0)
                region = region with { Covered = [new DeskRect(0, 0, 100, 100)] };
            regions.Add(region);
        }

        // The stash: the page in view, less what a window or a toast covers.
        if (layout.Stash is { } stash)
        {
            var hidden = StashGrids.Hidden(stash, [], pitch);
            for (var j = 0; j < stash.Rows; j++)
            {
                for (var i = 0; i < stash.Cols; i++)
                {
                    int cx = stash.X0 + i * pitch + pitch / 2, cy = stash.Y0 + j * pitch + pitch / 2;
                    hidden[j * stash.Cols + i] = layout.Over.Any(o => o.Contains(cx, cy));
                }
            }
            var (grid, covered, gridOdd) = await CellsAsync(bitmap, "stash", stash, hidden, 0, 0, lines, highPassed, pitch, catalogue, looks, ct);
            odd += gridOdd;
            regions.Add(new DeskRegion("stash", DeskRegionKind.Stash, stash.Cols, stash.Rows, grid) { Covered = covered });
        }

        var page = new DeskPage(at, regions);
        return new DeskLook(w, h, pitch, layout, looks, page, odd, layoutMs, sw.ElapsedMilliseconds);
    }

    // Whether a box lies under a window or a toast by more than a quarter.
    private static bool Under(SKRectI box, IReadOnlyList<SKRectI> over) =>
        over.Any(o => SKRectI.Intersect(o, box) is { IsEmpty: false } i && (long)i.Width * i.Height * 4 > (long)box.Width * box.Height);

    // A slot: its item's label at the box's top right, its condition at the bottom right; no footprint. Null for an empty
    // slot (no label read).
    private async Task<DeskCellLook?> SlotAsync(SKBitmap bitmap, SKRectI box, int pitch, DeskCatalogue catalogue, CancellationToken ct)
    {
        var (label, fit, best) = await _labels.ReadLabelAsync(bitmap, box, pitch, text => catalogue.Match.Match(text, (int)Math.Round(box.Width / (double)pitch)), DeskMatch.Enough, ct);
        if (label is null || (fit < DeskMatch.Enough && label.Count(char.IsLetterOrDigit) < 3))
            return null;
        var ids = fit >= DeskMatch.Enough && best is not null ? best : [];
        var (countText, print) = ids.Count > 0 ? await CountAsync(bitmap, box, pitch, ct) : (null, null);
        var cell = new DeskCell(0, 0, 1, 1, ids.Select(c => c.ItemId).Distinct(StringComparer.Ordinal).ToList())
        {
            Print = print,
            Holds = HoldsOf(ids),
        };
        return new DeskCellLook("", box, cell, label, fit, false, countText);
    }

    // A region's compartments: each a grid of its own, placed side by side in the region's units (gaps closed), its cells
    // read; what lies under a window or a toast is covered.
    private async Task<(List<DeskCell> Cells, List<DeskRect> Parts, List<DeskRect> Covered, int Odd)> GridAsync(SKBitmap bitmap, string region,
        IReadOnlyList<SKRectI> compartments, IReadOnlyList<SKRectI> over, StashLines lines, StashGray highPassed, int pitch, DeskCatalogue catalogue,
        List<DeskCellLook> looks, CancellationToken ct)
    {
        var cells = new List<DeskCell>();
        var parts = new List<DeskRect>();
        var covered = new List<DeskRect>();
        var odd = 0;
        if (compartments.Count == 0)
            return (cells, parts, covered, odd);
        var xs = Units(compartments.Select(c => (c.Left, (int)Math.Round(c.Width / (double)pitch))), pitch);
        var ys = Units(compartments.Select(c => (c.Top, (int)Math.Round(c.Height / (double)pitch))), pitch);
        foreach (var c in compartments)
        {
            int cols = Math.Max(1, (int)Math.Round(c.Width / (double)pitch)), rows = Math.Max(1, (int)Math.Round(c.Height / (double)pitch));
            var grid = new StashGrid(c.Left, c.Top, cols, rows, StashGridPlace.Window);
            int i0 = xs[c.Left], j0 = ys[c.Top];
            parts.Add(new DeskRect(i0, j0, cols, rows));
            var hidden = new bool[grid.Area];
            for (var j = 0; j < rows; j++)
            {
                for (var i = 0; i < cols; i++)
                {
                    int cx = c.Left + i * pitch + pitch / 2, cy = c.Top + j * pitch + pitch / 2;
                    hidden[j * cols + i] = over.Any(o => o.Contains(cx, cy));
                }
            }
            var (read, hiddenRects, gridOdd) = await CellsAsync(bitmap, region, grid, hidden, i0, j0, lines, highPassed, pitch, catalogue, looks, ct);
            cells.AddRange(read);
            covered.AddRange(hiddenRects);
            odd += gridOdd;
        }
        return (cells, parts, covered, odd);
    }

    // The compartments' places in units: each distinct left edge (or top) a whole number of cells after the one before
    // it, so the few pixels between compartments never add up to a cell.
    private static Dictionary<int, int> Units(IEnumerable<(int At, int Size)> spans, int pitch)
    {
        var edges = spans.Select(s => s.At).Distinct().Order().ToList();
        var units = new Dictionary<int, int>();
        var previous = int.MinValue;
        var unit = 0;
        foreach (var edge in edges)
        {
            if (previous != int.MinValue)
                unit += edge - previous <= 4 ? 0 : Math.Max(1, (int)Math.Round((edge - previous) / (double)pitch));
            units[edge] = unit;
            if (edge - previous > 4 || previous == int.MinValue)
                previous = edge;
        }
        return units;
    }

    // A grid's item rectangles, each read: named, "one of N", or nothing said. A rectangle next to a hidden cell may run
    // under the window: covered, as the hidden cells are.
    private async Task<(List<DeskCell> Cells, List<DeskRect> Covered, int Odd)> CellsAsync(SKBitmap bitmap, string region, StashGrid grid, bool[] hidden,
        int i0, int j0, StashLines lines, StashGray highPassed, int pitch, DeskCatalogue catalogue, List<DeskCellLook> looks, CancellationToken ct)
    {
        var cells = new List<DeskCell>();
        var covered = new List<DeskRect>();
        var odd = 0;
        var empty = StashGrids.Empty(grid, highPassed, pitch);
        for (var k = 0; k < grid.Area; k++)
        {
            if (hidden[k])
                covered.Add(new DeskRect(i0 + k % grid.Cols, j0 + k / grid.Cols, 1, 1));
        }
        SKRectI Pixels(int i, int j, int w, int h) => SKRectI.Create(grid.X0 + i * pitch, grid.Y0 + j * pitch, w * pitch, h * pitch);

        // First every rectangle's label, then the cells: a label may claim its neighbours (below). An odd shape (dark art
        // read as empty cells, a hover highlight) is read over its bounding box.
        var rects = new List<(StashRect Rect, string? Label, double Fit, List<DeskCandidate> Fitting)>();
        foreach (var rect in StashGrids.Items(grid, lines, hidden, empty, pitch))
        {
            ct.ThrowIfCancellationRequested();
            if (Touches(grid, rect, hidden))
            {
                covered.Add(new DeskRect(i0 + rect.I0, j0 + rect.J0, rect.W, rect.H));
                continue;
            }
            var (label, fit, best) = await _labels.ReadLabelAsync(bitmap, Pixels(rect.I0, rect.J0, rect.W, rect.H), pitch,
                text => catalogue.Match.Match(text, rect.W), DeskMatch.Enough, ct, Confirm);
            rects.Add((rect, label, fit, fit >= DeskMatch.Enough && best is not null ? best : []));
        }

        // The item file's footprint, either way round (an item may lie turned), is the tie-break and a check. Where the
        // grid's lines split an item (its art drew a line, or dark art read as empty cells), its label still stands at its
        // top right: the cells its footprint takes from there are its own when no other label fits there.
        var areas = new Dictionary<int, (int I, int J, int W, int H)>();
        var consumed = new HashSet<int>();
        for (var n = 0; n < rects.Count; n++)
        {
            var (rect, _, _, fitting) = rects[n];
            if (consumed.Contains(n) || fitting.Count == 0 || fitting.Any(c => Fits(c, rect.W, rect.H)) || Footprint(fitting) is not { } footprint)
                continue;
            // Only ever larger than the rectangle: a footprint smaller than it means several items read as one.
            var found = new[] { footprint, (footprint.H, footprint.W) }.Distinct()
                .Where(f => f.Item1 >= rect.W && f.Item2 >= rect.H)
                .Select(f => (I: rect.I0 + rect.W - f.Item1, J: rect.J0, W: f.Item1, H: f.Item2))
                .Select(a => (Area: a, Claimed: Claims(grid, hidden, rects, consumed, n, a)))
                .Where(x => x.Claimed is not null).ToList();
            if (found.Count != 1)
                continue;
            areas[n] = found[0].Area;
            consumed.UnionWith(found[0].Claimed!);
        }
        for (var n = 0; n < rects.Count; n++)
        {
            if (consumed.Contains(n))
                continue;
            var (rect, label, fit, fitting) = rects[n];
            var at = areas.TryGetValue(n, out var area) ? area : (I: rect.I0, J: rect.J0, W: rect.W, H: rect.H);
            var sized = fitting.Where(c => Fits(c, at.W, at.H)).ToList();
            // A larger footprint the label could also have, taking cells read as empty or unread from its top right (a
            // black rig's dark column reads as empty): the reading can't tell the two, so both are among the candidates.
            if (!areas.ContainsKey(n) && sized.Count > 0 && sized.Count < fitting.Count)
            {
                var larger = fitting.Except(sized).Where(c => new[] { (c.W, c.H), (c.H, c.W) }.Any(f => f.Item1 >= rect.W && f.Item2 >= rect.H &&
                    Claims(grid, hidden, rects, consumed, n, (rect.I0 + rect.W - f.Item1, rect.J0, f.Item1, f.Item2)) is not null)).ToList();
                sized.AddRange(larger);
            }
            if (rect.Odd && sized.Count == 0)
            {
                odd++;
                continue;
            }
            var pixels = Pixels(at.I, at.J, at.W, at.H);
            var contradicted = fitting.Count > 0 && sized.Count == 0;
            var ids = sized.Select(c => c.ItemId).Distinct(StringComparer.Ordinal).ToList();
            string? countText = null;
            DeskPrint? print = null;
            var count = 1;
            bool? fir = null;
            if (ids.Count > 0)
            {
                (countText, print) = await CountAsync(bitmap, pixels, pitch, ct);
                count = print is null ? StashLabels.Count(countText) : 1;
                fir = StashMarks.Seen(bitmap, pixels, pitch);
            }
            var cell = new DeskCell(i0 + at.I, j0 + at.J, at.W, at.H, ids) { Count = count, Print = print, FoundInRaid = fir, Holds = HoldsOf(sized) };
            cells.Add(cell);
            looks.Add(new DeskCellLook(region, pixels, cell, label, fit, contradicted, countText));
        }
        return (cells.OrderBy(c => c.J).ThenBy(c => c.I).ToList(), covered, odd);
    }

    // A label of three letters or fewer ("TG" read for "TG arm.") is read once more: a longer reading that fits wins.
    private static bool Confirm(string label) => Core.Screenshots.ExitList.Fold(label).Length <= 3;

    private static bool Fits(DeskCandidate c, int w, int h) => (c.W == w && c.H == h) || (c.W == h && c.H == w);

    // The one footprint all the candidates share (either way round), or null.
    private static (int W, int H)? Footprint(IReadOnlyList<DeskCandidate> candidates)
    {
        var first = candidates[0];
        return candidates.All(c => Fits(c, first.W, first.H)) ? (first.W, first.H) : null;
    }

    // The rectangles a footprint from a label's top right claims: null when it leaves the grid, takes a hidden cell, or
    // cuts or takes a rectangle whose own label fits an item; else the others it takes (cells read as empty are taken too:
    // dark art reads so).
    private static List<int>? Claims(StashGrid grid, bool[] hidden, List<(StashRect Rect, string? Label, double Fit, List<DeskCandidate> Fitting)> rects,
        HashSet<int> consumed, int self, (int I, int J, int W, int H) area)
    {
        if (area.I < 0 || area.J < 0 || area.I + area.W > grid.Cols || area.J + area.H > grid.Rows)
            return null;
        for (var j = area.J; j < area.J + area.H; j++)
        {
            for (var i = area.I; i < area.I + area.W; i++)
            {
                if (hidden[j * grid.Cols + i])
                    return null;
            }
        }
        var claimed = new List<int>();
        for (var m = 0; m < rects.Count; m++)
        {
            if (m == self)
                continue;
            var r = rects[m].Rect;
            var meets = r.I0 < area.I + area.W && area.I < r.I0 + r.W && r.J0 < area.J + area.H && area.J < r.J0 + r.H;
            if (!meets)
                continue;
            var inside = r.I0 >= area.I && r.J0 >= area.J && r.I0 + r.W <= area.I + area.W && r.J0 + r.H <= area.J + area.H;
            if (!inside || consumed.Contains(m) || rects[m].Fitting.Count > 0)
                return null;
            claimed.Add(m);
        }
        return claimed;
    }

    // The number at the bottom right, and the condition when it is one: a "current/max" is read once more in a band as
    // wide as the cell allows (the recognition drops a long condition's first digit now and then: "469/1800" for
    // "1469/1800"), the reading with more digits taken.
    private async Task<(string? Text, DeskPrint? Print)> CountAsync(SKBitmap bitmap, SKRectI pixels, int pitch, CancellationToken ct)
    {
        var text = await _labels.ReadCountAsync(bitmap, pixels, pitch, ct);
        if (text is null || !text.Contains('/'))
            return (text, null);
        var print = PrintOf(text);
        if (pixels.Width > pitch)
        {
            var wide = await _labels.ReadCountAsync(bitmap, pixels, pitch, ct, cells: 1.5);
            if (PrintOf(wide) is { } other && (print is null || other.Current.ToString().Length > print.Value.Current.ToString().Length))
                (text, print) = (wide, other);
        }
        return (text, print);
    }

    /// <summary>A printed "current/max" as the recognition reads it: junk before the first digit and after the last dropped
    /// (the cell's line read as "\" or "|"), and a "1" after the maximum that makes it more than the current's reach (the
    /// line of a window's grid, two pixels wide, read as a digit: "400/4001", "3/31") taken off when what is left still
    /// holds the current.</summary>
    internal static DeskPrint? PrintOf(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || !text.Contains('/'))
            return null;
        var t = text.Trim().TrimEnd('\\', '|', '"', '\'', '.', ',', ' ');
        var start = t.IndexOfAny("0123456789OØSIl".ToCharArray());
        if (start > 0)
            t = t[start..];
        if (DeskPrint.Parse(t) is not { } print)
            return null;
        var max = print.Max.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (max.Length >= 2 && max[^1] == '1' && int.Parse(max[..^1], System.Globalization.CultureInfo.InvariantCulture) is var shorter && shorter >= print.Current
            && max.Length > print.Current.ToString(System.Globalization.CultureInfo.InvariantCulture).Length)
            return new DeskPrint(print.Current, shorter);
        return print;
    }

    // Whether the rectangle has a hidden cell beside one of its own: it may go on under the window.
    private static bool Touches(StashGrid grid, StashRect rect, bool[] hidden)
    {
        for (var j = rect.J0 - 1; j <= rect.J0 + rect.H; j++)
        {
            for (var i = rect.I0 - 1; i <= rect.I0 + rect.W; i++)
            {
                if (i < 0 || j < 0 || i >= grid.Cols || j >= grid.Rows)
                    continue;
                var inside = i >= rect.I0 && i < rect.I0 + rect.W && j >= rect.J0 && j < rect.J0 + rect.H;
                var side = !inside && (i >= rect.I0 && i < rect.I0 + rect.W || j >= rect.J0 && j < rect.J0 + rect.H);
                if ((inside || side) && hidden[j * grid.Cols + i])
                    return true;
            }
        }
        return false;
    }

    // What the candidates hold out of sight, when they all hold the same.
    // What a cell's candidates hold out of sight: contents when any may be a container (a "?" cell's "+" says at least, and
    // a window may attach to it), mods when all are weapons.
    private static DeskHolds HoldsOf(IReadOnlyList<DeskCandidate> candidates) =>
        candidates.Any(c => c.Holds == DeskHolds.Contents) ? DeskHolds.Contents
        : candidates.Count > 0 && candidates.All(c => c.Holds == DeskHolds.Mods) ? DeskHolds.Mods
        : DeskHolds.Nothing;
}
