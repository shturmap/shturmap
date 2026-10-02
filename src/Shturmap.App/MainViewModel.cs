using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Shturmap.Core.Quests;

namespace Shturmap.App;

/// <param name="Needs">Keys or items this objective needs, or empty.</param>
public sealed record ObjectiveItem(string QuestId, string Text, string Quest, string Distance, string Direction, bool Done, ObjectiveKind Kind, string Needs,
    string? TraderId = null, string? TraderName = null)
{
    public Visibility NeedsVisibility => Needs.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    // Without a position there is no distance and no direction; an empty line would still take its height.
    public Visibility DistanceVisibility => Distance.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DirectionVisibility => Direction.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Muted for what is done at a trader after the raid: nothing to do about it here.</summary>
    public Microsoft.UI.Xaml.Media.Brush TextBrush =>
        (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[QuestTaxonomy.InRaid(Kind) ? "InkBrush" : "MutedBrush"];
}

/// <param name="Id">The map marker's id, for linked highlighting.</param>
/// <param name="Needs">What it takes to leave ("Pay 5,000 ₽", "Red Rebel ice pick and paracord, no armored rig"), or empty.</param>
public sealed record ExtractItem(string Id, string Name, string Kind, string Distance, string Direction, string Needs = "", string? NeedItemId = null)
{
    public Visibility NeedsVisibility => Needs.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ItemVisibility => NeedItemId is null ? Visibility.Collapsed : Visibility.Visible;
}

public sealed record MapChoice(string NormalizedName, string Name)
{
    public override string ToString() => Name;
}

/// <param name="Needs">What it needs brought on the map (empty: nothing), or null where bringing doesn't apply.</param>
/// <param name="Synopsis">What it asks on the map in a few words, under the name; empty for none.</param>
public sealed record QuestLine(string QuestId, ObjectiveKind Kind, string Name, string? TraderId, string TraderName,
    IReadOnlyList<Controls.NeedChip>? Needs = null, string Synopsis = "")
{
    public Visibility SynopsisVisibility => Synopsis.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
}

/// <param name="Glyph">Segoe Fluent Icons character: key or briefcase, shown until the item's icon arrives.</param>
/// <param name="QuestIds">The quests it is for, for linked highlighting.</param>
/// <param name="Source">The easiest way to get it ("Prapor LL1 · 18,936 ₽"), or empty.</param>
public sealed record RequirementLine(string Glyph, string Text, string For, string ItemId, IReadOnlyList<string> QuestIds, string Source)
{
    public Visibility SourceVisibility => Source.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
}

/// <summary>One floor in the map's floor picker, top floor first.</summary>
/// <param name="PlayerHere">The floor of the last position fix.</param>
public sealed record FloorChoice(int Index, string Name, bool Shown, bool PlayerHere)
{
    public Microsoft.UI.Xaml.Media.Brush Foreground => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[Shown ? "AmberBrush" : "InkBrush"];

    public Microsoft.UI.Xaml.Media.Brush Border => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[Shown ? "AmberBrush" : "LineBrush"];

    public Visibility PlayerVisibility => PlayerHere ? Visibility.Visible : Visibility.Collapsed;
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
    IReadOnlyList<Controls.NeedChip>? Needs = null)
{
    public string MapTitle => Caps.Of(MapName);

    public Visibility FoldedVisibility => Expanded ? Visibility.Collapsed : Visibility.Visible;

    public Microsoft.UI.Xaml.Media.Brush CardBorder =>
        (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[Expanded ? "LineStrongBrush" : "LineBrush"];

    public Visibility ExpandedVisibility => Expanded ? Visibility.Visible : Visibility.Collapsed;

    public Visibility FinishVisibility => Expanded && Finish.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ProgressVisibility => Expanded && Progress.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RequirementsVisibility => Expanded && Requirements.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Microsoft.UI.Xaml.Media.Brush CardBackground =>
        (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[Expanded ? "RaisedBrush" : "RailBrush"];
}

/// <summary>A quest in the raid card: its line as in Plan, with its objectives on this map under it.</summary>
/// <param name="Complete">Whether this raid can complete it (Plan's COMPLETE), or only progress it.</param>
public sealed record RaidQuest(string QuestId, ObjectiveKind Kind, string Name, string? TraderId, string TraderName,
    IReadOnlyList<ObjectiveItem> Objectives, bool Complete, IReadOnlyList<Controls.NeedChip>? Needs = null)
{
    // As in Plan: quests this raid completes in gold and ink, the ones it only progresses muted.
    public Microsoft.UI.Xaml.Media.Brush GlyphBrush => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[Complete ? "AmberBrush" : "MutedBrush"];

    public Microsoft.UI.Xaml.Media.Brush NameBrush => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[Complete ? "InkBrush" : "MutedBrush"];

    public double PictureOpacity => Complete ? 1 : 0.6;
}

public sealed record LegendItem(ObjectiveKind Kind, string Label, string Explanation);

/// <summary>What the window shows, as display-ready text. Filled from each session snapshot.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    [ObservableProperty] public partial string ModeText { get; set; } = "PvE";

    [ObservableProperty] public partial string RaidText { get; set; } = "Starting…";

    [ObservableProperty] public partial bool InRaid { get; set; }

    [ObservableProperty] public partial string FixText { get; set; } = "No position yet";

    [ObservableProperty] public partial string LogsText { get; set; } = "Logs";

    [ObservableProperty] public partial bool LogsOk { get; set; }

    [ObservableProperty] public partial string ScreenshotsText { get; set; } = "Screenshots";

    [ObservableProperty] public partial bool ScreenshotsOk { get; set; }

    [ObservableProperty] public partial string DataText { get; set; } = "Data";

    [ObservableProperty] public partial bool DataOk { get; set; }

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

    [ObservableProperty] public partial IReadOnlyList<RaidQuest> RaidComplete { get; set; } = [];

    [ObservableProperty] public partial IReadOnlyList<RaidQuest> RaidProgress { get; set; } = [];

    [ObservableProperty] public partial IReadOnlyList<RequirementLine> RaidBring { get; set; } = [];

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

    [ObservableProperty] public partial string Hint { get; set; } = "";

    [ObservableProperty] public partial string RaidLine { get; set; } = "";

    /// <summary>"LOADING · PLAYER SPAWNED" (the last step the log reported) while the raid loads, else empty.</summary>
    [ObservableProperty] public partial string LoadingText { get; set; } = "";

    /// <summary>"PREVIEW · CUSTOMS" while another map is shown from a Plan card under the pointer; else empty.</summary>
    [ObservableProperty] public partial string PreviewText { get; set; } = "";

    /// <summary>In a raid, "POSITION 7 MIN OLD" when the last position is too old to trust at a glance; else empty.</summary>
    [ObservableProperty] public partial string StaleText { get; set; } = "";

    /// <summary>What to do about it: "PRESS PRTSC OR HOME FOR A NEW ONE".</summary>
    [ObservableProperty] public partial string StaleHint { get; set; } = "";

    [ObservableProperty] public partial string LastRaidText { get; set; } = "";

    [ObservableProperty] public partial IReadOnlyList<PlanCard> Plans { get; set; } = [];

    [ObservableProperty] public partial IReadOnlyList<QuestLine> AnyMap { get; set; } = [];

    [ObservableProperty] public partial string HelpKeys { get; set; } = "your screenshot key";

    [ObservableProperty] public partial bool NoticeOpen { get; set; }

    [ObservableProperty] public partial string NoticeText { get; set; } = "";

    [ObservableProperty] public partial string Attribution { get; set; } = "";

    [ObservableProperty] public partial IReadOnlyList<FloorChoice> Floors { get; set; } = [];

    /// <summary>The wiki's interactive map for the shown map, or null.</summary>
    [ObservableProperty] public partial Uri? WikiMap { get; set; }

    public IReadOnlyList<LegendItem> Legend { get; } = Enum.GetValues<ObjectiveKind>()
        .Select(k => new LegendItem(k, QuestTaxonomy.Label(k), QuestTaxonomy.Explanation(k)))
        .ToList();
}
