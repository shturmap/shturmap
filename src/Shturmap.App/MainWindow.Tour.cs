using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Shturmap.App.Controls;
using Shturmap.App.Rules;
using Shturmap.Core;
using Shturmap.Data.TarkovDev;
using Shturmap.Map;
using Shturmap.Session;
using Path = Microsoft.UI.Xaml.Shapes.Path;
using Point = Windows.Foundation.Point;
using Rect = Windows.Foundation.Rect;

namespace Shturmap.App;

// The tour (owner, 2026-10-09: "a proper first-time-opening tour of the app ... later accessible through the help menu";
// from the panel, the briefing (A) told through an example raid (B), with first-time lines (E); docs/DESIGN.md §4,
// "Screen anatomy", *The tour*). Seven chapters from docs/tour.md over the real window: the rest dims, the parts a chapter
// is about are cut out of the dim and framed with the map sheet's corner marks, which glide from part to part; the
// chapter's title decodes in a band at the map's foot as the cue's does; and an example is staged on the map by the app's
// own drawing, from the cache, as What's New's previews are: nothing is bundled (§3), and nothing of the player's
// changes. It starts by itself at a first start, in help's place, outside a raid; a raid closes it and it comes back
// after, at its chapter; help brings it back. With Windows' animation effects off, and in snapshots, each chapter shows
// its end at once.
public sealed partial class MainWindow
{
    private const string TourPreviewPrefix = "tour:";
    private const string FirstRaidSetting = "firstRaid";
    private const string FirstFixSetting = "firstFix";
    private IReadOnlyList<Tour.Chapter>? _tourChapters;
    private int _tourAt = -1;
    // A raid closed the tour at this chapter: it comes back there once the raid is over.
    private int? _tourResume;
    private bool _tourChecked;
    private bool _tourMotion;
    // Counts the stages shown, and the cut-outs set: a stage's later steps stop once another has begun.
    private int _tourRun;
    private int _tourHolesRun;
    private List<Rect> _tourHoles = [];
    // The cut-outs as drawn this moment, mid-glide too: the next move starts from here.
    private List<Rect> _tourDrawn = [];
    private Func<List<Rect>>? _tourHolesNow;
    private bool _tourCue;
    private DispatcherQueueTimer? _tourTitleTimer;
    private Path? _tourPointer;
    private Point _tourPointerAt;
    private TourExampleSet? _tourExample;
    // The view of the player's own map when the tour opened: every map the tour stages gives it back as it was. Kept
    // apart from the previews' own, which a stage beginning while the last one's map is still coming back would take
    // from the staged view.
    private (Shturmap.Core.Maps.MapPoint Center, double Zoom)? _tourRestore;

    /// <summary>"--tour &lt;n&gt;" (snapshots): opens the tour at its n-th chapter, at the chapter's end.</summary>
    public int? TourOnStart { get; set; }

    private bool TourOpen => _tourAt >= 0;

    private IReadOnlyList<Tour.Chapter> TourChapters => _tourChapters ??= LoadTour();

    private static IReadOnlyList<Tour.Chapter> LoadTour()
    {
        using var stream = typeof(MainWindow).Assembly.GetManifestResourceStream("tour.md");
        if (stream is null)
            return [];
        using var reader = new StreamReader(stream);
        return Tour.Parse(reader.ReadToEnd()).Where(c => Tour.Stages.Contains(c.Stage)).ToList();
    }

    // Once the app has its data: at a first start the tour, in help's place, outside a raid; one a raid closed comes back
    // after it, at its chapter; "--tour <n>" opens it for a snapshot. Never in the website demo.
    private void ShowTourWhenDue(SessionSnapshot s)
    {
        if (s.Data is null || DemoMode || TourOpen)
            return;
        if (TourOnStart is { } n)
        {
            TourOnStart = null;
            _tourChecked = _helpShownOnce = true;
            OpenTour(n - 1, "snapshot");
            return;
        }
        if (SnapshotMode || WhileInRaid.Waits(s.Raid.Phase))
            return;
        if (_tourResume is { } at)
        {
            _tourResume = null;
            OpenTour(at, "resume");
            return;
        }
        if (_tourChecked)
            return;
        _tourChecked = true;
        if (!Tour.StartsByItself(_session.GetSetting(Tour.SeenSetting), _session.GetSetting("help.seen")))
            return;
        // A first start: the tour takes help's place, and the first raid and the first position each get their line.
        _helpShownOnce = true;
        _session.SetSetting(FirstRaidSetting, "due");
        _session.SetSetting(FirstFixSetting, "due");
        OpenTour(0, "start");
    }

    private void OpenTour(int at, string how)
    {
        if (TourChapters.Count == 0 || TourLayer.XamlRoot is null || ReportOpen)
            return;
        // Never in a raid (review of 2026-10-09: help's links and What's New opened it there): it covers the window. TAKE
        // THE TOUR is hidden then; a SHOW ME in help's text, or a What's New line, says why nothing opens.
        if (ViewModel.RaidHoldsBack)
        {
            if (how is "showme" or "whatsnew" or "help")
                ShowNotice("The tour waits until the raid is over.");
            return;
        }
        at = Math.Clamp(at, 0, TourChapters.Count - 1);
        if (HelpFlyout.IsOpen)
            HelpFlyout.Hide();
        if (SettingsFlyout.IsOpen)
            SettingsFlyout.Hide();
        _cards.CloseAll();
        _tourMotion = !SnapshotMode && new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
        if (!TourOpen)
        {
            Study.Ui("tour.open", ("how", how), ("chapter", at + 1));
            // Null where no map was drawn yet (a first start): the map then comes back fitted, not at the camera's default.
            _tourRestore = _previewing is not null ? _restoreView : Map.HasView ? Map.View : null;
            TourLayer.Visibility = Visibility.Visible;
            _tourHoles = [];
            _tourDrawn = [];
            TourLayer.Opacity = _tourMotion ? 0 : 1;
            if (_tourMotion)
                _ = Animate(350, t => TourLayer.Opacity = Math.Max(TourLayer.Opacity, t));
            TourLayer.UpdateLayout();
        }
        ShowChapter(at);
        PlaceTour();
    }

    /// <summary>Closes the tour: "done" (its last chapter), "esc", "end" (its link), or "raid" (a raid began: it comes back).</summary>
    private void CloseTour(string how)
    {
        if (!TourOpen)
            return;
        var at = _tourAt;
        _tourRun++;
        _tourHolesRun++;
        EndTourStage();
        _tourAt = -1;
        _tourHoles = [];
        _tourDrawn = [];
        _tourHolesNow = null;
        _tourTitleTimer?.Stop();
        Study.Ui("tour.close", ("how", how), ("chapter", at + 1));
        if (how == "raid")
        {
            _tourResume = at;
            TourLayer.Visibility = Visibility.Collapsed;
            return;
        }
        if (!SnapshotMode)
        {
            // Seen, and so is help: the tour took its place.
            _session.SetSetting(Tour.SeenSetting, GameSession.Version);
            _session.SetSetting("help.seen", "1");
        }
        if (_tourMotion)
            _ = FadeTourOut();
        else
            TourLayer.Visibility = Visibility.Collapsed;
        ShowNotice("The tour stays in help: F1, then TAKE THE TOUR.", TimeSpan.FromSeconds(6));
    }

    private async Task FadeTourOut()
    {
        await Animate(300, t => TourLayer.Opacity = 1 - t);
        if (!TourOpen)
            TourLayer.Visibility = Visibility.Collapsed;
    }

    private void TourStep(bool forward)
    {
        if (!TourOpen)
            return;
        if (Tour.Step(_tourAt, forward, TourChapters.Count) is not { } next)
            CloseTour("done");
        else if (next != _tourAt)
            ShowChapter(next);
    }

    /// <summary>A key while the tour is up: → Space Enter next, ← back, Esc ends it; every other key does nothing then.</summary>
    private bool TourKey(Windows.System.VirtualKey key)
    {
        switch (key)
        {
            case Windows.System.VirtualKey.Right or Windows.System.VirtualKey.Space or Windows.System.VirtualKey.Enter:
                Study.Ui("tour.key", ("key", key.ToString()));
                TourStep(forward: true);
                break;
            case Windows.System.VirtualKey.Left:
                Study.Ui("tour.key", ("key", key.ToString()));
                TourStep(forward: false);
                break;
            case Windows.System.VirtualKey.Escape:
                CloseTour("esc");
                break;
        }
        return true;
    }

    private void OnTourNextClick(object sender, RoutedEventArgs e) => TourStep(forward: true);

    private void OnTourBackClick(object sender, RoutedEventArgs e) => TourStep(forward: false);

    private void OnTourEndClick(object sender, RoutedEventArgs e) => CloseTour("end");

    // Help's TAKE THE TOUR: from its start.
    private void OnTakeTourClick(object sender, RoutedEventArgs e) => OpenTour(0, "help");

    // Help's SHOW ME beside a paragraph: the chapter that shows it (its stage, in the hyperlink's AutomationId).
    private void OnTourShowMeClick(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs e)
    {
        var stage = Microsoft.UI.Xaml.Automation.AutomationProperties.GetAutomationId(sender);
        var at = TourChapters.ToList().FindIndex(c => c.Stage == stage);
        OpenTour(Math.Max(0, at), "showme");
    }

    // ---- one chapter ----

    private void ShowChapter(int at)
    {
        var run = ++_tourRun;
        EndTourStage();
        _tourAt = at;
        var chapter = TourChapters[at];
        var count = TourChapters.Count;
        TourEyebrow.Text = Tour.Eyebrow(at, count);
        TourText.Text = string.Join("\n", chapter.Lines);
        TourNextText.Text = at + 1 < count ? "NEXT →" : "DONE";
        TourBack.Visibility = at > 0 ? Visibility.Visible : Visibility.Collapsed;
        TourTicks.ColumnDefinitions.Clear();
        TourTicks.Children.Clear();
        for (var i = 0; i < count; i++)
        {
            TourTicks.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var tick = new Rectangle { Height = 4, Fill = Resource(i <= at ? "AmberBrush" : "LineStrongBrush"), Opacity = i < at ? 0.45 : 1 };
            Grid.SetColumn(tick, i);
            TourTicks.Children.Add(tick);
        }
        _tourTitleTimer?.Stop();
        if (_tourMotion)
        {
            _tourTitleTimer = Decode(TourTitle, chapter.Title, TimeSpan.FromMilliseconds(700));
            // The band's gold rule shoots out from the middle with a slight overshoot, as the cue's rules do.
            _ = Animate(650, t =>
            {
                if (run == _tourRun)
                    TourRuleScale.ScaleX = Spring(t);
            });
        }
        else
        {
            TourTitle.Text = chapter.Title;
            TourRuleScale.ScaleX = 1;
        }
        Study.Ui("tour.chapter", ("n", at + 1), ("stage", chapter.Stage));
        _ = StageAsync(chapter, run);
    }

    // Ease out with a slight overshoot, settling at 1.
    private static double Spring(double t)
    {
        const double c1 = 1.2, c3 = c1 + 1;
        return 1 + c3 * Math.Pow(t - 1, 3) + c1 * Math.Pow(t - 1, 2);
    }

    // What a stage put up goes: its plate, its pointer and marks, its cue, and the map it staged (the map comes back as
    // it was, view and all).
    private void EndTourStage()
    {
        TourCanvas.Children.Clear();
        _tourPointer = null;
        TourPlate.Child = null;
        TourPlate.Visibility = Visibility.Collapsed;
        if (_tourCue)
        {
            _tourCue = false;
            _cueStory?.Stop();
            _cueTimer?.Stop();
            CuePanel.Visibility = Visibility.Collapsed;
        }
        if (_previewing is not null)
        {
            _previewTimer?.Stop();
            _restoreView = _tourRestore;
            EndPreview(restore: true);
        }
    }

    private async Task StageAsync(Tour.Chapter chapter, int run)
    {
        try
        {
            switch (chapter.Stage)
            {
                case "safe":
                    await StageSafe(chapter, run);
                    break;
                case "follows":
                    await StageFollows(chapter, run);
                    break;
                case "next":
                    await StageNext(chapter, run);
                    break;
                case "pick":
                    await StagePick(chapter, run);
                    break;
                case "key":
                    await StageKey(chapter, run);
                    break;
                case "raid":
                    await StageRaid(chapter, run);
                    break;
                case "know":
                    await StageKnow(chapter, run);
                    break;
            }
        }
        catch (Exception e)
        {
            AppLog.Error($"Tour: the stage '{chapter.Stage}' failed", e);
        }
    }

    // A stage's pause: none without motion (the chapter's end shows at once); false once another stage has begun.
    private async Task<bool> TourWait(int milliseconds, int run)
    {
        if (_tourMotion && milliseconds > 0)
            await Task.Delay(milliseconds);
        return run == _tourRun && TourOpen;
    }

    // ---- the stages ----

    // What Shturmap reads and what it never does, one line after another (the tour's words; SafetyTests holds the code to
    // the second list).
    private async Task StageSafe(Tour.Chapter chapter, int run)
    {
        TourHoles(() => []);
        var columns = new Grid { ColumnSpacing = 64 };
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var lines = new List<UIElement>();
        foreach (var (key, column) in new[] { ("reads", 0), ("never", 1) })
        {
            var stack = new StackPanel { Spacing = 10 };
            stack.Children.Add(TourEyebrowText(key.ToUpperInvariant(), key == "reads" ? "AmberBrush" : "MutedBrush"));
            foreach (var word in chapter.Words.Where(w => w.Key == key))
            {
                var line = new TextBlock { Text = word.Text, Style = TextStyle("TitleText"), TextWrapping = TextWrapping.NoWrap };
                stack.Children.Add(line);
                lines.Add(line);
            }
            Grid.SetColumn(stack, column);
            columns.Children.Add(stack);
        }
        ShowPlate(columns);
        await Reveal(lines, 160, run);
    }

    // Plan and Raid: the status bar's raid word and the rail framed; the two states, one after the other.
    private async Task StageFollows(Tour.Chapter chapter, int run)
    {
        TourHoles(() => Anchors(chapter));
        var rows = new StackPanel { Spacing = 18 };
        var lines = new List<UIElement>();
        foreach (var word in chapter.Words)
        {
            var row = new StackPanel { Spacing = 5 };
            row.Children.Add(new TextBlock
            {
                Text = word.Key, Style = TextStyle("StatusText"), FontSize = 14,
                Foreground = Resource(word.Key.StartsWith("IN RAID", StringComparison.Ordinal) ? "AmberBrush" : "InkBrush"),
            });
            row.Children.Add(new TextBlock { Text = word.Text, Style = TextStyle("TitleText") });
            rows.Children.Add(row);
            lines.Add(row);
        }
        ShowPlate(rows);
        await Reveal(lines, 700, run);
    }

    // NEXT RAID's rows framed; a drawn pointer rests on one, and its map previews as it does for the player: the
    // player's own rows, nothing staged (rows that can't be shown, with no game, frame the MAP list instead).
    private async Task StageNext(Tour.Chapter chapter, int run)
    {
        TourHoles(() => Anchors(chapter));
        var current = _snapshot?.Map?.NormalizedName;
        var rows = PlanRows.Visibility == Visibility.Visible ? MapRows(PlanRows).Where(r => r.ActualHeight > 0).ToList() : [];
        var row = rows.FirstOrDefault(r => r.Tag is string m && m != current) ?? rows.FirstOrDefault();
        if (row is null || !await TourWait(350, run))
            return;
        var map = MapRect();
        TourPointer(new Point(map.X + map.Width * 0.4, map.Y + map.Height * 0.55));
        await TourPointerTo(row.TransformToVisual(TourLayer).TransformPoint(new Point(row.ActualWidth * 0.7, row.ActualHeight * 0.45)), 950, run);
        if (!await TourWait(250, run))
            return;
        TourTint(row);
        if (row.Tag is string name && name != current)
        {
            StartPreview(name);
            if (_previewing is not null)
                _restoreView = _tourRestore;
        }
    }

    // An example on Customs: one quest picked (its pick colour, holding still), then another pointed at (lit, pulsing).
    private async Task StagePick(Tour.Chapter chapter, int run)
    {
        TourHoles(() => Anchors(chapter));
        if (_snapshot?.Data is not { } data || TourExample(data) is not { } example)
            return;
        MapContent? content = null;
        var first = example.QuestIds[0];
        var second = example.QuestIds.Count > 1 ? example.QuestIds[1] : first;
        // Framed on the two quests the chapter is about; the third stays on the map as one more quest.
        await TourScene(data, example, example.QuestNames, run, (_, c) => content = c,
            c => c.Markers.Where(m => (m.Group == first || m.Group == second) && m.Objective is not null).Select(m => m.Position).ToList(), 220);
        if (content is null || Map.Scene is not { } scene || !await TourWait(700, run))
            return;
        // The pen's pick: the quest takes the first pick's colour on the map, a ring leaving its place.
        scene.PickSlots = new Dictionary<string, int> { [first] = 0 };
        scene.Kept = new HashSet<string> { first };
        Map.Refresh();
        if (MarkerOf(content, first) is { } picked)
            TourRing(ToLayer(picked.Position), Linked.PickBrush(0));
        if (!await TourWait(1900, run) || MarkerOf(content, second) is not { } pointed || second == first)
            return;
        // Pointing: another quest lights up everywhere it is, and pulses.
        var map = MapRect();
        TourPointer(new Point(map.X + map.Width * 0.62, map.Bottom - 240));
        await TourPointerTo(ToLayer(pointed.Position), 900, run);
        if (!await TourWait(100, run))
            return;
        scene.Focus = new HashSet<string> { second };
        Map.Redraw();
    }

    // The screenshot key, large, pressed; the file name it brings decodes under it; then the position on the map pings,
    // and an example extract list lights the extracts that are yours.
    private async Task StageKey(Tour.Chapter chapter, int run)
    {
        TourHoles(() => []);
        if (_snapshot?.Data is not { } data || TourExample(data) is not { } example)
            return;
        MapContent? content = null;
        RaidReplay? walk = null;
        // Framed on where "you" will be, with room around: the chapter is about that spot.
        await TourScene(data, example, [], run, (_, c) => { content = c; walk = ReplayExample(c); },
            c => ReplayExample(c)?.Fixes.TakeLast(1).Select(f => f.Position).ToList(), 420);
        if (content is null || walk is null || Map.Scene is not { } scene)
            return;
        var you = walk.Fixes[^1].Position;
        var key = TourKeyCap(Caps.Of(_snapshot?.ScreenshotKeys.FirstOrDefault() ?? "PrtSc"), out var cap, out var name);
        ShowPlate(key);
        if (!await TourWait(950, run))
            return;
        await PressTourKey(cap, run);
        var file = FormattableString.Invariant($"…_{you.X:0.00}, {you.Y:0.00}, {you.Z:0.00}_…");
        if (_tourMotion)
            Decode(name, file, TimeSpan.FromMilliseconds(600));
        else
            name.Text = file;
        if (!await TourWait(1500, run))
            return;
        TourPlate.Visibility = Visibility.Collapsed;
        scene.Player = new PlayerFix(you, 35, DateTime.Now);
        Map.Refresh();
        TourHoles(() => [Square(ToLayer(you), 150)]);
        if (!await TourWait(2400, run))
            return;
        await StageExtractListRead(chapter, scene, content, you, run);
    }

    // The extract list as its own cause and effect (owner, 2026-10-09, after "The your screenshot key panel is weird
    // after the focus on the player position"): an example of the game's list, in Shturmap's own look, decodes where
    // the game shows it (the top right) under what to press; a press reads it; then the view takes in the extracts on
    // it, which light up one after another, nearest first, each with a ring in its kind's colour, and the rest go
    // hollow. The frame follows: from you to the list, then, as the list goes, to what the view shows of the extracts.
    private async Task StageExtractListRead(Tour.Chapter chapter, MapScene scene, MapContent content, WorldPoint you, int run)
    {
        var (listed, unsure, notListed) = ExampleExits(content);
        if (listed.Count == 0)
            return;
        listed = listed.OrderBy(m => m.Position.HorizontalDistanceTo(you)).ToList();
        var keyName = Caps.Of(_snapshot?.ScreenshotKeys.FirstOrDefault() ?? "PrtSc");
        var how = (chapter.Words.FirstOrDefault(w => w.Key == "list")?.Text ?? "").Replace("{key}", keyName, StringComparison.Ordinal);
        var all = listed.Count + unsure.Count + notListed.Count;
        var readText = (chapter.Words.FirstOrDefault(w => w.Key == "read")?.Text ?? "")
            .Replace("{n}", (listed.Count + unsure.Count).ToString(UiLanguage.Culture), StringComparison.Ordinal)
            .Replace("{all}", all.ToString(UiLanguage.Culture), StringComparison.Ordinal);
        var names = listed.Select(m => (m.Label, false)).Concat(unsure.Select(m => (m.Label, true))).ToList();
        var (plate, rows, read) = ExtractListPlate(how, names, readText);
        TourCanvas.Children.Add(plate);
        plate.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        var map = MapRect();
        var plateAt = new Rect(map.Right - 16 - plate.DesiredSize.Width, map.Y + 16, plate.DesiredSize.Width, plate.DesiredSize.Height);
        Canvas.SetLeft(plate, plateAt.X);
        Canvas.SetTop(plate, plateAt.Y);
        var plateHole = new Rect(plateAt.X - 6, plateAt.Y - 6, plateAt.Width + 12, plateAt.Height + 12);
        TourHoles(() => [Square(ToLayer(you), 150), plateHole]);
        if (_tourMotion)
        {
            plate.Opacity = 0;
            _ = Animate(250, t => plate.Opacity = t);
        }
        // The list's names, one after another, as the game's list fills in.
        foreach (var (row, text) in rows)
        {
            if (!await TourWait(row == rows[0].Row ? 450 : 200, run))
                return;
            row.Opacity = 1;
            if (_tourMotion)
                Decode(row, text, TimeSpan.FromMilliseconds(380));
        }
        if (!await TourWait(600, run))
            return;
        // The press: the plate's frame flashes, and it says what was read.
        plate.BorderBrush = Resource("AmberBrush");
        read.Opacity = 1;
        if (!await TourWait(350, run))
            return;
        plate.BorderBrush = Resource("LineStrongBrush");
        if (!await TourWait(1100, run))
            return;
        // Read, the list has done its part: it goes, and the map shows what it said (it would cover the extracts in the
        // map's top right).
        if (_tourMotion)
            await Animate(250, t => plate.Opacity = 1 - t);
        if (run != _tourRun || !TourOpen)
            return;
        TourCanvas.Children.Remove(plate);
        // The view takes in the listed extracts and you, above the band; the frame follows to what it shows of them.
        var points = listed.Concat(unsure).Select(m => m.Position).Append(you).ToList();
        var view = Map.FramingAbove(scene, points, TourFraming, TourBand.ActualHeight + 46, 300);
        if (_tourMotion)
        {
            Map.AnimateView(view, TimeSpan.FromMilliseconds(1200));
            await Task.Delay(1250);
        }
        else
            Map.Jump(view);
        if (run != _tourRun || !TourOpen)
            return;
        // What the view shows of them, with room for the labels the map writes to the right of its symbols.
        Rect Region()
        {
            var at = points.Select(ToLayer).ToList();
            var r = new Rect(at.Min(p => p.X) - 40, at.Min(p => p.Y) - 40, 0, 0);
            var mapNow = MapRect();
            double x0 = Math.Max(mapNow.X, r.X), y0 = Math.Max(mapNow.Y, r.Y);
            double x1 = Math.Min(mapNow.Right, at.Max(p => p.X) + 150), y1 = Math.Min(mapNow.Bottom, at.Max(p => p.Y) + 40);
            return new Rect(x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
        }
        TourHoles(() => [Region()]);
        // One after another, nearest first; then the ones the list leaves out go hollow, and the "??:??:??" one gets its "?".
        var lit = new HashSet<string>(StringComparer.Ordinal);
        foreach (var exit in listed)
        {
            if (!await TourWait(lit.Count == 0 ? 450 : 260, run))
                return;
            lit.Add(exit.Id);
            scene.ExitsListed = new HashSet<string>(lit, StringComparer.Ordinal);
            Map.Redraw();
            TourRing(ToLayer(exit.Position), Resource(exit.Kind == MarkerKind.ExtractShared ? "KhakiBrush" : "GreenBrush"));
        }
        if (!await TourWait(350, run))
            return;
        scene.ExitsUnsure = unsure.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
        scene.ExitsNotListed = notListed;
        Map.Redraw();
    }

    // The game's extract list as an example, in Shturmap's own look (no picture of the game's, §3): what to press over
    // the names, and under them, once read, how many are yours. The rows start hidden; the read line keeps its room.
    private static (Border Plate, List<(TextBlock Row, string Text)> Rows, TextBlock Read) ExtractListPlate(string how,
        IReadOnlyList<(string Name, bool Unsure)> names, string readText)
    {
        var stack = new StackPanel { Spacing = 6, Width = 250 };
        stack.Children.Add(new TextBlock { Text = how, Style = TextStyle("EyebrowText"), Margin = new Thickness(0), Foreground = Resource("AmberBrush"), TextWrapping = TextWrapping.Wrap });
        stack.Children.Add(new Rectangle { Height = 1, Fill = Resource("LineBrush"), Margin = new Thickness(0, 2, 0, 2) });
        var rows = new List<(TextBlock, string)>();
        foreach (var (name, unsure) in names)
        {
            var text = unsure ? $"{name} · ???" : name;
            var row = new TextBlock { Text = text, Style = TextStyle("TitleText"), FontSize = 15, TextWrapping = TextWrapping.NoWrap, Foreground = Resource(unsure ? "MutedBrush" : "InkBrush"), Opacity = 0 };
            stack.Children.Add(row);
            rows.Add((row, text));
        }
        var read = new TextBlock { Text = readText, Style = TextStyle("StatusText"), FontSize = 11.5, Foreground = Resource("GreenBrush"), TextWrapping = TextWrapping.Wrap, Opacity = 0, Margin = new Thickness(0, 6, 0, 0) };
        stack.Children.Add(read);
        var plate = new Border
        {
            Child = stack, Padding = new Thickness(18, 14, 18, 14), BorderThickness = new Thickness(1), BorderBrush = Resource("LineStrongBrush"),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xF0, 0x0B, 0x0C, 0x0B)),
        };
        return (plate, rows, read);
    }

    // RAID LOADING with the example's kit, as the cue pictures it; then the raid card's head with example values: the
    // time left, NEXT and EXIT.
    private async Task StageRaid(Tour.Chapter chapter, int run)
    {
        TourHoles(() => Anchors(chapter));
        if (_snapshot?.Data is not { } data || TourExample(data) is not { } example)
            return;
        MapContent? content = null;
        // Framed on the whole example, above the band (fitted, the map ran under the band and its labels off the right edge).
        await TourScene(data, example, example.QuestNames, run, (_, c) => content = c, c => c.Markers.Select(m => m.Position).ToList(), 0);
        if (content is null || !await TourWait(300, run))
            return;
        var plan = Planning.PlanFor(data, example.QuestIds, example.Map);
        var picks = new HashSet<string> { example.QuestIds[0] };
        var (shown, more) = Planning.CueKit(Planning.Kit(plan, picks), picks: picks);
        _tourCue = true;
        ShowCue(new ViewCue(CueKind.RaidLoading, example.MapName, KitMore: more,
            Kit: shown.Select(r => new CueItem(r.ItemId, r.Kind, Planning.ForPick(r, picks), 0, r.Count)).ToList()), example: true);
        if (!await TourWait(5200, run))
            return;
        _tourCue = false;
        _cueStory?.Stop();
        _cueTimer?.Stop();
        CuePanel.Visibility = Visibility.Collapsed;
        ShowPlate(ExampleRaidCard(data, plan, content, example));
    }

    // The three buttons at the top right, framed and named.
    private async Task StageKnow(Tour.Chapter chapter, int run)
    {
        TourHoles(() => Anchors(chapter));
        var below = 0;
        foreach (var word in chapter.Words)
        {
            if (!await TourWait(below == 0 ? 450 : 250, run))
                return;
            if (Anchor(word.Key, 0) is not { } button)
                continue;
            // Each label a step lower than the one before, right-aligned under its button, on a hairline up to it.
            var top = button.Bottom + 14 + below * 24;
            var x = button.X + button.Width / 2;
            var label = new TextBlock { Text = word.Text, Style = TextStyle("StatusText"), FontSize = 11.5, Foreground = Resource("AmberBrush") };
            label.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, x - label.DesiredSize.Width + 6);
            Canvas.SetTop(label, top);
            var line = new Rectangle { Width = 1, Height = top - button.Bottom + 2, Fill = Resource("AmberBrush") };
            Canvas.SetLeft(line, x);
            Canvas.SetTop(line, button.Bottom);
            TourCanvas.Children.Add(line);
            TourCanvas.Children.Add(label);
            below++;
        }
    }

    // ---- the example: Customs, the same for everyone ----

    private sealed record TourExampleSet(string Map, string MapName, IReadOnlyList<string> QuestIds, IReadOnlyList<string> QuestNames);

    // Customs, which every player knows (and What's New's previews use): up to three of its early quests with places on
    // the map. Nobody's own quests: the same example for every player.
    private TourExampleSet? TourExample(GameData data)
    {
        if (_tourExample is { } known)
            return known;
        if (data.MapByNormalizedName("customs") is not { } map)
            return null;
        string[] preferred = ["delivery-from-the-past", "checking", "bp-depot", "the-extortionist", "golden-swag", "chemical-part-1"];
        var candidates = preferred.Select(n => data.Tasks.Values.FirstOrDefault(t => t.NormalizedName == n)).OfType<ApiTask>()
            .Concat(data.Tasks.Values.Where(t => t.Map == map.Id || (t.Objectives ?? []).Any(o => o.Maps?.Contains(map.Id) == true))
                .OrderBy(t => t.MinPlayerLevel ?? 99).ThenBy(t => t.Name, StringComparer.Ordinal).Take(40))
            .DistinctBy(t => t.Id).ToList();
        var content = MapContentBuilder.Build(data, map.Id, candidates.Select(t => t.Id), new HashSet<string>());
        var placed = content.Markers.Where(m => m.Group is not null && m.Objective is not null).Select(m => m.Group!).ToHashSet(StringComparer.Ordinal);
        var chosen = candidates.Where(t => placed.Contains(t.Id) && t.NormalizedName is not null).Take(3).ToList();
        if (chosen.Count == 0)
            return null;
        return _tourExample = new TourExampleSet("customs", map.Name, chosen.Select(t => t.Id).ToList(), chosen.Select(t => t.NormalizedName!).ToList());
    }

    // DIPs around what a chapter frames on the map: room for the labels the map writes right of its symbols, which 60
    // cut at the map's right edge (review of 2026-10-09).
    private const double TourFraming = 90;

    // The example's map, staged as What's New's previews are (its label says it is an example), framed above the band.
    private async Task TourScene(GameData data, TourExampleSet example, IReadOnlyCollection<string> quests, int run,
        Action<MapScene, MapContent>? stage, Func<MapContent, IReadOnlyCollection<WorldPoint>?>? frame, double minMetres)
    {
        var wanted = TourPreviewPrefix + run;
        StopReplay("preview");
        EndWhatsNewPreview();
        _restoreView = _tourRestore;
        _previewing = wanted;
        ViewModel.PreviewText = $"PREVIEW · THE TOUR · {Caps.Of(example.MapName)}";
        ViewModel.PreviewHint = "AN EXAMPLE, NOT YOUR RAID";
        await PreviewSceneAsync(wanted, data, example.Map, quests, stage, frame, minMetres, TourBand.ActualHeight + 46, TourFraming);
        // One layout pass, so the view's points are where they are drawn.
        await Task.Delay(_tourMotion ? 60 : 30);
    }

    // The raid card's head, with example values: the map and side, the clock, NEXT and EXIT.
    private FrameworkElement ExampleRaidCard(GameData data, MapPlanView? plan, MapContent content, TourExampleSet example)
    {
        var card = new StackPanel { Width = 352, Spacing = 12 };
        var head = new Grid();
        head.Children.Add(new TextBlock { Text = Caps.Of(example.MapName), Style = TextStyle("TitleText"), CharacterSpacing = 80 });
        head.Children.Add(new TextBlock { Text = "PMC", Style = TextStyle("StatusText"), Foreground = Resource("MutedBrush"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center });
        card.Children.Add(head);
        var minutes = plan is { RaidMinutes: > 0 } p ? p.RaidMinutes : 40;
        // Thirteen minutes left, as the website's picture and What's New's clock say it, of the map's own raid length
        // (tarkov.dev's: 35 min for Customs in October 2026, so 22 min in).
        card.Children.Add(new Controls.RaidClock { Reading = RaidTime.Of(true, false, DateTime.Now.AddMinutes(-(minutes - 13)), minutes, DateTime.Now) });
        // As the raid card lists them: a hand-over of what another objective gets is no line of its own (Handovers).
        var next = content.Objectives.FirstOrDefault(o => o.Places.Count > 0 && example.QuestIds.Contains(o.Quest.Id) && !Handovers.Folds(o.Quest, o.Objective.Id));
        var exit = content.Markers.FirstOrDefault(m => m.Kind == MarkerKind.ExtractPmc);
        card.Children.Add(new Rectangle { Height = 1, Fill = Resource("LineBrush") });
        if (next is not null)
        {
            // In a few words, as the raid card says it ("Mark Stryker"; Planning.ObjectiveSynopses, the session's own call
            // for the card's lines); tarkov.dev's sentence only where the data isn't English (review of 2026-10-09).
            var shorts = Planning.ObjectiveSynopses(data, content.Objectives.Where(o => o.Quest.Id == next.Quest.Id && !Handovers.Folds(o.Quest, o.Objective.Id)),
                data.MapIdsSharing(example.Map));
            var text = shorts.GetValueOrDefault(next.Objective.Id) ?? Shorten(next.Objective.Description ?? next.Quest.Name, 34);
            card.Children.Add(GlanceRow("NEXT", text, next.Quest.Name, "86 m", "AHEAD-LEFT", "AmberBrush"));
        }
        if (exit is not null)
            card.Children.Add(GlanceRow("EXIT", exit.Label, "ON YOUR LIST THIS RAID", "214 m", "BEHIND", "GreenBrush"));
        card.Children.Add(new TextBlock { Text = "THE RAID CARD, WITH EXAMPLE VALUES", Style = TextStyle("EyebrowText"), Margin = new Thickness(0, 6, 0, 0) });
        return card;
    }

    private static string Shorten(string text, int length) => text.Length <= length ? text : text[..(length - 1)].TrimEnd() + "…";

    private static Grid GlanceRow(string label, string name, string note, string distance, string direction, string brush)
    {
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(new TextBlock { Text = label, Style = TextStyle("EyebrowText"), Margin = new Thickness(0, 4, 0, 0) });
        var what = new StackPanel { Spacing = 2 };
        what.Children.Add(new TextBlock { Text = name, Style = TextStyle("TitleText"), FontSize = 15, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap });
        what.Children.Add(new TextBlock { Text = note, Style = TextStyle("StatusText"), FontSize = 10.5, Foreground = Resource(brush == "GreenBrush" ? "GreenBrush" : "MutedBrush") });
        Grid.SetColumn(what, 1);
        row.Children.Add(what);
        var where = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Right };
        where.Children.Add(new TextBlock { Text = distance, Style = TextStyle("FigureText"), FontSize = 22, Foreground = Resource(brush) });
        where.Children.Add(new TextBlock { Text = direction, Style = TextStyle("StatusText"), FontSize = 10.5, HorizontalAlignment = HorizontalAlignment.Right });
        Grid.SetColumn(where, 2);
        row.Children.Add(where);
        return row;
    }

    // The screenshot key, drawn large: the cap, its name, and the line its file name decodes into.
    private static FrameworkElement TourKeyCap(string key, out Border cap, out TextBlock name)
    {
        cap = new Border
        {
            MinWidth = 220, Padding = new Thickness(40, 16, 40, 20), Background = Resource("RaisedBrush"), BorderBrush = Resource("LineStrongBrush"),
            BorderThickness = new Thickness(1.5), RenderTransform = new TranslateTransform(),
            Child = new TextBlock { Text = key, FontSize = 48, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontStretch = Windows.UI.Text.FontStretch.SemiCondensed, CharacterSpacing = 120, HorizontalAlignment = HorizontalAlignment.Center },
        };
        var edge = new Grid { HorizontalAlignment = HorizontalAlignment.Center, Padding = new Thickness(0, 0, 0, 9) };
        edge.Children.Add(new Border { Background = Resource("GroundBrush"), BorderBrush = Resource("LineStrongBrush"), BorderThickness = new Thickness(1.5), Margin = new Thickness(0, 9, 0, -9) });
        edge.Children.Add(cap);
        name = new TextBlock { FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontSize = 17, Foreground = Resource("AmberBrush"), HorizontalAlignment = HorizontalAlignment.Center, Text = " " };
        var stack = new StackPanel { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(edge);
        stack.Children.Add(new TextBlock { Text = "SCREENSHOT", FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontStretch = Windows.UI.Text.FontStretch.SemiCondensed, CharacterSpacing = 400, HorizontalAlignment = HorizontalAlignment.Center });
        stack.Children.Add(new TextBlock { Text = "POSITION FROM THE FILE NAME", Style = TextStyle("EyebrowText"), Margin = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Center });
        stack.Children.Add(name);
        return stack;
    }

    // The press: the cap sinks onto its edge in amber and comes back up, as the website's clip shows it.
    private async Task PressTourKey(Border cap, int run)
    {
        if (!_tourMotion || cap.RenderTransform is not TranslateTransform dip)
            return;
        cap.BorderBrush = Resource("AmberBrush");
        await Animate(90, t => dip.Y = 9 * t);
        await Task.Delay(140);
        await Animate(420, t => dip.Y = 9 * (1 - Math.Min(1, t * 2.5)));
        if (run == _tourRun)
            cap.BorderBrush = Resource("LineStrongBrush");
    }

    // ---- what the tour draws ----

    private Rect MapRect() => Map.TransformToVisual(TourLayer).TransformBounds(new Rect(0, 0, Map.ActualWidth, Map.ActualHeight));

    private Point ToLayer(WorldPoint world) => Map.TransformToVisual(TourLayer).TransformPoint(Map.PointOf(world));

    private static Rect Square(Point at, double size) => new(at.X - size / 2, at.Y - size / 2, size, size);

    // The band at the map's foot, clear of the map's buttons at the right (where the replay's band stands); the stage's
    // plate in the middle of the map above it.
    private void PlaceTour()
    {
        if (TourLayer.ActualWidth <= 0 || Map.ActualWidth <= 0)
            return;
        var map = MapRect();
        TourBand.Margin = new Thickness(map.X + 16, 0, 0, 46);
        TourBand.Width = Math.Max(320, map.Width - 16 - 68);
        var band = TourBand.ActualHeight > 0 ? TourBand.ActualHeight : 140;
        TourStage.Margin = new Thickness(map.X, map.Y, 0, 0);
        TourStage.Width = map.Width;
        TourStage.Height = Math.Max(160, map.Height - band - 46);
        if (TourOpen && _tourHolesNow is { } now)
            DrawTourShade(_tourHoles = now(), 1);
        else
            DrawTourShade(_tourHoles, 1);
    }

    private void OnTourLayerSizeChanged(object sender, SizeChangedEventArgs e) => PlaceTour();

    // A part of the window by its x:Name, in the tour's coordinates, with room around it; null when it isn't shown.
    private Rect? Anchor(string name, double pad = 6)
    {
        if (((FrameworkElement)Content).FindName(name) is not FrameworkElement element || element.ActualWidth < 1 || element.ActualHeight < 1 || !Shown(element))
            return null;
        if (element == Map)
            pad = 0;
        var r = element.TransformToVisual(TourLayer).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        return new Rect(r.X - pad, r.Y - pad, r.Width + 2 * pad, r.Height + 2 * pad);
    }

    private static bool Shown(DependencyObject element)
    {
        for (var d = element; d is not null; d = VisualTreeHelper.GetParent(d))
        {
            if (d is UIElement { Visibility: Visibility.Collapsed })
                return false;
        }
        return true;
    }

    // A chapter's parts: for each anchor the first of its names that is shown.
    private List<Rect> Anchors(Tour.Chapter chapter) =>
        chapter.Anchors.Select(names => names.Select(n => Anchor(n)).FirstOrDefault(r => r is not null)).Where(r => r is not null).Select(r => r!.Value).ToList();

    // The cut-outs move from what is drawn now to the new parts (owner, 2026-10-09: "sometimes they transition into
    // nothingness, like from section 3 to section 4"): paired by TourFrames.Pairs, each glides to its part, eased in
    // and out, with the corner marks closing in on it. With nothing drawn the new parts fade in; with no new part the
    // drawn ones fade out under the dim. Nothing shrinks into a point.
    private void TourHoles(Func<List<Rect>> holes)
    {
        _tourHolesNow = () => TourFrames.Merged(holes().Select(AsBox)).Select(AsRect).ToList();
        var target = _tourHolesNow();
        var from = _tourDrawn.ToList();
        _tourHoles = target;
        var run = ++_tourHolesRun;
        TourVeil.Data = null;
        TourMarks.Opacity = 1;
        if (!_tourMotion || (from.Count == 0 && target.Count == 0))
        {
            DrawTourShade(target, 1);
            return;
        }
        if (from.Count == 0 || target.Count == 0)
        {
            // A fade: the parts stay where they are while a veil of the dim over them goes (or comes), and their marks
            // with it. Leaving, they count as gone at once, so a part that comes meanwhile fades in on its own.
            var opening = from.Count == 0;
            var parts = opening ? target : from;
            DrawTourShade(parts, opening ? 0 : 1);
            if (!opening)
                _tourDrawn = [];
            TourVeil.Data = ShadeGeometry(parts, outer: false);
            TourVeil.Opacity = opening ? 1 : 0;
            TourMarks.Opacity = opening ? 0 : 1;
            _ = Animate(opening ? 450 : 350, t =>
            {
                if (run != _tourHolesRun || !TourOpen)
                    return;
                var e = 1 - Math.Pow(1 - t, 2);
                TourVeil.Opacity = opening ? 1 - e : e;
                TourMarks.Opacity = opening ? e : 1 - e;
                if (opening)
                    DrawTourShade(parts, t);
                if (t < 1)
                    return;
                TourVeil.Data = null;
                TourMarks.Opacity = 1;
                DrawTourShade(target, 1);
            });
            return;
        }
        var pairs = TourFrames.Pairs(from.Select(AsBox).ToList(), target.Select(AsBox).ToList());
        _ = Animate(650, t =>
        {
            if (run != _tourHolesRun || !TourOpen)
                return;
            var e = t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
            DrawTourShade(t < 1 ? pairs.Select(p => AsRect(TourFrames.Box.Lerp(p.From, p.To, e))).ToList() : target, t);
        });
    }

    private static TourFrames.Box AsBox(Rect r) => new(r.X, r.Y, r.Width, r.Height);

    private static Rect AsRect(TourFrames.Box b) => new(b.X, b.Y, b.Width, b.Height);

    // The dim with the cut-outs taken out (their union, as pieces that don't overlap: TourFrames.Disjoint), and the map
    // sheet's corner marks around each cut-out, closing in on it with a small overshoot as <paramref name="t"/> goes to 1.
    private void DrawTourShade(IReadOnlyList<Rect> holes, double t)
    {
        // Everything stays inside the window: a part at its edge (the rail, the map) has its marks clipped there.
        double w = TourLayer.ActualWidth, h = TourLayer.ActualHeight;
        var clipped = holes.Select(r => Clip(r, w, h)).Where(r => r.Width > 0 && r.Height > 0).ToList();
        _tourDrawn = clipped;
        TourShade.Data = ShadeGeometry(clipped, outer: true);
        var o = 5 + 14 * Math.Pow(1 - t, 2) - (t > 0.8 ? Math.Sin((t - 0.8) / 0.2 * Math.PI) * 2.5 : 0);
        var marks = new PathGeometry();
        foreach (var hole in clipped.Where(r => r.Width > 12 && r.Height > 12).Distinct())
            AddCorners(marks, hole, o, w, h);
        TourMarks.Data = marks;
    }

    // The window less the cut-outs (outer), or the cut-outs alone (the veil that fades them in or out).
    private Geometry ShadeGeometry(IReadOnlyList<Rect> holes, bool outer)
    {
        var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
        if (outer)
            group.Children.Add(new RectangleGeometry { Rect = new Rect(0, 0, TourLayer.ActualWidth, TourLayer.ActualHeight) });
        foreach (var piece in TourFrames.Disjoint(holes.Select(AsBox)))
            group.Children.Add(new RectangleGeometry { Rect = AsRect(piece) });
        return group;
    }

    private static Rect Clip(Rect r, double w, double h)
    {
        double x0 = Math.Clamp(r.X, 0, w), y0 = Math.Clamp(r.Y, 0, h);
        return new Rect(x0, y0, Math.Clamp(r.X + r.Width, 0, w) - x0, Math.Clamp(r.Y + r.Height, 0, h) - y0);
    }

    private static void AddCorners(PathGeometry geometry, Rect r, double o, double w, double h)
    {
        const double l = 16;
        double x0 = Math.Max(1, r.X - o), y0 = Math.Max(1, r.Y - o), x1 = Math.Min(w - 1, r.X + r.Width + o), y1 = Math.Min(h - 1, r.Y + r.Height + o);
        void Corner(Point a, Point b, Point c)
        {
            var figure = new PathFigure { StartPoint = a, IsClosed = false, IsFilled = false };
            figure.Segments.Add(new LineSegment { Point = b });
            figure.Segments.Add(new LineSegment { Point = c });
            geometry.Figures.Add(figure);
        }
        Corner(new(x0, y0 + l), new(x0, y0), new(x0 + l, y0));
        Corner(new(x1 - l, y0), new(x1, y0), new(x1, y0 + l));
        Corner(new(x1, y1 - l), new(x1, y1), new(x1 - l, y1));
        Corner(new(x0 + l, y1), new(x0, y1), new(x0, y1 - l));
    }

    private void ShowPlate(UIElement content)
    {
        TourPlate.Child = content;
        TourPlate.Visibility = Visibility.Visible;
        if (!_tourMotion)
            return;
        TourPlate.Opacity = 0;
        _ = Animate(260, t => TourPlate.Opacity = t);
    }

    // Lines come in one after another, rising a little as they appear.
    private async Task Reveal(IReadOnlyList<UIElement> lines, int gap, int run)
    {
        if (!_tourMotion)
            return;
        foreach (var line in lines)
        {
            line.Opacity = 0;
            line.RenderTransform = new TranslateTransform { Y = 12 };
        }
        foreach (var line in lines)
        {
            if (!await TourWait(gap, run))
                return;
            var shift = (TranslateTransform)line.RenderTransform;
            _ = Animate(380, t =>
            {
                var e = 1 - Math.Pow(1 - t, 3);
                line.Opacity = e;
                shift.Y = 12 * (1 - e);
            });
        }
    }

    // The linked highlight's tint over a row, as resting the pointer on it gives.
    private void TourTint(FrameworkElement row)
    {
        var r = row.TransformToVisual(TourLayer).TransformBounds(new Rect(0, 0, row.ActualWidth, row.ActualHeight));
        var tint = new Rectangle { Width = r.Width, Height = r.Height, Fill = Resource("LinkBrush") };
        Canvas.SetLeft(tint, r.X);
        Canvas.SetTop(tint, r.Y);
        TourCanvas.Children.Insert(0, tint);
    }

    // A ring leaving a place, as a ping does.
    private void TourRing(Point at, Brush brush)
    {
        var ring = new Ellipse { Width = 80, Height = 80, StrokeThickness = 3, Stroke = brush, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new ScaleTransform { ScaleX = 0.2, ScaleY = 0.2 } };
        Canvas.SetLeft(ring, at.X - 40);
        Canvas.SetTop(ring, at.Y - 40);
        TourCanvas.Children.Add(ring);
        if (!_tourMotion)
        {
            ((ScaleTransform)ring.RenderTransform).ScaleX = ((ScaleTransform)ring.RenderTransform).ScaleY = 0.6;
            return;
        }
        _ = Animate(1400, t =>
        {
            var e = 1 - Math.Pow(1 - t, 2);
            var scale = (ScaleTransform)ring.RenderTransform;
            scale.ScaleX = scale.ScaleY = 0.2 + 0.9 * e;
            ring.Opacity = 1 - t;
        });
    }

    // The drawn pointer, an arrow like the system's (the website clip's), inside the tour so a snapshot shows it.
    private void TourPointer(Point at)
    {
        if (_tourPointer is null)
        {
            _tourPointer = new Path
            {
                Data = (Geometry)Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(typeof(Geometry), "M0,0 L0,19 L4.6,14.6 L7.9,21.6 L10.6,20.4 L7.4,13.5 L13.6,13.5 Z"),
                Fill = new SolidColorBrush(Microsoft.UI.Colors.White),
                Stroke = Resource("GroundBrush"),
                StrokeThickness = 1.2,
                RenderTransform = new ScaleTransform { ScaleX = 1.2, ScaleY = 1.2 },
            };
            TourCanvas.Children.Add(_tourPointer);
        }
        _tourPointerAt = at;
        Canvas.SetLeft(_tourPointer, at.X);
        Canvas.SetTop(_tourPointer, at.Y);
    }

    private async Task TourPointerTo(Point to, int milliseconds, int run)
    {
        if (_tourPointer is null || !_tourMotion)
        {
            TourPointer(to);
            return;
        }
        var from = _tourPointerAt;
        await Animate(milliseconds, t =>
        {
            if (run != _tourRun || _tourPointer is null)
                return;
            var e = t * t * (3 - 2 * t);
            TourPointer(new Point(from.X + (to.X - from.X) * e, from.Y + (to.Y - from.Y) * e));
        });
    }

    private static MapMarker? MarkerOf(MapContent content, string quest) =>
        content.Markers.FirstOrDefault(m => m.Group == quest && m.Objective is not null);

    private static Style TextStyle(string name) => (Style)Application.Current.Resources[name];

    private static TextBlock TourEyebrowText(string text, string brush) =>
        new() { Text = text, Style = TextStyle("EyebrowText"), Margin = new Thickness(0), Foreground = Resource(brush) };

    // ---- first-time lines (owner, 2026-10-09, the panel's "E"): the habit, said when it matters, once ----

    // The first raid after a first start: one line under the cue's kit. Never for an example or a snapshot.
    private void FirstRaidLine(ViewCue cue, bool example)
    {
        var show = !example && !SnapshotMode && !DemoMode && cue.Kind == CueKind.RaidLoading && _session.GetSetting(FirstRaidSetting) == "due";
        CueFirst.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (!show)
            return;
        var key = _snapshot?.ScreenshotKeys.FirstOrDefault() ?? "your screenshot key";
        CueFirst.Text = $"FIRST RAID WITH SHTURMAP · PRESS {Caps.Of(key)} ONCE YOU'RE IN";
        _session.SetSetting(FirstRaidSetting, "done");
        Study.Ui("first.raid");
    }

    // The first position after a first start: one line that says what it is.
    private void FirstFixNotice(bool offScreen)
    {
        if (offScreen || TourOpen || _previewing is not null || SnapshotMode || DemoMode || _session.GetSetting(FirstFixSetting) != "due")
            return;
        _session.SetSetting(FirstFixSetting, "done");
        Study.Ui("first.fix");
        ShowNotice("That's you, from your screenshot's name. Each new one moves you.");
    }
}
