using System.Collections;
using System.Globalization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Shturmap.App.Controls;
using Shturmap.Core.Logs;
using Shturmap.Core.Maps;
using Shturmap.Core.Navigation;
using Shturmap.Core.Planning;
using Shturmap.Core.Quests;
using Shturmap.Core.Raid;
using Shturmap.Data.TarkovDev;
using Shturmap.Map;
using Shturmap.Session;
using SkiaSharp.Views.Windows;
using Windows.Graphics;
using MapLayer = Shturmap.Core.Maps.MapLayer;

namespace Shturmap.App;

public sealed partial class MainWindow : Window
{
    private readonly GameSession _session;
    private readonly DispatcherQueueTimer _clock;
    private readonly DispatcherQueueTimer _noticeTimer;
    private SessionSnapshot? _snapshot;
    private string? _sceneKey;
    private readonly HashSet<string> _sheetNoticeShown = new(StringComparer.Ordinal);
    private string? _selectedQuest;
    private bool _updatingPicker;
    private bool _helpShownOnce;
    private readonly CardStack _cards;
    private MapMarker? _hoveredMarker;
    private readonly Dictionary<string, QuestWindow> _pinned = [];
    private bool _pinnedRestored;
    private IReadOnlyList<MapLayer?> _floors = [];
    private string? _floorsFor;
    private int? _floorPick;
    private DateTime? _floorPickFix;
    private int _shownFloor = -1;

    public MainWindow(GameSession session, SizeInt32? size = null)
    {
        _session = session;
        InitializeComponent();
        AppWindow.SetIcon(App.IconPath);
        // A flat dark title bar like the rest; no translucent backdrop.
        AppWindow.TitleBar.BackgroundColor = (Windows.UI.Color)Application.Current.Resources["RailColor"];
        AppWindow.TitleBar.InactiveBackgroundColor = (Windows.UI.Color)Application.Current.Resources["RailColor"];
        AppWindow.TitleBar.ForegroundColor = (Windows.UI.Color)Application.Current.Resources["InkColor"];
        AppWindow.TitleBar.InactiveForegroundColor = (Windows.UI.Color)Application.Current.Resources["MutedColor"];
        AppWindow.TitleBar.ButtonBackgroundColor = (Windows.UI.Color)Application.Current.Resources["RailColor"];
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = (Windows.UI.Color)Application.Current.Resources["RailColor"];
        AppWindow.TitleBar.ButtonForegroundColor = (Windows.UI.Color)Application.Current.Resources["InkColor"];
        AppWindow.TitleBar.ButtonHoverBackgroundColor = (Windows.UI.Color)Application.Current.Resources["RaisedColor"];
        PlaceOnSecondMonitor(size);

        Picture.Art = () => _session.Art;
        Study.Log = session.Study;
        var root = (FrameworkElement)Content;
        StudyAttention(root);
        _cards = new CardStack(root, CreateCard, () => new Windows.Foundation.Rect(0, 0, root.ActualWidth, root.ActualHeight), besideRoot: false);
        HookPins(_cards, this);
        // Rows in any window (this one or a pinned card) open their cards in that window's stack.
        Linked.Hovered += (element, key) => CardStack.For(element.XamlRoot)?.Enter(element, key);
        Linked.Left += element => CardStack.For(element.XamlRoot)?.Exit(element);
        // A click on a quest keeps its card open; the highlighter beside it (rows, cards) keeps it lit on the map.
        Linked.Clicked += (element, key) => CardStack.For(element.XamlRoot)?.Click(element, key);
        Linked.KeepRequested += quest => Select(quest, "toggle");
        Linked.FocusChanged += OnFocusChanged;
        // A click on nothing in particular (bare rail, bare map) lets go of held cards. Rows, markers and buttons
        // handle their own clicks.
        root.Tapped += (_, e) =>
        {
            if (!e.Handled && !Linked.IsInside(e.OriginalSource) && _cards.AnyHeld)
                _cards.CloseAll();
        };
        root.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler((_, e) =>
        {
            if (_cards.AnyHeld)
                _cards.PointerAt(e.GetCurrentPoint(root).Position);
        }), handledEventsToo: true);
        _focusClear = DispatcherQueue.CreateTimer();
        _focusClear.Interval = TimeSpan.FromMilliseconds(250);
        _focusClear.IsRepeating = false;
        _focusClear.Tick += (_, _) => ApplyMapFocus();
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
        _noticeTimer.Tick += (_, _) =>
        {
            if (ViewModel.NoticeOpen)
                Study.Ui("notice.expire", ("text", ViewModel.NoticeText));
            ViewModel.NoticeOpen = false;
        };

        // A new position never moves the view; out of view, the edge arrow points to it and a notice says so.
        Map.PlayerPinged += offScreen =>
        {
            Study.Ui("fix.ping", ("inView", !offScreen));
            if (offScreen)
                ShowNotice("Your new position is outside the part of the map shown: press F, or click the arrow at the edge.", TimeSpan.FromSeconds(5));
        };
        Map.EdgeClicked += () => Study.Ui("map.showme", ("how", "edge"));
        AddShortcuts((UIElement)Content);

        // Snapshots arrive on background threads; only the newest one is applied.
        session.Changed += s =>
        {
            Volatile.Write(ref _snapshot, s);
            DispatcherQueue.TryEnqueue(() => Apply(s));
        };
        session.Notice += notice => DispatcherQueue.TryEnqueue(() => ShowNotice(notice.Text, notice.Duration, notice.OffersReport));
        session.Cue += cue => DispatcherQueue.TryEnqueue(() => ShowCue(cue));
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
        {
            // "ahead-left" turns into "NE": in the rail and on the cards.
            UpdateRaidLists(s);
            _cards.Refresh(UpdateCard);
            RefreshPinned();
        }
        Map.Redraw(); // the player's ring and age tag follow the fix's age
    }

    public MainViewModel ViewModel { get; } = new();

    // ---- x:Bind helpers ----

    public Brush RaidBrush(bool inRaid) => Resource(inRaid ? "AmberBrush" : "InkBrush");

    /// <summary>The keys in the help panel.</summary>
    public IReadOnlyList<KeyHelp> Keys { get; } =
    [
        new("F", "Show my position (the map never moves by itself)"),
        new("+ / −", "Zoom in / out (or the mouse wheel)"),
        new("0", "Show the whole map"),
        new("PGUP / PGDN", "Show the floor above / below"),
        new("ESC", "Close the cards; again to stop highlighting the quest"),
        new("F1 / ?", "This help"),
        new("MOUSE", "Drag to move the map, double-click to zoom in. Click a quest to keep its card open; click its highlighter to keep it lit on the map"),
    ];

    /// <summary>
    /// The map symbols in the help panel, drawn by the map's own renderer so they can't drift from the map (at twice
    /// the DIP size, which stays sharp up to 200 % and in snapshots).
    /// </summary>
    public IReadOnlyList<MapLegendRow> MapLegendRows { get; } = MapLegend.Rows
        .Select(row =>
        {
            using var bitmap = MapLegend.Draw(row.Symbol, 2);
            return new MapLegendRow(bitmap.ToWriteableBitmap(), row.Text);
        })
        .ToList();

    public Brush OkBrush(bool ok) => Resource(ok ? "GreenBrush" : "AmberBrush");

    public Visibility ShownIf(string? text) => string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ShownIfTrue(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ShownIfNot(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ShownIfAny(IEnumerable? items) => items?.GetEnumerator().MoveNext() == true ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ShownIfLink(Uri? link) => link is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ShownIfSet(object? value) => value is null ? Visibility.Collapsed : Visibility.Visible;

    private void OnWikiMapClick(object sender, RoutedEventArgs e) => Study.Ui("wiki.map", ("url", ViewModel.WikiMap?.ToString()));

    /// <summary>Shturmap's licence and the third-party ones a published build carries (eng\notices.ps1).</summary>
    public static string LicencesFolder { get; } = Path.Combine(AppContext.BaseDirectory, "licenses");

    /// <summary>A developer build has no licences folder, so the help panel shows the link only in a published one.</summary>
    public Visibility LicencesVisibility { get; } = Directory.Exists(LicencesFolder) ? Visibility.Visible : Visibility.Collapsed;

    private void OnLicencesClick(object sender, RoutedEventArgs e) => OpenFolder(LicencesFolder, "licences");

    private void OnLogFolderClick(object sender, RoutedEventArgs e)
    {
        Study.Ui("help.logs");
        OpenFolder(AppLog.Folder ?? AppPaths.Default.Logs, "log");
    }

    // Opens a folder of Shturmap's own in Explorer.
    private static void OpenFolder(string folder, string what)
    {
        try
        {
            Directory.CreateDirectory(folder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        {
            AppLog.Warn($"Opening the {what} folder failed", ex);
        }
    }

    // What a report needs, on the clipboard for the player to paste and read first; nothing is sent (docs/DESIGN.md
    // §8, "Diagnostics").
    private void OnCopyDiagnosticsClick(object sender, RoutedEventArgs e)
    {
        if (Volatile.Read(ref _snapshot) is null)
            return;
        var text = DiagnosticsText();
        try
        {
            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
            package.SetText(text);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
            // Kept on the clipboard after Shturmap closes.
            Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
            ShowNotice("Diagnostics copied: paste them into your message.");
        }
        catch (Exception ex)
        {
            AppLog.Warn("Copying the diagnostics failed", ex);
            ShowNotice("Couldn't copy the diagnostics: another app may be using the clipboard. Try again.");
        }
        Study.Ui("help.diagnostics");
    }

    private string DiagnosticsText() =>
        Diagnostics.Build(Volatile.Read(ref _snapshot) ?? new SessionSnapshot(), GameSession.Version, Diagnostics.WindowsVersion(), App.BuildKind,
            AppLog.Tail(Diagnostics.LogLines), DateTime.Now, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    private async void OnStudyLogClick(object sender, RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.Controls.Primitives.ToggleButton toggle)
            await _session.SetStudyLogAsync(toggle.IsChecked == true);
    }

    // The study switch's box: filled gold with a check when on, an empty gold square when off.
    public Brush StudyBoxBrush(bool on) => on ? Resource("AmberBrush") : new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    private static Brush Resource(string key) => (Brush)Application.Current.Resources[key];

    // ---- snapshot → view ----

    private void Apply(SessionSnapshot s)
    {
        if (!ReferenceEquals(s, _snapshot))
            return; // a newer snapshot is queued behind this one
        if (DemoHolds(s))
            return; // the website demo's pause; applied when it ends

        var vm = ViewModel;
        vm.ModeText = ModeReading.Text(s.Mode);
        vm.ModeDetail = s.ModeReading.Tooltip(gameFound: s.Locations is null || s.Locations.Install is not null, DateTime.Now);
        vm.InRaid = s.Raid.Phase != RaidPhase.Menu;
        vm.LogsText = s.Logs.Text;
        vm.LogsOk = s.Logs.Ok;
        vm.ScreenshotsText = s.Screenshots.Text;
        vm.ScreenshotsOk = s.Screenshots.Ok;
        vm.DataText = s.DataHealth.Text;
        vm.DataOk = s.DataHealth.Ok;
        vm.DataDetail = s switch
        {
            { Data: { Offline: true } data } => $"A saved copy of tarkov.dev's data from {data.CheckedAt.ToLocalTime():g}: tarkov.dev couldn't be reached.",
            { Data: { } data } => $"Game data from tarkov.dev, checked {data.CheckedAt.ToLocalTime():t}.",
            { DataProblem: { } problem } => GameSession.DataNotice(problem),
            _ => "Loading game data from tarkov.dev…",
        };
        vm.StudyLogOn = s.StudyLogOn;
        vm.HelpKeys = s.ScreenshotKeys.Count > 0 ? string.Join(" or ", s.ScreenshotKeys) : "your screenshot key";
        // A raid loading ends any preview at once: the raid's map is what matters now.
        if (vm.InRaid && _previewing is not null)
        {
            _previewTimer?.Stop();
            EndPreview(restore: false);
        }
        // A kept quest that is done (or failed) has nothing left to find.
        if (_selectedQuest is not null && s.Quests.GetValueOrDefault(_selectedQuest)?.State != QuestState.Active)
        {
            _selectedQuest = null;
            ShowSelection();
        }
        UpdateClockTexts();
        UpdatePicker(s);
        UpdatePlan(s);
        UpdateRaidLists(s);
        UpdateMap(s);
        _cards.Refresh(UpdateCard);
        RefreshPinned();
        RestorePinnedOnce(s);
        ShowHelpOnFirstRun(s);
        ShowQuestForSnapshot(s);
        DemoOnSnapshot(s);
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
            // A Scav joins a raid already under way, and how long is left isn't in the logs: better no time than a
            // wrong one.
            if (s.Raid.Side == RaidSide.Scav && s.Raid.Phase == RaidPhase.InRaid)
                parts.Add("joined under way");
            else if (elapsed is { } e && info.RaidMinutes > 0)
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
        if (s.Raid.Phase == RaidPhase.Loading)
        {
            ViewModel.LoadingText = LoadingProgress.Text(s.Raid);
            ShowLoadingSteps(LoadingProgress.Done(s.Raid));
        }
        else
        {
            ViewModel.LoadingText = "";
        }
        UpdateStale(s, elapsed);

        if (s.Fix is not { } fix)
        {
            ViewModel.FixText = $"No position yet · press {ViewModel.HelpKeys} in raid";
            return;
        }
        var age = DateTime.Now - fix.At;
        var ago = age.TotalSeconds < 60 ? $"{Math.Max(0, (int)age.TotalSeconds)} s" : age.TotalMinutes < 60 ? $"{(int)age.TotalMinutes} min" : $"{(int)age.TotalHours} h";
        ViewModel.FixText = $"Fix {ago} ago · {s.Floor?.Name ?? "ground"} · height {fix.Position.Y.ToString("0", CultureInfo.CurrentCulture)} m";
    }

    // A position older than this is too old to show without saying so in big type (the study log: positions came
    // about every 8 minutes and were often several minutes old when the app was looked at).
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(2);

    private void UpdateStale(SessionSnapshot s, TimeSpan? inRaid)
    {
        var age = s.Fix is { } fix ? DateTime.Now - fix.At : (TimeSpan?)null;
        ViewModel.StaleText = s.Raid.Phase != RaidPhase.InRaid ? ""
            : age is null ? (inRaid > TimeSpan.FromMinutes(1) ? "NO POSITION YET" : "")
            : age >= StaleAfter ? $"POSITION {(int)age.Value.TotalMinutes} MIN OLD"
            : "";
        ViewModel.StaleHint = ViewModel.StaleText.Length > 0 ? Caps.Of($"Press {ViewModel.HelpKeys} for a new one") : "";
    }

    // When the window gets focus with a stale position, the banner pops once, so the glance lands on it.
    private void PopStale()
    {
        if (ViewModel.StaleText.Length == 0)
            return;
        Study.Ui("stale.seen", ("text", ViewModel.StaleText));
        if (!new Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
            return;
        var story = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
        foreach (var property in new[] { "ScaleX", "ScaleY" })
        {
            var frames = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimationUsingKeyFrames();
            frames.KeyFrames.Add(new Microsoft.UI.Xaml.Media.Animation.LinearDoubleKeyFrame { KeyTime = TimeSpan.Zero, Value = 1 });
            frames.KeyFrames.Add(new Microsoft.UI.Xaml.Media.Animation.EasingDoubleKeyFrame
            {
                KeyTime = TimeSpan.FromMilliseconds(140), Value = 1.12,
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut },
            });
            frames.KeyFrames.Add(new Microsoft.UI.Xaml.Media.Animation.EasingDoubleKeyFrame
            {
                KeyTime = TimeSpan.FromMilliseconds(520), Value = 1,
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.ElasticEase { Oscillations = 1, Springiness = 4, EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut },
            });
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(frames, StaleScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(frames, property);
            story.Children.Add(frames);
        }
        story.Begin();
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
        QuestLine OnMap(PlanQuestView q, MapPlanView p) => Line(q) with { Needs = Chips(s, p, q.QuestId), Synopsis = q.Synopsis, StartsGroup = q.StartsGroup };
        vm.Plans = s.Plan.Select((p, i) => new PlanCard(
            p.NormalizedName,
            p.MapName,
            Summary(p.Finish.Count, p.Progress.Count),
            string.Join(" · ", new[] { p.WalkingMinutes > 0 ? $"~{p.WalkingMinutes} min walking" : null, p.RaidMinutes > 0 ? $"{p.RaidMinutes} min raid" : null }
                .Concat(p.Bosses).OfType<string>()),
            i == openIndex,
            p.Finish.Select(q => OnMap(q, p)).ToList(),
            p.Progress.Select(q => OnMap(q, p)).ToList(),
            p.Requirements.Select(r => BringLine(s, r)).ToList(),
            (i + 1).ToString(CultureInfo.InvariantCulture),
            p.Requirements.Select(r => Chip(s, r)).ToList()
        )).ToList();
        vm.AnyMap = s.AnyMap.Select(Line).ToList();
    }

    /// <summary>The easiest way to get an item, for one line under it ("Prapor LL1 · 18,936 ₽"), or empty.</summary>
    private static string BestSource(SessionSnapshot s, string itemId) =>
        s.Data is { } data ? ItemCards.Best(data, s.Sources, itemId)?.Text ?? "" : "";

    private static string GlyphOf(RequirementKind kind) => kind == RequirementKind.Key ? Glyphs.Key : Glyphs.Bring;

    /// <summary>A BRING row: the item, what it is for and for which quests, and where to get it.</summary>
    private static RequirementLine BringLine(SessionSnapshot s, RequirementView r) =>
        new(GlyphOf(r.Kind), r.Text, r.Why, r.ItemId, r.QuestIds, BestSource(s, r.ItemId));

    /// <summary>A tiny cell for an item; its tooltip says what it is, what for, and where to get it.</summary>
    private static NeedChip Chip(SessionSnapshot s, RequirementView r) =>
        new(r.ItemId, GlyphOf(r.Kind), string.Join("\n", new[] { r.Text, r.Why, BestSource(s, r.ItemId) }.Where(t => t.Length > 0)));

    /// <summary>What one quest needs brought on a plan's map, as tiny cells beside its name (empty: nothing).</summary>
    private static IReadOnlyList<NeedChip> Chips(SessionSnapshot s, MapPlanView? plan, string questId) =>
        (plan?.Requirements ?? []).Where(r => r.QuestIds.Contains(questId)).Select(r => Chip(s, r)).ToList();

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

    // The raid card is the plan card for this map, live: the same header and COMPLETE / PROGRESS / BRING sections,
    // with each quest's objectives here under it, nearest first, and the quests ordered by their nearest objective.
    private void UpdateRaidLists(SessionSnapshot s)
    {
        var vm = ViewModel;
        var age = s.Fix is { } fix ? DateTime.Now - fix.At : (TimeSpan?)null;
        var fresh = age < FreshFix;
        _fixWasFresh = age is null ? null : fresh;
        string Direction(RelativeDirection? relative, double? mapBearing) =>
            fresh && relative is { } r ? Bearing.Describe(r) : mapBearing is { } b ? Bearing.Compass(b) : "";

        vm.RaidTitle = Caps.Of(s.Map?.Name);
        // When the logs can't tell (PvE), the raid is shown as a PMC's and the tag is a switch to say otherwise.
        vm.RaidSide = s.Raid.Side switch { RaidSide.Scav => "SCAV", RaidSide.Pmc => "PMC", _ => vm.InRaid ? "PMC" : "" };
        vm.SideSwitchable = vm.InRaid && !s.SideFromLogs;
        vm.ScavRaid = vm.InRaid && s.Raid.Side == RaidSide.Scav;
        vm.RaidSummary = s.MapPlan is { } plan && plan.Finish.Count + plan.Progress.Count > 0 ? Summary(plan.Finish.Count, plan.Progress.Count) : "";
        vm.RaidFixNote = age switch
        {
            null => $"No position yet: press {vm.HelpKeys} for distances",
            _ when fresh => "",
            { TotalMinutes: < 60 } a => $"Distances from your screenshot {Math.Max(1, (int)a.TotalMinutes)} min ago",
            _ => "Distances from your last screenshot",
        };
        var complete = s.MapPlan?.Finish.Select(q => q.QuestId).ToHashSet() ?? [];
        var kinds = (s.MapPlan?.Finish ?? []).Concat(s.MapPlan?.Progress ?? []).ToDictionary(q => q.QuestId, q => q.Kind);
        var quests = s.Objectives
            .GroupBy(o => o.QuestId)
            .Select(g =>
            {
                var first = g.First();
                var objectives = g.OrderBy(o => o.HasPlace ? 0 : 1).ThenBy(o => o.Distance ?? double.MaxValue)
                    .Select(o => ToItem(o, o.HasPlace ? Direction(o.Direction, o.MapBearing) : Unplaced(o.Kind), s.Map?.Name))
                    .ToList();
                return (Nearest: g.Min(o => o.Distance ?? double.MaxValue), Quest: new RaidQuest(g.Key,
                    kinds.TryGetValue(g.Key, out var kind) ? kind : QuestTaxonomy.QuestKind(g.Select(o => o.Kind)),
                    first.QuestName, first.TraderId, first.Trader, objectives, complete.Contains(g.Key), Chips(s, s.MapPlan, g.Key)));
            })
            .OrderBy(q => q.Nearest)
            .ThenBy(q => q.Quest.Name, StringComparer.CurrentCulture)
            .Select(q => q.Quest)
            .ToList();
        vm.RaidComplete = quests.Where(q => q.Complete).ToList();
        vm.RaidProgress = quests.Where(q => !q.Complete).ToList();
        vm.RaidBring = (s.MapPlan?.Requirements ?? []).Select(r => BringLine(s, r)).ToList();
        vm.RaidNote = "";
        vm.RaidLoot = [];
        vm.RaidLootMore = "";
        if (vm.ScavRaid)
            ShowScavRaid(s);
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
            Direction(e.Direction, e.MapBearing),
            e.Needs,
            e.NeedItemId)).ToList();

        // The glance: where to go next and the nearest way out, the two things a few seconds' look is for (the study
        // log: in a raid the app got glances with a median of 3.9 s).
        vm.RaidNext = vm.ScavRaid ? null
            : s.Objectives.Where(o => o.HasPlace && o.Distance is not null).MinBy(o => o.Distance) is { } nearest
                ? ToItem(nearest, Direction(nearest.Direction, nearest.MapBearing), s.Map?.Name)
                : null;
        vm.RaidExit = vm.Extracts.FirstOrDefault(e => e.Distance.Length > 0);

        vm.Hint = s.Data is null ? "Loading quests and maps…"
            : !vm.InRaid && s.Plan.Count == 0 ? "None of your active quests is tied to a map."
            : vm.InRaid && !vm.ScavRaid && s.Objectives.Count == 0 ? $"None of your {s.ActiveQuestCount} active quests has an objective on this map."
            : "";
        // Credit the artist only where their SVG is drawn: maps.json also names the authors of tile renders, which
        // Shturmap doesn't use.
        vm.Attribution = s.Definition switch
        {
            { SvgPath: not null, Author: { } author } => $"Map © {author} and contributors, CC BY-NC-SA 4.0 · data tarkov.dev",
            { SvgPath: null } => "No map artwork · grid 10 m · data tarkov.dev",
            _ => "Data tarkov.dev",
        };
        // The wiki's interactive map for this map: its page name plus "_Interactive_Map".
        vm.WikiMap = s.Map is { } shown && s.Data?.Maps.GetValueOrDefault(shown.Id)?.Wiki is { Length: > 0 } wiki
            && Uri.TryCreate(wiki.TrimEnd('/') + "_Interactive_Map", UriKind.Absolute, out var uri) ? uri : null;
    }

    // The loot a Scav raid can find for your quests, up to this many rows; the rest is one line.
    private const int LootShown = 8;

    // A Scav raid in the raid card: quest objectives only count for the PMC, so instead of COMPLETE and PROGRESS the
    // card lists what your quests need found in raid, which counts whoever finds it.
    private void ShowScavRaid(SessionSnapshot s)
    {
        var vm = ViewModel;
        var loot = s is { Data: { } data, Map: { } map } ? ScavRaid.Loot(data, s.Quests, data.MapIdsSharing(map.NormalizedName)) : [];
        var quests = loot.SelectMany(l => l.QuestIds).Distinct().Count();
        vm.RaidSummary = quests switch { 0 => "", 1 => "Find items for 1 quest", _ => $"Find items for {quests} quests" };
        vm.RaidNote = "As a Scav, quest objectives don't count; items you find in raid do.";
        vm.RaidComplete = [];
        vm.RaidProgress = [];
        vm.RaidBring = [];
        vm.RaidLoot = loot.Take(LootShown).Select(l => new RequirementLine(Glyphs.Bring, l.Text, "for " + l.ForQuests, l.ItemId, l.QuestIds,
            l.SpotsHere switch { 0 => "", 1 => "Loose here · 1 spot", var n => $"Loose here · {n} spots" })).ToList();
        vm.RaidLootMore = loot.Count > LootShown ? $"And {loot.Count - LootShown} more items your quests need found in raid." : "";
    }

    // Where an objective without a place on the map is done: kills and finds anywhere on it, hand-overs at the trader.
    private static string Unplaced(ObjectiveKind kind) => kind switch
    {
        ObjectiveKind.Trader => "after the raid",
        ObjectiveKind.Survive => "",
        _ => "anywhere",
    };

    private static ObjectiveItem ToItem(ObjectiveView o, string direction, string? mapName)
    {
        if (o.HeightDifference is { } h)
            direction += (direction.Length > 0 ? " · " : "") + $"{Math.Abs(h):0} m {(h > 0 ? "up" : "down")}";
        // "… on Streets of Tarkov" says nothing while on Streets of Tarkov.
        var suffix = " on " + mapName;
        var text = mapName is not null && o.Text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? o.Text[..^suffix.Length] : o.Text;
        return new ObjectiveItem(o.QuestId, text, string.IsNullOrEmpty(o.Trader) ? o.QuestName : $"{o.QuestName} · {o.Trader}",
            Distance(o.Distance), direction, o.Done, o.Kind, o.Needs ?? "", o.TraderId, o.Trader);
    }

    // The map's guide plate says the same (MapRenderer.DistanceText).
    private static string Distance(double? metres) => metres is { } m ? MapRenderer.DistanceText(m) : "";


    private async void UpdateMap(SessionSnapshot s)
    {
        // While another map is previewed, the shown one waits; it comes back when the preview ends.
        if (s.Definition is null || _session.Artwork is null || _previewing is not null)
            return;
        var key = s.Definition.Key;
        if (key != _sceneKey)
        {
            _sceneKey = key;
            var artwork = await ArtworkFor(s.Definition, s.Map?.Name);
            if (_sceneKey != key)
                return;
            // No usable artwork (docs/DESIGN.md §3): a sheet with a metric grid stands in; said once per map.
            if (artwork is null && _sheetNoticeShown.Add(key))
                ShowNotice($"No map artwork for {s.Map?.Name}: a 10 m grid stands in, with your position, objectives and extracts.");
            Map.SetScene(new MapScene(s.Definition, artwork), _restoreView);
            _restoreView = null;
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

    // A map's artwork, or null for the sheet. A download that fails says why, once per map, and the sheet stands in;
    // the next time the map is shown it is tried again.
    private async Task<MapArtwork?> ArtworkFor(MapDefinition definition, string? mapName)
    {
        try
        {
            return await _session.Artwork!.GetAsync(definition);
        }
        catch (Exception e)
        {
            var problem = LoadProblem.Explain(e);
            AppLog.Warn($"Map artwork for {mapName} not loaded ({problem.Kind}{(problem.Status is { } s ? " " + s : "")})", e);
            if (_sheetNoticeShown.Add(definition.Key))
            {
                var why = problem.Kind switch
                {
                    LoadFailure.Unreachable or LoadFailure.TimedOut => "couldn't download it; check the internet connection",
                    LoadFailure.ServerBusy or LoadFailure.Refused => $"its server answered {problem.Status}",
                    LoadFailure.Disk => "couldn't save it on this PC; check the free disk space",
                    _ => "couldn't read it",
                };
                ShowNotice($"No map artwork for {mapName}: {why}. A 10 m grid stands in, with your position, objectives and extracts.",
                    TimeSpan.FromSeconds(12));
            }
            return null;
        }
    }

    // ---- previewing another map from Plan ----

    // Resting on a folded Plan card shows its map for as long as the pointer stays, without switching to it; the
    // view of the shown map comes back as it was (the study log: ten card clicks in 4.5 minutes to compare maps).
    private static readonly TimeSpan PreviewAfter = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan PreviewEndAfter = TimeSpan.FromMilliseconds(300);
    private DispatcherQueueTimer? _previewTimer;
    private string? _previewWanted;
    private string? _previewing;
    private (Shturmap.Core.Maps.MapPoint Center, double Zoom)? _restoreView;

    private void OnPlanPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button { Tag: string map } && !ViewModel.InRaid && map != _snapshot?.Map?.NormalizedName)
            Preview(map, PreviewAfter);
    }

    private void OnPlanPointerExited(object sender, PointerRoutedEventArgs e) => Preview(null, PreviewEndAfter);

    private void Preview(string? map, TimeSpan after)
    {
        _previewWanted = map;
        if (_previewTimer is null)
        {
            _previewTimer = DispatcherQueue.CreateTimer();
            _previewTimer.IsRepeating = false;
            _previewTimer.Tick += (_, _) =>
            {
                if (_previewWanted == _previewing)
                    return;
                if (_previewWanted is { } wanted)
                    StartPreview(wanted);
                else
                    EndPreview(restore: true);
            };
        }
        _previewTimer.Stop();
        // Moving straight from one card to the next switches at once; only the first one waits.
        _previewTimer.Interval = map is not null && _previewing is not null ? TimeSpan.FromMilliseconds(120) : after;
        _previewTimer.Start();
    }

    private async void StartPreview(string normalizedName)
    {
        if (_snapshot is not { Data: { } data } s || data.MapByNormalizedName(normalizedName) is not { } map
            || data.DefinitionFor(normalizedName) is not { } definition || _session.Artwork is null)
            return;
        if (_previewing is null)
            _restoreView = Map.View;
        _previewing = normalizedName;
        var artwork = await ArtworkFor(definition, map.Name);
        if (_previewing != normalizedName)
            return;
        var active = s.Quests.Values.Where(q => q.State == QuestState.Active).Select(q => q.QuestId);
        var content = MapContentBuilder.Build(data, map.Id, active, new HashSet<string>());
        Map.SetScene(new MapScene(definition, artwork) { Markers = content.Markers, Zones = content.Zones });
        _sceneKey = null;
        ViewModel.PreviewText = $"PREVIEW · {Caps.Of(map.Name)}";
        Study.Ui("map.preview", ("map", normalizedName));
    }

    private void EndPreview(bool restore)
    {
        if (_previewing is null)
            return;
        _previewing = null;
        _previewWanted = null;
        ViewModel.PreviewText = "";
        _sceneKey = null;
        if (!restore)
            _restoreView = null;
        if (_snapshot is { } s)
            UpdateMap(s);
    }

    // ---- study log: attention ----

    private DateTime? _pointerIn;
    private DateTime? _focusedAt;

    // Shturmap can't see where the player looks; window focus and the pointer resting over it are the closest
    // signals. Both are logged as they change, with how long they lasted.
    private void StudyAttention(FrameworkElement root)
    {
        Activated += (_, e) =>
        {
            var focused = e.WindowActivationState != WindowActivationState.Deactivated;
            if (focused && _focusedAt is null)
            {
                _focusedAt = DateTime.Now;
                Study.Ui("window.focus", ("railY", RailScroll.VerticalOffset), ("railH", RailScroll.ViewportHeight),
                    ("visible", Linked.QuestsVisibleIn(RailScroll).ToList()));
                PopStale();
            }
            else if (!focused && _focusedAt is { } since)
            {
                _focusedAt = null;
                Study.Ui("window.blur", ("s", DateTime.Now - since));
            }
        };
        // Entered/exited also arrive from child elements; only crossing the window's edge counts.
        root.PointerEntered += (_, _) =>
        {
            if (_pointerIn is not null)
                return;
            _pointerIn = DateTime.Now;
            Study.Ui("pointer.in");
        };
        root.PointerExited += (_, e) =>
        {
            var p = e.GetCurrentPoint(root).Position;
            if (_pointerIn is not { } since || (p.X > 0 && p.Y > 0 && p.X < root.ActualWidth - 1 && p.Y < root.ActualHeight - 1))
                return;
            _pointerIn = null;
            Study.Ui("pointer.out", ("s", DateTime.Now - since));
        };
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

    private void PickFloor(int index, string how)
    {
        if (_floors.Count == 0 || _snapshot is not { } s)
            return;
        _floorPick = Math.Clamp(index, 0, _floors.Count - 1);
        _floorPickFix = s.Fix?.At;
        Study.Ui("map.floor", ("floor", _floors[_floorPick.Value]?.Name ?? "Ground"), ("how", how));
        UpdateMap(s);
    }

    private void OnFloorClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: int index })
            PickFloor(index, "click");
    }

    // ---- quest cards and linked highlighting ----

    private QuestCardView? BuildCard(string questId) =>
        _snapshot is { Data: { } data } s ? QuestCards.Build(data, s.Quests, questId, LiveText, s.Sources) : null;

    private FrameworkElement? CreateCard(CardKey key) => key switch
    {
        CardKey.Quest q when BuildCard(q.Id) is { } view => new QuestCard(view),
        CardKey.Item i when _snapshot is { Data: { } data } s => new ItemCard(ItemCards.Build(data, s.Sources, s.Quests, i.Id)),
        _ => null,
    };

    // Open cards follow the session: a quest's status and distances, the quests an item is needed for.
    private bool UpdateCard(FrameworkElement card)
    {
        switch (card)
        {
            case QuestCard quest when BuildCard(quest.View.QuestId) is { } view:
                quest.Show(view);
                return true;
            case ItemCard item when _snapshot is { Data: { } data } s:
                item.Show(ItemCards.Build(data, s.Sources, s.Quests, item.View.ItemId));
                return true;
            default:
                return false;
        }
    }

    // A quest card's pop-out button, in whichever window's stack it opened, turns the card into a window of its own
    // ("pinned" in the code) where the card was: the card itself closes, so the quest isn't shown twice.
    private void HookPins(CardStack stack, Window window) => stack.CardOpened += card =>
    {
        if (card is QuestCard quest)
        {
            quest.PinClicked += c =>
            {
                var at = QuestWindow.ScreenPoint(window, stack.PositionOf(c));
                stack.Close(c);
                Pin(c.View, at);
            };
        }
    };

    /// <summary>"121 m · NE · 3 m up" for an objective on the shown map, from the last fix; empty without one.</summary>
    private string? LiveText(string objectiveId)
    {
        if (_snapshot is not { Fix: { } fix } s || s.Objectives.FirstOrDefault(o => o.ObjectiveId == objectiveId && o.Distance is not null) is not { } o)
            return null;
        var fresh = DateTime.Now - fix.At < FreshFix;
        var direction = fresh && o.Direction is { } r ? Bearing.Describe(r) : o.MapBearing is { } b ? Bearing.Compass(b) : "";
        var parts = new[] { Distance(o.Distance), direction, o.HeightDifference is { } h ? $"{Math.Abs(h):0} m {(h > 0 ? "up" : "down")}" : "" };
        return string.Join(" · ", parts.Where(p => p.Length > 0));
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

    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _focusClear;

    // Moving from one row to the next passes through "nothing in focus" for a moment; showing that moment would
    // make every marker on the map blink. So a new focus applies at once, but losing it waits a little.
    private void OnFocusChanged()
    {
        _focusClear.Stop();
        if (Linked.Current is null)
            _focusClear.Start();
        else
            ApplyMapFocus();
    }

    private void ApplyMapFocus()
    {
        if (Map.Scene is not { } scene)
            return;
        scene.Focus = MapFocus();
        // Pointing at an item shows where it lies loose on the shown map.
        scene.Spawns = Linked.Current?.Item is { } item && _snapshot is { Data: { } data, Map: { } map }
            ? data.SpawnsOf(item).Where(s => data.MapIdsSharing(map.NormalizedName).Contains(s.MapId)).Select(s => s.Position).ToList()
            : [];
        Map.Redraw();
    }

    private DateTime _markerHoveredAt;

    private void OnMarkerHovered(MapMarker? marker, Windows.Foundation.Point at)
    {
        if (_hoveredMarker is { } previous)
        {
            _cards.Exit(previous);
            if (DateTime.Now - _markerHoveredAt is { TotalMilliseconds: >= 400 } dwell)
                Study.Ui("hover", ("marker", previous.Id), ("quest", previous.Group), ("name", previous.Label), ("s", dwell), ("where", "map"));
        }
        _hoveredMarker = marker;
        _markerHoveredAt = DateTime.Now;
        _markerAt = at;
        switch (marker)
        {
            case null:
                Linked.Set(null);
                break;
            case { Group: { } quest, Objective: not null }:
                Linked.Set(Focus.Quest(quest));
                var p = Map.TransformToVisual(Content).TransformPoint(at);
                _cards.Enter(marker, new CardKey.Quest(quest), new Windows.Foundation.Rect(p.X - 8, p.Y - 8, 16, 16));
                break;
            case { Group: { } group }:
                // A boss: all its spawn zones light up together, including zones it shares with another boss.
                var bosses = MapContentBuilder.BossesOf(group);
                var groups = (Map.Scene?.Markers ?? []).Select(m => m.Group).OfType<string>()
                    .Where(g => MapContentBuilder.BossesOf(g).Intersect(bosses).Any())
                    .Append(group).ToHashSet();
                Linked.Set(new Focus(groups));
                break;
            default:
                Linked.Set(new Focus(new HashSet<string>(), Marker: marker.Id));
                break;
        }
    }

    // Clicking a quest marker holds its card, like clicking the quest in the list; the card's highlighter keeps it lit.
    private void OnMarkerClicked(MapMarker marker)
    {
        if (marker is not { Group: { } quest, Objective: not null })
            return;
        var at = Map.TransformToVisual(Content).TransformPoint(_markerAt);
        _cards.Click(new CardKey.Quest(quest), new Windows.Foundation.Rect(at.X - 8, at.Y - 8, 16, 16));
    }

    private Windows.Foundation.Point _markerAt;

    // A quest's highlighter keeps it lit (rows tinted, markers cyan and ringed, a line to the nearest one) until it is
    // clicked again, another quest's is clicked, or Esc.
    private void Select(string questId, string how)
    {
        _selectedQuest = _selectedQuest == questId ? null : questId;
        Study.Ui("select", ("quest", questId), ("on", _selectedQuest is not null), ("how", how));
        ShowSelection();
    }

    private void ShowSelection()
    {
        Linked.Selected = _selectedQuest;
        if (Map.Scene is { } scene)
        {
            scene.Selected = _selectedQuest;
            Map.Redraw(); // starts the pulse and the dimming
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
        var window = new QuestWindow(view, this, at, CreateCard);
        HookPins(window.Stack, window);
        var pinnedAt = DateTime.Now;
        Study.Ui("card.pin", ("quest", view.QuestId), ("name", view.Name));
        window.Closed += (_, _) =>
        {
            Study.Ui("pinned.close", ("quest", view.QuestId), ("name", view.Name), ("openS", DateTime.Now - pinnedAt),
                ("x", window.AppWindow.Position.X), ("y", window.AppWindow.Position.Y));
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

    // Pinned cards follow the session (status, distances in a raid). A finished quest has nothing left to show:
    // its card closes.
    private void RefreshPinned()
    {
        foreach (var window in _pinned.Values.ToList())
        {
            var view = BuildCard(window.QuestId);
            if (view is { State: QuestState.Active })
            {
                window.Update(view);
                window.Stack.Refresh(UpdateCard);
                continue;
            }
            if (view is { State: QuestState.Completed })
                ShowNotice($"{view.Name} is complete; its card is closed.");
            window.Close();
        }
    }

    // One thin segment per loading step, gold once the log has reported that step, a hairline until then.
    private void ShowLoadingSteps(bool[] done)
    {
        if (LoadingSegments.Children.Count != done.Length)
        {
            LoadingSegments.Children.Clear();
            LoadingSegments.ColumnDefinitions.Clear();
            for (var i = 0; i < done.Length; i++)
            {
                LoadingSegments.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var segment = new Border();
                Grid.SetColumn(segment, i);
                LoadingSegments.Children.Add(segment);
            }
        }
        for (var i = 0; i < done.Length; i++)
            ((Border)LoadingSegments.Children[i]).Background = Resource(done[i] ? "AmberBrush" : "LineBrush");
    }

    // ---- the big cue: Shturmap changed its view on its own ----

    // Every cue lasts 5 s; its entrance is played at 1.8 times the original pace (owner, 2026-10-02: the elements
    // should appear more slowly, the cue shouldn't stay longer).
    private static TimeSpan CueLength(CueKind kind) => TimeSpan.FromSeconds(5);
    private const double CuePace = 1.8;
    private Microsoft.UI.Xaml.Media.Animation.Storyboard? _cueStory;
    private DispatcherQueueTimer? _cueTimer;

    private (string Eyebrow, string Title, string Detail) CueText(ViewCue cue)
    {
        var map = Caps.Of(cue.MapName);
        var plan = _snapshot?.Plan.FirstOrDefault(p => p.MapName == cue.MapName) ?? _snapshot?.MapPlan;
        var here = plan is { } p && p.MapName == cue.MapName && p.Finish.Count + p.Progress.Count > 0 ? Summary(p.Finish.Count, p.Progress.Count) : "";
        return cue.Kind switch
        {
            CueKind.RaidLoading => ("RAID LOADING", map, here),
            CueKind.Transit => ("TRANSIT", map, here),
            CueKind.ScavRaid => (map, "SCAV RAID", "Quest objectives don't count here; items you find in raid do."),
            CueKind.LoadCancelled => (map, "LOADING CANCELLED", "Back to planning the next raid."),
            CueKind.GroupPick => ("GROUP PICKED", map, here),
            _ => (cue.RaidLength is { } length ? $"{map} · {(int)length.TotalMinutes} MIN" : map, "RAID OVER",
                _snapshot?.Plan.FirstOrDefault() is { } next
                    ? $"Next raid: {next.MapName} · {Summary(next.Finish.Count, next.Progress.Count)}"
                    : "Back to planning the next raid."),
        };
    }

    // The band springs open behind the text with a gold flash, its gold rules shoot out from the middle, and the
    // title slides up while it decodes letter by letter, the undecoded letters in gold; then it all fades. Nothing
    // with text in it is ever scaled, so the text stays sharp (owner, 2026-10-01: it was sometimes blurry, could
    // last longer and use more pop). Off with Windows' animation effects: it just shows and goes.
    private void ShowCue(ViewCue cue)
    {
        var (eyebrow, title, detail) = CueText(cue);
        CueEyebrow.Text = eyebrow;
        CueDetail.Text = detail;
        CueDetail.Visibility = detail.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        CuePanel.Visibility = Visibility.Visible;
        _cueStory?.Stop();
        _cueTimer?.Stop();

        if (!new Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
        {
            CueTitle.Text = title;
            CuePanel.Opacity = 1;
            CueBandScale.ScaleY = 1;
            CueRuleTopScale.ScaleX = CueRuleBottomScale.ScaleX = 1;
            CueTitleShift.Y = 0;
            CueFlash.Opacity = 0;
            if (SnapshotMode)
                return;
            _cueTimer = DispatcherQueue.CreateTimer();
            _cueTimer.Interval = CueLength(cue.Kind);
            _cueTimer.IsRepeating = false;
            _cueTimer.Tick += (_, _) => CuePanel.Visibility = Visibility.Collapsed;
            _cueTimer.Start();
            return;
        }

        var story = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
        Microsoft.UI.Xaml.Media.Animation.EasingFunctionBase easeOut = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut };
        Microsoft.UI.Xaml.Media.Animation.EasingFunctionBase spring = new Microsoft.UI.Xaml.Media.Animation.BackEase { Amplitude = 0.55, EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut };
        void Animate(DependencyObject target, string property, Microsoft.UI.Xaml.Media.Animation.EasingFunctionBase ease, params (double Seconds, double Value)[] keys)
        {
            var frames = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimationUsingKeyFrames();
            foreach (var (seconds, value) in keys)
            {
                frames.KeyFrames.Add(new Microsoft.UI.Xaml.Media.Animation.EasingDoubleKeyFrame
                {
                    KeyTime = TimeSpan.FromSeconds(seconds), Value = value, EasingFunction = ease,
                });
            }
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(frames, target);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(frames, property);
            story.Children.Add(frames);
        }
        var end = CueLength(cue.Kind).TotalSeconds;
        const double k = CuePace;
        // Developer snapshots keep the last cue up, so it can be looked at.
        if (SnapshotMode)
            Animate(CuePanel, "Opacity", easeOut, (0, 0), (0.12 * k, 1));
        else
            Animate(CuePanel, "Opacity", easeOut, (0, 0), (0.12 * k, 1), (end - 0.6, 1), (end, 0));
        Animate(CueBandScale, "ScaleY", spring, (0, 0), (0.42 * k, 1));
        Animate(CueFlash, "Opacity", easeOut, (0, 0), (0.1 * k, 0.32), (0.65 * k, 0));
        Animate(CueRuleTopScale, "ScaleX", spring, (0, 0), (0.15 * k, 0), (0.75 * k, 1));
        Animate(CueRuleBottomScale, "ScaleX", spring, (0, 0), (0.15 * k, 0), (0.75 * k, 1));
        Animate(CueTitleShift, "Y", easeOut, (0, 22), (0.1 * k, 22), (0.55 * k, 0));
        story.Completed += (_, _) =>
        {
            if (ReferenceEquals(story, _cueStory) && !SnapshotMode)
                CuePanel.Visibility = Visibility.Collapsed;
        };
        _cueStory = story;
        story.Begin();

        // The title decodes: undecoded letters flicker through random ones in gold, settling left to right in ink.
        const string glyphs = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var settled = new Microsoft.UI.Xaml.Documents.Run();
        var flicker = new Microsoft.UI.Xaml.Documents.Run { Foreground = Resource("AmberBrush") };
        CueTitle.Inlines.Clear();
        CueTitle.Inlines.Add(settled);
        CueTitle.Inlines.Add(flicker);
        var started = DateTime.Now;
        var decode = TimeSpan.FromMilliseconds(900 * CuePace);
        _cueTimer = DispatcherQueue.CreateTimer();
        _cueTimer.Interval = TimeSpan.FromMilliseconds(35);
        _cueTimer.Tick += (timer, _) =>
        {
            var p = Math.Min(1, (DateTime.Now - started) / decode);
            var resolved = (int)Math.Round(p * title.Length);
            settled.Text = title[..resolved];
            flicker.Text = string.Concat(title[resolved..].Select(c => char.IsLetterOrDigit(c) ? glyphs[Random.Shared.Next(glyphs.Length)] : c));
            if (p >= 1)
                timer.Stop();
        };
        _cueTimer.Start();
    }

    private void ShowNotice(string message, TimeSpan? duration = null, bool offersReport = false)
    {
        if (DemoQuiet)
            return;
        ViewModel.NoticeText = message;
        ViewModel.NoticeOffersReport = offersReport;
        ViewModel.NoticeOpen = true;
        _noticeTimer.Stop();
        _noticeTimer.Interval = duration ?? TimeSpan.FromSeconds(6);
        _noticeTimer.Start();
    }

    /// <summary>Set for "--snapshot" runs: the help panel opens to be rendered, and isn't marked as seen.</summary>
    public bool SnapshotMode { get; set; }

    /// <summary>Snapshots render at this multiple of the screen's pixel density (2 for sharp website images).</summary>
    public int SnapshotScale { get; set; } = 1;

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
        // Let the rail lay out first, so the highlight lands on real rows. Then: the quest's card, the card of the
        // first thing it needs (nested), and the quest pinned in a window.
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            Select(id, "snapshot");
            Linked.Set(Focus.Quest(id));
            _cards.Open(new CardKey.Quest(id), new Windows.Foundation.Rect(372, 150, 8, 8), 0);
            var item = view.Needs.FirstOrDefault()?.ItemId ?? view.Objectives.FirstOrDefault(o => o.ItemId is not null)?.ItemId;
            if (item is not null)
                _cards.Open(new CardKey.Item(item), new Windows.Foundation.Rect(372, 330, 8, 8), 1);
            var origin = QuestWindow.ScreenPoint(this, new Windows.Foundation.Point(((FrameworkElement)Content).ActualWidth - 420, 120));
            Pin(view, origin);
        });
    }

    // The help panel opens by itself once, the first time the app has something to show.
    private void ShowHelpOnFirstRun(SessionSnapshot s)
    {
        if (_helpShownOnce || s.Data is null || DemoMode)
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

    private DateTime _helpOpenedAt;

    private void OnHelpOpened(object sender, object e)
    {
        _helpOpenedAt = DateTime.Now;
        LoadCrashMode();
        Study.Ui("help.open");
    }

    private void OnHelpClosed(object sender, object e)
    {
        Study.Ui("help.close", ("s", DateTime.Now - _helpOpenedAt));
        if (!SnapshotMode)
            _session.SetSetting("help.seen", "1");
    }

    // Maximised on the second monitor, or on the only one at 1600×1000; a given size (developer runs) is kept as is.
    private void PlaceOnSecondMonitor(SizeInt32? size)
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
            AppWindow.Resize(size ?? new SizeInt32(1600, 1000));
            return;
        }
        AppWindow.Move(new PointInt32(target.WorkArea.X + 40, target.WorkArea.Y + 40));
        if (size is { } fixedSize)
            AppWindow.Resize(fixedSize);
        else if (AppWindow.Presenter is OverlappedPresenter presenter)
            presenter.Maximize();
    }

    // ---- keyboard (only while this window has focus; Shturmap registers no global hotkeys) ----

    private void AddShortcuts(UIElement root)
    {
        // Accelerators on the root would otherwise show their key ("F") as a tooltip over the whole window.
        root.KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;
        void Add(Windows.System.VirtualKey key, Action action)
        {
            var accelerator = new KeyboardAccelerator { Key = key };
            accelerator.Invoked += (_, e) =>
            {
                // In the Report dialog keys are text, and Esc closes the dialog.
                if (ReportOpen)
                {
                    if (key == Windows.System.VirtualKey.Escape)
                    {
                        CloseReport("esc");
                        e.Handled = true;
                    }
                    return;
                }
                Study.Ui("key", ("key", key.ToString()));
                action();
                e.Handled = true;
            };
            root.KeyboardAccelerators.Add(accelerator);
        }

        Add(Windows.System.VirtualKey.F, ShowMe);
        Add(Windows.System.VirtualKey.Add, () => ZoomBy(1.5, "key"));
        Add((Windows.System.VirtualKey)187, () => ZoomBy(1.5, "key")); // the +/= key
        Add(Windows.System.VirtualKey.Subtract, () => ZoomBy(1 / 1.5, "key"));
        Add((Windows.System.VirtualKey)189, () => ZoomBy(1 / 1.5, "key")); // the -/_ key
        Add(Windows.System.VirtualKey.Number0, () => OnFitClick(this, new RoutedEventArgs()));
        Add(Windows.System.VirtualKey.NumberPad0, () => OnFitClick(this, new RoutedEventArgs()));
        Add(Windows.System.VirtualKey.Escape, () =>
        {
            if (_cards.Cards.Count > 0)
                _cards.CloseAll();
            else
                ClearSelection();
        });
        Add(Windows.System.VirtualKey.PageUp, () => PickFloor(_shownFloor - 1, "key"));
        Add(Windows.System.VirtualKey.PageDown, () => PickFloor(_shownFloor + 1, "key"));
        Add(Windows.System.VirtualKey.F1, ShowHelp);
#if DEVTOOLS
        AddDevShortcuts(Add);
#endif
        root.CharacterReceived += (_, e) =>
        {
            if (e.Character == '?' && !ReportOpen)
            {
                ShowHelp();
                e.Handled = true;
            }
        };
    }

    private void ClearSelection()
    {
        if (_selectedQuest is null)
            return;
        Study.Ui("select", ("quest", _selectedQuest), ("on", false), ("how", "esc"));
        _selectedQuest = null;
        ShowSelection();
    }

    // ---- UI events ----

    private async void OnMapPicked(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingPicker || ViewModel.SelectedMap is not { } choice || choice.NormalizedName == _snapshot?.Map?.NormalizedName)
            return;
        Study.Ui("map.pick", ("to", choice.NormalizedName), ("how", "picker"));
        await _session.SelectMapAsync(choice.NormalizedName);
    }

    private async void OnPlanClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string map } && map != _snapshot?.Map?.NormalizedName)
        {
            // A click on a previewed map keeps it: the map is shown fitted, as after any pick.
            _previewTimer?.Stop();
            EndPreview(restore: false);
            Study.Ui("map.pick", ("to", map), ("how", "plan"), ("planRank", ViewModel.Plans.ToList().FindIndex(p => p.NormalizedName == map) + 1));
            await _session.SelectMapAsync(map);
        }
    }

    private async void OnSideClick(object sender, RoutedEventArgs e)
    {
        var to = _snapshot?.Raid.Side == RaidSide.Scav ? RaidSide.Pmc : RaidSide.Scav;
        Study.Ui("side.switch", ("to", to));
        await _session.SetSideAsync(to);
    }

    private void OnNoticeClosed(object sender, RoutedEventArgs e)
    {
        Study.Ui("notice.close", ("text", ViewModel.NoticeText));
        ViewModel.NoticeOpen = false;
    }

    private void OnShowMeClick(object sender, RoutedEventArgs e)
    {
        Study.Ui("map.showme", ("how", "button"));
        Map.CenterOnPlayer();
    }

    private void ShowMe()
    {
        Study.Ui("map.showme", ("how", "key"));
        Map.CenterOnPlayer();
    }

    private void OnFitClick(object sender, RoutedEventArgs e)
    {
        Study.Ui("map.fit");
        Map.FitMap();
    }

    private void OnZoomInClick(object sender, RoutedEventArgs e) => ZoomBy(1.5, "button");

    private void OnZoomOutClick(object sender, RoutedEventArgs e) => ZoomBy(1 / 1.5, "button");

    private void ZoomBy(double factor, string how)
    {
        Map.ZoomBy(factor);
        Study.Ui("map.zoom", ("factor", factor), ("how", how));
    }

    private void OnRailScrolled(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (!e.IsIntermediate && sender is ScrollViewer scroll)
            Study.Ui("rail.scroll", ("y", scroll.VerticalOffset), ("of", scroll.ScrollableHeight));
    }

    // ---- developer aid ----

    /// <summary>Renders the window's controls, the help panel and the map to PNG files in a folder ("--snapshot"), with
    /// the text Copy diagnostics would copy.</summary>
    public async Task SaveSnapshotAsync(string folder)
    {
        Directory.CreateDirectory(folder);
        try
        {
            await RenderToPngAsync((UIElement)Content, Path.Combine(folder, "window.png"));
            if (HelpFlyout.IsOpen && HelpFlyout.Content is UIElement help)
                await RenderToPngAsync(help, Path.Combine(folder, "help.png"));
            var cards = _cards.Cards;
            for (var i = 0; i < cards.Count; i++)
                await RenderToPngAsync(cards[i], Path.Combine(folder, i == 0 ? "card.png" : $"card-{i + 1}.png"));
            if (_pinned.Values.FirstOrDefault() is { } pinned)
                await RenderToPngAsync(pinned.Card, Path.Combine(folder, "pinned.png"));
            Map.SaveSnapshot(Path.Combine(folder, "map.png"), SnapshotScale);
            // What Copy diagnostics would put on the clipboard, to check it without clicking.
            await File.WriteAllTextAsync(Path.Combine(folder, "diagnostics.txt"), DiagnosticsText());
            AppLog.Debug("Snapshot saved to " + folder);
        }
        catch (Exception e)
        {
            AppLog.Error("Snapshot failed", e);
        }
    }

    private async Task RenderToPngAsync(UIElement element, string path)
    {
        var rtb = new Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap();
        var scale = Content.XamlRoot?.RasterizationScale ?? 1;
        if (SnapshotScale == 1)
            await rtb.RenderAsync(element);
        else
        {
            // A scaled render alone only stretches the bitmap; raising the element's rasterization scale first makes
            // text and vectors draw at the higher density.
            var previous = element.RasterizationScale;
            element.RasterizationScale = scale * SnapshotScale;
            await Task.Delay(250);
            await rtb.RenderAsync(element, (int)Math.Round(element.ActualSize.X * scale * SnapshotScale),
                (int)Math.Round(element.ActualSize.Y * scale * SnapshotScale));
            element.RasterizationScale = previous;
        }
        var pixels = await rtb.GetPixelsAsync();
        var storageFolder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(path));
        var file = await storageFolder.CreateFileAsync(Path.GetFileName(path), Windows.Storage.CreationCollisionOption.ReplaceExisting);
        using var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);
        var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);
        var dpi = 96 * scale * SnapshotScale;
        encoder.SetPixelData(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
            (uint)rtb.PixelWidth, (uint)rtb.PixelHeight, dpi, dpi, System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.ToArray(pixels));
        await encoder.FlushAsync();
    }
}
