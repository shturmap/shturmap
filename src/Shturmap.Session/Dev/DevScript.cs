#if DEVTOOLS
using System.Globalization;
using System.Text;

namespace Shturmap.Session.Dev;

/// <summary>One step of a developer script: the verb and its arguments, with its line for errors.</summary>
public sealed record DevStep(int Line, string Verb, IReadOnlyList<string> Args)
{
    public string Arg(int i, string fallback = "") => i < Args.Count ? Args[i] : fallback;

    public double Number(int i, double fallback = 0) =>
        i < Args.Count && double.TryParse(Args[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    public override string ToString() => Args.Count == 0 ? Verb : $"{Verb} {string.Join(' ', Args)}";
}

/// <summary>
/// "--dev-script &lt;file&gt;": the developer view's buttons, one per line, played in order after the session has
/// started, so a dev raid can be replayed headless and snapshotted (docs/DESIGN.md §8, "Developer aids"). A line is a
/// verb and its arguments, separated by spaces ("quotes" keep spaces); "#" starts a comment.
/// </summary>
public static class DevScript
{
    /// <summary>The verbs and what they take; the developer view's buttons do the same.</summary>
    public static IReadOnlyDictionary<string, string> Verbs { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["mode"] = "pve | pvp | seasonal: the Session mode line",
        ["map"] = "<normalized name>: the map the next raid commands use, shown in the app",
        ["side"] = "pmc | scav",
        ["hosting"] = "server | local",
        ["group"] = "the leader picks the current map",
        ["load"] = "loading starts: scene line, and match setup (server) or transit line (local)",
        ["steps"] = "the loading steps up to the spawn",
        ["start"] = "the raid starts",
        ["end"] = "the raid ends",
        ["cancel"] = "matching cancelled",
        ["transit"] = "<normalized name>: a transit to that map, during a raid",
        ["quest"] = "start | complete | fail <quest id or part of its name>",
        ["place"] = "<fx> <fy> [<to fx> <to fy>]: a click (or drag, for the facing) on the map at fractions of its size",
        ["pick"] = "<id or name>: picks (or unpicks) the quest for the coming raid, as its pen does",
        ["tick"] = "<quest id or name> <n>: ticks (or unticks) the quest's n-th objective as done, as the box on its card does; not saved",
        ["show"] = "<part of a quest's name>: its card held, the first thing it needs beside it, and the quest popped out, as --show-quest does for snapshots",
        ["hover"] = "[<quest id or name> [<n> | cell | key]]: the pointer on the quest's block in the lists, and in it on its n-th objective's line, its first need cell or a gold line that is a key, through the code the pointer's events call; alone, it leaves the innermost of them",
        ["point"] = "[<quest id or name> [<n>] | item <id or name>]: points at the quest, at its n-th objective or at an item, as the pointer on its line would; alone, at nothing again",
        ["trail"] = "<x> <y> [<x> <y> ...]: where the pointer has been in the main window, for the cards to tell where it is heading; no pointer moves",
        ["cards"] = "writes the open cards' titles to the app log, with its time",
        ["pos"] = "<x> <y> <z> [yaw]: a screenshot at that world position",
        ["repeat"] = "a screenshot at the last position again",
        ["age"] = "<minutes>: the last position becomes older",
        ["walk"] = "<seconds between>: a screenshot at each place picked for the path",
        ["trigger"] = "report | crash | update | offline | 404 | 503 | nogame | reload | key <down | up | enter | p | esc> (nogame: as if no game were on this PC; key: the rail's row keys, through the code the key events call, never a key sent)",
        ["choose"] = "<folder> | game | auto: \"Choose game folder…\" with that folder; game is this view's fake game; auto is FIND AUTOMATICALLY",
        ["wait"] = "<seconds>",
        ["snapshot"] = "<folder>: the window and the map as PNGs",
        ["exit"] = "closes the app",
    };

    public static (IReadOnlyList<DevStep> Steps, IReadOnlyList<string> Errors) Parse(string text)
    {
        var steps = new List<DevStep>();
        var errors = new List<string>();
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var hash = line.IndexOf('#');
            if (hash >= 0)
                line = line[..hash];
            var words = Words(line);
            if (words.Count == 0)
                continue;
            var verb = words[0].ToLowerInvariant();
            if (!Verbs.ContainsKey(verb))
            {
                errors.Add($"line {i + 1}: unknown step '{words[0]}'");
                continue;
            }
            steps.Add(new DevStep(i + 1, verb, words.Skip(1).ToList()));
        }
        return (steps, errors);
    }

    // Space-separated words; "double quotes" keep spaces inside a word.
    private static List<string> Words(string line)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        var quoted = false;
        var any = false;
        foreach (var ch in line)
        {
            if (ch == '"')
            {
                quoted = !quoted;
                any = true;
            }
            else if (char.IsWhiteSpace(ch) && !quoted)
            {
                if (any)
                    words.Add(word.ToString());
                word.Clear();
                any = false;
            }
            else
            {
                word.Append(ch);
                any = true;
            }
        }
        if (any)
            words.Add(word.ToString());
        return words;
    }
}
#endif
