namespace Shturmap.Map;

/// <summary>
/// The wheel's zoom, animated (owner, 2026-10-15): a notch zooms by about a fifth over 0.15 s, eased, where it used to
/// zoom at once. At once, a notch laid the crowded symbols out afresh and could flip them to the other side of their
/// places; over 0.15 s its frames change the zoom by a few per cent each, and the symbols hold steady as in any other
/// move of the view (docs/DESIGN.md "Map drawing", *Steady while the view moves*). A notch while one is under way adds
/// its step to where that one was going and sets off from where that one is, at the speed it has there, so a turn of
/// several notches is one zoom without a jump or a stop, landing 0.15 s after the last notch. The zoom eases on a log
/// scale, as every move of the view does, so a notch in and a notch out look alike. With Windows' animation effects off
/// a notch zooms at once, as before. Since the evening of 2026-10-15 (owner) + and −, the zoom buttons (1.5 times) and a
/// double-click (2 times) zoom the same way over 0.2 s (<see cref="Step"/>), and a press during a zoom under way goes on
/// from it as a notch does. Pure: MapView keeps the point under the pointer (or the view's middle) in place and draws a
/// frame at each step.
/// </summary>
public sealed class WheelZoom
{
    /// <summary>How long a notch takes (owner, 2026-10-15: "about 0.15 s per notch").</summary>
    public static readonly TimeSpan Notch = TimeSpan.FromMilliseconds(150);

    /// <summary>How long a step of + or −, a zoom button or a double-click takes (owner, 2026-10-15, evening: "over 0.2
    /// s, eased").</summary>
    public static readonly TimeSpan Step = TimeSpan.FromMilliseconds(200);

    /// <summary>The zoom a turn of the wheel asks for: 1.0015 to the power of its delta, so a notch (120) is 1.197, about a
    /// fifth more; a wheel that turns smoothly sends smaller deltas, each its share.</summary>
    public static double FactorOf(int wheelDelta) => Math.Pow(1.0015, wheelDelta);

    // The zoom it set off from and the one it goes to, both on a log scale; its speed when it set off (log zoom a second);
    // how long it takes from there.
    private double _from;
    private double _to;
    private double _speed;
    private DateTime _started;
    private TimeSpan _length = Notch;
    private bool _animate;

    /// <summary>A zoom is under way, or one with the effects off still has to be shown.</summary>
    public bool Running { get; private set; }

    /// <summary>The zoom it goes to.</summary>
    public double Target => Math.Exp(_to);

    /// <summary>
    /// A turn of the wheel by <paramref name="factor"/> at the view's zoom <paramref name="zoom"/>, or a step of the keys,
    /// the buttons or a double-click: it goes to that much more than where the zoom under way was going (or than the zoom,
    /// with none), within the zoom limits as <see cref="Camera.ZoomAt"/> keeps them (a view outside them moves toward them
    /// or stays), and sets off from where the zoom under way is at <paramref name="now"/> (the zoom shown, but for the
    /// time since the last frame), at the speed it has there if that is toward the new target, but never so fast that it
    /// would pass it. It lands <paramref name="over"/> later: a notch's 0.15 s (<see cref="Notch"/>) unless given, a
    /// step's 0.2 s (<see cref="Step"/>). With <paramref name="animate"/> off (Windows' animation effects) it is there at
    /// once.
    /// </summary>
    public void Turn(double zoom, double factor, double minZoom, double maxZoom, DateTime now, bool animate = true, TimeSpan? over = null)
    {
        var length = over ?? Notch;
        if (length <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(over), length, "A zoom takes some time; with the animation effects off, animate is false");
        var going = Running && _animate && now - _started < _length;
        var speed = going ? SpeedAt(now) : 0;
        // One not stopped yet drives the view: where it is now, though no frame may have shown it since the last.
        if (Running)
            zoom = At(now);
        var from = Math.Log(zoom);
        var to = Math.Clamp((going ? _to : from) + Math.Log(factor), Math.Log(Math.Min(minZoom, zoom)), Math.Log(Math.Max(maxZoom, zoom)));
        var way = to - from;
        // At three times the way's mean speed a zoom eases out only; any faster and it would pass its target and turn
        // back (a cubic from the speed it has to rest at the target).
        _speed = way == 0 || Math.Sign(speed) != Math.Sign(way) ? 0 : Math.Sign(way) * Math.Min(Math.Abs(speed), 3 * Math.Abs(way) / length.TotalSeconds);
        (_from, _to, _started, _length, _animate) = (from, to, now, length, animate);
        Running = true;
    }

    /// <summary>The zoom to draw at <paramref name="now"/>: on its way, eased (a cubic from the zoom and speed it set off
    /// with to rest at the target; from rest, eased in and out), and the target once its time is up.</summary>
    public double At(DateTime now)
    {
        var s = Progress(now);
        if (!Running || s >= 1)
            return Math.Exp(_to);
        var lead = _speed * _length.TotalSeconds;
        return Math.Exp(_from + (s * s * s - 2 * s * s + s) * lead + (3 * s * s - 2 * s * s * s) * (_to - _from));
    }

    /// <summary>Whether the zoom has landed by <paramref name="now"/>: the frame drawn then is the last.</summary>
    public bool Over(DateTime now) => !Running || Progress(now) >= 1;

    /// <summary>Stops it where it is (the view taken by something else: another map, a fit, a jump).</summary>
    public void Stop() => Running = false;

    // How far through its time it is, 0 to 1; 1 at once with the effects off.
    private double Progress(DateTime now) => _animate ? Math.Clamp((now - _started) / _length, 0, 1) : 1;

    // The speed (log zoom a second) at now: the cubic's slope.
    private double SpeedAt(DateTime now)
    {
        var s = Progress(now);
        if (s >= 1)
            return 0;
        var lead = _speed * _length.TotalSeconds;
        return ((3 * s * s - 4 * s + 1) * lead + (6 * s - 6 * s * s) * (_to - _from)) / _length.TotalSeconds;
    }
}
