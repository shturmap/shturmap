using System.Text.Json;
using System.Text.Json.Serialization;
using Shturmap.Core.Logs;

namespace Shturmap.Core.Operational;

/// <summary>What an operational task asks, from its objective's words (<see cref="OperationalWords.KindOf"/>).</summary>
public enum OperationalKind
{
    /// <summary>Words of no kind known: the line is shown as read, with the generic operational glyph.</summary>
    Unknown,

    /// <summary>"Eliminate …": a kill, with its condition.</summary>
    Kill,

    /// <summary>"Survive and extract …", "Exit the location".</summary>
    Survive,

    /// <summary>"Hand over the found in raid item: …": items found in raid, handed over after the raid.</summary>
    HandOver,
}

/// <summary>One objective line of an expanded row as read: its words and its count n/m where the game shows one.</summary>
public sealed record OperationalObjective(string Text, int? Done = null, int? Of = null);

/// <summary>
/// One row of the OPERATIONAL table as a screenshot showed it, for <see cref="OperationalLedger.ApplyTable"/>: the
/// task's name, its map (a tarkov.dev map id; null for "Any location"), the countdown and how many of the ways the
/// reading tried agreed on it, the progress read, and the objectives when the row was expanded (else none).
/// </summary>
public sealed record OperationalRead(string Name, string? MapId, TimeSpan? TimeLeft, int TimeVotes, int? Progress,
    IReadOnlyList<OperationalObjective> Objectives);

/// <summary>
/// An operational task as Shturmap keeps it (owner, 2026-10-19: from a panel of four, B; docs/DESIGN.md §2, "Menu
/// screenshots", and §8, "The operational tasks file"): its name and map, its objectives with their counts, its kind,
/// its progress as of the screenshot, when it ends (null when the countdown failed its checks and no earlier screenshot
/// gave one) and when it was read. No rewards, no pixel, no other word of the picture.
/// </summary>
/// <param name="MapId">tarkov.dev's id of the task's map; null for "Any location".</param>
/// <param name="ObjectivesAt">When the objectives were read: a newer table screenshot keeps them while its row isn't
/// expanded, so they may be older than <paramref name="ReadAt"/>.</param>
public sealed record OperationalTask(string Name, string? MapId, IReadOnlyList<OperationalObjective> Objectives, OperationalKind Kind,
    int? Progress, DateTimeOffset? ExpiresAt, DateTimeOffset ReadAt, DateTimeOffset? ObjectivesAt = null)
{
    /// <summary>"Any location": the task counts on every map.</summary>
    public bool AnyMap => MapId is null;

    /// <summary>The objective's count n/m when the task has exactly one counted objective, read with the table itself
    /// (or agreeing with its progress): "4 of 7" says it better than "57 %".</summary>
    public (int Done, int Of)? Count =>
        Objectives.Where(o => o.Of > 0).ToList() is [{ Done: { } done, Of: { } of }]
        && (ObjectivesAt == ReadAt || Progress is null || Math.Abs(Progress.Value - OperationalChecks.Percent(done, of)) <= 1)
            ? (done, of) : null;
}

/// <summary>Tasks that ran out since the last table screenshot: how many, and when the last of them ended.</summary>
public sealed record OperationalEnded(int Count, DateTimeOffset LastAt);

/// <summary>An item a hand-over names: its name as the game's screen writes it, and tarkov.dev's id when exactly one item
/// of the catalogue has that whole name (else null: the words alone, no picture).</summary>
public sealed record OperationalItem(string Name, string? ItemId);

/// <summary>A task as the rail shows it: the task, its map as Plan counts maps (the map's normalized name, a variant's
/// base map's; null for any map), and the items its hand-overs name.</summary>
public sealed record OperationalShown(OperationalTask Task, string? MapKey, IReadOnlyList<OperationalItem> Items);

/// <summary>
/// The checks the countdowns read by vote must pass (owner, 2026-10-19, B; docs/DESIGN.md §2): Windows' recognition
/// drops the game's small digits more often than it reads them, and read a wrong one twice in the spike's 168 tries,
/// so a countdown is taken only when it is plausible and agrees with what else the screenshots say.
/// </summary>
public static class OperationalChecks
{
    /// <summary>The longest a countdown can say: a weekly's week.</summary>
    public static readonly TimeSpan Longest = TimeSpan.FromDays(7);

    /// <summary>Countdowns closer than this on one screenshot are taken for one shared countdown (the dailies the game
    /// gives together end together), and must then agree to <see cref="RowsAgree"/>.</summary>
    public static readonly TimeSpan SharedWithin = TimeSpan.FromMinutes(1);

    /// <summary>How far one screenshot's rows that share a countdown may be apart: they are read at one moment.</summary>
    public static readonly TimeSpan RowsAgree = TimeSpan.FromSeconds(2);

    /// <summary>How far one task's expiry from two screenshots may be apart: the file's time is to the second, and the
    /// game writes the file a moment after the key.</summary>
    public static readonly TimeSpan ShotsAgree = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The countdown each row of one screenshot may keep: null where none was read or it fails a check. It must lie
    /// within 0 and <see cref="Longest"/>; and two rows whose countdowns lie within <see cref="SharedWithin"/> of each
    /// other but more than <see cref="RowsAgree"/> apart can't both be right: the one more of the reading's ways agreed
    /// on keeps its own, and on a tie neither does.
    /// </summary>
    public static IReadOnlyList<TimeSpan?> Countdowns(IReadOnlyList<(TimeSpan? Left, int Votes)> rows)
    {
        var plausible = rows.Select(r => r.Left is { } t && t > TimeSpan.Zero && t <= Longest ? t : (TimeSpan?)null).ToList();
        var kept = plausible.ToList();
        for (var i = 0; i < rows.Count; i++)
        {
            if (plausible[i] is not { } mine)
                continue;
            for (var j = 0; j < rows.Count; j++)
            {
                if (j == i || plausible[j] is not { } other)
                    continue;
                var apart = (mine - other).Duration();
                if (apart > RowsAgree && apart <= SharedWithin && rows[i].Votes <= rows[j].Votes)
                {
                    kept[i] = null;
                    break;
                }
            }
        }
        return kept;
    }

    /// <summary>Whether one task's expiry from two screenshots agrees.</summary>
    public static bool Agree(DateTimeOffset a, DateTimeOffset b) => (a - b).Duration() <= ShotsAgree;

    /// <summary>A count as the game's percentage: 4 of 7 is 57.</summary>
    public static int Percent(int done, int of) => of <= 0 ? 0 : Math.Clamp(done * 100 / of, 0, 100);

    /// <summary>
    /// The progress to keep: the percentage read when it passes, else from the objectives' counts, else none. With
    /// one counted objective read on the same screenshot, a percentage more than a point off its count fails (the
    /// count's digits read on the page; the percentage's by vote); counts of several objectives give their sum's share.
    /// </summary>
    /// <param name="objectivesReadNow">The objectives come from the same screenshot as the percentage (its row was
    /// expanded); older ones can't overrule a newer percentage.</param>
    public static int? Progress(int? read, IReadOnlyList<OperationalObjective> objectives, bool objectivesReadNow)
    {
        var counted = objectives.Where(o => o is { Done: not null, Of: > 0 }).ToList();
        int? fromCounts = counted.Count == 0 ? null : Percent(counted.Sum(o => o.Done!.Value), counted.Sum(o => o.Of!.Value));
        if (read is { } percent and >= 0 and <= 100)
        {
            if (counted.Count == 1 && objectivesReadNow && Math.Abs(percent - fromCounts!.Value) > 1)
                return fromCounts;
            return percent;
        }
        return fromCounts;
    }
}

/// <summary>
/// The operational tasks Shturmap learned from screenshots of Character › TASKS › OPERATIONAL (owner, 2026-10-19: from a
/// panel of four, B), one set per game mode, kept in <c>operational.json</c> in the data folder (docs/DESIGN.md §8,
/// "The operational tasks file"). A newer table screenshot replaces the mode's set (a task gone from it was handed in,
/// replaced or ran out); a row expanded on it brings that task's objectives, a row not expanded keeps the ones read
/// before. A task goes at its expiry, and is remembered as ended until the next table screenshot, a day at most, so the
/// rail can say "1 ended at 22:35".
/// </summary>
public sealed class OperationalLedger
{
    /// <summary>The file's name in the data folder.</summary>
    public const string FileName = "operational.json";

    public const int Version = 1;

    /// <summary>How long a task that ran out is still said to have ended, without a newer table screenshot.</summary>
    public static readonly TimeSpan EndedKept = TimeSpan.FromDays(1);

    private sealed class ModeSet
    {
        public DateTimeOffset ReadAt { get; init; }
        public List<OperationalTask> Tasks { get; init; } = [];
        public List<DateTimeOffset> Ended { get; init; } = [];
    }

    private readonly Dictionary<GameMode, ModeSet> _sets = [];

    /// <summary>Whether something changed since the last <see cref="Save"/> or load.</summary>
    public bool Dirty { get; private set; }

    /// <summary>A mode's tasks in the table's order; empty when none were read.</summary>
    public IReadOnlyList<OperationalTask> Tasks(GameMode mode) => _sets.GetValueOrDefault(mode)?.Tasks ?? [];

    /// <summary>When the mode's set was read: the last table screenshot's time.</summary>
    public DateTimeOffset? ReadAt(GameMode mode) => _sets.GetValueOrDefault(mode)?.ReadAt;

    /// <summary>The mode's tasks that ran out since its last table screenshot, or null.</summary>
    public OperationalEnded? Ended(GameMode mode) =>
        _sets.GetValueOrDefault(mode)?.Ended is { Count: > 0 } ended ? new OperationalEnded(ended.Count, ended.Max()) : null;

    /// <summary>The next moment a task runs out, in any mode, or null.</summary>
    public DateTimeOffset? NextExpiry() =>
        _sets.Values.SelectMany(s => s.Tasks).Select(t => t.ExpiresAt).OfType<DateTimeOffset>().Order().Cast<DateTimeOffset?>().FirstOrDefault();

    /// <summary>
    /// The table of a screenshot taken at <paramref name="at"/>: the mode's set, replaced. Each row's countdown goes
    /// through <see cref="OperationalChecks.Countdowns"/>; one that fails leaves the task the expiry an earlier
    /// screenshot gave it, else none; one that disagrees with an earlier screenshot's leaves it none (which of the two
    /// is wrong can't be told). Null, and nothing changes, for a screenshot older than the set.
    /// </summary>
    /// <returns>How many expiries the checks took away, or null when the screenshot is older than the set.</returns>
    public int? ApplyTable(GameMode mode, DateTimeOffset at, IReadOnlyList<OperationalRead> rows)
    {
        var before = _sets.GetValueOrDefault(mode);
        if (before is not null && at < before.ReadAt)
            return null;
        var lefts = OperationalChecks.Countdowns(rows.Select(r => (r.TimeLeft, r.TimeVotes)).ToList());
        var tasks = new List<OperationalTask>(rows.Count);
        var dropped = 0;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var was = Earlier(before, rows, i);
            var readNow = row.Objectives.Count > 0;
            var objectives = readNow ? row.Objectives : was?.Objectives ?? [];
            DateTimeOffset? expires;
            if (lefts[i] is { } left)
            {
                expires = at + left;
                if (was?.ExpiresAt is { } earlier && !OperationalChecks.Agree(expires.Value, earlier))
                {
                    expires = null;
                    dropped++;
                }
            }
            else
            {
                if (row.TimeLeft is not null)
                    dropped++;
                expires = was?.ExpiresAt;
            }
            tasks.Add(new OperationalTask(row.Name, row.MapId, objectives, OperationalWords.KindOf(objectives, row.Name),
                OperationalChecks.Progress(row.Progress, objectives, readNow), expires, at,
                readNow ? at : objectives.Count > 0 ? was?.ObjectivesAt : null));
        }
        _sets[mode] = new ModeSet { ReadAt = at, Tasks = tasks };
        Dirty = true;
        return dropped;
    }

    // The same task in the set before: its name and map, the n-th of that name and map on both screenshots (two
    // dailies of one name on one map stay apart).
    private static OperationalTask? Earlier(ModeSet? before, IReadOnlyList<OperationalRead> rows, int index)
    {
        if (before is null)
            return null;
        var row = rows[index];
        var nth = rows.Take(index).Count(r => Same(r.Name, r.MapId, row.Name, row.MapId));
        return before.Tasks.Where(t => Same(t.Name, t.MapId, row.Name, row.MapId)).ElementAtOrDefault(nth);
    }

    private static bool Same(string name, string? map, string otherName, string? otherMap) =>
        string.Equals(name, otherName, StringComparison.OrdinalIgnoreCase) && map == otherMap;

    /// <summary>Drops every task whose time ran out by <paramref name="now"/>, noting it as ended, and forgets the ended
    /// ones older than <see cref="EndedKept"/>. Returns how many tasks went.</summary>
    public int Expire(DateTimeOffset now)
    {
        var gone = 0;
        foreach (var set in _sets.Values)
        {
            foreach (var task in set.Tasks.Where(t => t.ExpiresAt <= now).ToList())
            {
                set.Tasks.Remove(task);
                set.Ended.Add(task.ExpiresAt!.Value);
                gone++;
                Dirty = true;
            }
            if (set.Ended.RemoveAll(e => now - e > EndedKept) > 0)
                Dirty = true;
        }
        return gone;
    }

    // ---- the file ----

    /// <summary>The tasks in a file, or none when there is no file. One that can't be read is set aside as
    /// <c>operational.json.unreadable</c> and the tasks start over: the next table screenshot brings them back.</summary>
    public static OperationalLedger Load(string path, Action<string>? warn = null)
    {
        var ledger = new OperationalLedger();
        if (!File.Exists(path))
            return ledger;
        try
        {
            var model = JsonSerializer.Deserialize(File.ReadAllText(path), OperationalJson.Default.OperationalFile);
            foreach (var (name, set) in model?.Modes ?? [])
            {
                if (ModeOf(name) is not { } mode || set is null)
                    continue;
                var tasks = (set.Tasks ?? []).Where(t => t is { Name.Length: > 0 }).Select(t => new OperationalTask(
                    t.Name!, t.Map,
                    (t.Objectives ?? []).Where(o => o is { Text.Length: > 0 }).Select(o => new OperationalObjective(o.Text!, o.Done, o.Of)).ToList(),
                    KindOf(t.Kind), t.Progress, t.ExpiresAt, t.ReadAt ?? set.ReadAt, t.ObjectivesAt)).ToList();
                ledger._sets[mode] = new ModeSet { ReadAt = set.ReadAt, Tasks = tasks, Ended = set.Ended ?? [] };
            }
            return ledger;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            warn?.Invoke($"The operational tasks file couldn't be read ({e.GetType().Name}); starting over");
            try
            {
                File.Move(path, path + ".unreadable", overwrite: true);
            }
            catch (Exception moving) when (moving is IOException or UnauthorizedAccessException)
            {
            }
            return new OperationalLedger();
        }
    }

    /// <summary>Writes the tasks: a temporary file, then one move, so a crash leaves the old file whole.</summary>
    public void Save(string path)
    {
        var model = new OperationalFile(Version, _sets.OrderBy(s => s.Key).ToDictionary(s => ModeName(s.Key), s => new ModeFile(
            s.Value.ReadAt,
            s.Value.Tasks.Select(t => new TaskFile(t.Name, t.MapId, KindName(t.Kind),
                t.Objectives.Count == 0 ? null : t.Objectives.Select(o => new ObjectiveFile(o.Text, o.Done, o.Of)).ToList(),
                t.ObjectivesAt, t.Progress, t.ExpiresAt, t.ReadAt)).ToList(),
            s.Value.Ended.Count == 0 ? null : s.Value.Ended.Order().ToList())));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(model, OperationalJson.Default.OperationalFile));
        File.Move(temp, path, overwrite: true);
        Dirty = false;
    }

    // "pve", "pvp": the mode's name in lower case, as the notes file writes it.
    private static string ModeName(GameMode mode) => mode.ToString().ToLowerInvariant();

    private static GameMode? ModeOf(string? name) =>
        Enum.GetValues<GameMode>().Where(m => ModeName(m) == name).Select(m => (GameMode?)m).FirstOrDefault();

    private static string KindName(OperationalKind kind) => kind switch
    {
        OperationalKind.Kill => "kill",
        OperationalKind.Survive => "survive",
        OperationalKind.HandOver => "handOver",
        _ => "unknown",
    };

    private static OperationalKind KindOf(string? name) => name switch
    {
        "kill" => OperationalKind.Kill,
        "survive" => OperationalKind.Survive,
        "handOver" => OperationalKind.HandOver,
        _ => OperationalKind.Unknown,
    };
}

// The file's shape (docs/DESIGN.md §8, "The operational tasks file"): per mode the set's time, its tasks in the
// table's order (the name and objectives as the game writes them, a tarkov.dev map id or none, the kind, the progress,
// the expiry and the times read), and when tasks ran out since. Dates with their offset. Read leniently.
internal sealed record OperationalFile(int Version, Dictionary<string, ModeFile>? Modes);

internal sealed record ModeFile(DateTimeOffset ReadAt, List<TaskFile>? Tasks, List<DateTimeOffset>? Ended);

internal sealed record TaskFile(string? Name, string? Map, string? Kind, List<ObjectiveFile>? Objectives, DateTimeOffset? ObjectivesAt,
    int? Progress, DateTimeOffset? ExpiresAt, DateTimeOffset? ReadAt);

internal sealed record ObjectiveFile(string? Text, int? Done, int? Of);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true)]
[JsonSerializable(typeof(OperationalFile))]
internal sealed partial class OperationalJson : JsonSerializerContext;
