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

    /// <summary>AI Scav spawns, at the centroid of a group of a zone's points: small and quiet, no label.</summary>
    ScavSpawn,

    /// <summary>A boss's spawns, at the centroid of a group of a zone's points: red, labelled with the boss and its chances.</summary>
    BossSpawn,

    /// <summary>Sniper Scav spawns, at the centroid of a group of a zone's points: a hollow hexagon.</summary>
    SniperSpawn,

    /// <summary>A locked door or car trunk (tarkov.dev's locks): a padlock, labelled with its key's short name.</summary>
    Lock,

    /// <summary>A switch the game knows (power, alarms, elevator buttons, trap switches): a power symbol with its name.</summary>
    Switch,

    /// <summary>A hazard tarkov.dev outlines as such (Labyrinth's traps): a hatched outline, drawn as a zone.</summary>
    Hazard,
}

/// <summary>A point of interest drawn at a fixed screen size.</summary>
/// <param name="Group">Markers of one quest share a group, so selecting a quest highlights all its points.</param>
/// <param name="Objective">For quest markers, what the objective asks; drawn as a glyph inside the marker.</param>
/// <param name="Optional">A quest marker of an objective tarkov.dev marks optional: the quest can be finished without it.</param>
public sealed record MapMarker(string Id, MarkerKind Kind, WorldPoint Position, string Label, string? Group = null,
    Shturmap.Core.Quests.ObjectiveKind? Objective = null, bool Optional = false);

/// <summary>An area drawn as an outline, e.g. a quest zone or an extract's footprint.</summary>
public sealed record MapZone(string Id, MarkerKind Kind, IReadOnlyList<WorldPoint> Outline, string? Group = null);

public sealed record PlayerFix(WorldPoint Position, double? YawDegrees, DateTime At);

/// <summary>Everything the renderer draws for one map.</summary>
public sealed class MapScene
{
    /// <param name="artwork">Null for a map without usable artwork: the renderer draws a schematic sheet instead.</param>
    public MapScene(MapDefinition definition, MapArtwork? artwork)
    {
        Definition = definition;
        Artwork = artwork;
        Projection = MapProjection.For(definition);
        if (artwork is not null)
            Placement = Projection.PlaceSvg(artwork.ViewBox.Left, artwork.ViewBox.Top, artwork.ViewBox.Width, artwork.ViewBox.Height);
    }

    public MapDefinition Definition { get; }

    public MapArtwork? Artwork { get; }

    public MapProjection Projection { get; }

    /// <summary>Where the artwork sits in map units; default when there is no artwork.</summary>
    public SvgPlacement Placement { get; }

    /// <summary>The floor shown above the base layer, or null for the base layer only.</summary>
    public MapLayer? Floor { get; set; }

    /// <summary>The map's floors with artwork of their own, top first (the base layer as null); empty without floors.</summary>
    public IReadOnlyList<MapLayer?> FloorStack => _floorStack ??= FloorResolver.Stack(Definition);

    private IReadOnlyList<MapLayer?>? _floorStack;

    public PlayerFix? Player { get; set; }

    /// <summary>Earlier fixes in this raid, oldest first.</summary>
    public IReadOnlyList<WorldPoint> Trail { get; set; } = [];

    public IReadOnlyList<MapMarker> Markers
    {
        get => _markers;
        set
        {
            _markers = value;
            _focusShown = null;
        }
    }

    private IReadOnlyList<MapMarker> _markers = [];

    public IReadOnlyList<MapZone> Zones
    {
        get => _zones;
        set
        {
            _zones = value;
            _focusShown = null;
        }
    }

    private IReadOnlyList<MapZone> _zones = [];

    // Whether any marker or zone belongs to these quest groups or marker ids. A highlight with nothing on this map
    // (a quest kept from another map, a quest for any map) must not grey out everything else.
    private bool Shows(IReadOnlySet<string> ids) =>
        ids.Count > 0 &&
        (_markers.Any(m => ids.Contains(m.Id) || (m.Group is not null && ids.Contains(m.Group))) ||
         _zones.Any(z => z.Group is not null && ids.Contains(z.Group)));

    private bool? _focusShown;

    private bool FocusShown => _focusShown ??= Shows(_focus);

    /// <summary>
    /// The quests picked for the coming raid (quest groups, or marker ids): drawn in the kept look (cyan, larger,
    /// ringed) with a line from the player to the nearest of their places, for as long as they are picked. Unlike
    /// what the pointer is on, picks never make anything else step back: every other quest marker stays at full
    /// strength (owner, 2026-10-03: "Still it should show all other quest markers").
    /// </summary>
    public IReadOnlySet<string> Kept
    {
        get => _kept;
        set => _kept = value.Count == 0 ? Nothing : value;
    }

    private IReadOnlySet<string> _kept = Nothing;

    /// <summary>Whether something pointed at is highlighted on this map (picks alone don't count: they dim nothing).</summary>
    public bool HasHighlight => FocusShown;

    /// <summary>
    /// Quest groups or marker ids the pointer is on somewhere in the window (linked highlighting). While set, these
    /// are drawn emphasised and everything else steps back.
    /// </summary>
    public IReadOnlySet<string> Focus
    {
        get => _focus;
        set
        {
            var same = value.SetEquals(_focus);
            _focus = value;
            _focusShown = null;
            if (FocusShown && !same)
                FocusSince = DateTime.Now;
            if (FocusShown)
                LastFocus = value;
        }
    }

    private IReadOnlySet<string> _focus = new HashSet<string>();

    /// <summary>The last non-empty focus: still emphasised while the dimming fades out.</summary>
    public IReadOnlySet<string> LastFocus { get; private set; } = new HashSet<string>();

    /// <summary>
    /// How far the markers outside the focus have stepped back, 0 to 1. The map view eases it toward 1 while
    /// something is in focus and back to 0 after, so the highlight fades in and out rather than blinking. How far
    /// each kind steps back at 1 is <see cref="MapRenderer.StepBackOf"/>.
    /// </summary>
    public float Dim { get; set; }

    /// <summary>
    /// The player is in a raid on this map. Markers outside the focus then step back less than while planning:
    /// extracts, nearby objectives and bosses still matter at a glance.
    /// </summary>
    public bool InRaid { get; set; }

    /// <summary>
    /// What is drawn emphasised: what the pointer is on, else (while the dimming fades out) what was pointed at last.
    /// Picks (<see cref="Kept"/>) have their own look and are never part of it.
    /// </summary>
    public IReadOnlySet<string> ShownFocus =>
        FocusShown ? _focus : Dim > 0 ? LastFocus : Nothing;

    private static readonly IReadOnlySet<string> Nothing = new HashSet<string>();

    /// <summary>When the current focus began: its pulse starts from there.</summary>
    public DateTime FocusSince { get; private set; }

    /// <summary>Focused markers pulse so the eye finds them at once; off when Windows' animation effects are off.</summary>
    public bool Pulse { get; set; } = true;

    /// <summary>
    /// Whether the emphasised markers pulse now: all the while the pointer is on something. Picks hold still: a pick
    /// pulsing all raid would be motion at the edge of the player's eye.
    /// </summary>
    public bool Pulsing => Pulse && FocusShown;

    /// <summary>How long a new position pings: rings leave the player marker, or its edge arrow when out of view.</summary>
    public static readonly TimeSpan PingLength = TimeSpan.FromSeconds(2.6);

    /// <summary>
    /// When a new position arrived (owner, 2026-10-01: a screenshot must not move the map; it shows where you are
    /// instead). Null when the position isn't new, e.g. an old screenshot found at start.
    /// </summary>
    public DateTime? PingSince { get; set; }

    /// <summary>Whether the newest position is still pinging.</summary>
    public bool Pinging => PingSince is { } since && DateTime.Now - since < PingLength;

    /// <summary>Where the item the pointer is on spawns as loose loot on this map (shown only while pointing at it).</summary>
    public IReadOnlyList<WorldPoint> Spawns { get; set; } = [];

    /// <summary>
    /// Where the map's loot containers stand (tarkov.dev's positions). Drawn only on a sheet, as faint dots: real points
    /// that sketch rooms and corridors where no artwork does. With artwork they would be clutter.
    /// </summary>
    public IReadOnlyList<WorldPoint> Containers { get; set; } = [];

    /// <summary>The map is drawn as a sheet (no artwork): container dots show, and landmarks show at any zoom.</summary>
    public bool IsSheet => Artwork is null;

    public bool ShowLabels { get; set; } = true;
}
