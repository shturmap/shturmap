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

    /// <summary>Where AI Scavs spawn: small and quiet, no label.</summary>
    ScavSpawn,

    /// <summary>Where a boss can spawn: red, labelled with the boss and its chance.</summary>
    BossSpawn,
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

    public IReadOnlyList<MapMarker> Markers
    {
        get => _markers;
        set
        {
            _markers = value;
            _focusShown = _selectedShown = null;
        }
    }

    private IReadOnlyList<MapMarker> _markers = [];

    public IReadOnlyList<MapZone> Zones
    {
        get => _zones;
        set
        {
            _zones = value;
            _focusShown = _selectedShown = null;
        }
    }

    private IReadOnlyList<MapZone> _zones = [];

    // Whether any marker or zone belongs to these quest groups or marker ids. A highlight with nothing on this map
    // (a quest kept from another map, a quest for any map) must not grey out everything else.
    private bool Shows(IReadOnlySet<string> ids) =>
        ids.Count > 0 &&
        (_markers.Any(m => ids.Contains(m.Id) || (m.Group is not null && ids.Contains(m.Group))) ||
         _zones.Any(z => z.Group is not null && ids.Contains(z.Group)));

    private bool? _focusShown, _selectedShown;

    private bool FocusShown => _focusShown ??= Shows(_focus);

    private bool SelectedShown => _selectedShown ??= _selected is not null && Shows(_selectedSet);

    /// <summary>
    /// The quest the player clicked to keep highlighted: drawn emphasised and pulsing, everything else dimmed, with a
    /// line from the player to its nearest point, until it is clicked again. Pointing at something else shows that
    /// instead for as long as the pointer is on it.
    /// </summary>
    public string? Selected
    {
        get => _selected;
        set
        {
            if (value == _selected)
                return;
            if (value is not null && !FocusShown)
                FocusSince = DateTime.Now;
            if (value is null && SelectedShown)
                LastFocus = _selectedSet;
            _selected = value;
            _selectedSet = value is null ? new HashSet<string>() : new HashSet<string> { value };
            _selectedShown = null;
        }
    }

    private string? _selected;
    private IReadOnlySet<string> _selectedSet = new HashSet<string>();

    /// <summary>Whether anything on this map is highlighted: something pointed at, or a quest kept highlighted.</summary>
    public bool HasHighlight => FocusShown || SelectedShown;

    /// <summary>
    /// Quest groups or marker ids the pointer is on somewhere in the window (linked highlighting). While set, these
    /// are drawn emphasised and everything else steps back.
    /// </summary>
    public IReadOnlySet<string> Focus
    {
        get => _focus;
        set
        {
            var wasShown = FocusShown;
            var same = value.SetEquals(_focus);
            _focus = value;
            _focusShown = null;
            if (FocusShown && !same)
                FocusSince = DateTime.Now;
            else if (!FocusShown && wasShown && SelectedShown)
                FocusSince = DateTime.Now; // back to the kept quest: it pulses again
            if (FocusShown)
                LastFocus = value;
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

    /// <summary>
    /// What is drawn emphasised: what the pointer is on, else the kept quest, else (while the dimming fades out) what
    /// was highlighted last.
    /// </summary>
    public IReadOnlySet<string> ShownFocus =>
        FocusShown ? _focus : SelectedShown ? _selectedSet : Dim > 0 ? LastFocus : Nothing;

    private static readonly IReadOnlySet<string> Nothing = new HashSet<string>();

    /// <summary>When the current focus began: its pulse starts from there.</summary>
    public DateTime FocusSince { get; private set; }

    /// <summary>Focused markers pulse so the eye finds them at once; off when Windows' animation effects are off.</summary>
    public bool Pulse { get; set; } = true;

    /// <summary>How often a kept quest pulses when it is picked, or when the pointer comes back to it; then it holds still.</summary>
    public const int KeptPulses = 3;

    /// <summary>
    /// Whether the emphasised markers pulse now: all the while the pointer is on something, and a few times for a kept
    /// quest. A kept quest pulsing for as long as it is kept would be motion at the edge of the player's eye all raid.
    /// </summary>
    public bool Pulsing => Pulse && (FocusShown || (SelectedShown && DateTime.Now - FocusSince < MapRenderer.PulsePeriod * KeptPulses));

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

    public bool ShowLabels { get; set; } = true;
}
