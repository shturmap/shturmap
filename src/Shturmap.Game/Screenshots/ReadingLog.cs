using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Shturmap.Core.Desk;
using Shturmap.Core.Operational;
using SkiaSharp;

namespace Shturmap.Game.Screenshots;

/// <summary>One menu screenshot's readings and what the session did with them, for the readings log
/// (<see cref="ReadingLog"/>).</summary>
/// <param name="Screenshot">The screenshot's path or file name: the log is named after its file name.</param>
/// <param name="TakenAt">When the screenshot was taken (the watcher's time: the file's creation, to the second).</param>
public sealed record ReadingLogEntry(string Screenshot, DateTimeOffset TakenAt)
{
    /// <summary>Shturmap's version and build as the app log's first line says them ("0.7.0+f5ffe43 (dev build, dev data
    /// folder) on Windows 11 (10.0.26200)").</summary>
    public string Shturmap { get; init; } = "";

    /// <summary>The game mode the reading went to ("Pve", "Pvp").</summary>
    public string Mode { get; init; } = "";

    /// <summary>"Read your stash, gear and tasks from menu screenshots, and loot in a raid".</summary>
    public bool ReadOn { get; init; } = true;

    /// <summary>"Delete screenshots after reading".</summary>
    public bool DeleteOn { get; init; }

    /// <summary>What the menus' reader gave: null when the picture couldn't be opened whole.</summary>
    public MenuReading? Reading { get; init; }

    /// <summary>How long the reading took, in ms.</summary>
    public double Ms { get; init; }

    /// <summary>An item's name for its id as the data has it ("Bottle of water (Water)"); null for one it doesn't know.</summary>
    public Func<string, string?>? NameOf { get; init; }

    /// <summary>What the stash desk's page did with the reading; null when it took none.</summary>
    public DeskChange? Desk { get; init; }

    /// <summary>What the operational tasks' set did with the reading; null when it took none.</summary>
    public OperationalTaken? Tasks { get; init; }

    /// <summary>How many of What You Have's marks the reading changed; null when it took none.</summary>
    public int? MarksChanged { get; init; }
}

/// <summary>What the operational tasks' set did with a Tasks screenshot's reading (the session's TakeTasks), for the
/// readings log.</summary>
public sealed record OperationalTaken
{
    /// <summary>A trader's TASKS tab's reading (else the OPERATIONAL table's).</summary>
    public bool Trader { get; init; }

    /// <summary>Older than the set kept (the last table's time): left.</summary>
    public bool Older { get; init; }

    /// <summary>A table's rows taken onto the set (those on a map the data knows, or on any).</summary>
    public int Rows { get; init; }

    /// <summary>The locations as read (null: none read) of the rows, or the header's task, on no map the data knows: left out.</summary>
    public IReadOnlyList<string?> Unplaced { get; init; } = [];

    /// <summary>The countdowns the checks didn't take.</summary>
    public int Dropped { get; init; }

    /// <summary>A trader's tab: its task added to the set, or a kept one updated.</summary>
    public bool Added { get; init; }

    public bool Updated { get; init; }

    /// <summary>A trader's tab: the kept tasks whose expiry its list's other entries refreshed.</summary>
    public int Refreshed { get; init; }

    /// <summary>The mode's set after the reading.</summary>
    public IReadOnlyList<OperationalTask> Set { get; init; } = [];
}

/// <summary>
/// The readings log (owner, 2026-10-25, after testing the dev build: "Can't you write out logs of what was detected and link
/// it to the screenshot so we can troubleshoot it afterwards?"; docs/DESIGN.md §8, "Developer aids", *Readings log*): one
/// plain text file per menu screenshot read, in the data folder's <see cref="FolderName"/>, named after the screenshot
/// (its file name with its extension, and ".txt"), written again when the same screenshot is read again; the last
/// <see cref="Kept"/> kept. One fact a line, in the order the reading went: the screenshot, Shturmap's build, the picture's
/// size, the mode and the ticks; the Tasks screen told, each row or entry as read with its bands' votes, and what the
/// operational tasks' set did; What You Have's items; the desk's screen, its layout in pixels, each cell's label as the
/// recognition read it, its fit and what it matched, and what the page in hand did. Never a pixel of the picture, never a
/// word the readings don't keep (a refused window title is said by its length), never a task's text, the player's name or
/// money (never read). Never in a report or the diagnostics.
/// </summary>
public static class ReadingLog
{
    /// <summary>The folder in the data folder.</summary>
    public const string FolderName = "readings";

    /// <summary>How many files the folder keeps, the newest by their time written.</summary>
    public const int Kept = 50;

    private static readonly Lock Gate = new();

    /// <summary>The file a screenshot's readings are written to: its file name and ".txt".</summary>
    public static string PathFor(string folder, string screenshot) => Path.Combine(folder, Path.GetFileName(screenshot) + ".txt");

    /// <summary>Writes the entry's file (a temporary file and one move) and lets the oldest go past <paramref name="kept"/>;
    /// the file's path. Throws what the file system does (the caller says it in the app log).</summary>
    public static string Write(string folder, ReadingLogEntry entry, int kept = Kept)
    {
        var path = PathFor(folder, entry.Screenshot);
        var text = string.Join(Environment.NewLine, Lines(entry)) + Environment.NewLine;
        lock (Gate)
        {
            Directory.CreateDirectory(folder);
            var temp = path + ".tmp";
            File.WriteAllText(temp, text, new UTF8Encoding(false));
            File.Move(temp, path, overwrite: true);
            Prune(folder, kept);
        }
        return path;
    }

    // The newest files kept, by when they were written; one that can't be deleted now goes next time.
    private static void Prune(string folder, int kept)
    {
        var old = new DirectoryInfo(folder).EnumerateFiles()
            .Where(f => f.Extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.LastWriteTimeUtc).ThenByDescending(f => f.Name, StringComparer.Ordinal)
            .Skip(kept).ToList();
        foreach (var file in old)
        {
            try
            {
                file.Delete();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>The file's lines.</summary>
    public static IReadOnlyList<string> Lines(ReadingLogEntry entry)
    {
        var lines = new List<string>();
        void Add(FormattableString line) => lines.Add(line.ToString(CultureInfo.InvariantCulture));

        Add($"What Shturmap read from a menu screenshot, and what came of it: for troubleshooting a reading afterwards. Nothing of the picture is in it.");
        Add($"Screenshot: {Path.GetFileName(entry.Screenshot)}");
        Add($"Taken: {Time(entry.TakenAt)}");
        Add($"Shturmap: {entry.Shturmap}");
        var reading = entry.Reading;
        if (reading?.Seen is { } seen)
            Add($"Picture: {seen.Width}x{seen.Height} px");
        Add($"Mode: {entry.Mode}");
        Add($"Read your stash, gear and tasks from menu screenshots: {OnOff(entry.ReadOn)}");
        Add($"Delete screenshots after reading: {OnOff(entry.DeleteOn)}");
        lines.Add("");
        if (reading is null)
        {
            Add($"The picture couldn't be opened whole (still being written after the tries, or gone): nothing read.");
            return lines;
        }
        Add($"Read in {entry.Ms:0} ms.");
        var nothing = reading.Tasks is null && (reading.Stash is null || reading.Stash is { IsTradingScreen: false, Grids.Count: 0 }) &&
                      reading.Desk is null or { Screen: DeskScreen.None };
        if (nothing)
        {
            var noRecognition = reading.Seen is { TasksScreen: null } ? " (no English text recognition on this PC)" : "";
            var notAsked = reading.Desk is null ? " (the desk's reading wasn't asked: no item file yet, or no text recognition)" : "";
            Add($"Nothing was read from it: no Tasks screen{noRecognition}, and neither the stash nor the GEAR tab{notAsked}.");
            return lines;
        }
        TasksLines(lines, entry, reading);
        if (reading.Tasks is not null)
            return lines;
        MarksLines(lines, entry, reading);
        DeskLines(lines, entry, reading);
        return lines;
    }

    private static string OnOff(bool on) => on ? "on" : "off";

    // "1 row", "2 rows".
    private static string N(int count, string one, string many) => string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? one : many)}");

    private static string Time(DateTimeOffset t) => t.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);

    private static string Box(SKRectI r) => string.Create(CultureInfo.InvariantCulture, $"({r.Left},{r.Top} {r.Width}x{r.Height})");

    private static string Item(ReadingLogEntry entry, string id) => $"{entry.NameOf?.Invoke(id) ?? "an item the data doesn't name"} [{id}]";

    // ---- the Tasks screen ----

    private static void TasksLines(List<string> lines, ReadingLogEntry entry, MenuReading reading)
    {
        void Add(FormattableString line) => lines.Add(line.ToString(CultureInfo.InvariantCulture));
        lines.Add("");
        if (reading.Tasks is not { } page)
        {
            Add($"TASKS SCREEN: {(reading.Seen is { TasksScreen: null } ? "not looked for (no English text recognition on this PC)" : "none (no Tasks table, no trader's TASKS tab)")}: the picture went on to the item grids.");
            return;
        }
        switch (page.Screen)
        {
            case TaskScreen.OtherTable:
                Add($"TASKS SCREEN: a Tasks table without a countdown in any of its {N(page.Rows.Count, "row", "rows")} (STORY or SIDE, presumably): told, not read.");
                break;
            case TaskScreen.Table:
                Add($"TASKS SCREEN: the OPERATIONAL table, {N(page.Rows.Count, "row", "rows")}.");
                foreach (var (row, n) in page.Rows.Select((r, i) => (r, i + 1)))
                {
                    Add($"row {n}: \"{row.Name}\" | location {Quoted(row.Location)} | status {Quoted(row.Status)} | time left {TimeLeft(row)} | progress {Progress(row)}");
                    Objectives(lines, row);
                }
                break;
            case TaskScreen.TraderScreen:
                TraderLines(lines, page);
                break;
            default:
                Add($"TASKS SCREEN: {page.Screen}: not read.");
                break;
        }
        if (reading.Seen is { Bands.Count: > 0 } seen)
        {
            Add($"bands, each read {TasksReader.Ways.Count} ways, the value most of them read taken:");
            foreach (var band in seen.Bands)
                Add($"  {band}");
        }
        lines.Add("");
        if (entry.Tasks is not { } taken)
        {
            Add($"TAKEN: nothing.");
            return;
        }
        if (taken.Older)
        {
            Add($"TAKEN: nothing: the screenshot is older than the last Tasks table's, whose set stands.");
            return;
        }
        var unplaced = taken.Unplaced.Count == 0 ? ""
            : $"; left out on no map the data knows: {string.Join(", ", taken.Unplaced.Select(Quoted))}";
        if (taken.Trader)
            Add($"TAKEN: {(taken.Added ? "the task added to the set" : taken.Updated ? "the kept task of its name and map updated" : "no task")}; {N(taken.Refreshed, "kept task's countdown", "kept tasks' countdowns")} refreshed by the list's entries; {N(taken.Dropped, "countdown", "countdowns")} not taken by the checks{unplaced}.");
        else
            Add($"TAKEN: {N(taken.Rows, "row", "rows")}, the set replaced; {N(taken.Dropped, "countdown", "countdowns")} not taken by the checks{unplaced}.");
        Add($"the set ({entry.Mode}) now: {N(taken.Set.Count, "task", "tasks")}");
        foreach (var task in taken.Set)
        {
            Add($"  \"{task.Name}\" on {task.MapId ?? "any location"} | {task.Kind} | expires {(task.ExpiresAt is { } end ? Time(end) : "unknown")} | progress {(task.Progress is { } p ? $"{p} %" : "unknown")} | {N(task.Objectives.Count, "objective", "objectives")} | read {Time(task.ReadAt)}");
        }
    }

    private static void TraderLines(List<string> lines, TaskPage page)
    {
        void Add(FormattableString line) => lines.Add(line.ToString(CultureInfo.InvariantCulture));
        if (page.Selected is { } task)
        {
            var own = page.Rows.Contains(task);
            Add($"TASKS SCREEN: a trader's TASKS tab, its header's task an operational one; {N(page.Rows.Count, "entry", "entries")} in its list's OPERATIONAL TASKS.");
            Add($"header: \"{task.Name}\" | location {Quoted(task.Location)} | status {Quoted(task.Status)} | time left {(own ? TimeLeft(task) : "none: two entries of its name in the list")}");
            Objectives(lines, task);
        }
        else
        {
            Add($"TASKS SCREEN: a trader's TASKS tab, its header's task none of the list's (a story or side quest, or none); {N(page.Rows.Count, "entry", "entries")} in its list's OPERATIONAL TASKS.");
        }
        foreach (var entry in page.Rows.Where(r => !ReferenceEquals(r, page.Selected)))
            Add($"entry: \"{entry.Name}\" | time left {TimeLeft(entry)}");
    }

    private static void Objectives(List<string> lines, TaskRow row)
    {
        foreach (var o in row.Objectives)
            lines.Add($"  objective \"{o.Text}\"{(o.Of is { } of ? string.Create(CultureInfo.InvariantCulture, $" {o.Done}/{of}") : "")}");
    }

    private static string Quoted(string? text) => text is null ? "unread" : $"\"{text}\"";

    private static string TimeLeft(TaskRow row) =>
        row.TimeLeft is not { } left ? $"unread{(row.TimeText is { } t ? $" (\"{t}\")" : "")}"
        : string.Create(CultureInfo.InvariantCulture, $"{Quoted(row.TimeText)} = {TaskTable.Hms(left)} {(row.TimeByPaddle ? "(PaddleOCR; no way of Windows' recognition read it)" : Votes(row.TimeVotes))}");

    private static string Progress(TaskRow row) =>
        row.Progress is not { } p ? $"unread{(row.ProgressText is { } t ? $" (\"{t}\")" : "")}"
        : string.Create(CultureInfo.InvariantCulture, $"{Quoted(row.ProgressText)} = {p} % {Votes(row.ProgressVotes)}");

    private static string Votes(int votes) => votes == 0 ? "(the page's words)" : string.Create(CultureInfo.InvariantCulture, $"({votes} of {TasksReader.Ways.Count} ways)");

    // ---- What You Have's marks ----

    private static void MarksLines(List<string> lines, ReadingLogEntry entry, MenuReading reading)
    {
        void Add(FormattableString line) => lines.Add(line.ToString(CultureInfo.InvariantCulture));
        lines.Add("");
        switch (reading.Stash)
        {
            case null:
                Add($"WHAT YOU HAVE: not read (no item the active quests need to look for).");
                return;
            case { IsTradingScreen: true }:
                Add($"WHAT YOU HAVE: the trading screen (its cells print prices, not names): nothing read, nor for the desk.");
                return;
            case { } stash:
                var items = stash.Grids.SelectMany(g => g.Items).ToList();
                Add($"WHAT YOU HAVE (against the items the active quests need): {N(stash.Grids.Count, "grid", "grids")} ({string.Join(", ", stash.Grids.Select(g => g.Kind.ToString().ToLowerInvariant()))}), {N(items.Count, "cell", "cells")} with such an item:");
                foreach (var item in items)
                    Add($"  {Item(entry, item.ItemId)}, count {item.Count}{Fir(item.FoundInRaid)}");
                if (entry.MarksChanged is { } changed)
                    Add($"  marks changed: {changed}");
                return;
        }
    }

    private static string Fir(bool? fir) => fir switch
    {
        true => ", found in raid",
        false => ", not found in raid",
        null => "",
    };

    // ---- the stash desk ----

    private static void DeskLines(List<string> lines, ReadingLogEntry entry, MenuReading reading)
    {
        void Add(FormattableString line) => lines.Add(line.ToString(CultureInfo.InvariantCulture));
        lines.Add("");
        if (reading.Desk is not { } desk)
        {
            Add($"DESK: not read{(reading.Stash is { IsTradingScreen: true } ? " (the trading screen)" : " (no item file yet, or no text recognition)")}.");
            return;
        }
        var look = reading.Seen?.Desk;
        Add($"DESK: {Screen(desk.Screen)}, read in {desk.Ms} ms{(look is null ? "" : $" (the layout {look.LayoutMs} ms), a cell {look.Pitch} px")}.");
        if (desk.Page is not { } page)
            return;
        if (look is not null)
            LayoutLines(lines, look, page);
        var cells = page.Regions.SelectMany(r => r.Cells).ToList();
        lines.Add("");
        Add($"CELLS: {N(page.Regions.Count, "region", "regions")}, {N(cells.Count, "cell", "cells")}: {cells.Count(c => c.Ids.Count == 1)} named, {cells.Count(c => c.Ids.Count > 1)} one of several, {cells.Count(c => c.Ids.Count == 0)} nothing said{(look is null ? "" : $"; {N(look.Odd, "shape", "shapes")} no label fits (their cells unread)")}.");
        foreach (var region in page.Regions)
        {
            Add($"{Head(region)}");
            var seen = look?.Cells.Where(c => c.Region == region.Key).ToList() ?? [];
            if (seen.Count > 0)
            {
                foreach (var cell in seen)
                    Add($"  {Place(cell.Cell)} {Box(cell.Pixels)}: {Label(cell)}: {Matched(entry, cell.Cell, cell.Label, cell.Contradicted)}{Extras(cell.Cell, cell.CountText)}");
            }
            else
            {
                foreach (var cell in region.Cells)
                    Add($"  {Place(cell)}: label not known: {Matched(entry, cell, null, false)}{Extras(cell, null)}");
            }
        }
        lines.Add("");
        PageLines(lines, entry, page);
    }

    private static string Screen(DeskScreen screen) => screen switch
    {
        DeskScreen.Gear => "the GEAR tab",
        DeskScreen.Stash => "the stash",
        DeskScreen.Trading => "the trading screen: nothing read",
        DeskScreen.Raid => "a raid's inventory screen: not the desk's",
        _ => "none of the desk's screens (neither the stash nor the GEAR tab): nothing read",
    };

    private static void LayoutLines(List<string> lines, DeskLook look, DeskPage page)
    {
        void Add(FormattableString line) => lines.Add(line.ToString(CultureInfo.InvariantCulture));
        var layout = look.Layout;
        Add($"LAYOUT, in the picture's pixels:");
        if (layout.Slots.Count > 0)
            Add($"  loadout slots: {string.Join(", ", layout.Slots.Select(s => $"{s.Key} {Box(s.Box)}"))}");
        foreach (var place in layout.Regions)
        {
            var title = place.Kind == DeskRegionKind.ContainerWindow ? $" \"{place.Title ?? "of no name"}\"" : "";
            var slot = place.SlotBox is { } box ? Box(box) + (place.SlotDerived ? ", boxed from its heading" : "") : "none";
            var parts = place.Parts.Count == 0 ? "none" : string.Join(" ", place.Parts.Select(Box));
            Add($"  {DeskRegion.KeyOf(place.Kind)}{title}: slot {slot}; parts {parts}{(place.Area is { } area ? $"; area {Box(area)}" : "")}{(place.Cut ? "; cut at the column's foot" : "")}");
        }
        Add($"  stash grid: {(layout.Stash is { } s ? string.Create(CultureInfo.InvariantCulture, $"at ({s.X0},{s.Y0}), {s.Cols}x{s.Rows} cells") : "none")}");
        var windows = layout.Regions.Select(p => p.Area).OfType<SKRectI>().ToHashSet();
        Add($"  over, not read nor what lies under it: {(layout.Over.Count == 0 ? "nothing" : string.Join(", ", layout.Over.Select(o => (windows.Contains(o) ? "window " : "toast ") + Box(o))))}");
        Add($"  frames: {layout.Frames.Count} found, {layout.Unplaced} under no region");
        var titles = page.Regions.Select(r => r.Title).OfType<string>().ToHashSet(StringComparer.Ordinal);
        foreach (var note in layout.Notes)
            Add($"  note: {(note.StartsWith("headings ", StringComparison.Ordinal) ? note : Unquoted(note, titles))}");
    }

    // A layout note's quoted words: a window's title the page has stays (desk.json keeps it too), and so do the headings'
    // note's words (each one of the game's headings, a letter off at most); any other (a word refused as a title, read
    // anywhere left of the stash pane) is said by its length only, so no word of the picture the readings don't keep is
    // written.
    private static string Unquoted(string note, IReadOnlySet<string> titles) =>
        Regex.Replace(note, "\"([^\"]*)\"", m => titles.Contains(m.Groups[1].Value) ? m.Value
            : string.Create(CultureInfo.InvariantCulture, $"a word of {m.Groups[1].Value.Length} characters"));

    private static string Head(DeskRegion r)
    {
        var text = new StringBuilder();
        text.Append(r.Key);
        if (r.Title is { } title)
            text.Append($" \"{title}\"");
        if (r.Cols > 0)
            text.Append(CultureInfo.InvariantCulture, $" {r.Cols}x{r.Rows}");
        if (r.Parts.Count > 0)
            text.Append(", parts ").Append(string.Join(" ", r.Parts.Select(Rect)));
        text.Append(CultureInfo.InvariantCulture, $": {N(r.Cells.Count, "cell", "cells")} ({r.Cells.Count(c => c.Ids.Count == 1)} named, {r.Cells.Count(c => c.Ids.Count > 1)} one of several, {r.Cells.Count(c => c.Ids.Count == 0)} nothing said)");
        if (r.Unread > 0)
            text.Append(CultureInfo.InvariantCulture, $", {N(r.Unread, "cell", "cells")} unread");
        if (r.Covered.Count > 0)
            text.Append(", covered by a window or a toast: ").Append(string.Join(" ", r.Covered.Select(Rect)));
        if (r.CoveredSlots.Count > 0)
            text.Append(", slots covered: ").Append(string.Join(", ", r.CoveredSlots));
        if (r.Partial)
            text.Append(", read in part");
        return text.ToString();
    }

    private static string Rect(DeskRect r) => string.Create(CultureInfo.InvariantCulture, $"{r.I},{r.J} {r.W}x{r.H}");

    private static string Place(DeskCell c) => c.IsSlot ? $"[{c.Slot}]" : string.Create(CultureInfo.InvariantCulture, $"{c.I},{c.J} {c.W}x{c.H}");

    private static string Label(DeskCellLook look) =>
        look.Label is null ? "no label read"
        : string.Create(CultureInfo.InvariantCulture, $"label \"{look.Label}\"{(look.ByPaddle ? " (PaddleOCR; Windows' recognition read none that fit)" : "")}, " +
                                                      $"fit {look.Fit:0.00}{(look.Contradicted ? ", its footprint contradicts it" : "")}");

    private static string Matched(ReadingLogEntry entry, DeskCell cell, string? label, bool contradicted) => cell.Ids.Count switch
    {
        1 => Item(entry, cell.Ids[0]),
        > 1 => $"one of {cell.Ids.Count}: {string.Join("; ", cell.Ids.Select(id => Item(entry, id)))}",
        _ when contradicted => "nothing: the items the label fits are of another size",
        _ when label is not null => "nothing: the label fits nothing",
        _ => "nothing",
    };

    private static string Extras(DeskCell cell, string? countText)
    {
        var text = new StringBuilder();
        if (cell.Count > 1 || countText is not null)
            text.Append(CultureInfo.InvariantCulture, $", count {cell.Count}");
        if (countText is not null)
            text.Append($" (read \"{countText}\")");
        if (cell.Print is { } print)
            text.Append($", condition {print}");
        text.Append(Fir(cell.FoundInRaid));
        if (cell.Holds != DeskHolds.Nothing)
            text.Append(", holds ").Append(cell.Holds == DeskHolds.Contents ? "contents" : "mods");
        return text.ToString();
    }

    /// <summary>Why a region read in part (<see cref="DeskRegion.Partial"/>) kept the page's grid, as the readings log, the
    /// app log and the CLI's desk command say it: its grid cut at the column's foot, its slot under a window, or none of it in
    /// view; "" for no region.</summary>
    public static string KeptWhy(DeskRegion? region) =>
        region is null ? "" : region.Parts.Count > 0 ? "cut at the column's foot" : region.CoveredSlots.Count > 0 ? "under a window" : "not in view";

    private static void PageLines(List<string> lines, ReadingLogEntry entry, DeskPage reading)
    {
        void Add(FormattableString line) => lines.Add(line.ToString(CultureInfo.InvariantCulture));
        if (entry.Desk is not { } change)
        {
            Add($"THE PAGE IN HAND: not taken.");
            return;
        }
        Add($"THE PAGE IN HAND ({entry.Mode}): {(change.First ? "the first reading" : change.Changed.Count == 0 ? "nothing changed" : "changed " + string.Join(", ", change.Changed.Order(StringComparer.Ordinal)))}.");
        foreach (var key in change.Refused.Order(StringComparer.Ordinal))
        {
            var read = reading.Region(key);
            Add($"  kept the page's {key}: read too blank ({N(read?.ReadCells ?? 0, "cell", "cells")} read, {read?.Unread ?? 0} unread; the page's has {change.Page.Region(key)?.ReadCells ?? 0})");
        }
        foreach (var key in change.Kept.Order(StringComparer.Ordinal))
        {
            var read = reading.Region(key);
            Add($"  kept the page's {key} grid outside the part read: {KeptWhy(read)}");
        }
        foreach (var key in change.Dropped.Order(StringComparer.Ordinal))
            Add($"  dropped {key}: a window attached to nothing, not shown by {DeskLedger.MissedToDrop} readings in a row");
        foreach (var w in change.Page.Regions.Where(r => r.Kind == DeskRegionKind.ContainerWindow))
        {
            var where = w.AttachedTo is { } a ? $"attached to {a.Region} {(a.Slot is { } slot ? $"[{slot}]" : string.Create(CultureInfo.InvariantCulture, $"{a.I},{a.J}"))}"
                : $"attached to none ({w.SameName} of its name on the page{(w.SameNameIn.Count > 0 ? ", in " + string.Join(" and ", w.SameNameIn) : "")})";
            Add($"  window \"{w.Title ?? "of no name"}\": {where}{(reading.Region(w.Key) is null ? $", not in this screenshot ({w.Missed} readings in a row)" : "")}");
        }
        Add($"  regions now: {string.Join("; ", change.Page.InReadingOrder().Select(r => $"{r.Key}{(r.Cols > 0 ? string.Create(CultureInfo.InvariantCulture, $" {r.Cols}x{r.Rows}") : "")} {N(r.Cells.Count, "cell", "cells")}, read {Time(r.ReadAt == DateTimeOffset.MinValue ? change.Page.ReadAt : r.ReadAt)}"))}");
    }
}
