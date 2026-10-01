using Spotter.Core;
using Spotter.Core.Maps;

namespace Spotter.Map;

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
    Spotter.Core.Quests.ObjectiveKind? Objective = null);

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
    public IReadOnlySet<string> Focus { get; set; } = new HashSet<string>();

    /// <summary>Where the item the pointer is on spawns as loose loot on this map (shown only while pointing at it).</summary>
    public IReadOnlyList<WorldPoint> Spawns { get; set; } = [];

    public bool ShowLabels { get; set; } = true;
}
