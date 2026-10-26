using Shturmap.Core.Desk;
using Shturmap.Core.Screenshots;

namespace Shturmap.Game.Screenshots;

/// <summary>An item of the catalogue the stash desk may name: its id, the short name the game prints on its cell, its full
/// name (a container window's title may be either), its footprint in cells, what it holds out of sight, and the slots it
/// goes in (<see cref="DeskSlotKind.Any"/> when that isn't known).</summary>
public sealed record DeskCandidate(string ItemId, string ShortName, string Name, int W, int H, DeskHolds Holds = DeskHolds.Nothing,
    DeskSlotKind Slots = DeskSlotKind.Any);

/// <summary>
/// A label matched to the whole catalogue (owner, 2026-10-19: the desk reads against every item; docs/DESIGN.md §2, "For
/// the stash desk"): the items whose short name fits the label best, when that fit is at least <see cref="Enough"/>.
/// The measurement of 2026-10-19 (docs/NEXT.md) found the label alone safe for a price at 0.9, not at the quest path's
/// 0.8: four labels read a letter off named another item there, up to 500 times off in price. The folded names are made
/// once per catalogue.
/// </summary>
internal sealed class DeskMatch
{
    /// <summary>How well a label must fit a short name for the desk: one letter in ten may be off.</summary>
    public const double Enough = 0.9;

    // The least letters a label cut by a quick slot's number must keep for its end to name anything.
    private const int LeastEnd = 4;

    // The fit of a name a label cut at the cell's edge begins but for its last letter: enough, below a name it begins whole.
    private const double CutLetterFit = 0.95;

    private sealed record Entry(DeskCandidate Candidate, string Folded, string Digits, bool HasDigits, string FoldedName);

    private readonly List<Entry> _entries;

    // The guns by their folded short name, for a label that is a weapon preset's name (Preset).
    private readonly ILookup<string, DeskCandidate> _guns;

    public DeskMatch(IEnumerable<DeskCandidate> candidates)
    {
        _entries = candidates.Where(c => c.ShortName.Length > 0)
            .Select(c =>
            {
                var folded = ExitList.Fold(c.ShortName);
                return new Entry(c, folded, new string(folded.Where(char.IsDigit).ToArray()), c.ShortName.Any(char.IsDigit), ExitList.Fold(c.Name));
            })
            .Where(e => e.Folded.Length > 0)
            .ToList();
        _guns = _entries.Where(e => e.Candidate.Holds == DeskHolds.Mods).ToLookup(e => e.Folded, e => e.Candidate, StringComparer.Ordinal);
    }

    public int Count => _entries.Count;

    /// <summary>
    /// How well the label fits the best names, and the items of those names (the stash reader's fit, <see cref="StashLabels.Fit"/>:
    /// folded letters, one in ten off at 0.9, digits that must agree). The game cuts a label at the cell's edge only when it
    /// doesn't fit the cell, so a label is taken for a name's beginning only when it is about as long as a label of a cell
    /// this wide can be (<paramref name="cells"/>, the cell's width): "Weapons" on a case five cells wide is the Weapons
    /// case, not one of the 49 sealed weapon cases it starts. A label the game's quick-slot number cuts at its start
    /// ("6 splint" for "Alu splint") is its name's end: the items whose short name ends so are all among them, never named
    /// by the end alone when another item's name ends alike; so is any label of a cell whose quick slot's number the
    /// reading saw (<paramref name="cutAtStart"/>: the recognition left the number out, "splint" for "Alu splint").
    /// A slot's label is matched against the items its slot takes (<paramref name="kind"/>; QA pass of 2026-10-19, F4).
    /// </summary>
    public (double Fit, List<DeskCandidate> Best) Match(string label, int cells = 1, DeskSlotKind kind = DeskSlotKind.Any, bool cutAtStart = false)
    {
        label = label.Replace("!", "").Replace("|", "").Trim();
        var cut = QuickSlotCut(label) ?? (cutAtStart ? label : null);
        if (cut is not null)
            label = cut;
        var a = ExitList.Fold(label);
        if (a.Length == 0)
            return (0, []);
        var (top, best) = Folded(a, cells, kind, cut is not null);
        if (top >= Enough)
            return (top, best);
        // A digit and a letter that look alike, one swapped (owner's second test, 2026-10-25: the recognition reads the
        // game's 6 as a G now and then, "GL31" for the 6L31 magazine; the fold already makes O, Q and D a 0, l and I a 1):
        // only where the label as read fits nothing, and the swapped label must fit as well as any label must.
        for (var n = 0; n < a.Length; n++)
        {
            if (Lookalike(a[n]) is not { } other)
                continue;
            var (swapFit, swapBest) = Folded(string.Concat(a.AsSpan(0, n), [other], a.AsSpan(n + 1)), cells, kind, cut is not null);
            if (swapFit >= Enough && swapFit > top + 1e-9)
                (top, best) = (swapFit, swapBest);
        }
        return (top, best);
    }

    // The digit a letter looks like, or the letter a digit does, in folded labels (lower case): 6 and g, 5 and s, 8 and
    // b, 2 and z.
    private static char? Lookalike(char c) => c switch
    {
        'g' => '6',
        '6' => 'g',
        's' => '5',
        '5' => 's',
        'b' => '8',
        '8' => 'b',
        'z' => '2',
        '2' => 'z',
        _ => null,
    };

    // The best names for a folded label (Match).
    private (double Fit, List<DeskCandidate> Best) Folded(string a, int cells, DeskSlotKind kind, bool cut)
    {
        var digits = new string(a.Where(char.IsDigit).ToArray());
        // A cell holds about seven folded letters a cell of its width at the label's size: shorter labels aren't cut.
        var mayBeCut = a.Length >= Math.Max(6, 6 * cells);
        double top = 0;
        var best = new List<DeskCandidate>();
        foreach (var e in _entries)
        {
            if (kind != DeskSlotKind.Any && (e.Candidate.Slots & kind) == 0)
                continue;
            var b = e.Folded;
            double fit;
            // How much of the name's beginning a label cut at the cell's right edge covers (0: not taken so).
            var prefix = 0;
            if (cut && a.Length >= LeastEnd && b.Length > a.Length && b.EndsWith(a, StringComparison.Ordinal))
                fit = 1;
            else if (mayBeCut && b.Length > a.Length && b.StartsWith(a, StringComparison.Ordinal))
            {
                fit = 1;
                prefix = a.Length;
            }
            // The game cuts a long label at the cell's right edge, and its last letter there may read as another (owner's
            // second test, 2026-10-25: "Marksme" for "Marksman 3x30", a cell one wide): seven folded letters or more
            // whose all but the last start a longer name; a name the label begins whole outranks it.
            else if (mayBeCut && a.Length >= 7 && b.Length > a.Length && b.StartsWith(a[..^1], StringComparison.Ordinal))
            {
                fit = CutLetterFit;
                prefix = a.Length - 1;
            }
            else
            {
                // Levenshtein can't do better than the lengths' difference allows: skip what can't reach the best so far.
                var bound = 1 - (double)Math.Abs(a.Length - b.Length) / Math.Max(a.Length, b.Length);
                if (bound < top - 1e-9 && !(mayBeCut && a.Length <= b.Length))
                    continue;
                fit = a == b ? 1 : StashLabels.Similarity(a, b);
                if (mayBeCut && a.Length <= b.Length)
                {
                    var same = 0;
                    while (same < a.Length && a[same] == b[same])
                        same++;
                    if (same >= 6)
                        fit = Math.Max(fit, (double)same / a.Length);
                }
            }
            // A name with digits must agree in them; a label cut before the name's digits has none of them to agree in
            // (owner's second test, 2026-10-25), but one with digits must have them all, as before ("Dorm 10" is no
            // beginning of "Dorm 104").
            var cutBeforeDigits = prefix > 0 && digits.Length == 0 && !b.AsSpan(0, prefix).ContainsAny("0123456789");
            if (e.HasDigits && digits != e.Digits && !cutBeforeDigits)
                fit = Math.Min(fit, 0.5);
            if (fit > top + 1e-9)
            {
                top = fit;
                best.Clear();
                best.Add(e.Candidate);
            }
            else if (Math.Abs(fit - top) <= 1e-9 && fit > 0)
                best.Add(e.Candidate);
        }
        return (top, best);
    }

    /// <summary>
    /// The guns a label names that fits no name as read (<see cref="Match"/> below <see cref="Enough"/>) but begins with a
    /// gun's whole short name, on a word boundary, and goes on with more words (owner, 2026-10-26): a weapon preset's name,
    /// which the catalogue leaves out ("USP .45 Expert" in the holster, the gun's short name "USP .45"). The cell is the gun,
    /// its figure the bare gun's with the "+" for its mods unseen, as a gun's cell has. Only guns, the items that hold mods:
    /// "RIP ammo pack" stays what it is. Where several guns' short names begin the label, the longest's; several guns of
    /// that one name are "one of N", never a guess. None for a label whose start a quick slot's number may have taken
    /// (<paramref name="cutAtStart"/>), and none where the slot takes no gun (<paramref name="kind"/>). None either for a
    /// label <see cref="NearMiss"/> near the name of an item that isn't a gun and begins with the gun's name: that item
    /// misread ("AK-308 DG" for the AK-308 DC muzzle brake, "TT SX" for the TT SK rig) as much as a preset's name.
    /// </summary>
    public List<DeskCandidate> Preset(string label, DeskSlotKind kind = DeskSlotKind.Any, bool cutAtStart = false)
    {
        if (cutAtStart)
            return [];
        label = label.Replace("!", "").Replace("|", "");
        var words = label.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // The label's beginnings of whole words, the longest first, with a word that holds a letter or a digit after it.
        for (var k = words.Length - 1; k >= 1; k--)
        {
            if (ExitList.Fold(string.Concat(words[k..])).Length == 0)
                continue;
            var name = ExitList.Fold(string.Concat(words[..k]));
            var guns = _guns[name].Where(c => kind == DeskSlotKind.Any || (c.Slots & kind) != 0).ToList();
            if (guns.Count == 0)
                continue;
            // Another item's name of that beginning, a letter off the label (its digits the same, as a match wants them).
            var a = ExitList.Fold(label);
            var digits = new string(a.Where(char.IsDigit).ToArray());
            var misread = _entries.Any(e => e.Candidate.Holds != DeskHolds.Mods && e.Folded.Length > name.Length && e.Folded.StartsWith(name, StringComparison.Ordinal) &&
                                            !(e.HasDigits && digits != e.Digits) && StashLabels.Similarity(a, e.Folded) >= NearMiss);
            return misread ? [] : guns;
        }
        return [];
    }

    /// <summary>How near a label that begins with a gun's name may come to another item's name of that beginning and still
    /// be taken for a preset's name: one folded letter in four off is that name misread (<see cref="Preset"/>). Of
    /// tarkov.dev's 400 presets whose names begin with a gun's (2026-10-26), 5 come so near another item's name, not
    /// whole ("UZI SD" and the "UZI SW" part, "G36 K" and "G36 KV"), and stay unread.</summary>
    public const double NearMiss = 0.75;

    /// <summary>The items a container window's title names: its short name or its full name fits the title at least as
    /// well as <see cref="Enough"/>, and it holds a grid.</summary>
    public List<DeskCandidate> Titled(string title)
    {
        var a = ExitList.Fold(title);
        if (a.Length == 0)
            return [];
        return _entries.Where(e => e.Candidate.Holds == DeskHolds.Contents &&
                                   (StashLabels.Fit(title, e.Candidate.ShortName) >= Enough || (e.FoldedName.Length > 0 && StashLabels.Fit(title, e.Candidate.Name) >= Enough)))
            .Select(e => e.Candidate).ToList();
    }

    // A label that starts with a lone digit, the quick slot's number the game draws over a pocket's or a rig's item at
    // its top left: the rest, which is the name's end.
    internal static string? QuickSlotCut(string label)
    {
        var words = label.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length >= 2 && words[0].Length == 1 && char.IsDigit(words[0][0]) ? string.Join(' ', words[1..]) : null;
    }
}
