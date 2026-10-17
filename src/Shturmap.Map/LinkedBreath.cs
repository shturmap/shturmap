namespace Shturmap.Map;

/// <summary>
/// The linked highlight's breath (owner, 2026-10-17: the highlight "really helps pre-attentively highlight elements …
/// However, the pre-attentiveness goes away directly after the highlight was done … the highlights are a bit too faint";
/// from a panel of four, "B"; docs/DESIGN.md §4, "Design system", *Motion*). While the pointer lights a linked set (a
/// trader's portrait, a boss's name, an item), the halos of the set's places on the map and the type glyphs of its rows
/// in the rail breathe together: one clock for the map view and the rows, so the two are in phase. The breath stops,
/// settling over <see cref="Ease"/> to the static look, when a click holds what is lit (<see cref="Hold"/>), when the
/// pointer has been still for <see cref="StillAfter"/> (a move starts it again, <see cref="Moved"/>), and it never
/// starts with Windows' animation effects off or in a snapshot (<see cref="Effects"/>). One thing pointed at (a quest,
/// an objective, an extract) keeps its rings and never lights the breath. Times come in from the caller, so a frame
/// drawn at a given time is the same every run.
/// </summary>
public sealed class LinkedBreath
{
    /// <summary>One breath, in and out.</summary>
    public static readonly TimeSpan Cycle = TimeSpan.FromSeconds(2);

    /// <summary>The highlight's ease (*Motion*): the breath starts once the set has lit, and settles to the static look in
    /// as long.</summary>
    public static readonly TimeSpan Ease = TimeSpan.FromSeconds(0.18);

    /// <summary>How long the pointer may rest before the breath stops: the set is found by then.</summary>
    public static readonly TimeSpan StillAfter = TimeSpan.FromSeconds(6);

    /// <summary>The halo's and the glyph's scale, out and in.</summary>
    public const float LowScale = 0.92f, HighScale = 1.08f;

    /// <summary>Their opacity, out and in.</summary>
    public const float LowOpacity = 0.85f, HighOpacity = 1f;

    /// <summary>
    /// The curve: <paramref name="sinceStart"/> after the breath began (the ease-in done), the scale and the opacity. An
    /// eased sine, low at 0, high at half the cycle, low again at its end: 0.92 and 0.85 at 0 s, 1.0 and 0.925 at 0.5 s,
    /// 1.08 and 1.0 at 1 s. Before its start it stands low.
    /// </summary>
    public static (float Scale, float Opacity) At(TimeSpan sinceStart)
    {
        var x = Math.Max(0, sinceStart.TotalSeconds) % Cycle.TotalSeconds / Cycle.TotalSeconds;
        var w = (float)((1 - Math.Cos(2 * Math.PI * x)) / 2);
        return (LowScale + (HighScale - LowScale) * w, LowOpacity + (HighOpacity - LowOpacity) * w);
    }

    /// <summary>Whether it may breathe: Windows' animation effects on and not a snapshot run. Off, every set stands in
    /// its static look from the start (<see cref="Settle"/> is 1).</summary>
    public bool Effects { get; set; } = true;

    /// <summary>Whether the pointer lights a linked set now (<see cref="Light"/>).</summary>
    public bool Lit => _lit is not null;

    /// <summary>When the breath began, or began again after it had stopped: its curve runs from there, after the ease.</summary>
    public DateTime Since { get; private set; }

    private IReadOnlySet<string>? _lit;
    private DateTime _movedAt;
    private DateTime? _heldAt;
    private bool _resumed;
    private float _endSettle = 1;

    /// <summary>
    /// The pointer lights a linked set (what the map puts in focus for it), or none (null, empty). The same set again
    /// changes nothing; another starts a breath of its own.
    /// </summary>
    public void Light(IReadOnlySet<string>? set, DateTime now)
    {
        if (set is not { Count: > 0 })
        {
            if (_lit is not null)
                _endSettle = Settle(now);
            _lit = null;
            return;
        }
        if (_lit is not null && _lit.SetEquals(set))
            return;
        _lit = set;
        Since = now;
        _movedAt = now;
        _heldAt = null;
        _resumed = false;
    }

    /// <summary>
    /// Whether a click holds what is lit (the trader strip's hold, <see cref="MapScene.Held"/>): held, the breath stops;
    /// held before it lit, it never starts; let go under the pointer, it starts again.
    /// </summary>
    public void Hold(bool held, DateTime now)
    {
        if (held)
        {
            _heldAt ??= now;
            return;
        }
        if (_heldAt is null)
            return;
        _heldAt = null;
        if (Lit)
            Restart(now);
    }

    /// <summary>The pointer moved. Once the breath had stopped for the pointer's rest, it starts again: true then, so
    /// the map and the rows draw it on.</summary>
    public bool Moved(DateTime now)
    {
        var rested = Lit && Effects && _heldAt is null && now >= StillFrom + StillAfter;
        _movedAt = now;
        if (!rested)
            return false;
        Restart(now);
        return true;
    }

    private DateTime StillFrom => _movedAt > Since ? _movedAt : Since;

    private void Restart(DateTime now)
    {
        Since = now;
        _movedAt = now;
        _resumed = true;
    }

    /// <summary>
    /// How far it has settled to the static look: 0 breathing, 1 standing still (the halo gone, the collar ring and the
    /// rows' bar there), eased over <see cref="Ease"/> from the moment it stopped. Started again, it eases back to 0. With
    /// nothing lit, where the last set left it.
    /// </summary>
    public float Settle(DateTime now)
    {
        if (!Effects)
            return 1;
        if (!Lit)
            return _endSettle;
        if (_heldAt is { } held)
            return held <= Since ? 1 : Ramp(now - held);
        var still = StillFrom + StillAfter;
        if (now >= still)
            return Ramp(now - still);
        return _resumed ? 1 - Ramp(now - Since) : 0;
    }

    /// <summary>Whether it moves at <paramref name="now"/>: breathing, or settling. Frames are drawn for it only then.</summary>
    public bool Running(DateTime now) => Lit && Effects && Settle(now) < 1;

    /// <summary>The scale and opacity of the halos and glyphs at <paramref name="now"/> (<see cref="At"/>, from
    /// <see cref="Since"/> after the ease).</summary>
    public (float Scale, float Opacity) Frame(DateTime now) => At(now - Since - Ease);

    private static float Ramp(TimeSpan t) => (float)Math.Clamp(t / Ease, 0, 1);
}
