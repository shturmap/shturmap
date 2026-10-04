namespace Shturmap.App.Rules;

/// <summary>
/// The linked elements the pointer is in, when one lies inside another: an objective's line inside its quest's block,
/// a need cell inside its quest's row. The innermost is the one pointed at; leaving it gives the pointer back to the
/// one around it, which it never left (the review of 2026-10-04: the linked highlight knew one element only, so
/// leaving an inner one let go of everything while the pointer was still on the row).
/// </summary>
/// <param name="inside">Whether the first element lies inside the second.</param>
public sealed class PointerNest<T>(Func<T, T, bool> inside)
    where T : class
{
    // In the order entered. Windows doesn't promise an order for an outer and an inner element entered in one move,
    // so the order is used only between elements that don't contain one another.
    private readonly List<T> _entered = [];

    public void Enter(T element)
    {
        _entered.Remove(element);
        _entered.Add(element);
    }

    /// <summary>Whether the pointer was in it.</summary>
    public bool Leave(T element) => _entered.Remove(element);

    /// <summary>The pointer is in nothing any more (nobody is looking, or something else took the focus).</summary>
    public void Clear() => _entered.Clear();

    /// <summary>
    /// The element pointed at: the innermost one around the pointer, or null when it is in none. Of two that don't
    /// contain one another (the next row was entered before the last one was left), the one entered last counts.
    /// </summary>
    public T? Top
    {
        get
        {
            if (_entered.Count == 0)
                return null;
            var top = _entered[^1];
            while (_entered.LastOrDefault(e => !ReferenceEquals(e, top) && inside(e, top)) is { } inner)
                top = inner;
            return top;
        }
    }

    /// <summary>The element pointed at, then each entered element around it, from the inside out; empty when the pointer is in none.</summary>
    public IReadOnlyList<T> Outward()
    {
        if (Top is not { } top)
            return [];
        var around = _entered.Where(e => !ReferenceEquals(e, top) && inside(top, e)).ToList();
        // The more of the others an element lies inside, the further in it is.
        return [top, .. around.OrderByDescending(e => around.Count(o => !ReferenceEquals(o, e) && inside(e, o)))];
    }
}
