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

    /// <summary>How many colours the picks have; a ninth pick takes the first again.</summary>
    public const int Colours = 8;

    /// <summary>The setting a mode's picks' colours are kept in: "quest id:number", comma-separated.</summary>
    public static string SlotsKey(GameMode mode) => $"pickslots.{mode}";

    private readonly Dictionary<GameMode, Dictionary<string, int>> _slots = [];

    /// <summary>
    /// Which colour each pick has (0 to <see cref="Colours"/> - 1), so its markers, its name and its row are told
    /// from the other picks' (owner, 2026-10-04: "A color per pick ... is the best solution"). A pick takes the
    /// first colour no other pick has and keeps it until it is unpicked, also across restarts: unpicking one quest
    /// never recolours the others. With every colour taken, it takes the one used least.
    /// </summary>
    public IReadOnlyDictionary<string, int> Slots(GameMode mode)
    {
        var slots = SlotsOf(mode);
        var changed = false;
        var picks = Of(mode);
        foreach (var gone in slots.Keys.Where(id => !picks.Contains(id)).ToList())
            changed |= slots.Remove(gone);
        foreach (var id in picks.Where(id => !slots.ContainsKey(id)).Order(StringComparer.Ordinal))
        {
            slots[id] = Enumerable.Range(0, Colours).OrderBy(c => slots.Values.Count(v => v == c)).ThenBy(c => c).First();
            changed = true;
        }
        if (changed)
            write(SlotsKey(mode), string.Join(",", slots.Where(s => Saved(mode).Contains(s.Key)).OrderBy(s => s.Key, StringComparer.Ordinal).Select(s => $"{s.Key}:{s.Value}")));
        return new Dictionary<string, int>(slots, StringComparer.Ordinal);
    }

    private Dictionary<string, int> SlotsOf(GameMode mode)
    {
        if (_slots.TryGetValue(mode, out var slots))
            return slots;
        slots = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var pair in (read(SlotsKey(mode)) ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var at = pair.LastIndexOf(':');
            if (at > 0 && int.TryParse(pair[(at + 1)..], out var slot) && slot >= 0 && slot < Colours)
                slots[pair[..at]] = slot;
        }
        return _slots[mode] = slots;
    }

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
