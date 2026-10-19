using Shturmap.Core.Desk;
using Shturmap.Core.Screenshots;
using SkiaSharp;

namespace Shturmap.Game.Screenshots;

/// <summary>Which screen a picture shows, for the stash desk: the GEAR tab (the loadout beside the stash), the stash alone
/// (the existing stash pages, a container's window over it), the trading screen (its cells print prices: nothing read), or
/// none of them.</summary>
public enum DeskScreen
{
    None,
    Gear,
    Stash,
    Trading,
}

/// <summary>A loadout slot's box on the GEAR tab, under its heading.</summary>
internal sealed record DeskSlotBox(string Key, SKRectI Box);

/// <summary>
/// A region as the screen lays it out: its kind, its slot box (a middle region's own container), its compartments in
/// pixels, and for a window its title and its whole area.
/// </summary>
internal sealed record DeskPlace(DeskRegionKind Kind, SKRectI? SlotBox, IReadOnlyList<SKRectI> Parts)
{
    public string? Title { get; init; }
    public SKRectI? Area { get; init; }
}

/// <summary>What the layout found on a picture: the screen, the loadout's slot boxes, the regions, the stash's grid, the
/// windows' and toasts' areas (what lies under them isn't read), and the frames no region took.</summary>
internal sealed record DeskLayoutFound(DeskScreen Screen, IReadOnlyList<DeskSlotBox> Slots, IReadOnlyList<DeskPlace> Regions, StashGrid? Stash,
    IReadOnlyList<SKRectI> Over, int Unplaced, IReadOnlyList<SKRectI> Frames);

/// <summary>
/// Where the GEAR tab's regions stand (five screenshots of 2026-10-19 at 2560×1440, docs/DESIGN.md §2, "For the stash
/// desk"): the loadout's slots under their headings at the left (EARPIECE … SHEATH), the middle column's parts under
/// theirs (TACTICAL RIG, POCKETS, SPECIAL SLOTS, BACKPACK, POUCH: a slot with its container's grid inline), the stash at
/// the right under SEARCH and SORT TABLE. The headings are read once over the whole picture at its size (about 150 ms);
/// the compartments are framed rectangles (<see cref="DeskFrames"/>). A container's floating window is a title (its
/// name, with search, sort and close glyphs) over compartments of its own; what it covers isn't read. The stash pane's
/// head (it prints the player's money) and the game's toasts at the lower right (they carry the screenshot's path) are
/// never read.
/// </summary>
internal static class DeskLayout
{
    private static readonly (DeskRegionKind Kind, string Heading)[] Middle =
    [
        (DeskRegionKind.Rig, "TACTICAL RIG"), (DeskRegionKind.Pockets, "POCKETS"), (DeskRegionKind.SpecialSlots, "SPECIAL SLOTS"),
        (DeskRegionKind.Backpack, "BACKPACK"), (DeskRegionKind.Pouch, "POUCH"),
    ];

    // A loadout slot's box under its heading, in cells: most are two by two; the armband and the dog tag a strip; the
    // weapons on the sling and on the back five wide. Measured on the screenshots of 2026-10-19 (1440p: the heading's
    // word starts 5 px in from the box, which starts 4 px under the word).
    private static (double W, double H) SlotSize(string key) => key switch
    {
        "armband" or "dogtag" => (2, 0.76),
        "onsling" or "onback" => (4.95, 2.05),
        _ => (2, 2.05),
    };

    /// <summary>The headings found: each phrase with its first word's box (<paramref name="all"/> gets every word of them).</summary>
    internal static List<(string Heading, DeskWord Word)> Headings(IReadOnlyList<DeskWord> words, IEnumerable<string> phrases, HashSet<DeskWord>? all = null)
    {
        var found = new List<(string, DeskWord)>();
        var wanted = phrases.Select(p => (Phrase: p, Parts: p.Split(' ').Select(ExitList.Fold).ToArray())).ToList();
        foreach (var line in words.GroupBy(w => w.Line))
        {
            var list = line.OrderBy(w => w.X).ToList();
            for (var i = 0; i < list.Count; i++)
            {
                foreach (var (phrase, parts) in wanted)
                {
                    if (i + parts.Length > list.Count)
                        continue;
                    var same = true;
                    for (var k = 0; k < parts.Length && same; k++)
                        same = ExitList.Fold(list[i + k].Text) == parts[k] && list[i + k].Text.Any(char.IsUpper) && !list[i + k].Text.Any(char.IsLower);
                    // The words of one heading stand close: "ON" and "BACK" of ON BACK, not "ON" of ON SLING and the top BACK.
                    for (var k = 1; k < parts.Length && same; k++)
                        same = list[i + k].X - list[i + k - 1].Right < 3 * list[i].H;
                    if (same)
                    {
                        found.Add((phrase, list[i]));
                        for (var k = 0; k < parts.Length; k++)
                            all?.Add(list[i + k]);
                    }
                }
            }
        }
        return found;
    }

    public static DeskLayoutFound Find(IReadOnlyList<DeskWord> words, StashLines lines, StashGray gray, int w, int h, int pitch)
    {
        var k = pitch / 84.0;
        var grids = StashGrids.Find(lines, gray, w, h, pitch);
        if (grids.Any(g => g.Place == StashGridPlace.Left))
            return new DeskLayoutFound(DeskScreen.Trading, [], [], null, [], 0, []);
        var stash = grids.FirstOrDefault(g => g.Place == StashGridPlace.Stash);

        var headingWords = new HashSet<DeskWord>();
        var loadout = Headings(words, DeskSlots.Loadout.Select(s => s.Heading), headingWords)
            .Where(x => x.Word.X < w * 0.4)
            // "BACK" alone is the screen's own button at the top right; ON BACK needs its "ON".
            .GroupBy(x => x.Heading).Select(g => g.OrderBy(x => x.Word.X).First()).ToList();
        var middle = Headings(words, Middle.Select(m => m.Heading), headingWords).Where(x => x.Word.X > w * 0.25 && x.Word.X < w * 0.66).ToList();
        var gear = loadout.Count >= 4;
        var screen = gear ? DeskScreen.Gear : stash is not null ? DeskScreen.Stash : DeskScreen.None;
        if (screen == DeskScreen.None)
            return new DeskLayoutFound(DeskScreen.None, [], [], null, [], 0, []);

        // The game's toasts at the lower right carry the screenshot's path: never read, nor what they cover (to the
        // screen's right edge, where the path runs on).
        var over = new List<SKRectI>();
        var toastLines = words.Where(x => x.X > w * 0.55 && x.Y > h * 0.6 && Toast(x.Text)).Select(x => x.Line).ToHashSet();
        if (toastLines.Count > 0)
        {
            var toast = words.Where(x => toastLines.Contains(x.Line)).ToList();
            var box = new SKRectI(toast.Min(x => x.X), toast.Min(x => x.Y), w, toast.Max(x => x.Bottom));
            over.Add(SKRectI.Inflate(box, pitch / 2, (int)(0.4 * pitch)));
        }

        // The windows first: their frames lie over everything, so they are whole; what they cover is found around them.
        // Down to the QUICK USE bar: its slots are framed too, and are no grid.
        var quickUse = words.FirstOrDefault(x => ExitList.Fold(x.Text) == "qu1ck" && x.Y > h * 0.75);
        var whole = new SKRectI(0, (int)(h * 0.05), w, quickUse is not null ? quickUse.Y + (int)(0.3 * pitch) : (int)(h * 0.885));
        // Inside the stash, frames on its own lattice are its cells, never a window's.
        var stashRect = stash is null ? SKRectI.Empty : SKRectI.Create(stash.X0, stash.Y0, stash.Cols * pitch, stash.Rows * pitch);
        bool StashCell(int x, int y) => stash is not null && stashRect.Contains(x + 2, y + 2) && OnLattice(x - stash.X0, pitch) && OnLattice(y - stash.Y0, pitch);
        var frames = Snapped(DeskFrames.Find(lines, pitch, whole, over, skip: StashCell), gray, pitch);
        frames.RemoveAll(f => StashCell(f.Left, f.Top));
        var lattices = grids.Where(g => g.Place == StashGridPlace.Window).Select(g => SKRectI.Create(g.X0, g.Y0, g.Cols * pitch, g.Rows * pitch)).ToList();
        var windows = Windows(words, gray, frames, lattices, headingWords, w, h, pitch);
        var windowAreas = windows.Select(x => x.Area!.Value).ToList();
        over.AddRange(windowAreas);
        if (windows.Count > 0)
        {
            // What a window covers: the frames running under it found again, their parts under it not asked for.
            frames = DeskFrames.Largest(frames.Where(f => !windowAreas.Any(a => a.IntersectsWith(f)) || windows.Any(x => x.Parts.Contains(f)))
                .Concat(Snapped(DeskFrames.Find(lines, pitch, whole, over, under: true, skip: StashCell), gray, pitch)).ToList());
        }
        var taken = new HashSet<SKRectI>(windows.SelectMany(x => x.Parts));

        var regions = new List<DeskPlace>(windows);
        var slots = new List<DeskSlotBox>();
        foreach (var (heading, word) in loadout)
        {
            var key = DeskSlots.Loadout.First(s => s.Heading == heading).Key;
            var (sw, sh) = SlotSize(key);
            slots.Add(new DeskSlotBox(key, SKRectI.Create(word.X - (int)Math.Round(5 * k), word.Bottom + (int)Math.Round(4 * k), (int)Math.Round(sw * pitch), (int)Math.Round(sh * pitch))));
        }

        // The middle column: the compartments between the loadout and the stash pane, each under the lowest heading above it
        // that starts left of it. A frame that runs into the column's bottom is cut by its scrolling: its region is left out
        // (the page keeps what it had), as one whose heading scrolled away above.
        var unplaced = 0;
        if (gear && middle.Count > 0)
        {
            var left = middle.Min(m => m.Word.X) - pitch / 2;
            var right = stash is not null ? stash.X0 - (int)(0.6 * pitch) : (int)(w * 0.66);
            var bottom = quickUse is not null ? quickUse.Y + (int)(0.25 * pitch) : (int)(h * 0.885);
            var heads = middle.Select(m => (Kind: Middle.First(x => x.Heading == m.Heading).Kind, m.Word)).ToList();
            var placed = new Dictionary<DeskRegionKind, (SKRectI? Slot, List<SKRectI> Parts, bool Cut)>();
            foreach (var (kind, _) in heads)
                placed.TryAdd(kind, (null, [], false));
            foreach (var frame in frames.Where(f => !taken.Contains(f) && f.Left >= left && f.Right <= right + pitch / 4))
            {
                var head = heads.Where(x => x.Word.Bottom <= frame.Top + 4 && x.Word.X - 0.4 * pitch <= frame.Left)
                    .OrderByDescending(x => x.Word.Y).ThenByDescending(x => x.Word.X).Select(x => ((DeskRegionKind, DeskWord)?)x).FirstOrDefault();
                if (head is not { } found)
                {
                    unplaced++;
                    continue;
                }
                var (kind, hw) = found;
                var entry = placed[kind];
                if (frame.Bottom >= bottom || Continues(lines, frame, pitch))
                {
                    placed[kind] = entry with { Cut = true };
                    continue;
                }
                var isSlot = kind is DeskRegionKind.Rig or DeskRegionKind.Backpack or DeskRegionKind.Pouch && entry.Slot is null &&
                             Math.Abs(frame.Left - hw.X) < 0.4 * pitch && frame.Top - hw.Bottom < 0.6 * pitch && Math.Abs(frame.Width - 2 * pitch) < 0.3 * pitch;
                if (isSlot)
                    placed[kind] = entry with { Slot = frame };
                else
                    entry.Parts.Add(frame);
            }
            foreach (var (kind, (slot, parts, cut)) in placed)
            {
                if (cut)
                    continue;
                // A container's slot with no grid found beside it: its grid is under a window or out of view; only an
                // empty slot (no container) has none.
                regions.Add(new DeskPlace(kind, slot, parts));
            }
        }
        return new DeskLayoutFound(screen, slots, regions, stash, over, unplaced, frames);
    }

    // The frames moved onto their lines' middle, as the stash's grid is (a cell cut two pixels early reads worse).
    private static List<SKRectI> Snapped(List<SKRectI> frames, StashGray gray, int pitch) =>
        frames.Select(f =>
        {
            var (x, y) = StashGrids.Snap(gray, f.Left, f.Top, Math.Max(1, (int)Math.Round(f.Width / (double)pitch)), Math.Max(1, (int)Math.Round(f.Height / (double)pitch)), pitch);
            return SKRectI.Create(x, y, f.Width, f.Height);
        }).ToList();

    // Whether a frame is part of a grid seen only in part (the middle column scrolled, a grid running out of view): its top
    // line runs on past its right corner, or its left line past its bottom one, with no gap. A compartment's own frame
    // ends at its corners (a few pixels of nothing to the next compartment).
    private static bool Continues(StashLines lines, SKRectI frame, int pitch)
    {
        var reach = Math.Max(4, pitch / 12);
        return lines.Horizontal(frame.Top, frame.Right + 2, frame.Right + 2 + reach) >= 0.8 ||
               lines.Vertical(frame.Left, frame.Bottom + 2, frame.Bottom + 2 + reach) >= 0.8;
    }

    // Whether a word is a toast's: the screenshot's path or its words.
    private static bool Toast(string text) =>
        text.Contains('\\') || text.Contains(":/", StringComparison.Ordinal) || text.Contains(".png", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("screenshot", StringComparison.OrdinalIgnoreCase);

    private static bool OnLattice(int offset, int pitch)
    {
        var phase = ((offset % pitch) + pitch) % pitch;
        return phase <= 3 || phase >= pitch - 3;
    }

    // The glyphs of a window's title bar as the recognition reads them: the magnifier before the title, sort and close
    // after it.
    private static bool Close(string text) => text.Trim() is "x" or "X" or "×";

    private static bool Glyph(string text) => text.Trim().Length == 1;

    // The least height of a title's word, in cells: a title is 15 px at 1440p (84 px a cell), a label at most 14.
    private const double TitleHeight = 0.17;

    // A window's title that is none: its search field open ("ITEM SEARCH", then "enter name").
    private static bool NoTitle(string title) => ExitList.Fold(title) is "1temsearch" or "enkername" or "entername";

    // The windows: a title bar over compartments of their own. The bar is a line of words with a glyph of its own (the
    // magnifier before the title, or sort and close after it) right over a compartment (a cell's height at most above
    // it, the title starting near its left edge), and no word of it inside a frame (a cell's label). Its compartments
    // are that one and those touching it, as far as the close glyph. A window the stash's grid finding saw (its
    // two-pixel lines) with no bar read is a window too, of no name: its search field open, or its title unread.
    private static List<DeskPlace> Windows(IReadOnlyList<DeskWord> words, StashGray gray, List<SKRectI> frames, List<SKRectI> lattices, HashSet<DeskWord> headings,
        int w, int h, int pitch)
    {
        var windows = new List<DeskPlace>();
        var used = new HashSet<SKRectI>();
        // A window the stash's grid finding saw stands on its lines' middle: its lattice rather than the frame found on it.
        var groups = lattices.Concat(frames.Where(f => !lattices.Any(l => Math.Abs(f.Left - l.Left) <= 4 && Math.Abs(f.Top - l.Top) <= 4))).ToList();
        bool InFrame(DeskWord x) => groups.Any(g => g.Contains(x.MidX, x.MidY));
        // The recognition reads the bar's glyphs and title as lines of their own: a row is the words level with the title.
        var candidates = words.Where(x => x.Y > h * 0.05 && x.Bottom < h * 0.92 && !headings.Contains(x) && !InFrame(x) &&
                                          x.Text.Count(char.IsLetterOrDigit) >= 2 && x.Text.Any(char.IsLetter)).OrderBy(x => x.Y).ThenBy(x => x.X).ToList();
        var titled = new HashSet<DeskWord>();
        foreach (var word in candidates)
        {
            if (titled.Contains(word))
                continue;
            var row = words.Where(x => Math.Abs(x.MidY - word.MidY) <= 0.2 * pitch && x.Right > word.X - pitch && x.X < word.X + 12 * pitch)
                .OrderBy(x => x.X).ToList();
            var at = row.IndexOf(word);
            var close = row.Skip(at + 1).FirstOrDefault(x => Close(x.Text));
            var magnifier = at > 0 && Glyph(row[at - 1].Text) && word.X - row[at - 1].Right < 0.6 * pitch;
            // Without its glyphs read, the title's size tells it from a cell's label: 15 px at 1440p, a label 11 to 14.
            if (!magnifier && close is null && word.H < TitleHeight * pitch)
                continue;
            var first = groups.Where(f => !used.Contains(f) && f.Top - word.Bottom >= -2 && f.Top - word.Bottom <= pitch &&
                                          word.X - f.Left >= -0.8 * pitch && word.X - f.Left <= 0.75 * pitch &&
                                          (close is null || f.Left < close.X))
                .OrderBy(f => f.Top - word.Bottom).ThenBy(f => Math.Abs(word.X - f.Left)).Select(f => (SKRectI?)f).FirstOrDefault();
            if (first is not { } start)
                continue;
            var title = new List<DeskWord>();
            foreach (var x in row.Skip(at))
            {
                if (Close(x.Text) || (Glyph(x.Text) && title.Count > 0) || (title.Count > 0 && x.X - title[^1].Right > 0.6 * pitch))
                    break;
                title.Add(x);
            }
            titled.UnionWith(title);
            var text = string.Join(" ", title.Select(t => t.Text));
            windows.Add(Window(groups, used, start, NoTitle(text) ? null : text, word.Y, close?.Right, pitch));
        }
        // A window's grid has two-pixel lines (the stash's and the GEAR tab's grids one): a grid the stash's finding saw
        // that has them and no title read is a window of no name.
        foreach (var lattice in lattices)
        {
            if (!used.Any(u => u.IntersectsWith(lattice)) && LineWidth(gray, lattice, pitch) >= 2)
                windows.Add(Window(groups, used, lattice, null, lattice.Top - (int)(1.1 * pitch), null, pitch));
        }
        return windows;
    }

    // How many pixels wide the line along a grid's left edge is: the columns around it, each averaged down the grid,
    // that stand out more than halfway from the field to the line's brightest.
    internal static int LineWidth(StashGray gray, SKRectI grid, int pitch)
    {
        int y0 = Math.Max(0, grid.Top + pitch / 4), y1 = Math.Min(gray.H, grid.Bottom - pitch / 4);
        if (y1 <= y0)
            return 0;
        var means = new double[9];
        for (var d = -4; d <= 4; d++)
        {
            var x = grid.Left + d;
            if (x < 0 || x >= gray.W)
                continue;
            double sum = 0;
            for (var y = y0; y < y1; y++)
                sum += gray[x, y];
            means[d + 4] = sum / (y1 - y0);
        }
        double peak = means.Max(), field = Math.Min(means[0], means[8]);
        return peak - field < 10 ? 0 : means.Count(m => m > field + 0.5 * (peak - field));
    }

    // A window from its first compartment: those touching it (a third of a cell apart at most) below its bar, up to the
    // close glyph; its area the compartments and the bar.
    private static DeskPlace Window(List<SKRectI> groups, HashSet<SKRectI> used, SKRectI start, string? title, int barTop, int? close, int pitch)
    {
        var reach = (int)(0.35 * pitch);
        var parts = new List<SKRectI> { start };
        var grown = true;
        while (grown)
        {
            grown = false;
            foreach (var f in groups)
            {
                if (parts.Contains(f) || used.Contains(f) || f.Top < barTop || f.Left < start.Left - reach || (close is { } c && f.Right > c + reach))
                    continue;
                if (parts.Any(p => SKRectI.Inflate(p, reach, reach).IntersectsWith(f)))
                {
                    parts.Add(f);
                    grown = true;
                }
            }
        }
        used.UnionWith(parts);
        int x0 = parts.Min(p => p.Left) - pitch / 2, y0 = barTop - (int)(0.25 * pitch);
        int x1 = Math.Max(parts.Max(p => p.Right), close ?? 0), y1 = parts.Max(p => p.Bottom);
        return new DeskPlace(DeskRegionKind.ContainerWindow, null, parts)
        {
            Title = title,
            Area = SKRectI.Inflate(new SKRectI(x0, y0, x1, y1), (int)(0.12 * pitch), (int)(0.12 * pitch)),
        };
    }
}
