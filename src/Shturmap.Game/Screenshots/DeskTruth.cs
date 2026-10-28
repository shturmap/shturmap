using Shturmap.Core.Desk;

namespace Shturmap.Game.Screenshots;

/// <summary>One item of a truth by the page's regions: "backpack 1,0 Triton" (a grid's top left cell) or "loadout
/// [earpiece] ComTac II" (a slot), and for counts and marks the same place.</summary>
public sealed record DeskTruthPlace(string Region, string? Slot, int I, int J)
{
    public override string ToString() => Slot is not null ? $"{Region} [{Slot}]" : $"{Region} {I},{J}";

    internal static DeskTruthPlace? Parse(string region, string place)
    {
        if (place.StartsWith('['))
            return new DeskTruthPlace(region, place.Trim('[', ']'), 0, 0);
        var at = place.Split(',');
        return at.Length == 2 && int.TryParse(at[0], out var i) && int.TryParse(at[1], out var j) ? new DeskTruthPlace(region, null, i, j) : null;
    }

    /// <summary>The cell the page has there: a slot by its key, a grid's cell by the cell that takes the top left; null
    /// where none stands.</summary>
    public DeskCell? On(DeskPage? page)
    {
        var region = page?.Region(Region);
        return Slot is not null
            ? region?.Cells.FirstOrDefault(c => c.Slot == Slot)
            : region?.Cells.FirstOrDefault(c => !c.IsSlot && c.Rect.Contains(new DeskRect(I, J, 1, 1)));
    }
}

/// <summary>How a reading fared against a picture's truth (<see cref="DeskTruth"/>); added up over pictures.</summary>
public sealed record DeskScore
{
    public int Pictures { get; init; }

    // The items: named right, wrong, "one of N" with the truth among them or not, nothing said, no cell there.
    public int Items { get; init; }
    public int Right { get; init; }
    public int Wrong { get; init; }
    public int OneOf { get; init; }
    public int NotAmong { get; init; }
    public int Silent { get; init; }
    public int NoCell { get; init; }

    // The regions: of their size, missing, of another size, made up; screens told wrong.
    public int Regions { get; init; }
    public int RegionsRight { get; init; }
    public int RegionsMissing { get; init; }
    public int RegionsOtherSize { get; init; }
    public int RegionsMadeUp { get; init; }
    public int ScreensWrong { get; init; }

    // The counts and conditions printed ("45/45", "6911"): right, wrong, unread (no cell, or none read); a count read where
    // nothing is printed (made up); the game's "?/20" and "~Full" not scored.
    public int Counts { get; init; }
    public int CountsRight { get; init; }
    public int CountsWrong { get; init; }
    public int CountsUnread { get; init; }
    public int CountsMadeUp { get; init; }

    // The found-in-raid mark: right, wrong, not looked for (the reader said nothing of it: unsure, its corner not judged, or
    // a slot's until 2026-10-28), no cell; a mark out of view ("?") not scored.
    public int Marks { get; init; }
    public int MarksRight { get; init; }
    public int MarksWrong { get; init; }
    public int MarksNotLooked { get; init; }
    public int MarksNoCell { get; init; }

    /// <summary>What went wrong, a line each ("WRONG stash 4,6: read AK-104, truth Propital").</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];

    public static DeskScore operator +(DeskScore a, DeskScore b) => new()
    {
        Pictures = a.Pictures + b.Pictures,
        Items = a.Items + b.Items, Right = a.Right + b.Right, Wrong = a.Wrong + b.Wrong, OneOf = a.OneOf + b.OneOf, NotAmong = a.NotAmong + b.NotAmong,
        Silent = a.Silent + b.Silent, NoCell = a.NoCell + b.NoCell,
        Regions = a.Regions + b.Regions, RegionsRight = a.RegionsRight + b.RegionsRight, RegionsMissing = a.RegionsMissing + b.RegionsMissing,
        RegionsOtherSize = a.RegionsOtherSize + b.RegionsOtherSize, RegionsMadeUp = a.RegionsMadeUp + b.RegionsMadeUp, ScreensWrong = a.ScreensWrong + b.ScreensWrong,
        Counts = a.Counts + b.Counts, CountsRight = a.CountsRight + b.CountsRight, CountsWrong = a.CountsWrong + b.CountsWrong,
        CountsUnread = a.CountsUnread + b.CountsUnread, CountsMadeUp = a.CountsMadeUp + b.CountsMadeUp,
        Marks = a.Marks + b.Marks, MarksRight = a.MarksRight + b.MarksRight, MarksWrong = a.MarksWrong + b.MarksWrong,
        MarksNotLooked = a.MarksNotLooked + b.MarksNotLooked, MarksNoCell = a.MarksNoCell + b.MarksNoCell,
        Notes = [.. a.Notes, .. b.Notes],
    };

    public string ItemsLine => $"{Items} items: right {Right}, wrong {Wrong}, one of N {OneOf} + {NotAmong} not among them, nothing said {Silent}, no cell read {NoCell}";

    public string RegionsLine => $"{Regions} regions: right {RegionsRight}, missing {RegionsMissing}, of another size {RegionsOtherSize}, made up {RegionsMadeUp}; screens told wrong {ScreensWrong}";

    public string CountsLine => $"{Counts} counts: right {CountsRight}, wrong {CountsWrong}, unread {CountsUnread}; made up where none is printed {CountsMadeUp}";

    public string MarksLine => $"{Marks} found-in-raid marks: right {MarksRight}, wrong {MarksWrong}, not looked for {MarksNotLooked}, no cell {MarksNoCell}";
}

/// <summary>
/// For measuring the desk's and the glance's reading (shturmap-cli desk, DeskRealSetTests): the hand-written answers
/// beside a picture. <c>truth-&lt;picture&gt;.txt</c>, every item whose label shows by the page's regions ("backpack 1,0
/// Triton", "loadout [earpiece] ComTac II": the short name, or the full name where items share the short name and the
/// picture tells which); <c>regions-&lt;picture&gt;.txt</c>, "screen Gear" and each region the reading should make with the
/// size of its grid in view ("backpack 5x6", "loadout", "loot:backpack 0x0"); <c>extra-&lt;picture&gt;.txt</c>, each
/// item's count or condition as printed and its found-in-raid mark ("stash 0,5 | 45/45 | fir yes": "-" nothing printed,
/// "?/20" or "~Full" the game's own, not scored; "fir ?" the mark's corner out of view). Lines starting with # are notes;
/// a file not there scores nothing. Scored as the throwaway script of the assessment of 2026-10-27 did, the marks the
/// reader said nothing of said apart from wrong ones.
/// </summary>
public sealed class DeskTruth
{
    public string? Screen { get; private init; }
    public IReadOnlyList<(string Key, string Size)> Regions { get; private init; } = [];
    public IReadOnlyList<(DeskTruthPlace Place, string Name)> Items { get; private init; } = [];
    public IReadOnlyList<(DeskTruthPlace Place, string Count, bool? Mark)> Extras { get; private init; } = [];

    /// <summary>The answers beside a picture, or null where it has none.</summary>
    public static DeskTruth? Beside(string picture)
    {
        string Path(string kind) => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(picture)!, $"{kind}-{System.IO.Path.GetFileNameWithoutExtension(picture)}.txt");
        string truth = Path("truth"), regions = Path("regions"), extra = Path("extra");
        if (!File.Exists(truth) && !File.Exists(regions) && !File.Exists(extra))
            return null;
        var items = new List<(DeskTruthPlace, string)>();
        foreach (var line in Lines(truth))
        {
            // "A 3,0 Marl 214", the stash spike's grids, is the CLI's alone.
            var parts = line.Split(' ', 3);
            if (parts.Length == 3 && !(parts[0].Length == 1 && char.IsAsciiLetterUpper(parts[0][0])) && DeskTruthPlace.Parse(parts[0], parts[1]) is { } place)
                items.Add((place, parts[2].Trim()));
        }
        string? screen = null;
        var sizes = new List<(string, string)>();
        foreach (var line in Lines(regions))
        {
            if (line.StartsWith("screen ", StringComparison.Ordinal))
                screen ??= line["screen ".Length..].Trim();
            else
            {
                var p = line.Split(' ', 2);
                sizes.Add((p[0], p.Length > 1 ? p[1].Trim() : ""));
            }
        }
        var extras = new List<(DeskTruthPlace, string, bool?)>();
        foreach (var line in Lines(extra))
        {
            var parts = line.Split('|').Select(p => p.Trim()).ToArray();
            if (parts.Length != 3)
                continue;
            var where = parts[0].Split(' ', 2);
            if (where.Length != 2 || DeskTruthPlace.Parse(where[0], where[1]) is not { } place)
                continue;
            var mark = parts[2].EndsWith("yes", StringComparison.Ordinal) ? true : parts[2].EndsWith("no", StringComparison.Ordinal) ? false : (bool?)null;
            extras.Add((place, parts[1], mark));
        }
        return new DeskTruth { Screen = File.Exists(regions) ? screen ?? "None" : null, Regions = sizes, Items = items, Extras = extras };
    }

    private static IEnumerable<string> Lines(string path) =>
        File.Exists(path) ? File.ReadAllLines(path).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')) : [];

    /// <summary>The reading's page and screen against the answers. <paramref name="name"/> and <paramref name="shortName"/>
    /// give an item's names by its id: a truth names an item by either.</summary>
    public DeskScore Score(DeskScreen screen, DeskPage? page, Func<string, string> name, Func<string, string> shortName)
    {
        var notes = new List<string>();
        int right = 0, wrong = 0, oneOf = 0, notAmong = 0, silent = 0, noCell = 0;
        foreach (var (place, truth) in Items)
        {
            var cell = place.On(page);
            var names = cell?.Ids.Select(id => name(id) == truth ? truth : shortName(id)).ToList() ?? [];
            if (cell is null)
            {
                noCell++;
                notes.Add($"NO CELL {place}: truth {truth}");
            }
            else if (names.Count == 0)
                silent++;
            else if (names.Count == 1 && names[0] == truth)
                right++;
            else if (names.Count == 1)
            {
                wrong++;
                notes.Add($"WRONG {place}: read {names[0]}, truth {truth}");
            }
            else if (names.Contains(truth))
                oneOf++;
            else
            {
                notAmong++;
                notes.Add($"NOT AMONG {place}: one of {names.Count} ({string.Join(", ", names.Distinct().Take(4))}), truth {truth}");
            }
        }

        int regionsRight = 0, missing = 0, otherSize = 0, madeUp = 0, screensWrong = 0;
        if (Screen is not null)
        {
            var read = page?.Regions ?? [];
            static string Size(DeskRegion r) => r.Kind == DeskRegionKind.Loadout ? "" : $"{r.Cols}x{r.Rows}";
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (key, size) in Regions)
            {
                if (!keys.Add(key))
                    continue;
                if (read.FirstOrDefault(r => r.Key == key) is not { } r)
                {
                    missing++;
                    notes.Add($"MISSING region {key}");
                }
                else if (Size(r) == size)
                    regionsRight++;
                else
                {
                    otherSize++;
                    notes.Add($"OF ANOTHER SIZE {key} {Size(r)} (truth {size})");
                }
            }
            foreach (var r in read.Where(r => !keys.Contains(r.Key)))
            {
                madeUp++;
                notes.Add($"MADE UP {r.Key} {Size(r)}");
            }
            if (screen.ToString() != Screen)
            {
                screensWrong++;
                notes.Add($"SCREEN {screen}, truth {Screen}");
            }
        }

        int counts = 0, countsRight = 0, countsWrong = 0, countsUnread = 0, countsMadeUp = 0;
        int marks = 0, marksRight = 0, marksWrong = 0, marksNotLooked = 0, marksNoCell = 0;
        foreach (var (place, count, mark) in Extras)
        {
            var cell = place.On(page);
            // As read: a condition or rounds ("45/45"), a stack's count (more than one), or nothing.
            var read = cell?.Print is { } print ? print.ToString() : cell is { Count: > 1 } ? cell.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
            if (count == "-")
            {
                if (read is not null)
                {
                    countsMadeUp++;
                    notes.Add($"COUNT MADE UP {place}: read {read}, none printed");
                }
            }
            else if (!count.Contains('?') && !count.Contains('~'))
            {
                counts++;
                if (read == count)
                    countsRight++;
                else if (read is null)
                {
                    countsUnread++;
                    notes.Add($"count unread {place}: is {count}");
                }
                else
                {
                    countsWrong++;
                    notes.Add($"COUNT WRONG {place}: read {read}, is {count}");
                }
            }
            if (mark is not { } yes)
                continue;
            marks++;
            if (cell is null)
                marksNoCell++;
            else if (cell.FoundInRaid is not { } seen)
            {
                marksNotLooked++;
                notes.Add($"fir unread {place}: is {(yes ? "yes" : "no")}");
            }
            else if (seen == yes)
                marksRight++;
            else
            {
                marksWrong++;
                notes.Add($"FIR WRONG {place}: read {(seen ? "yes" : "no")}, is {(yes ? "yes" : "no")}");
            }
        }

        return new DeskScore
        {
            Pictures = 1,
            Items = Items.Count, Right = right, Wrong = wrong, OneOf = oneOf, NotAmong = notAmong, Silent = silent, NoCell = noCell,
            Regions = Screen is null ? 0 : Regions.Select(r => r.Key).Distinct(StringComparer.Ordinal).Count(),
            RegionsRight = regionsRight, RegionsMissing = missing, RegionsOtherSize = otherSize, RegionsMadeUp = madeUp, ScreensWrong = screensWrong,
            Counts = counts, CountsRight = countsRight, CountsWrong = countsWrong, CountsUnread = countsUnread, CountsMadeUp = countsMadeUp,
            Marks = marks, MarksRight = marksRight, MarksWrong = marksWrong, MarksNotLooked = marksNotLooked, MarksNoCell = marksNoCell,
            Notes = notes,
        };
    }
}
