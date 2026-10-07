using Shturmap.Core;
using Shturmap.Core.Logs;
using Shturmap.Core.Maps;
using Shturmap.Core.Navigation;
using Shturmap.Core.Quests;
using Shturmap.Core.Raid;
using Shturmap.Data.TarkovDev;
using Shturmap.Game.Install;
using Shturmap.Map;

namespace Shturmap.Session;

/// <summary>A one-line message for the user and how long it stays.</summary>
/// <param name="OffersReport">The notice asks the player to report it: the UI offers the Report dialog (docs/DESIGN.md §8, "Reports").</param>
public sealed record SessionNotice(string Text, TimeSpan Duration, bool OffersReport = false);

public enum CueKind
{
    RaidLoading,
    Transit,
    ScavRaid,
    RaidOver,

    /// <summary>The game went back to the menus before the raid began (matching cancelled).</summary>
    LoadCancelled,

    /// <summary>The group's leader picked a raid; its map is shown before loading starts.</summary>
    GroupPick,
}

/// <summary>A change of view the app makes on its own, announced big in the middle of the map for a few seconds.</summary>
/// <param name="RaidLength">How long the raid lasted (raid over).</param>
/// <param name="Kit">For a raid loading or a group's pick, the first items of the kit reminder, pictured under the map's
/// name (<see cref="Planning.CueKit"/>); <paramref name="KitMore"/> says how many more ("+3").</param>
/// <param name="Replay">At a raid's end, its replay (<see cref="RaidReplay"/>); the cue plays it when it
/// <see cref="RaidReplay.Plays"/>.</param>
public sealed record ViewCue(CueKind Kind, string MapName, TimeSpan? RaidLength = null, IReadOnlyList<CueItem>? Kit = null, int KitMore = 0,
    RaidReplay? Replay = null);

/// <summary>An item pictured in the big cue: its picture, or its kind's glyph where there is none.</summary>
/// <param name="ForPick">Needed by a quest picked for this raid: the cue shows these first, framed in the pick's colour.</param>
/// <param name="PickSlot">The colour of the first pick that needs it (<see cref="QuestPicks.Slots"/>).</param>
/// <param name="Count">How many of it are needed; the cue says it on the picture from two up.</param>
public sealed record CueItem(string ItemId, Shturmap.Core.Planning.RequirementKind Kind, bool ForPick = false, int PickSlot = 0, int Count = 1);

/// <summary>How one input is doing, for the status chips: "Logs ✓", "Screenshots ✓", "Data 1 h ago".</summary>
public sealed record SourceHealth(bool Ok, string Text);

/// <summary>An objective on the rail's map (the raid's own in a raid, else the shown one), measured from the last position fix.</summary>
/// <param name="HeightDifference">Metres above (+) or below (−) the player, when it matters (over 3 m).</param>
/// <param name="Needs">Keys or items this objective needs, e.g. "Key: Dorm room 114 key", or null.</param>
/// <param name="MapBearing">Degrees clockwise from map-up, from the last fix; stays true when the facing goes stale.</param>
/// <param name="TraderId">The quest giver, for the portrait.</param>
/// <param name="NeedKey">The key <paramref name="Needs"/> stands for, when it names exactly one (<see cref="Planning.NeedKey"/>).</param>
public sealed record ObjectiveView(
    string QuestId,
    string QuestName,
    string Trader,
    string ObjectiveId,
    string Text,
    bool Done,
    bool HasPlace,
    double? Distance,
    RelativeDirection? Direction,
    double? HeightDifference,
    ObjectiveKind Kind,
    string? Needs,
    double? MapBearing = null,
    string? TraderId = null,
    string? NeedKey = null)
{
    /// <summary>
    /// The objective in a few words, as Plan's synopsis says it ("Mark Stryker"), for the raid card's lines; null
    /// when the data isn't in English (<see cref="Planning.ObjectiveSynopses"/>): then <see cref="Text"/> stands.
    /// </summary>
    public string? Short { get; init; }

    /// <summary>
    /// What the hand-over mark's tooltip says ("Hand over to Therapist"), when a hand-over of the quest gives what this
    /// objective gets (<see cref="Shturmap.Data.TarkovDev.Handovers"/>); empty otherwise. It goes to the quest's
    /// trader, <see cref="TraderId"/>.
    /// </summary>
    public string Handover { get; init; } = "";

    /// <summary>How many of what it gets go to the trader after the raid; 0 without a hand-over.</summary>
    public int HandoverCount { get; init; }

    /// <summary>The item it is about (<see cref="QuestCards.ItemOf"/>), pictured beside a hand-over's mark; or null.</summary>
    public string? ItemId { get; init; }
}

/// <summary>The raid at a glance: length, bosses, the in-raid time of day from the last screenshot.</summary>
public sealed record RaidInfo(int RaidMinutes, IReadOnlyList<string> Bosses, double? ClockHours);

/// <summary>The raid that just ended, for one line in Plan (<see cref="RaidStatus.LastRaid"/> words it).</summary>
/// <param name="Duration">How long it ran, when <see cref="LengthKnown"/>; zero otherwise.</param>
/// <param name="EndedAt">When the log ended it; for an end the log never told, the last the log said of it.</param>
public sealed record LastRaidView(string MapName, TimeSpan Duration, RaidSide Side, DateTime EndedAt)
{
    /// <summary>
    /// False for a raid whose end never reached the log (the game was closed or crashed in it): it is over, but
    /// nothing says how long it ran, so no length is shown (review of 2026-10-04).
    /// </summary>
    public bool LengthKnown { get; init; } = true;
}

/// <param name="MapBearing">Degrees clockwise from map-up, from the last fix; stays true when the facing goes stale.</param>
/// <param name="Needs">What it takes to leave here ("Pay 5,000 ₽"), or empty.</param>
/// <param name="NeedItemId">An item to hand over, to picture next to it.</param>
public sealed record ExtractView(string Id, string Name, MarkerKind Kind, double? Distance, RelativeDirection? Direction, double? MapBearing = null,
    string Needs = "", string? NeedItemId = null)
{
    /// <summary>Whether the game's own list names it this raid, once a screenshot showed that list (<see cref="ExitState"/>).</summary>
    public ExitState State { get; init; }
}

/// <summary>Everything the UI shows, replaced as a whole whenever something changes.</summary>
public sealed record SessionSnapshot
{
    public RaidState Raid { get; init; } = new();

    /// <summary>Whether the logs told the raid's side; if not, the side is the player's say (or unknown, shown as PMC).</summary>
    public bool SideFromLogs { get; init; }

    public GameMode Mode { get; init; } = GameMode.Pve;

    /// <summary>What the game's log said about the mode, for the status bar's tooltip.</summary>
    public ModeReading ModeReading { get; init; } = new();

    /// <summary>
    /// The map on screen: the raid's map while in a raid, unless the player looks at another one (the MAP list; the
    /// next position brings the raid's back), otherwise the last raid's or the one picked. What the map view draws
    /// (<see cref="Definition"/>, <see cref="Content"/>, <see cref="Fix"/>, <see cref="Trail"/>, <see cref="Floor"/>)
    /// is this map's.
    /// </summary>
    public MapIdentity? Map { get; init; }

    /// <summary>
    /// The raid's own map while one loads or runs, as the game's log names it; null in the menus, and in a raid on a
    /// map the data doesn't know (<see cref="RaidMapUnknown"/>). What the rail and the status words say in a raid
    /// (<see cref="Objectives"/>, <see cref="Extracts"/>, <see cref="MapPlan"/>, <see cref="RaidInfo"/>,
    /// <see cref="RaidFix"/>) is this map's, whatever map is on screen.
    /// </summary>
    public MapIdentity? RaidMap { get; init; }

    /// <summary>A raid loads or runs on a map the data doesn't know: no map is named for it and no position is plotted.</summary>
    public bool RaidMapUnknown => Raid.Phase != RaidPhase.Menu && RaidMap is null;

    /// <summary>
    /// In a raid, another map than the raid's is on screen (picked in the MAP list): the rail stays the raid's, the map
    /// shows no "you", and the next position brings the raid's map back.
    /// </summary>
    public bool LooksAtAnotherMap => Raid.Phase != RaidPhase.Menu && RaidMap is not null && Map is not null && RaidMap.Id != Map.Id;

    public MapDefinition? Definition { get; init; }

    /// <summary>The last position as the map draws it: only while the map it was taken on is on screen.</summary>
    public PlayerFix? Fix { get; init; }

    public IReadOnlyList<WorldPoint> Trail { get; init; } = [];

    /// <summary>The floor of <see cref="Fix"/> on the shown map.</summary>
    public MapLayer? Floor { get; init; }

    /// <summary>
    /// The last position in this raid, whatever map is on screen: what the rail's distances, the fix's age and the
    /// status bar's "fix … ago" are about. (With no game logs to follow, the last position on the shown map.)
    /// </summary>
    public PlayerFix? RaidFix { get; init; }

    /// <summary>The floor of <see cref="RaidFix"/> on its map.</summary>
    public MapLayer? RaidFloor { get; init; }

    public GameData? Data { get; init; }

    /// <summary>Where items come from; arrives a little after <see cref="Data"/>.</summary>
    public ItemSources? Sources { get; init; }

    public IReadOnlyDictionary<string, QuestStatus> Quests { get; init; } = new Dictionary<string, QuestStatus>();

    public MapContent? Content { get; init; }

    public IReadOnlyList<ObjectiveView> Objectives { get; init; } = [];

    public IReadOnlyList<ExtractView> Extracts { get; init; } = [];

    public GameLocations? Locations { get; init; }

    /// <summary>Discovery found no game on this PC (not while still looking): the no-game state.</summary>
    public bool NoGameFound => Locations is { Install: null };

    /// <summary>The game is found but has no log sessions yet: it hasn't run on this PC.</summary>
    public bool GameNotRunYet => Locations is { Install: not null, LogsFolder: null };

    /// <summary>"Choose game folder…" can work: discovery isn't overridden by a fake game.</summary>
    public bool CanChooseGameFolder { get; init; }

    /// <summary>The folder the player chose for the game, as saved (it may no longer hold the game), or null: then
    /// settings offer FIND AUTOMATICALLY.</summary>
    public string? ChosenGameFolder { get; init; }

    public SourceHealth Logs { get; init; } = new(false, "Looking for the game…");

    public SourceHealth Screenshots { get; init; } = new(false, "Looking for screenshots…");

    public SourceHealth DataHealth { get; init; } = new(false, "Loading game data…");

    /// <summary>Why there is no game data, in plain words, or null.</summary>
    public LoadProblem? DataProblem { get; init; }

    /// <summary>The game's language code, as its settings say it ("ge"), or null when unknown.</summary>
    public string? GameLanguage { get; init; }

    /// <summary>Whether the study log is being kept this session.</summary>
    public bool StudyLogOn { get; init; }

    /// <summary>Whether "Delete position screenshots" is ticked: each one is deleted a few seconds after its name was read.</summary>
    public bool DeleteScreenshots { get; init; }

    /// <summary>Whether "Read the extract list from screenshots" is ticked (on unless unticked).</summary>
    public bool ReadExits { get; init; } = true;

    /// <summary>
    /// When the screenshot was taken that last showed the game's extract list this raid; null while none did. With
    /// it, each of <see cref="Extracts"/> says whether the list names it.
    /// </summary>
    public DateTime? ExitsReadAt { get; init; }

    /// <summary>The language Windows reads the list in ("en-US"), once a raid's screenshot asked for it; else null.</summary>
    public string? ExitReaderLanguage { get; init; }

    /// <summary>Windows has no text recognition language, so no list can be read (known once a raid's screenshot asked).</summary>
    public bool ExitReaderMissing { get; init; }

    public IReadOnlyList<string> ScreenshotKeys { get; init; } = [];

    /// <summary>Suggested maps for the next raid, best first (maps with picks first).</summary>
    public IReadOnlyList<MapPlanView> Plan { get; init; } = [];

    /// <summary>The quests picked for the coming raid on the map the rail is about: the raid's map in a raid, the map
    /// shown otherwise (<see cref="QuestPicks"/>; picks are kept per map). The pen picks for this map.</summary>
    public IReadOnlySet<string> Picks { get; init; } = new HashSet<string>();

    /// <summary>Which colour each of those picks has (<see cref="QuestPicks.Slots"/>).</summary>
    public IReadOnlyDictionary<string, int> PickSlots { get; init; } = new Dictionary<string, int>();

    /// <summary>Every map's picks in this mode, by <see cref="Planning.PickKey"/>: Plan's list shows each map's own.</summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> PicksByMap { get; init; } = new Dictionary<string, IReadOnlySet<string>>();

    /// <summary>Every map's picks' colours, by <see cref="Planning.PickKey"/>.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> PickSlotsByMap { get; init; } = new Dictionary<string, IReadOnlyDictionary<string, int>>();

    private static readonly IReadOnlySet<string> NoPicks = new HashSet<string>();
    private static readonly IReadOnlyDictionary<string, int> NoSlots = new Dictionary<string, int>();

    /// <summary>The picks of a map (any of its variants' names).</summary>
    public IReadOnlySet<string> PicksOn(string? map) =>
        map is not null && PicksByMap.TryGetValue(Planning.PickKey(Data, map), out var picks) ? picks : NoPicks;

    /// <summary>The colours of a map's picks.</summary>
    public IReadOnlyDictionary<string, int> PickSlotsOn(string? map) =>
        map is not null && PickSlotsByMap.TryGetValue(Planning.PickKey(Data, map), out var slots) ? slots : NoSlots;

    /// <summary>
    /// The objectives the player ticked as done in this mode, each with the day it was set (<see cref="ObjectiveTicks"/>).
    /// The plans, the rail and the map already leave them out; the cards show them as done and say why.
    /// </summary>
    public IReadOnlyDictionary<string, DateOnly> Ticks { get; init; } = new Dictionary<string, DateOnly>();

    /// <summary>The ticked objectives' ids.</summary>
    public IReadOnlySet<string> Done { get; init; } = new HashSet<string>();

    /// <summary>Active quests that can be worked on in any raid (kills anywhere, found-in-raid items).</summary>
    public IReadOnlyList<PlanQuestView> AnyMap { get; init; } = [];

    /// <summary>The plan for the raid's map in a raid, else for the map on screen (ranked or not): the raid view is this plan, live.</summary>
    public MapPlanView? MapPlan { get; init; }

    public RaidInfo? RaidInfo { get; init; }

    public LastRaidView? LastRaid { get; init; }

    /// <summary>
    /// The last raid's replay, from its end until the next raid loads; in memory only, so gone at a restart. Plan's
    /// last-raid line offers REPLAY when it <see cref="RaidReplay.Plays"/>.
    /// </summary>
    public RaidReplay? Replay { get; init; }

    public int ActiveQuestCount => Quests.Values.Count(q => q.State == QuestState.Active);
}
