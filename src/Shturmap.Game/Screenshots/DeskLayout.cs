using Shturmap.Core.Desk;
using Shturmap.Core.Screenshots;
using SkiaSharp;

namespace Shturmap.Game.Screenshots;

/// <summary>Which screen a picture shows, for the stash desk: the GEAR tab (the loadout beside the stash), the stash alone
/// (the existing stash pages, a container's window over it), the trading screen (its cells print prices: nothing read), or
/// none of them; for the in-raid glance, the raid's inventory screen (the GEAR tab without the stash, a LOOT pane at its
/// right when looting; <see cref="DeskLoot"/>).</summary>
public enum DeskScreen
{
    None,
    Gear,
    Stash,
    Trading,
    Raid,
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

    /// <summary>A LOOT part's search state (<see cref="DeskLoot"/>).</summary>
    public DeskSearch Search { get; init; }

    /// <summary>A LOOT part whose grid the pane's scrolling cut at its foot; on the GEAR tab, a middle region whose grid
    /// the column's foot cut, or whose frames ran on past what shows (<see cref="DeskLayout"/>).</summary>
    public bool Cut { get; init; }

    /// <summary>A container's slot box taken from its heading's place, no frame having shown it (a frame across the slot
    /// and its grid, the slot under a window): no label there says nothing of an empty slot.</summary>
    public bool SlotDerived { get; init; }

    /// <summary>A window's panel as measured (<see cref="DeskLayout.Panel"/>): its outline, the bar and the grid inside it;
    /// null where it wasn't found and the window's area is the old reckoning from its compartments.</summary>
    public SKRectI? Outline { get; init; }

    /// <summary>The areas of the windows this window lies over (their outline holds its bar): they hide none of its cells.</summary>
    public IReadOnlyList<SKRectI> Over { get; init; } = [];
}

/// <summary>What the layout found on a picture: the screen, the loadout's slot boxes, the regions, the stash's grid, the
/// windows' and toasts' areas (what lies under them isn't read), and the frames no region took; on the raid's inventory
/// screen, the LOOT pane when it shows.</summary>
internal sealed record DeskLayoutFound(DeskScreen Screen, IReadOnlyList<DeskSlotBox> Slots, IReadOnlyList<DeskPlace> Regions, StashGrid? Stash,
    IReadOnlyList<SKRectI> Over, int Unplaced, IReadOnlyList<SKRectI> Frames)
{
    public DeskLootFound? Loot { get; init; }

    /// <summary>What the layout left out and why, for measuring (shturmap-cli desk --why): a frame under no heading, a
    /// region cut at the column's foot, a title refused.</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];
}

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
    internal static (double W, double H) SlotSize(string key) => key switch
    {
        "armband" or "dogtag" => (2, 0.76),
        "onsling" or "onback" => (4.95, 2.05),
        _ => (2, 2.05),
    };

    /// <summary>Whether a word as read is a heading's word: in capitals, the same folded, or, for a word of four letters
    /// or more, one letter off (dropped, added or changed, the first one too; accents aside). Owner, 2026-10-25: the
    /// recognition read FACE COVER as "*ACE COVER" and ARMBAND, half under a window, as "ÄRMBANI" on the screenshots of
    /// 2026-10-19, and the slot box under each became a window. "BACK" alone stays the screen's own button: ON BACK needs
    /// its "ON", which, two letters, must be read as it is.</summary>
    internal static bool HeadingWord(string text, string wanted)
    {
        if (!text.Any(char.IsUpper) || text.Any(char.IsLower))
            return false;
        var read = ExitList.Fold(Plain(text));
        return read == wanted || (wanted.Length >= 4 && OneOff(read, wanted));
    }

    // The letters without their accents ("Ä" → "A").
    private static string Plain(string text) =>
        new(text.Normalize(System.Text.NormalizationForm.FormD).Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());

    // Whether two words are one letter apart: one dropped, added or changed.
    private static bool OneOff(string a, string b)
    {
        if (Math.Abs(a.Length - b.Length) > 1)
            return false;
        if (a.Length == b.Length)
            return a.Zip(b).Count(p => p.First != p.Second) == 1;
        var (shorter, longer) = a.Length < b.Length ? (a, b) : (b, a);
        var i = 0;
        while (i < shorter.Length && shorter[i] == longer[i])
            i++;
        return shorter.AsSpan(i).SequenceEqual(longer.AsSpan(i + 1));
    }

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
                        same = HeadingWord(list[i + k].Text, parts[k]);
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
        var headingWords = new HashSet<DeskWord>();
        var loadout = Headings(words, DeskSlots.Loadout.Select(s => s.Heading), headingWords)
            .Where(x => x.Word.X < w * 0.4)
            // "BACK" alone is the screen's own button at the top right; ON BACK needs its "ON".
            .GroupBy(x => x.Heading).Select(g => g.OrderBy(x => x.Word.X).First()).ToList();
        var middle = Headings(words, Middle.Select(m => m.Heading), headingWords).Where(x => x.Word.X > w * 0.25 && x.Word.X < w * 0.66).ToList();
        var gear = loadout.Count >= 4;
        var menuBar = DeskLoot.MenuBar(words, h);
        // In a raid the GEAR tab has no stash pane and no menus' bar at its foot (DeskLoot). Looting, the LOOT pane stands
        // where the menus' stash pane would, and a container's grid there may be ten cells wide or show lines enough to be
        // taken for the stash (the owner's weapon box of 2026-10-26: its 6×6 grid read as a stash 10×6, the screen as the
        // GEAR tab, and the glance said nothing). The loadout's headings with no menus' bar and the LOOT pane's head over the
        // right third are the raid's inventory: a grid there is the container's, never the stash.
        var lootHead = gear && !menuBar ? DeskLoot.Head(words, w, h) : null;
        var grids = StashGrids.Find(lines, gray, w, h, pitch, stashPane: lootHead is null);
        if (grids.Any(g => g.Place == StashGridPlace.Left))
            return new DeskLayoutFound(DeskScreen.Trading, [], [], null, [], 0, []);
        var stash = grids.FirstOrDefault(g => g.Place == StashGridPlace.Stash);
        var raid = gear && stash is null && !menuBar;
        var screen = raid ? DeskScreen.Raid : gear ? DeskScreen.Gear : stash is not null ? DeskScreen.Stash : DeskScreen.None;
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
        // The game's tooltip (the pointer over an item: its full name in a black box; owner's second test, 2026-10-25): what
        // it covers isn't read, and its words are never a label or a title.
        var tooltips = Tooltips(words, gray, lines, pitch);
        over.AddRange(tooltips);
        var notes0 = tooltips.Select(t => $"tooltip {Note(t)}").ToList();

        // The windows first: their frames lie over everything, so they are whole; what they cover is found around them.
        // Down to the QUICK USE bar: its slots are framed too, and are no grid. (Its word folds to "0u1ck": until 2026-10-25
        // it was looked for as "qu1ck", never found, and the foot was always 88.5 % of the picture's height.)
        var quickUse = words.FirstOrDefault(x => ExitList.Fold(x.Text) == ExitList.Fold("QUICK") && x.Y > h * 0.75);
        var whole = new SKRectI(0, (int)(h * 0.05), w, quickUse is not null ? quickUse.Y + (int)(0.3 * pitch) : (int)(h * 0.885));
        // The middle column's foot: where its view ends, its scrolling cutting what runs on below.
        var foot = quickUse is not null ? quickUse.Y + (int)(0.25 * pitch) : (int)(h * 0.885);
        var notes = new List<string>
        {
            "headings " + string.Join(", ", loadout.Concat(middle).Select(x => $"{x.Heading} \"{x.Word.Text}\" ({x.Word.X},{x.Word.Y} {x.Word.W}x{x.Word.H})")) +
            $"; foot {foot}" + (quickUse is { } q ? $" (QUICK USE at {q.Y})" : ""),
        };
        notes.AddRange(notes0);
        // The middle column, between the loadout and the stash pane, and its headings (the GEAR tab's; in a raid the same).
        var column = gear && middle.Count > 0
            ? new SKRectI(middle.Min(m => m.Word.X) - pitch / 2, whole.Top, stash is not null ? stash.X0 - (int)(0.6 * pitch) : (int)(w * 0.66), whole.Bottom)
            : SKRectI.Empty;
        var heads = middle.Select(m => (Kind: Middle.First(x => x.Heading == m.Heading).Kind, m.Word)).ToList();
        bool InColumn(SKRectI r) => !column.IsEmpty && r.Left >= column.Left && r.Right <= column.Right + pitch / 4;
        // The lowest heading above a rectangle that starts left of it.
        (DeskRegionKind Kind, DeskWord Word)? HeadOf(SKRectI r) =>
            heads.Where(x => x.Word.Bottom <= r.Top + 4 && x.Word.X - 0.4 * pitch <= r.Left)
                .OrderByDescending(x => x.Word.Y).ThenByDescending(x => x.Word.X).Select(x => ((DeskRegionKind, DeskWord)?)x).FirstOrDefault();

        // Inside the stash, frames on its own lattice are its cells, never a window's.
        var stashRect = stash is null ? SKRectI.Empty : SKRectI.Create(stash.X0, stash.Y0, stash.Cols * pitch, stash.Rows * pitch);
        bool StashCell(int x, int y) => stash is not null && stashRect.Contains(x + 2, y + 2) && OnLattice(x - stash.X0, pitch) && OnLattice(y - stash.Y0, pitch);
        // In the menus' middle column a compartment may run past the foot (DeskFrames.OpenAt); the raid's glance keeps to
        // whole ones.
        SKRectI? open = screen == DeskScreen.Gear && !column.IsEmpty ? column : null;
        var cutFound = new HashSet<SKRectI>();
        var frames = Snapped(DeskFrames.Find(lines, pitch, whole, over, skip: StashCell, open: open, cutFound: cutFound), gray, pitch, cutFound);
        frames.RemoveAll(f => StashCell(f.Left, f.Top));
        // The windows' lattices stand on their cells' lines (StashGrids.Aligned).
        var lattices = grids.Where(g => g.Place == StashGridPlace.Window).Select(g => SKRectI.Create(g.X0, g.Y0, g.Cols * pitch, g.Rows * pitch)).ToList();
        // A lattice in the middle column under one of its headings is that region's grid (the layout round's test
        // set, 02-gear.png and 09-gear.png: the POUCH's grid, its lines two pixels wide as a window's, read as a window of no
        // name, the POUCH left out); one inside a larger compartment is some of that one's cells. In a raid the glance keeps
        // its reading of before: a region the foot cuts is left out there, and its grid would go with it.
        var columnLattices = screen == DeskScreen.Gear
            ? lattices.Where(l => InColumn(l) && HeadOf(l) is not null && !InsideCompartment(l, frames, pitch)).ToList()
            : [];
        var loadoutRows = loadout.Select(x => x.Word.MidY).ToList();
        var paneLeft = stash is not null ? stash.X0 - (int)(0.6 * pitch) : lootHead is not null ? lootHead.X - pitch : (int)(w * 0.66);
        var windows = Windows(words, gray, lines, frames, lattices, columnLattices, headingWords, loadoutRows, paneLeft, w, h, pitch, notes, cutFound, over);
        // The LOOT pane's grids have a window's two-pixel lines and no title, or a part's "SEARCHING ✕" over them: the
        // pane's, not windows.
        if (lootHead is not null)
            windows.RemoveAll(x => (x.Title is null || DeskLoot.PaneWord(x.Title)) && x.Area is { } a && a.MidX > lootHead.X - pitch && a.MidY > lootHead.Bottom);
        var windowAreas = windows.Select(x => x.Area!.Value).ToList();
        over.AddRange(windowAreas);
        if (windows.Count > 0)
        {
            // What a window covers: the frames running under it found again, their parts under it not asked for. A window
            // measured by its panel takes no frame outside it: one running along its outline was its outline read as a
            // compartment's side (picture 14: the Beta 2 window's whole grid as one shape, its left side the outline 6 px off).
            var cutUnder = new HashSet<SKRectI>();
            frames = DeskFrames.Largest(frames.Where(f => !windowAreas.Any(a => a.IntersectsWith(f)) || windows.Any(x => x.Parts.Contains(f)))
                .Concat(Snapped(DeskFrames.Find(lines, pitch, whole, over, under: true, skip: StashCell, open: open, cutFound: cutUnder), gray, pitch, cutUnder)).ToList());
            cutFound.UnionWith(cutUnder);
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

        var unplaced = 0;
        // (A lattice inside a frame the windows' second look found is that frame's cells in view: a grid running under a
        // window, its rows below the window seen as a lattice of their own.)
        if (!column.IsEmpty)
            regions.AddRange(Column(lines, frames, cutFound,
                columnLattices.Where(l => !taken.Contains(l) && !windowAreas.Any(a => a.IntersectsWith(l)) && !InsideCompartment(l, frames, pitch)).ToList(),
                taken, windowAreas, heads, InColumn, HeadOf, foot, raid, k, pitch, notes, ref unplaced));
        var loot = lootHead is null ? null
            : DeskLoot.Find(words, lootHead, frames.Where(f => !taken.Contains(f)).ToList(),
                lattices.Where(l => !taken.Contains(l)).ToList(), w, foot, pitch);
        return new DeskLayoutFound(screen, slots, regions, stash, over, unplaced, frames) { Loot = loot, Notes = notes };
    }

    /// <summary>
    /// The middle column's regions: its compartments, each under the lowest heading above it that starts left of it, a
    /// container's own slot the first under BACKPACK, TACTICAL RIG and POUCH (its box under the heading where no frame shows
    /// it: a frame across the slot and its grid, or the slot under a window). A lattice under one of the column's headings
    /// is that region's grid, never a window; frames across it are dropped. A compartment whose heading a window may hide
    /// (a window between it and the heading above) is placed under none (the test set's 13-gear-keys.png: a
    /// cell of the POUCH's grid below the Keys window read as the BACKPACK's grid). A region whose grid the foot cuts
    /// (a compartment framed down to it, or one ending at it) is read as far as it shows and said cut; one whose frames run
    /// on past their corners is said cut with those frames left unread; the page keeps the rest (owner, 2026-10-25). In a
    /// raid a cut region is left out, as before.
    /// </summary>
    private static List<DeskPlace> Column(StashLines lines, List<SKRectI> frames, HashSet<SKRectI> cutFound, List<SKRectI> lattices, HashSet<SKRectI> taken,
        List<SKRectI> windowAreas, List<(DeskRegionKind Kind, DeskWord Word)> heads, Func<SKRectI, bool> inColumn,
        Func<SKRectI, (DeskRegionKind Kind, DeskWord Word)?> headOf, int foot, bool raid, double k, int pitch, List<string> notes, ref int unplaced)
    {
        // The region grids the lattices are, in place of the frames across them or inside them.
        var compartments = frames.Where(f => !taken.Contains(f) && inColumn(f) && !lattices.Any(l => l.IntersectsWith(f))).Concat(lattices).ToList();
        var placed = new Dictionary<DeskRegionKind, (SKRectI? Slot, List<SKRectI> Parts, bool Cut)>();
        foreach (var (kind, _) in heads)
            placed.TryAdd(kind, (null, [], false));
        foreach (var frame in compartments.OrderBy(f => f.Top).ThenBy(f => f.Left))
        {
            if (headOf(frame) is not { } found)
            {
                unplaced++;
                notes.Add($"frame {Note(frame)} under no heading");
                continue;
            }
            var (kind, hw) = found;
            // A heading stands left of its compartments: a window from the heading's place to the frame's left, over the
            // rows between them, may hide the frame's own heading; not where a compartment stands right above the frame (a
            // few pixels apart: no heading between them).
            var stacked = compartments.Any(o => o != frame && o.Bottom <= frame.Top + 2 && frame.Top - o.Bottom <= pitch / 4 && o.Left < frame.Right && o.Right > frame.Left);
            // The window must cover a heading's room between them (a quarter of a cell): the POUCH's grid right under its
            // heading, a window over the rows above both, is the POUCH's (owner's second test, 2026-10-25).
            if (!stacked && windowAreas.Any(a => a.Left < frame.Left + 0.4 * pitch && a.Right > hw.X &&
                                                 Math.Min(a.Bottom, frame.Top) - Math.Max(a.Top, hw.Bottom) >= pitch / 4))
            {
                unplaced++;
                notes.Add($"frame {Note(frame)} under {kind} with a window between: its heading may be hidden");
                continue;
            }
            var entry = placed[kind];
            if (!cutFound.Contains(frame) && Continues(lines, frame, pitch))
            {
                notes.Add($"{kind}: frame {Note(frame)} cut (its lines run on)");
                placed[kind] = entry with { Cut = true };
                continue;
            }
            // At the foot, a compartment ending there may run on below it (the raid's glance keeps its rule of before:
            // past it).
            var atFoot = cutFound.Contains(frame) || frame.Bottom >= foot - (raid ? 0 : pitch / 4);
            if (atFoot)
            {
                notes.Add($"{kind}: frame {Note(frame)} at the foot{(cutFound.Contains(frame) ? ", framed down to it" : "")}");
                entry = entry with { Cut = true };
                placed[kind] = entry;
            }
            var isSlot = DeskRegion.IsContainer(kind) && entry.Slot is null && !lattices.Contains(frame) &&
                         Math.Abs(frame.Left - hw.X) < 0.4 * pitch && frame.Top - hw.Bottom < 0.6 * pitch && Math.Abs(frame.Width - 2 * pitch) < 0.3 * pitch;
            if (isSlot)
                placed[kind] = entry with { Slot = frame };
            else
                entry.Parts.Add(frame);
        }
        var regions = new List<DeskPlace>();
        foreach (var (kind, (found, parts, cut)) in placed)
        {
            if (raid && cut)
                continue;
            var slot = found;
            var derived = false;
            // A container's slot no frame showed: its box under the heading, as the loadout's (5 px left of the word, 6
            // under it at 1440p), where the column shows it.
            if (slot is null && DeskRegion.IsContainer(kind) && heads.FirstOrDefault(x => x.Kind == kind).Word is { } word)
            {
                var box = SKRectI.Create(word.X - (int)Math.Round(5 * k), word.Bottom + (int)Math.Round(6 * k), 2 * pitch, 2 * pitch);
                if (box.Bottom <= foot)
                {
                    slot = box;
                    derived = true;
                }
            }
            // A part that is the slot's own box is no grid (the test set's 13-gear-keys.png: the backpack read
            // as a grid of one cell, its slot).
            if (slot is { } own)
                parts.RemoveAll(p => Overlapping(p, own));
            regions.Add(new DeskPlace(kind, slot, parts) { Cut = cut, SlotDerived = derived });
        }
        return regions;
    }

    private static string Note(SKRectI r) => $"({r.Left},{r.Top} {r.Width}x{r.Height})";

    // The frames moved onto their lines' middle, as the stash's grid is (a cell cut two pixels early reads worse); the cut
    // ones among them (DeskFrames.OpenAt) moved alike.
    private static List<SKRectI> Snapped(List<SKRectI> frames, StashGray gray, int pitch, HashSet<SKRectI>? cut = null)
    {
        var snapped = new List<SKRectI>();
        var cutSnapped = new HashSet<SKRectI>();
        foreach (var f in frames)
        {
            var (x, y) = StashGrids.Snap(gray, f.Left, f.Top, Math.Max(1, (int)Math.Round(f.Width / (double)pitch)), Math.Max(1, (int)Math.Round(f.Height / (double)pitch)), pitch);
            var moved = SKRectI.Create(x, y, f.Width, f.Height);
            snapped.Add(moved);
            if (cut?.Contains(f) == true)
                cutSnapped.Add(moved);
        }
        if (cut is not null)
        {
            cut.Clear();
            cut.UnionWith(cutSnapped);
        }
        return snapped;
    }

    // Whether two rectangles are one compartment seen twice (a lattice and the frame found on its lines, a few pixels
    // apart): the same size give or take a cell, their corners within a quarter of a cell.
    private static bool SameCompartment(SKRectI a, SKRectI b, int pitch) =>
        Math.Abs(a.Left - b.Left) <= pitch / 4 && Math.Abs(a.Top - b.Top) <= pitch / 4 &&
        Math.Abs(a.Width - b.Width) <= pitch && Math.Abs(a.Height - b.Height) <= pitch;

    // Whether a rectangle lies over another by more than a quarter of the smaller one: two compartments of one window
    // never overlap, so such a pair is one compartment found twice, and its cells would be read twice.
    internal static bool Overlapping(SKRectI a, SKRectI b)
    {
        var i = SKRectI.Intersect(a, b);
        return !i.IsEmpty && (long)i.Width * i.Height * 4 > Math.Min((long)a.Width * a.Height, (long)b.Width * b.Height);
    }

    // Whether a frame is part of a grid seen only in part (the middle column scrolled, a grid running out of view): its top
    // line runs on past its right corner, or its left line past its bottom one, with no gap. A compartment's own frame
    // ends at its corners (a few pixels of nothing to the next compartment).
    private static bool Continues(StashLines lines, SKRectI frame, int pitch)
    {
        var reach = Math.Max(4, pitch / 12);
        return lines.Horizontal(frame.Top, frame.Right + 2, frame.Right + 2 + reach) >= 0.8 ||
               lines.Vertical(frame.Left, frame.Bottom + 2, frame.Bottom + 2 + reach) >= 0.8;
    }

    // Whether a word is a toast's: the screenshot's path or its words; in a raid the game's "Searching experience – Items
    // (50)", "Looting experience" (the 22 raid screenshots of 2026-10-19), which may cover the LOOT pane's foot.
    private static bool Toast(string text) =>
        text.Contains('\\') || text.Contains(":/", StringComparison.Ordinal) || text.Contains(".png", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("screenshot", StringComparison.OrdinalIgnoreCase) || text.Contains("experience", StringComparison.OrdinalIgnoreCase);

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

    /// <summary>How many letters a word has that are no digit's lookalike (Ø, O and o for 0; l, I and | for 1; S and s for 5;
    /// B for 8): a title needs two (owner's second test, 2026-10-25: a pistol's rounds "0/17" in the stash, its slashed zero
    /// read as the letter Ø, made the window "Ø/17").</summary>
    internal static int TitleLetters(string text) =>
        text.Count(c => char.IsLetter(c) && c is not ('Ø' or 'ø' or 'O' or 'o' or 'l' or 'I' or 'S' or 's' or 'B'));

    // The windows: a title bar over compartments of their own. The bar is a line of words with a glyph of its own (the
    // magnifier before the title, or sort and close after it) right over a compartment (a cell's height at most above
    // it, the title starting near its left edge), and no word of it inside a frame (a cell's label). Its compartments
    // are that one and those touching it, as far as the close glyph. A window the stash's grid finding saw (its
    // two-pixel lines) with no bar read is a window too, of no name: its search field open, or its title unread; never
    // one of the middle column's region grids (columnLattices).
    //
    // Where the loadout and the middle column stand, a bare word in capitals is no title (owner, 2026-10-25; the owner's
    // screenshots of 2026-10-19: FACE COVER read as "ACE COVER" and ARMBAND, half under the Keys window, as "ÄRMBANI" were
    // no headings, and the slot box under each became a window): a word level with the loadout's headings, left of 0.4 of
    // the width, is a title only after the magnifier; left of the stash pane, a title in capitals needs its bar's glyphs
    // read, or a two-pixel lattice under it that is no region's grid. A title in small letters (a container's short name,
    // "Zhuk": the game's headings are capitals) and, right of the pane's edge, any title still counts by its size alone
    // (the owner's GEAR pictures of 2026-10-19, the third: the Zhuk window over the middle column, its glyphs unread).
    // A compartment the column's foot cuts is never a window's: a window stands whole on the screen.
    //
    // A window is first looked for as the game draws it (owner's second test, 2026-10-25): a panel, a thin light outline
    // with black inside it, its bar at the top holding the title (Panel). Where the panel is measured, it is the window's
    // evidence and its area, and its compartments are found anew inside it (Parts): no frame outside it is its own (picture
    // 16: the POUCH's grid under the Beta 2 window's lower edge was taken as three more of its rows), and none running along
    // its outline (picture 14: the window's left side, 6 px off its grid, read as one compartment of the whole window). A
    // window whose bar stands inside another's outline lies over it.
    private static List<DeskPlace> Windows(IReadOnlyList<DeskWord> words, StashGray gray, StashLines lines, List<SKRectI> frames, List<SKRectI> lattices,
        List<SKRectI> columnLattices, HashSet<DeskWord> headings, IReadOnlyList<int> loadoutRows, int paneLeft, int w, int h, int pitch, List<string> notes,
        IReadOnlySet<SKRectI> cut, IReadOnlyList<SKRectI> over)
    {
        var windows = new List<DeskPlace>();
        var used = new HashSet<SKRectI>();
        // A window the stash's grid finding saw stands on its lines' middle (moved onto its cells' lines, StashGrids.Aligned): its
        // lattice rather than the frame found on it, a few pixels off (the Keys window of 2026-10-19, 20:21: both, 6 px
        // apart, read as two compartments one cell apart, its keys once and a "?" across the other).
        var groups = lattices.Concat(frames.Where(f => !cut.Contains(f) && !lattices.Any(l => SameCompartment(f, l, pitch)))).ToList();
        bool InFrame(DeskWord x) => groups.Any(g => g.Contains(x.MidX, x.MidY));
        bool HeadingRow(DeskWord x) => x.X < w * 0.4 && loadoutRows.Any(y => Math.Abs(x.MidY - y) <= 0.25 * pitch);
        // The recognition reads the bar's glyphs and title as lines of their own: a row is the words level with the title.
        // A word under a toast or a tooltip is neither.
        var candidates = words.Where(x => x.Y > h * 0.05 && x.Bottom < h * 0.92 && !headings.Contains(x) && TitleLetters(x.Text) >= 2 &&
                                          !over.Any(o => o.Contains(x.MidX, x.MidY))).OrderBy(x => x.Y).ThenBy(x => x.X).ToList();
        var titled = new HashSet<DeskWord>();
        var panels = new List<SKRectI>();
        foreach (var word in candidates)
        {
            if (titled.Contains(word))
                continue;
            var row = words.Where(x => Math.Abs(x.MidY - word.MidY) <= 0.2 * pitch && x.Right > word.X - pitch && x.X < word.X + 12 * pitch)
                .OrderBy(x => x.X).ToList();
            var at = row.IndexOf(word);
            var close = row.Skip(at + 1).FirstOrDefault(x => Close(x.Text));
            var magnifier = at > 0 && Glyph(row[at - 1].Text) && word.X - row[at - 1].Right < 0.6 * pitch;
            // The panel round the bar: its top left corner up to three quarters of a cell left of the title (or of its
            // magnifier) and half a cell above it; the title within its bar.
            var lead = magnifier ? row[at - 1] : word;
            var panel = Panel(lines, gray, new SKRectI(lead.X - (int)(0.75 * pitch), word.Y - (int)(0.45 * pitch), lead.X - 2, word.Y - 2), pitch);
            if (panel is { } found && (word.Bottom > found.Top + BarHeight * pitch || !Bar(gray, found, pitch) || panels.Contains(found)))
                panel = null;
            SKRectI? start = null;
            if (panel is null)
            {
                if (InFrame(word))
                    continue;
                // Without its glyphs read, the title's size tells it from a cell's label: 15 px at 1440p, a label 11 to 14.
                if (!magnifier && close is null && word.H < TitleHeight * pitch)
                    continue;
                if (!magnifier && HeadingRow(word))
                {
                    notes.Add($"\"{word.Text}\" at ({word.X},{word.Y}) where a loadout heading stands: no title");
                    continue;
                }
                start = groups.Where(f => !used.Contains(f) && !panels.Any(p => p.IntersectsWith(f)) && f.Top - word.Bottom >= -2 && f.Top - word.Bottom <= pitch &&
                                          word.X - f.Left >= -0.8 * pitch && word.X - f.Left <= 0.75 * pitch &&
                                          (close is null || f.Left < close.X))
                    .OrderBy(f => f.Top - word.Bottom).ThenBy(f => Math.Abs(word.X - f.Left)).Select(f => (SKRectI?)f).FirstOrDefault();
                if (start is not { } first)
                    continue;
                if (!magnifier && close is null && word.X < paneLeft && !word.Text.Any(char.IsLower) &&
                    !(lattices.Contains(first) && !columnLattices.Contains(first) && LineWidth(gray, first, pitch) >= 2))
                {
                    notes.Add($"\"{word.Text}\" at ({word.X},{word.Y}) in capitals left of the stash pane without its bar's glyphs: no title");
                    continue;
                }
            }
            var title = new List<DeskWord>();
            foreach (var x in row.Skip(at))
            {
                // A digit right after the title is the title's ("Beta 2"; owner's second test, 2026-10-25: read as a glyph,
                // the window was "Beta", which names no container).
                var titleDigit = title.Count > 0 && x.Text.Trim() is [var d] && char.IsDigit(d) && x.X - title[^1].Right <= 0.25 * pitch;
                if (Close(x.Text) || (Glyph(x.Text) && title.Count > 0 && !titleDigit) || (title.Count > 0 && x.X - title[^1].Right > 0.6 * pitch))
                    break;
                if (panel is { } bar && x.X > bar.Right)
                    break;
                title.Add(x);
            }
            titled.UnionWith(title);
            var text = string.Join(" ", title.Select(t => t.Text));
            notes.Add($"window \"{text}\" at ({word.X},{word.Y}): magnifier {(magnifier ? "read" : "not read")}, close {(close is null ? "not read" : "read")}, {word.H} px high" +
                      (panel is { } p ? $", panel {Note(p)}" : ", no panel"));
            if (panel is { } outline)
            {
                panels.Add(outline);
                used.UnionWith(groups.Where(g => g.IntersectsWith(outline)));
                windows.Add(new DeskPlace(DeskRegionKind.ContainerWindow, null, []) { Title = NoTitle(text) ? null : text, Outline = outline, Area = outline });
            }
            else
                windows.Add(Window(groups, used, start!.Value, NoTitle(text) ? null : text, word.Y, close?.Right, pitch));
        }
        // A window's grid has two-pixel lines (the stash's and, mostly, the GEAR tab's grids one): a grid the stash's finding
        // saw that has them and no title read is a window of no name. Not one inside a larger compartment on its lattice: the
        // game's highlight round items just looted thickens their lines (the raid screenshots of 2026-10-19: two columns of
        // the backpack read as a window of no name, its items counted in neither total). Not one of the middle column's
        // region grids, whose lines may be two pixels wide too (the POUCH's on the owner's screenshots of 2026-10-19).
        foreach (var lattice in lattices)
        {
            if (!used.Any(u => u.IntersectsWith(lattice)) && !panels.Any(p => p.IntersectsWith(lattice)) && !columnLattices.Contains(lattice) &&
                LineWidth(gray, lattice, pitch) >= 2 && !InsideCompartment(lattice, frames, pitch))
            {
                notes.Add($"window of no name at {Note(lattice)}");
                if (Panel(lines, gray, new SKRectI(lattice.Left - pitch / 6, lattice.Top - (int)(0.6 * pitch), lattice.Left - 2, lattice.Top - 2), pitch) is { } outline &&
                    Bar(gray, outline, pitch))
                {
                    panels.Add(outline);
                    used.UnionWith(groups.Where(g => g.IntersectsWith(outline)));
                    windows.Add(new DeskPlace(DeskRegionKind.ContainerWindow, null, []) { Outline = outline, Area = outline });
                }
                else
                    windows.Add(Window(groups, used, lattice, null, lattice.Top - (int)(1.1 * pitch), null, pitch));
            }
        }
        // The panels' compartments, found anew inside each outline, under the windows lying over it (a window whose bar
        // stands inside another's outline: picture 15, the CR MK2 (R) window opened over the Beta 2 window).
        for (var n = 0; n < windows.Count; n++)
        {
            if (windows[n].Outline is not { } outline)
                continue;
            var above = panels.Where(p => p != outline && outline.Contains(p.Left + 2, p.Top + 2)).ToList();
            var below = panels.Where(p => p != outline && p.Contains(outline.Left + 2, outline.Top + 2)).ToList();
            var inner = SKRectI.Inflate(outline, -2, -2);
            var hidden = above.Concat(over).ToList();
            var own = lattices.Where(l => SKRectI.Inflate(inner, Slack, Slack).Contains(l) && !hidden.Any(o => Overlapping(o, l))).ToList();
            // Moved onto their lines' middle as the page's frames are, but never onto the outline (brighter than the grid's
            // line six pixels in: picture 16, the Beta 2 window's grid moved onto it and read blank).
            var found = DeskFrames.Find(lines, pitch, inner, hidden, skip: (x, y) => above.Any(a => a.Contains(x, y)))
                .Select(f => StashGrids.Snap(gray, f.Left, f.Top, Math.Max(1, (int)Math.Round(f.Width / (double)pitch)), Math.Max(1, (int)Math.Round(f.Height / (double)pitch)),
                    pitch, inner.Left + 1, inner.Top + 1) is var (x, y) ? SKRectI.Create(x, y, f.Width, f.Height) : f)
                .Where(f => SKRectI.Inflate(inner, Slack, Slack).Contains(f) && !own.Any(l => SameCompartment(f, l, pitch) || Overlapping(f, l)))
                .ToList();
            var parts = own.Concat(found).OrderBy(f => f.Top).ThenBy(f => f.Left).ToList();
            notes.Add($"panel {Note(outline)}: {parts.Count} compartments" + (above.Count > 0 ? $", under {above.Count} window(s)" : ""));
            windows[n] = windows[n] with { Parts = parts, Area = SKRectI.Inflate(outline, 1, 1), Over = below.Select(b => SKRectI.Inflate(b, 1, 1)).ToList() };
        }
        return windows;
    }

    // How far a side may stand off its place, in pixels (as DeskFrames).
    private const int Slack = 3;

    // How tall a window's bar is, in cells, from the outline's top: the title stands within it (0.36 at 1440p: the outline
    // at 446, the grid's line at 480, 2026-10-25).
    private const double BarHeight = 0.42;

    // The levels of a panel's outline and of its inside: the outline 88–96 grey (gold, about 198, on the active window), a
    // tooltip's border 87; the panel's margin pure black (2026-10-25, 1440p).
    private const float PanelLight = 40;
    private const float PanelBlack = 2;

    /// <summary>
    /// A panel of the game's (a container's window, a tooltip; owner's second test, 2026-10-25): a thin light outline with
    /// pure black inside it for a few pixels, its top left corner within <paramref name="search"/>, nearest the search's
    /// bottom right first. Its right side and its bottom are where its top and left lines end; each corner must turn (the
    /// side going on from it) with black inside. Null when there is none.
    /// </summary>
    internal static SKRectI? Panel(StashLines lines, StashGray gray, SKRectI search, int pitch)
    {
        for (var y = Math.Min(search.Bottom, gray.H - 6); y >= Math.Max(0, search.Top); y--)
        {
            for (var x = Math.Min(search.Right, gray.W - 6); x >= Math.Max(0, search.Left); x--)
            {
                if (!Corner(gray, x, y, 1, 1) || lines.Vertical(x, y + 2, y + pitch / 2) < 0.9 || lines.Horizontal(y, x + 2, x + pitch) < 0.9)
                    continue;
                var right = End(gray, x, y, 1, 0);
                var bottom = End(gray, x, y, 0, 1);
                // Back from where the lines end to the corners that turn with black inside.
                while (right > x + pitch && !Corner(gray, right, y, -1, 1))
                    right--;
                while (bottom > y + pitch / 2 && !Corner(gray, x, bottom, 1, -1))
                    bottom--;
                if (right - x < 2 * pitch || bottom - y < pitch / 2)
                    continue;
                // The far sides by their corners (half a cell of each: another window may lie over the rest, picture 15's CR
                // MK2 (R) window over the Beta 2 window's right side).
                if (lines.Vertical(right, y + 2, Math.Min(bottom, y + pitch / 2) - 2) < 0.8 || lines.Horizontal(bottom, x + 2, Math.Min(right, x + pitch / 2) - 2) < 0.8)
                    continue;
                return new SKRectI(x, y, right + 1, bottom + 1);
            }
        }
        return null;
    }

    // A panel's corner at (x, y), its sides going (dx, dy): light there and a few pixels along both sides, black inside.
    private static bool Corner(StashGray g, int x, int y, int dx, int dy)
    {
        if (x - 4 < 0 || y - 4 < 0 || x + 4 >= g.W || y + 4 >= g.H)
            return false;
        return g[x, y] >= PanelLight && g[x + 3 * dx, y] >= PanelLight && g[x, y + 3 * dy] >= PanelLight &&
               g[x + dx, y + dy] <= PanelBlack && g[x + 2 * dx, y + 2 * dy] <= PanelBlack && g[x + 3 * dx, y + dy] <= PanelBlack && g[x + dx, y + 3 * dy] <= PanelBlack;
    }

    // Where a panel's side, from its corner, ends: the last light pixel along (dx, dy), gaps of two pixels bridged (another
    // window's corner over it).
    private static int End(StashGray g, int x, int y, int dx, int dy)
    {
        int at = dx != 0 ? x : y, limit = dx != 0 ? g.W - 1 : g.H - 1, last = at, gap = 0;
        while (at < limit && gap <= 2)
        {
            at++;
            var v = dx != 0 ? g[at, y] : g[x, at];
            if (v >= PanelLight)
            {
                last = at;
                gap = 0;
            }
            else
                gap++;
        }
        return last;
    }

    // Whether the panel's top band is a window's bar (the game's grey, about 26) and not a tooltip's black: left of the
    // magnifier, under the outline's margin.
    private static bool Bar(StashGray g, SKRectI panel, int pitch)
    {
        double sum = 0;
        var n = 0;
        for (var y = panel.Top + 5; y <= panel.Top + Math.Min(15, (int)(BarHeight * pitch) - 6) && y < g.H; y++)
        {
            for (var x = panel.Left + 4; x <= panel.Left + 8 && x < g.W; x++)
            {
                sum += g[x, y];
                n++;
            }
        }
        return n > 0 && sum / n is >= 12 and <= 60;
    }

    /// <summary>
    /// The game's tooltips (owner's second test, 2026-10-25, picture 14: the pointer over the CR MK2 (R) in the Beta 2 window, its
    /// full name "Stich Profi Chest Rig MK2 (Recon, A-TACS FG)" in a black box with a thin grey border over the rig and the
    /// stash pane's first column): a panel round a line of words whose inside is black (a window's bar is grey). What it
    /// covers isn't read, as a toast's isn't.
    /// </summary>
    internal static List<SKRectI> Tooltips(IReadOnlyList<DeskWord> words, StashGray gray, StashLines lines, int pitch)
    {
        var found = new List<SKRectI>();
        foreach (var line in words.GroupBy(x => x.Line))
        {
            var first = line.OrderBy(x => x.X).First();
            if (found.Any(f => f.Contains(first.MidX, first.MidY)) || Black(gray, first.X - 3, first.MidY) is false)
                continue;
            if (Panel(lines, gray, new SKRectI(first.X - (int)(0.6 * pitch), first.Y - (int)(0.4 * pitch), first.X - 2, first.Y - 2), pitch) is { } box &&
                !Bar(gray, box, pitch) && Black(gray, box.Left + 4, box.Top + 4) == true && box.Contains(first.MidX, first.MidY))
                found.Add(box);
        }
        return found;
    }

    // Whether a pixel is the panels' pure black (null off the picture).
    private static bool? Black(StashGray g, int x, int y) => x < 0 || y < 0 || x >= g.W || y >= g.H ? null : g[x, y] <= PanelBlack;

    // Whether a lattice lies inside a larger compartment and on its lattice: some of its cells seen as a grid of their own.
    private static bool InsideCompartment(SKRectI lattice, IReadOnlyList<SKRectI> frames, int pitch) =>
        frames.Any(f => SKRectI.Inflate(f, pitch / 4, pitch / 4).Contains(lattice) && (long)f.Width * f.Height >= (long)lattice.Width * lattice.Height + pitch * pitch &&
                        OnLattice(f.Left - lattice.Left, pitch) && OnLattice(f.Top - lattice.Top, pitch));

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
                // One compartment found twice is taken once (its cells would count twice).
                if (parts.Any(p => Overlapping(p, f)))
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
