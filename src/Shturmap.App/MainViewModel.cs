using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Shturmap.Core.Quests;

namespace Shturmap.App;

/// <param name="Needs">Keys or items this objective needs, or empty.</param>
/// <param name="ObjectiveId">The objective, for linked highlighting: its line marks it within its quest.</param>
/// <param name="KeyId">The key the gold "Key: …" line stands for, when it names exactly one: the line is that key.</param>
public sealed record ObjectiveItem(string QuestId, string Text, string Quest, string Distance, string Direction, bool Done, ObjectiveKind Kind, string Needs,
    string? TraderId = null, string? TraderName = null, string? ObjectiveId = null, string? KeyId = null)
{
    // An objective ticked as done needs nothing any more.
    public Visibility NeedsVisibility => Needs.Length > 0 && !Done ? Visibility.Visible : Visibility.Collapsed;

    // Without a position there is no distance and no direction; an empty line would still take its height.
    public Visibility DistanceVisibility => Distance.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DirectionVisibility => Direction.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Muted for what is done at a trader after the raid, and for what the player ticked as done: nothing to
    /// do about either here.</summary>
    public Microsoft.UI.Xaml.Media.Brush TextBrush =>
        (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[QuestTaxonomy.InRaid(Kind) && !Done ? "InkBrush" : "MutedBrush"];
}

/// <param name="Id">The map marker's id, for linked highlighting.</param>
/// <param name="Needs">What it takes to leave ("Pay 5,000 ₽", "Red Rebel ice pick and paracord, no armored rig"), or empty.</param>
/// <param name="Marker">What kind of way out it is on the map: its colour here is its symbol's there.</param>
public sealed record ExtractItem(string Id, string Name, string Kind, string Distance, string Direction, string Needs = "", string? NeedItemId = null,
    Shturmap.Map.MarkerKind Marker = Shturmap.Map.MarkerKind.ExtractPmc)
{
    public Visibility NeedsVisibility => Needs.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ItemVisibility => NeedItemId is null ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>
    /// Under the glance's EXIT. An extract is the nearest one for the player's side, but the game opens only some
    /// extracts in each raid, by where the player started, and neither its logs nor tarkov.dev's data say which
    /// (owner, 2026-10-04: say so in the row). At a raid's start the nearest is usually not one of the player's. A
    /// transit is open to everyone: no note.
    /// </summary>
    public string NearestNote => Marker == Shturmap.Map.MarkerKind.Transit ? "" : "Nearest · check your list in game";

    /// <summary>
    /// A way out wears the colour of its kind, in the rail as on the map: a PMC extract green, a Scav's teal, one for
    /// both sides khaki, a transit violet (one colour, one meaning; 2026-10-04: in the rail every way out was green,
    /// which is the PMC extract's on the map).
    /// </summary>
    public Microsoft.UI.Xaml.Media.Brush KindBrush => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[Marker switch
    {
        Shturmap.Map.MarkerKind.ExtractScav => "TealBrush",
        Shturmap.Map.MarkerKind.ExtractShared => "KhakiBrush",
        Shturmap.Map.MarkerKind.Transit => "VioletBrush",
        _ => "GreenBrush",
    }];
}

public sealed record MapChoice(string NormalizedName, string Name)
{
    public override string ToString() => Name;
}

/// <param name="Needs">What it needs brought on the map (empty: nothing), or null where bringing doesn't apply.</param>
/// <param name="Synopsis">What it asks on the map in a few words, under the name; empty for none.</param>
/// <param name="StartsGroup">The first row of a later effort group in a Plan section (Session.Planning.Rows).</param>
/// <param name="Note">For a PROGRESS row, why it only progresses here ("2 of 5 objectives here"); else empty.</param>
public sealed record QuestLine(string QuestId, ObjectiveKind Kind, string Name, string? TraderId, string TraderName,
    IReadOnlyList<Controls.NeedChip>? Needs = null, string Synopsis = "", bool StartsGroup = false, string Note = "")
{
    public Visibility SynopsisVisibility => Synopsis.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility NoteVisibility => Note.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>The colour the quest has as a pick, for its glyph in a map's row.</summary>
    public Microsoft.UI.Xaml.Media.Brush PickBrush => Controls.Linked.PickBrush(QuestId);

    /// <summary>A hairline above the row where a later effort group starts; no heading (owner, 2026-10-03).</summary>
    public Thickness GroupLine => StartsGroup ? new Thickness(0, 1, 0, 0) : new Thickness(0);
}

/// <param name="Glyph">Segoe Fluent Icons character: key or briefcase, shown until the item's icon arrives.</param>
/// <param name="QuestIds">The quests it is for, for linked highlighting.</param>
/// <param name="Source">The easiest way to get it ("Prapor LL1 · 18,936 ₽"), or empty.</param>
/// <param name="StartsOthers">The first row that serves no pick, after rows that do: a hairline above it.</param>
public sealed record RequirementLine(string Glyph, string Text, string For, string ItemId, IReadOnlyList<string> QuestIds, string Source,
    bool StartsOthers = false)
{
    /// <summary>Every item the row stands for when it stands for several ("A or B", gear worn together, a weapon
    /// class): the row is each of them for the linked highlight.</summary>
    public IReadOnlyList<string>? Alternatives { get; init; }

    public Visibility SourceVisibility => Source.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Thickness OthersLine => StartsOthers ? new Thickness(0, 1, 0, 0) : new Thickness(0);
}

/// <summary>One floor in the map's floor picker, top floor first.</summary>
/// <param name="PlayerHere">The floor of the last position fix.</param>
public sealed record FloorChoice(int Index, string Name, bool Shown, bool PlayerHere)
{
    public Microsoft.UI.Xaml.Media.Brush Foreground => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[Shown ? "AmberBrush" : "InkBrush"];

    public Microsoft.UI.Xaml.Media.Brush Border => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[Shown ? "AmberBrush" : "LineBrush"];

    public Visibility PlayerVisibility => PlayerHere ? Visibility.Visible : Visibility.Collapsed;
}

/// <summary>
/// One part of a line of facts ("40 min raid", "Kaban 75%"), an element of its own so that a boss's name can be
/// linked to its spawn zones on the map (the review of 2026-10-04, E3: the line was one text).
/// </summary>
/// <param name="Groups">The map markers' groups this part stands for (a boss's spawn zones), or null: plain text.</param>
public sealed record LinePart(string Text, IReadOnlyList<string>? Groups = null)
{
    /// <summary>The last part of its line: no dot after it.</summary>
    public bool Last { get; init; }

    public Visibility DotVisibility => Last ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>The parts of a line, the last one marked.</summary>
    public static IReadOnlyList<LinePart> Line(IEnumerable<LinePart> parts)
    {
        var list = parts.Where(p => p.Text.Length > 0).ToList();
        return list.Select((p, i) => p with { Last = i == list.Count - 1 }).ToList();
    }

    /// <summary>Whether two lines say and link the same: then the one on screen stays, with the pointer's state on it.</summary>
    public static bool Same(IReadOnlyList<LinePart> a, IReadOnlyList<LinePart> b) =>
        a.Count == b.Count && a.Zip(b).All(p => p.First.Text == p.Second.Text && p.First.Last == p.Second.Last
            && (p.First.Groups ?? []).SequenceEqual(p.Second.Groups ?? []) && (p.First.Groups is null) == (p.Second.Groups is null));
}

/// <summary>One suggested map in Plan. Only the expanded card shows its quests and requirements.</summary>
/// <param name="Summary">"Complete 8 quests · progress 2 more".</param>
/// <param name="Rank">Its place in the suggestions: "1", "2", ….</param>
public sealed record PlanCard(
    string NormalizedName,
    string MapName,
    string Summary,
    string Detail,
    bool Expanded,
    IReadOnlyList<QuestLine> Finish,
    IReadOnlyList<QuestLine> Progress,
    IReadOnlyList<RequirementLine> Requirements,
    string Rank = "",
    IReadOnlyList<Controls.NeedChip>? Needs = null,
    IReadOnlyList<QuestLine>? Picks = null)
{
    public string MapTitle => Caps.Of(MapName);

    /// <summary>The summary's counts for the map's row in Plan's list: "Complete 5 · progress 1".</summary>
    public string ShortSummary { get; init; } = "";

    /// <summary><see cref="Detail"/> part by part: the raid's length, then each boss, linked to its markers.</summary>
    public IReadOnlyList<LinePart> DetailParts { get; init; } = [];

    /// <summary>The quests picked for the coming raid on this map, first in the card (owner, 2026-10-03).</summary>
    public IReadOnlyList<QuestLine> Picked => Picks ?? [];

    public Visibility PicksVisibility => Expanded && Picked.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>On a folded card, the picks' glyphs (cyan) come first, then a hairline.</summary>
    public Visibility FoldedPicksVisibility => Picked.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PickDividerVisibility => Picked.Count > 0 && Finish.Count + Progress.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Microsoft.UI.Xaml.Media.Brush CardBorder =>
        (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[Expanded ? "LineStrongBrush" : "LineBrush"];

    public Visibility ExpandedVisibility => Expanded ? Visibility.Visible : Visibility.Collapsed;

    public Visibility FinishVisibility => Expanded && Finish.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ProgressVisibility => Expanded && Progress.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>On a folded card, a hairline between the glyphs of quests to complete and those to progress.</summary>
    public Visibility GlyphDividerVisibility => Finish.Count > 0 && Progress.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RequirementsVisibility => Expanded && Requirements.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Microsoft.UI.Xaml.Media.Brush CardBackground =>
        (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[Expanded ? "RaisedBrush" : "RailBrush"];
}

/// <summary>A quest in the raid card: its line as in Plan, with its objectives on this map under it.</summary>
/// <param name="Complete">Whether this raid can complete it (Plan's COMPLETE), or only progress it.</param>
public sealed record RaidQuest(string QuestId, ObjectiveKind Kind, string Name, string? TraderId, string TraderName,
    IReadOnlyList<ObjectiveItem> Objectives, bool Complete, IReadOnlyList<Controls.NeedChip>? Needs = null);

public sealed record LegendItem(ObjectiveKind Kind, string Label, string Explanation);

/// <summary>What the window shows, as display-ready text. Filled from each session snapshot.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    [ObservableProperty] public partial string ModeText { get; set; } = "PvE";

    /// <summary>Where the mode comes from: the game's log and when, or the mode last played.</summary>
    [ObservableProperty] public partial string ModeDetail { get; set; } = "";

    [ObservableProperty] public partial string RaidText { get; set; } = "Starting…";

    /// <summary>Where the raid state comes from (the game's log), for its tooltip.</summary>
    [ObservableProperty] public partial string RaidDetail { get; set; } = "";

    [ObservableProperty] public partial bool InRaid { get; set; }

    /// <summary>A raid is loading or running: what comes up by itself and waits for a click stays away until it is
    /// over (<see cref="Rules.WhileInRaid.Waits"/>).</summary>
    [ObservableProperty] public partial bool RaidHoldsBack { get; set; }

    /// <summary>The last fix in the status bar ("Fix 4 s ago · ground · height 4 m"); in a raid without one, how to
    /// get one; else empty (<see cref="Rules.StatusBarFit.NoPosition"/>).</summary>
    [ObservableProperty] public partial string FixText { get; set; } = "";

    /// <summary>Whether the status bar's three lights keep their words: the first thing to go in a narrow window
    /// (<see cref="Rules.StatusBarFit.Words"/>).</summary>
    [ObservableProperty] public partial bool LightWords { get; set; } = true;

    [ObservableProperty] public partial string LogsText { get; set; } = "Logs";

    [ObservableProperty] public partial bool LogsOk { get; set; }

    /// <summary>The LOGS light's tooltip: which logs are followed, and newer game logs elsewhere if a folder was chosen.</summary>
    [ObservableProperty] public partial string LogsDetail { get; set; } = "";

    [ObservableProperty] public partial string ScreenshotsText { get; set; } = "Screenshots";

    [ObservableProperty] public partial bool ScreenshotsOk { get; set; }

    [ObservableProperty] public partial string DataText { get; set; } = "Data";

    [ObservableProperty] public partial bool DataOk { get; set; }

    /// <summary>The data chip's tooltip: where the data is from, or why there is none, in plain words.</summary>
    [ObservableProperty] public partial string DataDetail { get; set; } = "Loading game data from tarkov.dev…";

    [ObservableProperty] public partial bool StudyLogOn { get; set; }

    /// <summary>"Delete position screenshots" in settings: off unless the player ticked it.</summary>
    [ObservableProperty] public partial bool DeleteScreenshots { get; set; }

    /// <summary>Help's "Uninstall Shturmap…" asks its one question.</summary>
    [ObservableProperty] public partial bool UninstallAsking { get; set; }

    /// <summary>"Also delete my Shturmap data": unticked until the player ticks it, and each time the question opens.</summary>
    [ObservableProperty] public partial bool UninstallDeleteData { get; set; }

    [ObservableProperty] public partial IReadOnlyList<MapChoice> MapChoices { get; set; } = [];

    [ObservableProperty] public partial MapChoice? SelectedMap { get; set; }

    /// <summary>The raid card's title: the map, in capitals like Plan's cards.</summary>
    [ObservableProperty] public partial string RaidTitle { get; set; } = "";

    /// <summary>"Complete 3 quests · progress 2 more", as on the map's Plan card.</summary>
    [ObservableProperty] public partial string RaidSummary { get; set; } = "";

    /// <summary>What the distances are measured from when it isn't a fresh screenshot, or empty.</summary>
    [ObservableProperty] public partial string RaidFixNote { get; set; } = "";

    /// <summary>The nearest objective with a place, from the last position: the first thing a glance should find.</summary>
    [ObservableProperty] public partial ObjectiveItem? RaidNext { get; set; }

    /// <summary>The nearest extract or transit for your side, from the last position.</summary>
    [ObservableProperty] public partial ExtractItem? RaidExit { get; set; }

    /// <summary>The picked quests on the raid's map, first in the raid card, nearest first.</summary>
    [ObservableProperty] public partial IReadOnlyList<RaidQuest> RaidPicks { get; set; } = [];

    /// <summary>Whether any quest is picked (for CLEAR PICKS, outside raids).</summary>
    [ObservableProperty] public partial bool HasPicks { get; set; }

    [ObservableProperty] public partial IReadOnlyList<RaidQuest> RaidComplete { get; set; } = [];

    [ObservableProperty] public partial IReadOnlyList<RaidQuest> RaidProgress { get; set; } = [];

    [ObservableProperty] public partial IReadOnlyList<RequirementLine> RaidBring { get; set; } = [];

    /// <summary>While the raid loads, "CHECK YOUR KIT": what gets you in or out, then what the picks need (or all of
    /// it without picks here); BRING itself steps aside until the raid starts (Planning.KitWhileLoading).</summary>
    [ObservableProperty] public partial IReadOnlyList<RequirementLine> RaidKit { get; set; } = [];

    /// <summary>With picks here, the kit's other rows, under "ALSO USEFUL".</summary>
    [ObservableProperty] public partial IReadOnlyList<RequirementLine> RaidKitMore { get; set; } = [];

    /// <summary>"PMC" or "SCAV" beside the raid card's title, or empty when the logs can't tell.</summary>
    [ObservableProperty] public partial string RaidSide { get; set; } = "";

    /// <summary>The logs can't tell the side (PvE): the side tag is a switch.</summary>
    [ObservableProperty] public partial bool SideSwitchable { get; set; }

    /// <summary>A Scav raid: the card shows the loot your quests need instead of their objectives.</summary>
    [ObservableProperty] public partial bool ScavRaid { get; set; }

    /// <summary>One line on what the side means for your quests, or empty.</summary>
    [ObservableProperty] public partial string RaidNote { get; set; } = "";

    /// <summary>In a Scav raid: items your quests need found in raid, the ones loose here first.</summary>
    [ObservableProperty] public partial IReadOnlyList<RequirementLine> RaidLoot { get; set; } = [];

    /// <summary>"And 14 more items …" when the loot list is cut short, or empty.</summary>
    [ObservableProperty] public partial string RaidLootMore { get; set; } = "";

    [ObservableProperty] public partial IReadOnlyList<ExtractItem> Extracts { get; set; } = [];

    /// <summary>The EXIT row's link to the whole list under the card, "ALL 11 ↓", or empty when the list holds no more
    /// than the row shows.</summary>
    [ObservableProperty] public partial string AllExitsText { get; set; } = "";

    [ObservableProperty] public partial string Hint { get; set; } = "";

    [ObservableProperty] public partial string RaidLine { get; set; } = "";

    /// <summary>The raid line part by part (<see cref="RaidLine"/> is the same as one text): its bosses are linked to
    /// their markers.</summary>
    [ObservableProperty] public partial IReadOnlyList<LinePart> RaidLineParts { get; set; } = [];

    /// <summary>"LOADING · PLAYER SPAWNED" (the last step the log reported) while the raid loads, else empty.</summary>
    [ObservableProperty] public partial string LoadingText { get; set; } = "";

    /// <summary>"PREVIEW · CUSTOMS" while another map is shown from its row in Plan under the pointer; else empty.</summary>
    [ObservableProperty] public partial string PreviewText { get; set; } = "";

    /// <summary>"LOOKING AT WOODS" while another map than the raid's is on screen in a raid (the MAP list); else empty.</summary>
    [ObservableProperty] public partial string LookText { get; set; } = "";

    /// <summary>Beside <see cref="LookText"/>: where the raid is, and what brings its map back.</summary>
    [ObservableProperty] public partial string LookNote { get; set; } = "";

    [ObservableProperty] public partial string LastRaidText { get; set; } = "";

    [ObservableProperty] public partial IReadOnlyList<PlanCard> Plans { get; set; } = [];

    /// <summary>Whether Plan lists the suggested maps above the open map's card (<see cref="Rules.PlanList.Shown"/>).</summary>
    [ObservableProperty] public partial bool PlanListShown { get; set; }

    [ObservableProperty] public partial IReadOnlyList<QuestLine> AnyMap { get; set; } = [];

    [ObservableProperty] public partial string HelpKeys { get; set; } = "your screenshot key";

    [ObservableProperty] public partial bool NoticeOpen { get; set; }

    [ObservableProperty] public partial string NoticeText { get; set; } = "";

    /// <summary>The notice asks for a report: it shows a REPORT link (COPY DIAGNOSTICS where reporting isn't set up).</summary>
    [ObservableProperty] public partial bool NoticeOffersReport { get; set; }

    /// <summary>The "Crash reports" setting in settings: "Ask", "Always" or "Never".</summary>
    [ObservableProperty] public partial string CrashMode { get; set; } = "Ask";

    /// <summary>What settings say under "Updates" when this run can't update, and why (Distribution.NoUpdatesText).</summary>
    [ObservableProperty] public partial string UpdatesNote { get; set; } = Shturmap.Session.Distribution.NoUpdatesText(false, false);

    /// <summary>The "Updates" setting in settings: "Automatic", "TellOnly" or "Off".</summary>
    [ObservableProperty] public partial string UpdateMode { get; set; } = "Automatic";

    /// <summary>This build can update itself (installed by its Setup); otherwise help says it can't.</summary>
    [ObservableProperty] public partial bool UpdatesAvailable { get; set; }

    /// <summary>The one quiet line about a new version, between raids; empty when there is none.</summary>
    [ObservableProperty] public partial string UpdateLine { get; set; } = "";

    /// <summary>The rail's line while the game, or its logs, aren't found (<see cref="Shturmap.Session.GameStateLine"/>); empty otherwise.</summary>
    [ObservableProperty] public partial string GameLine { get; set; } = "";

    [ObservableProperty] public partial string GameNote { get; set; } = "";

    [ObservableProperty] public partial bool GameFolderChoosable { get; set; }

    /// <summary>Settings' "GAME FOLDER" row: where Shturmap found the game, or that it didn't.</summary>
    [ObservableProperty] public partial string GameFolderText { get; set; } = "";

    /// <summary>Settings' CHOOSE… beside the game folder: whenever a folder can be chosen (not in a fake game).</summary>
    [ObservableProperty] public partial bool GameFolderChangeable { get; set; }

    /// <summary>Settings' FIND AUTOMATICALLY: only while a chosen folder is saved (owner, 2026-10-04).</summary>
    [ObservableProperty] public partial bool FindGameAutomatically { get; set; }

    /// <summary>"Newer game logs in C:\…" when another install has newer logs than the chosen folder; empty otherwise.</summary>
    [ObservableProperty] public partial string NewerGameLogs { get; set; } = "";

    /// <summary>No game logs to plan from (no game, or it hasn't run): NEXT RAID and its cards step aside for the line.</summary>
    [ObservableProperty] public partial bool NoGameLogs { get; set; }

    /// <summary>No game on this PC: the mode is chosen by hand instead of read from the log.</summary>
    [ObservableProperty] public partial bool ModeChoosable { get; set; }

    [ObservableProperty] public partial bool UpdateOffersDownload { get; set; }

    [ObservableProperty] public partial bool UpdateOffersRestart { get; set; }

    /// <summary>The question after a crash, then what came of the answer; empty when there is nothing to say.</summary>
    [ObservableProperty] public partial string CrashQuestion { get; set; } = "";

    /// <summary>Still asking: Send, Don't send, Always send.</summary>
    [ObservableProperty] public partial bool CrashAsking { get; set; }

    /// <summary>Sent: a note can be added through the Report dialog.</summary>
    [ObservableProperty] public partial bool CrashSent { get; set; }

    /// <summary>Exactly what the crash report would send, when the player asked to see it.</summary>
    [ObservableProperty] public partial string CrashDetails { get; set; } = "";

    [ObservableProperty] public partial string Attribution { get; set; } = "";

    [ObservableProperty] public partial IReadOnlyList<FloorChoice> Floors { get; set; } = [];

    /// <summary>The wiki's interactive map for the shown map, or null.</summary>
    [ObservableProperty] public partial Uri? WikiMap { get; set; }

    public IReadOnlyList<LegendItem> Legend { get; } = Enum.GetValues<ObjectiveKind>()
        .Select(k => new LegendItem(k, QuestTaxonomy.Label(k), QuestTaxonomy.Explanation(k)))
        .ToList();
}
