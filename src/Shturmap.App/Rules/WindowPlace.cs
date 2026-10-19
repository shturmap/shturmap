using System.Globalization;

namespace Shturmap.App.Rules;

/// <summary>
/// Where the main window stood, kept between runs (owner, 2026-10-04: remember the window's monitor and size): the
/// monitor it was on, its bounds while not maximised, and whether it was maximised. It comes back there only if
/// that monitor is still connected in the same place; otherwise the first start's rule applies again (the first
/// monitor that isn't the primary one, maximised). A run that only takes pictures opens off every monitor instead
/// (<see cref="OffScreen"/>). Everything is in screen pixels.
/// </summary>
public static class WindowPlace
{
    /// <summary>The setting's key in shturmap.db: "x,y,w,h;max|normal;x,y,w,h" (bounds, state, monitor).</summary>
    public const string Setting = "window.place";

    /// <summary>The smallest window, in device-independent pixels: the status bar's three buttons stay in view, with
    /// the last fix trimmed, and the rail keeps a strip of map beside it.</summary>
    public const int MinWidth = 900;

    /// <inheritdoc cref="MinWidth"/>
    public const int MinHeight = 560;

    /// <summary>
    /// How much taller the smallest window grows per 1 of the text size's factor above 1. Measured (2026-10-19, German,
    /// the fake raid's Streets in a 900 DIP wide window): EXIT's foot stood at 497, 584 and 702 DIP in the window's
    /// content at 100, 125 and 150 %, 24 DIP above the foot of a 900×560 window at 100 %; 410 keeps those 24 DIP at 150 %
    /// (765), 40 at 125 % (663).
    /// </summary>
    public const int HeightPerText = 410;

    /// <summary>
    /// The smallest window at a text size (owner, 2026-10-19: the minimum grows with the text size, so the raid card's
    /// head, NEXT and EXIT stay in view as at 100 %): 900×560 at 100 %, taller by <see cref="HeightPerText"/> DIP per 1
    /// of the factor above 1 (663 at 125 %, 765 at 150 %, 970 at 200 %), the width as it is (the rail's is fixed, and the
    /// status bar gives way by its own rules).
    /// </summary>
    /// <param name="textFactor">The whole text size, Windows' times settings' step (TextSize.Effective).</param>
    public static (int Width, int Height) Smallest(double textFactor)
    {
        var factor = double.IsFinite(textFactor) ? Math.Clamp(textFactor, 1, TextSize.Most) : 1;
        return (MinWidth, (int)Math.Ceiling(MinHeight + HeightPerText * (factor - 1) - 1e-9));
    }

    /// <summary>A window's size when nothing better is known (one monitor, a first start).</summary>
    public const int DefaultWidth = 1600;

    /// <inheritdoc cref="DefaultWidth"/>
    public const int DefaultHeight = 1000;

    // How much of the window's top edge must be on the monitor for it to be grabbed and moved, and how far a new
    // window stands in from the monitor's corner.
    private const int Grab = 120;
    private const int Inset = 40;
    private const int Border = 16;

    // How far past the right-most monitor (or its main window) a picture-taking run's window stands: Windows draws a
    // window's shadow outside its bounds, a few dozen pixels at a large scale.
    private const int OffScreenGap = 200;

    public readonly record struct Rect(int X, int Y, int Width, int Height)
    {
        public int Right => X + Width;

        public int Bottom => Y + Height;
    }

    /// <param name="Bounds">The window's bounds while not maximised.</param>
    /// <param name="Monitor">The whole area of the monitor it was on.</param>
    public readonly record struct Saved(Rect Bounds, bool Maximised, Rect Monitor);

    public static string Format(Saved place) => string.Create(CultureInfo.InvariantCulture,
        $"{Text(place.Bounds)};{(place.Maximised ? "max" : "normal")};{Text(place.Monitor)}");

    public static Saved? Parse(string? setting)
    {
        if ((setting ?? "").Split(';') is not [var bounds, var state, var monitor] || state is not ("max" or "normal"))
            return null;
        return ParseRect(bounds) is { } b && ParseRect(monitor) is { Width: > 0, Height: > 0 } m ? new Saved(b, state == "max", m) : null;
    }

    /// <summary>
    /// The saved place, if the window can stand there again: its monitor is still among the connected ones, in the
    /// same place and of the same size. The bounds are brought onto that monitor where they aren't on it (a maximised
    /// window moved to another monitor keeps the bounds it last had unmaximised, on the monitor before). Null: place
    /// the window as at a first start.
    /// </summary>
    /// <param name="scale">The monitor's scale factor for the smallest window (1 at 100 %).</param>
    public static Saved? Restorable(Saved? saved, IReadOnlyList<Rect> monitors, double scale = 1) =>
        saved is { } place && monitors.Contains(place.Monitor) ? place with { Bounds = OnMonitor(place.Bounds, place.Monitor, scale) } : null;

    /// <summary>
    /// The bounds as they are if they can be used on the monitor: at least the smallest window, and with enough of
    /// the top edge on the monitor to grab it. Otherwise a window of that size (the default size where there is none)
    /// standing in from the monitor's corner, no larger than the monitor.
    /// </summary>
    public static Rect OnMonitor(Rect bounds, Rect monitor, double scale = 1)
    {
        var minWidth = (int)(MinWidth * scale);
        var minHeight = (int)(MinHeight * scale);
        var grab = (int)(Grab * scale);
        // A window snapped to a screen edge stands a few pixels past it (its invisible border): that is still on it.
        var usable = bounds.Width >= minWidth && bounds.Height >= minHeight
            && bounds.Y >= monitor.Y - Border && bounds.Y <= monitor.Bottom - grab
            && Math.Min(bounds.Right, monitor.Right) - Math.Max(bounds.X, monitor.X) >= grab;
        if (usable)
            return bounds;
        var inset = (int)(Inset * scale);
        var width = Math.Clamp(bounds.Width >= minWidth ? bounds.Width : (int)(DefaultWidth * scale), 1, Math.Max(1, monitor.Width - 2 * inset));
        var height = Math.Clamp(bounds.Height >= minHeight ? bounds.Height : (int)(DefaultHeight * scale), 1, Math.Max(1, monitor.Height - 2 * inset));
        return new Rect(monitor.X + inset, monitor.Y + inset, width, height);
    }

    /// <summary>
    /// A window's first place on a monitor: the default size at the monitor's scale, standing in from the corner of
    /// its work area and no larger than it (review of 2026-10-09: on one monitor the default was taken as pixels,
    /// 800×500 at 200 % and past the edge of a 1366×768 screen).
    /// </summary>
    public static Rect First(Rect workArea, double scale = 1) => OnMonitor(default, workArea, scale);

    /// <summary>The monitor whose right edge lies furthest right (of those that share it, the first listed); none
    /// without monitors.</summary>
    public static Rect? RightMost(IReadOnlyList<Rect> monitors) => monitors.Count == 0 ? null : monitors.MaxBy(m => m.Right);

    /// <summary>
    /// Where a run that only takes pictures opens its window (owner, 2026-10-15, "quiet local runs": the UI is checked
    /// while the owner plays on the same PC): past the right edge of the right-most monitor, level with its top, so no
    /// monitor shows any of it, its shadow included, and that monitor is the nearest, whose scale Windows gives a window
    /// off every monitor. The size is the caller's; without monitors, as if one stood at the origin.
    /// </summary>
    public static Rect OffScreen(IReadOnlyList<Rect> monitors, int width, int height)
    {
        var beside = RightMost(monitors) ?? default;
        return Beside(beside, width, height);
    }

    /// <summary>
    /// A window past the right edge of another, level with its top, as far off as <see cref="OffScreen"/> stands from a
    /// monitor: a quiet run's second window (the developer view of a script's run, owner 2026-10-15) beside its main
    /// window, off every monitor as that one is, and not over it.
    /// </summary>
    public static Rect Beside(Rect other, int width, int height) => new(other.Right + OffScreenGap, other.Y, width, height);

    private static string Text(Rect r) => string.Create(CultureInfo.InvariantCulture, $"{r.X},{r.Y},{r.Width},{r.Height}");

    private static Rect? ParseRect(string text)
    {
        var parts = text.Split(',');
        if (parts.Length != 4)
            return null;
        var numbers = new int[4];
        for (var i = 0; i < 4; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out numbers[i]))
                return null;
        }
        return new Rect(numbers[0], numbers[1], numbers[2], numbers[3]);
    }
}
