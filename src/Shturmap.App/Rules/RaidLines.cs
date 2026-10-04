namespace Shturmap.App.Rules;

/// <summary>The raid card's objective lines.</summary>
public static class RaidLines
{
    /// <summary>
    /// The need a quest's lines all repeat ("Bring: MS2000 Marker" under each of three places to mark), to be said
    /// once under the quest's name instead (owner, 2026-10-04, from the review's C1). It is one when two or more open
    /// lines have a need and every one of those has the same; a line that needs nothing doesn't count either way.
    /// Empty otherwise: needs that differ stay on their lines, where they say which place they are for.
    /// </summary>
    public static string SharedNeed(IReadOnlyList<(string Needs, bool Done)> lines)
    {
        var needs = lines.Where(l => !l.Done && l.Needs.Length > 0).Select(l => l.Needs).ToList();
        return needs.Count >= 2 && needs.All(n => n == needs[0]) ? needs[0] : "";
    }
}
