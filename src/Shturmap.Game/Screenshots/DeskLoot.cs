using Shturmap.Core.Desk;
using Shturmap.Core.Screenshots;
using SkiaSharp;

namespace Shturmap.Game.Screenshots;

/// <summary>The LOOT pane of a raid's inventory screen as the layout found it: the container's kind as the game names it
/// beside BACK ("BODY", "DRAWER", "SAFE"), the pane, a body's slots under their headings, and the parts: a body's TACTICAL
/// RIG, POCKETS and BACKPACK (each with its search state), or a container's one grid.</summary>
internal sealed record DeskLootFound(string? Kind, SKRectI Pane, IReadOnlyList<DeskSlotBox> Slots, IReadOnlyList<DeskPlace> Parts)
{
    /// <summary>The LOOT regions' title: the game's words, "LOOT · BODY".</summary>
    public string Title => Kind is { Length: > 0 } kind ? "LOOT · " + kind : "LOOT";
}

/// <summary>
/// The raid's inventory screen (the in-raid glance, owner, 2026-10-19; docs/DESIGN.md §2, "For the stash desk", and §4,
/// *The in-raid glance*; 22 screenshots of one raid, 2026-10-19, 2560×1440, PvE): the GEAR tab's layout without the menus'
/// bar at the foot (BUILDS, HANDBOOK, MESSENGER) and without the stash pane (its money line, SEARCH and SORT TABLE); QUICK USE
/// stays. Looting, the right third is the LOOT pane: "LOOT" at its head, the container's kind at the head's right ("BODY",
/// "SAFE"). A container's pane is one grid (the game's two-pixel lines, as a window's). A body's is the dead player's slots
/// under the loadout's headings (armour with its print, weapons, whose "?/30" says nothing), then TACTICAL RIG and BACKPACK,
/// each a slot with the container and its grid beside it, and POCKETS: a part not searched yet shows "UNSEARCHED" where its
/// grid stands (with a SEARCH link at its heading), one being searched "SEARCHING ✕ 0:00" at its heading and the items
/// found so far. The pane scrolls on its own: a part whose heading scrolled away above is not read.
/// </summary>
internal static class DeskLoot
{
    private static readonly (DeskRegionKind Kind, string Heading)[] Parts =
    [
        (DeskRegionKind.LootRig, "TACTICAL RIG"), (DeskRegionKind.LootPockets, "POCKETS"), (DeskRegionKind.LootBackpack, "BACKPACK"),
    ];

    // The menus' bar at the screen's foot: a raid's inventory has none (its QUICK USE bar stands there instead).
    private static readonly string[] MenuBarWords = ["HANDBOOK", "MESSENGER", "BUILDS", "EXPANSIONS"];

    /// <summary>Whether the picture shows the menus' bar at its foot (BUILDS, HANDBOOK, MESSENGER, EXPANSIONS).</summary>
    public static bool MenuBar(IReadOnlyList<DeskWord> words, int h)
    {
        var folded = MenuBarWords.Select(ExitList.Fold).ToHashSet(StringComparer.Ordinal);
        return words.Any(x => x.Y > h * 0.9 && folded.Contains(ExitList.Fold(x.Text)));
    }

    /// <summary>Whether a word is the pane's own over a part's grid ("SEARCHING", its SEARCH link), not a window's title.</summary>
    public static bool PaneWord(string text)
    {
        var folded = ExitList.Fold(text);
        return folded.StartsWith(ExitList.Fold("SEARCH"), StringComparison.Ordinal) || folded.Contains(ExitList.Fold("UNSEARCHED"), StringComparison.Ordinal);
    }

    /// <summary>The LOOT pane's head word: "LOOT" at the top of the picture's right third, or null.</summary>
    public static DeskWord? Head(IReadOnlyList<DeskWord> words, int w, int h) =>
        words.Where(x => x.Y < h * 0.08 && x.X > w * 0.6 && x.X < w * 0.85 && ExitList.Fold(x.Text) == ExitList.Fold("LOOT") && !x.Text.Any(char.IsLower))
            .OrderBy(x => x.Y).FirstOrDefault();

    /// <summary>
    /// The container's kind as the game names it, its words in order: the capitals in the head's row that end at the
    /// screen's right edge (BACK stands above them), the phrase as far left as its words stand a space apart (the owner's
    /// picture of 2026-10-27 of an airdrop: "AIRDROP COMMON SUPPLY CRATE" starts left of the right tenth, which took "SUPPLY
    /// CRATE" alone, and its first word was made a window's title). A slot's label in the pane's first row, level with the
    /// head further left, stands apart.
    /// </summary>
    public static List<DeskWord> KindWords(IReadOnlyList<DeskWord> words, DeskWord head, int w, int pitch)
    {
        var row = words.Where(x => x.X > head.Right && Math.Abs(x.MidY - head.MidY) < 0.4 * pitch && x.Text.Any(char.IsLetter) && !x.Text.Any(char.IsLower))
            .OrderByDescending(x => x.X).ToList();
        var kind = new List<DeskWord>();
        foreach (var x in row)
        {
            if (kind.Count == 0 ? x.X <= w * 0.9 : kind[^1].X - x.Right > 0.6 * pitch)
                break;
            kind.Add(x);
        }
        kind.Reverse();
        return kind;
    }

    /// <summary>
    /// The pane's parts. <paramref name="frames"/> are the framed compartments of the whole picture and
    /// <paramref name="lattices"/> the windows' lattices the grid finding saw (a container's grid among them); both are
    /// taken where they lie in the pane.
    /// </summary>
    public static DeskLootFound Find(IReadOnlyList<DeskWord> words, DeskWord head, IReadOnlyList<SKRectI> frames, IReadOnlyList<SKRectI> lattices,
        int w, int bottom, int pitch)
    {
        var k = pitch / 84.0;
        var pane = new SKRectI(head.X - pitch, head.Bottom, w, bottom);
        var kindWords = KindWords(words, head, w, pitch);
        var kind = kindWords.Count > 0 ? string.Join(" ", kindWords.Select(x => x.Text)) : null;
        bool InPane(DeskWord x) => pane.Contains(x.MidX, x.MidY);
        bool Within(SKRectI r) => r.Left >= pane.Left - 4 && r.Top >= pane.Top - 4 && r.Right <= pane.Right + 4;
        var compartments = lattices.Where(Within).Concat(frames.Where(f => Within(f) && !lattices.Any(l => DeskLayout.Overlapping(l, f)))).ToList();

        var slotHeads = DeskLayout.Headings(words, DeskSlots.Loadout.Select(s => s.Heading)).Where(x => InPane(x.Word))
            .GroupBy(x => x.Heading).Select(g => g.OrderBy(x => x.Word.Y).First()).ToList();
        var partHeads = DeskLayout.Headings(words, Parts.Select(p => p.Heading)).Where(x => InPane(x.Word))
            .GroupBy(x => x.Heading).Select(g => g.OrderBy(x => x.Word.Y).First()).ToList();
        var body = slotHeads.Count + partHeads.Count > 0;

        var searching = words.Where(x => InPane(x) && ExitList.Fold(x.Text).StartsWith(ExitList.Fold("SEARCHING"), StringComparison.Ordinal)).ToList();
        var unsearched = words.Where(x => InPane(x) && ExitList.Fold(x.Text).Contains(ExitList.Fold("UNSEARCHED"), StringComparison.Ordinal)).ToList();

        if (!body)
        {
            // A container: its grid, as far as the pane shows it. Its frames on one lattice are that one grid (a safe in the
            // raid screenshots of 2026-10-19: its Clock's frame and the hover's around the Horse stood out, the frame round
            // the whole didn't, and the USD in the corner between them fell in neither).
            var state = searching.Count > 0 ? DeskSearch.Searching : unsearched.Count > 0 ? DeskSearch.Unsearched : DeskSearch.Searched;
            // A frame over a lattice on its lines is more of that one grid, not the lattice seen twice: the lattice holds
            // only the cells framed all round, the frames the items' rows too (the owner's weapon box of 2026-10-26: a 4×5
            // lattice of its empty cells, its rifles' rows framed beside and above it, the grid 6×6).
            compartments.AddRange(frames.Where(f => Within(f) && lattices.Any(l => Within(l) && DeskLayout.Overlapping(l, f) && OnLattice(l, f, pitch))));
            // The grid's rows stand on the head's: its top line 10 px (at 1440p) under the head. A frame whose top was found
            // on the head's edge a few pixels above it (the owner's airdrop of 2026-10-27: 5 px, its grid faint) is on the
            // grid's rows too, so the grid's frames make one grid.
            var gridTop = pane.Top + (int)Math.Round(10 * k);
            compartments = compartments.Select(c => Math.Abs(c.Top - gridTop) is var off && off > 4 && off <= pitch / 8 && c.Top < gridTop
                ? new SKRectI(c.Left, gridTop, c.Right, c.Bottom) : c).ToList();
            compartments = OneGrid(compartments, pitch);
            // Its grid starts right under the head; a first row whose items hid the grid's top line (a weapon box of the
            // same screenshots: a rifle part's red ground) left the frame found a row lower.
            if (compartments.OrderBy(c => c.Top).FirstOrDefault() is { IsEmpty: false } top && top.Top - pane.Top >= 0.75 * pitch)
            {
                var rows = (int)Math.Round((top.Top - pane.Top - 10 * k) / pitch);
                compartments[compartments.IndexOf(top)] = new SKRectI(top.Left, top.Top - rows * pitch, top.Right, top.Bottom);
            }
            var place = new DeskPlace(DeskRegionKind.Loot, null, compartments)
            {
                Search = state,
                Cut = compartments.Any(c => c.Bottom >= pane.Bottom - pitch / 4),
            };
            return new DeskLootFound(kind, pane, [], [place]);
        }

        // A body's slots: a box under each heading, as the loadout's (DeskLayout.SlotSize).
        var slots = slotHeads.Select(x =>
        {
            var key = DeskSlots.Loadout.First(s => s.Heading == x.Heading).Key;
            var (sw, sh) = DeskLayout.SlotSize(key);
            return new DeskSlotBox(key, SKRectI.Create(x.Word.X - (int)Math.Round(5 * k), x.Word.Bottom + (int)Math.Round(4 * k), (int)Math.Round(sw * pitch),
                (int)Math.Round(sh * pitch)));
        }).ToList();

        // Its parts: each from its heading down to the next heading below it (or the pane's foot).
        var heads = slotHeads.Concat(partHeads).Select(x => x.Word).ToList();
        var parts = new List<DeskPlace>();
        foreach (var (heading, word) in partHeads.OrderBy(x => x.Word.Y))
        {
            var partKind = Parts.First(p => p.Heading == heading).Kind;
            var next = heads.Where(x => x.Y > word.Y + pitch / 2).Select(x => x.Y).DefaultIfEmpty(pane.Bottom).Min();
            SKRectI? slot = partKind == DeskRegionKind.LootPockets ? null
                : SKRectI.Create(word.X - (int)Math.Round(5 * k), word.Bottom + (int)Math.Round(4 * k), 2 * pitch, (int)Math.Round(2.05 * pitch));
            var left = slot is { } s ? s.Right - pitch / 3 : pane.Left;
            var own = compartments.Where(c => c.Top >= word.Bottom - 4 && c.Top < next && c.Left >= left).ToList();
            var state = searching.Any(x => Math.Abs(x.MidY - word.MidY) < 0.45 * pitch && x.X > word.Right) ? DeskSearch.Searching
                : unsearched.Any(x => x.Y > word.Bottom && x.Y < next) ? DeskSearch.Unsearched
                : DeskSearch.Searched;
            parts.Add(new DeskPlace(partKind, slot, own) { Search = state, Cut = own.Any(c => c.Bottom >= pane.Bottom - pitch / 4) });
        }
        return new DeskLootFound(kind ?? "BODY", pane, slots, parts);
    }

    // Whether an offset is whole cells, within a few pixels.
    private static bool Whole(int d, int pitch)
    {
        var phase = ((d % pitch) + pitch) % pitch;
        return phase <= 4 || phase >= pitch - 4;
    }

    // Whether two rectangles stand on one lattice: their corners whole cells apart.
    private static bool OnLattice(SKRectI a, SKRectI b, int pitch) => Whole(a.Left - b.Left, pitch) && Whole(a.Top - b.Top, pitch);

    // Rectangles on one lattice (their corners whole cells apart, within a few pixels) that touch or overlap, as the one
    // rectangle round them.
    internal static List<SKRectI> OneGrid(List<SKRectI> rects, int pitch)
    {
        var groups = rects.ToList();
        var merged = true;
        while (merged)
        {
            merged = false;
            for (var a = 0; a < groups.Count && !merged; a++)
            {
                for (var b = a + 1; b < groups.Count && !merged; b++)
                {
                    var (x, y) = (groups[a], groups[b]);
                    if (!Whole(x.Left - y.Left, pitch) || !Whole(x.Top - y.Top, pitch) || !SKRectI.Inflate(x, pitch / 3, pitch / 3).IntersectsWith(y))
                        continue;
                    groups[a] = SKRectI.Union(x, y);
                    groups.RemoveAt(b);
                    merged = true;
                }
            }
        }
        return groups;
    }
}
