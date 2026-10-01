using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Spotter.Core.Logs;
using Spotter.Core.Navigation;
using Spotter.Core.Raid;
using Spotter.Map;
using Spotter.Session;
using Windows.Graphics;

namespace Spotter.App;

public sealed partial class MainWindow : Window
{
    private readonly GameSession _session;
    private readonly DispatcherQueueTimer _clock;
    private readonly DispatcherQueueTimer _noticeTimer;
    private SessionSnapshot? _snapshot;
    private string? _sceneKey;
    private string? _selectedQuest;
    private bool _updatingPicker;
    private bool _scanDismissed;
    private ScanResult? _shownScan;

    public MainWindow(GameSession session)
    {
        _session = session;
        InitializeComponent();
        ExtendsContentIntoTitleBar = false;
        SystemBackdrop = new MicaBackdrop();
        PlaceOnSecondMonitor();

        _clock = DispatcherQueue.CreateTimer();
        _clock.Interval = TimeSpan.FromSeconds(1);
        _clock.Tick += (_, _) => UpdateClockTexts();
        _clock.Start();
        _noticeTimer = DispatcherQueue.CreateTimer();
        _noticeTimer.Interval = TimeSpan.FromSeconds(6);
        _noticeTimer.IsRepeating = false;
        _noticeTimer.Tick += (_, _) => ViewModel.NoticeOpen = false;

        Map.FollowChanged += () => ViewModel.Following = Map.FollowPlayer;
        var follow = new KeyboardAccelerator { Key = Windows.System.VirtualKey.F };
        follow.Invoked += (_, e) =>
        {
            ViewModel.Following = true;
            OnFollowClick(this, new RoutedEventArgs());
            e.Handled = true;
        };
        ((UIElement)Content).KeyboardAccelerators.Add(follow);

        // Snapshots arrive on background threads; only the newest one is applied.
        session.Changed += s =>
        {
            Volatile.Write(ref _snapshot, s);
            DispatcherQueue.TryEnqueue(() => Apply(s));
        };
        session.Notice += message => DispatcherQueue.TryEnqueue(() => ShowNotice(message));
    }

    public MainViewModel ViewModel { get; } = new();

    public Brush RaidBrush(bool inRaid) => (Brush)Application.Current.Resources[inRaid ? "AmberBrush" : "InkBrush"];

    public Brush OkBrush(bool ok) => (Brush)Application.Current.Resources[ok ? "GreenBrush" : "AmberBrush"];

    private void Apply(SessionSnapshot s)
    {
        if (!ReferenceEquals(s, _snapshot))
            return; // a newer snapshot is queued behind this one

        var vm = ViewModel;
        vm.ModeText = s.Mode switch { GameMode.Pvp => "PvP", GameMode.Seasonal => "Seasonal", _ => "PvE" };
        vm.InRaid = s.Raid.Phase != RaidPhase.Menu;
        vm.LogsText = s.Logs.Text;
        vm.LogsOk = s.Logs.Ok;
        vm.ScreenshotsText = s.Screenshots.Text;
        vm.ScreenshotsOk = s.Screenshots.Ok;
        vm.DataText = s.DataHealth.Text;
        vm.DataOk = s.DataHealth.Ok;
        UpdateClockTexts();
        UpdatePicker(s);
        UpdateLists(s);
        UpdateScan(s);
        UpdateMap(s);
    }

    private void UpdateClockTexts()
    {
        if (_snapshot is not { } s)
            return;
        var map = s.Map?.Name ?? "";
        var side = s.Raid.Side switch { RaidSide.Pmc => " · PMC", RaidSide.Scav => " · Scav", _ => "" };
        ViewModel.RaidText = s.Raid.Phase switch
        {
            RaidPhase.Loading => $"Loading {map}",
            RaidPhase.InRaid when s.Raid.RaidStartedAt is { } started => $"In raid · {map}{side} · {(int)(DateTime.Now - started).TotalMinutes} min",
            RaidPhase.InRaid => $"In raid · {map}{side}",
            _ => "In the menus",
        };

        if (s.Fix is not { } fix)
        {
            var keys = s.ScreenshotKeys.Count > 0 ? string.Join(" or ", s.ScreenshotKeys) : "your screenshot key";
            ViewModel.FixText = $"No position yet · press {keys} in raid";
            return;
        }
        var age = DateTime.Now - fix.At;
        var ago = age.TotalSeconds < 60 ? $"{Math.Max(0, (int)age.TotalSeconds)} s" : age.TotalMinutes < 60 ? $"{(int)age.TotalMinutes} min" : $"{(int)age.TotalHours} h";
        var floor = s.Floor?.Name ?? "ground";
        ViewModel.FixText = $"Fix {ago} ago · {floor} · height {fix.Position.Y.ToString("0", CultureInfo.CurrentCulture)} m";
    }

    private void UpdatePicker(SessionSnapshot s)
    {
        if (s.Data is null)
            return;
        _updatingPicker = true;
        try
        {
            if (ViewModel.MapChoices.Count != s.Data.Maps.Count)
            {
                ViewModel.MapChoices = s.Data.Maps.Values
                    .Where(m => s.Data.DefinitionFor(m.NormalizedName) is not null)
                    .OrderBy(m => m.Name, StringComparer.CurrentCulture)
                    .Select(m => new MapChoice(m.NormalizedName, m.Name))
                    .ToList();
            }
            ViewModel.SelectedMap = ViewModel.MapChoices.FirstOrDefault(c => c.NormalizedName == s.Map?.NormalizedName);
        }
        finally
        {
            _updatingPicker = false;
        }
    }

    private void UpdateLists(SessionSnapshot s)
    {
        var vm = ViewModel;
        vm.ObjectivesHeader = vm.InRaid ? "OBJECTIVES HERE · NEAREST FIRST" : $"OBJECTIVES ON {s.Map?.Name.ToUpperInvariant()}";
        vm.Objectives = s.Objectives.Where(o => o.HasPlace).Select(ToItem).ToList();
        vm.Unplaced = s.Objectives.Where(o => !o.HasPlace).Select(ToItem).ToList();
        vm.Extracts = s.Extracts.Select(e => new ExtractItem(
            e.Name,
            e.Kind switch
            {
                MarkerKind.ExtractPmc => "PMC extract",
                MarkerKind.ExtractScav => "Scav extract",
                MarkerKind.Transit => "Transit",
                _ => "Shared extract",
            },
            Distance(e.Distance),
            e.Direction is { } d ? Bearing.Describe(d) : "")).ToList();

        vm.Hint = s.Data is null ? "Loading quests and maps…"
            : s.Objectives.Count == 0 ? $"None of your {s.ActiveQuestCount} active quests has an objective on this map."
            : "";
        vm.Attribution = s.Definition?.Author is { } author ? $"Map © {author} and contributors · data tarkov.dev" : "Data tarkov.dev";
    }

    private static ObjectiveItem ToItem(ObjectiveView o)
    {
        var direction = o.Direction is { } d ? Bearing.Describe(d) : "";
        if (o.HeightDifference is { } h)
            direction += (direction.Length > 0 ? " · " : "") + $"{Math.Abs(h):0} m {(h > 0 ? "up" : "down")}";
        return new ObjectiveItem(o.QuestId, o.Text, string.IsNullOrEmpty(o.Trader) ? o.QuestName : $"{o.QuestName} · {o.Trader}",
            Distance(o.Distance), direction, o.Done);
    }

    private static string Distance(double? metres) => metres switch
    {
        null => "",
        < 1000 => $"{metres:0} m",
        _ => $"{metres / 1000:0.0} km",
    };

    private void UpdateScan(SessionSnapshot s)
    {
        if (s.LastScan is not { } scan)
        {
            ViewModel.ScanOpen = false;
            return;
        }
        if (!ReferenceEquals(scan, _shownScan) && (_shownScan is null || scan.File != _shownScan.File))
            _scanDismissed = false;
        _shownScan = scan;
        ViewModel.ScanTitle = $"Tasks scan · {scan.Tab} tab · {scan.Rows} rows read";
        ViewModel.ScanMessage = scan.NewlyActive.Count > 0 ? "Now active: " + string.Join(", ", scan.NewlyActive) : "Nothing new.";
        if (scan.NeedConfirmation.Count > 0)
            ViewModel.ScanMessage += " Please check these reads:";
        if (scan.Unread > 0)
            ViewModel.ScanMessage += $" {scan.Unread} row(s) could not be read.";
        ViewModel.Confirmations = scan.NeedConfirmation.Where(m => m.Quest is not null)
            .Select(m => new ConfirmItem(m.Quest!.Id, m.RowName, m.Quest.Name)).ToList();
        ViewModel.ScanOpen = !_scanDismissed;
    }

    private async void UpdateMap(SessionSnapshot s)
    {
        if (s.Definition is null || _session.Artwork is null)
            return;
        var key = s.Definition.Key;
        if (key != _sceneKey)
        {
            _sceneKey = key;
            var artwork = await _session.Artwork.GetAsync(s.Definition);
            if (_sceneKey != key)
                return;
            if (artwork is null)
            {
                Map.SetScene(null);
                ShowNotice($"{s.Map?.Name} is only published as image tiles; drawing those comes in a later version.");
                return;
            }
            Map.SetScene(new MapScene(s.Definition, artwork));
        }
        if (Map.Scene is not { } scene || _snapshot is not { } latest)
            return;
        scene.Player = latest.Fix;
        scene.Trail = latest.Trail;
        scene.Floor = latest.Floor;
        scene.Markers = latest.Content?.Markers ?? [];
        scene.Zones = latest.Content?.Zones ?? [];
        scene.Selected = _selectedQuest;
        Map.Refresh();
    }

    /// <summary>Developer aid: renders the window's controls and the map to PNG files in a folder.</summary>
    public async Task SaveSnapshotAsync(string folder)
    {
        Directory.CreateDirectory(folder);
        try
        {
            var root = (UIElement)Content;
            var rtb = new Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap();
            await rtb.RenderAsync(root);
            var pixels = await rtb.GetPixelsAsync();
            var file = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(folder)
                .AsTask().ContinueWith(t => t.Result.CreateFileAsync("window.png", Windows.Storage.CreationCollisionOption.ReplaceExisting).AsTask()).Unwrap();
            using (var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite))
            {
                var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);
                var dpi = 96 * (Content.XamlRoot?.RasterizationScale ?? 1);
                encoder.SetPixelData(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                    (uint)rtb.PixelWidth, (uint)rtb.PixelHeight, dpi, dpi, System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.ToArray(pixels));
                await encoder.FlushAsync();
            }
            Map.SaveSnapshot(Path.Combine(folder, "map.png"));
            AppLog.Info("Snapshot saved to " + folder);
        }
        catch (Exception e)
        {
            AppLog.Error("Snapshot failed", e);
        }
    }

    private void ShowNotice(string message)
    {
        ViewModel.NoticeText = message;
        ViewModel.NoticeOpen = true;
        _noticeTimer.Stop();
        _noticeTimer.Start();
    }

    private void PlaceOnSecondMonitor()
    {
        var displays = DisplayArea.FindAll();
        DisplayArea? target = null;
        for (var i = 0; i < displays.Count; i++)
        {
            if (displays[i].DisplayId.Value != DisplayArea.Primary.DisplayId.Value)
            {
                target = displays[i];
                break;
            }
        }
        if (target is null)
        {
            AppWindow.Resize(new SizeInt32(1600, 1000));
            return;
        }
        AppWindow.Move(new PointInt32(target.WorkArea.X + 40, target.WorkArea.Y + 40));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
            presenter.Maximize();
    }

    // ---- UI events ----

    private async void OnModeClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: string tag } && Enum.TryParse<GameMode>(tag, out var mode))
            await _session.SetModeAsync(mode);
    }

    private async void OnMapPicked(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingPicker || ViewModel.SelectedMap is not { } choice || choice.NormalizedName == _snapshot?.Map?.NormalizedName)
            return;
        await _session.SelectMapAsync(choice.NormalizedName);
    }

    private void OnObjectiveClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not ObjectiveItem item)
            return;
        _selectedQuest = _selectedQuest == item.QuestId ? null : item.QuestId;
        if (Map.Scene is { } scene)
        {
            scene.Selected = _selectedQuest;
            Map.Refresh();
        }
    }

    private async void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string questId })
            await _session.ConfirmScanAsync(questId);
    }

    private void OnScanClosed(InfoBar sender, object args)
    {
        _scanDismissed = true;
        ViewModel.ScanOpen = false;
    }

    private void OnNoticeClosed(InfoBar sender, object args) => ViewModel.NoticeOpen = false;

    private void OnFollowClick(object sender, RoutedEventArgs e)
    {
        Map.FollowPlayer = ViewModel.Following;
        if (Map.FollowPlayer)
            Map.CenterOnPlayer();
    }

    private void OnFitClick(object sender, RoutedEventArgs e)
    {
        ViewModel.Following = false;
        Map.FollowPlayer = false;
        Map.FitMap();
    }

    private void OnZoomInClick(object sender, RoutedEventArgs e) => Map.ZoomBy(1.5);

    private void OnZoomOutClick(object sender, RoutedEventArgs e) => Map.ZoomBy(1 / 1.5);
}
