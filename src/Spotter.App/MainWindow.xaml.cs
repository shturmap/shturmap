using System.Collections;
using System.Globalization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Spotter.Core.Logs;
using Spotter.Core.Navigation;
using Spotter.Core.Planning;
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
    private bool _helpShownOnce;
    private ScanResult? _shownScan;

    public MainWindow(GameSession session)
    {
        _session = session;
        InitializeComponent();
        SystemBackdrop = new MicaBackdrop();
        PlaceOnSecondMonitor();

        _clock = DispatcherQueue.CreateTimer();
        _clock.Interval = TimeSpan.FromSeconds(1);
        _clock.Tick += (_, _) => OnClockTick();
        _clock.Start();
        _noticeTimer = DispatcherQueue.CreateTimer();
        _noticeTimer.Interval = TimeSpan.FromSeconds(6);
        _noticeTimer.IsRepeating = false;
        _noticeTimer.Tick += (_, _) => ViewModel.NoticeOpen = false;

        Map.FollowChanged += () => ViewModel.Following = Map.FollowPlayer;
        AddShortcuts((UIElement)Content);

        // Snapshots arrive on background threads; only the newest one is applied.
        session.Changed += s =>
        {
            Volatile.Write(ref _snapshot, s);
            DispatcherQueue.TryEnqueue(() => Apply(s));
        };
        session.Notice += notice => DispatcherQueue.TryEnqueue(() => ShowNotice(notice.Text, notice.Duration));
    }

    // Facing-relative directions are only true briefly after a fix; past this they turn into map directions.
    private static readonly TimeSpan FreshFix = TimeSpan.FromSeconds(45);
    private bool? _fixWasFresh;

    private void OnClockTick()
    {
        UpdateClockTexts();
        if (_snapshot is not { Fix: { } fix } s)
            return;
        var fresh = DateTime.Now - fix.At < FreshFix;
        if (fresh != _fixWasFresh)
            UpdateRaidLists(s);
        Map.Redraw(); // the marker fades and its "may have moved" ring grows with age
    }

    public MainViewModel ViewModel { get; } = new();

    // ---- x:Bind helpers ----

    public Brush RaidBrush(bool inRaid) => Resource(inRaid ? "AmberBrush" : "InkBrush");

    public Brush OkBrush(bool ok) => Resource(ok ? "GreenBrush" : "AmberBrush");

    public Visibility ShownIf(string? text) => string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ShownIfTrue(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ShownIfNot(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ShownIfAny(IEnumerable? items) => items?.GetEnumerator().MoveNext() == true ? Visibility.Visible : Visibility.Collapsed;

    private static Brush Resource(string key) => (Brush)Application.Current.Resources[key];

    // ---- snapshot → view ----

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
        vm.HelpKeys = s.ScreenshotKeys.Count > 0 ? string.Join(" or ", s.ScreenshotKeys) : "your screenshot key";
        UpdateClockTexts();
        UpdatePicker(s);
        UpdatePlan(s);
        UpdateRaidLists(s);
        UpdateScan(s);
        UpdateMap(s);
        ShowHelpOnFirstRun(s);
    }

    private void UpdateClockTexts()
    {
        if (_snapshot is not { } s)
            return;
        var map = s.Map?.Name ?? "";
        var side = s.Raid.Side switch { RaidSide.Pmc => " · PMC", RaidSide.Scav => " · Scav", _ => "" };
        var elapsed = s.Raid.RaidStartedAt is { } started ? DateTime.Now - started : (TimeSpan?)null;
        ViewModel.RaidText = s.Raid.Phase switch
        {
            RaidPhase.Loading => $"Loading {map}",
            RaidPhase.InRaid when elapsed is { } e => $"In raid · {map}{side} · {(int)e.TotalMinutes} min",
            RaidPhase.InRaid => $"In raid · {map}{side}",
            _ => "In the menus",
        };

        var parts = new List<string>();
        if (s.RaidInfo is { } info)
        {
            if (elapsed is { } e && info.RaidMinutes > 0)
                parts.Add($"{Math.Max(0, info.RaidMinutes - (int)e.TotalMinutes)} min left");
            else if (info.RaidMinutes > 0)
                parts.Add($"{info.RaidMinutes} min raid");
            if (info.ClockHours is { } clock)
            {
                var minutes = (int)Math.Round(clock * 60) % (24 * 60);
                parts.Add($"{minutes / 60:00}:{minutes % 60:00} in raid");
            }
            parts.AddRange(info.Bosses);
        }
        ViewModel.RaidLine = string.Join(" · ", parts);

        if (s.Fix is not { } fix)
        {
            ViewModel.FixText = $"No position yet · press {ViewModel.HelpKeys} in raid";
            return;
        }
        var age = DateTime.Now - fix.At;
        var ago = age.TotalSeconds < 60 ? $"{Math.Max(0, (int)age.TotalSeconds)} s" : age.TotalMinutes < 60 ? $"{(int)age.TotalMinutes} min" : $"{(int)age.TotalHours} h";
        ViewModel.FixText = $"Fix {ago} ago · {s.Floor?.Name ?? "ground"} · height {fix.Position.Y.ToString("0", CultureInfo.CurrentCulture)} m";
    }

    private void UpdatePicker(SessionSnapshot s)
    {
        if (s.Data is null)
            return;
        _updatingPicker = true;
        try
        {
            if (ViewModel.MapChoices.Count == 0)
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

    private void UpdatePlan(SessionSnapshot s)
    {
        var vm = ViewModel;
        vm.LastRaidText = s.LastRaid is { } last
            ? $"Last raid · {last.MapName} · {(int)last.Duration.TotalMinutes} min" + (last.Side == RaidSide.Unknown ? "" : $" · {(last.Side == RaidSide.Pmc ? "PMC" : "Scav")}")
            : "";

        // The card for the map on screen is open; if that map isn't suggested, the best one is.
        var openIndex = s.Plan.ToList().FindIndex(p => p.NormalizedName == s.Map?.NormalizedName);
        if (openIndex < 0)
            openIndex = 0;
        vm.Plans = s.Plan.Select((p, i) => new PlanCard(
            p.NormalizedName,
            p.MapName,
            p.Progress.Count > 0 ? $"{p.Finish.Count} finish · {p.Progress.Count} progress" : $"{p.Finish.Count} finish",
            string.Join(" · ", new[] { p.WalkingMinutes > 0 ? $"~{p.WalkingMinutes} min walking" : null, p.RaidMinutes > 0 ? $"{p.RaidMinutes} min raid" : null }
                .Concat(p.Bosses).OfType<string>()),
            i == openIndex,
            p.Finish.Select(q => new QuestLine(q.Kind, q.Name)).ToList(),
            p.Progress.Select(q => new QuestLine(q.Kind, q.Name)).ToList(),
            p.Requirements.Select(r => new RequirementLine(r.Kind == RequirementKind.Key ? Glyphs.Key : Glyphs.Bring, r.Text, "for " + r.ForQuests)).ToList()
        )).ToList();
        vm.AnyMap = s.AnyMap.Select(q => new QuestLine(q.Kind, q.Name)).ToList();
    }

    private void UpdateRaidLists(SessionSnapshot s)
    {
        var vm = ViewModel;
        var age = s.Fix is { } fix ? DateTime.Now - fix.At : (TimeSpan?)null;
        var fresh = age < FreshFix;
        _fixWasFresh = age is null ? null : fresh;
        vm.ObjectivesHeader = age switch
        {
            null => $"OBJECTIVES ON {s.Map?.Name.ToUpperInvariant()}",
            _ when fresh => "OBJECTIVES HERE · NEAREST FIRST",
            { TotalMinutes: < 60 } a => $"FROM YOUR FIX {Math.Max(1, (int)a.TotalMinutes)} MIN AGO · NEAREST FIRST",
            _ => "FROM YOUR LAST FIX · NEAREST FIRST",
        };
        string Direction(RelativeDirection? relative, double? mapBearing) =>
            fresh && relative is { } r ? Bearing.Describe(r) : mapBearing is { } b ? Bearing.Compass(b) : "";
        vm.Objectives = s.Objectives.Where(o => o.HasPlace).Select(o => ToItem(o, Direction(o.Direction, o.MapBearing))).ToList();
        vm.Unplaced = s.Objectives.Where(o => !o.HasPlace).Select(o => ToItem(o, "")).ToList();
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
            Direction(e.Direction, e.MapBearing))).ToList();

        vm.Hint = s.Data is null ? "Loading quests and maps…"
            : !vm.InRaid && s.Plan.Count == 0 ? "None of your active quests is tied to a map. To update your quests, take a screenshot of Character → Tasks in the game."
            : vm.InRaid && s.Objectives.Count == 0 ? $"None of your {s.ActiveQuestCount} active quests has an objective on this map."
            : "";
        vm.Attribution = s.Definition?.Author is { } author ? $"Map © {author} and contributors · data tarkov.dev" : "Data tarkov.dev";
    }

    private static ObjectiveItem ToItem(ObjectiveView o, string direction)
    {
        if (o.HeightDifference is { } h)
            direction += (direction.Length > 0 ? " · " : "") + $"{Math.Abs(h):0} m {(h > 0 ? "up" : "down")}";
        return new ObjectiveItem(o.QuestId, o.Text, string.IsNullOrEmpty(o.Trader) ? o.QuestName : $"{o.QuestName} · {o.Trader}",
            Distance(o.Distance), direction, o.Done, o.Kind, o.Needs ?? "");
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
        if (_shownScan is null || scan.File != _shownScan.File)
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

    private void ShowNotice(string message, TimeSpan? duration = null)
    {
        ViewModel.NoticeText = message;
        ViewModel.NoticeOpen = true;
        _noticeTimer.Stop();
        _noticeTimer.Interval = duration ?? TimeSpan.FromSeconds(6);
        _noticeTimer.Start();
    }

    /// <summary>Set for "--snapshot" runs: the help panel opens to be rendered, and isn't marked as seen.</summary>
    public bool SnapshotMode { get; set; }

    // The help panel opens by itself once, the first time the app has something to show.
    private void ShowHelpOnFirstRun(SessionSnapshot s)
    {
        if (_helpShownOnce || s.Data is null)
            return;
        _helpShownOnce = true;
        if (SnapshotMode || _session.GetSetting("help.seen") is null)
            ShowHelp();
    }

    private void ShowHelp()
    {
        if (HelpButton.XamlRoot is not null)
            HelpFlyout.ShowAt(HelpButton);
    }

    private void OnHelpClosed(object sender, object e)
    {
        if (!SnapshotMode)
            _session.SetSetting("help.seen", "1");
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

    // ---- keyboard (only while this window has focus; Spotter registers no global hotkeys) ----

    private void AddShortcuts(UIElement root)
    {
        void Add(Windows.System.VirtualKey key, Action action)
        {
            var accelerator = new KeyboardAccelerator { Key = key };
            accelerator.Invoked += (_, e) =>
            {
                action();
                e.Handled = true;
            };
            root.KeyboardAccelerators.Add(accelerator);
        }

        Add(Windows.System.VirtualKey.F, () =>
        {
            ViewModel.Following = true;
            OnFollowClick(this, new RoutedEventArgs());
        });
        Add(Windows.System.VirtualKey.Add, () => Map.ZoomBy(1.5));
        Add((Windows.System.VirtualKey)187, () => Map.ZoomBy(1.5)); // the +/= key
        Add(Windows.System.VirtualKey.Subtract, () => Map.ZoomBy(1 / 1.5));
        Add((Windows.System.VirtualKey)189, () => Map.ZoomBy(1 / 1.5)); // the -/_ key
        Add(Windows.System.VirtualKey.Number0, () => OnFitClick(this, new RoutedEventArgs()));
        Add(Windows.System.VirtualKey.NumberPad0, () => OnFitClick(this, new RoutedEventArgs()));
        Add(Windows.System.VirtualKey.Escape, ClearSelection);
        Add(Windows.System.VirtualKey.F1, ShowHelp);
        root.CharacterReceived += (_, e) =>
        {
            if (e.Character == '?')
            {
                ShowHelp();
                e.Handled = true;
            }
        };
    }

    private void ClearSelection()
    {
        _selectedQuest = null;
        if (Map.Scene is { } scene)
        {
            scene.Selected = null;
            Map.Refresh();
        }
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

    private async void OnPlanClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string map } && map != _snapshot?.Map?.NormalizedName)
            await _session.SelectMapAsync(map);
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

    // ---- developer aid ----

    /// <summary>Renders the window's controls, the help panel and the map to PNG files in a folder ("--snapshot").</summary>
    public async Task SaveSnapshotAsync(string folder)
    {
        Directory.CreateDirectory(folder);
        try
        {
            await RenderToPngAsync((UIElement)Content, Path.Combine(folder, "window.png"));
            if (HelpFlyout.IsOpen && HelpFlyout.Content is UIElement help)
                await RenderToPngAsync(help, Path.Combine(folder, "help.png"));
            Map.SaveSnapshot(Path.Combine(folder, "map.png"));
            AppLog.Info("Snapshot saved to " + folder);
        }
        catch (Exception e)
        {
            AppLog.Error("Snapshot failed", e);
        }
    }

    private async Task RenderToPngAsync(UIElement element, string path)
    {
        var rtb = new Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap();
        await rtb.RenderAsync(element);
        var pixels = await rtb.GetPixelsAsync();
        var storageFolder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(path));
        var file = await storageFolder.CreateFileAsync(Path.GetFileName(path), Windows.Storage.CreationCollisionOption.ReplaceExisting);
        using var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);
        var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);
        var dpi = 96 * (Content.XamlRoot?.RasterizationScale ?? 1);
        encoder.SetPixelData(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
            (uint)rtb.PixelWidth, (uint)rtb.PixelHeight, dpi, dpi, System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.ToArray(pixels));
        await encoder.FlushAsync();
    }
}
