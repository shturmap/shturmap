using System.Globalization;

namespace Shturmap.App.Rules;

/// <summary>
/// Where the main window stood, kept between runs (owner, 2026-10-04: remember the window's monitor and size): the
/// monitor it was on, its bounds while not maximised, and whether it was maximised. It comes back there only if
/// that monitor is still connected in the same place; otherwise the first start's rule applies again (the first
/// monitor that isn't the primary one, maximised). Everything is in screen pixels.
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

    /// <summary>A window's size when nothing better is known (one monitor, a first start).</summary>
    public const int DefaultWidth = 1600;

    /// <inheritdoc cref="DefaultWidth"/>
    public const int DefaultHeight = 1000;

    // How much of the window's top edge must be on the monitor for it to be grabbed and moved, and how far a new
    // window stands in from the monitor's corner.
    private const int Grab = 120;
    private const int Inset = 40;
    private const int Border = 16;

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
