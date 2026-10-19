namespace Shturmap.Game.Screenshots;

/// <summary>
/// For measuring <see cref="TasksReader"/> (shturmap-cli tasks, TaskTableTests' real pictures): a hand-written truth for
/// one picture (truth-&lt;picture&gt;.txt beside it), one line each:
/// "screen table|trader|none"; "row name | location | status | hh:mm:ss | progress" per row, top to bottom;
/// "objective row name | text | n/m" per objective of the expanded row, "-" for an objective without a count. Lines
/// starting with # are notes.
/// </summary>
internal sealed record TaskTruth(string Screen, List<(string Name, string Location, string Status, string Time, string Progress)> Rows,
    List<(string Row, string Text, string Count)> Objectives)
{
    public static TaskTruth Load(string path)
    {
        var screen = "none";
        var rows = new List<(string, string, string, string, string)>();
        var objectives = new List<(string, string, string)>();
        foreach (var line in File.ReadAllLines(path).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')))
        {
            var space = line.IndexOf(' ');
            if (space < 0)
                continue;
            var parts = line[(space + 1)..].Split('|').Select(p => p.Trim()).ToArray();
            switch (line[..space])
            {
                case "screen": screen = parts[0]; break;
                case "row": rows.Add((parts[0], parts[1], parts[2], parts[3], parts[4])); break;
                case "objective": objectives.Add((parts[0], parts[1], parts[2])); break;
            }
        }
        return new TaskTruth(screen, rows, objectives);
    }

    public static bool Same(string a, string b) => TaskTable.Fold(a) == TaskTable.Fold(b);

    /// <summary>An objective's count as a truth writes it: "0/3", or "-" for none.</summary>
    public static string Count(TaskObjective o) => o.Of is null ? "-" : $"{o.Done}/{o.Of}";
}
