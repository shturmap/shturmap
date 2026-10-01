using CommunityToolkit.Mvvm.ComponentModel;

namespace Spotter.App;

public sealed record ObjectiveItem(string QuestId, string Text, string Quest, string Distance, string Direction, bool Done);

public sealed record ExtractItem(string Name, string Kind, string Distance, string Direction);

public sealed record ConfirmItem(string QuestId, string Read, string Quest);

public sealed record MapChoice(string NormalizedName, string Name)
{
    public override string ToString() => Name;
}

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

    [ObservableProperty] public partial bool ScanOpen { get; set; }

    [ObservableProperty] public partial string ScanTitle { get; set; } = "";

    [ObservableProperty] public partial string ScanMessage { get; set; } = "";

    [ObservableProperty] public partial IReadOnlyList<ConfirmItem> Confirmations { get; set; } = [];

    [ObservableProperty] public partial bool NoticeOpen { get; set; }

    [ObservableProperty] public partial string NoticeText { get; set; } = "";

    [ObservableProperty] public partial bool Following { get; set; } = true;

    [ObservableProperty] public partial string Attribution { get; set; } = "";
}
