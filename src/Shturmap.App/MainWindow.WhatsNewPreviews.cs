using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Shturmap.App.Rules;
using Shturmap.Core;
using Shturmap.Core.Maps;
using Shturmap.Core.Operational;
using Shturmap.Map;
using SkiaSharp.Views.Windows;

namespace Shturmap.App;

// What's New's previews of 0.7.0's features (owner, 2026-10-27, D4 B: "each feature should have a icon and a appropriate
// picture or even animation"; docs/DESIGN.md §4, *What's New*): each staged live from made-up data, as the older lines'
// previews are, under the preview label that says it is an example. The ones that move (the desk, the glance, the grid)
// play once in about 4.5 s and hold their end (Rules.WhatsNew.Moves): the app's own motion, cells coming in 50 ms apart,
// totals counting up, a figure decoding, rows rising in. Text size and the dailies are stills: text size on a plate, since
// the window's own text size would move the line out from under the pointer; the dailies as the cue of a Tasks screenshot
// read calls them, each on its map. Nothing of the player's is shown or changed, nothing is bundled (§3): the desk's and
// the glance's pages are the made-up ones the developer runs show (Rules.DeskExample, Rules.GlanceExample), their item
// pictures those the app downloaded.
public sealed partial class MainWindow
{
    // The plate's box at its width as written (MainWindow.xaml): the desk's and the glance's previews widen it.
    private const double PlateWidth = 360;

    // A moving preview's steps, each stopped as the preview ends (StopWhatsNewSteps).
    private readonly List<DispatcherQueueTimer> _whatsNewSteps = [];

    // The grid shown for its preview where the player has it off: put back as the preview ends (PutGridBack).
    private bool _whatsNewGridShown;

    // The desk's page and the glance's plate for their previews, each made once and shown anew: a page of their own, never
    // the player's desk or a raid's plate.
    private Controls.DeskBoard? _whatsNewDesk;
    private Controls.GlancePlate? _whatsNewGlance;

    // A step of a moving preview, after so many seconds, while its preview is the one showing.
    private void WhatsNewAfter(string wanted, double seconds, Action step)
    {
        var timer = WindowTimer(t =>
        {
            t.Stop();
            _whatsNewSteps.Remove(t);
            if (_previewing == wanted)
                step();
        });
        timer.IsRepeating = false;
        timer.Interval = TimeSpan.FromSeconds(Math.Max(0.001, seconds));
        _whatsNewSteps.Add(timer);
        timer.Start();
    }

    private void StopWhatsNewSteps()
    {
        foreach (var timer in _whatsNewSteps)
            timer.Stop();
        _whatsNewSteps.Clear();
    }

    // The plate's box for a control that is its own frame (the desk's page, the glance's plate): no padding, no border,
    // its width the control's.
    private void PlateHolds(FrameworkElement child, double width, bool framed)
    {
        WhatsNewPlateBox.Child = child;
        WhatsNewPlateBox.Width = width + (framed ? 2 : 0);
        WhatsNewPlateBox.Padding = new Thickness(0);
        WhatsNewPlateBox.BorderThickness = new Thickness(framed ? 1 : 0);
        WhatsNewPlateBox.Background = null;
    }

    // ---- the stash desk ----

    // The made-up desk's first reading as a page on the plate, laid out at about the map's size, as the desk takes the map's
    // place: its cells come in 50 ms apart and its totals count up from nothing, as a reading's do (Controls.DeskBoard);
    // then the stash's LEDX is clicked, its frame flashing gold and its figure decoding, as a copy does. The desk itself
    // isn't opened: the card and the line stay under the pointer.
    private void PreviewDesk(string wanted, Shturmap.Data.TarkovDev.GameData data, bool motion)
    {
        if (_snapshot is not { } s)
            return;
        // Laid out at the map's size, the desk's own place, so the page has the desk's columns; the plate draws it smaller.
        var desk = _whatsNewDesk ??= new Controls.DeskBoard { IsHitTestVisible = false };
        var width = Math.Max(360, Map.ActualWidth - 48);
        desk.Width = width;
        desk.Height = Math.Max(260, Map.ActualHeight - 48);
        var view = DeskExample.View(1, s.Mode, 1, id => data.TraderName(id) is { Length: > 0 } name ? name : id, DateTimeOffset.Now);
        PlateHolds(desk, width, framed: true);
        WhatsNewPlateNote.Text = AppTexts.WhatsNewDeskExample;
        WhatsNewPlate.Visibility = Visibility.Visible;
        // From an empty page, so the totals count up from nothing each time, as a first reading's do.
        desk.Show(null, new HashSet<string>(StringComparer.Ordinal), motion: false);
        desk.Show(view, view.Sheet.Regions.Select(r => r.Region.Key).ToHashSet(StringComparer.Ordinal), motion);
        if (!motion)
            return;
        // The page has come in by then (its cells 50 ms apart); the copy, then the end held.
        WhatsNewAfter(wanted, 3.3, () =>
        {
            if ((desk.CellAt("stash", 8, 0) ?? desk.FirstCellOf("stash")) is not { } cell)
                return;
            Controls.DeskBoard.Flash(cell);
            if (cell.FigureBlock is { } block && cell.FigureNumber is { } number)
                Decode(block, number, TimeSpan.FromMilliseconds(600));
        });
    }

    // ---- the in-raid glance ----

    // The made-up glance's first reading (a drawer with three items) on the plate, at the size a raid's plate takes in this
    // map's column: it rises in over the dimmed map as the cue's rows rise, then its figures decode one after another.
    private void PreviewGlance(string wanted, Shturmap.Data.TarkovDev.GameData data, bool motion)
    {
        var plate = _whatsNewGlance ??= new Controls.GlancePlate();
        // Clear of the map's buttons at its right (72 DIP each side), as the plate stands in the middle here, not at the top.
        var width = Math.Clamp(Map.ActualWidth - 2 * 72, 280, 680);
        var height = Math.Max(140, Map.ActualHeight * GlancePlateRules.HeightShare);
        var glance = GlanceExample.Glance(1, 1, id => data.TraderName(id) is { Length: > 0 } name ? name : id, DateTimeOffset.Now);
        plate.Show(glance, width, height);
        PlateHolds(plate, width, framed: false);
        WhatsNewPlateNote.Text = AppTexts.WhatsNewGlanceExample;
        WhatsNewPlate.Visibility = Visibility.Visible;
        if (!motion)
            return;
        // A moment over the dimmed map, as the screenshot is taken; then the plate rises in over 0.35 s.
        WhatsNewPlateBox.Opacity = 0;
        WhatsNewPlateRise.Y = 18;
        WhatsNewAfter(wanted, 0.7, () =>
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var rise = new DoubleAnimation { From = 18, To = 0, Duration = TimeSpan.FromSeconds(0.35), EasingFunction = ease };
            Storyboard.SetTarget(rise, WhatsNewPlateRise);
            Storyboard.SetTargetProperty(rise, "Y");
            var show = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromSeconds(0.35), EasingFunction = ease };
            Storyboard.SetTarget(show, WhatsNewPlateBox);
            Storyboard.SetTargetProperty(show, "Opacity");
            new Storyboard { Children = { rise, show } }.Begin();
        });
        // Its loot's figures decode, 120 ms apart, as a copy's figure does.
        var figures = Cells(plate).Where(c => c.FigureBlock is not null && c.FigureNumber is not null).ToList();
        for (var i = 0; i < figures.Count; i++)
        {
            var cell = figures[i];
            WhatsNewAfter(wanted, 1.15 + i * 0.12, () => Decode(cell.FigureBlock!, cell.FigureNumber!, TimeSpan.FromMilliseconds(600)));
        }
    }

    // The desk's cells drawn under an element (the glance's plate has no board to ask).
    private static IEnumerable<Controls.DeskCellView> Cells(DependencyObject under)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(under); i++)
        {
            var child = VisualTreeHelper.GetChild(under, i);
            if (child is Controls.DeskCellView cell)
            {
                yield return cell;
                continue;
            }
            foreach (var deeper in Cells(child))
                yield return deeper;
        }
    }

    // ---- text size ----

    // A still on the plate: the same three lines, a quest's name, its trader and an extract's map label, at NORMAL and at
    // LARGEST, side by side, drawn by the map's code at each step's sizes (WinUI's curve, Rules.TextSize.Scaled). The
    // window's own text size isn't touched: the rail would be laid out anew and the line move out from under the pointer.
    private void PreviewTextSize(Shturmap.Data.TarkovDev.GameData data)
    {
        var quest = data.Tasks.Values.FirstOrDefault(t => t.NormalizedName == "ballet-lover")
            ?? data.Tasks.Values.OrderBy(t => t.Name, StringComparer.Ordinal).FirstOrDefault(t => t.Trader is not null);
        var customs = data.MapByNormalizedName("customs");
        var extract = customs?.Extracts?.Where(e => e.Name is { Length: > 0 }).OrderBy(e => e.Name, StringComparer.Ordinal).FirstOrDefault()?.Name;
        if (quest is null || extract is null)
            return;
        var trader = quest.Trader is { } id && data.TraderName(id) is { Length: > 0 } name ? name : customs!.Name;
        FeatureGlyphs.TextStepSample Step(string caption, TextStep step)
        {
            var factor = TextSize.Of(step);
            return new(caption, (float)TextSize.Scaled(14, factor), (float)TextSize.Scaled(11.5, factor), (float)TextSize.Scaled(11, factor));
        }
        var scale = (float)Math.Max(2, Content?.XamlRoot?.RasterizationScale ?? 1);
        using var bitmap = FeatureGlyphs.TextSizes(
            [Step(ViewTexts.SettingsTextSizeNormal, TextStep.Normal), Step(ViewTexts.SettingsTextSizeLargest, TextStep.Largest)],
            quest.Name, trader, extract, scale);
        var image = new Image
        {
            Source = bitmap.ToWriteableBitmap(),
            Width = bitmap.Width / scale,
            Height = bitmap.Height / scale,
            Stretch = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        WhatsNewPlateBox.Child = image;
        WhatsNewPlateBox.Width = Math.Max(PlateWidth, image.Width + 40);
        WhatsNewPlateNote.Text = AppTexts.WhatsNewTextSizeExample;
        WhatsNewPlate.Visibility = Visibility.Visible;
    }

    // ---- the squad grid ----

    // Customs with the grid, whether or not the player has it on, and a made-up position that moves from C5's top right
    // into D5's top middle over 3 s: YOU's reference beside the marker and at the map's top left goes from C5-9 to D5-8
    // (the line's own example). The map lights no square (DESIGN.md, *The grid*): the reference says it. With the effects
    // off, YOU stands in D5-8 at once.
    private async Task PreviewGridAsync(string wanted, Shturmap.Data.TarkovDev.GameData data, bool motion)
    {
        if (data.DefinitionFor("customs") is not { } definition)
            return;
        var grid = SquadGrid.For(definition);
        (double X, double Z) At(int column, int row, double across, double down) =>
            grid.World(grid.OriginU + column * SquadGrid.Square + across, grid.OriginV + (row - 1) * SquadGrid.Square + down);
        var from = At(2, 5, 78, 14);
        var to = At(3, 5, 50, 15);
        if (!Map.ShowGrid)
        {
            _whatsNewGridShown = true;
            Map.ShowGrid = true;
            PlaceInsideGrid();
        }
        var taken = DateTime.Now;
        double y = 0;
        WorldPoint Point((double X, double Z) p) => new(p.X, y, p.Z);
        await PreviewSceneAsync(wanted, data, "customs", [], (scene, content) =>
        {
            y = content.Markers.FirstOrDefault(m => m.Kind is MarkerKind.ExtractPmc or MarkerKind.ExtractShared)?.Position.Y ?? 0;
            scene.Player = new PlayerFix(Point(motion ? from : to), null, taken);
        }, _ => [Point(from), Point(to)], 320);
        if (_previewing != wanted || !motion)
            return;
        // A moment still, then 3 s along, eased in and out; the map drawn anew each frame, the position never pinging.
        var start = DateTime.Now + TimeSpan.FromSeconds(1);
        var walk = TimeSpan.FromSeconds(3);
        var step = WindowTimer(t =>
        {
            if (_previewing != wanted || Map.Scene is not { } scene)
            {
                t.Stop();
                return;
            }
            var p = Math.Clamp((DateTime.Now - start) / walk, 0, 1);
            var eased = p * p * (3 - 2 * p);
            scene.Player = new PlayerFix(Point((from.X + (to.X - from.X) * eased, from.Z + (to.Z - from.Z) * eased)), null, taken);
            Map.Redraw();
            if (p >= 1)
                t.Stop();
        });
        step.Interval = TimeSpan.FromMilliseconds(33);
        _whatsNewSteps.Add(step);
        step.Start();
    }

    // The grid as the player has it, after a preview that showed it for them.
    private void PutGridBack()
    {
        if (!_whatsNewGridShown)
            return;
        _whatsNewGridShown = false;
        Map.ShowGrid = ViewModel.MapGrid;
        PlaceInsideGrid();
    }

    // ---- dailies and weeklies ----

    /// <summary>
    /// Three made-up operational tasks for What's New's preview, each with its map as Plan counts maps: a daily's survival
    /// on Streets of Tarkov ending in 25 minutes, a daily's kill on Customs in 8 hours, a weekly's hand-over on any map in
    /// two days. The objectives' words are the game's own as a Tasks screenshot shows them (English game), which the app
    /// words anew (OperationalWords); nobody's real tasks, and never written to the player's file.
    /// </summary>
    internal static IReadOnlyList<OperationalShown> TasksExample(Shturmap.Data.TarkovDev.GameData data, DateTimeOffset now)
    {
        OperationalShown Task(string name, string? map, OperationalKind kind, TimeSpan left, int? progress, params OperationalObjective[] objectives)
        {
            var known = map is null ? null : data.MapByNormalizedName(map);
            var task = new OperationalTask(name, known?.Id, objectives, kind, progress, now + left, now, now);
            return new OperationalShown(task, known?.NormalizedName, []);
        }
        return
        [
            Task("Exit the location", "streets-of-tarkov", OperationalKind.Survive, TimeSpan.FromMinutes(25), 57,
                new OperationalObjective("Survive and extract from the location", 4, 7)),
            Task("Elimination", "customs", OperationalKind.Kill, new TimeSpan(8, 14, 11), 0,
                new OperationalObjective("Eliminate Scavs with a headshot from a distance of more than 50m", 0, 3)),
            Task("Find and transfer", null, OperationalKind.HandOver, new TimeSpan(2, 3, 0, 0), null,
                new OperationalObjective("Hand over the found in raid item: \"SSD drive\"")),
        ];
    }
}
