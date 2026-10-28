using System.Diagnostics;
using Shturmap.Core.Desk;
using SkiaSharp;

namespace Shturmap.Game.Screenshots;

/// <summary>The catalogue the stash desk names cells from: every item with a short name, matched once per data load; and
/// the candidates' grid images a "one of N" cell's picture is compared against (<see cref="DeskIcons"/>), when given.</summary>
public sealed class DeskCatalogue(IEnumerable<DeskCandidate> candidates, DeskIcons? icons = null)
{
    internal DeskMatch Match { get; } = new(candidates);

    internal DeskIcons Icons { get; } = icons ?? DeskIcons.None;

    public int Count => Match.Count;
}

/// <summary>What one screenshot showed for the stash desk: the screen, and the page of its regions (null when the screen
/// is none of the desk's, or the trading screen); how long the reading took.</summary>
public sealed record DeskReading(DeskScreen Screen, DeskPage? Page, long Ms);

/// <summary>One cell as the reading saw it, for measuring (shturmap-cli desk): its region, where it stands in pixels, the
/// cell made of it, the label read, how well it fit, and whether the footprint contradicted the label.</summary>
internal sealed record DeskCellLook(string Region, SKRectI Pixels, DeskCell Cell, string? Label, double Fit, bool Contradicted, string? CountText)
{
    /// <summary>For a cell whose label fit several items, each one's likeness to its picture (<see cref="DeskIcons"/>).</summary>
    public IReadOnlyList<DeskIconScore> Icons { get; init; } = [];

    /// <summary>Whether the label is PaddleOCR's reading of a band Windows' recognition left unread (2026-10-26).</summary>
    public bool ByPaddle { get; init; }
}

/// <summary>Everything one look saw: the layout, the cells, the page, the odd shapes and the cells left covered.</summary>
internal sealed record DeskLook(int Width, int Height, int Pitch, DeskLayoutFound Layout, IReadOnlyList<DeskCellLook> Cells, DeskPage? Page,
    int Odd, long LayoutMs, long TotalMs);

/// <summary>
/// The stash desk's reading of a menu screenshot (owner, 2026-10-19; docs/DESIGN.md §2, "For the stash desk"): every grid
/// of the player's own stash or GEAR tab, against the whole catalogue. The screen and its regions come from the headings,
/// read once over the whole picture, and the compartments' frames (<see cref="DeskLayout"/>); each cell's label is read
/// enlarged as the stash reader reads it (<see cref="StashReader"/>, Windows' own recognition, on the PC) and named only
/// when it fits a short name at 0.9 (<see cref="DeskMatch"/>); a label that fits several items alike is "one of N", the
/// item file's footprint leaving those of the cell's size (either way round); a slot has no footprint. A cell or a slot
/// whose label no reading fit is read once more, at the end, by PaddleOCR's recognition where the reader has it (owner,
/// 2026-10-26; <see cref="PaddleReader"/>), through the same match. Counts, a printed condition ("46/50") and the
/// found-in-raid mark are read as the stash reader reads them, by Windows' recognition alone. What a window or a toast
/// covers isn't read. Nothing of the picture is kept: the page holds item ids.
/// </summary>
public sealed class DeskReader
{
    private const int LeastWidth = 1280;
    private const int LeastHeight = 720;

    private readonly StashReader _labels;
    private readonly ILineReader? _paddle;

    /// <param name="paddle">The second reader of a label band Windows' recognition left unread, or null for Windows'
    /// alone.</param>
    public DeskReader(StashReader labels, ILineReader? paddle = null)
    {
        _labels = labels;
        _paddle = paddle;
    }

    /// <summary>A reader in the game's language (as the stash reader's), or null when Windows has no recognition.</summary>
    public static DeskReader? Create(string? gameLanguage, ILineReader? paddle = null) =>
        StashReader.Create(gameLanguage) is { } labels ? new DeskReader(labels, paddle) : null;

    public string LanguageTag => _labels.LanguageTag;

    /// <summary>What a decoded picture shows for the desk, its page taken at <paramref name="at"/> (the screenshot's time): a
    /// menu screenshot's stash or GEAR tab; the raid's inventory screen is none of the desk's (<see cref="ReadRaidAsync"/>).</summary>
    public async Task<DeskReading> ReadAsync(SKBitmap picture, DeskCatalogue catalogue, DateTimeOffset at, CancellationToken ct = default)
    {
        var look = await LookAsync(picture, catalogue, at, ct, raid: false);
        return new DeskReading(look.Layout.Screen, look.Page, look.TotalMs);
    }

    /// <summary>
    /// What a raid's screenshot shows for the in-raid glance (owner, 2026-10-19; docs/DESIGN.md §2, "For the stash desk", "In
    /// a raid"; §4, *The in-raid glance*): on the raid's inventory screen, the player's own regions (ON YOU: the loadout,
    /// the rig, the pockets, the special slots, the backpack, the pouch, an open window) and the LOOT pane's (a body's slots
    /// and parts, a container's grid; <see cref="DeskLoot"/>), read as the desk reads its grids; a page of no screen (Screen
    /// None, no page) for any other picture: the world, a map. Nothing of it is kept: the caller publishes it for the glance
    /// and lets it go.
    /// </summary>
    public async Task<DeskReading> ReadRaidAsync(SKBitmap picture, DeskCatalogue catalogue, DateTimeOffset at, CancellationToken ct = default)
    {
        if (!await ShowsTabsAsync(picture, ct))
            return new DeskReading(DeskScreen.None, null, 0);
        var look = await LookAsync(picture, catalogue, at, ct, raid: true);
        return new DeskReading(look.Layout.Screen, look.Page, look.TotalMs);
    }

    /// <summary>Whether the picture shows the character screen's tabs at its top (OVERALL, GEAR, HEALTH, SKILLS): a raid's
    /// screenshots are mostly the world, read here in a strip of the picture's top (about 30 ms) before the whole picture
    /// is (about a second).</summary>
    internal async Task<bool> ShowsTabsAsync(SKBitmap picture, CancellationToken ct)
    {
        var strip = SKRectI.Create(0, 0, (int)(picture.Width * 0.75), Math.Max(1, (int)(picture.Height * 0.04)));
        using var part = new SKBitmap(strip.Width, strip.Height, picture.ColorType, picture.AlphaType);
        if (!picture.ExtractSubset(part, strip))
            return false;
        using var copy = part.Copy();
        var words = await DeskWords.ReadAsync(_labels.Engine, copy, ct);
        var tabs = new[] { "OVERALL", "GEAR", "HEALTH", "SKILLS" }.Select(Core.Screenshots.ExitList.Fold).ToHashSet(StringComparer.Ordinal);
        return words.Count(x => tabs.Contains(Core.Screenshots.ExitList.Fold(x.Text))) >= 2;
    }

    /// <param name="raid">True: the raid's inventory screen only (the in-raid glance); false: the menus' screens only (the
    /// desk); null: whichever the picture shows (the CLI).</param>
    internal async Task<DeskLook> LookAsync(SKBitmap bitmap, DeskCatalogue catalogue, DateTimeOffset at, CancellationToken ct, bool? raid = null)
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
        // The LOOT pane's head, too small at 1920×1080 for the whole picture's reading, read again enlarged (the glance's,
        // and the CLI's; never in the menus, whose bar stands at the foot).
        if (raid != false && DeskLoot.Head(words, w, h) is null && !DeskLoot.MenuBar(words, h))
            words = await DeskWords.WithLootHeadAsync(_labels.Engine, bitmap, words, pitch, ct);
        var layout = DeskLayout.Find(words, lines, gray, w, h, pitch);
        var layoutMs = sw.ElapsedMilliseconds;
        // The desk reads the menus' screens, the glance the raid's inventory (the CLI either); to the desk, the raid's is none.
        var wanted = raid switch
        {
            true => layout.Screen == DeskScreen.Raid,
            false => layout.Screen != DeskScreen.Raid,
            null => true,
        };
        if (layout.Screen is DeskScreen.None or DeskScreen.Trading || !wanted)
            return new DeskLook(w, h, pitch, raid is false && layout.Screen == DeskScreen.Raid ? layout with { Screen = DeskScreen.None } : layout, [], null, 0,
                layoutMs, sw.ElapsedMilliseconds);

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
                if (await SlotAsync(bitmap, slot.Box, pitch, catalogue, DeskSlots.KindOf(slot.Key), lines, layout.Over, ct) is not { } read)
                    continue;
                var cell = read.Cell with { I = order, J = 0, Slot = slot.Key, Holds = DeskSlots.IsWeapon(slot.Key) ? DeskHolds.Mods : read.Cell.Holds };
                cells.Add(cell);
                looks.Add(read with { Region = "loadout", Cell = cell });
            }
            // A strip of slot cells, no grid (QA pass of 2026-10-19, F2: an eleven by one grid drew empty cells under the strip).
            regions.Add(new DeskRegion("loadout", DeskRegionKind.Loadout, 0, 0, cells.OrderBy(c => c.I).ToList()) { CoveredSlots = covered });
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
                else if (await SlotAsync(bitmap, box, pitch, catalogue, DeskSlots.KindOf(slotKey), lines, layout.Over, ct) is { } read)
                {
                    var cell = read.Cell with { I = 0, J = 0, Slot = slotKey };
                    cells.Add(cell);
                    looks.Add(read with { Region = key, Cell = cell });
                }
            }
            // A window's cells are hidden by what lies over it, never by itself or by the windows it lies over.
            var over = place.Area is { } own ? layout.Over.Where(o => o != own && !place.Over.Contains(o)).ToList() : layout.Over.ToList();
            // The special slots take their own items only; a pocket's or a rig's item may be bound to a quick slot, whose
            // number covers its label's start.
            var kind = place.Kind == DeskRegionKind.SpecialSlots ? DeskSlotKind.Special : DeskSlotKind.Any;
            var quickSlots = place.Kind is DeskRegionKind.Pockets or DeskRegionKind.Rig;
            var (grid, parts, covered, gridOdd, unread) = await GridAsync(bitmap, key, place.Parts, over, lines, highPassed, pitch, catalogue, looks, kind, quickSlots, ct);
            odd += gridOdd;
            cells.AddRange(grid);
            var cols = parts.Count > 0 ? parts.Max(p => p.I + p.W) : 0;
            var rows = parts.Count > 0 ? parts.Max(p => p.J + p.H) : 0;
            var region = new DeskRegion(key, place.Kind, cols, rows, cells) { Parts = parts, Covered = covered, CoveredSlots = coveredSlots, Unread = unread };
            if (place.Kind == DeskRegionKind.ContainerWindow)
                region = region with { Title = place.Title, TitleIds = catalogue.Match.Titled(place.Title ?? "").Select(c => c.ItemId).ToList() };
            // A middle region whose grid ran past what the screenshot shows (owner, 2026-10-25): cut by the column's foot, or
            // not in view at all (under a window, scrolled out of view: the test set's 11-gear.png to
            // 13-gear-keys.png, read the backpack as its slot alone and the page lost its grid). The page keeps its cells and
            // compartments outside what was read (DeskLedger). Only a container's slot seen empty, a frame in view with no
            // label, says the grid is gone with it.
            else if (place.Kind != DeskRegionKind.Loadout)
            {
                var emptySlot = place.SlotBox is not null && !place.SlotDerived && coveredSlots.Count == 0 && !cells.Any(c => c.IsSlot);
                if (place.Cut || (place.Parts.Count == 0 && !emptySlot))
                    region = region with { Partial = true };
                // In a raid the glance reads it as far as it shows, its contents past the foot unseen: ON YOU "+" (owner,
                // 2026-10-28, as the LOOT pane's grid its scrolling cuts).
                if (place.Cut && layout.Screen == DeskScreen.Raid)
                    region = region with { Cut = true };
            }
            regions.Add(region);
        }

        // The LOOT pane of a raid's inventory screen (the in-raid glance): a body's slots and parts, or a container's grid.
        if (layout.Loot is { } loot)
            odd += await LootAsync(bitmap, layout, loot, lines, highPassed, pitch, catalogue, looks, regions, ct);

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
            var (grid, covered, gridOdd, unread) = await CellsAsync(bitmap, "stash", stash, hidden, 0, 0, lines, highPassed, pitch, catalogue, looks,
                DeskSlotKind.Any, false, layout.Over, ct, openTop: true, openBottom: true);
            odd += gridOdd;
            regions.Add(new DeskRegion("stash", DeskRegionKind.Stash, stash.Cols, stash.Rows, grid) { Covered = covered, Unread = unread });
        }

        var page = new DeskPage(at, regions);
        return new DeskLook(w, h, pitch, layout, looks, page, odd, layoutMs, sw.ElapsedMilliseconds);
    }

    // The LOOT pane's regions, each titled with the game's words ("LOOT · BODY"): a body's slots as a strip, its TACTICAL RIG
    // and BACKPACK each with its own slot and grid, its POCKETS, each with its search state (a part not searched, or being
    // searched, has contents unseen); a container's grid. A part whose slot holds nothing (the body's rig taken) is left
    // out. The odd shapes found.
    private async Task<int> LootAsync(SKBitmap bitmap, DeskLayoutFound layout, DeskLootFound loot, StashLines lines, StashGray highPassed, int pitch,
        DeskCatalogue catalogue, List<DeskCellLook> looks, List<DeskRegion> regions, CancellationToken ct)
    {
        var odd = 0;
        if (loot.Slots.Count > 0)
        {
            var cells = new List<DeskCell>();
            var covered = new List<string>();
            foreach (var slot in loot.Slots)
            {
                if (Under(slot.Box, layout.Over))
                {
                    covered.Add(slot.Key);
                    continue;
                }
                if (await SlotAsync(bitmap, slot.Box, pitch, catalogue, DeskSlots.KindOf(slot.Key), lines, layout.Over, ct) is not { } read)
                    continue;
                var cell = read.Cell with { I = DeskSlots.Order(slot.Key), J = 0, Slot = slot.Key, Holds = DeskSlots.IsWeapon(slot.Key) ? DeskHolds.Mods : read.Cell.Holds };
                cells.Add(cell);
                looks.Add(read with { Region = DeskRegion.KeyOf(DeskRegionKind.LootSlots), Cell = cell });
            }
            regions.Add(new DeskRegion(DeskRegion.KeyOf(DeskRegionKind.LootSlots), DeskRegionKind.LootSlots, 0, 0, cells.OrderBy(c => c.I).ToList())
            {
                CoveredSlots = covered,
                Title = loot.Title,
            });
        }
        foreach (var place in loot.Parts)
        {
            var key = DeskRegion.KeyOf(place.Kind);
            var cells = new List<DeskCell>();
            var coveredSlots = new List<string>();
            var slotKey = place.Kind switch
            {
                DeskRegionKind.LootRig => "rig",
                DeskRegionKind.LootBackpack => "backpack",
                _ => null,
            };
            if (place.SlotBox is { } box && slotKey is not null)
            {
                if (Under(box, layout.Over))
                    coveredSlots.Add(slotKey);
                else if (await SlotAsync(bitmap, box, pitch, catalogue, DeskSlots.KindOf(slotKey), lines, layout.Over, ct) is { } read)
                {
                    var cell = read.Cell with { I = 0, J = 0, Slot = slotKey };
                    cells.Add(cell);
                    looks.Add(read with { Region = key, Cell = cell });
                }
                // An empty slot: no container, nothing beside it.
                if (cells.Count == 0 && coveredSlots.Count == 0 && place.Parts.Count == 0 && place.Search == DeskSearch.Searched)
                    continue;
            }
            var (grid, parts, covered, gridOdd, unread) = await GridAsync(bitmap, key, place.Parts, layout.Over, lines, highPassed, pitch, catalogue, looks,
                DeskSlotKind.Any, false, ct);
            odd += gridOdd;
            cells.AddRange(grid);
            var cols = parts.Count > 0 ? parts.Max(p => p.I + p.W) : 0;
            var rows = parts.Count > 0 ? parts.Max(p => p.J + p.H) : 0;
            regions.Add(new DeskRegion(key, place.Kind, cols, rows, cells)
            {
                Parts = parts,
                Covered = covered,
                CoveredSlots = coveredSlots,
                Unread = unread,
                Search = place.Search,
                Cut = place.Cut,
                Title = loot.Title,
            });
        }
        return odd;
    }

    // Whether a box lies under a window or a toast by more than a quarter.
    private static bool Under(SKRectI box, IReadOnlyList<SKRectI> over) =>
        over.Any(o => SKRectI.Intersect(o, box) is { IsEmpty: false } i && (long)i.Width * i.Height * 4 > (long)box.Width * box.Height);

    // A slot: its item's label at the box's top right, matched against the items the slot takes; its condition at the
    // bottom right and its found-in-raid mark beside it; no footprint. Null for an empty slot (no label read).
    private async Task<DeskCellLook?> SlotAsync(SKBitmap bitmap, SKRectI box, int pitch, DeskCatalogue catalogue, DeskSlotKind kind, StashLines lines,
        IReadOnlyList<SKRectI> over, CancellationToken ct)
    {
        var rect = LabelRect(bitmap, box, pitch, reach: 6);
        var cells = (int)Math.Round(box.Width / (double)pitch);
        var (label, fit, best) = OrPreset(await _labels.ReadLabelAsync(bitmap, rect, pitch, text => catalogue.Match.Match(text, cells, kind), DeskMatch.Enough, ct),
            catalogue, kind);
        var byPaddle = false;
        // A slot whose label no reading fit, or that read none (an empty slot's box, or a label the recognition missed: the
        // backpack slots' "Trooper 35" and "ScavBP" of 2026-10-19): PaddleOCR's reading of its band (owner, 2026-10-26).
        if (fit < DeskMatch.Enough && await PaddleAsync(bitmap, rect, pitch, text => catalogue.Match.Match(text, cells, kind), catalogue, kind, false, ct) is { } paddle)
            (label, fit, best, byPaddle) = (paddle.Label, paddle.Fit, paddle.Best, true);
        if (label is null || (fit < DeskMatch.Enough && label.Count(char.IsLetterOrDigit) < 3))
            return null;
        var ids = fit >= DeskMatch.Enough && best is not null ? best : [];
        // A label several items of the slot share: the picture against their grid images (owner, 2026-10-25).
        (ids, var icons) = catalogue.Icons.Settle(bitmap, box, pitch, ids, DeskIconPlace.Fitted);
        var (countText, print) = ids.Count > 0 ? await CountAsync(bitmap, box, pitch, ct) : (null, null);
        // The found-in-raid mark at its corner (owner, 2026-10-28; a grid cell's since 2026-10-11), where the corner can be judged.
        var fir = ids.Count > 0 && MarkCornerShows(box, pitch, lines, over)
            ? StashMarks.SeenOnSlot(bitmap, box, pitch, barred: kind is DeskSlotKind.Weapon or DeskSlotKind.Knife)
            : null;
        var cell = new DeskCell(0, 0, 1, 1, ids.Select(c => c.ItemId).Distinct(StringComparer.Ordinal).ToList())
        {
            Print = print,
            FoundInRaid = fir,
            Holds = HoldsOf(ids),
        };
        return new DeskCellLook("", box, cell, label, fit, false, countText) { Icons = icons, ByPaddle = byPaddle };
    }

    /// <summary>
    /// Whether a slot's corner, where its found-in-raid mark stands, can be judged: nothing lies over it (a window, a toast,
    /// a tooltip), and the slot's frame shows along the bottom of its last cell. The pane's or the column's foot cutting the
    /// slot, the hover's light (the slot's ground lit as light as its frame) and the red low-health tint at its strongest
    /// (the ground at the frame's level) all hide that line; the mark is then left unread, never guessed.
    /// </summary>
    internal static bool MarkCornerShows(SKRectI box, int pitch, StashLines lines, IReadOnlyList<SKRectI> over)
    {
        var corner = new SKRectI(box.Right - (int)Math.Round(0.5 * pitch), box.Bottom - (int)Math.Round(0.85 * pitch), box.Right, box.Bottom);
        if (over.Any(o => o.IntersectsWith(corner)))
            return false;
        // A heading's box stands a pixel or two off the frame: the best of the rows near its bottom.
        double line = 0;
        for (var y = box.Bottom - 5; y <= box.Bottom; y++)
            line = Math.Max(line, lines.Horizontal(y, box.Right - pitch, box.Right - 3));
        return line >= 0.5;
    }

    /// <summary>
    /// A label band Windows' recognition left unread in every way it tries, read once more by PaddleOCR's recognition at the
    /// picture's own size (owner, 2026-10-26, decision 1; <see cref="PaddleReader"/>): its text through the same match and
    /// preset rule as Windows' (<paramref name="match"/>, <see cref="OrPreset"/>); null where it names nothing, reads
    /// nothing, or the reader isn't there. Asked only once a cell's place is settled, so its reading never moves a cell.
    /// </summary>
    private async Task<(string Label, double Fit, List<DeskCandidate> Best)?> PaddleAsync(SKBitmap bitmap, SKRectI rect, int pitch,
        Func<string, (double Fit, List<DeskCandidate> Best)> match, DeskCatalogue catalogue, DeskSlotKind kind, bool cutAtStart, CancellationToken ct)
    {
        if (_paddle is null || StashReader.LabelLine(bitmap, rect, pitch) is not { } line)
            return null;
        string? text;
        using (line)
            text = await _paddle.ReadAsync(line, ct);
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var (fit, best) = match(text);
        var read = OrPreset((text, fit, best), catalogue, kind, cutAtStart);
        return read.Fit >= DeskMatch.Enough && read.Best is { Count: > 0 } named ? (text, read.Fit, named) : null;
    }

    // A region's compartments: each a grid of its own, placed side by side in the region's units (gaps closed), its cells
    // read; what lies under a window or a toast is covered.
    private async Task<(List<DeskCell> Cells, List<DeskRect> Parts, List<DeskRect> Covered, int Odd, int Unread)> GridAsync(SKBitmap bitmap, string region,
        IReadOnlyList<SKRectI> compartments, IReadOnlyList<SKRectI> over, StashLines lines, StashGray highPassed, int pitch, DeskCatalogue catalogue,
        List<DeskCellLook> looks, DeskSlotKind kind, bool quickSlots, CancellationToken ct)
    {
        var cells = new List<DeskCell>();
        var parts = new List<DeskRect>();
        var covered = new List<DeskRect>();
        var odd = 0;
        var unread = 0;
        if (compartments.Count == 0)
            return (cells, parts, covered, odd, unread);
        // Two compartments of one region never overlap: one lying over another is the same one found twice, and its cells
        // would be read and counted twice (a Junk window of 2026-10-19 had its GPUs and hoses twice in the total).
        var found = compartments;
        compartments = found.Where((c, n) => !found.Take(n).Any(o => DeskLayout.Overlapping(o, c))).ToList();
        var xs = Units(compartments.Select(c => (c.Left, c.Right)), pitch);
        var ys = Units(compartments.Select(c => (c.Top, c.Bottom)), pitch);
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
            var (read, hiddenRects, gridOdd, gridUnread) = await CellsAsync(bitmap, region, grid, hidden, i0, j0, lines, highPassed, pitch, catalogue,
                looks, kind, quickSlots, over, ct);
            cells.AddRange(read);
            covered.AddRange(hiddenRects);
            odd += gridOdd;
            unread += gridUnread;
        }
        return (cells, parts, covered, odd, unread);
    }

    // The compartments' places in units: each distinct left edge (or top) a whole number of cells after the one before
    // it, so the few pixels between compartments never add up to a cell. A compartment whose edge stands inside a
    // compartment no wider than itself, away from its ends, is set between the columns (or rows) of the others: it takes
    // the unit of the column it stands in, and adds none (layout round, 2026-10-28: the AVS rig, five pockets over five
    // over two wide ones set between them, read seven wide, its second pocket a column off).
    internal static Dictionary<int, int> Units(IEnumerable<(int From, int To)> spans, int pitch)
    {
        // Edges within a quarter of a cell are one (until 2026-10-25 within 4 px: the test set's 01-gear.png
        // had a pocket's frame found 5 px above the others', and the pockets came out three rows high instead of two).
        var all = spans.Distinct().ToList();
        var same = pitch / 4;
        bool Set(int from, int to) => all.Any(s => s.To - s.From <= to - from && from > s.From + same && from < s.To - same);
        var between = all.GroupBy(s => s.From).Where(g => g.All(s => Set(s.From, s.To))).Select(g => g.Key).ToHashSet();
        bool Between(int edge) => between.Contains(edge);
        var edges = all.Select(s => s.From).Distinct().Order().ToList();
        var units = new Dictionary<int, int>();
        var previous = int.MinValue;
        var unit = 0;
        foreach (var edge in edges.Where(e => !Between(e)))
        {
            if (previous != int.MinValue)
                unit += edge - previous <= same ? 0 : Math.Max(1, (int)Math.Round((edge - previous) / (double)pitch));
            units[edge] = unit;
            if (edge - previous > same || previous == int.MinValue)
                previous = edge;
        }
        // An edge set between: the unit of the lattice edge at or before it (the column it stands in).
        foreach (var edge in edges.Where(Between))
            units[edge] = units.Where(u => u.Key <= edge).Select(u => (Key: u.Key, Unit: u.Value)).DefaultIfEmpty((Key: edge, Unit: 0)).MaxBy(u => u.Key).Unit;
        return units;
    }

    // A grid's item rectangles, each read: named, "one of N", or nothing said. A rectangle next to a hidden cell may run
    // under the window: read by the labels that show in it (since 2026-10-25), else covered, as the hidden cells are; so
    // is one in the stash's first row running on under the pane's head (openTop). The items are of the kind the grid takes
    // (the special slots');
    // where quick slots may stand (pockets, a rig), a label whose cell shows a quick slot's number may have lost its start.
    private async Task<(List<DeskCell> Cells, List<DeskRect> Covered, int Odd, int Unread)> CellsAsync(SKBitmap bitmap, string region, StashGrid grid, bool[] hidden,
        int i0, int j0, StashLines lines, StashGray highPassed, int pitch, DeskCatalogue catalogue, List<DeskCellLook> looks, DeskSlotKind kind,
        bool quickSlots, IReadOnlyList<SKRectI> over, CancellationToken ct, bool openTop = false, bool openBottom = false)
    {
        // Whether a part of the picture lies in part under a window, a toast or a tooltip.
        bool UnderAny(SKRectI r) => over.Any(o => o.IntersectsWith(r));
        var cells = new List<DeskCell>();
        var covered = new List<DeskRect>();
        var odd = 0;
        var unread = 0;
        var empty = StashGrids.Empty(grid, highPassed, pitch);
        // A cell whose band shows a label is an item's top right, whatever its art (owner's second test, 2026-10-25: the
        // Beta 2 backpack's top right cell, plain purple under its label "Beta 2", read as empty).
        for (var k = 0; k < grid.Area; k++)
        {
            if (empty[k] && !hidden[k] && LabelInk(bitmap, SKRectI.Create(grid.X0 + k % grid.Cols * pitch, grid.Y0 + k / grid.Cols * pitch, pitch, pitch), pitch))
                empty[k] = false;
        }
        for (var k = 0; k < grid.Area; k++)
        {
            if (hidden[k])
                covered.Add(new DeskRect(i0 + k % grid.Cols, j0 + k / grid.Cols, 1, 1));
        }
        SKRectI Pixels(int i, int j, int w, int h) => SKRectI.Create(grid.X0 + i * pitch, grid.Y0 + j * pitch, w * pitch, h * pitch);
        // A cell's label band, the line right of it with it.
        SKRectI Band(int i, int j) => SKRectI.Create(grid.X0 + i * pitch, grid.Y0 + j * pitch, pitch + 2, (int)Math.Round(0.2 * pitch));

        // A special slot holds its item in one cell whatever its footprint (the Surv12, three by one): no footprint there.
        var footprints = kind != DeskSlotKind.Special;

        // First every rectangle's label, then the cells: a label may claim its neighbours (below). An odd shape (dark art
        // read as empty cells, a hover highlight) is read over its bounding box.
        var rects = new List<(StashRect Rect, string? Label, double Fit, List<DeskCandidate> Fitting)>();
        var labels = new Dictionary<(int, int, int, int), (string? Label, double Fit, List<DeskCandidate>? Best)>();
        async Task<(string? Label, double Fit, List<DeskCandidate>? Best)> LabelOf(StashRect r)
        {
            if (labels.TryGetValue((r.I0, r.J0, r.W, r.H), out var known))
                return known;
            var at = Pixels(r.I0, r.J0, r.W, r.H);
            var cutAtStart = quickSlots && QuickSlotNumber(bitmap, at, pitch);
            var read = OrPreset(await _labels.ReadLabelAsync(bitmap, LabelRect(bitmap, at, pitch), pitch, text => catalogue.Match.Match(text, r.W, kind, cutAtStart),
                DeskMatch.Enough, ct, Confirm), catalogue, kind, cutAtStart);
            labels[(r.I0, r.J0, r.W, r.H)] = read;
            return read;
        }
        List<DeskCandidate> Named((string? Label, double Fit, List<DeskCandidate>? Best) read) =>
            read.Fit >= DeskMatch.Enough && read.Best is not null ? read.Best : [];
        IReadOnlyList<(int I, int J)> CellsOf(StashRect r) => r.Cells.Count > 0 ? r.Cells
            : Enumerable.Range(r.J0, r.H).SelectMany(j => Enumerable.Range(r.I0, r.W).Select(i => (i, j))).ToList();
        // The item under the pointer: its lit ground reads as an empty cell's flat field, and is one item (layout round,
        // 2026-10-28).
        var lit = StashGrids.Lit(grid, bitmap.GetPixelSpan(), bitmap.Width, bitmap.Height, pitch);
        for (var k = 0; k < grid.Area; k++)
        {
            if (lit[k] && !hidden[k])
                empty[k] = false;
        }
        var itemRects = StashGrids.Items(grid, lines, hidden, empty, pitch, lit);
        // The cells read as empty that no rectangle took: dark art an item's footprint may take (as Claims does).
        var occupied = itemRects.SelectMany(CellsOf).ToHashSet();
        bool Free(int i, int j) => empty[j * grid.Cols + i] && !occupied.Contains((i, j));
        // A shape read by the labels in its cells (owner's second test, 2026-10-25): each cell whose band shows a label, right
        // to left and top row first (a label stands at its item's top right), names its item, which takes its footprint from
        // there over the shape's cells, the cells read as empty no rectangle took (dark art) and the cells a window or a
        // tooltip hides; a cell whose label's band a window covers in part is never read so (its label may run on under the
        // window). The items found, and the cells no label took.
        async Task<(List<(StashRect Rect, string? Label, double Fit, List<DeskCandidate> Fitting)> Items, List<(int I, int J)> Left)> ByLabelsAsync(
            IReadOnlyList<(int I, int J)> shape)
        {
            var inShape = shape.ToHashSet();
            var claimed = new HashSet<(int, int)>();
            var items = new List<(StashRect, string?, double, List<DeskCandidate>)>();
            bool Hidden(int i, int j) => hidden[j * grid.Cols + i];
            foreach (var (ci, cj) in shape.OrderBy(c => c.J).ThenByDescending(c => c.I))
            {
                var one = Pixels(ci, cj, 1, 1);
                // Its label's band must show whole: a window over it may hide the label's end, or the label is another's.
                if (claimed.Contains((ci, cj)) || UnderAny(Band(ci, cj)))
                    continue;
                if (!LabelInk(bitmap, one, pitch))
                    continue;
                var cut = quickSlots && QuickSlotNumber(bitmap, one, pitch);
                var (l1, f1, b1) = OrPreset(await _labels.ReadLabelAsync(bitmap, LabelRect(bitmap, one, pitch), pitch, text => catalogue.Match.Match(text, 1, kind, cut),
                    DeskMatch.Enough, ct, Confirm), catalogue, kind, cut);
                if (f1 < DeskMatch.Enough || b1 is not { Count: > 0 })
                    continue;
                // The largest footprint the label's items take from here (the shape's cells joined: one item rather than
                // a smaller one and cells of nothing), its items those of that footprint.
                var shapes = (footprints ? b1.SelectMany(c => new[] { (W: c.W, H: c.H), (W: c.H, H: c.W) }) : [(W: 1, H: 1)]).Distinct()
                    .Select(f => (I: ci - f.W + 1, J: cj, f.W, f.H))
                    .Where(a => a.I >= 0 && a.I + a.W <= grid.Cols && a.J + a.H <= grid.Rows &&
                                Enumerable.Range(a.I, a.W).SelectMany(i => Enumerable.Range(a.J, a.H).Select(j => (i, j)))
                                    .All(c => !claimed.Contains(c) && (inShape.Contains(c) || Hidden(c.i, c.j) || Free(c.i, c.j))))
                    .OrderByDescending(a => a.W * a.H).ToList();
                if (shapes.Count == 0)
                    continue;
                var area = shapes[0];
                var named = footprints ? b1.Where(c => Fits(c, area.W, area.H)).ToList() : b1;
                items.Add((new StashRect(area.I, area.J, area.W, area.H, false), l1, f1, named));
                for (var i = area.I; i < area.I + area.W; i++)
                {
                    for (var j = area.J; j < area.J + area.H; j++)
                    {
                        claimed.Add((i, j));
                        occupied.Add((i, j));
                    }
                }
            }
            return (items, shape.Where(c => !claimed.Contains(c)).ToList());
        }
        var queue = new Queue<StashRect>(itemRects);
        var touching = new List<(int I, int J)>();
        while (queue.Count > 0)
        {
            var rect = queue.Dequeue();
            ct.ThrowIfCancellationRequested();
            // In the stash's first row, an item with no line along its top runs on above it, under the stash pane's head
            // (owner's second test, 2026-10-25, picture 17: an armour plate and the Blueprints, two by two, their top rows
            // and labels under the head, read as a "?" two by one each): what lies under the head isn't read, as under a
            // window.
            var runsAbove = openTop && rect.J0 == 0 && Pixels(rect.I0, 0, rect.W, 1) is var top &&
                            lines.Horizontal(top.Top, top.Left + 4, top.Right - 4) < 0.4;
            if (runsAbove || Touches(grid, rect, hidden))
            {
                // It may run on under the window: read with the others that do, below.
                touching.AddRange(CellsOf(rect));
                continue;
            }
            var (label, fit, best) = await LabelOf(rect);
            if (!rect.Odd && rect.W * rect.H > 1 && footprints && Named((label, fit, best)) is { Count: > 0 } named0 && Sized(named0, rect.W, rect.H).Count == 0 &&
                named0.All(c => (c.W <= rect.W && c.H <= rect.H) || (c.H <= rect.W && c.W <= rect.H)))
            {
                // Its label names items smaller than it only: several items read as one, the line between them unseen (a
                // highlight's frame; the owner's second test of 2026-10-25, picture 16, a Bloodset over a Bandage in the CR
                // MK2 (R) window). It is read by the labels in its cells; what no label takes stays a shape of nothing said.
                var (items, left) = await ByLabelsAsync(CellsOf(rect));
                if (items.Count > 0)
                {
                    rects.AddRange(items);
                    if (left.Count > 0)
                        rects.Add((new StashRect(rect.I0, rect.J0, rect.W, rect.H, true) { Cells = left }, label, fit, []));
                    continue;
                }
            }
            if (rect.Pieces.Count > 0 && footprints && Sized(Named((label, fit, best)), rect.W, rect.H).Count == 0)
            {
                // Joined across faint lines, and its label names no item of its size: two items whose frame between them
                // stood out faintly (a keys case over a vest, 2026-10-19, 20:22) are read as the lines that show give them
                // when one of those pieces names an item of its own size; else the rectangle stands.
                var named = false;
                foreach (var piece in rect.Pieces.Where(p => !p.Odd))
                    named |= Sized(Named(await LabelOf(piece)), piece.W, piece.H).Count > 0;
                if (named)
                {
                    foreach (var piece in rect.Pieces)
                        queue.Enqueue(piece);
                    continue;
                }
            }
            // (An odd shape is no item, whatever its bounding box's label fits, unless that names an item of the box's size: a
            // gun whose dark art reads as empty cells.)
            if (rect.Odd && rect.Cells.Count is > 1 and <= OddCellsRead &&
                (footprints ? Sized(Named((label, fit, best)), rect.W, rect.H).Count == 0 : fit < DeskMatch.Enough || best is null))
            {
                // An odd shape whose label fits nothing: the lines between its items didn't show (a hover highlight, light
                // art; the Keys window of 2026-10-19, 20:22, RB-VO, RB-AK and RB-PP with Dorm 103 and Container in one
                // cross; the owner's second test of 2026-10-25, picture 14, the Water, the CR MK2 (R) and the GPU round the
                // rig the pointer lit). It is read by the labels in its cells, each item taking its footprint from its
                // label's cell (until 2026-10-25 items of one cell only).
                var (items, left) = await ByLabelsAsync(rect.Cells);
                rects.AddRange(items);
                // What is left of it stays an odd shape: nothing said there.
                if (left.Count > 0)
                    rects.Add((rect with { Cells = left }, label, fit, []));
                continue;
            }
            rects.Add((rect, label, fit, fit >= DeskMatch.Enough && best is not null ? best : []));
        }
        // A cell a window hides whose label's band shows above the window (the owner's second test of 2026-10-25, picture
        // 15: the Beta 2 window's CR MK2 (R), its label over the CR MK2 (R) window's top edge) is read with them.
        if (touching.Count > 0 || hidden.Any(x => x))
        {
            touching.AddRange(Enumerable.Range(0, grid.Area).Where(k => hidden[k] && !UnderAny(Band(k % grid.Cols, k / grid.Cols)))
                .Select(k => (k % grid.Cols, k / grid.Cols)));
        }
        if (touching.Count > 0)
        {
            // What may run on under a window or a tooltip, all together (an item's cells may stand in several of its pieces):
            // read by the labels that show in it, an item taking the hidden cells its footprint needs (owner's second
            // test, 2026-10-25: the Beta 2 backpack in the backpack's grid, its top row over the Beta 2 window, its label
            // "Beta 2" at its top right; the CR MK2 (R) under the game's tooltip); the rest is covered, as before.
            var (under, rest) = await ByLabelsAsync(touching);
            rects.AddRange(under);
            covered.AddRange(rest.Select(c => new DeskRect(i0 + c.I, j0 + c.J, 1, 1)));
        }

        // The item file's footprint, either way round (an item may lie turned), is the tie-break and a check. Where the
        // grid's lines split an item (its art drew a line, or dark art read as empty cells), its label still stands at its
        // top right: the cells its footprint takes from there are its own when no other label fits there.
        var areas = new Dictionary<int, (int I, int J, int W, int H)>();
        var consumed = new HashSet<int>();
        // In the stash's last row, an item with no line along its bottom runs on below the view (layout round, 2026-10-28:
        // the owner's picture of 2026-10-27, a tomahawk one by three and a helmet two by two in the last rows, their feet
        // under the grid's end, said nothing for their footprints): a footprint as wide and taller is its, never one turned
        // over the cells beside it, and its count and mark are out of view.
        bool RunsBelow(int i, int j, int w, int h) => openBottom && j + h == grid.Rows &&
            lines.Horizontal(grid.Y0 + grid.Rows * pitch, grid.X0 + i * pitch + 4, grid.X0 + (i + w) * pitch - 4) < 0.4;
        for (var n = 0; n < rects.Count; n++)
        {
            var (rect, _, _, fitting) = rects[n];
            if (!footprints || consumed.Contains(n) || fitting.Count == 0 || Sized(fitting, rect.W, rect.H).Count > 0 || Footprint(fitting) is not { } footprint ||
                RunsBelow(rect.I0, rect.J0, rect.W, rect.H))
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
            var sized = footprints ? Sized(fitting, at.W, at.H) : fitting.ToList();
            var runsBelow = RunsBelow(at.I, at.J, at.W, at.H);
            if (runsBelow && footprints && sized.Count == 0)
                sized = fitting.Where(c => (c.W == at.W && c.H > at.H) || (c.H == at.W && c.W > at.H)).ToList();
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
                unread += rect.Cells.Count > 0 ? rect.Cells.Count : rect.W * rect.H;
                continue;
            }
            var pixels = Pixels(at.I, at.J, at.W, at.H);
            var contradicted = fitting.Count > 0 && sized.Count == 0;
            // An item in its place whose label no reading fit (owner, 2026-10-26, decision 1): PaddleOCR's reading of its
            // band, matched as Windows' is, at the cell's footprint. Never where a reading fit and the footprint contradicted
            // it (Windows read that label), never an odd shape's cells, never a probe that settles where cells stand.
            var byPaddle = false;
            if (fitting.Count == 0 && !rect.Odd)
            {
                var cut = quickSlots && QuickSlotNumber(bitmap, pixels, pitch);
                if (await PaddleAsync(bitmap, LabelRect(bitmap, pixels, pitch), pitch, text => catalogue.Match.Match(text, at.W, kind, cut), catalogue, kind, cut, ct) is { } paddle &&
                    (footprints ? Sized(paddle.Best, at.W, at.H) : paddle.Best) is { Count: > 0 } named)
                    (sized, label, fit, byPaddle) = (named, paddle.Label, paddle.Fit, true);
            }
            // A label several items of the cell's footprint share: the picture against their grid images (owner, 2026-10-25).
            (sized, var icons) = catalogue.Icons.Settle(bitmap, pixels, pitch, sized, footprints ? DeskIconPlace.Footprint : DeskIconPlace.Fitted, at.W, at.H);
            var ids = sized.Select(c => c.ItemId).Distinct(StringComparer.Ordinal).ToList();
            string? countText = null;
            DeskPrint? print = null;
            var count = 1;
            bool? fir = null;
            // An item running on under a window: its count and mark at its bottom right are the window's pixels, not read.
            var bottomHidden = runsBelow || hidden[(at.J + at.H - 1) * grid.Cols + at.I + at.W - 1] ||
                               UnderAny(SKRectI.Create(pixels.Right - pitch, pixels.Bottom - (int)Math.Round(0.3 * pitch), pitch, (int)Math.Round(0.3 * pitch)));
            if (ids.Count > 0 && !bottomHidden)
            {
                (countText, print) = await CountAsync(bitmap, pixels, pitch, ct);
                count = print is null ? StashLabels.Count(countText) : 1;
                fir = StashMarks.Seen(bitmap, pixels, pitch);
            }
            var cell = new DeskCell(i0 + at.I, j0 + at.J, at.W, at.H, ids) { Count = count, Print = print, FoundInRaid = fir, Holds = HoldsOf(sized) };
            if (ids.Count == 0)
                unread += at.W * at.H;
            cells.Add(cell);
            looks.Add(new DeskCellLook(region, pixels, cell, label, fit, contradicted, countText) { Icons = icons, ByPaddle = byPaddle });
        }
        return (cells.OrderBy(c => c.J).ThenBy(c => c.I).ToList(), covered, odd, unread);
    }

    // The most cells of an odd shape read one by one (a cross of five keys under a hover highlight; a bigger shape is
    // more likely large items cut into cells, whose cells alone fit nothing). Only the cells whose band shows a label's
    // ink are read (LabelInk).
    private const int OddCellsRead = 24;

    /// <summary>
    /// Where a label is read: the cell's rectangle ending three pixels past its label's ink (the light pixels in its top
    /// band, the game's labels near white), so that no line or highlight right of the label is read as a letter (owner's
    /// second test, 2026-10-25: the 6L31 magazine's orange highlight read as a "1" after it, "6L311"); up to
    /// <paramref name="reach"/> pixels past the rectangle where a frame came out narrower than its box (the Zhuk rig's
    /// slot, its label's "k" past the frame found: "Zhi-o"). The rectangle as it is where no ink shows.
    /// </summary>
    internal static SKRectI LabelRect(SKBitmap bitmap, SKRectI rect, int pitch, int reach = 0)
    {
        int y0 = Math.Max(0, rect.Top + 3), y1 = Math.Min(bitmap.Height, rect.Top + (int)Math.Round(0.2 * pitch));
        var left = rect.Left + (int)Math.Round(0.3 * pitch);
        for (var x = Math.Min(bitmap.Width - 1, rect.Right - 1 + reach); x >= left; x--)
        {
            for (var y = y0; y < y1; y++)
            {
                var c = bitmap.GetPixel(x, y);
                if (Math.Min(c.Red, Math.Min(c.Green, c.Blue)) >= LabelInkLevel)
                    return rect with { Right = Math.Min(rect.Right + reach, x + 3) };
            }
        }
        return rect;
    }

    // The least of a label's ink's channels: the game's labels are near white (214 grey), dimmed in a window behind another
    // (100 to 149: the Beta 2 window's "Water" under the CR MK2 (R) window, picture 15); its lines 73 to 96, a window's gold
    // outline 97, its orange highlight 69 at the least.
    private const int LabelInkLevel = 110;

    /// <summary>Whether a cell's label band shows ink a label may be: a few light pixels (<see cref="LabelInkLevel"/>) in its
    /// top band, right of where a quick slot's plate stands. A cell without is no item's top right.</summary>
    internal static bool LabelInk(SKBitmap bitmap, SKRectI cell, int pitch)
    {
        int x0 = cell.Left + (int)Math.Round(0.3 * pitch), x1 = cell.Right - 2;
        int y0 = cell.Top + 3, y1 = cell.Top + (int)Math.Round(0.2 * pitch);
        var light = 0;
        for (var y = Math.Max(0, y0); y < Math.Min(bitmap.Height, y1); y++)
        {
            for (var x = Math.Max(0, x0); x < Math.Min(bitmap.Width, x1); x++)
            {
                var c = bitmap.GetPixel(x, y);
                if (Math.Min(c.Red, Math.Min(c.Green, c.Blue)) >= LabelInkLevel)
                    light++;
            }
        }
        return light >= 6;
    }

    /// <summary>Whether a cell shows a quick slot's number at its top left: the game's light plate, about a third of a
    /// cell square, with the slot's digit on it ("6" over "Alu splint"). Measured on the screenshots of 2026-10-19: the
    /// plate's inner part 64 to 71 % near-white, a pocket's item without one 0 to 1 %, the stash's cells 45 % at the
    /// most (a white item's art).</summary>
    internal static bool QuickSlotNumber(SKBitmap bitmap, SKRectI cell, int pitch)
    {
        int x0 = cell.Left + (int)Math.Round(0.10 * pitch), x1 = cell.Left + (int)Math.Round(0.30 * pitch);
        int y0 = cell.Top + (int)Math.Round(0.12 * pitch), y1 = cell.Top + (int)Math.Round(0.28 * pitch);
        if (x0 < 0 || y0 < 0 || x1 > bitmap.Width || y1 > bitmap.Height || x1 <= x0 || y1 <= y0)
            return false;
        var light = 0;
        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
            {
                var c = bitmap.GetPixel(x, y);
                if (Math.Min(c.Red, Math.Min(c.Green, c.Blue)) >= 170)
                    light++;
            }
        }
        return light >= 0.55 * (x1 - x0) * (y1 - y0);
    }

    // A label of three letters or fewer ("TG" read for "TG arm.") is read once more: a longer reading that fits wins.
    private static bool Confirm(string label) => Core.Screenshots.ExitList.Fold(label).Length <= 3;

    // A label that fit no name in any of its readings but begins with a gun's short name and goes on (a weapon preset's
    // name, "USP .45 Expert"; owner, 2026-10-26): the gun (DeskMatch.Preset), at the least fit that names. Only after every
    // reading failed, so a cell a reading names stays as it was.
    private static (string? Label, double Fit, List<DeskCandidate>? Best) OrPreset((string? Label, double Fit, List<DeskCandidate>? Best) read,
        DeskCatalogue catalogue, DeskSlotKind kind, bool cutAtStart = false) =>
        read.Label is { } label && read.Fit < DeskMatch.Enough && catalogue.Match.Preset(label, kind, cutAtStart) is { Count: > 0 } guns
            ? (label, DeskMatch.Enough, guns)
            : read;

    private static bool Fits(DeskCandidate c, int w, int h) => (c.W == w && c.H == h) || (c.W == h && c.H == w);

    /// <summary>The candidates whose footprint is the cell's (either way round), or where none is, the guns the cell can
    /// hold: a gun's footprint in the item file is its bare receiver's (the M4A1 one by one, the AKS-74U three by one), and
    /// the game draws it with its mods, larger (the 20:38 page of 2026-10-19: the M4A1 six by two, the AKMN five by two).</summary>
    private static List<DeskCandidate> Sized(IReadOnlyList<DeskCandidate> candidates, int w, int h)
    {
        var exact = candidates.Where(c => Fits(c, w, h)).ToList();
        return exact.Count > 0 ? exact
            : candidates.Where(c => c.Holds == DeskHolds.Mods && ((c.W <= w && c.H <= h) || (c.W <= h && c.H <= w))).ToList();
    }

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
    // "1469/1800"), the reading with more digits taken. Where the band reads nothing, it is read again by its brightest
    // channel (the game prints a used-up med's "1/3" in red, which the recognition reads dim), and on a cell wider than
    // one in the wider band (a slot's "147/147" past the one-cell band's reach; QA pass of 2026-10-19, F4).
    private async Task<(string? Text, DeskPrint? Print)> CountAsync(SKBitmap bitmap, SKRectI pixels, int pitch, CancellationToken ct)
    {
        // The band starts right of a magazine's loaded mark (layout round, 2026-10-28: the AVS rig's three MPX magazines,
        // "0/41" read "10/41", the mark's glyph a "1").
        if (MagazineMark(bitmap, pixels, pitch) is { } mark)
            pixels = new SKRectI(Math.Min(mark, pixels.Right - pitch / 2), pixels.Top, pixels.Right, pixels.Bottom);
        // Only a two-pixel line (a window's grid) is read as a "1" after the number: else that "1" is the maximum's own
        // (the same magazines' "4/41", taken for "4/4").
        var lineRead = TwoPixelLine(bitmap, pixels, pitch);
        var text = await _labels.ReadCountAsync(bitmap, pixels, pitch, ct)
                   ?? await _labels.ReadCountAsync(bitmap, pixels, pitch, ct, lit: true);
        if (text is null && pixels.Width > pitch)
            text = await _labels.ReadCountAsync(bitmap, pixels, pitch, ct, cells: 1.5)
                   ?? await _labels.ReadCountAsync(bitmap, pixels, pitch, ct, cells: 1.5, lit: true);
        if (text is null || !text.Contains('/'))
            return (text, null);
        var print = PrintOf(text, lineRead);
        if (pixels.Width > pitch)
        {
            var wide = await _labels.ReadCountAsync(bitmap, pixels, pitch, ct, cells: 1.5);
            if (PrintOf(wide, lineRead) is { } other && (print is null || other.Current.ToString().Length > print.Value.Current.ToString().Length))
                (text, print) = (wide, other);
        }
        return (text, print);
    }

    /// <summary>A printed "current/max" as the recognition reads it: junk before the first digit and after the last dropped
    /// (the cell's line read as "\" or "|"), and a "1" after the maximum that makes it more than the current's reach (the
    /// line of a window's grid, two pixels wide, read as a digit: "400/4001", "3/31") taken off when what is left still
    /// holds the current.</summary>
    internal static DeskPrint? PrintOf(string? text, bool lineRead = true)
    {
        if (string.IsNullOrWhiteSpace(text) || !text.Contains('/'))
            return null;
        var t = text.Trim().TrimEnd('\\', '|', '"', '\'', '.', ',', ' ');
        var start = t.IndexOfAny("0123456789OØSsIl".ToCharArray());
        if (start > 0)
            t = t[start..];
        if (DeskPrint.Parse(t) is not { } print)
            return null;
        var max = print.Max.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (lineRead && max.Length >= 2 && max[^1] == '1' && int.Parse(max[..^1], System.Globalization.CultureInfo.InvariantCulture) is var shorter && shorter >= print.Current
            && max.Length > print.Current.ToString(System.Globalization.CultureInfo.InvariantCulture).Length)
            return new DeskPrint(print.Current, shorter);
        return print;
    }

    /// <summary>Where a magazine's loaded mark ends, at its cell's bottom left: the game's small green square with a light
    /// glyph on it (its green about 145 over 100 and 85, measured at 1440p), null where there is none.</summary>
    internal static int? MagazineMark(SKBitmap bitmap, SKRectI cell, int pitch)
    {
        int x0 = cell.Left + (int)Math.Round(0.04 * pitch), x1 = cell.Left + (int)Math.Round(0.36 * pitch);
        int y0 = cell.Bottom - (int)Math.Round(0.3 * pitch), y1 = cell.Bottom - (int)Math.Round(0.03 * pitch);
        if (x0 < 0 || y0 < 0 || x1 > bitmap.Width || y1 > bitmap.Height || x1 <= x0 || y1 <= y0)
            return null;
        int green = 0, right = -1;
        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
            {
                var c = bitmap.GetPixel(x, y);
                if (c.Green >= 100 && c.Green >= c.Red + 25 && c.Green >= c.Blue + 25)
                {
                    green++;
                    right = Math.Max(right, x);
                }
            }
        }
        return green >= 0.1 * (x1 - x0) * (y1 - y0) ? right + 2 : null;
    }

    /// <summary>Whether the line along a cell's right side, at its count's height, is two pixels wide or more (a window's
    /// grid; a stash's or a compartment's line is one): that one may be read as a "1" after the count.</summary>
    internal static bool TwoPixelLine(SKBitmap bitmap, SKRectI cell, int pitch)
    {
        int y0 = cell.Bottom - (int)Math.Round(0.22 * pitch), y1 = cell.Bottom - 2;
        if (cell.Right - 4 < 0 || cell.Right + 3 >= bitmap.Width || y0 < 0 || y1 > bitmap.Height || y1 <= y0)
            return true;
        var means = new double[8];
        for (var d = -4; d <= 3; d++)
        {
            double sum = 0;
            for (var y = y0; y < y1; y++)
            {
                var c = bitmap.GetPixel(cell.Right + d, y);
                sum += 0.299 * c.Red + 0.587 * c.Green + 0.114 * c.Blue;
            }
            means[d + 4] = sum / (y1 - y0);
        }
        // The line's columns: within two pixels of the side, lighter than both ends by half the line's height.
        double field = Math.Min(means[0], means[7]), peak = means.Skip(2).Take(4).Max();
        return peak - field >= 10 && means.Skip(2).Take(4).Count(m => m > field + 0.5 * (peak - field)) >= 2;
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
