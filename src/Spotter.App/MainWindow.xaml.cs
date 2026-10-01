using System.Collections;
using System.Globalization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Spotter.App.Controls;
using Spotter.Core.Logs;
using Spotter.Core.Maps;
using Spotter.Core.Navigation;
using Spotter.Core.Planning;
using Spotter.Core.Quests;
using Spotter.Core.Raid;
using Spotter.Map;
using Spotter.Session;
using Windows.Graphics;
using MapLayer = Spotter.Core.Maps.MapLayer;

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
    private readonly QuestCardHost _cards;
    private readonly Dictionary<string, QuestWindow> _pinned = [];
    private bool _pinnedRestored;
    private IReadOnlyList<MapLayer?> _floors = [];
    private string? _floorsFor;
    private int? _floorPick;
    private DateTime? _floorPickFix;
    private int _shownFloor = -1;

    public MainWindow(GameSession session)
    {
        _session = session;
        InitializeComponent();
        SystemBackdrop = new MicaBackdrop();
        PlaceOnSecondMonitor();

        Picture.Art = () => _session.Art;
        _cards = new QuestCardHost((FrameworkElement)Content, DispatcherQueue, BuildCard);
        _cards.PinRequested += (view, at) => Pin(view, QuestWindow.ScreenPoint(this, at));
        Linked.QuestHovered += OnQuestHovered;
        Linked.FocusChanged += OnFocusChanged;
        Map.MarkerHovered += OnMarkerHovered;
        Map.MarkerClicked += OnMarkerClicked;
        Closed += (_, _) =>
        {
            SavePinned();
            foreach (var window in _pinned.Values.ToList())
                window.Close();
        };

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
        Map.Redraw(); // the player marker fades with age
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
        _cards.Refresh();
        RefreshPinned();
        RestorePinnedOnce(s);
        ShowHelpOnFirstRun(s);
        ShowQuestForSnapshot(s);
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
        QuestLine Line(PlanQuestView q) => new(q.QuestId, q.Kind, q.Name, q.TraderId, s.Data?.TraderName(q.TraderId) ?? "");
        vm.Plans = s.Plan.Select((p, i) => new PlanCard(
            p.NormalizedName,
            p.MapName,
            Summary(p.Finish.Count, p.Progress.Count),
            string.Join(" · ", new[] { p.WalkingMinutes > 0 ? $"~{p.WalkingMinutes} min walking" : null, p.RaidMinutes > 0 ? $"{p.RaidMinutes} min raid" : null }
                .Concat(p.Bosses).OfType<string>()),
            i == openIndex,
            p.Finish.Select(Line).ToList(),
            p.Progress.Select(Line).ToList(),
            p.Requirements.Select(r => new RequirementLine(r.Kind == RequirementKind.Key ? Glyphs.Key : Glyphs.Bring, r.Text, "for " + r.ForQuests, r.ItemId, r.QuestIds)).ToList()
        )).ToList();
        vm.AnyMap = s.AnyMap.Select(Line).ToList();
    }

    /// <summary>"Complete 8 quests · progress 2 more": what one raid on the map does for your quest list.</summary>
    private static string Summary(int complete, int progress)
    {
        static string Quests(int n) => n == 1 ? "1 quest" : $"{n} quests";
        return (complete, progress) switch
        {
            (> 0, > 0) => $"Complete {Quests(complete)} · progress {progress} more",
            (> 0, _) => $"Complete {Quests(complete)}",
            _ => $"Progress {Quests(progress)}",
        };
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
            e.Id,
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
            : !vm.InRaid && s.Plan.Count == 0 ? "None of your active quests is tied to a map. If quests are missing, take a screenshot of Character → Tasks in the game."
            : vm.InRaid && s.Objectives.Count == 0 ? $"None of your {s.ActiveQuestCount} active quests has an objective on this map."
            : "";
        vm.Attribution = s.Definition?.Author is { } author ? $"Map © {author} and contributors · data tarkov.dev" : "Data tarkov.dev";
    }

    private static ObjectiveItem ToItem(ObjectiveView o, string direction)
    {
        if (o.HeightDifference is { } h)
            direction += (direction.Length > 0 ? " · " : "") + $"{Math.Abs(h):0} m {(h > 0 ? "up" : "down")}";
        return new ObjectiveItem(o.QuestId, o.Text, string.IsNullOrEmpty(o.Trader) ? o.QuestName : $"{o.QuestName} · {o.Trader}",
            Distance(o.Distance), direction, o.Done, o.Kind, o.Needs ?? "", o.TraderId, o.Trader);
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
        scene.Floor = ShownFloor(latest);
        scene.Markers = latest.Content?.Markers ?? [];
        scene.Zones = latest.Content?.Zones ?? [];
        scene.Selected = _selectedQuest;
        scene.Focus = MapFocus();
        Map.Refresh();
    }

    // ---- floors ----

    /// <summary>
    /// The floor to draw: the player's, from the last fix, unless one was picked since that fix. A new screenshot
    /// says where you are, so it ends the pick.
    /// </summary>
    private MapLayer? ShownFloor(SessionSnapshot s)
    {
        if (s.Definition is null)
            return s.Floor;
        if (s.Definition.Key != _floorsFor)
        {
            _floorsFor = s.Definition.Key;
            _floors = FloorResolver.Stack(s.Definition);
            _floorPick = null;
        }
        if (_floorPick is not null && s.Fix?.At != _floorPickFix)
            _floorPick = null;
        var playerFloor = s.Fix is null ? -1 : IndexOfFloor(s.Floor);
        _shownFloor = _floorPick ?? IndexOfFloor(s.Floor);
        ViewModel.Floors = _floors.Select((layer, i) => new FloorChoice(i, layer?.Name ?? "Ground", i == _shownFloor, i == playerFloor)).ToList();
        return _shownFloor >= 0 && _shownFloor < _floors.Count ? _floors[_shownFloor] : s.Floor;
    }

    // A floor without artwork of its own is drawn in the base layer, so it counts as ground here.
    private int IndexOfFloor(MapLayer? layer)
    {
        var index = layer is null ? -1 : _floors.ToList().IndexOf(layer);
        return index >= 0 ? index : _floors.ToList().IndexOf(null);
    }

    private void PickFloor(int index)
    {
        if (_floors.Count == 0 || _snapshot is not { } s)
            return;
        _floorPick = Math.Clamp(index, 0, _floors.Count - 1);
        _floorPickFix = s.Fix?.At;
        UpdateMap(s);
    }

    private void OnFloorClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: int index })
            PickFloor(index);
    }

    // ---- quest cards and linked highlighting ----

    private QuestCardView? BuildCard(string questId) =>
        _snapshot is { Data: { } data } s ? QuestCards.Build(data, s.Quests, questId) : null;

    private Windows.Foundation.Rect BoundsInWindow(FrameworkElement element) =>
        element.TransformToVisual(Content).TransformBounds(new Windows.Foundation.Rect(0, 0, element.ActualWidth, element.ActualHeight));

    private void OnQuestHovered(FrameworkElement element, string? questId)
    {
        // Rows inside pinned windows highlight here too, but their cards would open in the wrong window.
        if (element.XamlRoot != Content.XamlRoot)
            return;
        if (questId is null)
            _cards.Leave();
        else
            _cards.Hover(questId, BoundsInWindow(element));
    }

    private IReadOnlySet<string> MapFocus()
    {
        if (Linked.Current is not { } focus)
            return new HashSet<string>();
        var ids = new HashSet<string>(focus.Quests);
        if (focus.Marker is { } marker)
            ids.Add(marker);
        return ids;
    }

    private void OnFocusChanged()
    {
        if (Map.Scene is not { } scene)
            return;
        scene.Focus = MapFocus();
        Map.Redraw();
    }

    private void OnMarkerHovered(MapMarker? marker, Windows.Foundation.Point at)
    {
        switch (marker)
        {
            case null:
                Linked.Set(null);
                _cards.Leave();
                break;
            case { Group: { } quest, Objective: not null }:
                Linked.Set(Focus.Quest(quest));
                var p = Map.TransformToVisual(Content).TransformPoint(at);
                _cards.Hover(quest, new Windows.Foundation.Rect(p.X - 8, p.Y - 8, 16, 16));
                break;
            default:
                Linked.Set(new Focus(new HashSet<string>(), Marker: marker.Id));
                _cards.Leave();
                break;
        }
    }

    private void OnMarkerClicked(MapMarker marker)
    {
        if (marker is { Group: { } quest, Objective: not null })
            Select(quest);
    }

    private void Select(string questId)
    {
        _selectedQuest = _selectedQuest == questId ? null : questId;
        if (Map.Scene is { } scene)
        {
            scene.Selected = _selectedQuest;
            Map.Refresh();
        }
    }

    // ---- pinned cards ----

    private const string PinnedSetting = "pinned.cards";

    private void Pin(QuestCardView view, PointInt32? at)
    {
        if (_pinned.TryGetValue(view.QuestId, out var open))
        {
            open.Activate();
            return;
        }
        var window = new QuestWindow(view, this, at);
        window.Closed += (_, _) =>
        {
            if (_pinned.Remove(window.QuestId))
                SavePinned();
        };
        _pinned[view.QuestId] = window;
        window.Activate();
        SavePinned();
    }

    private void SavePinned()
    {
        if (SnapshotMode)
            return;
        _session.SetSetting(PinnedSetting, string.Join(";", _pinned.Values.Select(w =>
            FormattableString.Invariant($"{w.QuestId}@{w.AppWindow.Position.X},{w.AppWindow.Position.Y}"))));
    }

    // Pinned cards come back where they were, as long as their quest is still active.
    private void RestorePinnedOnce(SessionSnapshot s)
    {
        if (_pinnedRestored || s.Data is null || SnapshotMode)
            return;
        _pinnedRestored = true;
        foreach (var entry in (_session.GetSetting(PinnedSetting) ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = entry.Split('@', ',');
            if (parts.Length != 3 || BuildCard(parts[0]) is not { State: QuestState.Active } view)
                continue;
            PointInt32? at = int.TryParse(parts[1], CultureInfo.InvariantCulture, out var x) && int.TryParse(parts[2], CultureInfo.InvariantCulture, out var y)
                && DisplayArea.GetFromPoint(new PointInt32(x + 40, y + 20), DisplayAreaFallback.None) is not null
                ? new PointInt32(x, y)
                : null;
            Pin(view, at);
        }
    }

    // A finished quest has nothing left to show: its card closes.
    private void RefreshPinned()
    {
        foreach (var window in _pinned.Values.ToList())
        {
            var view = BuildCard(window.QuestId);
            if (view is { State: QuestState.Active })
            {
                if (view.Status != window.View?.Status)
                    window.Update(view);
                continue;
            }
            if (view is { State: QuestState.Completed })
                ShowNotice($"{view.Name} is complete; its card is closed.");
            window.Close();
        }
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

    /// <summary>"--show-quest &lt;part of a name&gt;" (snapshots): highlights that quest, holds its card and pins it.</summary>
    public string? ShowQuest { get; set; }

    private void ShowQuestForSnapshot(SessionSnapshot s)
    {
        if (ShowQuest is not { } text || s.Data is null || s.Plan.Count == 0 && s.Objectives.Count == 0)
            return;
        ShowQuest = null;
        var id = s.Plan.SelectMany(p => p.Finish.Concat(p.Progress)).Select(q => (q.QuestId, q.Name))
            .Concat(s.Objectives.Select(o => (o.QuestId, Name: o.QuestName)))
            .FirstOrDefault(q => q.Name.Contains(text, StringComparison.OrdinalIgnoreCase)).QuestId;
        if (id is null || BuildCard(id) is not { } view)
            return;
        // Let the rail lay out first, so the highlight lands on real rows.
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            Linked.Set(Focus.Quest(id));
            _cards.ShowHeld(id, new Windows.Foundation.Rect(372, 150, 8, 8));
            var origin = QuestWindow.ScreenPoint(this, new Windows.Foundation.Point(((FrameworkElement)Content).ActualWidth - 420, 120));
            Pin(view, origin);
        });
    }

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
        // Accelerators on the root would otherwise show their key ("F") as a tooltip over the whole window.
        root.KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;
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
        Add(Windows.System.VirtualKey.Escape, () =>
        {
            if (_cards.ShownQuest is not null)
                _cards.Close();
            else
                ClearSelection();
        });
        Add(Windows.System.VirtualKey.PageUp, () => PickFloor(_shownFloor - 1));
        Add(Windows.System.VirtualKey.PageDown, () => PickFloor(_shownFloor + 1));
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
        if (e.ClickedItem is ObjectiveItem item)
            Select(item.QuestId);
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
            if (_cards.ShownQuest is not null)
            {
                AppLog.Info($"Snapshot: card {_cards.ShownQuest} is {_cards.Mode}");
                await RenderToPngAsync(_cards.Card, Path.Combine(folder, "card.png"));
            }
            if (_pinned.Values.FirstOrDefault() is { } pinned)
                await RenderToPngAsync(pinned.Card, Path.Combine(folder, "pinned.png"));
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
