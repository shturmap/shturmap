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

/// <summary>An extract ringing out as a list just read reached it: its turn, and whether it rang as lit (on the list) or as
/// "??:??:??".</summary>
public readonly record struct ExitRing(DateTime At, bool Listed);

/// <summary>
/// One row of the roll call's band, which the window hands the map so the row's icon can fly to its extract's place
/// (<see cref="MapScene.CallOutExits"/>).
/// </summary>
/// <param name="MarkerId">The extract's marker id.</param>
/// <param name="FromX">Where the icon lifts off: the centre of the row's triangle, in the map panel's pixels, the space
/// <see cref="MapRenderer.Render"/> draws in and <see cref="SymbolTrace.At"/> reports (physical pixels from the panel's top
/// left: a point in the map view's DIPs times its RasterizationScale). The centre is <see cref="MapRenderer"/>'s triangle
/// centre: a tenth of the triangle's height below the middle of its box.</param>
/// <param name="FromY">See <paramref name="FromX"/>.</param>
/// <param name="Turn">Its row's place in the roll call: 0, 1, 2 …, nearest the player first
/// (<see cref="MapScene.ExitsToCall"/>).</param>
public readonly record struct ExitCall(string MarkerId, float FromX, float FromY, int Turn);

/// <summary>An extract's flight from its row in the roll call's band to its place (<see cref="MapScene.CallOutExits"/>).</summary>
/// <param name="FromX">Where it lifts off, in the map panel's pixels (<see cref="ExitCall.FromX"/>).</param>
/// <param name="LiftOff">When its icon leaves its row; it lands <see cref="MapScene.FlightLength"/> later, and its ring
/// starts there (<see cref="Landing"/>).</param>
/// <param name="Turn">Its place in the flights' order: the call's own, or after the last for one a later list added.</param>
/// <param name="Listed">Whether it flies to be lit (on the list) or to carry its "?" ("??:??:??").</param>
public readonly record struct ExitFlight(string MarkerId, float FromX, float FromY, DateTime LiftOff, int Turn, bool Listed)
{
    /// <summary>When it lands on its place, and its ring starts (<see cref="MapScene.ExitsRinging"/>).</summary>
    public DateTime Landing => LiftOff + MapScene.FlightLength;
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

    /// <summary>The last repel's input and how far it set each symbol off its place (<see cref="MapRenderer.Repel"/>):
    /// panning moves every place alike, so the same symbols at the same zoom are set off alike; and while the view
    /// moves, the next repel goes on from it, so nothing jumps from one frame to the next.</summary>
    internal MapRenderer.RepelPlacement? LastRepel { get; set; }

    /// <summary>Where the scale bar stood in the last layout, for which view: while the view only moves it keeps that
    /// place (<see cref="MapRenderer.ScaleBar.Held"/>).</summary>
    internal MapRenderer.ScaleBarPlacement? LastScaleBar { get; set; }

    /// <summary>The scale bar as the last frame drew it (<see cref="MapRenderer.Render"/>; null before one): where it
    /// stood, and whether it held its place while the view moved, so the window draws one more frame for it to settle
    /// in once the view is still (MapView), and the frames tool records where it stood.</summary>
    public MapRenderer.ScaleBar? ScaleBarShown => LastLayout?.Layout.Scale;

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

    /// <summary>
    /// The raid replay on this map while it plays (owner, 2026-10-07; <see cref="RaidReplay"/>): the map recedes, and the
    /// pen is drawn up to <see cref="ReplayMinute"/>. Null otherwise.
    /// </summary>
    public RaidReplay? Replay { get; set; }

    /// <summary>The raid's minute the replay's playhead is at: positions up to it are drawn, and the pen grows toward the next.</summary>
    public double ReplayMinute { get; set; }

    /// <summary>The replay has played to its end: every position, and the last one's LAST SEEN tag.</summary>
    public bool ReplayDone { get; set; }

    /// <summary>DIPs at the view's foot the replay's band covers: the minute tags stay above it.</summary>
    public float ReplayFoot { get; set; }

    /// <summary>How strongly the replay is drawn, 0 to 1: it fades in as its band comes up and out at its end.</summary>
    public float ReplayOpacity { get; set; } = 1;

    /// <summary>
    /// The places of quests the log just reported completed, which ring out in gold with a check and go (owner,
    /// 2026-10-07: "For completed quests, also make a nice animation"), each with when it began. Not with animation
    /// effects off.
    /// </summary>
    public IReadOnlyList<(MapMarker Marker, DateTime Since)> Leaving { get; set; } = [];

    /// <summary>How long a completed quest's places take to ring out and go.</summary>
    public static readonly TimeSpan LeaveLength = TimeSpan.FromSeconds(1.6);

    /// <summary>Whether a completed quest's places are ringing out now.</summary>
    public bool LeavingNow => Pulse && Leaving.Any(l => DateTime.Now - l.Since < LeaveLength);

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
    public bool Pinging => PingingAt(DateTime.Now);

    /// <summary>Whether the newest position pings at a frame's time (<see cref="MapRenderer.Render"/>'s).</summary>
    public bool PingingAt(DateTime now) => PingSince is { } since && now - since < PingLength;

    /// <summary>
    /// While the view glides to a new position (Follow my position): the position is on its way into view, so the edge
    /// badge waits rather than flash for a moment.
    /// </summary>
    public bool Gliding { get; set; }

    /// <summary>
    /// The extracts the game's own list didn't name this raid (marker ids), once that list was read from a screenshot
    /// (owner, 2026-10-05): struck out and faint, their names fainter, so the ones the player can leave by are the solid
    /// ones (hollow until 2026-10-14). Empty while no list was read: then every extract is drawn solid, as outside a raid.
    /// </summary>
    public IReadOnlySet<string> ExitsNotListed
    {
        get => _exitsNotListed;
        set
        {
            _exitsNotListed = value.Count == 0 ? Nothing : value;
            LayoutVersion++;
        }
    }

    private IReadOnlySet<string> _exitsNotListed = Nothing;

    /// <summary>
    /// The extracts the game's own list named this raid without "??:??:??" (marker ids): they are lit, a steady glow in
    /// their colour and their names in bold (owner, 2026-10-05: "highlighted better on the map so it can be seen that
    /// they are active"); a list just read lights each at its turn, its name bold from then on (owner, 2026-10-15;
    /// <see cref="ExitsRinging"/>). Empty while no list was read.
    /// </summary>
    public IReadOnlySet<string> ExitsListed
    {
        get => _exitsListed;
        set
        {
            _exitsListed = value.Count == 0 ? Nothing : value;
            LayoutVersion++;
        }
    }

    private IReadOnlySet<string> _exitsListed = Nothing;

    /// <summary>
    /// The listed extracts the game marked "??:??:??" (marker ids): it may be closed, or it needs something. They
    /// carry the "?" a possible location carries, maybe here, in their triangle (at its corner until 2026-10-14).
    /// </summary>
    public IReadOnlySet<string> ExitsUnsure
    {
        get => _exitsUnsure;
        set
        {
            _exitsUnsure = value.Count == 0 ? Nothing : value;
            LayoutVersion++;
        }
    }

    private IReadOnlySet<string> _exitsUnsure = Nothing;

    /// <summary>
    /// The extracts a list the player just showed rings out (owner, 2026-10-14: "animate the icons for a couple of
    /// seconds so they are visible pre-attentively when the exits are read"), each with its turn and whether it rang
    /// as lit or as "??:??:??" (<see cref="RingExits"/>). Rings that are over stay until the next list.
    /// </summary>
    public IReadOnlyDictionary<string, ExitRing> ExitsRinging => _ringing;

    private Dictionary<string, ExitRing> _ringing = new(StringComparer.Ordinal);

    /// <summary>
    /// A list just read, with <see cref="ExitsListed"/> and <see cref="ExitsUnsure"/> set from it: its extracts ring
    /// out one after another, the lit ones first, each kind nearest you first, for about three seconds. Players take
    /// several screenshots in a run (owner, 2026-10-14: "updated on the fly with the newly added items that come in
    /// after the first animation has been started"): while rings are still going, a later list adds only what it lit
    /// or marked anew, after the last turn, and what is already ringing carries on. Once they are over, a list rings
    /// them all again: the player looked at it again. Not after a roll call in this raid (<paramref name="called"/>;
    /// owner, 2026-10-15, from the design panel: option A, "Roll call, then fly-out"): a list that comes once the roll
    /// call's cue has gone rings only what it lit or marked anew, where it stands, a turn apart, nearest you first, lit and
    /// "??:??:??" alike as the roll call names them; the same list again changes nothing. Until then a list after the
    /// rings rang them all again.
    /// </summary>
    /// <param name="called">What a roll call called in this raid (marker id to lit), or null where none ran in it. The
    /// window keeps it for the raid, not the scene: a look at another map and back makes the scene anew, and the scene's
    /// own record went with it, so the next list got a whole roll call again (coordinator, 2026-10-15).</param>
    public void RingExits(DateTime now, IReadOnlyDictionary<string, bool>? called = null)
    {
        var going = RingsGoing(now);
        var turn = MapRenderer.ExitTurn;
        if (called is not null)
        {
            var at = going ? Max(now, _ringing.Values.Max(r => r.At) + turn) : now;
            foreach (var exit in ExitsToCall())
            {
                var listed = ExitsListed.Contains(exit.Id);
                if ((_ringing.TryGetValue(exit.Id, out var rung) && rung.Listed == listed)
                    || (called.TryGetValue(exit.Id, out var was) && was == listed))
                    continue;
                _ringing[exit.Id] = new ExitRing(at, listed);
                // Rung where it stands: a flight it had (one marked "??:??:??" lit meanwhile) would land off its ring.
                _flights.Remove(exit.Id);
                at += turn;
            }
            return;
        }
        if (!going)
            StartAfresh();
        // A list read at once: its turns from now, the lit ones' few, then the "??:??:??" ones'. One joining a ring
        // still going: the turns after its last.
        var next = going ? Max(now, _ringing.Values.Max(r => r.At) + turn) : now;
        var cap = going ? int.MaxValue : MapRenderer.ExitTurns;
        foreach (var listed in new[] { true, false })
        {
            var turns = 0;
            foreach (var exit in Near(listed ? ExitsListed : ExitsUnsure))
            {
                if (_ringing.TryGetValue(exit.Id, out var ring) && ring.Listed == listed)
                    continue;
                _ringing[exit.Id] = new ExitRing(next + turn * Math.Min(turns, cap - 1), listed);
                // Rung where it stands now: a flight it had (one marked "??:??:??" lit meanwhile) would land off its ring.
                _flights.Remove(exit.Id);
                turns++;
            }
            next += turn * Math.Min(turns, cap);
        }
    }

    // Nearest you first, where there is a you; else in the data's order.
    private IEnumerable<MapMarker> Near(IReadOnlySet<string> ids)
    {
        var these = Markers.Where(m => ids.Contains(m.Id));
        return Player is { } you ? these.OrderBy(m => m.Position.HorizontalDistanceTo(you.Position)) : these;
    }

    // Whether an extract still rings out, flies or waits for its turn at this moment.
    private bool RingsGoing(DateTime now) => _ringing.Values.Any(r => now - r.At < MapRenderer.RingLength(r.Listed));

    // A list with nothing going: what rang before is over, and goes.
    private void StartAfresh()
    {
        _ringing = new Dictionary<string, ExitRing>(StringComparer.Ordinal);
        _flights = new Dictionary<string, ExitFlight>(StringComparer.Ordinal);
    }

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    // ---- the roll call: the band names the list's extracts, then each flies to its place (owner, 2026-10-15, from the
    // design panel: option A, "Roll call, then fly-out") ----

    /// <summary>From the list's reading to the first icon's lift-off: the band has come in and called its rows by then, and
    /// the view has begun to zoom out.</summary>
    public static readonly TimeSpan CallOutLiftOff = TimeSpan.FromSeconds(1.6);

    /// <summary>How far apart the icons lift off, in their turns' order: the rings' own turn (<see cref="MapRenderer.ExitTurn"/>).</summary>
    public static TimeSpan FlightTurn => MapRenderer.ExitTurn;

    /// <summary>How long an icon takes from its row to its extract's place, eased in and out.</summary>
    public static readonly TimeSpan FlightLength = TimeSpan.FromSeconds(0.9);

    /// <summary>When the view begins to zoom out to frame the listed extracts and the player (<see cref="Camera.FittingAll"/>),
    /// from the list's reading; the window moves it with MapView.AnimateView.</summary>
    public static readonly TimeSpan CallOutFitAt = TimeSpan.FromSeconds(1.2);

    /// <summary>How long the view takes to zoom out to that framing.</summary>
    public static readonly TimeSpan CallOutFitLength = TimeSpan.FromSeconds(1.2);

    /// <summary>
    /// The extracts the roll call names, in its order: the list's lit and "??:??:??" ones alike, nearest the player first,
    /// in the data's order without a position. The window lays out the band's rows by it and gives their
    /// <see cref="ExitCall.Turn"/>s from it.
    /// </summary>
    public IReadOnlyList<MapMarker> ExitsToCall()
    {
        var on = new HashSet<string>(ExitsListed, StringComparer.Ordinal);
        on.UnionWith(ExitsUnsure);
        on.ExceptWith(ExitsNotListed);
        return [.. Near(on).Where(m => IsExtract(m.Kind))];
    }

    private static bool IsExtract(MarkerKind kind) => kind is MarkerKind.ExtractPmc or MarkerKind.ExtractScav or MarkerKind.ExtractShared;

    /// <summary>
    /// The extracts flying from the roll call's band to their places, or flown there (<see cref="CallOutExits"/>), by
    /// marker id. Flights that are over stay until the next list that starts afresh.
    /// </summary>
    public IReadOnlyDictionary<string, ExitFlight> ExitFlights => _flights;

    private Dictionary<string, ExitFlight> _flights = new(StringComparer.Ordinal);

    /// <summary>
    /// The map's side of the roll call (owner, 2026-10-15, from the design panel: option A, "Roll call, then fly-out"): a
    /// list just read, with <see cref="ExitsListed"/>, <see cref="ExitsUnsure"/> and <see cref="ExitsNotListed"/> set from
    /// it, and the band's rows. Each called extract's icon lifts off its row <see cref="CallOutLiftOff"/> after
    /// <paramref name="now"/> plus a <see cref="FlightTurn"/> per <see cref="ExitCall.Turn"/>, flies to its place over
    /// <see cref="FlightLength"/>, and lands as its ring starts: the ring <see cref="RingExits"/> rings, at the landing
    /// (<see cref="ExitsRinging"/>). Until then the extract at its place is drawn as before its turn: not lit, no "?". A
    /// call for an extract the list doesn't light or mark (struck out, a transit, one it leaves out), or for one already
    /// ringing, flying or waiting in that state, changes nothing; while flights or rings are going, a later list's new
    /// calls go after the last turn, as <see cref="RingExits"/> adds what a later list lit anew; once all is over, a list
    /// calls afresh (the player looked again). With animation effects off nothing flies and nothing is recorded: the
    /// extracts are simply lit, as with <see cref="RingExits"/>.
    /// </summary>
    /// <returns>The flights it added, in their turns' order: when each row's icon lifts off, for the band to fade the row
    /// as it leaves.</returns>
    public IReadOnlyList<ExitFlight> CallOutExits(IReadOnlyList<ExitCall> calls, DateTime now)
    {
        if (!Pulse)
            return [];
        var going = RingsGoing(now);
        if (!going)
            StartAfresh();
        var markers = Markers.Where(m => IsExtract(m.Kind)).GroupBy(m => m.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var added = new List<ExitFlight>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var call in calls.Select((c, i) => (Call: c, Index: i)).OrderBy(c => c.Call.Turn).ThenBy(c => c.Index).Select(c => c.Call))
        {
            // Struck-out extracts never move, and transits take no part: only what the list lights or marks flies.
            if (!markers.ContainsKey(call.MarkerId) || ExitsNotListed.Contains(call.MarkerId) || !seen.Add(call.MarkerId))
                continue;
            bool listed;
            if (ExitsListed.Contains(call.MarkerId))
                listed = true;
            else if (ExitsUnsure.Contains(call.MarkerId))
                listed = false;
            else
                continue;
            if (_ringing.TryGetValue(call.MarkerId, out var ring) && ring.Listed == listed)
                continue;
            added.Add(new ExitFlight(call.MarkerId, call.FromX, call.FromY, default, Math.Max(0, call.Turn), listed));
        }
        if (added.Count == 0)
            return [];
        // Called at once: each lifts off at its own turn, as its row in the band was called. Joining flights still going:
        // after the last turn, never in the past.
        var flights = _flights.Values.ToList();
        if (flights.Count == 0)
        {
            for (var i = 0; i < added.Count; i++)
                added[i] = added[i] with { LiftOff = now + CallOutLiftOff + FlightTurn * added[i].Turn };
        }
        else
        {
            var last = flights.MaxBy(f => f.LiftOff);
            var next = Max(now, last.LiftOff + FlightTurn);
            var turn = flights.Max(f => f.Turn) + 1;
            for (var i = 0; i < added.Count; i++)
                added[i] = added[i] with { LiftOff = next + FlightTurn * i, Turn = turn + i };
        }
        foreach (var flight in added)
        {
            _flights[flight.MarkerId] = flight;
            _ringing[flight.MarkerId] = new ExitRing(flight.Landing, flight.Listed);
        }
        return added;
    }

    /// <summary>Whether an extract still rings out, or waits for its turn to.</summary>
    public bool ExitsArriving => ExitsArrivingAt(DateTime.Now);

    /// <summary>Whether an extract rings out, or waits for its turn to, at a frame's time (<see cref="MapRenderer.Render"/>'s).</summary>
    public bool ExitsArrivingAt(DateTime now) => Pulse && _ringing.Values.Any(r => now - r.At < MapRenderer.RingLength(r.Listed));

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
