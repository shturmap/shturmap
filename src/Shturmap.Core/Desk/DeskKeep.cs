namespace Shturmap.Core.Desk;

// The desk's keep mark (owner, 2026-10-19: the keep mark's levels from the quest dependency tree, not a planner screen of
// their own; docs/DESIGN.md §2, "For the stash desk", and §4, *The cell*): NOW, an active quest needs the item; NEXT, a
// quest the active ones unlock (or one open to start) needs it; LATER, a quest further down the tree; and a hideout
// module's need, whatever the hideout has built (its progress isn't in the logs; hideout tracking stays a not-goal).

/// <summary>How strongly a need says "keep", weakest first. The mark's order: now, next, a hideout module, later.</summary>
public enum DeskKeepLevel
{
    None,
    Later,
    Hideout,
    Next,
    Now,
}

/// <summary>One need of an item: by which quest (or hideout station and level), how many, and whether found in raid.</summary>
/// <param name="For">The quest's id, or the hideout station's id.</param>
/// <param name="StationLevel">The hideout level that takes it; null for a quest.</param>
public sealed record DeskNeed(DeskKeepLevel Level, string For, int Count, bool FoundInRaid, int? StationLevel = null);

/// <summary>What one quest needs of items (hand-overs, finds, plants, a marker, items to use, keys, gear to wear, mods)
/// and the quests it needs done first.</summary>
public sealed record DeskQuestNeeds(string QuestId, IReadOnlyList<string> Prerequisites, IReadOnlyList<(string ItemId, int Count, bool FoundInRaid)> Items);

/// <summary>An item a hideout level takes.</summary>
public sealed record DeskHideoutNeed(string StationId, int Level, string ItemId, int Count, bool FoundInRaid);

/// <summary>Every item's needs, from the quest tree's state and the hideout.</summary>
public sealed class DeskKeep
{
    private readonly Dictionary<string, List<DeskNeed>> _needs = new(StringComparer.Ordinal);

    public static DeskKeep Empty { get; } = new();

    /// <summary>
    /// The needs by item: an active quest's are NOW; a quest neither active nor done whose prerequisites are all active or
    /// done (the active ones unlock it, or it is open to start) is NEXT; every other quest not done is LATER (further
    /// down the tree); every hideout level's are a module's need. A quest done (completed or failed) needs nothing.
    /// </summary>
    public static DeskKeep Of(IEnumerable<DeskQuestNeeds> quests, IReadOnlySet<string> active, IReadOnlySet<string> done, IEnumerable<DeskHideoutNeed> hideout)
    {
        var keep = new DeskKeep();
        foreach (var quest in quests)
        {
            if (done.Contains(quest.QuestId))
                continue;
            var level = active.Contains(quest.QuestId) ? DeskKeepLevel.Now
                : quest.Prerequisites.All(p => active.Contains(p) || done.Contains(p)) ? DeskKeepLevel.Next
                : DeskKeepLevel.Later;
            foreach (var (itemId, count, fir) in quest.Items)
                keep.Add(itemId, new DeskNeed(level, quest.QuestId, Math.Max(1, count), fir));
        }
        foreach (var need in hideout)
            keep.Add(need.ItemId, new DeskNeed(DeskKeepLevel.Hideout, need.StationId, Math.Max(1, need.Count), need.FoundInRaid, need.Level));
        return keep;
    }

    private void Add(string itemId, DeskNeed need)
    {
        if (!_needs.TryGetValue(itemId, out var list))
            _needs[itemId] = list = [];
        list.Add(need);
    }

    /// <summary>The item's needs in progress order for the card's KEEP (step C, 2026-10-19): now, next, a hideout module's,
    /// later (the notch's order: a module's need is wanted at some level for certain, a later quest's is far off).</summary>
    public IReadOnlyList<DeskNeed> NeedsOf(string itemId) =>
        _needs.TryGetValue(itemId, out var list)
            ? list.OrderBy(n => n.Level switch { DeskKeepLevel.Now => 0, DeskKeepLevel.Next => 1, DeskKeepLevel.Hideout => 2, _ => 3 })
                .ThenBy(n => n.StationLevel ?? 0).ToList()
            : [];

    /// <summary>
    /// The cell's notch for an item: the strongest need sets it (now, next, a hideout module, later). A LATER need marks
    /// only when it is found-in-raid or the item can't be bought (no flea, no trader): quests far down the tree want common
    /// things, so "later" everywhere would mark half the stash and say nothing (docs/DESIGN.md §4, *The cell*).
    /// </summary>
    public DeskKeepLevel MarkOf(string itemId, bool buyable)
    {
        if (!_needs.TryGetValue(itemId, out var list))
            return DeskKeepLevel.None;
        return list.Where(n => n.Level != DeskKeepLevel.Later || n.FoundInRaid || !buyable)
            .Select(n => n.Level).DefaultIfEmpty(DeskKeepLevel.None).Max();
    }
}
