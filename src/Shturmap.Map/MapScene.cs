using Shturmap.Core;
using Shturmap.Core.Maps;

namespace Shturmap.Map;

public enum MarkerKind
{
    Objective,
    ObjectiveDone,
    PossibleLocation,
    ExtractPmc,
    ExtractScav,
    ExtractShared,
    Transit,
}

/// <summary>A point of interest drawn at a fixed screen size.</summary>
/// <param name="Group">Markers of one quest share a group, so selecting a quest highlights all its points.</param>
/// <param name="Objective">For quest markers, what the objective asks; drawn as a glyph inside the marker.</param>
public sealed record MapMarker(string Id, MarkerKind Kind, WorldPoint Position, string Label, string? Group = null,
    Shturmap.Core.Quests.ObjectiveKind? Objective = null);

/// <summary>An area drawn as an outline, e.g. a quest zone or an extract's footprint.</summary>
public sealed record MapZone(string Id, MarkerKind Kind, IReadOnlyList<WorldPoint> Outline, string? Group = null);

public sealed record PlayerFix(WorldPoint Position, double? YawDegrees, DateTime At);

/// <summary>Everything the renderer draws for one map.</summary>
public sealed class MapScene
{
    public MapScene(MapDefinition definition, MapArtwork artwork)
    {
        Definition = definition;
        Artwork = artwork;
        Projection = new MapProjection(definition);
        Placement = Projection.PlaceSvg(artwork.ViewBox.Left, artwork.ViewBox.Top, artwork.ViewBox.Width, artwork.ViewBox.Height);
    }

    public MapDefinition Definition { get; }

    public MapArtwork Artwork { get; }

    public MapProjection Projection { get; }

    public SvgPlacement Placement { get; }

    /// <summary>The floor shown above the base layer, or null for the base layer only.</summary>
    public MapLayer? Floor { get; set; }

    public PlayerFix? Player { get; set; }

    /// <summary>Earlier fixes in this raid, oldest first.</summary>
    public IReadOnlyList<WorldPoint> Trail { get; set; } = [];

    public IReadOnlyList<MapMarker> Markers { get; set; } = [];

    public IReadOnlyList<MapZone> Zones { get; set; } = [];

    /// <summary>A quest group or marker id to emphasise and point to from the player.</summary>
    public string? Selected { get; set; }

    /// <summary>
    /// Quest groups or marker ids the pointer is on somewhere in the window (linked highlighting). While set, these
    /// are drawn emphasised and everything else steps back.
    /// </summary>
    public IReadOnlySet<string> Focus
    {
        get => _focus;
        set
        {
            if (value.Count > 0 && !value.SetEquals(_focus))
                FocusSince = DateTime.Now;
            if (value.Count > 0)
                LastFocus = value;
            _focus = value;
        }
    }

    private IReadOnlySet<string> _focus = new HashSet<string>();

    /// <summary>The last non-empty focus: still emphasised while the dimming fades out.</summary>
    public IReadOnlySet<string> LastFocus { get; private set; } = new HashSet<string>();

    /// <summary>
    /// How far the markers outside the focus have stepped back, 0 to 1. The map view eases it toward 1 while
    /// something is in focus and back to 0 after, so the highlight fades in and out rather than blinking.
    /// </summary>
    public float Dim { get; set; }

    /// <summary>What is drawn emphasised: the focus, or while the dimming fades out, the focus that just ended.</summary>
    public IReadOnlySet<string> ShownFocus => _focus.Count > 0 ? _focus : Dim > 0 ? LastFocus : _focus;

    /// <summary>When the current focus began: its pulse starts from there.</summary>
    public DateTime FocusSince { get; private set; }

    /// <summary>Focused markers pulse so the eye finds them at once; off when Windows' animation effects are off.</summary>
    public bool Pulse { get; set; } = true;

    /// <summary>Where the item the pointer is on spawns as loose loot on this map (shown only while pointing at it).</summary>
    public IReadOnlyList<WorldPoint> Spawns { get; set; } = [];

    public bool ShowLabels { get; set; } = true;
}
