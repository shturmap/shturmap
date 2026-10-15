using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Shturmap.App.Rules;
using Shturmap.Map;
using Shturmap.Session;
using Point = Windows.Foundation.Point;

namespace Shturmap.App;

// YOUR EXTRACTS THIS RAID, the extract list's roll call (owner, 2026-10-15: "When the extraction list is read, I want a
// nice fancy animation in line with the others where a list of the extracts appears in the middle of the screen along
// with their icons. Then, the icons move where extracts are (also outside of the current view pane) so it contextualizes
// them and the player knows where they are"; from a panel of four the same day: A, "Roll call, then fly-out";
// docs/DESIGN.md §4, principle 11, and "Map drawing", *A list just read is called out*). The cue's band names the list's
// extracts, a row each, nearest the player first; from 1.2 s the view zooms out to take them all in, and from 1.6 s each
// row's triangle flies to its extract's place (MapScene.CallOutExits draws the flight), its row fading as it leaves and
// the band's dark going with the rows, so it covers no landing; the title stays on its halo until 1.5 s after the last
// landing. A list that comes while the cue is up joins it; one after it rings what it lit anew where it stands
// (MapScene.RingExits). With animation effects off the band stands still for 5 s with all its rows and the view takes the
// extracts in at once. The tour's chapter 5 and What's New's extracts preview call their example list out the same way.
public sealed partial class MainWindow
{
    // Who called the roll: the raid's list, the tour's example (it ends with the chapter) or What's New's (with the preview).
    private enum RollOwner
    {
        Raid,
        Tour,
        WhatsNew,
    }

    // A row of the band: the extract, whether it is lit or "??:??:??", its parts, and when it rises in and its icon leaves.
    private sealed class RollRow
    {
        public required MapMarker Exit { get; init; }

        public required bool Listed { get; init; }

        public required Grid Element { get; init; }

        public required FrameworkElement Triangle { get; init; }

        public required TextBlock Name { get; init; }

        public required TranslateTransform Shift { get; init; }

        public DateTime RiseAt { get; set; }

        // When its icon lifts off (MapScene.ExitFlights); none with animation effects off.
        public DateTime? LiftOff { get; set; }
    }

    // The rows a list (and the lists that joined it) called, and the moments their icons left in: the band's dark goes with
    // each group's rows (RollBand).
    private readonly List<RollRow> _rollRows = [];
    private readonly List<(DateTime Start, DateTime FirstLift, DateTime LastLift, bool Join)> _rollGroups = [];
    private readonly int[] _rollFill = new int[RollColumnsMost];
    // The extracts the band names, rows and "+N" alike: the title's count.
    private readonly HashSet<string> _rollIds = new(StringComparer.Ordinal);
    private IReadOnlyList<MapMarker> _rollWaiting = [];
    private MapScene? _rollScene;
    private RollOwner _rollOwner;
    private int _rollTotal;
    private int _rollMore;
    private DateTime _rollStart;
    private DateTime _rollLastJoin;
    // The icons fly and the rows fade: off with animation effects off.
    private bool _rollMotion;
    // DIPs at the map's foot that the band and the view keep above (the tour's band), at its top that the view keeps under
    // (the tour's label), and the room the view keeps around.
    private double _rollFoot;
    private double _rollHead;
    private double _rollPadding = Camera.CallOutPadding;
    // The cue fades out: a list that comes now rings where it stands, as after the cue.
    private bool _rollEnding;
    // Where the roll call's view took the map, to tell whether a joining extract is in it.
    private (Shturmap.Core.Maps.MapPoint Center, double Zoom)? _rollView;
    private DispatcherQueueTimer? _rollFrames;
    private DispatcherQueueTimer? _rollFit;
    private DispatcherQueueTimer? _rollHide;

    private bool RollCallShowing => _rollRows.Count > 0 && !_rollEnding;

    // Columns of up to four rows, up to three (a side of a map lists up to twelve extracts); past that the twelfth slot says
    // "+N".
    private const int RollColumnRows = 4;
    private const int RollColumnsMost = 3;
    private const int RollRowsMost = 12;

    // A row's line, in DIPs: its triangle's box, and its name's line height.
    private const double RollLine = 20;

    // The row triangle's centre in its box: 1.2 DIP below the box's middle, as the map's triangle centre lies a tenth of its
    // height below the middle of its box (MapRenderer's Triangle; ExitCall.FromX).
    private static readonly Point RollTriangleCentre = new(10, RollLine / 2 + 1.2);

    // The rows rise in from 0.5 s, 0.1 s apart, over 0.3 s (as the kit's pictures come in, one after another); a row fades
    // over 0.3 s from its icon's lift-off; the title stays 1.5 s after the last landing; without motion the band stands 5 s.
    private static readonly TimeSpan RollRiseFrom = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan RollRiseApart = TimeSpan.FromSeconds(0.1);
    private static readonly TimeSpan RollRise = TimeSpan.FromSeconds(0.3);
    private static readonly TimeSpan RollFade = TimeSpan.FromSeconds(0.3);
    private static readonly TimeSpan RollTitleStays = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan RollStill = TimeSpan.FromSeconds(5);

    // A joining list's rows rise in first: their icons lift off no sooner than this after the list came (and after the last
    // icon before them, MapScene.CallOutExits), never straight off a row that isn't there yet.
    private static readonly TimeSpan RollJoinRise = TimeSpan.FromSeconds(0.5);

    // How long the band's dark takes to come back for a joining list's rows once it had gone.
    private static readonly TimeSpan RollBandBack = TimeSpan.FromSeconds(0.2);

    /// <summary>
    /// A list from a screenshot just taken, once per list (UpdateMap): the roll call; while its cue is up, the list joins
    /// it; after it, what the list lit or marked anew rings out where it stands, with no cue and no change of view
    /// (MapScene.RingExits, which keeps to that once a roll call ran in the raid); the same list again changes nothing.
    /// </summary>
    private void ExitListJustRead(MapScene scene, SessionSnapshot s)
    {
        var now = DateTime.Now;
        _rollRaid = RollCallRecord.Of(s.RaidMap?.Id, s.Raid);
        if (RollCallShowing && _rollOwner == RollOwner.Raid && ReferenceEquals(_rollScene, scene)
            && (!_rollMotion || scene.ExitsArrivingAt(now + RollJoinRise)))
            JoinRollCall(now);
        else if (_rollCalls.In(_rollRaid) is { } called)
        {
            scene.RingExits(now, called);
            RecordRollCall(scene);
        }
        else
            RollCall(scene, s.Extracts.Count(e => e.Kind != MarkerKind.Transit), RollOwner.Raid);
        Map.Redraw();
    }

    // What the roll call called in the raid it ran in, kept for the raid, not on the map's scene (Rules.RollCallRecord):
    // a look at another map and back makes the scene anew, and a later list then got a whole roll call again.
    private readonly RollCallRecord _rollCalls = new();
    private RollCallRecord.Raid? _rollRaid;

    // The raid's list as called now (by the roll call, a list joining it or one rung after it): kept for the raid.
    private void RecordRollCall(MapScene scene)
    {
        if (_rollRaid is { } raid)
            _rollCalls.Called(raid, scene.ExitsToCall().Select(m => (m.Id, scene.ExitsListed.Contains(m.Id))));
    }

    /// <summary>
    /// Calls the scene's list out (its ExitsListed, ExitsUnsure and ExitsNotListed set): the YOUR EXTRACTS THIS RAID cue with
    /// a row per extract to call (MapScene.ExitsToCall), each row's icon flown to its place. <paramref name="total"/>: the
    /// map's extracts for the side, as the rail counts them. <paramref name="foot"/> DIPs at the map's foot stay clear (the
    /// tour's band), and the view takes its extracts in under <paramref name="head"/> DIPs at its top (the tour's label; QA
    /// pass, 2026-10-15), <paramref name="padding"/> DIPs around them.
    /// </summary>
    private void RollCall(MapScene scene, int total, RollOwner owner, double foot = 0, double padding = Camera.CallOutPadding, double head = 0)
    {
        var exits = scene.ExitsToCall();
        if (exits.Count == 0)
            return;
        EndRollCall();
        _rollScene = scene;
        _rollOwner = owner;
        _rollTotal = total;
        _rollFoot = foot;
        _rollHead = head;
        _rollPadding = padding;
        _rollStart = _rollLastJoin = DateTime.Now;
        _rollMotion = scene.Pulse;
        _rollWaiting = exits;
        _rollIds.UnionWith(exits.Select(m => m.Id));
        ShowCue(new ViewCue(CueKind.ExitsListed, ""), example: owner != RollOwner.Raid);
    }

    // The cue's words: "FROM YOUR SCREENSHOT" over "YOUR EXTRACTS THIS RAID · 6 OF 12", the extracts the band names (the
    // "+N" ones too) of the map's for the side.
    private (string Eyebrow, string Title, string Detail) RollCallText() =>
        (AppTexts.CueExitsListedEyebrow, AppTexts.CueExitsListed(count: _rollIds.Count, total: Math.Max(_rollTotal, _rollIds.Count)), "");

    // ShowCue lays the rows out under the title (the kit's place): columns of up to four, the first column filled first.
    private void ShowRollRows()
    {
        CueExits.Children.Clear();
        CueExits.ColumnDefinitions.Clear();
        CueExits.RowDefinitions.Clear();
        Array.Clear(_rollFill);
        var exits = _rollWaiting;
        var rows = Math.Min(exits.Count, RollRowsMost);
        var columns = Math.Min(RollColumnsMost, (rows + RollColumnRows - 1) / RollColumnRows);
        // Balanced: six rows stand three and three, not four and two; a list of four or fewer is one column a joining list
        // fills first.
        var perColumn = rows <= RollColumnRows ? RollColumnRows : (rows + columns - 1) / columns;
        var shown = exits.Count > RollRowsMost ? RollRowsMost - 1 : exits.Count;
        _rollMore = exits.Count - shown;
        _rollNameWidth = RollNameMost;
        for (var i = 0; i < shown; i++)
            AddRollRow(exits[i], i / perColumn, i % perColumn);
        if (_rollMore > 0)
        {
            var more = new TextBlock
            {
                Text = $"+{_rollMore}", Style = TextStyle("FigureText"), Foreground = Resource("MutedBrush"), LineHeight = RollLine,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight, Margin = new Thickness(RollTriangleWidth + 8, 0, 0, 0),
            };
            Place(more, shown / perColumn, shown % perColumn);
            CueExits.Children.Add(more);
        }
        CueExitsBox.MaxWidth = CueExitsBox.MaxHeight = double.PositiveInfinity;
        CueExitsBox.Visibility = Visibility.Visible;
    }

    private const double RollTriangleWidth = 20;

    // DIPs the rows keep from each side of the map's column: the map's buttons and 8 more.
    private const double RollClearOfButtons = 60;

    // A name's line at most, in DIPs, and the width the names now break at (FitRollRows).
    private const double RollNameMost = 320;
    private double _rollNameWidth = RollNameMost;

    /// <summary>
    /// The rows in the room the band has under its title, in the map's column above <see cref="_rollFoot"/>: each name on one
    /// line where its column has room, else broken between words (never inside one), the names' width chosen so the rows
    /// stand as large as the room lets them; and where even so they don't fit (twelve rows at the smallest window), drawn
    /// smaller to fit, never cut, as the tour's plates are (owner, 2026-10-10). Before the rows are handed to the map.
    /// </summary>
    private void FitRollRows()
    {
        var names = _rollRows.Select(r => r.Name).ToList();
        if (names.Count == 0 || Map.ActualWidth <= 0)
            return;
        CueExitsBox.MaxWidth = CueExitsBox.MaxHeight = double.PositiveInfinity;
        CuePanel.UpdateLayout();
        // Clear of the map's buttons at the right (16 from the edge, 36 wide), on both sides: the rows stand in the middle.
        var width = Map.ActualWidth - 2 * RollClearOfButtons;
        var height = Map.ActualHeight - _rollFoot - 16 - (CueStack.ActualHeight - CueExitsBox.ActualHeight);
        // Never narrower than a name's longest word: a word too long for its line would be broken inside or cut.
        var narrowest = names.Max(WidestWord);
        var best = (Scale: -1.0, Width: RollNameMost);
        for (var w = Math.Min(RollNameMost, Math.Ceiling(narrowest)); ; w = Math.Min(RollNameMost, w + 10))
        {
            foreach (var name in names)
                name.MaxWidth = w;
            CueExits.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            var size = CueExits.DesiredSize;
            var scale = Math.Min(1, Math.Min(width / Math.Max(1, size.Width), height / Math.Max(1, size.Height)));
            // The largest scale, and of the widths that give it the widest: fewer lines broken.
            if (scale >= best.Scale - 1e-6)
                best = (Math.Max(scale, best.Scale), w);
            if (w >= RollNameMost)
                break;
        }
        _rollNameWidth = best.Width;
        foreach (var name in names)
            name.MaxWidth = _rollNameWidth;
        CueExitsBox.MaxWidth = Math.Max(1, width);
        CueExitsBox.MaxHeight = Math.Max(RollLine, height);
        CuePanel.UpdateLayout();
    }

    // The widest word of a name, as the row sets it.
    private static double WidestWord(TextBlock name) =>
        name.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(word =>
        {
            var probe = new TextBlock { Text = word, Style = name.Style, FontSize = name.FontSize };
            probe.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            return probe.DesiredSize.Width;
        }).DefaultIfEmpty(0).Max() + 1;

    private void AddRollRow(MapMarker exit, int column, int row)
    {
        var listed = _rollScene?.ExitsListed.Contains(exit.Id) ?? true;
        var shift = new TranslateTransform();
        var element = new Grid { ColumnSpacing = 8, RenderTransform = shift };
        element.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        element.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        element.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var triangle = ExitTriangle(exit.Kind);
        triangle.VerticalAlignment = VerticalAlignment.Top;
        element.Children.Add(triangle);
        // The name in the cue's item type (its 15), broken between words in a narrow column rather than cut (FitRollRows),
        // never after a hyphen in a word (Rules.WordBreaks; QA pass, 2026-10-15); "???" where the game marks the extract
        // "??:??:??", muted: a sign, not a word.
        var name = new TextBlock
        {
            Text = WordBreaks.BetweenWordsOnly(exit.Label), Style = TextStyle("TitleText"), FontSize = 15, LineHeight = RollLine, LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            TextWrapping = TextWrapping.WrapWholeWords, MaxWidth = _rollNameWidth,
        };
        Grid.SetColumn(name, 1);
        element.Children.Add(name);
        if (!listed)
        {
            var sign = new TextBlock
            {
                Text = "???", Style = TextStyle("TitleText"), FontSize = 15, LineHeight = RollLine, LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                Foreground = Resource("MutedBrush"), TextWrapping = TextWrapping.NoWrap,
            };
            Grid.SetColumn(sign, 2);
            element.Children.Add(sign);
        }
        Place(element, column, row);
        CueExits.Children.Add(element);
        _rollFill[column] = Math.Max(_rollFill[column], row + 1);
        _rollRows.Add(new RollRow { Exit = exit, Listed = listed, Element = element, Triangle = triangle, Name = name, Shift = shift });
    }

    private void Place(FrameworkElement element, int column, int row)
    {
        while (CueExits.ColumnDefinitions.Count <= column)
            CueExits.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        while (CueExits.RowDefinitions.Count <= row)
            CueExits.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetColumn(element, column);
        Grid.SetRow(element, row);
    }

    // The extract's triangle as the map draws it (MapRenderer.DrawExtract), at the size its flight starts from (0.8 of the
    // map's 7.5): apex up, R = 6 DIP, a 3 DIP dark collar, the kind's colour with a dark outline, the shared kind's split
    // line; in a box of a row's line, its centre at RollTriangleCentre.
    private static Canvas ExitTriangle(MarkerKind kind)
    {
        const double r = 6;
        var (cx, cy) = (RollTriangleCentre.X, RollTriangleCentre.Y);
        PointCollection Corners() => [new Point(cx, cy - 1.2 * r), new Point(cx + 1.1 * r, cy + 0.8 * r), new Point(cx - 1.1 * r, cy + 0.8 * r)];
        // The map's collar: the ground at 170 of 255 (MapRenderer.MarkerCollarAlpha), 3 DIP around.
        var collar = new SolidColorBrush(Windows.UI.Color.FromArgb(170, 0x0B, 0x0C, 0x0B));
        var canvas = new Canvas { Width = RollTriangleWidth, Height = RollLine, IsHitTestVisible = false };
        canvas.Children.Add(new Polygon { Points = Corners(), Fill = collar, Stroke = collar, StrokeThickness = 6, StrokeLineJoin = PenLineJoin.Round });
        canvas.Children.Add(new Polygon
        {
            Points = Corners(), StrokeThickness = 1.6, Stroke = Resource("GroundBrush"),
            Fill = Resource(kind switch
            {
                MarkerKind.ExtractScav => "TealBrush",
                MarkerKind.ExtractShared => "KhakiBrush",
                _ => "GreenBrush",
            }),
        });
        // Shared by both sides: split down the middle, as on the map.
        if (kind == MarkerKind.ExtractShared)
            canvas.Children.Add(new Line { X1 = cx, X2 = cx, Y1 = cy - 0.85 * r, Y2 = cy + 0.8 * r, Stroke = Resource("GroundBrush"), StrokeThickness = 1.8 });
        return canvas;
    }

    /// <summary>
    /// ShowCue has laid the band out: each row's triangle is handed to the map where it stands (ExitCall: the centre of the
    /// row's triangle in the map panel's physical pixels, DIPs times the window's RasterizationScale, from the map's top
    /// left), the rows rise in, each fades as its icon lifts off, the view zooms out at 1.2 s, and the cue ends 1.5 s after
    /// the last landing (HideAfter).
    /// </summary>
    private void StartRollCall()
    {
        _rollWaiting = [];
        CuePanel.Margin = new Thickness(0, 0, 0, _rollFoot);
        FitRollRows();
        CuePanel.UpdateLayout();
        HoldRollTitle();
        FillHalos();
        var calls = RollCalls(0);
        var flights = _rollScene!.CallOutExits(calls, _rollStart);
        LogRollCall(calls, flights, _rollStart);
        // The roll call ran in this raid, with animation effects off too (its cue stands still): a later list rings, no cue.
        if (_rollOwner == RollOwner.Raid)
            RecordRollCall(_rollScene);
        for (var i = 0; i < _rollRows.Count; i++)
            _rollRows[i].RiseAt = _rollStart + RollRiseFrom + RollRiseApart * i;
        Lifting(flights, 0, _rollStart, join: false);
        if (!_rollMotion)
        {
            RollFit(animate: false);
            return;
        }
        RollFrames();
        _rollFit ??= OneShot(() => RollFit(animate: true));
        _rollFit.Stop();
        _rollFit.Interval = MapScene.CallOutFitAt;
        _rollFit.Start();
    }

    // The calls for the rows from the index on: each row's triangle centre in the map panel's pixels, its turn its row's.
    private List<ExitCall> RollCalls(int from)
    {
        var scale = Map.XamlRoot?.RasterizationScale ?? 1;
        var calls = new List<ExitCall>();
        for (var i = from; i < _rollRows.Count; i++)
        {
            var at = _rollRows[i].Triangle.TransformToVisual(Map).TransformPoint(RollTriangleCentre);
            calls.Add(new ExitCall(_rollRows[i].Exit.Id, (float)(at.X * scale), (float)(at.Y * scale), i));
        }
        return calls;
    }

    // Each row's hand-off point and lift-off in the app log (developer detail): where the map draws the icon leave from must
    // be the row's triangle, which a script's window.png shows in the map's panel at that place.
    private static void LogRollCall(IReadOnlyList<ExitCall> calls, IReadOnlyList<ExitFlight> flights, DateTime start) =>
        Shturmap.Session.AppLog.Debug("Roll call: " + string.Join("; ", calls.Select(c =>
            FormattableString.Invariant($"{c.MarkerId} row {c.Turn} at {c.FromX:0},{c.FromY:0} px")
            + (flights.FirstOrDefault(f => f.MarkerId == c.MarkerId) is { MarkerId: not null } f
                ? FormattableString.Invariant($", lifts off at {(f.LiftOff - start).TotalSeconds:0.00} s")
                : ""))));

    // The flights the map took (MapScene.CallOutExits gives the ones it added): each row fades as its icon leaves it, and
    // the band's dark goes with the group's rows.
    private void Lifting(IReadOnlyList<ExitFlight> flights, int from, DateTime start, bool join)
    {
        foreach (var flight in flights)
        {
            if (_rollRows.Skip(from).LastOrDefault(r => r.Exit.Id == flight.MarkerId) is { } row)
                row.LiftOff = flight.LiftOff;
        }
        if (flights.Count > 0)
            _rollGroups.Add((start, flights.Min(f => f.LiftOff), flights.Max(f => f.LiftOff), join));
    }

    // Frame by frame while anything on the band moves: the rows' rise and fade, the triangles leaving, the band's dark going
    // and the title's halo taking its place. Worked out from the moments, so a frame that comes late (a snapshot holds the
    // window's thread for most of a second) shows where everything is by then.
    private void RollFrames()
    {
        if (_rollFrames is null)
        {
            _rollFrames = DispatcherQueue.CreateTimer();
            _rollFrames.Interval = TimeSpan.FromMilliseconds(15);
            _rollFrames.Tick += (_, _) => RollFrame();
        }
        RollFrame();
        _rollFrames.Start();
    }

    private void RollFrame()
    {
        var now = DateTime.Now;
        // A developer's snapshot keeps the band up with its rows, as it keeps every cue up, so it can be looked at.
        var keep = SnapshotMode;
        var busy = false;
        foreach (var row in _rollRows)
        {
            var rise = Math.Clamp((now - row.RiseAt) / RollRise, 0, 1);
            var shown = 1 - Math.Pow(1 - rise, 3);
            var gone = !keep && row.LiftOff is { } lift ? Math.Clamp((now - lift) / RollFade, -1, 1) : -1;
            row.Triangle.Opacity = gone >= 0 ? 0 : 1;
            row.Element.Opacity = shown * (1 - Math.Max(0, gone));
            row.Shift.Y = 8 * (1 - shown);
            busy |= rise < 1 || (row.LiftOff is not null && !keep && gone < 1);
        }
        var band = keep ? 1 : RollBand(now);
        CueBand.Opacity = CueRuleTop.Opacity = CueRuleBottom.Opacity = band;
        CueEyebrowHalo.Opacity = CueTitleHalo.Opacity = CueDetailHalo.Opacity = 1 - band;
        busy |= !keep && _rollGroups.Any(g => now < g.LastLift + RollFade + RollBandBack);
        if (!busy)
            _rollFrames?.Stop();
    }

    // The words' halos, the dark the map's labels stand on (MapRenderer's label halo): each word's letters again in the
    // ground's dark, a few DIPs out in eight directions, behind it. Made of the words as they end (the title decodes into
    // the same letters), in the same box with the same room, so each copy breaks its lines where the words do (given the
    // words' width, a copy broke a line a word early).
    private void FillHalos()
    {
        FillHalo(CueEyebrowHalo, CueEyebrow, CueEyebrow.Text, 1.5);
        FillHalo(CueTitleHalo, CueTitle, RollCallText().Title, 3);
        FillHalo(CueDetailHalo, CueDetail, CueDetail.Visibility == Visibility.Visible ? CueDetail.Text : "", 1.5);
    }

    /// <summary>The name each of the halos' copies of the words goes by.</summary>
    internal const string HaloName = "CueHalo";

    private static void FillHalo(Grid halo, TextBlock words, string text, double reach)
    {
        halo.Children.Clear();
        if (text.Length == 0 || words.ActualWidth <= 0)
            return;
        for (var k = 0; k < 8; k++)
        {
            var angle = k * Math.PI / 4;
            halo.Children.Add(new TextBlock
            {
                // The layout check takes its copies for one of its deliberate layers (Dev.LayoutCheck.Layers).
                Name = HaloName, Text = text, FontFamily = words.FontFamily, FontSize = words.FontSize, FontWeight = words.FontWeight, FontStretch = words.FontStretch,
                CharacterSpacing = words.CharacterSpacing, TextAlignment = words.TextAlignment, TextWrapping = words.TextWrapping,
                HorizontalAlignment = words.HorizontalAlignment, MaxWidth = words.MaxWidth, Foreground = Resource("GroundBrush"),
                RenderTransform = new TranslateTransform { X = reach * Math.Cos(angle), Y = reach * Math.Sin(angle) },
            });
        }
    }

    // How much of the band's dark stands: whole until a group's first icon leaves, gone with its last row; a joining list's
    // brings it back for its rows. The title's halo is what is left of it.
    private double RollBand(DateTime now)
    {
        if (_rollGroups.Count == 0)
            return 1;
        double most = 0;
        foreach (var (start, first, last, join) in _rollGroups)
        {
            if (now < start)
                continue;
            var comes = join ? Math.Clamp((now - start) / RollBandBack, 0, 1) : 1;
            var stays = now < first ? 1 : Math.Clamp(1 - (now - first) / (last + RollFade - first), 0, 1);
            most = Math.Max(most, Math.Min(comes, stays));
        }
        return most;
    }

    // The view takes in every extract on the band and the player (MapView.CallOutView, a zoom out only), between the tour's
    // label and band where it is up; over 1.2 s, eased, or at once with animation effects off. Where they are all in view
    // already, the view doesn't move at all (coordinator, 2026-10-15), and a Follow glide under way goes on; else the glide
    // stops for it (MapView.AnimateView). Nothing moves the view back afterwards ("Map drawing", *The view leaves the
    // player*).
    private void RollFit(bool animate)
    {
        if (_rollScene is not { } scene || !ReferenceEquals(Map.Scene, scene) || _rollRows.Count == 0)
            return;
        var fit = Map.CallOutView(_rollRows.Select(r => r.Exit.Position).ToList(), scene.Player?.Position, _rollFoot, _rollPadding, _rollHead);
        if (fit is not { } call)
            return;
        _rollView = call.View;
        if (!call.Moves)
            return;
        if (animate)
            Map.AnimateView(call.View, MapScene.CallOutFitLength);
        else
            Map.Jump(call.View);
    }

    /// <summary>
    /// A list joined the cue on screen: its extracts the band hasn't named (or named lit where it is now marked, or the
    /// other way round) come in as rows after the others, the title counts them all and decodes again, and their icons fly
    /// after the last (MapScene.CallOutExits, called with the whole list: only the new ones come back). The band holds its
    /// place, so the rows still waiting lift off from where they stand. One that lies outside the view the roll call took
    /// has the view take them all in again, the same way (provisional, 2026-10-15: the panel drew no joining list).
    /// </summary>
    private void JoinRollCall(DateTime now)
    {
        var scene = _rollScene!;
        var fresh = scene.ExitsToCall().Where(m => !_rollRows.Any(r => r.Exit.Id == m.Id && r.Listed == scene.ExitsListed.Contains(m.Id))).ToList();
        if (fresh.Count == 0)
            return;
        HoldRollPlace();
        var before = _rollRows.Count;
        foreach (var exit in fresh)
        {
            // The last column fills up to four, then a new one opens, up to three; past that the extract is lit without a
            // row (no map lists that many for a side).
            var column = Array.FindLastIndex(_rollFill, n => n > 0);
            if (column < 0 || _rollMore > 0)
                break;
            if (_rollFill[column] >= RollColumnRows)
                column++;
            if (column >= RollColumnsMost)
                break;
            AddRollRow(exit, column, _rollFill[column]);
        }
        _rollIds.UnionWith(fresh.Select(m => m.Id));
        RecordRollCall(scene);
        if (_rollRows.Count == before)
            return;
        var (_, title, _) = RollCallText();
        // The count grows ("7 OF 12"): laid out anew as it ends, and held there while it decodes.
        LetGoOfRollTitle();
        CueTitle.Text = title;
        CuePanel.UpdateLayout();
        HoldRollTitle();
        FillHalos();
        if (_rollMotion)
            DecodeCueTitle(title);
        for (var i = before; i < _rollRows.Count; i++)
            _rollRows[i].RiseAt = now + RollRiseApart * (i - before);
        var calls = RollCalls(before);
        var flights = scene.CallOutExits(calls, _rollMotion ? now + RollJoinRise : now);
        LogRollCall(calls, flights, now);
        Lifting(flights, before, now, join: true);
        _rollLastJoin = now;
        Study.Ui("cue.join", ("kind", CueKind.ExitsListed), ("extracts", _rollIds.Count));
        var outside = _rollRows.Skip(before).Any(r => !Map.Shows(_rollView ?? Map.View, r.Exit.Position, _rollFoot));
        if (!_rollMotion)
        {
            if (outside)
                RollFit(animate: false);
        }
        else
        {
            RollFrames();
            // Before the view's move the move takes them in anyway.
            if (outside && now - _rollStart >= MapScene.CallOutFitAt)
                RollFit(animate: true);
        }
        HideRollCallAfter();
    }

    // The rows' places hold while the title decodes: its scrambled letters, wider or narrower, would move the band. The
    // title keeps the box it has as it ends.
    private void HoldRollTitle()
    {
        // Two DIPs over its own width: held at that width to the pixel, layout rounding broke its last line a word early.
        CueTitle.Width = Math.Ceiling(CueTitle.ActualWidth) + 2;
        CueTitle.MinHeight = CueTitle.MaxHeight = CueTitle.ActualHeight;
    }

    private void LetGoOfRollTitle()
    {
        CueTitle.Width = double.NaN;
        CueTitle.MinHeight = 0;
        CueTitle.MaxHeight = double.PositiveInfinity;
    }

    // A joining list adds rows: the band stays where it stands and grows down and to the right, so the rows already there
    // don't move under their icons (nor shrink, while the rows still fit the room FitRollRows gave them).
    private void HoldRollPlace()
    {
        if (CuePanel.VerticalAlignment == VerticalAlignment.Top)
            return;
        var top = CuePanel.TransformToVisual(Map).TransformPoint(new Point(0, 0)).Y;
        var stack = CueStack.TransformToVisual(CuePanel).TransformPoint(new Point(0, 0)).X;
        var rows = CueExitsBox.TransformToVisual(CueStack).TransformPoint(new Point(0, 0)).X - CueStack.Padding.Left;
        CuePanel.VerticalAlignment = VerticalAlignment.Top;
        CuePanel.Margin = new Thickness(0, top, 0, 0);
        CueStack.HorizontalAlignment = HorizontalAlignment.Left;
        CueStack.Margin = new Thickness(stack, 0, 0, 0);
        CueExitsBox.HorizontalAlignment = HorizontalAlignment.Left;
        CueExitsBox.Margin = new Thickness(rows, CueExitsBox.Margin.Top, 0, 0);
    }

    // The cue ends 1.5 s after the last icon landed, its title fading as every cue's does; with animation effects off, 5 s
    // after the list (or the last that joined it) came. A developer's snapshot keeps it up.
    private void HideRollCallAfter()
    {
        if (SnapshotMode)
            return;
        _rollHide ??= OneShot(FadeRollCallOut);
        _rollHide.Stop();
        var lifts = _rollRows.Where(r => r.LiftOff is not null).Select(r => r.LiftOff!.Value).ToList();
        var at = _rollMotion && lifts.Count > 0 ? lifts.Max() + MapScene.FlightLength + RollTitleStays : _rollLastJoin + RollStill;
        var left = at - DateTime.Now;
        _rollHide.Interval = left > TimeSpan.Zero ? left : TimeSpan.FromMilliseconds(1);
        _rollHide.Start();
    }

    private void FadeRollCallOut()
    {
        _rollEnding = true;
        _rollFrames?.Stop();
        if (!_rollMotion)
        {
            EndRollCall();
            CuePanel.Visibility = Visibility.Collapsed;
            return;
        }
        var fade = new DoubleAnimation { From = CuePanel.Opacity, To = 0, Duration = TimeSpan.FromSeconds(0.6) };
        Storyboard.SetTarget(fade, CuePanel);
        Storyboard.SetTargetProperty(fade, "Opacity");
        var story = new Storyboard { Children = { fade } };
        story.Completed += (_, _) =>
        {
            if (!ReferenceEquals(story, _cueStory))
                return;
            EndRollCall();
            CuePanel.Visibility = Visibility.Collapsed;
        };
        _cueStory = story;
        story.Begin();
    }

    /// <summary>
    /// The roll call's cue goes or gives way (another cue, the tour's chapter, What's New's preview ending): its timers stop,
    /// its rows go, and the band is the other cues' again. The map's flights and rings carry on: they are the scene's.
    /// </summary>
    private void EndRollCall()
    {
        _rollFrames?.Stop();
        _rollFit?.Stop();
        _rollHide?.Stop();
        _rollRows.Clear();
        _rollGroups.Clear();
        _rollIds.Clear();
        _rollWaiting = [];
        _rollScene = null;
        _rollView = null;
        _rollEnding = false;
        _rollMore = 0;
        Array.Clear(_rollFill);
        CueExits.Children.Clear();
        CueExits.ColumnDefinitions.Clear();
        CueExits.RowDefinitions.Clear();
        CueExitsBox.Visibility = Visibility.Collapsed;
        CueExitsBox.HorizontalAlignment = HorizontalAlignment.Center;
        CueExitsBox.Margin = new Thickness(0, 6, 0, 0);
        CueExitsBox.MaxWidth = CueExitsBox.MaxHeight = double.PositiveInfinity;
        _rollNameWidth = RollNameMost;
        CueBand.Opacity = CueRuleTop.Opacity = CueRuleBottom.Opacity = 1;
        foreach (var halo in new[] { CueEyebrowHalo, CueTitleHalo, CueDetailHalo })
        {
            halo.Children.Clear();
            halo.Opacity = 0;
        }
        LetGoOfRollTitle();
        CueStack.HorizontalAlignment = HorizontalAlignment.Center;
        CueStack.Margin = new Thickness(0);
        CuePanel.VerticalAlignment = VerticalAlignment.Center;
        CuePanel.Margin = new Thickness(0);
    }

    private DispatcherQueueTimer OneShot(Action tick)
    {
        var timer = DispatcherQueue.CreateTimer();
        timer.IsRepeating = false;
        timer.Tick += (_, _) => tick();
        return timer;
    }

    /// <summary>The scene's example list (the tour's, What's New's) called out as a list just read: the map's extracts for
    /// the side, all three sets of it.</summary>
    private void RollCallExample(MapScene scene, RollOwner owner, double foot = 0, double padding = Camera.CallOutPadding, double head = 0) =>
        RollCall(scene, scene.ExitsListed.Count + scene.ExitsUnsure.Count + scene.ExitsNotListed.Count, owner, foot, padding, head);
}
