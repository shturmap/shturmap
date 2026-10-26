using System.Collections;
using System.Globalization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Shturmap.Core;
using Shturmap.App.Controls;
using Shturmap.App.Rules;
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
    private readonly SceneGate _scenes = new();
    private RaidPhase _phase = RaidPhase.Menu;
    private readonly HashSet<string> _sheetNoticeShown = new(StringComparer.Ordinal);
    private bool _updatingPicker;
    private bool _helpShownOnce;
    private readonly CardStack _cards;
    private MapMarker? _hoveredMarker;
    private IReadOnlyList<MapLayer?> _floors = [];
    private string? _floorsFor;
    private int? _floorPick;
    private DateTime? _floorPickFix;
    private int _shownFloor = -1;
    // The shown map's floors as the picker lists them, also while it steps aside (ShowFloors).
    private IReadOnlyList<FloorChoice> _floorChoices = [];

    /// <param name="size">A size to open at (developer runs: "--window"); kept as is, and nothing is remembered.</param>
    /// <param name="savedPlace">Where the window stood when it was last closed (<see cref="WindowPlace"/>), or null.</param>
    /// <param name="rememberPlace">Whether this run restores and saves the window's place (not snapshots or the demo).</param>
    /// <param name="quiet">A run that only takes pictures: off every monitor, unactivated, out of the switchers (<see cref="Quiet"/>).</param>
    public MainWindow(GameSession session, SizeInt32? size = null, string? savedPlace = null, bool rememberPlace = false, bool quiet = false)
    {
        _session = session;
        Quiet = quiet;
        InitializeComponent();
        Title = App.Title;
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
        PlaceWindow(size, savedPlace, rememberPlace);
#if DEVTOOLS
        AddStudySwitch();
#endif
        WatchLanguage();
        WatchTextSize();

        Picture.Art = () => _session.Art;
        Study.Log = session.Study;
        var root = (FrameworkElement)Content;
        StudyAttention(root);
        _cards = new CardStack(root, CreateCard, CardRoom);
        InstallDesk();
        InstallGlance();
        // Rows open their cards in their window's stack.
        Linked.Hovered += (element, key) => CardStack.For(element.XamlRoot)?.Enter(element, key);
        // A row unloaded under the pointer can't say which window it was in: every stack hears it, its own takes it.
        Linked.Left += CardStack.Leave;
        // A click on a quest keeps its card open; the pen beside it (rows, cards) picks it for the coming raid.
        Linked.Clicked += (element, key) => CardStack.For(element.XamlRoot)?.Click(element, key);
        Linked.KeepRequested += quest => _ = _session.TogglePickAsync(quest, "pen");
        // The tick at the end of an objective's row on a quest card: done, the player says (the logs never do).
        TickBox.Requested += objective => _ = _session.ToggleTickAsync(objective, "card");
        Linked.FocusChanged += OnFocusChanged;
        // The linked highlight's breath: one clock for the map and the rows, so the two breathe in phase.
        Linked.Breath = Map.Breath;
        // A click on nothing in particular (bare rail, bare map) lets go of held cards. Rows, markers and buttons
        // handle their own clicks.
        root.Tapped += (_, e) =>
        {
            if (!e.Handled && !Linked.IsInside(e.OriginalSource) && _cards.AnyHeld)
                _cards.CloseAll();
        };
        root.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler((_, e) =>
        {
            _cards.PointerAt(e.GetCurrentPoint(root).Position);
            // The breath stops once the pointer has rested 6 s on a linked set; a move starts it again.
            if (Map.Breath.Moved(DateTime.Now))
            {
                Map.Redraw();
                Linked.BreathChanged();
            }
        }), handledEventsToo: true);
        _focusClear = WindowTimer(ApplyMapFocus);
        _focusClear.Interval = TimeSpan.FromMilliseconds(250);
        _focusClear.IsRepeating = false;
        Map.MarkerHovered += OnMarkerHovered;
        Map.MarkerClicked += OnMarkerClicked;
        ObserveActivation(this);
        Closed += (_, _) =>
        {
            StopForExit();
            SavePlace();
        };

        _clock = WindowTimer(OnClockTick);
        _clock.Interval = TimeSpan.FromSeconds(1);
        _clock.Start();
        _noticeTimer = WindowTimer(() =>
        {
            if (ViewModel.NoticeOpen)
                Study.Ui("notice.expire", ("text", ViewModel.NoticeText));
            ViewModel.NoticeOpen = false;
        });
        _noticeTimer.Interval = TimeSpan.FromSeconds(6);
        _noticeTimer.IsRepeating = false;

        // A new position moves the view only while Follow my position is on; otherwise, out of view, the edge arrow
        // points to it and a notice says so.
        Map.PlayerPinged += offScreen =>
        {
            Study.Ui("fix.ping", ("inView", !offScreen));
            if (offScreen)
                ShowNotice(AppTexts.NoticePositionOffScreen, TimeSpan.FromSeconds(5));
            FirstFixNotice(offScreen);
        };
        Map.EdgeClicked += () => Study.Ui("map.showme", ("how", "edge"));
        AddShortcuts((UIElement)Content);
        // The floor picker steps aside while a cue or What's New's plate shows, and is back when it goes (ShowFloors).
        CuePanel.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, (_, _) => ShowFloors());
        // The cue's line under its title stands in a box with its halo (MainWindow.RollCall): the box goes with the line,
        // so the band keeps its spacing without one.
        CueDetail.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, (_, _) => CueDetailBox.Visibility = CueDetail.Visibility);
        WhatsNewPlate.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, (_, _) => ShowFloors());
        // And while the in-raid glance's plate stands (the panel's C: "the floor picker steps aside as for any cue"; at
        // 900×560 the plate's right end lay over it).
        RaidGlancePlate.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, (_, _) => ShowFloors());
        // What's New's plate keeps clear of the notes at the map's top and of its foot (PlaceWhatsNewPlate), from the moment
        // it shows: a plate of the size it last had changes no size as it comes back.
        WhatsNewPlate.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, (_, _) =>
        {
            if (WhatsNewPlate.Visibility != Visibility.Visible)
                return;
            WhatsNewPlate.UpdateLayout();
            PlaceWhatsNewPlate();
        });
        MapNotes.SizeChanged += (_, _) => PlaceWhatsNewPlate();
        MapFoot.SizeChanged += (_, _) => PlaceWhatsNewPlate();
        // The cue of the tasks read keeps to its room as a notice comes or goes and as the window's size changes
        // (MainWindow.TasksCue, FitTasksCue).
        MapNotes.SizeChanged += (_, _) => RefitTasksCue();
        MapFoot.SizeChanged += (_, _) => RefitTasksCue();
        Map.SizeChanged += (_, _) => RefitTasksCue();
        WhatsNewPlateBox.SizeChanged += (_, _) => PlaceWhatsNewPlate();
        WhatsNewPlate.SizeChanged += (_, _) => PlaceWhatsNewPlate();
        WhatsNewPlateStack.SizeChanged += (_, _) => PlaceWhatsNewPlate();
        // The status bar gives way in a narrow window (FitStatusBar): looked at when its size or its words' change.
        StatusBar.SizeChanged += (_, _) => FitStatusBar();
        RaidWord.SizeChanged += (_, _) => FitStatusBar();
        StatusRight.SizeChanged += (_, _) => FitStatusBar();

        // Snapshots arrive on background threads; only the newest one is applied. Once the app is ending
        // (StopForExit), the session, which runs a moment longer, hands the window nothing more (WindowTimer says why).
        session.Changed += s =>
        {
            Volatile.Write(ref _snapshot, s);
            DispatcherQueue.TryEnqueue(() =>
            {
                if (!_stopped)
                    Apply(s);
            });
        };
        session.Notice += notice => DispatcherQueue.TryEnqueue(() =>
        {
            if (!_stopped)
                ShowNotice(notice.Text, notice.Duration, notice.OffersReport);
        });
        session.Cue += cue => DispatcherQueue.TryEnqueue(() =>
        {
            if (_stopped)
                return;
            ShowCue(cue);
            // What's New goes once the first raid since the update is over; an update waiting is said once more
            // (MainWindow.Updates).
            if (cue.Kind == CueKind.RaidOver)
            {
                WhatsNewSeen("raid");
                UpdatesAfterRaid();
            }
        });
    }

    /// <summary>
    /// The app is ending (the window closes, or the app exits by itself: a snapshot run, RESTART NOW, an uninstall, a
    /// developer script's "exit"): the window's timers do nothing more (WindowTimer), the cards neither show nor
    /// settle, and the map draws no more. The map's pulse kept asking for frames while the drawing surface was taken
    /// down, which ended the process with an access violation instead of a clean exit (found 2026-10-04). Safe to call
    /// twice.
    /// </summary>
    public void StopForExit()
    {
        _stopped = true;
        _clock.Stop();
        _noticeTimer.Stop();
        _focusClear.Stop();
        _updateTimer?.Stop();
        _cueTimer?.Stop();
        _glanceHide?.Stop();
        _glanceWait?.Stop();
        _cards.Stop();
        Map.StopDrawing();
        StopTasksCue();
        StashDesk.Stop();
    }

    // The app is ending (StopForExit): nothing of the window's own runs again.
    private bool _stopped;

    /// <summary>
    /// A timer of the window: every one is made here, so that once the app is ending (StopForExit) its tick stops it
    /// and does nothing. Application.Exit takes the window's XAML down while the dispatcher queue still runs; a tick
    /// that then set a text or an opacity threw E_UNEXPECTED inside the queue, where no handler sees it, and the exit
    /// ended with 0xC000027B in CoreMessagingXP instead of a clean one (the tasks cue's QA fix round, 2026-10-19: an
    /// exit within 1.6 s of any cue, its title still decoding, or while the roll call's frames ran; about 3 runs in 4).
    /// SafetyTests hold the window to it.
    /// </summary>
    private DispatcherQueueTimer WindowTimer(Action<DispatcherQueueTimer> tick)
    {
        var timer = DispatcherQueue.CreateTimer();
        timer.Tick += (t, _) =>
        {
            if (_stopped)
            {
                t.Stop();
                return;
            }
            tick(t);
        };
        return timer;
    }

    private DispatcherQueueTimer WindowTimer(Action tick) => WindowTimer(_ => tick());

    // Facing-relative directions are only true briefly after a fix; past this they turn into map directions.
    private static readonly TimeSpan FreshFix = FixAge.Fresh;
    private bool? _fixWasFresh;

    // The raid clock's minute the extract rows' muted lines were worded at (UpdateRaidLists): a train's window passes
    // with the clock, so a new minute words them again.
    private int? _noteMinute;

    private void OnClockTick()
    {
        UpdateClockTexts();
        if (_snapshot is { } now && ViewModel.RaidClock?.In != _noteMinute && now.Extracts.Any(e => e.Notes.Count > 0))
            UpdateRaidLists(now);
        if (_snapshot is not { RaidFix: { } fix } s)
            return;
        var fresh = FixAge.Of(fix.At, DateTime.Now) < FreshFix;
        if (fresh != _fixWasFresh)
        {
            // "ahead-left" turns into "NE": in the rail and on the cards.
            UpdateRaidLists(s);
            _cards.Refresh(UpdateCard);
        }
        Map.Redraw(); // the player's ring and age tag follow the fix's age
    }

    public MainViewModel ViewModel { get; } = new();

    // ---- x:Bind helpers ----

    public Brush RaidBrush(bool inRaid) => Resource(inRaid ? "AmberBrush" : "InkBrush");

    // The OR row under EXIT (Rules.ExitsNote.Plain).
    public string PlainExitNote => Caps.Of(Rules.ExitsNote.PlainNote);

    public string PlainExitTip => Rules.ExitsNote.PlainTip;

    /// <summary>The keys in the help panel.</summary>
    public IReadOnlyList<KeyHelp> Keys =>
    [
        new(AppTexts.HelpKeyShowMe, AppTexts.HelpKeyShowMeAction),
        new(AppTexts.HelpKeyFollow, AppTexts.HelpKeyFollowAction),
        new(AppTexts.HelpKeyZoom, AppTexts.HelpKeyZoomAction),
        new(AppTexts.HelpKeyFit, AppTexts.HelpKeyFitAction),
        new(AppTexts.HelpKeyFloor, AppTexts.HelpKeyFloorAction),
        new(AppTexts.HelpKeyClose, AppTexts.HelpKeyCloseAction),
        new(AppTexts.HelpKeyHelp, AppTexts.HelpKeyHelpAction),
        new(AppTexts.HelpKeySettings, AppTexts.HelpKeySettingsAction),
        new(AppTexts.HelpKeyMouse, AppTexts.HelpKeyMouseAction),
    ];

    /// <summary>
    /// The map symbols in the help panel, drawn by the map's own renderer so they can't drift from the map (at twice
    /// the DIP size, which stays sharp up to 200 % and in snapshots).
    /// </summary>
    private IReadOnlyList<(LegendSymbol Symbol, MapLegendRow Row)> _legendRows = LegendRows();

    // Made again when the language changes (MainWindow.Language): the rows' words are looked up as they are read.
    private static List<(LegendSymbol Symbol, MapLegendRow Row)> LegendRows() => MapLegend.Rows
        .Select(row =>
        {
            using var bitmap = MapLegend.Draw(row.Symbol, 2);
            return (row.Symbol, new MapLegendRow(bitmap.ToWriteableBitmap(), row.Text));
        })
        .ToList();

    private IReadOnlySet<LegendSymbol>? _legendOn;
    private int _legendRest;
    private bool _legendAll;

    /// <summary>
    /// Help lists the symbols on the map shown now, the twelve most important of them; the rest, and the symbols the
    /// map doesn't have, wait behind a link (owner, 2026-10-04, from the review's C9: every symbol of every map made
    /// help 2,350 px tall; 2026-10-09: Streets alone listed 26; Rules.LegendFold). With no map up, all of them count.
    /// Called when help opens, and while it is open when the map changes (help opens by itself at the first start,
    /// before a map is up); the link's state then stays as the player left it.
    /// </summary>
    private void ShowLegend(bool opened)
    {
        var on = Map.Scene is { } scene ? MapLegend.On(scene) : null;
        if (!opened && (on is null ? _legendOn is null : _legendOn?.SetEquals(on) == true))
            return;
        _legendOn = on;
        LegendHeading.Text = on is null ? ViewTexts.HelpLegendHeading : AppTexts.HelpLegendOnThisMap;
        var (listed, more, elsewhere) = LegendFold.Split(_legendRows, r => on?.Contains(r.Symbol) != false);
        LegendHere.ItemsSource = listed.Select(r => r.Row).ToList();
        LegendRestHere.ItemsSource = more.Select(r => r.Row).ToList();
        LegendRestHere.Visibility = more.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        LegendElsewhere.ItemsSource = elsewhere.Select(r => r.Row).ToList();
        LegendElsewhere.Visibility = LegendElsewhereHeading.Visibility = elsewhere.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _legendRest = more.Count + elsewhere.Count;
        LegendMore.Visibility = _legendRest > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (opened)
            _legendAll = false;
        ShowLegendRest();
    }

    private void ShowLegendRest()
    {
        LegendRest.Visibility = _legendAll && _legendRest > 0 ? Visibility.Visible : Visibility.Collapsed;
        LegendMoreText.Text = _legendAll
            ? AppTexts.HelpLegendHide(count: _legendRest)
            : AppTexts.HelpLegendShowMore(count: _legendRest);
    }

    private void OnLegendMoreClick(object sender, RoutedEventArgs e)
    {
        _legendAll = !_legendAll;
        ShowLegendRest();
        Study.Ui("help.legend", ("all", _legendAll));
    }

    public Brush OkBrush(bool ok) => Resource(ok ? "GreenBrush" : "AmberBrush");

    public Visibility ShownIf(string? text) => string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ShownIfTrue(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ShownIfNot(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ShownIfAny(IEnumerable? items) => items?.GetEnumerator().MoveNext() == true ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ShownIfLink(Uri? link) => link is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ShownIfSet(object? value) => value is null ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>The rail's hint stands above the raid card's head in a raid (the head stays at the top of the rail and
    /// the rest of the card scrolls under it), and at the top of what scrolls otherwise.</summary>
    public Visibility HintInRaid(string? hint, bool inRaid) => inRaid && !string.IsNullOrEmpty(hint) ? Visibility.Visible : Visibility.Collapsed;

    // ANY MAP: in Plan and a PMC raid; not in a Scav's (its kills don't count), nor while the stash desk is up.
    public Visibility AnyMapShown(bool scavRaid, bool deskUp) => scavRaid || deskUp ? Visibility.Collapsed : Visibility.Visible;

    public Visibility HintOutsideRaid(string? hint, bool inRaid, bool deskUp) => !inRaid && !deskUp && !string.IsNullOrEmpty(hint) ? Visibility.Visible : Visibility.Collapsed;

    private void OnWikiMapClick(object sender, RoutedEventArgs e) => Study.Ui("wiki.map", ("url", ViewModel.WikiMap?.ToString()));

    /// <summary>Shturmap's licence and the third-party ones a published build carries (eng\notices.ps1).</summary>
    public static string LicencesFolder { get; } = Path.Combine(AppContext.BaseDirectory, "licenses");

    /// <summary>A developer build has no licences folder, so settings shows the link only in a published one.</summary>
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
            ShowNotice(AppTexts.NoticeDiagnosticsCopied);
        }
        catch (Exception ex)
        {
            AppLog.Warn("Copying the diagnostics failed", ex);
            ShowNotice(AppTexts.NoticeDiagnosticsNotCopied);
        }
        Study.Ui("help.diagnostics");
    }

    private string DiagnosticsText() =>
        Diagnostics.Build(Volatile.Read(ref _snapshot) ?? new SessionSnapshot(), GameSession.Version, Diagnostics.WindowsVersion(), App.BuildKind,
            AppLog.Tail(Diagnostics.LogLines), DateTime.Now, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            AppPaths.Default.KindText);

#if DEVTOOLS
    // "Keep a study log" in settings: developer builds only (owner, 2026-10-03: "The study log should only be part of the
    // dev version and not be in the release version"). Built here, not in MainWindow.xaml, so a release carries none of
    // it; it looks like the uninstall question's tick.
    private void AddStudySwitch()
    {
        var check = new FontIcon { Glyph = "", FontSize = 9, Foreground = Resource("GroundBrush") };
        var box = new Border
        {
            Width = 13, Height = 13, BorderThickness = new Thickness(1), BorderBrush = Resource("AmberBrush"),
            VerticalAlignment = VerticalAlignment.Center, Child = check,
        };
        var toggle = new Microsoft.UI.Xaml.Controls.Primitives.ToggleButton
        {
            Style = (Style)Application.Current.Resources["TickToggle"],
            Margin = new Thickness(0, 6, 0, 0),
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 9,
                Children = { box, new TextBlock { Text = AppTexts.SettingsStudyLog, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold } },
            },
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(toggle, AppTexts.SettingsStudyLog);
        void Show()
        {
            var on = ViewModel.StudyLogOn;
            toggle.IsChecked = on;
            box.Background = StudyBoxBrush(on);
            check.Visibility = ShownIfTrue(on);
        }
        Show();
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.StudyLogOn))
                Show();
        };
        toggle.Click += async (_, _) => await _session.SetStudyLogAsync(toggle.IsChecked == true);
        StudySwitch.Children.Add(toggle);
        StudySwitch.Children.Add(new TextBlock
        {
            Style = (Style)Application.Current.Resources["NoteText"], Margin = new Thickness(22, 0, 0, 0),
            Text = AppTexts.SettingsStudyLogNote,
        });
        StudySwitch.Visibility = Visibility.Visible;
    }
#endif

    // The study switch's and the uninstall question's box: filled gold with a check when on, an empty gold square when off.
    public Brush StudyBoxBrush(bool on) => on ? Resource("AmberBrush") : new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    // "Delete screenshots after reading" in settings (owner, 2026-10-04): the one thing Shturmap changes outside its own
    // folders, so it is off unless ticked and its note says what goes and what stays.
    public string DeleteScreenshotsNote =>
        AppTexts.SettingsDeleteScreenshotsNote(seconds: (int)Math.Round(Shturmap.Game.Screenshots.ScreenshotCleaner.Grace.TotalSeconds));

    // "Read the extract list from screenshots" in settings (owner, 2026-10-05): the one case where Shturmap opens a
    // screenshot's picture, so its note says what is looked at, and unticking it stops it.
    public string ReadExitsNote =>
        AppTexts.SettingsReadExitsNote;

    private async void OnReadExitsClick(object sender, RoutedEventArgs e)
    {
        var on = !ViewModel.ReadExits;
        await _session.SetReadExitsAsync(on);
        Study.Ui("settings.readExits", ("on", on));
    }

    // "Read your stash and tasks from menu screenshots" in settings (owner, 2026-10-11; the Tasks screen's operational
    // tasks since 2026-10-19): the second case where Shturmap opens a screenshot's picture; its note says what is looked
    // at and what is kept, and unticking it stops it.
    public string ReadStashNote =>
        AppTexts.SettingsReadStashNote;

    private async void OnReadStashClick(object sender, RoutedEventArgs e)
    {
        var on = !ViewModel.ReadStash;
        await _session.SetReadStashAsync(on);
        Study.Ui("settings.readStash", ("on", on));
    }

    // FORGET ALL MARKS in settings (owner, 2026-10-11): every mark goes at once, as NOT ANY MORE takes one; no question
    // asked, as there (a stash pass brings them back).
    private async void OnForgetMarksClick(object sender, RoutedEventArgs e)
    {
        Study.Ui("settings.forgetMarks");
        await _session.ForgetAllMarksAsync();
    }

    /// <summary>The FORGET ALL MARKS link's colour: amber while there are marks to forget, muted when there are none.</summary>
    public Brush MarksLinkBrush(bool hasMarks) => Resource(hasMarks ? "AmberBrush" : "MutedBrush");

    private async void OnDeleteScreenshotsClick(object sender, RoutedEventArgs e)
    {
        var on = !ViewModel.DeleteScreenshots;
        await _session.SetDeleteScreenshotsAsync(on);
        Study.Ui("settings.deleteScreenshots", ("on", on));
    }

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
        ApplyGameState(s);
        vm.InRaid = s.Raid.Phase != RaidPhase.Menu;
        vm.RaidHoldsBack = WhileInRaid.Waits(s.Raid.Phase);
        vm.LogsText = s.Logs.Text;
        vm.LogsOk = s.Logs.Ok;
        vm.ScreenshotsText = s.Screenshots.Text;
        vm.ScreenshotsOk = s.Screenshots.Ok;
        vm.DataText = s.DataHealth.Text;
        vm.DataOk = s.DataHealth.Ok;
        vm.DataDetail = s switch
        {
            { Data: { Offline: true } data } => AppTexts.StatusDataOffline(time: UiLanguage.DayMonthTime(data.CheckedAt.ToLocalTime().DateTime)),
            { Data: { } data } => AppTexts.StatusDataChecked(time: data.CheckedAt.ToLocalTime().ToString("HH:mm", UiLanguage.Culture)),
            { DataProblem: { } problem } => GameSession.DataNotice(problem),
            _ => AppTexts.StatusDataLoading,
        };
        vm.StudyLogOn = s.StudyLogOn;
        ShowLanguage(s);
        vm.DeleteScreenshots = s.DeleteScreenshots;
        vm.ReadExits = s.ReadExits;
        vm.ReadStash = s.ReadStash;
        vm.HasMarks = s.Marks.Count > 0;
        vm.HelpKeys = s.ScreenshotKeys.Count > 0 ? string.Join(AppTexts.KeysOr, s.ScreenshotKeys.Select(k => k.Name)) : AppTexts.ScreenshotKeyUnknown;
        // A raid loading ends any preview at once: the raid's map is what matters now.
        if (vm.InRaid && _previewing is not null)
        {
            _previewTimer?.Stop();
            EndPreview(restore: false);
        }
        // And a replay: the next raid is what matters now (its positions are gone with it, in the session too).
        if (vm.InRaid)
            StopReplay("raid");
        // And at each step into a raid (it loads, it starts) the cards open then let go of the map (WhileInRaid). A
        // snapshot's and the demo's shown card is their picture: it stays.
        if (WhileInRaid.LetsGo(_phase, s.Raid.Phase) && !SnapshotMode && !DemoMode)
        {
            if (_cards.Cards.Count > 0)
            {
                Study.Ui("cards.raid", ("open", _cards.Cards.Count), ("held", _cards.AnyHeld), ("phase", s.Raid.Phase));
                _cards.CloseAll();
            }
            // The help panel too: it stays open while the game has the focus, over the raid card. If it had opened
            // by itself, it comes back when the raid is over (OnHelpClosed).
            if (HelpFlyout.IsOpen)
            {
                _helpClosedForRaid = true;
                HelpFlyout.Hide();
            }
            // And the tour: it covers the window. It comes back at its chapter when the raid is over (MainWindow.Tour).
            CloseTour("raid");
        }
        _phase = s.Raid.Phase;
        NoteFreshMarks(s);
        NoteFreshTasks(s);
        ShowPicks(s);
        UpdateClockTexts();
        HoldTraderOnStart(s);
        ReadExampleTasksOnStart(s);
        UpdateDesk(s);
        ShowDeskExampleOnStart(s);
        UpdateGlance(s);
        ShowGlanceExampleOnStart(s);
        UpdateTraders(s);
        UpdatePicker(s);
        UpdatePlan(s);
        UpdateRaidLists(s);
        LoadFollowOnce();
        LoadGridOnce();
        UpdateMap(s);
        _cards.Refresh(UpdateCard);
        ShowTourWhenDue(s);
        ShowHelpOnFirstRun(s);
        ShowWhatsNewOnce(s);
        ShowQuestForSnapshot(s);
        DemoOnSnapshot(s);
    }

    private void UpdateClockTexts()
    {
        if (_snapshot is not { } s)
            return;
        // Only what the log shows: outside a raid "Not in a raid", never "in the menus" (RaidStatus).
        ViewModel.RaidText = RaidStatus.Text(s, DateTime.Now);
        ViewModel.RaidDetail = RaidStatus.Tooltip(s.Raid, DateTime.Now);

        // In a raid: how long it has run and how long it still runs, on a line of its own (owner, 2026-10-04: "we
        // do not care when the start time was and all this info ... how much time we're in the raid and how much
        // time is left. This is crucial information"). Until then the line said "40 min raid · started 21:02" and
        // the game's time of day, and no time left (the same day's review: a figure the game never states). It is
        // back on the owner's word, with its tooltip saying how it is made; a Scav gets none (Rules.RaidTime).
        var inRaid = s.Raid.Phase == RaidPhase.InRaid;
        var time = Rules.RaidTime.Of(inRaid, s.Raid.Side == RaidSide.Scav, s.Raid.RaidStartedAt, s.RaidInfo?.RaidMinutes ?? 0, DateTime.Now);
        ViewModel.RaidClock = time;
        var parts = new List<string>();
        // Before the raid runs (it loads) the map's raid length stands in the facts; once it runs, the line above says more.
        if (s.RaidInfo is { RaidMinutes: > 0 } info && time is null && !(inRaid && s.Raid.Side == RaidSide.Scav))
            parts.Add(AppTexts.RaidLength(minutes: info.RaidMinutes));
        // The bosses are parts of their own: each is linked to its spawn zones on the map.
        var raidParts = LinePart.Line(parts.Select(p => new LinePart(p)).Concat(BossParts(s, s.RaidMap?.NormalizedName, s.RaidInfo?.Bosses ?? [])));
        ViewModel.RaidLine = string.Join(" · ", raidParts.Select(p => p.Text));
        if (!LinePart.Same(ViewModel.RaidLineParts, raidParts))
            ViewModel.RaidLineParts = raidParts;

        // The position's age, in the status bar and in the raid card's line on where its distances come from: both
        // with the clock, so they never say two ages (FixAge).
        var age = s.RaidFix is { } at ? FixAge.Of(at.At, DateTime.Now) : (TimeSpan?)null;
        // While the raid loads there is nothing to press for yet: "No position yet" comes with the raid itself, as in
        // the status bar.
        ViewModel.RaidFixNote = age is null && s.Raid.Phase != RaidPhase.InRaid ? "" : FixAge.Note(age, ViewModel.HelpKeys);
        ViewModel.FixText = s.RaidFix is { } fix && age is { } old
            ? AppTexts.StatusFix(age: FixAge.Text(old), floor: s.RaidFloor?.Name ?? AppTexts.StatusFloorGround, height: fix.Position.Y.ToString("0", UiLanguage.Culture))
            : StatusBarFit.NoPosition(s.Raid.Phase, ViewModel.HelpKeys);
        FitStatusBar();
    }

    // ---- the status bar in a narrow window (StatusBarFit; review of 2026-10-04, C4) ----

    // As in MainWindow.xaml: a light is a 6 px square and, 6 px on, its word; 16 px lie between the lights, between
    // the words on the left, and between the bar's two halves; the bar ends 10 px before the window's edge.
    private const double LightSquare = 6;
    private const double LightGap = 6;
    private const double StatusSpacing = 16;
    private const double StatusEnd = 10;
    private TextBlock? _statusMeasure;

    // The width a status word takes, whether it is shown now or not.
    private double StatusWidth(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
        _statusMeasure ??= new TextBlock { Style = (Style)StatusBar.Resources["StatusBarText"] };
        _statusMeasure.Text = Caps.Of(text);
        Controls.TextScale.Probe(_statusMeasure);
        _statusMeasure.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        return _statusMeasure.DesiredSize.Width;
    }

    /// <summary>
    /// Whether the bar has room for all it says. If not, the lights' words go first: the lights stay, with their
    /// tooltips, and the raid state and the last fix keep their room. Called when the bar's size or its words change.
    /// </summary>
    private void FitStatusBar()
    {
        if (StatusBar.ActualWidth <= 0)
            return;
        // Side by side: everything up to the raid state as it is laid out, the whole raid state and last fix, the lights
        // with their words, and the three buttons (the right half less its lights, whatever they show at the moment).
        var raidAt = RaidWord.TransformToVisual(StatusBar).TransformPoint(default).X;
        var left = raidAt + StatusWidth(ViewModel.RaidText);
        var fix = StatusWidth(ViewModel.FixText);
        var lights = new[] { ViewModel.LogsText, ViewModel.ScreenshotsText, ViewModel.DataText }
            .Sum(word => LightSquare + (StatusWidth(word) is > 0 and var width ? LightGap + width : 0)) + 2 * StatusSpacing;
        var buttons = Math.Max(0, StatusRight.ActualWidth - StatusLights.ActualWidth);
        var needed = left + (fix > 0 ? StatusSpacing + fix : 0) + StatusSpacing + lights + buttons + StatusEnd;
        var words = StatusBarFit.Words(ViewModel.LightWords, StatusBar.ActualWidth, needed);
        if (words != ViewModel.LightWords)
        {
            ViewModel.LightWords = words;
            Study.Ui("statusbar.words", ("shown", words), ("width", StatusBar.ActualWidth));
        }
        // The last resort, where even the raid state hasn't room (a large text size in the smallest window; 2026-10-19:
        // at 200 % it was cut off without "…"): it trims, the last fix gone before it.
        // Its room: up to the left half's slot, which ends before the right half and the gap between them.
        var halfEnd = StatusBar.ActualWidth - StatusBar.Padding.Right - StatusRight.ActualWidth - StatusBar.ColumnSpacing;
        var room = Math.Max(0, halfEnd - raidAt);
        if (Math.Abs(RaidWord.MaxWidth - room) >= 0.5)
            RaidWord.MaxWidth = room;
    }

    /// <summary>A light's tooltip: what is behind it, and its word first when the bar is too narrow to show it.</summary>
    public string? LightTip(string word, string detail, bool wordShown) =>
        wordShown ? (detail.Length > 0 ? detail : null) : detail.Length > 0 ? $"{word}\n{detail}" : word;

    /// <summary>The tooltip of a light with nothing more to say than its word: only while the word is hidden.</summary>
    public string? LightWord(string word, bool wordShown) => wordShown ? null : word;

    /// <summary>Something that waits for a click (the question after a crash), shown outside raids only.</summary>
    public Visibility ShownUnlessHeldBack(string? text, bool heldBack) =>
        string.IsNullOrEmpty(text) || heldBack ? Visibility.Collapsed : Visibility.Visible;

    // The MAP list (owner, 2026-10-08, "A"; Rules.MapList): the maps with your quests first, in Plan's order with their
    // counts, then OTHER MAPS; a map's variants are one entry, as Plan counts them. Made anew only when what it says
    // changes, and never while it is open: new items would close it under the pointer.
    private void UpdatePicker(SessionSnapshot s)
    {
        if (s.Data is not { } data)
            return;
        _updatingPicker = true;
        try
        {
            string NameOf(string id) => data.Maps.TryGetValue(id, out var map) ? map.NormalizedName : id;
            // With a trader's focus held, each map's count is that trader's quests there ("Ragman 2"; PlanList.Count).
            var trader = HeldTraderName(s);
            var list = MapList.Build(
                Planning.Maps(data).Select(m => new MapList.Map(NameOf(m.Id), m.Name, m.MapIds.Select(NameOf).ToList())),
                s.Plan.Select(p => (p.NormalizedName, PlanList.Count(ShortSummary(p.Finish.Count, p.Progress.Count), trader, HeldCountOn(p)))));
            if (!MapList.Same(ViewModel.MapChoices, list) && !MapPicker.IsDropDownOpen)
                ViewModel.MapChoices = list;
            ViewModel.SelectedMap = MapList.For(ViewModel.MapChoices, s.Map?.NormalizedName);
        }
        finally
        {
            _updatingPicker = false;
        }
    }

    private void UpdatePlan(SessionSnapshot s)
    {
        var vm = ViewModel;
        // A raid the log never ended has no length to say: RaidStatus words it ("end not in the log").
        vm.LastRaidText = s.LastRaid is { } last ? RaidStatus.LastRaid(last) : "";
        // REPLAY on that line while the raid just over can be replayed (until the next one loads).
        vm.ReplayOffered = s.Replay is { Plays: true } && s.Raid.Phase == RaidPhase.Menu;

        // The open card is the map on screen's own (owner, 2026-10-08: the rail follows the map on screen; PlanList.Open):
        // its row's, or one more after the rows, with a row of its own when your quests have work there and none when
        // they haven't. Maps go by the name Plan counts them under, so a variant on screen (Night Factory) opens its
        // map's card. Until then a map that wasn't suggested left the best suggestion's card open.
        var shownKey = s.Map is null ? null : Planning.PickKey(s.Data, s.Map.NormalizedName);
        var shownPlan = s.MapPlan is { } mapPlan && mapPlan.NormalizedName == shownKey ? mapPlan : null;
        // A trader's focus held: the rows go by that trader's quests on each map, most first (PlanList.Ranked).
        // The maps with operational work and none of your quests join the list's end, by name (owner, 2026-10-19, B: the
        // planner's order and score unchanged).
        var ranked = PlanList.Ranked(s.Plan, HeldCountOn, held: _heldTrader is not null).Concat(s.OperationalPlans).ToList();
        var (openIndex, added) = PlanList.Open(ranked.Select(p => p.NormalizedName).ToList(), shownKey, shownPlan is not null);
        IReadOnlyList<MapPlanView> plans = added ? [.. ranked, shownPlan!] : ranked;
        bool CardOnly(MapPlanView p, int i) => added && i == plans.Count - 1 && p.Finish.Count + p.Progress.Count == 0;
        bool NoQuests(MapPlanView p) => p.Finish.Count + p.Progress.Count == 0;
        // A trader's focus held (the strip under the MAP list; owner, 2026-10-17): the other traders' quests step back in
        // the rows, a pick never; each map counts the trader's quests there (TraderFocus, PlanList.Count).
        var trader = HeldTraderName(s);
        var inRaid = s.Raid.Phase != RaidPhase.Menu;
        QuestLine Line(PlanQuestView q, IReadOnlySet<string> picks) => new(q.QuestId, q.Kind, q.Name, q.TraderId, s.Data?.TraderName(q.TraderId) ?? "")
        {
            SteppedBack = TraderFocus.StepsBack(_heldTrader, inRaid, q.TraderId, picks.Contains(q.QuestId)),
        };
        QuestLine OnMap(PlanQuestView q, MapPlanView p) => Line(q, s.PicksOn(p.NormalizedName)) with
            { Needs = Chips(s, p, q.QuestId), Synopsis = q.Synopsis, StartsGroup = q.StartsGroup, Note = q.Note };
        // The picks first, in a group of their own; COMPLETE and PROGRESS without them (Planning.Sections). The cards on
        // screen stay while they say the same (RowLists): a rebuilt row loses the pointer resting on it.
        vm.Plans = RowLists.Keep(vm.Plans, plans.Select((p, i) =>
        {
            // Each map has its own picks, in its own colours (owner, 2026-10-04).
            var sections = Planning.Sections(p, s.PicksOn(p.NormalizedName));
            var slots = s.PickSlotsOn(p.NormalizedName);
            return new PlanCard(
                p.NormalizedName,
                p.MapName,
                CardOnly(p, i) || NoQuests(p) ? AppTexts.PlanNoneOfYourQuests(map: p.MapName) : Summary(p.Finish.Count, p.Progress.Count),
                Planning.FactsLine(p),
                i == openIndex,
                sections.Finish.Select(q => OnMap(q, p)).ToList(),
                sections.Progress.Select(q => OnMap(q, p)).ToList(),
                BringLines(s, p.Requirements, s.PicksOn(p.NormalizedName), stepBack: !inRaid),
                (i + 1).ToString(UiLanguage.Culture),
                p.Requirements.Select(r => Chip(s, r)).ToList(),
                sections.Picks.Select(q => OnMap(q, p) with { PickSlot = slots.GetValueOrDefault(q.QuestId, -1) }).ToList())
            {
                ShortSummary = NoQuests(p) ? "" : PlanList.Count(ShortSummary(p.Finish.Count, p.Progress.Count), trader, HeldCountOn(p)),
                RowSteppedBack = !inRaid && PlanList.StepsBack(trader, HeldCountOn(p)),
                DetailParts = LinePart.Line(new[] { new LinePart(Planning.LengthText(p)) }.Concat(BossParts(s, p.NormalizedName, p.Bosses))),
                CardOnly = CardOnly(p, i),
                NoQuests = NoQuests(p),
                StashNote = s.StashNoteShown,
                // The daily and weekly operational tasks (owner, 2026-10-19, B): the row's framed glyphs, the card's section.
                OperationalGlyphs = TaskGlyphs(s, p.NormalizedName),
                Operational = i == openIndex ? TaskLines(s, p.NormalizedName, inRaid: false) : [],
                OperationalAsOf = s.OperationalReadAt is { } readAt ? OperationalRail.AsOf(readAt, DateTimeOffset.Now) : "",
                OperationalEnded = i == openIndex ? OperationalRail.Ended(s.OperationalEnded) : "",
            };
        }).ToList(), PlanCard.Same);
        // The rows are what shows a map (PlanList): one suggested map gets its row only while another map is on screen.
        vm.PlanListShown = PlanList.Shown(vm.Plans.Where(p => !p.CardOnly).Select(p => p.NormalizedName).ToList(), shownKey);
        vm.AnyMap = RowLists.Keep(vm.AnyMap, s.AnyMap.Select(q => Line(q, s.Picks)).ToList(), QuestLine.Same);
    }

    /// <summary>BRING: what the picks need first, then a hairline and the rest (Planning.BringOrder). In Plan, with a
    /// trader's focus held, a row that serves none of that trader's quests nor a pick steps back (TraderFocus).</summary>
    private IReadOnlyList<RequirementLine> BringLines(SessionSnapshot s, IReadOnlyList<RequirementView> rows, IReadOnlySet<string> picks, bool stepBack = false) =>
        Planning.BringOrder(rows, picks).Select(b => BringLine(s, b.Row) with
        {
            StartsOthers = b.StartsOthers,
            SteppedBack = stepBack && TraderFocus.StepsBackServing(_heldTrader, false, b.Row.QuestIds.Select(q => (TraderOf(s, q), picks.Contains(q)))),
        }).ToList();

    /// <summary>The easiest way to get the row's item ("Prapor LL1 · 18,936 ₽"), or, for a row any of several items
    /// will do (a weapon class), one of them ("e.g. Mosin rifle (Sniper) · …"); empty if unknown.</summary>
    private static string BestSource(SessionSnapshot s, RequirementView r) =>
        s.Data is { } data ? ItemCards.BestOf(data, s.Sources, r.Kind == RequirementKind.Weapon && r.Alternatives is { Count: > 0 } all ? all : [r.ItemId], s.Quests) : "";

    // A weapon row names its class once the item categories are loaded ("Any sniper rifle"); the rest as given.
    private static string RowText(SessionSnapshot s, RequirementView r) =>
        r.Kind == RequirementKind.Weapon && r.Alternatives is { Count: > 1 } all && s.Data is { } data ? Planning.WeaponText(data, s.Sources, all) : r.Text;

    private static string GlyphOf(RequirementKind kind) => kind == RequirementKind.Key ? Glyphs.Key : Glyphs.Bring;

    /// <summary>What Shturmap learned of having the row's item, or null (Session.StashMarks).</summary>
    private static StashRowView? MarkOf(SessionSnapshot s, RequirementView r) =>
        s.Data is { } data && s.Marks.Count > 0 ? StashMarks.ForRow(data, r, s.Marks) : null;

    /// <summary>A BRING row: the item, what it is for and for which quests, what is known of having it (glowing in when
    /// it just arrived), and where to get it (or the rest of it, under a part mark; nothing under a mark that covers
    /// the row).</summary>
    private RequirementLine BringLine(SessionSnapshot s, RequirementView r)
    {
        var mark = MarkOf(s, r);
        var source = mark is null ? BestSource(s, r)
            : mark.More > 0 && BestSource(s, r) is { Length: > 0 } rest ? SessionTexts.MarkMore(count: mark.More.ToString("N0", UiLanguage.Culture), source: rest)
            : "";
        return new RequirementLine(GlyphOf(r.Kind), RowText(s, r), r.Why, r.ItemId, r.QuestIds, source)
        {
            Alternatives = r.Alternatives,
            PictureId = ItemSources.PictureOf(s.Sources, r.ItemId),
            Mark = mark?.Text ?? "",
            Fresh = mark is not null && FreshMark(r),
        };
    }

    /// <summary>A tiny cell for an item, with how many (the row's, or one quest's); its tooltip says what it is, what
    /// for, what is known of having it, and where to get it.</summary>
    private NeedChip Chip(SessionSnapshot s, RequirementView r, int? count = null)
    {
        var mark = MarkOf(s, r);
        return new NeedChip(r.ItemId, GlyphOf(r.Kind), string.Join("\n", new[] { RowText(s, r), r.Why, mark?.Text ?? "", BestSource(s, r) }.Where(t => t.Length > 0)))
        {
            Alternatives = r.Alternatives,
            PictureId = ItemSources.PictureOf(s.Sources, r.ItemId),
            Count = count ?? r.Count,
            Marked = mark is not null,
            Fresh = mark is not null && FreshMark(r),
        };
    }

    /// <summary>What one quest needs brought on a plan's map, as tiny cells beside its name, each with that quest's own
    /// number (empty: nothing).</summary>
    private IReadOnlyList<NeedChip> Chips(SessionSnapshot s, MapPlanView? plan, string questId) =>
        (plan?.Requirements ?? []).Where(r => r.QuestIds.Contains(questId)).Select(r => Chip(s, r, r.CountFor(questId))).ToList();

    /// <summary>
    /// A map's bosses as parts of a line of facts, each linked to its spawn zones' markers while that map is the one
    /// shown: pointing at "Kaban 75%" lights Kaban's zones, and pointing at one of them tints the name (the review of
    /// 2026-10-04, E3). The names are the session's; the ids the markers' groups are made of come from the same list
    /// in the data, and if the two don't line up the names stay plain text.
    /// </summary>
    private static IEnumerable<LinePart> BossParts(SessionSnapshot s, string? normalizedName, IReadOnlyList<string> texts)
    {
        var mobs = s.Data is { } data && data.Maps.Values.FirstOrDefault(m => m.NormalizedName == normalizedName) is { } map
            ? data.BossMobsOn(map.Id).Take(texts.Count).ToList()
            : [];
        var linked = s.Map?.NormalizedName == normalizedName && mobs.Select(b => Planning.BossText((b.Name, b.Chance))).SequenceEqual(texts);
        var groups = linked ? (s.Content?.Markers ?? []).Select(m => m.Group).OfType<string>().Distinct().ToList() : [];
        return texts.Select((text, i) => new LinePart(text,
            linked ? groups.Where(g => MapContentBuilder.BossesOf(g).Contains(mobs[i].Mob)).ToList() : null));
    }

    // The same counts in a row of Plan's map list, where the heading and the card say what they count.
    private static string ShortSummary(int complete, int progress) => (complete, progress) switch
    {
        (> 0, > 0) => AppTexts.PlanRowCompleteAndProgress(complete: complete, progress: progress),
        (> 0, _) => AppTexts.PlanRowComplete(complete: complete),
        _ => AppTexts.PlanRowProgress(progress: progress),
    };

    /// <summary>"Complete 8 quests · progress 2 more": what one raid on the map does for your quest list.</summary>
    private static string Summary(int complete, int progress)
    {
        return (complete, progress) switch
        {
            (> 0, > 0) => AppTexts.PlanSummaryCompleteAndProgress(complete: complete, progress: progress),
            (> 0, _) => AppTexts.PlanSummaryComplete(complete: complete),
            _ => AppTexts.PlanSummaryProgress(progress: progress),
        };
    }

    // The raid card is the plan card for this map, live: the same header and COMPLETE / PROGRESS / BRING sections,
    // with each quest's objectives here under it, nearest first, and the quests ordered by their nearest objective.
    private void UpdateRaidLists(SessionSnapshot s)
    {
        var vm = ViewModel;
        var age = s.RaidFix is { } fix ? FixAge.Of(fix.At, DateTime.Now) : (TimeSpan?)null;
        var fresh = age < FreshFix;
        _fixWasFresh = age is null ? null : fresh;
        string Direction(RelativeDirection? relative, double? mapBearing) =>
            fresh && relative is { } r ? Bearing.Describe(r) : mapBearing is { } b ? Bearing.Compass(b) : "";

        // The raid's own map, never one that is only being looked at; a map the data doesn't know is said, not guessed.
        vm.RaidTitle = s.RaidMapUnknown ? AppTexts.RaidMapNotKnown : Caps.Of(s.RaidMap?.Name ?? s.Map?.Name);
        vm.LookText = s.LooksAtAnotherMap ? AppTexts.RaidLookingAt(map: Caps.Of(s.Map?.Name)) : "";
        vm.LookNote = s.LooksAtAnotherMap ? AppTexts.RaidLookNote(map: Caps.Of(s.RaidMap?.Name)) : "";
        // When the logs can't tell (PvE), the raid is shown as a PMC's and the tag is a switch to say otherwise.
        vm.RaidSide = s.Raid.Side switch { RaidSide.Scav => AppTexts.RaidSideScav, RaidSide.Pmc => AppTexts.RaidSidePmc, _ => vm.InRaid ? AppTexts.RaidSidePmc : "" };
        vm.SideSwitchable = vm.InRaid && !s.SideFromLogs;
        vm.ScavRaid = vm.InRaid && s.Raid.Side == RaidSide.Scav;
        vm.RaidSummary = s.MapPlan is { } plan && plan.Finish.Count + plan.Progress.Count > 0 ? Summary(plan.Finish.Count, plan.Progress.Count) : "";
        var complete = s.MapPlan?.Finish.Select(q => q.QuestId).ToHashSet() ?? [];
        var kinds = (s.MapPlan?.Finish ?? []).Concat(s.MapPlan?.Progress ?? []).ToDictionary(q => q.QuestId, q => q.Kind);
        // A quest whose objectives here are all ticked as done has nothing left to do on this map: it isn't listed.
        // Otherwise its open lines come first, nearest first, then the ticked ones, muted, with "done" where the
        // distance was (owner, 2026-10-04).
        var quests = s.Objectives
            .GroupBy(o => o.QuestId)
            .Where(g => g.Any(o => !o.Done))
            .Select(g =>
            {
                var first = g.First();
                var objectives = g.OrderBy(o => o.Done ? 1 : 0).ThenBy(o => o.HasPlace ? 0 : 1).ThenBy(o => o.Distance ?? double.MaxValue)
                    .Select(o => ToItem(o, o.Done ? AppTexts.RaidObjectiveDone : o.HasPlace ? Direction(o.Direction, o.MapBearing) : Unplaced(o.Kind), s.RaidMap?.Name, s.Sources))
                    .ToList();
                return (Nearest: g.Min(o => o.Distance ?? double.MaxValue), Quest: new RaidQuest(g.Key,
                    kinds.TryGetValue(g.Key, out var kind) ? kind : QuestTaxonomy.QuestKind(g.Select(o => o.Kind)),
                    first.QuestName, first.TraderId, first.Trader, objectives, complete.Contains(g.Key), Chips(s, s.MapPlan, g.Key)));
            })
            .OrderBy(q => q.Nearest)
            .ThenBy(q => q.Quest.Name, StringComparer.Create(UiLanguage.Culture, ignoreCase: false))
            .Select(q => q.Quest)
            .ToList();
        // The picks first, nearest first; then COMPLETE and PROGRESS without them. Each list on screen stays while it
        // says the same (RowLists).
        vm.RaidPicks = RowLists.Keep(vm.RaidPicks, quests.Where(q => s.Picks.Contains(q.QuestId)).ToList(), RaidQuest.Same);
        vm.RaidComplete = RowLists.Keep(vm.RaidComplete, quests.Where(q => q.Complete && !s.Picks.Contains(q.QuestId)).ToList(), RaidQuest.Same);
        vm.RaidProgress = RowLists.Keep(vm.RaidProgress, quests.Where(q => !q.Complete && !s.Picks.Contains(q.QuestId)).ToList(), RaidQuest.Same);
        // While the raid loads, the kit comes first, as a last check while matching can still be cancelled; BRING
        // returns to its place below when the raid starts (owner, 2026-10-03).
        var kit = Planning.KitWhileLoading(s.Raid, s.MapPlan, s.Picks);
        vm.RaidKit = RowLists.Keep(vm.RaidKit, kit.Main.Select(r => BringLine(s, r)).ToList(), RequirementLine.Same);
        vm.RaidKitMore = RowLists.Keep(vm.RaidKitMore, kit.More.Select(r => BringLine(s, r)).ToList(), RequirementLine.Same);
        vm.RaidBring = RowLists.Keep(vm.RaidBring, s.Raid.Phase == RaidPhase.Loading ? [] : BringLines(s, s.MapPlan?.Requirements ?? [], s.Picks),
            RequirementLine.Same);
        // The raid's map's operational tasks and the any-map ones (owner, 2026-10-19, B); a Scav's are its FIND IN RAID's.
        vm.RaidOperational = RowLists.Keep(vm.RaidOperational, vm.ScavRaid ? [] : TaskLines(s, s.MapPlan?.NormalizedName, inRaid: true), OperationalLine.Same);
        vm.RaidOperationalEnded = vm.ScavRaid ? "" : OperationalRail.Ended(s.OperationalEnded);
        vm.RaidNote = "";
        vm.RaidLoot = [];
        vm.RaidLootMore = "";
        if (vm.ScavRaid)
            ShowScavRaid(s);
        // The muted line of what is so of each exit, as the raid card's clock stands (its MIN IN and MIN LEFT; a Scav's
        // own minutes aren't the raid's): "may be closed" until the list was read, a train's window stepping back once it
        // has passed (owner, 2026-10-17). OnClockTick words it again at each new minute.
        var clock = vm.RaidClock;
        _noteMinute = clock?.In;
        vm.Extracts = RowLists.Keep(vm.Extracts, s.Extracts.Select(e =>
        {
            var note = ExtractRules.NoteLine(e.Notes, e.State != ExitState.NotChecked, clock is { Scav: false } c ? c.In : null, clock?.Left, clock?.Over == true);
            return new ExtractItem(
                e.Id,
                e.Name,
                e.Kind switch
                {
                    MarkerKind.ExtractPmc => AppTexts.ExtractKindPmc,
                    MarkerKind.ExtractScav => AppTexts.ExtractKindScav,
                    MarkerKind.Transit => AppTexts.ExtractKindTransit,
                    _ => AppTexts.ExtractKindShared,
                },
                Distance(e.Distance),
                Direction(e.Direction, e.MapBearing),
                e.Needs,
                e.NeedItemId,
                e.Kind)
            {
                State = e.State,
                ListReadable = s.ReadExits && !s.ExitReaderMissing,
                Note = note.Text,
                NotePassed = note.Passed,
            };
        }).ToList());
        vm.ExitsNote = Rules.ExitsNote.Of(s.Raid.Phase == RaidPhase.InRaid, s.ReadExits, s.ExitReaderMissing, s.ExitsReadAt,
            s.Extracts.Count(e => e.State is ExitState.Listed or ExitState.Unsure), s.Extracts.Count(e => e.Kind != MarkerKind.Transit));

        // The glance: where to go next and the nearest way out, the two things a few seconds' look is for (the study
        // log: in a raid the app got glances with a median of 3.9 s).
        // With picks, NEXT is the nearest objective among them (the guide line leads there too); else the nearest of all.
        var placed = s.Objectives.Where(o => o.HasPlace && o.Distance is not null && !o.Done).ToList();
        var next = placed.Where(o => s.Picks.Contains(o.QuestId)).MinBy(o => o.Distance) ?? placed.MinBy(o => o.Distance);
        vm.RaidNext = vm.ScavRaid || next is null ? null : ToItem(next, Direction(next.Direction, next.MapBearing), s.RaidMap?.Name, s.Sources);
        // The nearest extract, never a transit (Rules.ExitsNote.Exit).
        var exit = Rules.ExitsNote.Exit(vm.Extracts.Select(e => (e.Distance.Length > 0, e.State == ExitState.NotListed, e.Marker == MarkerKind.Transit)).ToList());
        vm.RaidExit = exit is { } at ? vm.Extracts[at] : null;
        vm.AllExitsText = vm.RaidExit is not null && vm.Extracts.Count > 1 ? AppTexts.RaidAllExits(count: vm.Extracts.Count) : "";
        // Under it, where EXIT takes something or isn't sure: the nearest one on the list that takes nothing (owner,
        // 2026-10-05: "where you can simply exfil").
        var plain = Rules.ExitsNote.Plain(
            vm.Extracts.Select(e => (e.Distance.Length > 0, e.State == ExitState.Listed, e.Marker == MarkerKind.Transit, e.Needs)).ToList(),
            vm.RaidExit is null ? null : vm.Extracts.ToList().IndexOf(vm.RaidExit));
        vm.RaidPlainExit = plain is { } p ? vm.Extracts[p] : null;

        vm.Hint = s.Data is null ? AppTexts.HintLoading
            : vm.NoGameLogs ? "" // the no-game line says why there is nothing to plan
            : !vm.InRaid && s.Plan.Count == 0 ? AppTexts.HintNoQuestOnAMap
            : vm.InRaid && !vm.ScavRaid && !s.Objectives.Any(o => !o.Done) ? AppTexts.HintNothingLeftHere(count: s.ActiveQuestCount)
            : "";
        vm.Attribution = AttributionFor(s.Definition);
        // The wiki's interactive map for this map: its page name plus "_Interactive_Map".
        vm.WikiMap = s.Map is { } shown ? Rules.OutsideLink.WikiMap(s.Data?.Maps.GetValueOrDefault(shown.Id)?.Wiki) : null;
    }

    // The loot a Scav raid can find for your quests, up to this many rows; the rest is one line.
    private const int LootShown = 8;

    // A Scav raid in the raid card: quest objectives only count for the PMC, so instead of COMPLETE and PROGRESS the
    // card lists what your quests need found in raid, which counts whoever finds it.
    private void ShowScavRaid(SessionSnapshot s)
    {
        var vm = ViewModel;
        var loot = s is { Data: { } data, Map: { } map } ? ScavRaid.Loot(data, s.Quests, data.MapIdsSharing(map.NormalizedName), s.Done) : [];
        var quests = loot.SelectMany(l => l.QuestIds).Distinct().Count();
        vm.RaidSummary = quests switch { 0 => "", _ => AppTexts.ScavFindItems(count: quests) };
        // Loading as a Scav (a server-hosted raid's setup says so early) has no kit to check: nothing counts for quests.
        vm.RaidNote = s.Raid.Phase == RaidPhase.Loading
            ? AppTexts.ScavNoteLoading
            : AppTexts.ScavNote;
        vm.RaidKit = [];
        vm.RaidKitMore = [];
        vm.RaidPicks = [];
        vm.RaidComplete = [];
        vm.RaidProgress = [];
        vm.RaidBring = [];
        // After the quests' loot, the operational tasks' hand-over items (owner, 2026-10-19, B): found in raid by a Scav
        // as by a PMC.
        vm.RaidLoot = loot.Take(LootShown).Select(l => new RequirementLine(Glyphs.Bring, l.Text, AppTexts.ScavLootFor(quests: l.ForQuests), l.ItemId, l.QuestIds,
            l.SpotsHere switch { 0 => "", var n => AppTexts.ScavLooseHere(count: n) })).Concat(TaskLoot(s)).ToList();
        vm.RaidLootMore = loot.Count > LootShown ? AppTexts.ScavLootMore(count: loot.Count - LootShown) : "";
    }

    // Where an objective without a place on the map is done: kills and finds anywhere on it, hand-overs at the trader.
    private static string Unplaced(ObjectiveKind kind) => kind switch
    {
        ObjectiveKind.Trader => AppTexts.ObjectiveAfterRaid,
        ObjectiveKind.Survive => "",
        _ => AppTexts.ObjectiveAnywhere,
    };

    private static ObjectiveItem ToItem(ObjectiveView o, string direction, string? mapName, ItemSources? sources) =>
        ToItemOf(o, direction, mapName) with { PictureId = o.ItemId is { } item ? ItemSources.PictureOf(sources, item) : null };

    private static ObjectiveItem ToItemOf(ObjectiveView o, string direction, string? mapName)
    {
        if (o.HeightDifference is { } h)
            direction += (direction.Length > 0 ? " · " : "") + (h > 0 ? AppTexts.HeightUp(metres: Math.Abs(h).ToString("0", UiLanguage.Culture)) : AppTexts.HeightDown(metres: Math.Abs(h).ToString("0", UiLanguage.Culture)));
        // In a few words where the data allows it (owner, 2026-10-04: the raid card is read in seconds; the quest's
        // card keeps tarkov.dev's sentence).
        if (o.Short is { Length: > 0 } few)
        {
            return new ObjectiveItem(o.QuestId, few, string.IsNullOrEmpty(o.Trader) ? o.QuestName : $"{o.QuestName} · {o.Trader}",
                Distance(o.Distance), direction, o.Done, o.Kind, o.Needs ?? "", o.TraderId, o.Trader, o.ObjectiveId, o.NeedKey, o.Handover,
                o.HandoverCount, o.ItemId);
        }
        // "… on Streets of Tarkov" says nothing while on Streets of Tarkov; an optional objective keeps its "(optional)".
        var text = o.TextOn(mapName);
        return new ObjectiveItem(o.QuestId, text, string.IsNullOrEmpty(o.Trader) ? o.QuestName : $"{o.QuestName} · {o.Trader}",
            Distance(o.Distance), direction, o.Done, o.Kind, o.Needs ?? "", o.TraderId, o.Trader, o.ObjectiveId, o.NeedKey, o.Handover,
            o.HandoverCount, o.ItemId);
    }

    // The map's guide plate says the same (MapRenderer.DistanceText).
    private static string Distance(double? metres) => metres is { } m ? MapRenderer.DistanceText(m) : "";


    private async void UpdateMap(SessionSnapshot s)
    {
        // While another map is previewed, the shown one waits; it comes back when the preview ends.
        if (s.Definition is null || _session.Artwork is null || _previewing is not null)
            return;
        var key = s.Definition.Key;
        // The map's scene is made once its artwork is here. Until then the view holds the scene of the map before,
        // and nothing about this map goes into it (SceneGate): a snapshot that arrives meanwhile waits for the scene.
        if (_scenes.Wants(key))
        {
            var artwork = await ArtworkFor(s.Definition, s.Map?.Name);
            // Another map was asked for meanwhile, or a preview took the view: its end makes the scene anew.
            if (_previewing is not null || !_scenes.Arrived(key))
                return;
            // Without SVG artwork, tarkov.dev's tile render where it has one (The Lab, Labyrinth, Icebreaker).
            var tiles = artwork is null ? TilesFor(s.Definition, s.Map?.Name) : null;
            // No usable artwork at all (docs/DESIGN.md §3): a sheet with a metric grid stands in; said once per map.
            if (artwork is null && tiles is null && _sheetNoticeShown.Add(key))
                ShowNotice(AppTexts.NoticeNoArtwork(map: s.Map?.Name));
            Map.SetScene(new MapScene(s.Definition, artwork, tiles), _restoreView);
            _restoreView = null;
        }
        // The newest snapshot fills the scene, if the scene in the view is its map's.
        if (Map.Scene is not { } scene || _snapshot is not { } latest || !_scenes.Holds(latest.Definition?.Key))
            return;
        scene.Player = latest.Fix;
        scene.InRaid = latest.Raid.Phase == RaidPhase.InRaid;
        scene.Trail = latest.Trail;
        scene.Floor = ShownFloor(latest);
        // A quest just completed: its places that go now ring out first (MainWindow.Completion).
        var before = scene.Markers;
        scene.Markers = latest.Content?.Markers ?? [];
        NoteRemoved(before, scene.Markers);
        scene.Zones = latest.Content?.Zones ?? [];
        scene.Containers = latest.Content?.Containers ?? [];
        // Before the picks and the focus: a quest brings the locks of the keys it needs here along, and the bosses its kill
        // objectives target.
        scene.QuestKeys = latest.Content?.QuestKeys ?? new Dictionary<string, IReadOnlyList<string>>();
        scene.QuestBosses = latest.Content?.QuestBosses ?? new Dictionary<string, IReadOnlyList<string>>();
        // The picks of the map drawn (picks are kept per map), which in a raid can be another than the raid's.
        scene.PickSlots = latest.PickSlotsOn(latest.Map?.NormalizedName);
        scene.Kept = latest.PicksOn(latest.Map?.NormalizedName);
        // The extracts the game's own list named this raid, once a screenshot showed it (owner, 2026-10-05): they are lit,
        // the others are drawn hollow, the ones marked "??:??:??" carry a "?". Only on the raid's own map.
        var exitsRead = latest.ExitsReadAt is not null && !latest.LooksAtAnotherMap;
        scene.ExitsNotListed = exitsRead ? latest.Extracts.Where(e => e.State == ExitState.NotListed).Select(e => e.Id).ToHashSet(StringComparer.Ordinal) : NoExits;
        scene.ExitsListed = exitsRead ? latest.Extracts.Where(e => e.State == ExitState.Listed).Select(e => e.Id).ToHashSet(StringComparer.Ordinal) : NoExits;
        scene.ExitsUnsure = exitsRead ? latest.Extracts.Where(e => e.State == ExitState.Unsure).Select(e => e.Id).ToHashSet(StringComparer.Ordinal) : NoExits;
        // A list the player just showed is called out: YOUR EXTRACTS THIS RAID names its extracts and flies each to its place
        // (owner, 2026-10-15, from a panel of four: A, "Roll call, then fly-out"; MainWindow.RollCall), and one more of a
        // run of screenshots joins it, or after it rings what it lit anew: not a list read a while ago, found as the logs
        // are read at start, nor one called out already when the map comes back after another.
        var exitsArrive = false;
        if (exitsRead && latest.ExitsReadAt != _exitsReadShown)
        {
            _exitsReadShown = latest.ExitsReadAt;
            exitsArrive = Shturmap.Core.Logs.WallClock.Elapsed(latest.ExitsReadAt!.Value, DateTime.Now) < MapView.JustTaken;
        }
        scene.LinkedSet = Linked.Current is { IsSet: true };
        scene.Focus = LightBreath(latest);
        scene.FocusObjective = Linked.Current?.Objective;
        // A trader's held focus: its quests lit while nothing is pointed at, the rest stepped back (TraderFocus.OnMap).
        scene.Held = HeldOnMap(latest);
        // A replay playing on this map: a scene made anew (the map came back after a preview) takes it up.
        ApplyReplay();
        // The position first (it pings, and with Follow my position on its glide begins), then the list's roll call.
        Map.Refresh();
        // A held focus that shows on this scene, or no longer does (a new scene, its quests' places come or go): the
        // stepping back eases in or out as it does for the pointer.
        Map.Redraw();
        if (exitsArrive)
            ExitListJustRead(scene, latest);
        if (HelpFlyout.IsOpen)
            ShowLegend(opened: false);
    }

    private static readonly IReadOnlySet<string> NoExits = new HashSet<string>();

    // The screenshot time of the extract list the map last showed as read (SessionSnapshot.ExitsReadAt).
    private DateTime? _exitsReadShown;

    // Who drew what is on the map. The SVG maps' artists by name and licence; the tile renders (Battlestate's level,
    // rendered by tarkov.dev or TarkovBOT.eu; docs/DESIGN.md §3) by who maps.json names; the sheet says what it is.
    private string AttributionFor(MapDefinition? definition) => definition switch
    {
        { SvgPath: not null, Author: { } author } => AppTexts.MapCreditArtwork(author: author),
        { SvgPath: null } when _session.Artwork?.TilesFor(definition) is { Status: not TileStatus.Unavailable } =>
            AppTexts.MapCreditRender(author: definition.Author ?? "tarkov.dev"),
        { SvgPath: null } => AppTexts.MapCreditSheet(spacing: Map.ShowGrid ? 100 : 10),
        _ => AppTexts.MapCreditData,
    };

    private readonly HashSet<MapTiles> _tilesWatched = [];

    // A map's tile render. If no tile can be had (offline without saved tiles, or none published), the grid sheet
    // stands in, said once per map, and the credit line follows.
    private MapTiles? TilesFor(MapDefinition definition, string? mapName)
    {
        if (_session.Artwork?.TilesFor(definition) is not { } tiles)
            return null;
        if (_tilesWatched.Add(tiles))
        {
            tiles.Changed += () => DispatcherQueue.TryEnqueue(() =>
            {
                ViewModel.Attribution = AttributionFor(_snapshot?.Definition);
                if (tiles.Status == TileStatus.Unavailable && _sheetNoticeShown.Add(definition.Key))
                {
                    AppLog.Warn($"Map render for {mapName} not loaded: no tile could be had");
                    ShowNotice(AppTexts.NoticeNoRender(map: mapName),
                        TimeSpan.FromSeconds(12));
                }
            });
        }
        return tiles;
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
                    LoadFailure.Unreachable or LoadFailure.TimedOut => AppTexts.NoticeArtworkOffline(map: mapName),
                    LoadFailure.ServerBusy or LoadFailure.Refused => AppTexts.NoticeArtworkRefused(map: mapName, status: problem.Status),
                    LoadFailure.Disk => AppTexts.NoticeArtworkDisk(map: mapName),
                    _ => AppTexts.NoticeArtworkUnreadable(map: mapName),
                };
                ShowNotice(why,
                    TimeSpan.FromSeconds(12));
            }
            return null;
        }
    }

    // ---- previewing another map from Plan ----

    // Resting on a map's row in Plan's list (or on the open card, when its map isn't the one on screen) shows that
    // map for as long as the pointer stays, without switching to it; the view of the shown map comes back as it was
    // (the study log: ten card clicks in 4.5 minutes to compare maps). Only a click on a row switches the map.
    private static readonly TimeSpan PreviewAfter = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan PreviewEndAfter = TimeSpan.FromMilliseconds(300);
    private DispatcherQueueTimer? _previewTimer;
    private string? _previewWanted;
    private string? _previewing;
    private (Shturmap.Core.Maps.MapPoint Center, double Zoom)? _restoreView;

    private void OnPlanPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string map } && !ViewModel.InRaid && map != _snapshot?.Map?.NormalizedName)
            Preview(map, PreviewAfter);
    }

    private void OnPlanPointerExited(object sender, PointerRoutedEventArgs e) => Preview(null, PreviewEndAfter);

    // The rows of Plan's map list (the tour points at one): the buttons that show a suggested map.
    private static IEnumerable<Button> MapRows(DependencyObject under)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(under); i++)
        {
            var child = VisualTreeHelper.GetChild(under, i);
            if (child is UIElement { Visibility: Visibility.Collapsed })
                continue;
            if (child is Button { Tag: string, DataContext: PlanCard } map)
            {
                yield return map;
                continue;
            }
            foreach (var deeper in MapRows(child))
                yield return deeper;
        }
    }

    private void Preview(string? map, TimeSpan after)
    {
        // While the tour is up it owns the map, and what the pointer asks for counts for nothing (Rules.PreviewOwner;
        // owner, 2026-10-15: the tour:5 line's leaving, under the tour's layer, ended YOUR SCREENSHOT KEY's staging).
        if (!PreviewOwner.Heeds(TourOpen))
            return;
        _previewWanted = map;
        if (_previewTimer is null)
        {
            _previewTimer = WindowTimer(() =>
            {
                switch (PreviewOwner.After(TourOpen, _previewing, _previewWanted))
                {
                    case PreviewOwner.Step.Start:
                        StartPreview(_previewWanted!);
                        break;
                    case PreviewOwner.Step.End:
                        EndPreview(restore: true);
                        break;
                }
            });
            _previewTimer.IsRepeating = false;
        }
        _previewTimer.Stop();
        // Moving straight from one card to the next switches at once; only the first one waits.
        _previewTimer.Interval = map is not null && _previewing is not null ? TimeSpan.FromMilliseconds(120) : after;
        _previewTimer.Start();
    }

    private async void StartPreview(string normalizedName)
    {
        // A preview takes the map: a replay playing on it ends, and a What's New plate from the preview before goes.
        StopReplay("preview");
        EndWhatsNewPreview();
        if (normalizedName.StartsWith(WhatsNewPreviewPrefix, StringComparison.Ordinal))
        {
            await StartWhatsNewPreviewAsync(normalizedName);
            return;
        }
        if (_snapshot is not { Data: { } data } s || data.MapByNormalizedName(normalizedName) is not { } map
            || data.DefinitionFor(normalizedName) is not { } definition || _session.Artwork is null)
            return;
        // No map drawn yet: nothing to come back to; the map is fitted when the preview ends.
        if (_previewing is null)
            _restoreView = Map.HasView ? Map.View : null;
        _previewing = normalizedName;
        var artwork = await ArtworkFor(definition, map.Name);
        if (_previewing != normalizedName)
            return;
        var active = s.Quests.Values.Where(q => q.State == QuestState.Active).Select(q => q.QuestId);
        var content = MapContentBuilder.Build(data, map.Id, active, s.Done);
        Map.SetScene(new MapScene(definition, artwork, artwork is null ? TilesFor(definition, map.Name) : null)
            { Markers = content.Markers, Zones = content.Zones, Containers = content.Containers, Held = HeldOnMap(s) });
        Map.Redraw();
        _scenes.Forget();
        ShowFloors();
        ViewModel.PreviewText = AppTexts.PreviewMap(map: Caps.Of(map.Name));
        Study.Ui("map.preview", ("map", normalizedName));
    }

    // The floor picker is the shown map's: while another map is previewed it steps aside, and the shown map's scene
    // puts it back when the preview ends (review of 2026-10-10: a tour chapter staged on Customs showed Streets' floors).
    // It steps aside the same way while a cue or What's New's plate shows, and is back when that goes (QA pass of
    // 2026-10-15: at 900×560 it lay over the end of SEEN IN YOUR STASH's and RAID LOADING's titles, and over the right
    // edge of the item card on the plate).
    private void ShowFloors() =>
        ViewModel.Floors = _previewing is not null || CuePanel.Visibility == Visibility.Visible || WhatsNewPlate.Visibility == Visibility.Visible
            || RaidGlancePlate.Visibility == Visibility.Visible
            ? [] : _floorChoices;

    private void EndPreview(bool restore)
    {
        if (_previewing is null)
            return;
        _previewing = null;
        _previewWanted = null;
        ViewModel.PreviewText = "";
        EndWhatsNewPreview();
        var s = _snapshot;
        // A preview that put a map of its own in the view gives the player's back, made anew, at the view it had (or
        // fitted, when none was drawn before); one that showed only a cue or a plate over it leaves that scene as it
        // was, and the view where it is (SceneGate.MakeAnew).
        if (!_scenes.MakeAnew(s?.Definition?.Key) || !restore)
            _restoreView = null;
        if (s is not null)
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
        _floorChoices = _floors.Select((layer, i) => new FloorChoice(i, layer?.Name ?? AppTexts.MapFloorGround, i == _shownFloor, i == playerFloor)).ToList();
        ShowFloors();
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

    private QuestCardView? BuildCard(string questId)
    {
        if (_snapshot is not { Data: { } data } s)
            return null;
        var view = QuestCards.Build(data, s.Quests, questId, LiveText, s.Sources, s.Ticks, notes: s.QuestNotes);
        if (s.Marks.Count == 0 || view is null)
            return view;
        // The card's BRING rows carry the marks as Plan's do (Session.StashMarks).
        return view with
        {
            Needs = view.Needs.Select(n =>
            {
                var row = new RequirementView(n.Kind, n.Text, "", n.ItemId, n.QuestIds, Alternatives: n.Alternatives, Count: n.Count);
                if (StashMarks.ForRow(data, row, s.Marks) is not { } mark)
                    return n;
                var source = mark.More > 0 && n.Source.Length > 0 ? SessionTexts.MarkMore(count: mark.More.ToString("N0", UiLanguage.Culture), source: n.Source) : "";
                return n with { Mark = mark.Text, Source = source, Fresh = FreshMark(row) };
            }).ToList(),
        };
    }

    private FrameworkElement? CreateCard(CardKey key)
    {
        switch (key)
        {
            case CardKey.Quest q when BuildCard(q.Id) is { } view:
                var questCard = new QuestCard(view);
                // A note written on the card, or deleted from it (owner, 2026-10-15): the card follows the session's
                // next snapshot, which comes after the notes file is written.
                questCard.NoteAdded += (quest, text) => _ = _session.AddQuestNoteAsync(quest, text);
                questCard.NoteDeleted += note => _ = _session.DeleteQuestNoteAsync(note);
                return questCard;
            case CardKey.Item i when _snapshot is { Data: not null } s:
                var card = new ItemCard(BuildItemCard(s, i.Id));
                // NOT ANY MORE: the mark goes, from any source; the card follows the session's next snapshot.
                card.NotAnyMore += itemId =>
                {
                    Study.Ui("card.notanymore");
                    _ = _session.NotAnyMoreAsync(itemId);
                };
                return card;
            default:
                return null;
        }
    }

    // The item card's view, with what Shturmap learned of having the item (Session.StashMarks), if anything.
    private static ItemCardView BuildItemCard(SessionSnapshot s, string itemId)
    {
        var data = s.Data!;
        var view = ItemCards.Build(data, s.Sources, s.Quests, itemId, s.Done);
        return s.Marks.GetValueOrDefault(itemId) is { } mark ? view with { Stash = StashMarks.ForCard(data, mark) } : view;
    }

    // Open cards follow the session: a quest's status and distances, the quests an item is needed for.
    private bool UpdateCard(FrameworkElement card)
    {
        switch (card)
        {
            case QuestCard quest when BuildCard(quest.View.QuestId) is { } view:
                quest.Show(view);
                return true;
            case ItemCard item when _snapshot is { Data: not null } s:
                item.Show(BuildItemCard(s, item.View.ItemId));
                return true;
            default:
                return false;
        }
    }

    /// <summary>"121 m · NE · 3 M UP" for an objective on the shown map, from the last fix; empty without one. The
    /// raid card's style: the figure as it is, the direction and height in capitals (2026-10-09: the card said
    /// "294 m · left · 8 m up" beside the raid card's "LEFT · 8 M UP").</summary>
    private string? LiveText(string objectiveId)
    {
        if (_snapshot is not { RaidFix: { } fix } s || s.Objectives.FirstOrDefault(o => o.ObjectiveId == objectiveId && o.Distance is not null) is not { } o)
            return null;
        var fresh = FixAge.Of(fix.At, DateTime.Now) < FreshFix;
        var direction = fresh && o.Direction is { } r ? Bearing.Describe(r) : o.MapBearing is { } b ? Bearing.Compass(b) : "";
        var parts = new[] { Distance(o.Distance), Caps.Of(direction), Caps.Of(o.HeightDifference is { } h ? (h > 0 ? AppTexts.HeightUp(metres: Math.Abs(h).ToString("0", UiLanguage.Culture)) : AppTexts.HeightDown(metres: Math.Abs(h).ToString("0", UiLanguage.Culture))) : "") };
        return string.Join(" · ", parts.Where(p => p.Length > 0));
    }

    private IReadOnlySet<string> MapFocus()
    {
        if (Linked.Current is not { } focus)
            return new HashSet<string>();
        var ids = new HashSet<string>(focus.Quests);
        if (focus.Marker is { } marker)
        {
            ids.Add(marker);
            // An extract lights the switches it needs, a switch the extracts it opens.
            if (_snapshot?.Content?.Links.TryGetValue(marker, out var linked) == true)
                ids.UnionWith(linked);
        }
        // A key lights the locks it opens; a row that stands for several keys ("A or B"), those of each.
        foreach (var item in focus.Items)
            ids.Add(MapContentBuilder.KeyGroup(item));
        return ids;
    }

    /// <summary>
    /// The linked highlight's breath (owner, 2026-10-17, from a panel of four: "B"; LinkedBreath): lit while the pointer
    /// lights a linked set (a trader's portrait, a boss's name, an item), held while a click holds just that (the
    /// trader strip's hold); one thing pointed at rings and lights none. Returns the map's focus.
    /// </summary>
    private IReadOnlySet<string> LightBreath(SessionSnapshot? s)
    {
        var focus = MapFocus();
        var now = DateTime.Now;
        var set = Linked.Current is { IsSet: true };
        Map.Breath.Light(set ? focus : null, now);
        Map.Breath.Hold(set && HoldsFocus(s, focus), now);
        return focus;
    }

    // Whether the trader's held focus is what the pointer lights (its portrait pointed at): held, it never breathes.
    private bool HoldsFocus(SessionSnapshot? s, IReadOnlySet<string> focus) =>
        s is not null && HeldOnMap(s) is { Count: > 0 } held && held.SetEquals(focus);

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
        // The rows take the breath's look whether or not a map is shown.
        var focus = LightBreath(Volatile.Read(ref _snapshot));
        if (Map.Scene is not { } scene)
            return;
        scene.LinkedSet = Linked.Current is { IsSet: true };
        scene.Focus = focus;
        // One objective of the quest pointed at: only its places pulse, the quest's others stay lit.
        scene.FocusObjective = Linked.Current?.Objective;
        // Pointing at an item shows where it lies loose on the shown map; a row of several items, where each does.
        scene.Spawns = Linked.Current is { } pointed && _snapshot is { Data: { } data, Map: { } map }
            ? pointed.Items.SelectMany(data.SpawnsOf).Where(s => data.MapIdsSharing(map.NormalizedName).Contains(s.MapId)).Select(s => s.Position).Distinct().ToList()
            : [];
        Map.Redraw();
    }

    // ---- nobody looking ----

    private DispatcherQueueTimer? _nobodyLooking;

    // While something is pointed at, the map draws its pulse about 60 times a second. A pointer left resting on a row
    // when the player turns to the game would keep that up behind the game for the whole raid. So when Shturmap's
    // window isn't the active one any more, the pointer's focus goes; moving the pointer onto something lights it
    // again, active window or not. The short wait lets an activation that follows at once cancel it.
    private void ObserveActivation(Window window) => window.Activated += (_, e) =>
    {
        if (_nobodyLooking is null)
        {
            _nobodyLooking = WindowTimer(DropPointerFocus);
            _nobodyLooking.Interval = TimeSpan.FromMilliseconds(200);
            _nobodyLooking.IsRepeating = false;
        }
        _nobodyLooking.Stop();
        if (e.WindowActivationState == WindowActivationState.Deactivated)
            _nobodyLooking.Start();
    };

    private void DropPointerFocus()
    {
        // A snapshot's shown quest, the clip's drawn pointer and a developer script's aren't a mouse: they stay,
        // whichever window is active.
        if (SnapshotMode || DemoMode || ScriptedPointer)
            return;
        Map.ClearHover();
        Linked.LetGo();
    }

    /// <summary>A developer script points at something ("point", "hover"): there is no mouse to let go of it.</summary>
    private bool ScriptedPointer { get; set; }

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
                // The quest, and within it the objective this place belongs to: its line in the raid card and its row
                // on the quest's card light up, and only its own places pulse.
                Linked.Set(new Focus(new HashSet<string> { quest }, Objective: MapRenderer.ObjectiveOf(marker)));
                var p = Map.TransformToVisual(Content).TransformPoint(at);
                _cards.Enter(marker, new CardKey.Quest(quest), new Windows.Foundation.Rect(p.X - 8, p.Y - 8, 16, 16));
                break;
            case { Kind: MarkerKind.Lock } when MapContentBuilder.KeyOf(marker.Group) is { } key:
                // A padlock: its key's card, the key's rows in BRING and every lock the key opens light up, and the
                // quests the key is for here take the weaker tint (a door is its key, and it is for quests).
                Linked.Set(new Focus(Rules.LinkDoor.Quests(marker.Group, _snapshot?.Content?.QuestKeys), Item: key, Marker: marker.Id));
                var k = Map.TransformToVisual(Content).TransformPoint(at);
                _cards.Enter(marker, new CardKey.Item(key), new Windows.Foundation.Rect(k.X - 8, k.Y - 8, 16, 16));
                break;
            case { Group: { } group }:
                // A boss: all its spawn zones light up together, including zones it shares with another boss.
                var bosses = MapContentBuilder.BossesOf(group);
                var groups = (Map.Scene?.Markers ?? []).Select(m => m.Group).OfType<string>()
                    .Where(g => MapContentBuilder.BossesOf(g).Intersect(bosses).Any())
                    .Append(group).ToHashSet();
                // A set, as the boss's name is: its zones breathe together.
                Linked.Set(new Focus(groups) { IsSet = true });
                break;
            default:
                Linked.Set(new Focus(new HashSet<string>(), Marker: marker.Id));
                break;
        }
    }

    // Clicking a quest marker holds its card, like clicking the quest in the list; the card's pen picks the quest.
    private void OnMarkerClicked(MapMarker marker)
    {
        if (marker is not { Group: { } quest, Objective: not null })
            return;
        var at = Map.TransformToVisual(Content).TransformPoint(_markerAt);
        _cards.Click(new CardKey.Quest(quest), new Windows.Foundation.Rect(at.X - 8, at.Y - 8, 16, 16));
    }

    private Windows.Foundation.Point _markerAt;

    // The picks (the pen on a quest; GameSession keeps them): rows tinted, markers ringed, each pick in its colour, a line to the
    // nearest place, until a quest is done, its pen is clicked again or the picks are cleared.
    private void ShowPicks(SessionSnapshot s)
    {
        Linked.PickSlots = s.PickSlots;
        Linked.Picks = s.Picks;
        ViewModel.HasPicks = s.Picks.Count > 0;
        // The scene in view is a preview's (another map, What's New, the tour's example): the player's picks on the map
        // on screen aren't its own (review of 2026-10-09). The map's own scene takes them when the preview ends (UpdateMap).
        if (_previewing is not null)
            return;
        var (onMap, slotsOnMap) = (s.PicksOn(s.Map?.NormalizedName), s.PickSlotsOn(s.Map?.NormalizedName));
        if (Map.Scene is { } scene && (!scene.Kept.SetEquals(onMap) || scene.PickSlots.Count != slotsOnMap.Count
            || slotsOnMap.Any(p => !scene.PickSlots.TryGetValue(p.Key, out var slot) || slot != p.Value)))
        {
            scene.PickSlots = slotsOnMap;
            scene.Kept = onMap;
            Map.Redraw();
        }
    }

    private async void OnClearPicksClick(object sender, RoutedEventArgs e) => await _session.ClearPicksAsync();


    // ---- the big cue: Shturmap changed its view on its own ----

    // A cue lasts 5 s; its entrance is played at 1.8 times the original pace (owner, 2026-10-02: the elements
    // should appear more slowly, the cue shouldn't stay longer). One that pictures a kit stays 7.5 s (owner,
    // 2026-10-04: "The animation can be a bit longer"), and a quarter of a second more for each picture past the
    // first row of eight, up to 11 s: a long kit takes longer to look over.
    private static TimeSpan CueLength(ViewCue cue) =>
        Replays(cue) ? ReplayCueLength
        : TimeSpan.FromSeconds(cue.Kit is { Count: > 0 } kit ? Math.Min(11, 7.5 + 0.25 * Math.Max(0, kit.Count - CueKitRow)) : 5);

    // RAID OVER with a raid to replay: the middle cue gives way to the replay's band (MainWindow.Replay).
    private static bool Replays(ViewCue cue) => cue is { Kind: CueKind.RaidOver, Replay.Plays: true };
    private const double CuePace = 1.8;
    private Microsoft.UI.Xaml.Media.Animation.Storyboard? _cueStory;
    private DispatcherQueueTimer? _cueTimer;

    // How many pictures stand in one row of the cue's kit.
    private const int CueKitRow = 8;

    // The kit pictured under the map's name (a raid loading, a group's pick): a reminder to take in at a glance, "did
    // I pack everything?", that may make the player cancel the load and stock up; nothing in it has to be read
    // (owner, 2026-10-04). So: the items' pictures, larger than BRING's (52 px), every one of them up to three rows
    // of eight (then "+3": it was six in one row, and a long kit sat behind "+9" beside empty space), with "×3" on
    // a picture where several are needed. With picks, what they need comes first, each framed in its pick's colour,
    // then a hairline and the rest; without picks (many players never pick) it is one plain grid, what gets in and
    // out of the map first. Returns the cells in their order, for the entrance to bring them in one after another.
    private List<FrameworkElement> ShowCueKit(ViewCue cue)
    {
        CueKit.Children.Clear();
        var cells = new List<FrameworkElement>();
        var kit = cue.Kit ?? [];
        StackPanel? row = null;
        var inRow = 0;
        StackPanel Row()
        {
            if (row is null || inRow == CueKitRow)
            {
                row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
                CueKit.Children.Add(row);
                inRow = 0;
            }
            return row;
        }
        for (var i = 0; i < kit.Count; i++)
        {
            var item = kit[i];
            var cell = new Grid();
            cell.Children.Add(new Picture { ItemId = item.ItemId, PictureId = ItemSources.PictureOf(_snapshot?.Sources, item.ItemId), Glyph = GlyphOf(item.Kind), Size = 52 });
            if (item.Count > 1)
            {
                cell.Children.Add(new Border
                {
                    HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Padding = new Thickness(3, 0, 3, 1),
                    Background = Resource("GroundBrush"),
                    Child = new TextBlock
                    {
                        Text = "×" + item.Count.ToString("N0", UiLanguage.Culture), Style = (Style)Application.Current.Resources["FigureText"],
                        FontSize = 13, Foreground = Resource("InkBrush"),
                    },
                });
            }
            // Every cell has the frame's room, so framed and plain ones stand in one grid.
            var framed = new Border
            {
                BorderBrush = item.ForPick ? Linked.PickBrush(item.PickSlot) : new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(2), Padding = new Thickness(1), Child = cell,
            };
            Row().Children.Add(framed);
            inRow++;
            cells.Add(framed);
            if (item.ForPick && i + 1 < kit.Count && !kit[i + 1].ForPick && inRow < CueKitRow)
            {
                row!.Children.Add(new Microsoft.UI.Xaml.Shapes.Rectangle
                {
                    Width = 1, Height = 38, Margin = new Thickness(1, 0, 1, 0), VerticalAlignment = VerticalAlignment.Center, Fill = Resource("LineStrongBrush"),
                });
            }
        }
        if (cue.KitMore > 0 && row is not null)
        {
            var more = new TextBlock
            {
                Text = $"+{cue.KitMore}",
                Style = (Style)Application.Current.Resources["FigureText"],
                Foreground = Resource("MutedBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0),
            };
            row.Children.Add(more);
            cells.Add(more);
        }
        CueKit.Visibility = CueKitLabel.Visibility = cells.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        return cells;
    }

    private (string Eyebrow, string Title, string Detail) CueText(ViewCue cue)
    {
        var map = Caps.Of(cue.MapName);
        var plan = _snapshot?.Plan.FirstOrDefault(p => p.MapName == cue.MapName) ?? _snapshot?.MapPlan;
        var here = plan is { } p && p.MapName == cue.MapName && p.Finish.Count + p.Progress.Count > 0 ? Summary(p.Finish.Count, p.Progress.Count) : "";
        return cue.Kind switch
        {
            CueKind.RaidLoading => (AppTexts.CueRaidLoading, map, here),
            CueKind.Transit => (AppTexts.CueTransit, map, here),
            CueKind.ScavRaid => (map, AppTexts.CueScavRaid, AppTexts.CueScavRaidDetail),
            CueKind.LoadCancelled => (map, AppTexts.CueLoadingCancelled, AppTexts.CueBackToPlanning),
            CueKind.GroupPick => (AppTexts.CueGroupPicked, map, here),
            CueKind.QuestComplete => CompletionText(),
            CueKind.StashSeen => StashCueText(cue),
            CueKind.ExitsListed => RollCallText(),
            CueKind.UpdateReady => UpdateCueText(cue),
            CueKind.TasksRead => TasksCueText(),
            _ => (cue.RaidLength is { } length ? AppTexts.CueMapAndLength(map: map, minutes: (int)length.TotalMinutes) : map, AppTexts.CueRaidOver,
                _snapshot?.Plan.FirstOrDefault() is { } next
                    ? AppTexts.CueNextRaid(map: next.MapName, summary: Summary(next.Finish.Count, next.Progress.Count))
                    : AppTexts.CueBackToPlanning),
        };
    }

    // The band springs open behind the text with a gold flash, its gold rules shoot out from the middle, and the
    // title slides up while it decodes letter by letter, the undecoded letters in gold; then it all fades. Nothing
    // with text in it is ever scaled, so the text stays sharp (owner, 2026-10-01: it was sometimes blurry, could
    // last longer and use more pop). Off with Windows' animation effects: it just shows and goes.
    /// <param name="how">For a replay: "end" (the raid's end) or "link" (REPLAY on the last raid's line).</param>
    /// <param name="example">The tour's example (MainWindow.Tour): its line says so, in place of the player's own plan.</param>
    private void ShowCue(ViewCue cue, string how = "end", bool example = false)
    {
        // The tasks a Tasks screenshot read: a later reading of the same run of screenshots joins the cue on screen or
        // calls none, and the cue steps back to a notice while a replay plays or QUEST COMPLETE or SEEN IN YOUR STASH is
        // up (MainWindow.TasksCue); any other cue ends it (below, once SEEN IN YOUR STASH has had its say: it steps back
        // to its notice while this cue is up).
        if (cue.Kind == CueKind.TasksRead && TasksReadHandled(cue))
            return;
        // QUEST COMPLETE joins one on screen, or waits for a replay to end (MainWindow.Completion); any other cue ends it.
        if (cue is { Kind: CueKind.QuestComplete, Completed: { } quest } && CompletionHandled(quest))
            return;
        if (cue.Kind != CueKind.QuestComplete && CompletedShowing)
        {
            _completed.Clear();
            _completedHide?.Stop();
        }
        // SEEN IN YOUR STASH joins one on screen too, or steps back to a notice while a replay plays or QUEST COMPLETE
        // is up (MainWindow.StashCue); any other cue ends it.
        if (cue.Kind == CueKind.StashSeen && StashSeenHandled(cue, example))
            return;
        if (cue.Kind != CueKind.StashSeen && StashShowing)
            EndStashSeen();
        if (cue.Kind != CueKind.TasksRead && TasksShowing)
            EndTasksRead();
        // YOUR EXTRACTS THIS RAID gives way to any other cue as they do; its icons flying carry on, on the map
        // (MainWindow.RollCall).
        if (cue.Kind != CueKind.ExitsListed && _rollRows.Count > 0)
            EndRollCall();
        // The in-raid glance's plate gives way to any cue, as any cue ends another; a reading waiting keeps waiting for it to
        // go (MainWindow.GlancePlate).
        GlanceGivesWay();
        // A new cue ends a replay still playing; RAID OVER with a raid to replay plays it (MainWindow.Replay).
        StopReplay("cue");
        if (cue is { Kind: CueKind.RaidOver, Replay: { Plays: false } skipped })
            Study.Ui("replay.skip", ("positions", skipped.Fixes.Count), ("minutes", skipped.Minutes));
        var animations = new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
        if (Replays(cue) && (SnapshotMode || !animations))
        {
            _cueStory?.Stop();
            _cueTimer?.Stop();
            PlayReplay(cue.Replay!, how);
            return;
        }
        var (eyebrow, title, detail) = CueText(cue);
        if (example)
            detail = cue.Kind == CueKind.StashSeen ? AppTexts.CueExampleStash : AppTexts.CueExample;
        FirstRaidLine(cue, example);
        UpdateCueLine(cue);
        CueEyebrow.Text = eyebrow;
        CueDetail.Text = detail;
        CueDetail.Visibility = detail.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        var kitCells = ShowCueKit(cue);
        // The stash cue's pictures need no "check your kit" over them: the title says what they are.
        if (cue.Kind == CueKind.StashSeen)
            CueKitLabel.Visibility = Visibility.Collapsed;
        // The roll call's rows stand where the kit would (MainWindow.RollCall).
        if (cue.Kind == CueKind.ExitsListed)
            ShowRollRows();
        // The rows of the tasks read stand there too (MainWindow.TasksCue); any other cue has none.
        var taskRows = cue.Kind == CueKind.TasksRead ? ShowTasksRows() : ClearTasksRows();
        CueStamp.Visibility = Visibility.Collapsed;
        StampShape(cue.Kind);
        // The words as wide as the map's column, the title in two lines at most (FitCueTitle).
        if (Map.ActualWidth > 0)
            CueStack.MaxWidth = Map.ActualWidth;
        FitCueTitle(title);
        CuePanel.Visibility = Visibility.Visible;
        _cueStory?.Stop();
        _cueTimer?.Stop();
        if (Replays(cue))
            PlayReplay(cue.Replay!, how);
        // Laid out with its title as it ends, the cue of the tasks read keeps to its room, as the roll call's
        // (MainWindow.TasksCue, FitTasksCue).
        if (cue.Kind == CueKind.TasksRead)
        {
            CueTitle.Text = title;
            FitTasksCue();
        }
        // Laid out with its title as it ends, each row's triangle is handed to the map where it stands. An example list
        // may name fewer extracts to fit its room (MainWindow.RollCall, FitRollCue): the title counts what it names.
        if (cue.Kind == CueKind.ExitsListed)
        {
            CueTitle.Text = title;
            StartRollCall();
            title = CueTitle.Text;
        }

        if (!animations)
        {
            CueTitle.Text = title;
            CuePanel.Opacity = 1;
            CueBandScale.ScaleY = 1;
            CueRuleTopScale.ScaleX = CueRuleBottomScale.ScaleX = 1;
            CueTitleShift.Y = 0;
            CueFlash.Opacity = 0;
            if (Stays(cue))
            {
                if (cue.Kind != CueKind.ExitsListed)
                    StampStill();
                HideAfter(cue);
                return;
            }
            if (KeepsCues)
                return;
            _cueTimer = WindowTimer(() => CuePanel.Visibility = Visibility.Collapsed);
            _cueTimer.Interval = CueLength(cue);
            _cueTimer.IsRepeating = false;
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
        var end = CueLength(cue).TotalSeconds;
        const double k = CuePace;
        // Developer snapshots keep the last cue up, so it can be looked at (but for the glance's plate, KeepsCues). QUEST
        // COMPLETE and SEEN IN YOUR STASH stay as long as their kind keeps coming, and fade by themselves (HideAfter).
        if (KeepsCues || Stays(cue))
            Animate(CuePanel, "Opacity", easeOut, (0, 0), (0.12 * k, 1));
        else
            Animate(CuePanel, "Opacity", easeOut, (0, 0), (0.12 * k, 1), (end - 0.6, 1), (end, 0));
        Animate(CueBandScale, "ScaleY", spring, (0, 0), (0.42 * k, 1));
        Animate(CueFlash, "Opacity", easeOut, (0, 0), (0.1 * k, 0.32), (0.65 * k, 0));
        Animate(CueRuleTopScale, "ScaleX", spring, (0, 0), (0.15 * k, 0), (0.75 * k, 1));
        Animate(CueRuleBottomScale, "ScaleX", spring, (0, 0), (0.15 * k, 0), (0.75 * k, 1));
        Animate(CueTitleShift, "Y", easeOut, (0, 22), (0.1 * k, 22), (0.55 * k, 0));
        // The kit's pictures come in one after another once the title stands, 50 ms apart: the eye is led along
        // them, and all are there within two and a half seconds.
        for (var i = 0; i < kitCells.Count; i++)
        {
            var at = 0.6 * k + i * 0.05;
            kitCells[i].Opacity = 0;
            Animate(kitCells[i], "Opacity", easeOut, (0, 0), (at, 0), (at + 0.2, 1));
        }
        // The rows of the tasks read rise in one after another, as the roll call's rows do (MainWindow.TasksCue).
        for (var i = 0; i < taskRows.Count; i++)
        {
            var (row, shift) = taskRows[i];
            var at = (RollRiseFrom + RollRiseApart * i).TotalSeconds;
            var rise = RollRise.TotalSeconds;
            row.Opacity = 0;
            shift.Y = TasksRiseBy;
            Animate(row, "Opacity", easeOut, (0, 0), (at, 0), (at + rise, 1));
            Animate(shift, "Y", easeOut, (0, TasksRiseBy), (at, TasksRiseBy), (at + rise, 0));
        }
        story.Completed += (_, _) =>
        {
            if (ReferenceEquals(story, _cueStory) && !KeepsCues && !CompletedShowing && !StashShowing && !TasksShowing && _rollRows.Count == 0)
                CuePanel.Visibility = Visibility.Collapsed;
        };
        _cueStory = story;
        story.Begin();
        if (Stays(cue))
        {
            if (cue.Kind != CueKind.ExitsListed)
                Stamp(0.3 * k);
            HideAfter(cue);
        }
        DecodeCueTitle(title);
    }

    private void DecodeCueTitle(string title)
    {
        _cueTimer?.Stop();
        _cueTimer = Decode(CueTitle, title, TimeSpan.FromMilliseconds(900 * CuePace));
    }

    // The cue's words as wide as the map's column, as the window's size changes too (CueFit draws them at that width).
    private void OnCuePanelSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width > 0 && Math.Abs(e.NewSize.Width - CueStack.MaxWidth) > 0.5)
            CueStack.MaxWidth = e.NewSize.Width;
    }

    /// <summary>
    /// The cue's title in two lines at most (QA pass 3, 2026-10-15: at 900×560 "YOUR EXTRACTS THIS RAID · 7 OF 12" took
    /// three, and the cue ran under the preview label and the tour's band; owner, confirmed the same day). Where two lines or one
    /// hold it in the map's column, it breaks there as it did; where the column is too narrow for two at its 48 px (the
    /// smallest window), it breaks where two lines take the least width, never inside a word, and CueTitleBox draws it
    /// smaller to the column, never cut, as the roll call's rows are.
    /// </summary>
    private void FitCueTitle(string title)
    {
        var column = (Map.ActualWidth > 0 ? Map.ActualWidth : CueStack.MaxWidth) - CueStack.Padding.Left - CueStack.Padding.Right;
        if (title.Length == 0 || !(column > 0) || double.IsInfinity(column))
        {
            CueTitle.MaxWidth = double.PositiveInfinity;
            return;
        }
        var probe = TextProbe(CueTitle);
        probe.Text = title;
        Windows.Foundation.Size Measured(double width)
        {
            probe.MaxWidth = width;
            probe.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            return probe.DesiredSize;
        }
        var one = Measured(double.PositiveInfinity);
        // Never narrower than its widest word: a word too long for its line would break inside it.
        var widestWord = title.Split(' ', StringSplitOptions.RemoveEmptyEntries).Max(word =>
        {
            probe.Text = word;
            return Measured(double.PositiveInfinity).Width;
        }) + 1;
        probe.Text = title;
        bool TwoLines(double width) => Measured(width).Height <= 2 * one.Height + 1;
        if (widestWord <= column && TwoLines(column))
        {
            CueTitle.MaxWidth = column;
            return;
        }
        // The least width two lines hold it in, to a DIP: between the column (or its widest word) and its one line.
        double narrow = Math.Max(column, widestWord), wide = Math.Ceiling(one.Width) + 1;
        if (TwoLines(narrow))
            wide = narrow;
        while (wide - narrow > 1)
        {
            var middle = (narrow + wide) / 2;
            if (TwoLines(middle))
                wide = middle;
            else
                narrow = middle;
        }
        CueTitle.MaxWidth = Math.Ceiling(wide) + 1;
    }

    // A text block measured off screen as the given one sets its words (FitCueTitle, Decode).
    private static TextBlock TextProbe(TextBlock like) => new()
    {
        FontFamily = like.FontFamily, FontSize = like.FontSize, FontWeight = like.FontWeight, FontStretch = like.FontStretch, FontStyle = like.FontStyle,
        CharacterSpacing = like.CharacterSpacing, TextWrapping = like.TextWrapping, TextAlignment = like.TextAlignment, LineHeight = like.LineHeight,
        LineStackingStrategy = like.LineStackingStrategy, TextLineBounds = like.TextLineBounds,
    };

    // Text decodes like a terminal (the cue's title, the tour's, the screenshot key's file name): undecoded letters
    // flicker through random ones in gold, settling left to right in ink. Each flickers through glyphs no wider than
    // itself, and a frame whose glyphs would still take the words past the box they end in (a pair the font sets closer
    // in the words than in the glyphs) shows the letters themselves in gold: the text never grows while it decodes, so
    // it never leaves the box held for it (QA pass 3, 2026-10-15: the roll call's title, held at its width as it ends,
    // read "YOUR EXTRACTS THIS RAID · 7 OF" and nothing after at 1600×900, its gold glyphs being wider than its letters).
    private DispatcherQueueTimer Decode(TextBlock block, string text, TimeSpan length)
    {
        var glyphs = DecodeGlyphs(block, text);
        var settled = new Microsoft.UI.Xaml.Documents.Run();
        var flicker = new Microsoft.UI.Xaml.Documents.Run { Foreground = Resource("AmberBrush") };
        block.Inlines.Clear();
        block.Inlines.Add(settled);
        block.Inlines.Add(flicker);
        // The box the words end in: the width the block is held at (the roll call's title) or its words' own as they end
        // (its widest line), and their lines.
        var probe = TextProbe(block);
        var probeSettled = new Microsoft.UI.Xaml.Documents.Run();
        var probeFlicker = new Microsoft.UI.Xaml.Documents.Run();
        probe.Inlines.Add(probeSettled);
        probe.Inlines.Add(probeFlicker);
        probe.MaxWidth = double.IsNaN(block.Width) ? block.MaxWidth : block.Width;
        Windows.Foundation.Size Measured(string done, string rest)
        {
            probeSettled.Text = done;
            probeFlicker.Text = rest;
            probe.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            return probe.DesiredSize;
        }
        var end = Measured(text, "");
        var widest = double.IsNaN(block.Width) ? end.Width + 1 : block.Width;
        var started = DateTime.Now;
        var timer = WindowTimer(t =>
        {
            var p = Math.Min(1, (DateTime.Now - started) / length);
            var resolved = (int)Math.Round(p * text.Length);
            var rest = text[resolved..];
            var scrambled = string.Concat(rest.Select(c => glyphs.TryGetValue(c, out var any) ? any[Random.Shared.Next(any.Length)] : c));
            if (scrambled != rest && Measured(text[..resolved], scrambled) is var size && (size.Width > widest + 0.5 || size.Height > end.Height + 0.5))
                scrambled = rest;
            settled.Text = text[..resolved];
            flicker.Text = scrambled;
            if (p >= 1)
                t.Stop();
        });
        timer.Interval = TimeSpan.FromMilliseconds(35);
        timer.Start();
        return timer;
    }

    // The glyphs a letter or digit of the words flickers through: of A–Z and 0–9, those no wider than it in the block's
    // type (itself where none is), measured once per type.
    private static Dictionary<char, char[]> DecodeGlyphs(TextBlock block, string text)
    {
        const string glyphs = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var key = $"{block.FontFamily?.Source}|{block.FontSize}|{block.FontWeight.Weight}|{block.FontStretch}|{block.FontStyle}|{block.CharacterSpacing}";
        if (!GlyphWidths.TryGetValue(key, out var widths))
            GlyphWidths[key] = widths = [];
        var probe = TextProbe(block);
        probe.TextWrapping = TextWrapping.NoWrap;
        double Width(char c)
        {
            if (!widths.TryGetValue(c, out var width))
            {
                // Eight in a row, so a pixel's rounding and the edges' bearings weigh an eighth.
                probe.Text = new string(c, 8);
                probe.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
                widths[c] = width = probe.DesiredSize.Width / 8;
            }
            return width;
        }
        var choices = new Dictionary<char, char[]>();
        foreach (var c in text.Distinct().Where(char.IsLetterOrDigit))
        {
            var room = Width(c) + 0.01;
            var narrower = glyphs.Where(g => Width(g) <= room).ToArray();
            choices[c] = narrower.Length > 0 ? narrower : [c];
        }
        return choices;
    }

    // Each type's glyph widths, in DIPs (DecodeGlyphs).
    private static readonly Dictionary<string, Dictionary<char, double>> GlyphWidths = new(StringComparer.Ordinal);

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

    /// <summary>Set for "--snapshot" runs: the help panel opens to be rendered, and isn't marked as seen; a linked set
    /// stands in its static look, as with the animation effects off.</summary>
    public bool SnapshotMode
    {
        get => _snapshotMode;
        set
        {
            _snapshotMode = value;
            Map.StaticHighlight = value;
        }
    }

    private bool _snapshotMode;

    /// <summary>Snapshots render at this multiple of the screen's pixel density (2 for sharp website images).</summary>
    public int SnapshotScale { get; set; } = 1;

    /// <summary>"--show-quest &lt;part of a name&gt;" (snapshots): picks and highlights that quest and holds its card.</summary>
    public string? ShowQuest { get; set; }

    private void ShowQuestForSnapshot(SessionSnapshot s)
    {
        if (ShowQuest is not { } text || s.Data is null || s.Plan.Count == 0 && s.Objectives.Count == 0)
            return;
        ShowQuest = null;
        // By its name in the data's language, or by tarkov.dev's normalized name, the same in every language ("Dandies",
        // "dandies"): a German run found no "Dandies" and showed no card (review of 2026-10-10).
        var normalized = text.Trim().ToLowerInvariant().Replace(' ', '-');
        var data = s.Data;
        var id = s.Plan.SelectMany(p => p.Finish.Concat(p.Progress)).Select(q => (q.QuestId, q.Name))
            .Concat(s.Objectives.Select(o => (o.QuestId, Name: o.QuestName)))
            .FirstOrDefault(q => q.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                                 || data.Tasks.GetValueOrDefault(q.QuestId)?.NormalizedName?.Contains(normalized, StringComparison.Ordinal) == true).QuestId;
        if (id is null || BuildCard(id) is not { } view)
            return;
        // Let the rail lay out first, so the highlight lands on real rows. Then: the quest's card, and the card of the
        // first thing it needs (nested).
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (_snapshot?.Picks.Contains(id) != true)
                _ = _session.TogglePickAsync(id, "snapshot", save: false);
            Linked.Set(Focus.Quest(id));
            _cards.Open(new CardKey.Quest(id), new Windows.Foundation.Rect(372, 150, 8, 8), 0);
            var item = view.Needs.FirstOrDefault()?.ItemId ?? view.Objectives.FirstOrDefault(o => o.ItemId is not null)?.ItemId;
            if (item is not null)
                _cards.Open(new CardKey.Item(item), new Windows.Foundation.Rect(372, 330, 8, 8), 1);
        });
    }

    // The help panel opens by itself once, the first time the app has something to show. Not in a raid, where it
    // would lie over the raid card until someone clicks it away: then it opens at the next chance, when the raid is
    // over (WhileInRaid). A snapshot run opens it for its picture, whatever the state. Before the window is up it can't
    // open: then it opens with the next snapshot of the session.
    private void ShowHelpOnFirstRun(SessionSnapshot s)
    {
        if (_helpShownOnce || s.Data is null || DemoMode)
            return;
        if (!SnapshotMode && WhileInRaid.Waits(s.Raid.Phase))
            return;
        if (SnapshotMode || _session.GetSetting("help.seen") is null)
        {
            if (!ShowHelp())
                return;
            _helpByItself = true;
            _helpInSnapshot = SnapshotMode;
        }
        _helpShownOnce = true;
    }

    // A snapshot's help, opened for its picture: it is in the picture even if it closed on the way (review of
    // 2026-10-10: help.png was missing from some of the layout check's runs, in every language, where the panel had
    // opened; six runs in a row kept it, and what closed it wasn't found).
    private bool _helpInSnapshot;

    // The help panel is open because it opened by itself (the first start), not because the player asked for it.
    private bool _helpByItself;
    // A raid began under the open help panel and closed it (WhileInRaid.LetsGo).
    private bool _helpClosedForRaid;

    private bool ShowHelp()
    {
        if (HelpButton.XamlRoot is null)
            return false;
        if (Quiet)
            ShowWithoutFocus(HelpFlyout, HelpButton);
        else
            HelpFlyout.ShowAt(HelpButton);
        return true;
    }

    private DateTime _helpOpenedAt;

    private void OnHelpOpened(object sender, object e)
    {
        _helpOpenedAt = DateTime.Now;
        ShowLegend(opened: true);
        Study.Ui("help.open");
    }

    // Settings, apart from help (owner, 2026-10-03): the preferences and the app's data. It never opens by itself.
    private void ShowSettings()
    {
        if (SettingsButton.XamlRoot is not null)
            SettingsFlyout.ShowAt(SettingsButton);
    }

    private DateTime _settingsOpenedAt;

    private void OnSettingsOpened(object sender, object e)
    {
        _settingsOpenedAt = DateTime.Now;
        LoadCrashMode();
        Study.Ui("settings.open");
    }

    private void OnSettingsClosed(object sender, object e)
    {
        Study.Ui("settings.close", ("s", DateTime.Now - _settingsOpenedAt));
        // An unanswered "Remove Shturmap?" doesn't wait for the next visit.
        ViewModel.UninstallAsking = false;
    }

    private Task<bool> OpenSettingsForSnapshotAsync() => OpenForSnapshotAsync(SettingsFlyout, SettingsButton);

    // A flyout opened for a snapshot's picture, once it has opened and laid out its contents.
    private static async Task<bool> OpenForSnapshotAsync(Flyout flyout, FrameworkElement button)
    {
        if (button.XamlRoot is null)
            return false;
        var opened = new TaskCompletionSource();
        void Done(object? sender, object e) => opened.TrySetResult();
        flyout.Opened += Done;
        try
        {
            ShowWithoutFocus(flyout, button);
            if (await Task.WhenAny(opened.Task, Task.Delay(3000)) != opened.Task)
                return false;
            await Task.Delay(300); // one layout pass for its contents
            return true;
        }
        finally
        {
            flyout.Opened -= Done;
        }
    }

    /// <summary>
    /// A flyout opened where its placement puts it, but without moving the focus into it: what a run that takes pictures
    /// opens (help by itself, help and settings for their pictures). The focus on a flyout's first link showed that link's
    /// tooltip, and a tooltip is a window of its own, kept on a monitor: help's TAKE THE TOUR tip stood in the top right
    /// corner of a monitor in use for about twelve seconds of each run, however far off every monitor the window was,
    /// where the person at the PC could click it (release QA pass of 2026-10-19, F3: the tour came over two views' pictures).
    /// </summary>
    private static void ShowWithoutFocus(Flyout flyout, FrameworkElement button) =>
        flyout.ShowAt(button, new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions
        {
            ShowMode = Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowMode.Transient,
            Placement = flyout.Placement,
        });

    /// <summary>The version, its release's name and the kind of this build, at the foot of settings: "Shturmap
    /// 0.4.0+d349909 "Praetorian" · installed" (owner, 2026-10-09: named major releases; Rules.WhatsNew.NameOf).</summary>
    public string VersionText => WhatsNew.NameOf(WhatsNewSections, WhatsNew.VersionOf(GameSession.Version)) is { } name
        ? AppTexts.SettingsVersionNamed(build: BuildKindText(App.BuildKind), name: name, version: GameSession.Version)
        : AppTexts.SettingsVersion(build: BuildKindText(App.BuildKind), version: GameSession.Version);

    private void OnHelpClosed(object sender, object e)
    {
        var forRaid = _helpClosedForRaid;
        var byItself = _helpByItself;
        _helpClosedForRaid = false;
        _helpByItself = false;
        Study.Ui("help.close", ("s", DateTime.Now - _helpOpenedAt), ("how", forRaid ? "raid" : "player"));
        // The first start's help, closed by a raid before the player closed it: not seen yet. It opens again at the
        // next chance, when the raid is over.
        if (forRaid && byItself)
        {
            _helpShownOnce = false;
            return;
        }
        if (!SnapshotMode)
            _session.SetSetting("help.seen", "1");
    }

    // ---- the window's place (owner, 2026-10-04: remember the window's monitor and size; docs/DESIGN.md "Screen
    // anatomy", "The window") ----

    private bool _rememberPlace;
    private bool _sizeGiven;
    private WindowPlace.Rect _unmaximised;
    private DispatcherQueueTimer? _placeSettled;

    /// <summary>
    /// A run that only takes pictures ("--snapshot", "--demo": every run of tools\fake-raid.ps1 and the layout check; a
    /// developer script's, whose view stands beside this window), unless "--visible" asks to watch it: the window opens
    /// off every monitor, is shown without activation and stays out of the taskbar and Alt+Tab, so it never interrupts
    /// a game on the other monitor (owner, 2026-10-15, "quiet local runs": the UI is checked while the owner plays on
    /// the same PC). Its pictures are rendered from the XAML tree, not from the screen (<see cref="SaveSnapshotAsync"/>).
    /// </summary>
    public bool Quiet { get; }

    /// <summary>Shows the window: activated, or in a quiet run without taking the focus from the window that has it.</summary>
    public void Open()
    {
        if (Quiet)
        {
            // The window's x:Bind values, every text in its XAML among them, are set here: the generated code sets them
            // when the window is first activated, and a quiet run's window never is by itself. Help opening activated it
            // (a snapshot run opens help), so the window had its texts; with the tour opened at the start instead, nothing
            // did, and the window and the tour's band showed no bound text for the whole run (QA pass of 2026-10-15:
            // "-Tour 5" had the band without ← BACK and ESC ENDS THE TOUR, the window its frames without a word, and a
            // script's run kept them so after "tour end"). Activate, below, sets them as before.
            Bindings.Initialize();
            AppWindow.Show(activateWindow: false);
        }
        else
            Activate();
    }

    // Where the player left it: on its monitor, at its bounds, maximised or not, as long as that monitor is still
    // there. Otherwise, and at a first start, the rule below; a quiet run off every monitor. A given size (developer
    // runs) is kept as is.
    private void PlaceWindow(SizeInt32? size, string? saved, bool remember)
    {
        _sizeGiven = size is not null;
        _rememberPlace = remember && size is null && !Quiet;
        var parsed = WindowPlace.Parse(saved);
        var last = _rememberPlace ? WindowPlace.Restorable(parsed, Monitors(), ScaleOn(parsed?.Monitor)) : null;
        if (last is { } place)
        {
            AppWindow.MoveAndResize(new RectInt32(place.Bounds.X, place.Bounds.Y, place.Bounds.Width, place.Bounds.Height));
            _unmaximised = place.Bounds;
            if (place.Maximised && AppWindow.Presenter is OverlappedPresenter presenter)
                presenter.Maximize();
        }
        else if (Quiet)
        {
            PlaceOffScreen(size);
        }
        else
        {
            PlaceOnSecondMonitor(size);
        }
        var now = BoundsNow();
        var how = last is not null ? "where it was last" : Quiet
            ? "off every monitor, shown without activation and out of the taskbar and Alt+Tab (a run that takes pictures; --visible shows it)"
            : "placed anew";
        AppLog.Info($"Window {how}: {now.Width}×{now.Height} at {now.X},{now.Y}" +
                    (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized } ? ", maximised" : ""));
        AppWindow.Changed += (_, e) =>
        {
            if (e.DidPositionChange || e.DidSizeChange || e.DidPresenterChange)
                OnPlaceChanged();
        };
        // The smallest size needs the monitor's scale, which the content knows once it is loaded.
        var root = (FrameworkElement)Content;
        root.Loaded += (_, _) =>
        {
            ApplySmallestSize();
            root.XamlRoot.Changed += (_, _) => ApplySmallestSize();
        };
    }

    private static List<WindowPlace.Rect> Monitors()
    {
        var displays = DisplayArea.FindAll();
        var monitors = new List<WindowPlace.Rect>();
        for (var i = 0; i < displays.Count; i++)
        {
            var area = displays[i].OuterBounds;
            monitors.Add(new WindowPlace.Rect(area.X, area.Y, area.Width, area.Height));
        }
        return monitors;
    }

    // The scale of the monitor with these bounds, for the smallest window and the inset on it (WindowPlace counts in
    // pixels; review of 2026-10-09: at 150 % and 200 % they were taken unscaled); 1 where no monitor has them.
    private static double ScaleOn(WindowPlace.Rect? monitor)
    {
        var displays = DisplayArea.FindAll();
        for (var i = 0; i < displays.Count; i++)
        {
            var area = displays[i].OuterBounds;
            if (monitor == new WindowPlace.Rect(area.X, area.Y, area.Width, area.Height))
                return ScaleOf(displays[i]);
        }
        return 1;
    }

    // The scale Windows gives a monitor (1 at 100 %), the one the window's content is drawn at there; 1 where it can't say.
    private static double ScaleOf(DisplayArea display)
    {
        try
        {
            var monitor = Microsoft.UI.Win32Interop.GetMonitorFromDisplayId(display.DisplayId);
            return GetDpiForMonitor(monitor, EffectiveDpi, out var dpi, out _) == 0 && dpi > 0 ? dpi / 96.0 : 1;
        }
        catch (Exception e)
        {
            AppLog.Warn("Window: the monitor's scale couldn't be read; taken as 100 %", e);
            return 1;
        }
    }

    private const int EffectiveDpi = 0; // MDT_EFFECTIVE_DPI

    [System.Runtime.InteropServices.DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

    private WindowPlace.Rect BoundsNow() => new(AppWindow.Position.X, AppWindow.Position.Y, AppWindow.Size.Width, AppWindow.Size.Height);

    /// <summary>
    /// Where cards may go (CardStack), in the window's DIPs: its whole width, and up and down as far as it is on its
    /// monitor's work area (CardPlace.Room; owner, 2026-10-15: "When selecting a quest at the lower border of the screen it
    /// gets cut off"): a window that reaches behind the taskbar or past the screen's edge keeps its cards where they can be
    /// seen. The client area's place on the screen is the window's less its frame, as wide at the foot as at the sides.
    /// </summary>
    private Windows.Foundation.Rect CardRoom()
    {
        var root = (FrameworkElement)Content;
        var room = new CardPlace.Span(0, root.ActualHeight);
        if (!Quiet && root.XamlRoot is { } xaml && AppWindow is { } window)
        {
            var work = DisplayArea.GetFromWindowId(window.Id, DisplayAreaFallback.Nearest).WorkArea;
            var frame = Math.Max(0, (window.Size.Width - window.ClientSize.Width) / 2.0);
            var clientTop = window.Position.Y + window.Size.Height - frame - window.ClientSize.Height;
            room = CardPlace.Room(root.ActualHeight, clientTop, work.Y, work.Y + work.Height, xaml.RasterizationScale);
        }
        return new Windows.Foundation.Rect(0, room.Top, root.ActualWidth, room.Height);
    }

    // Maximised on the second monitor, or on the only one at 1600×1000 at its scale and within its work area
    // (WindowPlace.First, which is also where the maximised window goes back to); a given size (developer runs) is
    // kept as is.
    private void PlaceOnSecondMonitor(SizeInt32? size)
    {
        var target = OpeningMonitor(out var second);
        var work = target.WorkArea;
        var first = WindowPlace.First(new WindowPlace.Rect(work.X, work.Y, work.Width, work.Height), ScaleOf(target));
        AppWindow.MoveAndResize(size is { } fixedSize
            ? new RectInt32(first.X, first.Y, fixedSize.Width, fixedSize.Height)
            : new RectInt32(first.X, first.Y, first.Width, first.Height));
        _unmaximised = BoundsNow();
        if (second && size is null && AppWindow.Presenter is OverlappedPresenter presenter)
            presenter.Maximize();
    }

    // The monitor a window opens on without a saved place: the first that isn't the primary one (the game's), else the
    // primary.
    private static DisplayArea OpeningMonitor(out bool second)
    {
        var displays = DisplayArea.FindAll();
        for (var i = 0; i < displays.Count; i++)
        {
            if (displays[i].DisplayId.Value != DisplayArea.Primary.DisplayId.Value)
            {
                second = true;
                return displays[i];
            }
        }
        second = false;
        return DisplayArea.Primary;
    }

    // A quiet run (owner, 2026-10-15): off every monitor (WindowPlace.OffScreen), never maximised, at the size it would
    // open at: the second monitor's work area, where it would be maximised, the first start's size on the only monitor,
    // or a given size, each in pixels at the scale of the monitor it would open on. Off every monitor it takes the scale
    // of the one it stands beside, so the size is converted to that one's: the content is laid out as it would be on
    // the monitor it used to open on (the layout check's sizes are counted there).
    private void PlaceOffScreen(SizeInt32? size)
    {
        var target = OpeningMonitor(out var second);
        var work = target.WorkArea;
        var scale = ScaleOf(target);
        var first = WindowPlace.First(new WindowPlace.Rect(work.X, work.Y, work.Width, work.Height), scale);
        var (width, height) = size is { } given ? (given.Width, given.Height) : second ? (work.Width, work.Height) : (first.Width, first.Height);
        var monitors = Monitors();
        var ratio = ScaleOn(WindowPlace.RightMost(monitors)) / scale;
        var place = WindowPlace.OffScreen(monitors, (int)Math.Round(width * ratio), (int)Math.Round(height * ratio));
        AppWindow.IsShownInSwitchers = false;
        AppWindow.MoveAndResize(new RectInt32(place.X, place.Y, place.Width, place.Height));
        _unmaximised = BoundsNow();
    }

    // The window moved, was resized, maximised or put back: its bounds while it isn't maximised are what it goes back
    // to, and its place is saved once the change has settled (a drag is a stream of changes).
    private void OnPlaceChanged()
    {
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Restored })
            _unmaximised = BoundsNow();
        if (!_rememberPlace)
            return;
        if (_placeSettled is null)
        {
            _placeSettled = WindowTimer(SavePlace);
            _placeSettled.Interval = TimeSpan.FromMilliseconds(600);
            _placeSettled.IsRepeating = false;
        }
        _placeSettled.Stop();
        _placeSettled.Start();
    }

    private void SavePlace()
    {
        // Minimised, the window has no place of its own: what was saved before stands.
        if (!_rememberPlace || AppWindow.Presenter is not OverlappedPresenter presenter || presenter.State == OverlappedPresenterState.Minimized)
            return;
        var display = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
        var area = display.OuterBounds;
        var monitor = new WindowPlace.Rect(area.X, area.Y, area.Width, area.Height);
        var maximised = presenter.State == OverlappedPresenterState.Maximized;
        // A maximised window sent to another monitor still has its unmaximised bounds on the one before: they follow it.
        var bounds = maximised ? WindowPlace.OnMonitor(_unmaximised, monitor, ScaleOf(display)) : _unmaximised;
        _session.SetSetting(WindowPlace.Setting, WindowPlace.Format(new WindowPlace.Saved(bounds, maximised, monitor)));
    }

    // No smaller than the status bar needs to keep its three buttons in view, with the last fix trimmed, and the rail
    // its glance, at this monitor's scale (the presenter counts in pixels) and the text size (WindowPlace.Smallest; owner,
    // 2026-10-19: the minimum grows with the text size), never more than the monitor's work area holds. A window
    // smaller than that (a larger text size just chosen, a place saved before) grows to it where it stands. A size
    // given by a developer run is left as it is.
    private void ApplySmallestSize()
    {
        if (_sizeGiven || AppWindow.Presenter is not OverlappedPresenter presenter || Content.XamlRoot is not { } xaml)
            return;
        var (width, height) = WindowPlace.Smallest(Controls.TextScale.Effective);
        var work = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        var least = new SizeInt32(
            Math.Min((int)Math.Ceiling(width * xaml.RasterizationScale), Math.Max(1, work.Width)),
            Math.Min((int)Math.Ceiling(height * xaml.RasterizationScale), Math.Max(1, work.Height)));
        if (presenter.PreferredMinimumWidth != least.Width || presenter.PreferredMinimumHeight != least.Height)
            AppLog.Info(string.Create(CultureInfo.InvariantCulture,
                $"Smallest window: {width}×{height} at text ×{Controls.TextScale.Effective:0.###}, {least.Width}×{least.Height} px"));
        presenter.PreferredMinimumWidth = least.Width;
        presenter.PreferredMinimumHeight = least.Height;
        if (presenter.State == OverlappedPresenterState.Restored && (AppWindow.Size.Width < least.Width || AppWindow.Size.Height < least.Height))
            AppWindow.Resize(new SizeInt32(Math.Max(AppWindow.Size.Width, least.Width), Math.Max(AppWindow.Size.Height, least.Height)));
    }

    // ---- keyboard (only while this window has focus; Shturmap registers no global hotkeys) ----

    // A text box has the keyboard: a held quest card's note field (owner, 2026-10-15). Its keys are text, so none of
    // the window's shortcuts fires, Esc included (the field clears itself), and typing a note never moves the map or
    // changes the picks. Asked of the window's XAML root: the cards lie in popups, which share it.
    private bool TypingText => Content?.XamlRoot is { } xamlRoot && FocusManager.GetFocusedElement(xamlRoot) is TextBox;

    private void AddShortcuts(UIElement root)
    {
        // Accelerators on the root would otherwise show their key ("F") as a tooltip over the whole window.
        root.KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;
        void AddKey(Windows.System.VirtualKey key, bool shift, Action action)
        {
            var accelerator = new KeyboardAccelerator { Key = key };
            if (shift)
                accelerator.Modifiers = Windows.System.VirtualKeyModifiers.Shift;
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
                // In a note's field keys are text: unhandled, they go on to it.
                if (TypingText)
                    return;
                // While the tour is up, its keys step through it, Esc ends it, and the rest do nothing.
                if (TourOpen)
                {
                    e.Handled = TourKey(key);
                    return;
                }
                // While the stash desk is up the map, out of sight, takes no keys; PgUp and PgDn scroll the desk's page.
                if (_deskChosen && DeskSwitch.MapKey((int)key))
                {
                    if (key is Windows.System.VirtualKey.PageUp or Windows.System.VirtualKey.PageDown)
                        StashDesk.PageBy(key == Windows.System.VirtualKey.PageDown ? 1 : -1);
                    e.Handled = true;
                    return;
                }
                Study.Ui("key", ("key", shift ? "Shift+" + key : key.ToString()));
                action();
                e.Handled = true;
            };
            root.KeyboardAccelerators.Add(accelerator);
        }
        void Add(Windows.System.VirtualKey key, Action action) => AddKey(key, false, action);

        // The tour's own keys (MainWindow.Tour): →, Space or Enter next, ← back. Only while it is up; nothing else uses
        // them, so with the tour closed they go on to whatever has the focus (a button's Enter). Up and Down do nothing
        // while it is up: the MAP list, where the window's focus rests at the start, would switch the map under it.
        foreach (var tourKey in new[]
        {
            Windows.System.VirtualKey.Right, Windows.System.VirtualKey.Left, Windows.System.VirtualKey.Space, Windows.System.VirtualKey.Enter,
            Windows.System.VirtualKey.Up, Windows.System.VirtualKey.Down,
        })
        {
            var accelerator = new KeyboardAccelerator { Key = tourKey };
            accelerator.Invoked += (_, e) => e.Handled = TourOpen && !ReportOpen && !TypingText && TourKey(tourKey);
            root.KeyboardAccelerators.Add(accelerator);
        }

        Add(Windows.System.VirtualKey.F, ShowMe);
        // "+" is Shift and the "=" key on a US keyboard, a key of its own on others (ZoomKeys).
        foreach (var (key, shift) in ZoomKeys.In)
            AddKey((Windows.System.VirtualKey)key, shift, () => ZoomBy(1.5, "key"));
        foreach (var (key, shift) in ZoomKeys.Out)
            AddKey((Windows.System.VirtualKey)key, shift, () => ZoomBy(1 / 1.5, "key"));
        Add(Windows.System.VirtualKey.Number0, () => OnFitClick(this, new RoutedEventArgs()));
        Add(Windows.System.VirtualKey.NumberPad0, () => OnFitClick(this, new RoutedEventArgs()));
        // Esc closes the cards. It leaves the picks alone: they are the plan for the coming raids, and a key press that
        // throws away a plan would be too easy to hit (owner, 2026-10-03; CLEAR PICKS is the deliberate way). With no
        // card open it lets go of a trader's held focus (TraderFocus.Escape; owner, 2026-10-17).
        Add(Windows.System.VirtualKey.Escape, OnEscape);
        Add(Windows.System.VirtualKey.PageUp, () => PickFloor(_shownFloor - 1, "key"));
        Add(Windows.System.VirtualKey.PageDown, () => PickFloor(_shownFloor + 1, "key"));
        Add(Windows.System.VirtualKey.F1, () => ShowHelp());
        // Ctrl+, opens settings, as in many Windows apps (the gear beside "?").
        var settingsKey = new KeyboardAccelerator { Key = (Windows.System.VirtualKey)188, Modifiers = Windows.System.VirtualKeyModifiers.Control };
        settingsKey.Invoked += (_, e) =>
        {
            if (ReportOpen || TourOpen || TypingText)
                return;
            Study.Ui("key", ("key", "Ctrl+Comma"));
            ShowSettings();
            e.Handled = true;
        };
        root.KeyboardAccelerators.Add(settingsKey);
        // Shift+F turns Follow my position on and off (F alone shows the position once).
        var followKey = new KeyboardAccelerator { Key = Windows.System.VirtualKey.F, Modifiers = Windows.System.VirtualKeyModifiers.Shift };
        followKey.Invoked += (_, e) =>
        {
            if (ReportOpen || TourOpen || TypingText || _deskChosen)
                return;
            Study.Ui("key", ("key", "Shift+F"));
            SetFollow(!Map.Follow, "key");
            e.Handled = true;
        };
        root.KeyboardAccelerators.Add(followKey);
#if DEVTOOLS
        AddDevShortcuts(Add);
#endif
        root.CharacterReceived += (_, e) =>
        {
            if (e.Character == '?' && !ReportOpen && !TourOpen && !TypingText)
            {
                ShowHelp();
                e.Handled = true;
            }
        };
    }

    // ---- UI events ----

    private async void OnMapPicked(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingPicker)
            return;
        // OTHER MAPS is a heading, not a map: its item can't be chosen (MapChoiceStyles), and if it ever is, the list
        // goes back to the map on screen.
        if (ViewModel.SelectedMap is { IsHeader: true })
        {
            _updatingPicker = true;
            ViewModel.SelectedMap = MapList.For(ViewModel.MapChoices, _snapshot?.Map?.NormalizedName);
            _updatingPicker = false;
            return;
        }
        // A variant on screen (Night Factory) is its map's entry: choosing that entry again changes nothing.
        if (ViewModel.SelectedMap is not { } choice || choice.Stands(_snapshot?.Map?.NormalizedName))
            return;
        Study.Ui("map.pick", ("to", choice.NormalizedName), ("how", "picker"));
        // A map picked is a map to look at: the desk gives Plan its place back.
        LeaveDesk("map");
        await _session.SelectMapAsync(choice.NormalizedName);
    }

    // A map's row in Plan's list. The open map's card is no button: a click in it is a click on what is under the
    // pointer, a quest's row or its pen, and never also a change of map (PlanList).
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

    // ---- follow my position (owner, 2026-10-03; docs/DESIGN.md "Follow my position") ----

    /// <summary>"--follow" (snapshots, the dev view): Follow my position on from the start of this run.</summary>
    public bool FollowOnStart { get; set; }

    private bool _followLoaded;

    // It comes back as the player left it (off at first); snapshot and demo runs leave the saved choice alone.
    private void LoadFollowOnce()
    {
        if (_followLoaded)
            return;
        _followLoaded = true;
        var on = FollowOnStart || !SnapshotMode && !DemoMode && MapFollow.Parse(_session.GetSetting(MapFollow.Setting));
        FollowButton.IsChecked = on;
        Map.SetFollow(on);
    }

    private void OnFollowClick(object sender, RoutedEventArgs e) => SetFollow(FollowButton.IsChecked == true, "button");

    private void SetFollow(bool on, string how)
    {
        FollowButton.IsChecked = on;
        Map.SetFollow(on);
        SaveFollow(on);
        Study.Ui("map.follow", ("on", on), ("how", how));
    }

    private void SaveFollow(bool on)
    {
        if (!SnapshotMode && !DemoMode)
            _session.SetSetting(MapFollow.Setting, MapFollow.Format(on));
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
        // The raid card's head stays while the rest scrolls under it: once anything has gone under, the head closes at
        // its foot; unscrolled it stays open, so head and body read as one card.
        RaidHead.BorderThickness = RailScroll.VerticalOffset > 0.5 ? new Thickness(1) : new Thickness(1, 1, 1, 0);
        if (!e.IsIntermediate && sender is ScrollViewer scroll)
            Study.Ui("rail.scroll", ("y", scroll.VerticalOffset), ("of", scroll.ScrollableHeight));
    }

    // The EXIT row's "ALL n ↓": the list of every way out, under the raid card, comes to the top of the rail.
    private void OnAllExitsClick(object sender, RoutedEventArgs e)
    {
        ExtractsHeading.StartBringIntoView(new BringIntoViewOptions { VerticalAlignmentRatio = 0, AnimationDesired = true });
        Study.Ui("rail.exits");
    }

    // ---- developer aid ----

    /// <summary>Renders the window's controls, the help panel and the map to PNG files in a folder ("--snapshot"), with
    /// the text Copy diagnostics would copy.</summary>
    public async Task SaveSnapshotAsync(string folder)
    {
        // In the language in use: the current culture travels with async work, and a snapshot's began with the app,
        // before a switch ("--switch-language"; the layout check took the German switch for none, its culture still
        // English, review of 2026-10-10). What the window shows was right: it formats with UiLanguage.Culture.
        UiLanguage.ApplyHere();
        Directory.CreateDirectory(folder);
        try
        {
            // The tour over the window goes apart, with its transparency (tour.png): the window's own picture shows the map's
            // panel empty, to be filled with map.png, and a dim over it would read as empty too.
            if (TourOpen)
            {
                TourLayer.Visibility = Visibility.Collapsed;
                await Task.Delay(100);
                await RenderToPngAsync((UIElement)Content, Path.Combine(folder, "window.png"));
                TourLayer.Visibility = Visibility.Visible;
                await Task.Delay(100);
                await RenderToPngAsync(TourLayer, Path.Combine(folder, "tour.png"));
            }
            else
                await RenderToPngAsync((UIElement)Content, Path.Combine(folder, "window.png"));
            if (_helpInSnapshot && !TourOpen && !HelpFlyout.IsOpen)
                await OpenForSnapshotAsync(HelpFlyout, HelpButton);
            if (HelpFlyout.IsOpen && HelpFlyout.Content is UIElement help)
                await RenderToPngAsync(help, Path.Combine(folder, "help.png"), popup: true);
            // Settings never opens by itself, so a snapshot opens it just for its picture, help closed first: opened while
            // help closed under it, settings was laid out but drawn as nothing, every pixel of settings.png transparent
            // (UI QA re-check of 94591a5, F4).
            if (SnapshotMode && HelpFlyout.IsOpen)
            {
                HelpFlyout.Hide();
                await Task.Delay(300);
            }
            if (SnapshotMode && SettingsFlyout.Content is UIElement settings && await OpenSettingsForSnapshotAsync())
            {
                await RenderToPngAsync(settings, Path.Combine(folder, "settings.png"), popup: true);
                SettingsFlyout.Hide();
            }
            var cards = _cards.Cards;
            for (var i = 0; i < cards.Count; i++)
            {
                var name = i == 0 ? "card.png" : $"card-{i + 1}.png";
                await RenderToPngAsync(cards[i], Path.Combine(folder, name));
                // Where it stands in the window, to put it over window.png: the picture is the whole card, the part a
                // window too low for it would cut too (owner, 2026-10-15: a card from a row at the lower border).
                var at = cards[i].TransformToVisual((UIElement)Content).TransformPoint(new Windows.Foundation.Point(0, 0));
                AppLog.Debug(FormattableString.Invariant(
                    $"Snapshot: {name} at {at.X:0},{at.Y:0} DIP, {cards[i].ActualWidth:0}×{cards[i].ActualHeight:0}, in a window of {((FrameworkElement)Content).ActualWidth:0}×{((FrameworkElement)Content).ActualHeight:0}"));
            }
            // A tile render (The Lab, Labyrinth, Icebreaker) loads its tiles for this view first.
            await Map.TilesLoadedAsync(TimeSpan.FromSeconds(20));
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

    // A popup's picture (help, settings) drawn with no pixel at all is taken again, up to five times half a second apart:
    // a popup is drawn on transparency, so an empty picture of one is a render taken before it was drawn, never the popup.
    private async Task RenderToPngAsync(UIElement element, string path, bool popup = false)
    {
        // A larger text size: a popup opened just for its picture takes it too, and the fits settle (Controls.TextScale).
        if (Controls.TextScale.Own != 1)
        {
            Controls.TextScale.Now();
            Controls.TextScale.Probe((FrameworkElement)element);
            element.UpdateLayout();
            await Task.Delay(300);
        }
        var scale = Content.XamlRoot?.RasterizationScale ?? 1;
        var (rtb, pixels) = await RenderAsync();
        for (var again = 1; popup && again <= 5 && !Drawn(pixels); again++)
        {
            await Task.Delay(500);
            (rtb, pixels) = await RenderAsync();
            AppLog.Info($"Snapshot: {Path.GetFileName(path)} drawn empty, taken again ({again} of 5){(Drawn(pixels) ? ", drawn" : "")}");
        }

        static bool Drawn(Windows.Storage.Streams.IBuffer buffer)
        {
            var bytes = System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.ToArray(buffer);
            for (var i = 3; i < bytes.Length; i += 4)
            {
                if (bytes[i] != 0)
                    return true;
            }
            return false;
        }

        async Task<(Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap, Windows.Storage.Streams.IBuffer)> RenderAsync()
        {
            var rtb = new Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap();
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
            return (rtb, await rtb.GetPixelsAsync());
        }
        var storageFolder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(path));
        var file = await storageFolder.CreateFileAsync(Path.GetFileName(path), Windows.Storage.CreationCollisionOption.ReplaceExisting);
        using var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);
        var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);
        var dpi = 96 * scale * SnapshotScale;
        encoder.SetPixelData(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
            (uint)rtb.PixelWidth, (uint)rtb.PixelHeight, dpi, dpi, System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.ToArray(pixels));
        await encoder.FlushAsync();
#if DEVTOOLS
        // The layout check: the picture's texts and what a longer language breaks in it, beside it (Dev/LayoutCheck.cs).
        await CheckLayoutAsync(element, path, rtb);
#endif
    }
}
