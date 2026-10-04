namespace Shturmap.App.Rules;

/// <summary>
/// What a door on the map is for (the review of 2026-10-04, E5: a quest lit its doors, a door only its key). A lock
/// marker's group is its key's ("key:&lt;id&gt;"), and the map's content says for each quest with something there
/// which key groups it needs (<c>MapContent.QuestKeys</c>). Read the other way round, that gives the quests a door is
/// for: pointing at the door puts them in the focus beside its key, so their rows take the weaker tint
/// (<see cref="LinkStrength"/>: the quests a pointed-at item is for).
/// </summary>
public static class LinkDoor
{
    /// <param name="lockGroup">The pointed-at lock marker's group, or null.</param>
    /// <param name="questKeys">Per quest, the key groups it needs on the shown map; null when no map is up.</param>
    public static IReadOnlySet<string> Quests(string? lockGroup, IReadOnlyDictionary<string, IReadOnlyList<string>>? questKeys)
    {
        var quests = new HashSet<string>(StringComparer.Ordinal);
        if (lockGroup is null || questKeys is null)
            return quests;
        foreach (var (quest, groups) in questKeys)
        {
            if (groups.Contains(lockGroup, StringComparer.Ordinal))
                quests.Add(quest);
        }
        return quests;
    }
}
