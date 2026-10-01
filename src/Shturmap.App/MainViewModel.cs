using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Shturmap.Core.Quests;

namespace Shturmap.App;

/// <param name="Needs">Keys or items this objective needs, or empty.</param>
public sealed record ObjectiveItem(string QuestId, string Text, string Quest, string Distance, string Direction, bool Done, ObjectiveKind Kind, string Needs,
    string? TraderId = null, string? TraderName = null)
{
    public Visibility NeedsVisibility => Needs.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
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

public sealed record QuestLine(string QuestId, ObjectiveKind Kind, string Name, string? TraderId, string TraderName);

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
    string Rank = "")
{
    public string MapTitle => Caps.Of(MapName);

    public Microsoft.UI.Xaml.Media.Brush CardBorder =>
        (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[Expanded ? "LineStrongBrush" : "LineBrush"];

    public Visibility ExpandedVisibility => Expanded ? Visibility.Visible : Visibility.Collapsed;

    public Visibility FinishVisibility => Expanded && Finish.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ProgressVisibility => Expanded && Progress.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RequirementsVisibility => Expanded && Requirements.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Microsoft.UI.Xaml.Media.Brush CardBackground =>
        (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[Expanded ? "RaisedBrush" : "RailBrush"];
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

    [ObservableProperty] public partial IReadOnlyList<ObjectiveItem> Objectives { get; set; } = [];

    [ObservableProperty] public partial IReadOnlyList<ObjectiveItem> Unplaced { get; set; } = [];

    [ObservableProperty] public partial IReadOnlyList<ExtractItem> Extracts { get; set; } = [];

    [ObservableProperty] public partial string ObjectivesHeader { get; set; } = "OBJECTIVES HERE";

    [ObservableProperty] public partial string Hint { get; set; } = "";

    [ObservableProperty] public partial string RaidLine { get; set; } = "";

    [ObservableProperty] public partial string LastRaidText { get; set; } = "";

    [ObservableProperty] public partial IReadOnlyList<PlanCard> Plans { get; set; } = [];

    [ObservableProperty] public partial IReadOnlyList<QuestLine> AnyMap { get; set; } = [];

    [ObservableProperty] public partial string HelpKeys { get; set; } = "your screenshot key";

    [ObservableProperty] public partial bool NoticeOpen { get; set; }

    [ObservableProperty] public partial string NoticeText { get; set; } = "";

    [ObservableProperty] public partial bool Following { get; set; } = true;

    [ObservableProperty] public partial string Attribution { get; set; } = "";

    [ObservableProperty] public partial IReadOnlyList<FloorChoice> Floors { get; set; } = [];

    /// <summary>The wiki's interactive map for the shown map, or null.</summary>
    [ObservableProperty] public partial Uri? WikiMap { get; set; }

    public IReadOnlyList<LegendItem> Legend { get; } = Enum.GetValues<ObjectiveKind>()
        .Select(k => new LegendItem(k, QuestTaxonomy.Label(k), QuestTaxonomy.Explanation(k)))
        .ToList();
}
