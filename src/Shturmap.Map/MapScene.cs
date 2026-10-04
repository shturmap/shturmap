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
    /// <param name="artwork">Null for a map without SVG artwork.</param>
    /// <param name="tiles">tarkov.dev's tile render for a map without SVG artwork (The Lab, Labyrinth, Icebreaker), or
    /// null. With neither, or while the tiles can't be had, the renderer draws a schematic sheet.</param>
    public MapScene(MapDefinition definition, MapArtwork? artwork, MapTiles? tiles = null)
    {
        Definition = definition;
        Artwork = artwork;
        Tiles = artwork is null ? tiles : null;
        Projection = MapProjection.For(definition);
        if (artwork is not null)
            Placement = Projection.PlaceSvg(artwork.ViewBox.Left, artwork.ViewBox.Top, artwork.ViewBox.Width, artwork.ViewBox.Height);
    }

    public MapDefinition Definition { get; }

    public MapArtwork? Artwork { get; }

    /// <summary>The map's tile render, for maps tarkov.dev publishes only as tiles; null otherwise.</summary>
    public MapTiles? Tiles { get; }

    /// <summary>
    /// Whether the map is drawn as the grid sheet now: no SVG artwork, and no tile render (or none to be had). On a sheet
    /// the container dots show, and landmarks at any zoom; over artwork or tiles they would be clutter.
    /// </summary>
    public bool IsSheet => Artwork is null && Tiles is not { Status: not TileStatus.Unavailable };

    public MapProjection Projection { get; }

    /// <summary>Where the artwork sits in map units; default when there is no artwork.</summary>
    public SvgPlacement Placement { get; }

    /// <summary>
    /// Counts the changes to what a frame's layout is made from (markers, zones, the player, the picks, the focus, the
    /// floor, labels on or off), so that the renderer can keep the last layout while it stands
    /// (<see cref="MapRenderer.LayoutOf"/>). Every setter that feeds the layout counts; a new one must too.
    /// </summary>
    internal int LayoutVersion { get; private set; }

    /// <summary>The last frame's layout with what it was made from (<see cref="MapRenderer.LayoutOf"/>).</summary>
    internal (MapRenderer.LayoutKey Key, MapRenderer.MapLayout Layout)? LastLayout { get; set; }

    /// <summary>The floor shown above the base layer, or null for the base layer only.</summary>
    public MapLayer? Floor
    {
        get => _floor;
        set
        {
            _floor = value;
            LayoutVersion++;
        }
    }

    private MapLayer? _floor;

    /// <summary>The map's floors with artwork of their own, top first (the base layer as null); empty without floors.</summary>
    public IReadOnlyList<MapLayer?> FloorStack => _floorStack ??= FloorResolver.Stack(Definition);

    private IReadOnlyList<MapLayer?>? _floorStack;

    public PlayerFix? Player
    {
        get => _player;
        set
        {
            _player = value;
            LayoutVersion++;
        }
    }

    private PlayerFix? _player;

    /// <summary>Earlier fixes in this raid, oldest first.</summary>
    public IReadOnlyList<WorldPoint> Trail { get; set; } = [];

    public IReadOnlyList<MapMarker> Markers
    {
        get => _markers;
        set
        {
            _markers = value;
            _focusShown = null;
            LayoutVersion++;
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
            // No zone is laid out, but whether the focus shows on this map can turn on one.
            LayoutVersion++;
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
        set
        {
            _kept = value.Count == 0 ? Nothing : value;
            _keptKeys = KeysOf(_kept);
            LayoutVersion++;
        }
    }

    private IReadOnlySet<string> _kept = Nothing;

    /// <summary>
    /// Which of the picks' colours each picked quest has (<see cref="MapRenderer.PickColors"/>); a pick that isn't in
    /// here has the first. Each pick has a colour of its own, so its places are told from the other picks' at a
    /// glance (owner, 2026-10-04).
    /// </summary>
    public IReadOnlyDictionary<string, int> PickSlots
    {
        get => _pickSlots;
        set
        {
            _pickSlots = value;
            LayoutVersion++;
        }
    }

    private IReadOnlyDictionary<string, int> _pickSlots = new Dictionary<string, int>();

    /// <summary>The pick a lock's key is for: of the picks that need it here, the one with the first colour.</summary>
    public string? PickOfKey(string keyGroup) =>
        _kept.Where(q => _questKeys.TryGetValue(q, out var keys) && keys.Contains(keyGroup))
            .OrderBy(q => _pickSlots.GetValueOrDefault(q)).ThenBy(q => q, StringComparer.Ordinal).FirstOrDefault();

    /// <summary>
    /// The locks of the keys the picked quests need on this map (<see cref="QuestKeys"/>): lit in the picks' colour
    /// with their key's name at every zoom, so the door reads as part of the pick (owner, 2026-10-03: Golden Swag's
    /// trailer park cabin). They are a means, not a goal: the guide line never leads to one.
    /// </summary>
    public IReadOnlySet<string> KeptKeys => _keptKeys;

    private IReadOnlySet<string> _keptKeys = Nothing;

    /// <summary>
    /// Per quest, the lock groups ("key:&lt;item&gt;") of the keys it needs on this map
    /// (<see cref="MapContent.QuestKeys"/>). A quest picked or pointed at lights them with its own markers.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> QuestKeys
    {
        get => _questKeys;
        set
        {
            _questKeys = value;
            _keptKeys = KeysOf(_kept);
            Focus = _focusAsked;
            LayoutVersion++;
        }
    }

    private IReadOnlyDictionary<string, IReadOnlyList<string>> _questKeys = new Dictionary<string, IReadOnlyList<string>>();

    // The lock groups of the keys the given quests need here.
    private IReadOnlySet<string> KeysOf(IReadOnlySet<string> quests)
    {
        if (quests.Count == 0 || _questKeys.Count == 0)
            return Nothing;
        var keys = quests.Where(_questKeys.ContainsKey).SelectMany(q => _questKeys[q]).ToHashSet(StringComparer.Ordinal);
        return keys.Count == 0 ? Nothing : keys;
    }

    /// <summary>Whether something pointed at is highlighted on this map (picks alone don't count: they dim nothing).</summary>
    public bool HasHighlight => FocusShown;

    /// <summary>
    /// Quest groups or marker ids the pointer is on somewhere in the window (linked highlighting). While set, these
    /// are drawn emphasised and everything else steps back. A quest brings the locks of its keys along
    /// (<see cref="QuestKeys"/>).
    /// </summary>
    public IReadOnlySet<string> Focus
    {
        get => _focus;
        set
        {
            _focusAsked = value;
            var keys = KeysOf(value);
            IReadOnlySet<string> shown = keys.Count == 0 ? value : value.Concat(keys).ToHashSet(StringComparer.Ordinal);
            var same = shown.SetEquals(_focus);
            _focus = shown;
            _focusShown = null;
            LayoutVersion++;
            if (FocusShown && !same)
                FocusSince = DateTime.Now;
            if (FocusShown)
            {
                LastFocus = shown;
                _lastFocusObjective = _focusObjective;
            }
        }
    }

    private IReadOnlySet<string> _focus = new HashSet<string>();

    // The focus as it was asked for, before the locks of its quests' keys were added.
    private IReadOnlySet<string> _focusAsked = new HashSet<string>();

    /// <summary>The last non-empty focus: still emphasised while the dimming fades out.</summary>
    public IReadOnlySet<string> LastFocus { get; private set; } = new HashSet<string>();

    /// <summary>
    /// The objective the pointer is on, within the quest in <see cref="Focus"/> (its id, as its markers' ids carry
    /// it), or null when the pointer is on the quest as a whole. With one, only that objective's places pulse and
    /// take the pointed-at size; the quest's other places stay lit and hold still.
    /// </summary>
    public string? FocusObjective
    {
        get => _focusObjective;
        set
        {
            if (value == _focusObjective)
                return;
            _focusObjective = value;
            LayoutVersion++;
            if (!FocusShown)
                return;
            // Another objective of the same quest: its pulse starts anew, as a new focus's does.
            FocusSince = DateTime.Now;
            _lastFocusObjective = value;
        }
    }

    private string? _focusObjective;

    // The objective of the last focus shown: its places keep their size while the dimming fades out.
    private string? _lastFocusObjective;

    /// <summary>The objective marked within what is drawn emphasised (<see cref="ShownFocus"/>), or null.</summary>
    public string? ShownFocusObjective => FocusShown ? _focusObjective : Dim > 0 ? _lastFocusObjective : null;

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
    /// Whether the emphasised markers pulse now: all the while the pointer is on something (with an objective pointed
    /// at, only that objective's places: <see cref="FocusObjective"/>). Picks hold still: a pick pulsing all raid
    /// would be motion at the edge of the player's eye.
    /// </summary>
    public bool Pulsing => Pulse && FocusShown;

    /// <summary>How long a new position pings: rings leave the player marker, or its edge arrow when out of view.</summary>
    public static readonly TimeSpan PingLength = TimeSpan.FromSeconds(2.6);

    /// <summary>
    /// When a new position arrived: it shows where you are, and moves the map only while Follow my position is on
    /// (owner, 2026-10-01 and 2026-10-03). Null when the position isn't new, e.g. an old screenshot found at start.
    /// </summary>
    public DateTime? PingSince { get; set; }

    /// <summary>Whether the newest position is still pinging.</summary>
    public bool Pinging => PingSince is { } since && DateTime.Now - since < PingLength;

    /// <summary>
    /// While the view glides to a new position (Follow my position): the position is on its way into view, so the edge
    /// badge waits rather than flash for a moment.
    /// </summary>
    public bool Gliding { get; set; }

    /// <summary>Where the item the pointer is on spawns as loose loot on this map (shown only while pointing at it).</summary>
    public IReadOnlyList<WorldPoint> Spawns { get; set; } = [];

    /// <summary>
    /// Where the map's loot containers stand (tarkov.dev's positions). Drawn only on a sheet, as faint dots: real points
    /// that sketch rooms and corridors where no artwork does. With artwork they would be clutter.
    /// </summary>
    public IReadOnlyList<WorldPoint> Containers { get; set; } = [];

    public bool ShowLabels
    {
        get => _showLabels;
        set
        {
            _showLabels = value;
            LayoutVersion++;
        }
    }

    private bool _showLabels = true;
}
