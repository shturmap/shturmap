using Shturmap.Core.Logs;
using Shturmap.Core.Quests;

namespace Shturmap.Session;

/// <summary>
/// The quests the player picked for the coming raid, kept in the settings so they survive a restart (owner,
/// 2026-10-03: "something like the current 'mark quest' thing, but for all the quest you want to tackle"). One set
/// per game mode (PvE and PvP progress are separate) and, within it, **per map** (owner, 2026-10-04: "Store the
/// selected quests per map and persistent between sessions"): a pick is "this quest, on this map". What is picked
/// for Customs waits there while Streets is planned with picks of its own, a quest with work on several maps is
/// picked only where the player picked it, and each map hands out the picks' colours from the first, so a map's
/// two or three picks always get the colours that are easiest to tell apart. No upkeep: a quest the log reports
/// completed or failed leaves the picks of every map by itself (<see cref="Prune"/>); otherwise picks stay until
/// the pen is clicked again or the map's picks are cleared, since a quest often takes several raids.
/// </summary>
/// <param name="read">Reads a setting (null when unset).</param>
/// <param name="write">Writes a setting.</param>
public sealed class QuestPicks(Func<string, string?> read, Action<string, string> write)
{
    // A map's picks, each with its colour (0 to Colours - 1).
    private readonly Dictionary<GameMode, Dictionary<string, Dictionary<string, int>>> _byMode = [];

    // Picks from before they were kept per map: quest ids picked "everywhere", until Adopt gives each its maps.
    private readonly Dictionary<GameMode, HashSet<string>> _fromBefore = [];

    // Picks made only for this session (a snapshot's -ShowQuest): shown, never saved.
    private readonly Dictionary<GameMode, Dictionary<string, HashSet<string>>> _unsaved = [];

    /// <summary>The setting a mode's picks are kept in: "map|quest:colour", comma-separated. An entry without a map is
    /// a pick from before picks were kept per map.</summary>
    public static string Key(GameMode mode) => $"picks.{mode}";

    /// <summary>How many colours the picks have; a map's ninth pick takes the first again.</summary>
    public const int Colours = 8;

    /// <summary>A map's picks: a copy, since snapshots hand it to the UI thread while the session may change the picks
    /// (a quest completing).</summary>
    public IReadOnlySet<string> Of(GameMode mode, string map) => Slots(mode, map).Keys.ToHashSet(StringComparer.Ordinal);

    /// <summary>The maps that have picks, each with its picks.</summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> ByMap(GameMode mode) =>
        Maps(mode).ToDictionary(map => map, map => Of(mode, map), StringComparer.Ordinal);

    /// <summary>
    /// Which colour each of a map's picks has (0 to <see cref="Colours"/> - 1), so its markers, its name and its row
    /// are told from the other picks' (owner, 2026-10-04: "A color per pick ... is the best solution"). A pick takes
    /// the first colour no other pick on its map has and keeps it until it is unpicked, also across restarts:
    /// unpicking one quest never recolours the others. With every colour taken, it takes the one used least.
    /// </summary>
    public IReadOnlyDictionary<string, int> Slots(GameMode mode, string map)
    {
        var slots = new Dictionary<string, int>(StringComparer.Ordinal);
        if (Saved(mode).TryGetValue(map, out var saved))
            foreach (var (quest, slot) in saved)
                slots[quest] = slot;
        if (_unsaved.TryGetValue(mode, out var maps) && maps.TryGetValue(map, out var unsaved))
            foreach (var quest in unsaved.Where(q => !slots.ContainsKey(q)).Order(StringComparer.Ordinal))
                slots[quest] = FreeSlot(slots.Values);
        return slots;
    }

    /// <summary>Picks a quest on a map, or unpicks it there if it is picked; returns whether it is picked now.</summary>
    /// <param name="save">False for picks that must not outlive the session (developer snapshots).</param>
    public bool Toggle(GameMode mode, string map, string questId, bool save = true)
    {
        var saved = Saved(mode);
        if (saved.TryGetValue(map, out var here) && here.Remove(questId))
        {
            if (here.Count == 0)
                saved.Remove(map);
            Save(mode);
            Unsaved(mode, map).Remove(questId);
            return false;
        }
        if (Unsaved(mode, map).Remove(questId))
            return false;
        if (save)
        {
            if (here is null)
                saved[map] = here = new Dictionary<string, int>(StringComparer.Ordinal);
            here[questId] = FreeSlot(here.Values);
            Save(mode);
        }
        else
        {
            Unsaved(mode, map).Add(questId);
        }
        return true;
    }

    /// <summary>Unpicks everything on a map.</summary>
    public void Clear(GameMode mode, string map)
    {
        if (_unsaved.TryGetValue(mode, out var maps))
            maps.Remove(map);
        if (Saved(mode).Remove(map))
            Save(mode);
    }

    /// <summary>
    /// Takes out, on every map, the picks the log reports completed or failed, and returns them. A quest whose state
    /// isn't known (the data still loading, a quest from another catalog) stays picked: only what the log says counts.
    /// </summary>
    public IReadOnlyList<string> Prune(GameMode mode, IReadOnlyDictionary<string, QuestStatus> quests)
    {
        bool Over(string id) => quests.GetValueOrDefault(id)?.State is QuestState.Completed or QuestState.Failed;
        var saved = Saved(mode);
        var gone = saved.Values.SelectMany(m => m.Keys).Concat(_fromBefore.GetValueOrDefault(mode) ?? [])
            .Concat(_unsaved.GetValueOrDefault(mode)?.Values.SelectMany(m => m) ?? []).Where(Over).Distinct(StringComparer.Ordinal).ToList();
        if (gone.Count == 0)
            return gone;
        foreach (var map in saved.Keys.ToList())
        {
            foreach (var id in gone)
                saved[map].Remove(id);
            if (saved[map].Count == 0)
                saved.Remove(map);
        }
        _fromBefore.GetValueOrDefault(mode)?.ExceptWith(gone);
        foreach (var unsaved in _unsaved.GetValueOrDefault(mode)?.Values.ToList() ?? [])
            unsaved.ExceptWith(gone);
        Save(mode);
        return gone;
    }

    /// <summary>
    /// Picks from before they were kept per map were picked on every map their quest has work on: each becomes a pick
    /// on those maps, once the data can say which they are. A quest it names no map for (not active any more) is let go.
    /// </summary>
    /// <param name="mapsOf">The maps a quest has work on.</param>
    public void Adopt(GameMode mode, Func<string, IEnumerable<string>> mapsOf)
    {
        var saved = Saved(mode);
        if (!_fromBefore.TryGetValue(mode, out var before) || before.Count == 0)
            return;
        foreach (var quest in before.Order(StringComparer.Ordinal))
            foreach (var map in mapsOf(quest))
            {
                if (!saved.TryGetValue(map, out var here))
                    saved[map] = here = new Dictionary<string, int>(StringComparer.Ordinal);
                if (!here.ContainsKey(quest))
                    here[quest] = FreeSlot(here.Values);
            }
        before.Clear();
        Save(mode);
    }

    private IEnumerable<string> Maps(GameMode mode) =>
        Saved(mode).Keys.Concat(_unsaved.GetValueOrDefault(mode)?.Where(m => m.Value.Count > 0).Select(m => m.Key) ?? []).Distinct(StringComparer.Ordinal);

    private HashSet<string> Unsaved(GameMode mode, string map)
    {
        var maps = _unsaved.TryGetValue(mode, out var m) ? m : _unsaved[mode] = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        return maps.TryGetValue(map, out var set) ? set : maps[map] = new HashSet<string>(StringComparer.Ordinal);
    }

    // The first colour none of these has; with all taken, the one used least.
    private static int FreeSlot(IEnumerable<int> taken)
    {
        var used = taken.ToList();
        return Enumerable.Range(0, Colours).OrderBy(c => used.Count(v => v == c)).ThenBy(c => c).First();
    }

    private Dictionary<string, Dictionary<string, int>> Saved(GameMode mode)
    {
        if (_byMode.TryGetValue(mode, out var maps))
            return maps;
        maps = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        var before = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in (read(Key(mode)) ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var bar = entry.IndexOf('|');
            if (bar <= 0)
            {
                before.Add(entry);
                continue;
            }
            var (map, rest) = (entry[..bar], entry[(bar + 1)..]);
            var colon = rest.LastIndexOf(':');
            var quest = colon > 0 ? rest[..colon] : rest;
            if (!maps.TryGetValue(map, out var here))
                maps[map] = here = new Dictionary<string, int>(StringComparer.Ordinal);
            here[quest] = colon > 0 && int.TryParse(rest[(colon + 1)..], out var slot) && slot >= 0 && slot < Colours ? slot : FreeSlot(here.Values);
        }
        _fromBefore[mode] = before;
        return _byMode[mode] = maps;
    }

    private void Save(GameMode mode) => write(Key(mode), string.Join(",",
        Saved(mode).OrderBy(m => m.Key, StringComparer.Ordinal).SelectMany(m => m.Value.OrderBy(q => q.Key, StringComparer.Ordinal).Select(q => $"{m.Key}|{q.Key}:{q.Value}"))
            .Concat((_fromBefore.GetValueOrDefault(mode) ?? []).Order(StringComparer.Ordinal))));
}
