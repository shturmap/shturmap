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
public sealed record ViewCue(CueKind Kind, string MapName, TimeSpan? RaidLength = null);

/// <summary>How one input is doing, for the status chips: "Logs ✓", "Screenshots ✓", "Data 1 h ago".</summary>
public sealed record SourceHealth(bool Ok, string Text);

/// <summary>An objective on the shown map, measured from the last position fix.</summary>
/// <param name="HeightDifference">Metres above (+) or below (−) the player, when it matters (over 3 m).</param>
/// <param name="Needs">Keys or items this objective needs, e.g. "Key: Dorm room 114 key", or null.</param>
/// <param name="MapBearing">Degrees clockwise from map-up, from the last fix; stays true when the facing goes stale.</param>
/// <param name="TraderId">The quest giver, for the portrait.</param>
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
    string? TraderId = null);

/// <summary>The raid at a glance: length, bosses, the in-raid time of day from the last screenshot.</summary>
public sealed record RaidInfo(int RaidMinutes, IReadOnlyList<string> Bosses, double? ClockHours);

/// <summary>The raid that just ended, for one line in Plan.</summary>
public sealed record LastRaidView(string MapName, TimeSpan Duration, RaidSide Side, DateTime EndedAt);

/// <param name="MapBearing">Degrees clockwise from map-up, from the last fix; stays true when the facing goes stale.</param>
/// <param name="Needs">What it takes to leave here ("Pay 5,000 ₽"), or empty.</param>
/// <param name="NeedItemId">An item to hand over, to picture next to it.</param>
public sealed record ExtractView(string Id, string Name, MarkerKind Kind, double? Distance, RelativeDirection? Direction, double? MapBearing = null,
    string Needs = "", string? NeedItemId = null);

/// <summary>Everything the UI shows, replaced as a whole whenever something changes.</summary>
public sealed record SessionSnapshot
{
    public RaidState Raid { get; init; } = new();

    /// <summary>Whether the logs told the raid's side; if not, the side is the player's say (or unknown, shown as PMC).</summary>
    public bool SideFromLogs { get; init; }

    public GameMode Mode { get; init; } = GameMode.Pve;

    /// <summary>What the game's log said about the mode, for the status bar's tooltip.</summary>
    public ModeReading ModeReading { get; init; } = new();

    /// <summary>The map on screen: the raid's map while in a raid, otherwise the last raid's or the one picked.</summary>
    public MapIdentity? Map { get; init; }

    public MapDefinition? Definition { get; init; }

    public PlayerFix? Fix { get; init; }

    public IReadOnlyList<WorldPoint> Trail { get; init; } = [];

    public MapLayer? Floor { get; init; }

    public GameData? Data { get; init; }

    /// <summary>Where items come from; arrives a little after <see cref="Data"/>.</summary>
    public ItemSources? Sources { get; init; }

    public IReadOnlyDictionary<string, QuestStatus> Quests { get; init; } = new Dictionary<string, QuestStatus>();

    public MapContent? Content { get; init; }

    public IReadOnlyList<ObjectiveView> Objectives { get; init; } = [];

    public IReadOnlyList<ExtractView> Extracts { get; init; } = [];

    public GameLocations? Locations { get; init; }

    public SourceHealth Logs { get; init; } = new(false, "Looking for the game…");

    public SourceHealth Screenshots { get; init; } = new(false, "Looking for screenshots…");

    public SourceHealth DataHealth { get; init; } = new(false, "Loading game data…");

    /// <summary>Why there is no game data, in plain words, or null.</summary>
    public LoadProblem? DataProblem { get; init; }

    /// <summary>The game's language code, as its settings say it ("ge"), or null when unknown.</summary>
    public string? GameLanguage { get; init; }

    /// <summary>Whether the study log is being kept this session.</summary>
    public bool StudyLogOn { get; init; }

    public IReadOnlyList<string> ScreenshotKeys { get; init; } = [];

    /// <summary>Suggested maps for the next raid, best first.</summary>
    public IReadOnlyList<MapPlanView> Plan { get; init; } = [];

    /// <summary>Active quests that can be worked on in any raid (kills anywhere, found-in-raid items).</summary>
    public IReadOnlyList<PlanQuestView> AnyMap { get; init; } = [];

    /// <summary>The plan for the map on screen (ranked or not): the raid view is this plan, live.</summary>
    public MapPlanView? MapPlan { get; init; }

    public RaidInfo? RaidInfo { get; init; }

    public LastRaidView? LastRaid { get; init; }

    public int ActiveQuestCount => Quests.Values.Count(q => q.State == QuestState.Active);
}
