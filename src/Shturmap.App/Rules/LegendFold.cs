namespace Shturmap.App.Rules;

/// <summary>
/// Help's legend of map symbols (docs/DESIGN.md "Map drawing", *Legend*). The rows come most important first; help
/// lists the shown map's first <see cref="Listed"/> and keeps the rest of them, and the symbols this map doesn't have,
/// behind one link (owner, 2026-10-09: help trimmed; Streets listed 26 rows). With no map up every row counts as on it.
/// </summary>
public static class LegendFold
{
    /// <summary>How many of the map's symbols help lists before its link.</summary>
    public const int Listed = 12;

    /// <summary>The rows, in their order: those listed, the map's others behind the link, and those it doesn't have.</summary>
    public static (IReadOnlyList<T> Listed, IReadOnlyList<T> More, IReadOnlyList<T> Elsewhere) Split<T>(IReadOnlyList<T> rows, Func<T, bool> onMap)
    {
        var here = rows.Where(onMap).ToList();
        return (here.Take(Listed).ToList(), here.Skip(Listed).ToList(), rows.Where(r => !onMap(r)).ToList());
    }
}
