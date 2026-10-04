using System.Globalization;
using Shturmap.Core.Logs;
using Shturmap.Core.Quests;

namespace Shturmap.Session;

/// <summary>
/// The objectives the player ticked as done, one set per game mode, kept in the settings beside the picks (owner,
/// 2026-10-04). The game's logs say when a quest starts, completes or fails, never when a single objective is done,
/// so a quest that takes several raids kept leading to places already dealt with. A tick is the one thing the player
/// may tell Shturmap about progress: it is never asked for, and quest states still come from the logs alone. No
/// upkeep: a tick leaves by itself when the log reports its quest completed or failed (<see cref="Prune"/>);
/// otherwise it stays until its box is clicked again. Each tick keeps the day it was set, so the card can say where
/// "done" comes from ("ticked by you, 4 Oct").
/// </summary>
/// <param name="read">Reads a setting (null when unset).</param>
/// <param name="write">Writes a setting.</param>
public sealed class ObjectiveTicks(Func<string, string?> read, Action<string, string> write)
{
    private readonly Dictionary<GameMode, Dictionary<string, DateOnly>> _byMode = [];

    // Ticks made only for this session (a developer script's, for a snapshot): shown, never saved.
    private readonly Dictionary<GameMode, Dictionary<string, DateOnly>> _unsaved = [];

    /// <summary>The setting a mode's ticks are kept in: "objective id@day", comma-separated.</summary>
    public static string Key(GameMode mode) => $"ticks.{mode}";

    /// <summary>The ticks for a mode, each with the day it was set: a copy, since snapshots hand it to the UI thread
    /// while the session may change the ticks (a quest completing).</summary>
    public IReadOnlyDictionary<string, DateOnly> Of(GameMode mode)
    {
        var ticks = new Dictionary<string, DateOnly>(Saved(mode), StringComparer.Ordinal);
        if (_unsaved.TryGetValue(mode, out var extra))
        {
            foreach (var (id, day) in extra)
                ticks[id] = day;
        }
        return ticks;
    }

    /// <summary>Ticks an objective, or unticks it if it is ticked; returns whether it is ticked now.</summary>
    /// <param name="day">The day of the tick, for "ticked by you, 4 Oct".</param>
    /// <param name="save">False for ticks that must not outlive the session (developer snapshots).</param>
    public bool Toggle(GameMode mode, string objectiveId, DateOnly day, bool save = true)
    {
        var saved = Saved(mode);
        var unsaved = _unsaved.TryGetValue(mode, out var u) ? u : _unsaved[mode] = new(StringComparer.Ordinal);
        if (saved.Remove(objectiveId))
        {
            Save(mode);
            unsaved.Remove(objectiveId);
            return false;
        }
        if (unsaved.Remove(objectiveId))
            return false;
        if (save)
        {
            saved[objectiveId] = day;
            Save(mode);
        }
        else
        {
            unsaved[objectiveId] = day;
        }
        return true;
    }

    /// <summary>
    /// Takes out the ticks whose quest the log reports completed or failed, and returns them: the quest is over, so
    /// there is nothing left to leave out. A tick whose objective the data doesn't know (the data still loading, a
    /// quest from another catalog) stays, and so does one whose quest's state isn't known: only what the log says
    /// counts.
    /// </summary>
    /// <param name="questOf">The quest an objective belongs to, or null when the data doesn't know the objective.</param>
    public IReadOnlyList<string> Prune(GameMode mode, IReadOnlyDictionary<string, QuestStatus> quests, Func<string, string?> questOf)
    {
        var gone = Of(mode).Keys
            .Where(id => questOf(id) is { } quest && quests.GetValueOrDefault(quest)?.State is QuestState.Completed or QuestState.Failed)
            .ToList();
        if (gone.Count == 0)
            return gone;
        var saved = Saved(mode);
        var changed = false;
        foreach (var id in gone)
        {
            changed |= saved.Remove(id);
            if (_unsaved.TryGetValue(mode, out var unsaved))
                unsaved.Remove(id);
        }
        if (changed)
            Save(mode);
        return gone;
    }

    private Dictionary<string, DateOnly> Saved(GameMode mode)
    {
        if (_byMode.TryGetValue(mode, out var ticks))
            return ticks;
        ticks = new Dictionary<string, DateOnly>(StringComparer.Ordinal);
        foreach (var entry in (read(Key(mode)) ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var at = entry.IndexOf('@');
            var id = at < 0 ? entry : entry[..at];
            if (id.Length == 0)
                continue;
            // A tick without a readable day is still a tick; the card then says "ticked by you" without one.
            ticks[id] = at >= 0 && DateOnly.TryParseExact(entry[(at + 1)..], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                ? day
                : default;
        }
        return _byMode[mode] = ticks;
    }

    private void Save(GameMode mode) => write(Key(mode), string.Join(",", Saved(mode)
        .OrderBy(t => t.Key, StringComparer.Ordinal)
        .Select(t => t.Value == default ? t.Key : $"{t.Key}@{t.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}")));
}
