using SkiaSharp;

namespace Shturmap.Map;

/// <summary>How a frame drew an extract or a transit, by what the game's list said of it this raid (docs/DESIGN.md, "Map
/// drawing", *The player's extracts this raid*).</summary>
public enum ExitLook
{
    /// <summary>Solid: no list read, or one that doesn't speak of it.</summary>
    Plain,

    /// <summary>On the list: lit (<see cref="MapScene.ExitsListed"/>).</summary>
    Listed,

    /// <summary>On the list, marked "??:??:??": a "?" in its triangle (<see cref="MapScene.ExitsUnsure"/>).</summary>
    Unsure,

    /// <summary>Left out: struck out (<see cref="MapScene.ExitsNotListed"/>).</summary>
    NotListed,

    /// <summary>A transit, drawn as ever.</summary>
    Transit,
}

/// <summary>
/// What one frame drew of a symbol, for a look at the map's animations frame by frame without a window (owner,
/// 2026-10-15: the QA pass must see animations frame by frame; <c>shturmap-cli frames</c>, ExitRingFramesTests). The
/// renderer fills it as it draws, from the values it draws with (<see cref="MapRenderer.Render"/>'s trace), so it says
/// what the picture shows, not what a second computation thinks it should.
/// </summary>
public sealed class SymbolTrace
{
    internal SymbolTrace(MapRenderer.ShownMarker shown) => Shown = shown;

    /// <summary>The symbol as the frame laid it out: its marker, where it is drawn, its radius, and its true place where
    /// it was set off it (<see cref="MapRenderer.ShownMarker.Leader"/>).</summary>
    public MapRenderer.ShownMarker Shown { get; }

    public string Id => Shown.Marker.Id;

    public string Name => Shown.Marker.Label;

    public MarkerKind Kind => Shown.Marker.Kind;

    /// <summary>Where its centre is drawn, in pixels (<see cref="MapRenderer.ShownMarker.At"/>).</summary>
    public SKPoint At => Shown.At;

    /// <summary>The radius it is drawn at, in pixels (<see cref="MapRenderer.ShownMarker.R"/>): an extract's triangle
    /// reaches 1.2 R up and 1.1 R to either side.</summary>
    public float R => Shown.R;

    /// <summary>How an extract or a transit was drawn; null for every other symbol.</summary>
    public ExitLook? Look { get; internal set; }

    /// <summary>Seconds since its turn came as a list just read reached it, negative before (ArrivalOf); null while it
    /// doesn't ring.</summary>
    public double? Arrival { get; internal set; }

    /// <summary>A lit extract's glow strength (GlowAt): 0 before its turn, swelling to 1.6 and settling at 1; null for
    /// one not lit.</summary>
    public float? Glow { get; internal set; }

    /// <summary>How far each echo of its triangle that was drawn is on its way out (DrawEcho's p, 0 to 1); empty when
    /// none was.</summary>
    public IReadOnlyList<double> Echoes => EchoesDrawn;

    internal readonly List<double> EchoesDrawn = [];

    /// <summary>A "??:??:??" extract's "?" scale (QuestionPop): 0 before its turn, popping in past 1 and settling at 1;
    /// null for one without a "?".</summary>
    public float? Question { get; internal set; }

    /// <summary>Whether its name was drawn in the bold label face (true) or the regular one (false); null where its name
    /// wasn't drawn (it has none, or no side of it was free). A listed extract's turns bold as a list just read lights it,
    /// a picked or pointed-at symbol's is bold (MapRenderer.DrawLabel).</summary>
    public bool? Bold { get; internal set; }

    /// <summary>How far a called extract's icon is on its flight from the roll call's band to its place, 0 at lift-off to
    /// 1 at landing, before easing (<see cref="MapScene.CallOutExits"/>); null while it isn't in the air.</summary>
    public double? Flight { get; internal set; }

    /// <summary>Where the flying icon's centre is drawn, in pixels (the space of <see cref="At"/> and
    /// <see cref="ExitCall.FromX"/>); null while it isn't in the air.</summary>
    public SKPoint? Flying { get; internal set; }

    /// <summary>The flying icon's size against its symbol's: 0.8 at lift-off, lifted to about 1.12 halfway, 1 at landing;
    /// null while it isn't in the air.</summary>
    public float? FlightScale { get; internal set; }

    /// <summary>A linked set's lit place while it breathes (<see cref="LinkedBreath"/>): its halo's radius in pixels and
    /// its opacity in its middle, as drawn; null while it has none (one thing pointed at rings, the static look rings the
    /// collar).</summary>
    public (float Radius, float Alpha)? Halo { get; internal set; }

    /// <summary>A linked set's lit place in its static look: the weight of the ring on its collar, 0 to 1; null while it
    /// has none.</summary>
    public float? Ring { get; internal set; }

    /// <summary>Whether the symbol was laid out and drawn in this frame. False for a called extract whose place is out of
    /// view while its icon flies there: only the flight is drawn, toward where the place is (<see cref="At"/>).</summary>
    public bool InView { get; internal set; } = true;
}
