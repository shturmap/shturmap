namespace Shturmap.App.Rules;

/// <summary>
/// Where a card stands up and down (docs/DESIGN.md §4, "Quest cards"; owner, 2026-10-15: "When selecting a quest at the
/// lower border of the screen it gets cut off"): beside what it opened from, its top a little above that row's, and lower
/// never than its whole height allows inside the room, with <see cref="Inset"/> to spare; a card from a row near the room's
/// foot opens moved up as far as it needs, never above the room's top. The same rule holds while it is open: a card that
/// grows (a held quest card's NOTES field, a note added) moves up, never down. The room is the window, less what of it
/// lies off the screen (<see cref="Room"/>). A card taller than the room is held to its height (<see cref="MaxHeight"/>)
/// and scrolls inside. Everything is in the window's DIPs.
/// </summary>
public static class CardPlace
{
    /// <summary>DIPs a card keeps from the room's top and foot.</summary>
    public const double Inset = 8;

    /// <summary>How far above its row's top a card's top stands, so its title is level with the row's.</summary>
    public const double Lift = 10;

    /// <summary>In a room too low for it a card still gets this much height.</summary>
    public const double MinHeight = 160;

    /// <summary>The room up and down, in the window's DIPs.</summary>
    public readonly record struct Span(double Top, double Bottom)
    {
        public double Height => Bottom - Top;
    }

    /// <summary>The most a card may be tall in the room: no more than the room, less the insets.</summary>
    public static double MaxHeight(Span room) => Math.Max(MinHeight, room.Height - 2 * Inset);

    /// <summary>
    /// A card's top as it opens beside a row whose top is <paramref name="rowTop"/>: a little above the row, moved up as
    /// far as its whole <paramref name="height"/> needs to stay inside the room, never above the room's top.
    /// </summary>
    public static double Top(double rowTop, double height, Span room) =>
        Math.Clamp(rowTop - Lift, room.Top + Inset, Math.Max(room.Top + Inset, room.Bottom - Inset - height));

    /// <summary>
    /// An open card's top once it is <paramref name="height"/> tall: where it stands, or as far up as it must to stay
    /// inside the room (never above its top); never down.
    /// </summary>
    public static double Kept(double top, double height, Span room)
    {
        var over = top + height - (room.Bottom - Inset);
        return over > 0 ? Math.Max(room.Top + Inset, top - over) : top;
    }

    /// <summary>
    /// The room the cards have: the window's <paramref name="windowHeight"/> DIPs, less what of it lies off the screen's
    /// work area (behind the taskbar, past the screen's lower or upper edge), where the window's client area stands at
    /// <paramref name="clientTop"/> screen pixels on a monitor whose work area runs from <paramref name="workTop"/> to
    /// <paramref name="workBottom"/>, at <paramref name="scale"/> pixels per DIP. A window that isn't on a screen (a
    /// picture-taking run's, off every monitor), or one that leaves too little of itself in sight for a card, keeps
    /// its whole height: a card is then where the window is.
    /// </summary>
    public static Span Room(double windowHeight, double clientTop, double workTop, double workBottom, double scale)
    {
        var whole = new Span(0, windowHeight);
        if (scale <= 0 || windowHeight <= 0)
            return whole;
        var top = Math.Max(0, (workTop - clientTop) / scale);
        var bottom = Math.Min(windowHeight, (workBottom - clientTop) / scale);
        return bottom - top < MinHeight + 2 * Inset ? whole : new Span(top, bottom);
    }
}
