using Shturmap.Core.Raid;
using Shturmap.Core.Screenshots;
using Shturmap.Game.Screenshots;

namespace Shturmap.Session;

/// <summary>What is known of one way out in this raid.</summary>
public enum ExitState
{
    /// <summary>The game's list wasn't read this raid (or this is a transit, which the list isn't read for).</summary>
    NotChecked,

    /// <summary>On the game's list for this raid.</summary>
    Listed,

    /// <summary>On the list, marked "??:??:??": it may be closed, or it needs something.</summary>
    Unsure,

    /// <summary>The list was read and doesn't name it: not one of the player's exits this raid.</summary>
    NotListed,
}

// The extracts the game opened for the player this raid, from its own list in a screenshot (owner, 2026-10-05: "check a
// screenshot if it's taken if it contains this information ... read it and update the map accordingly, highlighting
// the open exfils"). The game shows that list at the top right when a raid starts and when the player asks for it;
// neither its logs nor tarkov.dev's data say what is on it. A screenshot the player takes while it shows is read
// (ExitListReader: the picture's top right corner, on this PC), and what it names holds for the raid.
// docs/DESIGN.md §2 and §4, "Screen anatomy".
public sealed partial class GameSession
{
    /// <summary>The key of "Read the extract list from screenshots" in the app's settings ("on" or "off"; absent is on).</summary>
    public const string ReadExitsSetting = "screenshots.readExits";

    private volatile bool _readExits = true;

    /// <summary>Whether the saved setting says on. Everything but "off" does: the list is read unless the player said no.</summary>
    public static bool ReadExitsOn(string? setting) => setting != "off";

    /// <summary>
    /// Reads the list in a screenshot, or null when it shows none; the reader for the game's language when not set
    /// (tests give their own, and a session without text recognition has none).
    /// </summary>
    public Func<string, CancellationToken, Task<ExitListReading?>>? ExitReader { get; init; }

    private Func<string, CancellationToken, Task<ExitListReading?>>? _exitReader;
    private bool _exitReaderLooked;

    /// <summary>The language Windows reads the list in ("en-US"), or null: no reader yet, or Windows has none.</summary>
    private string? _exitReaderLanguage;

    // What the list named this raid: marker id ("extract:<id>") to whether the game marked it "??:??:??"; null until a
    // list was read. A later reading adds to it and renews the marks of what it names: the game's list doesn't
    // shrink, and a reading that missed a row must not close an exit an earlier one saw.
    private Dictionary<string, bool>? _exits;
    private DateTime? _exitsReadAt;

    private void ForgetExits()
    {
        _exits = null;
        _exitsReadAt = null;
    }

    /// <summary>
    /// The "Read the extract list from screenshots" tick, saved with the app's settings. Off: no screenshot's picture is
    /// opened from now on, and what was read this raid is let go.
    /// </summary>
    public async Task SetReadExitsAsync(bool on)
    {
        await _gate.WaitAsync();
        try
        {
            _store?.SetSetting(ReadExitsSetting, on ? "on" : "off");
            _readExits = on;
            if (!on)
                ForgetExits();
            AppLog.Info("Read the extract list from screenshots: " + (on ? "on" : "off"));
            Publish();
        }
        finally
        {
            _gate.Release();
        }
    }

    // Under the gate. The reader is made at the first screenshot of a raid, not at start: most runs never need it.
    private Func<string, CancellationToken, Task<ExitListReading?>>? ReaderForExits()
    {
        if (ExitReader is not null)
            return ExitReader;
        if (_exitReaderLooked)
            return _exitReader;
        _exitReaderLooked = true;
        if (ExitListReader.Create(_settings.Language) is { } reader)
        {
            _exitReaderLanguage = reader.LanguageTag;
            _exitReader = (path, ct) => reader.ReadAsync(path, ct: ct);
            AppLog.Info($"Extract list: read with Windows' text recognition for {reader.LanguageTag}");
        }
        else
        {
            AppLog.Warn("Extract list: this Windows has no text recognition language, so the list in a screenshot can't be read");
        }
        return _exitReader;
    }

    /// <summary>The exits of a map a list's rows can name for a side: marker id, and the names in the game's language and in English.</summary>
    public static IReadOnlyList<ExitName> ExitNames(Shturmap.Data.TarkovDev.GameData data, string mapId, RaidSide side) =>
        (data.Maps.GetValueOrDefault(mapId)?.Extracts ?? [])
            .Where(e => side == RaidSide.Scav ? !string.Equals(e.Faction, "pmc", StringComparison.OrdinalIgnoreCase) : !string.Equals(e.Faction, "scav", StringComparison.OrdinalIgnoreCase))
            .Select(e => new ExitName("extract:" + e.Id, new[] { e.Name, data.EnglishName(e.Id, e.Name) }.OfType<string>().Where(n => n.Length > 0).Distinct().ToList()))
            .Where(e => e.Names.Count > 0)
            .ToList();

    // A position screenshot taken in a raid: if its picture shows the game's extract list, what the list names holds
    // for this raid. Reading takes a moment (the game is still writing the file; decoding its corner), so it runs
    // outside the gate, and counts only if the same raid is still on when it is done.
    private async Task ReadExitsAsync(ScreenshotSeen seen)
    {
        Func<string, CancellationToken, Task<ExitListReading?>>? read;
        DateTime? raid;
        string? mapId;
        await _gate.WaitAsync();
        try
        {
            if (!_readExits || _tracker.State.Phase != RaidPhase.InRaid || _raidMap is null || _data is null)
                return;
            read = ReaderForExits();
            raid = _tracker.State.RaidStartedAt;
            mapId = _raidMap.Id;
        }
        finally
        {
            _gate.Release();
        }
        if (read is null)
            return;
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var reading = await read(seen.Path, _stop.Token);
        if (reading is null)
            return;
        await _gate.WaitAsync();
        try
        {
            if (!_readExits || _data is null || _tracker.State.Phase != RaidPhase.InRaid || _tracker.State.RaidStartedAt != raid || _raidMap?.Id != mapId)
                return;
            var named = ExitList.Match(reading, ExitNames(_data, mapId, ShownRaid.Side));
            var isList = ExitList.IsList(reading, named.Count);
            Study.Game("exits.read", ("rows", reading.Rows.Count), ("named", named.Count), ("marked", named.Count(n => n.Value)), ("list", isList),
                ("ms", System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds));
            if (!isList)
            {
                // A green bar and rows, but not the list: the box the game shows in an exit, or names Windows couldn't read.
                AppLog.Info($"Extract list: a screenshot showed {reading.Rows.Count} rows, {named.Count} of them an exit of this map; not taken as the list");
                return;
            }
            var first = _exits is null;
            _exits ??= new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var (id, marked) in named)
                _exits[id] = marked;
            _exitsReadAt = seen.CreatedAt;
            AppLog.Info($"Extract list read from a screenshot: {_exits.Count} exits, {_exits.Count(e => e.Value)} marked ???");
            if (first)
                Say($"Your extract list is read: {_exits.Count} {(_exits.Count == 1 ? "extract" : "extracts")} for this raid");
            Publish();
        }
        finally
        {
            _gate.Release();
        }
    }

    // Under the gate: what is known of a way out now.
    private ExitState ExitStateOf(string markerId, bool transit) =>
        _exits is null || transit ? ExitState.NotChecked
        : !_exits.TryGetValue(markerId, out var marked) ? ExitState.NotListed
        : marked ? ExitState.Unsure : ExitState.Listed;
}
