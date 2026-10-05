using System.Text;

namespace Shturmap.Core.Screenshots;

/// <summary>A word read from a screenshot, with its box in the picture's pixels.</summary>
public readonly record struct ReadWord(string Text, double X, double Y, double Width, double Height)
{
    public double CenterY => Y + Height / 2;
}

/// <summary>One line of the game's extract list as read: its words, and where it stands.</summary>
public sealed record ExitListLine(string Text, double Top, double Bottom);

/// <summary>A row of the game's extract list.</summary>
/// <param name="Text">The row's words ("EXFIL02 Primorsky Ave Taxi V-Ex").</param>
/// <param name="Marked">Something stands in the row's right column: the game's "??:??:??" (the exit may be closed or
/// needs something) or a countdown.</param>
public sealed record ExitListRow(string Text, bool Marked);

/// <summary>The game's extract list as read from one screenshot: the green bar's words and the rows under it.</summary>
public sealed record ExitListReading(string Header, IReadOnlyList<ExitListRow> Rows);

/// <summary>An exit of the raid's map that a row can name: the names it goes by (the game's language, and English).</summary>
public sealed record ExitName(string Id, IReadOnlyList<string> Names);

/// <summary>
/// The game's own list of the ways out it gave the player this raid, which it shows at the top right at a raid's start
/// and when the player asks for it (O twice by default), read from a screenshot that happens to show it (owner,
/// 2026-10-05: "check a screenshot if it's taken if it contains this information ... read it and update the map
/// accordingly"). Neither the logs nor tarkov.dev's data say which extracts a raid opens for the player; this list
/// does. Here: the words a reader found become rows, and rows are matched to the map's exits by name. Opening the
/// picture and finding the words is the reader's part (Shturmap.Game, <c>ExitListReader</c>).
/// </summary>
public static class ExitList
{
    /// <summary>The green bar's words in an English game. The bar also heads the one-row box the game shows while the
    /// player stands in an exit ("Stay in the extraction point"), which is no list (<see cref="IsList"/>).</summary>
    public const string EnglishHeader = "Find an extraction point";

    /// <summary>
    /// The words as lines: the line inside the green bar is the header, the lines under it are rows. Words left of
    /// the bar belong to the scene behind the list (a sign, a container's number) and are left out.
    /// </summary>
    /// <param name="barLeft">The green bar's left edge.</param>
    /// <param name="barTop">Its top.</param>
    /// <param name="barBottom">Its bottom.</param>
    public static (string Header, IReadOnlyList<ExitListLine> Lines) Lines(IEnumerable<ReadWord> words, double barLeft, double barTop, double barBottom)
    {
        var bar = barBottom - barTop;
        var inList = words.Where(w => w.Text.Length > 0 && w.X >= barLeft - bar * 0.25).OrderBy(w => w.CenterY).ToList();
        var header = string.Join(' ', inList.Where(w => w.CenterY >= barTop && w.CenterY <= barBottom).OrderBy(w => w.X).Select(w => w.Text));
        var lines = new List<List<ReadWord>>();
        foreach (var word in inList.Where(w => w.CenterY > barBottom))
        {
            // Words of one row stand on one line: their middles lie within half a word's height of each other.
            if (lines.Count > 0 && Math.Abs(lines[^1].Average(w => w.CenterY) - word.CenterY) <= Math.Max(lines[^1].Max(w => w.Height), word.Height) * 0.5)
                lines[^1].Add(word);
            else
                lines.Add([word]);
        }
        return (header, lines.Select(l => new ExitListLine(string.Join(' ', l.OrderBy(w => w.X).Select(w => w.Text)), l.Min(w => w.Y), l.Max(w => w.Y + w.Height))).ToList());
    }

    /// <summary>
    /// Which of the map's exits the rows name, each with whether its row is marked. A row is its label ("EXFIL02"),
    /// then the exit's name; the name is compared without the label, letter by letter with what a reader confuses
    /// folded together (the game's slashed zero is read as "Ø", "1" as "I"), and may be a little off. A row that
    /// names no exit (a transit, the note under one, something from the scene) names none; a row that could be two
    /// exits equally well names neither.
    /// </summary>
    public static IReadOnlyDictionary<string, bool> Match(ExitListReading reading, IReadOnlyList<ExitName> exits)
    {
        var known = exits.Select(e => (e.Id, Forms: e.Names.SelectMany(Forms).Distinct(StringComparer.Ordinal).ToList())).ToList();
        var found = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var row in reading.Rows)
        {
            var words = row.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // With the label, without it, and without a label the reader split in two ("TRANSIT 02").
            var said = Enumerable.Range(0, Math.Min(3, words.Length)).SelectMany(skip => Forms(string.Join(' ', words.Skip(skip)))).Distinct(StringComparer.Ordinal).ToList();
            var scores = known.Select(k => (k.Id, k.Forms, Score: k.Forms.Max(f => said.Max(s => Similarity(f, s))))).Where(k => k.Score >= Enough).ToList();
            if (scores.Count == 0)
                continue;
            var best = scores.Max(k => k.Score);
            // Two exits of one name (one entry per side in the data) are one row; two different names equally near are a guess.
            var winners = scores.Where(k => k.Score == best).ToList();
            if (winners.Select(k => k.Forms[0]).Distinct(StringComparer.Ordinal).Count() > 1)
                continue;
            foreach (var winner in winners)
                found[winner.Id] = row.Marked || found.GetValueOrDefault(winner.Id);
        }
        return found;
    }

    /// <summary>
    /// Whether what was read is the list. Two exits named: yes. One: only under the list's own header, since the box
    /// the game shows while the player stands in an exit has the same green bar and names that one exit; taking it
    /// for the list would say every other exit is closed. (In another game language a list of one exit isn't taken.)
    /// </summary>
    public static bool IsList(ExitListReading reading, int exitsNamed) =>
        exitsNamed >= 2 || (exitsNamed == 1 && Similarity(Fold(reading.Header), Fold(EnglishHeader)) >= Enough);

    // How alike two names must be to be the same exit: one letter in five may be off.
    private const double Enough = 0.8;

    // A name as it may stand in the list: whole, and without what stands in brackets ("Klimov Street (Flare)": the
    // data's name in another language can lack the bracket).
    private static IEnumerable<string> Forms(string name)
    {
        var whole = Fold(name);
        if (whole.Length >= 3)
            yield return whole;
        var open = name.IndexOf('(');
        if (open > 0 && Fold(name[..open]) is { Length: >= 3 } bare)
            yield return bare;
    }

    /// <summary>Letters and digits only, lower case, with the look-alikes a reader confuses made one: O, Ø, Q and D
    /// are 0; I, L and | are 1.</summary>
    public static string Fold(string text)
    {
        var folded = new StringBuilder(text.Length);
        foreach (var ch in text.ToLowerInvariant())
        {
            switch (ch)
            {
                case 'o' or 'ø' or 'q' or 'd' or '0':
                    folded.Append('0');
                    break;
                case 'i' or 'l' or '|' or '1' or '!':
                    folded.Append('1');
                    break;
                default:
                    if (char.IsLetterOrDigit(ch))
                        folded.Append(ch);
                    break;
            }
        }
        return folded.ToString();
    }

    // 1 for the same text, less by the share of letters to change (Levenshtein).
    private static double Similarity(string a, string b)
    {
        if (a == b)
            return 1;
        if (a.Length == 0 || b.Length == 0)
            return 0;
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
            previous[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1], previous[j]) + 1, previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            (previous, current) = (current, previous);
        }
        return 1 - (double)previous[b.Length] / Math.Max(a.Length, b.Length);
    }
}
