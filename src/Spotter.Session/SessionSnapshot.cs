using Spotter.Core;
using Spotter.Core.Logs;
using Spotter.Core.Maps;
using Spotter.Core.Navigation;
using Spotter.Core.Quests;
using Spotter.Core.Raid;
using Spotter.Data.TarkovDev;
using Spotter.Game.Install;
using Spotter.Map;
using Spotter.Ocr;

namespace Spotter.Session;

/// <summary>How one input is doing, for the status chips: "Logs ✓", "Screenshots ✓", "Data 1 h ago".</summary>
public sealed record SourceHealth(bool Ok, string Text);

/// <summary>An objective on the shown map, measured from the last position fix.</summary>
/// <param name="HeightDifference">Metres above (+) or below (−) the player, when it matters (over 3 m).</param>
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
    double? HeightDifference);

public sealed record ExtractView(string Id, string Name, MarkerKind Kind, double? Distance, RelativeDirection? Direction);

/// <summary>The outcome of reading a Tasks screenshot.</summary>
public sealed record ScanResult(
    DateTime At,
    string File,
    TasksTab Tab,
    int Rows,
    IReadOnlyList<string> NewlyActive,
    IReadOnlyList<QuestMatch> NeedConfirmation,
    int Unread);

/// <summary>Everything the UI shows, replaced as a whole whenever something changes.</summary>
public sealed record SessionSnapshot
{
    public RaidState Raid { get; init; } = new();

    public GameMode Mode { get; init; } = GameMode.Pve;

    /// <summary>The map on screen: the raid's map while in a raid, otherwise the last raid's or the one picked.</summary>
    public MapIdentity? Map { get; init; }

    public MapDefinition? Definition { get; init; }

    public PlayerFix? Fix { get; init; }

    public IReadOnlyList<WorldPoint> Trail { get; init; } = [];

    public MapLayer? Floor { get; init; }

    public GameData? Data { get; init; }

    public IReadOnlyDictionary<string, QuestStatus> Quests { get; init; } = new Dictionary<string, QuestStatus>();

    public MapContent? Content { get; init; }

    public IReadOnlyList<ObjectiveView> Objectives { get; init; } = [];

    public IReadOnlyList<ExtractView> Extracts { get; init; } = [];

    public GameLocations? Locations { get; init; }

    public SourceHealth Logs { get; init; } = new(false, "Looking for the game…");

    public SourceHealth Screenshots { get; init; } = new(false, "Looking for screenshots…");

    public SourceHealth DataHealth { get; init; } = new(false, "Loading game data…");

    public IReadOnlyList<string> ScreenshotKeys { get; init; } = [];

    public ScanResult? LastScan { get; init; }

    public int ActiveQuestCount => Quests.Values.Count(q => q.State == QuestState.Active);
}
