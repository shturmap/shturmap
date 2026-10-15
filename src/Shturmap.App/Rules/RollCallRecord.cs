using Shturmap.Core.Raid;

namespace Shturmap.App.Rules;

/// <summary>
/// What the extract list's roll call called in this raid, and how: each extract named, lit or "??:??:??" (docs/DESIGN.md
/// §4, "Map drawing", *A list just read is called out*). Once a roll call ran in a raid, a later list rings only what it lit
/// or marked anew, where it stands, with no cue. Kept by the window for the raid and its map, not on the map's scene
/// (coordinator, 2026-10-15, from the roll call's build): a look at another map in the raid and back makes the raid map's
/// scene anew, and with the scene went its record, so the next list got a whole roll call again, cue and all. A new raid,
/// or the raid on another map (a transit), is another raid: its first list is called out as a first.
/// </summary>
public sealed class RollCallRecord
{
    /// <summary>A raid as the record knows it: the raid's map, the game's id for the raid and when it started.</summary>
    public readonly record struct Raid(string Map, string? Id, DateTime? Started);

    private Raid? _raid;
    private readonly Dictionary<string, bool> _called = new(StringComparer.Ordinal);

    /// <summary>The raid running on <paramref name="raidMap"/> (the raid's own map, whatever is on screen); null outside
    /// one.</summary>
    public static Raid? Of(string? raidMap, RaidState state) =>
        state.Phase == RaidPhase.InRaid && raidMap is not null ? new Raid(raidMap, state.RaidId, state.RaidStartedAt) : null;

    /// <summary>The extracts a roll call called in this raid (marker id to lit), or null where none ran in it.</summary>
    public IReadOnlyDictionary<string, bool>? In(Raid? raid) => raid is { } r && _raid == r ? _called : null;

    /// <summary>
    /// A roll call, a list that joined it or one rung after it called these extracts in <paramref name="raid"/>, each
    /// lit (true) or "??:??:??": kept, and what an earlier raid's roll call called is forgotten.
    /// </summary>
    public void Called(Raid raid, IEnumerable<(string Id, bool Listed)> exits)
    {
        if (_raid != raid)
        {
            _raid = raid;
            _called.Clear();
        }
        foreach (var (id, listed) in exits)
            _called[id] = listed;
    }
}
