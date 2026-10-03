using Shturmap.Core.Logs;
using Shturmap.Core.Quests;

namespace Shturmap.Session;

/// <summary>
/// The quests the player picked for the coming raid, one set per game mode (PvE and PvP progress are separate), kept
/// in the settings so they survive a restart (owner, 2026-10-03: "something like the current 'mark quest' thing, but
/// for all the quest you want to tackle"). No upkeep: a quest the log reports completed or failed leaves the picks by
/// itself (<see cref="Prune"/>); otherwise picks stay until the pen is clicked again or they are cleared, since a quest
/// often takes several raids.
/// </summary>
/// <param name="read">Reads a setting (null when unset).</param>
/// <param name="write">Writes a setting.</param>
public sealed class QuestPicks(Func<string, string?> read, Action<string, string> write)
{
    private readonly Dictionary<GameMode, HashSet<string>> _byMode = [];

    // Picks made only for this session (a snapshot's -ShowQuest): shown, never saved.
    private readonly Dictionary<GameMode, HashSet<string>> _unsaved = [];

    /// <summary>The setting a mode's picks are kept in: quest ids, comma-separated.</summary>
    public static string Key(GameMode mode) => $"picks.{mode}";

    /// <summary>The picks for a mode: a copy, since snapshots hand it to the UI thread while the session may change
    /// the picks (a quest completing).</summary>
    public IReadOnlySet<string> Of(GameMode mode)
    {
        var picks = new HashSet<string>(Saved(mode), StringComparer.Ordinal);
        if (_unsaved.TryGetValue(mode, out var extra))
            picks.UnionWith(extra);
        return picks;
    }

    /// <summary>Picks a quest, or unpicks it if it is picked; returns whether it is picked now.</summary>
    /// <param name="save">False for picks that must not outlive the session (developer snapshots).</param>
    public bool Toggle(GameMode mode, string questId, bool save = true)
    {
        var saved = Saved(mode);
        var unsaved = _unsaved.TryGetValue(mode, out var u) ? u : _unsaved[mode] = [];
        if (saved.Remove(questId))
        {
            Save(mode);
            unsaved.Remove(questId);
            return false;
        }
        if (unsaved.Remove(questId))
            return false;
        if (save)
        {
            saved.Add(questId);
            Save(mode);
        }
        else
        {
            unsaved.Add(questId);
        }
        return true;
    }

    /// <summary>Unpicks everything for a mode.</summary>
    public void Clear(GameMode mode)
    {
        var saved = Saved(mode);
        _unsaved.Remove(mode);
        if (saved.Count == 0)
            return;
        saved.Clear();
        Save(mode);
    }

    /// <summary>
    /// Takes out the picks the log reports completed or failed, and returns them. A quest whose state isn't known
    /// (the data still loading, a quest from another catalog) stays picked: only what the log says counts.
    /// </summary>
    public IReadOnlyList<string> Prune(GameMode mode, IReadOnlyDictionary<string, QuestStatus> quests)
    {
        var gone = Of(mode).Where(id => quests.GetValueOrDefault(id)?.State is QuestState.Completed or QuestState.Failed).ToList();
        if (gone.Count == 0)
            return gone;
        var saved = Saved(mode);
        saved.ExceptWith(gone);
        if (_unsaved.TryGetValue(mode, out var unsaved))
            unsaved.ExceptWith(gone);
        Save(mode);
        return gone;
    }

    private HashSet<string> Saved(GameMode mode)
    {
        if (!_byMode.TryGetValue(mode, out var set))
        {
            set = (read(Key(mode)) ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
            _byMode[mode] = set;
        }
        return set;
    }

    private void Save(GameMode mode) => write(Key(mode), string.Join(",", Saved(mode).Order(StringComparer.Ordinal)));
}
