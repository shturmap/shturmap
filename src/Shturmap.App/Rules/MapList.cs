namespace Shturmap.App.Rules;

/// <summary>One entry of the MAP list: a map, or the OTHER MAPS heading (not a choice).</summary>
/// <param name="NormalizedName">The map, by the name Plan counts it under; empty for the heading.</param>
/// <param name="Count">What it holds, as its row in Plan says it ("Complete 2 · progress 1"); empty for none.</param>
/// <param name="Covers">The maps it stands for: itself and its variants drawn with the same artwork.</param>
public sealed record MapChoice(string NormalizedName, string Name, string Count, IReadOnlyList<string> Covers, bool IsHeader = false)
{
    /// <summary>The list's type-to-find goes by the name.</summary>
    public override string ToString() => Name;

    public bool Stands(string? normalizedName) => !IsHeader && normalizedName is not null && Covers.Contains(normalizedName);
}

/// <summary>
/// The MAP list at the top of the rail (owner, 2026-10-08, "A" of the maps panel: in the usage log every map was
/// picked in Plan's rows, none in this list, and the rows hold only the maps with quests). The maps with your quests
/// first, in Plan's order with their counts, then OTHER MAPS by name. A map's variants drawn with the same artwork
/// (Night Factory, Ground Zero 21+, The Lab (Dark)) are one entry, as Plan counts them; until then the list had every
/// variant, 17 entries.
/// </summary>
public static class MapList
{
    /// <summary>A map as Plan counts it.</summary>
    /// <param name="Covers">Its own name and its variants'.</param>
    public sealed record Map(string NormalizedName, string Name, IReadOnlyList<string> Covers);

    public const string OtherMaps = "OTHER MAPS";

    /// <param name="maps">Every map that can be shown.</param>
    /// <param name="withWork">The maps with your quests, best first, with what each holds.</param>
    public static IReadOnlyList<MapChoice> Build(IEnumerable<Map> maps, IEnumerable<(string NormalizedName, string Count)> withWork)
    {
        var all = maps.ToList();
        var byName = all.ToDictionary(m => m.NormalizedName, StringComparer.Ordinal);
        var list = new List<MapChoice>();
        foreach (var (name, count) in withWork)
            if (byName.Remove(name, out var map))
                list.Add(new MapChoice(map.NormalizedName, map.Name, count, map.Covers));
        var others = all.Where(m => byName.ContainsKey(m.NormalizedName)).OrderBy(m => m.Name, StringComparer.CurrentCulture).ToList();
        // The heading only between two groups: with no quests anywhere, or quests everywhere, a plain list.
        if (list.Count > 0 && others.Count > 0)
            list.Add(new MapChoice("", OtherMaps, "", [], IsHeader: true));
        list.AddRange(others.Select(m => new MapChoice(m.NormalizedName, m.Name, "", m.Covers)));
        return list;
    }

    /// <summary>The entry that stands for the map on screen, a variant included.</summary>
    public static MapChoice? For(IReadOnlyList<MapChoice> list, string? shown) => list.FirstOrDefault(c => c.Stands(shown));

    /// <summary>Whether two lists show the same: the list is only made anew when something in it changed, so an open
    /// list isn't closed under the pointer by a snapshot that changes nothing in it.</summary>
    public static bool Same(IReadOnlyList<MapChoice> a, IReadOnlyList<MapChoice> b) =>
        a.Count == b.Count && a.Zip(b).All(p => p.First.NormalizedName == p.Second.NormalizedName && p.First.Name == p.Second.Name
            && p.First.Count == p.Second.Count && p.First.IsHeader == p.Second.IsHeader && p.First.Covers.SequenceEqual(p.Second.Covers));
}
