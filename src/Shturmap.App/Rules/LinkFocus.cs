namespace Shturmap.App.Rules;

/// <summary>
/// What pointing at a linked element puts in focus, from what the element says about itself. Two of the things it
/// may say go into the focus without lighting the element itself or opening a card from it: the quests named with
/// "also" (a quest card's own body and rows, which would otherwise all glow for their quest) and the item named with
/// "also item" (an item card's own body). Without them the pointer let go of the quest or the item the moment it
/// moved from a row onto the card it had opened, and with the item went its locks and loose spots on the map (the
/// review of 2026-10-04, E3).
/// </summary>
public static class LinkFocus
{
    /// <param name="Quests">The quests in focus: the element's own, the ones its item is for, and its "also" quests.</param>
    /// <param name="Item">The item in focus: the element's own, else its "also item".</param>
    /// <param name="Alternatives">Every item the element stands for when it stands for several ("A or B"), else empty.</param>
    public sealed record Parts(IReadOnlySet<string> Quests, string? Item, IReadOnlyList<string> Alternatives, string? Marker, string? Objective);

    public static Parts Of(string? quest, IEnumerable<string>? quests, IEnumerable<string>? also, string? item, string? alsoItem,
        IEnumerable<string>? alternatives, string? marker, string? objective)
    {
        var all = new HashSet<string>(StringComparer.Ordinal);
        if (quest is not null)
            all.Add(quest);
        all.UnionWith(quests ?? []);
        all.UnionWith(also ?? []);
        return new Parts(all, item ?? alsoItem, alternatives?.Distinct(StringComparer.Ordinal).ToList() ?? [], marker, objective);
    }

    /// <summary>
    /// Whether pointing at an element lights a linked set, whose places breathe together, rather than one thing, whose
    /// places ring (owner, 2026-10-17, from a panel of four: "B"; docs/DESIGN.md §4, "Visual language", the linked
    /// highlight). A set: several quests named at once (a trader's portrait, a boss's name: its zones, the quests a BRING
    /// row is for) and an item (a need cell, a BRING row, a key's line, an item card's body: its locks and loose spots,
    /// the quests it is for). One thing: a quest (its row, its card), an objective (its line, also one that names an
    /// item), a marker (an extract and its switches). By what the element is, not by how many it names: a trader with
    /// one quest is still a trader.
    /// </summary>
    /// <param name="quests">Whether the element names its quests as several (Linked.Quests).</param>
    public static bool IsSet(bool quests, string? item, string? alsoItem, IEnumerable<string>? alternatives, string? objective) =>
        objective is null && (quests || item is not null || alsoItem is not null || alternatives?.Any() == true);
}
