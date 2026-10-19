using Microsoft.UI.Xaml;
using Shturmap.App.Controls;
using Shturmap.App.Rules;
using Shturmap.Core.Logs;
using Shturmap.Core.Operational;
using Shturmap.Core.Raid;
using Shturmap.Map;
using Shturmap.Session;

namespace Shturmap.App;

// The daily and weekly operational tasks where the map's work is (owner, 2026-10-19: from a panel of four, "Use B, the
// recommended one"; docs/DESIGN.md §4, "Screen anatomy", *Operational tasks*; Rules.OperationalRail): framed glyphs on
// NEXT RAID's rows, an OPERATIONAL section in the open card and the raid card, a Scav's hand-over items under FIND IN
// RAID. The rows a reading brought glow in (Controls.Glow) and the session says it in a notice; no cue.
public sealed partial class MainWindow
{
    /// <summary>"--operational &lt;n&gt;" with a fake game (snapshots): n made-up operational tasks once the data and the
    /// mode are known, and with "--operational-ended" one more that runs out at once (GameSession.ReadExampleTasksAsync).</summary>
    public (int Count, bool Ended)? ExampleTasksOnStart { get; set; }

    private void ReadExampleTasksOnStart(SessionSnapshot s)
    {
        if (ExampleTasksOnStart is not { } example || s.Data is null || s.Mode == GameMode.Unknown)
            return;
        ExampleTasksOnStart = null;
        _ = ReadExampleTasksAsync(example.Count, example.Ended);
    }

    private async Task ReadExampleTasksAsync(int count, bool ended)
    {
        if (await _session.ReadExampleTasksAsync(count, ended) is { } why)
            AppLog.Warn("--operational: " + why);
    }

    private HashSet<string>? _tasksApplied;
    private readonly Dictionary<string, DateTime> _freshTasks = new(StringComparer.Ordinal);

    // The tasks this snapshot brings, against the one applied before: fresh for a moment, so their rows glow. Not the
    // first snapshot's (the file's), and none in a snapshot run or the demo, as the stash's marks.
    private void NoteFreshTasks(SessionSnapshot s)
    {
        var now = DateTime.Now;
        var keys = s.Operational.Select(TaskKey).ToHashSet(StringComparer.Ordinal);
        if (_tasksApplied is { } before && !SnapshotMode && !DemoMode)
        {
            foreach (var key in keys.Where(k => !before.Contains(k)))
                _freshTasks[key] = now;
        }
        _tasksApplied = keys;
        foreach (var key in _freshTasks.Where(f => now - f.Value > FreshFor).Select(f => f.Key).ToList())
            _freshTasks.Remove(key);
    }

    // A task as one reading showed it: a newer screenshot of the same task is a new row, which glows in again.
    private static string TaskKey(OperationalShown t) =>
        string.Join("\n", t.Task.Name, t.Task.MapId ?? "", t.Task.ReadAt.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>A map's OPERATIONAL rows: its own tasks, then the any-map ones after a hairline (in every card, owner,
    /// 2026-10-19). In a raid each says its time only when it ends before the raid could, and "anywhere" stands where a
    /// quest row's distance would.</summary>
    private IReadOnlyList<OperationalLine> TaskLines(SessionSnapshot s, string? mapKey, bool inRaid)
    {
        var (here, anywhere) = OperationalRail.Section(s.Operational, mapKey);
        // The raid's time left as its clock says it; while it loads, the map's whole raid.
        var raidLeft = ViewModel.RaidClock?.Left ?? (s.Raid.Phase == RaidPhase.Loading && s.RaidInfo is { RaidMinutes: > 0 } info ? info.RaidMinutes : null);
        return here.Select(t => TaskLine(s, t, inRaid, raidLeft, startsAnywhere: false))
            .Concat(anywhere.Select((t, i) => TaskLine(s, t, inRaid, raidLeft, startsAnywhere: i == 0 && here.Count > 0)))
            .ToList();
    }

    private OperationalLine TaskLine(SessionSnapshot s, OperationalShown t, bool inRaid, int? raidLeft, bool startsAnywhere)
    {
        var now = DateTimeOffset.Now;
        var key = TaskKey(t);
        var synopsis = OperationalRail.Synopsis(OperationalWords.Synopsis(t.Task), t.Items.Select(i => i.Name));
        return new OperationalLine(key, t.Task.Kind, synopsis,
            inRaid ? OperationalRail.RaidLine(t) : OperationalRail.PlanLine(t, now),
            inRaid ? OperationalRail.EndsIn(t.Task, now, raidLeft) ?? "" : "",
            inRaid && t.Task.AnyMap ? AppTexts.ObjectiveAnywhere : "",
            startsAnywhere)
        {
            // A hand-over's items the catalogue knows by their whole English name, as cells in the app's language, as
            // any item cell: pointing at one opens its card.
            Items = t.Items.Where(i => i.ItemId is not null)
                .Select(i => new NeedChip(i.ItemId!, Glyphs.Bring, s.Data?.ItemName(i.ItemId!) ?? i.Name)
                {
                    Marked = s.Marks.ContainsKey(i.ItemId!),
                    PictureId = Shturmap.Data.TarkovDev.ItemSources.PictureOf(s.Sources, i.ItemId!),
                }).ToList(),
            Fresh = _freshTasks.ContainsKey(key),
            // A trader's focus held: an operational task is none of that trader's quests, so it steps back with the others.
            SteppedBack = !inRaid && _heldTrader is not null,
        };
    }

    /// <summary>The raid card's OPERATIONAL eyebrow: over rows, or over the line that tasks ran out.</summary>
    public Visibility ShownIfOperational(IReadOnlyList<OperationalLine>? rows, string? ended) =>
        rows is { Count: > 0 } || !string.IsNullOrEmpty(ended) ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>A NEXT RAID row's framed glyphs: the map's own tasks only (any-map ones would stand on every row).</summary>
    private IReadOnlyList<OperationalLine> TaskGlyphs(SessionSnapshot s, string mapKey) =>
        OperationalRail.OnRow(s.Operational, mapKey).Select(t => TaskLine(s, t, inRaid: false, raidLeft: null, startsAnywhere: false)).ToList();

    /// <summary>A Scav raid's FIND IN RAID: the hand-over items of the raid's map's and the any-map tasks that the catalogue
    /// knows by their whole name (a row is its item: its picture, its card), after the quests' loot. Kills and survivals
    /// are taken to count for the PMC only (DESIGN.md §4, an assumption to confirm).</summary>
    private IReadOnlyList<RequirementLine> TaskLoot(SessionSnapshot s)
    {
        var (here, anywhere) = OperationalRail.Section(s.Operational, s.MapPlan?.NormalizedName);
        return here.Concat(anywhere).Where(t => t.Task.Kind == OperationalKind.HandOver)
            .SelectMany(t => t.Items)
            .Select(i => i.ItemId).OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Select(id => new RequirementLine(Glyphs.Bring, s.Data?.ItemName(id) ?? id, AppTexts.ScavLootOperational, id, [], ""))
            .ToList();
    }
}
