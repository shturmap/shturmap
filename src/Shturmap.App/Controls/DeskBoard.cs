using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Shturmap.App.Rules;
using Shturmap.Core.Desk;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Shturmap.App.Controls;

/// <summary>
/// The stash desk's page, in the map's place out of a raid (owner, 2026-10-19, from a panel: A1 · B1 · C1 · D2, and the
/// addendum: A(2) · B1 · C1a · Da; owner, 2026-10-25, decision 1 of the layout round: A, the GEAR tab mirrored and fixed;
/// docs/DESIGN.md §4, "Screen anatomy", *The stash desk*): the head line over the page, then the game's GEAR tab's shape
/// in three columns, every region in its place whether or not the last screenshot showed it: LOADOUT as the game's slot
/// grid, each box under its slot's name; TACTICAL RIG, POCKETS beside SPECIAL SLOTS, BACKPACK and POUCH, a container's
/// slot box beside its grid; STASH; the windows in a fourth column or under the stash (<see cref="DeskPageRules.Layout"/>:
/// the cell's size by the desk's size alone, 40 to 128 DIP). A region no reading showed is its head over a dashed frame
/// ("NOT IN A SCREENSHOT YET"); one the last screenshot didn't show is drawn a step dimmer, its head saying since when
/// ("BACKPACK · 23:08"). A click on a cell copies its list price, a right-click pins its card. Each cell mirrors the
/// screenshot's, drawn from the catalogue's art with its figure, count, keep notch, change and tint (<see cref="DeskCells"/>);
/// a container's open window stands under "ZHUK · IN THE BACKPACK", stemmed to its cell, and pointing at either lights the
/// other. What a reading changed comes in cell by cell, 50 ms apart, as the kit's pictures do, and the totals count up
/// once; nothing else moves. Built in code, as the map's panels are: the page is rebuilt for each snapshot that changes it.
/// Until 2026-10-25 the regions a reading didn't change folded into one line and the page flowed into as many columns as
/// filled the desk.
/// </summary>
public sealed partial class DeskBoard : Grid
{
    /// <summary>The pointer came onto a cell: the rail shows its card.</summary>
    public event Action<DeskCellView>? CellPointed;

    /// <summary>A cell was clicked: the rail copies its list price (*The click*).</summary>
    public event Action<DeskCellView>? CellClicked;

    /// <summary>A cell was right-clicked: its card is pinned in the rail, or unpinned.</summary>
    public event Action<DeskCellView>? CellPinToggled;

    // The cells whose cards are pinned in the rail: their tab on the page.
    private IReadOnlySet<DeskCellKey> _pinned = new HashSet<DeskCellKey>();

    private static readonly TimeSpan Ease = TimeSpan.FromSeconds(0.18);
    private static readonly TimeSpan CountUpLength = TimeSpan.FromSeconds(0.9);
    private const double Gap = 18;

    // The head line's margins: 14 DIP over it, 4 under it, the page's lane under that (DeskPageRules.Lane).
    private const double HeadTop = 14, HeadBottom = 4;

    private readonly TextBlock _head;
    private readonly TextBlock _empty;
    private readonly ScrollViewer _scroll;
    private readonly Grid _more;
    private TextBlock? _moreWords;
    private readonly Grid _page;
    private readonly Grid _columns;
    private readonly Canvas _stems;
    private readonly List<Link> _links = [];
    private readonly List<DeskCellView> _cellViews = [];
    private readonly DispatcherQueueTimer _countUp;

    // A container's grid and the slot box beside it: a stem leaving the grid on the box's side goes round the box.
    private readonly Dictionary<Canvas, FrameworkElement> _boxes = [];

    private DeskView? _view;
    private string _builtShape = "";
    private bool _stemsDue;
    private (double OnYou, double Stash) _from;
    private (double OnYou, double Stash) _shown;
    private DateTime _countStart;

    // A window and the cell its container is: the head, its region's panel, the cell, and the stem between them.
    private sealed record Link(FrameworkElement Head, FrameworkElement Panel, Border HeadLight, DeskCellView Anchor, Path Stem);

    public DeskBoard()
    {
        Background = Brush("GroundBrush");
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _head = new TextBlock
        {
            Style = (Style)Application.Current.Resources["StatusText"],
            Foreground = Brush("MutedBrush"),
            Margin = new Thickness(Gap, HeadTop, Gap, HeadBottom),
            TextWrapping = TextWrapping.Wrap,
        };
        Children.Add(_head);
        _empty = new TextBlock
        {
            Style = (Style)Application.Current.Resources["NoteText"],
            Text = AppTexts.DeskEmpty,
            Margin = new Thickness(Gap, 18, Gap, 0),
            Visibility = Visibility.Collapsed,
        };
        Children.Add(_empty);
        _columns = new Grid { ColumnSpacing = DeskPageRules.ColumnGap, HorizontalAlignment = HorizontalAlignment.Left };
        _stems = new Canvas { IsHitTestVisible = false };
        _page = new Grid { Padding = new Thickness(Gap, DeskPageRules.Lane, Gap, DeskPageRules.PageBottom) };
        _page.Children.Add(_columns);
        _page.Children.Add(_stems);
        _scroll = new ScrollViewer
        {
            Content = _page,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Disabled,
            FontFamily = (FontFamily)Application.Current.Resources["UiFont"],
            Foreground = Brush("InkBrush"),
        };
        SetRow(_scroll, 1);
        Children.Add(_scroll);
        _more = MoreBelowMark();
        SetRow(_more, 1);
        Children.Add(_more);
        _scroll.ViewChanged += (_, _) => ShowMoreBelow();
        _scroll.SizeChanged += (_, _) => ShowMoreBelow();
        _page.SizeChanged += (_, _) => ShowMoreBelow();
        SizeChanged += (_, _) =>
        {
            // The cell's size and the columns follow the desk's width and height (DeskPageRules): a new layout only when
            // they change.
            if (_view is { } view && LayoutNow(view).Shape != _builtShape)
                Build(entering: null);
            _stemsDue = true;
        };
        _page.LayoutUpdated += (_, _) =>
        {
            if (_stemsDue && _page.ActualWidth > 0)
                DrawStems();
        };
        _countUp = DispatcherQueue.CreateTimer();
        _countUp.Interval = TimeSpan.FromMilliseconds(33);
        _countUp.Tick += (_, _) => CountUpTick();
        // The cells' marks are laid out by the size the text is drawn at: anew when it changes.
        TextScale.Changed += () =>
        {
            if (_view is not null)
                Build(entering: null);
        };
    }

    /// <summary>The app is ending (MainWindow.StopForExit): the totals' count-up stops, so no tick comes after the window's
    /// XAML is down (SafetyTests.Timers).</summary>
    public void Stop() => _countUp.Stop();

    /// <summary>The page on screen, for the layout check and the developer view.</summary>
    public DeskView? View => _view;

    /// <summary>
    /// Shows a page. With <paramref name="motion"/>, the cells of <paramref name="entering"/>'s regions come in, 50 ms
    /// apart, and the totals count up from what the head line said (from nothing on the page's first showing); without,
    /// the page stands at once (animation effects off, a snapshot run).
    /// </summary>
    public void Show(DeskView? view, IReadOnlySet<string> entering, bool motion)
    {
        var had = _view is not null;
        _view = view;
        var cells = Build(motion ? entering : null);
        if (view is null)
            return;
        var to = (view.Sheet.Totals.OnYou, view.Sheet.Totals.Stash);
        if (motion && entering.Count > 0 && to != _shown)
        {
            _from = had ? _shown : (0, 0);
            _countStart = DateTime.Now;
            _countUp.Start();
            HeadAt(_from);
        }
        else
        {
            _countUp.Stop();
            HeadAt(to);
        }
        if (cells.Count > 0)
            Enter(cells);
    }

    /// <summary>The cell at a region's grid place or slot, for the developer script's "desk point".</summary>
    public DeskCellView? CellAt(string region, int i, int j) =>
        _cellViews.FirstOrDefault(c => c.Region.Key == region && (c.Sheet.Cell.IsSlot ? i < 0 : c.Sheet.Cell.Rect.Contains(new DeskRect(i, j, 1, 1))));

    /// <summary>The first cell of a region (its slot, else its first grid cell), for the developer script.</summary>
    public DeskCellView? FirstCellOf(string region) => _cellViews.FirstOrDefault(c => c.Region.Key == region);

    /// <summary>Points at a cell as the pointer does: its card in the rail, and a window's or its cell's light. The page
    /// scrolls to the cell first, as the pointer could only reach it in view (the developer script's "desk point").</summary>
    public void Point(DeskCellView? cell)
    {
        cell?.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false, VerticalAlignmentRatio = 0.15 });
        foreach (var link in _links)
            Light(link, cell is not null && (ReferenceEquals(link.Anchor, cell) || link.Panel == PanelOf(cell)));
        if (cell is not null)
            CellPointed?.Invoke(cell);
    }

    /// <summary>PgUp and PgDn: the page up or down by most of what shows.</summary>
    public void PageBy(int direction)
    {
        if (_scroll.Visibility == Visibility.Visible)
            _scroll.ChangeView(null, Math.Max(0, _scroll.VerticalOffset + direction * _scroll.ViewportHeight * 0.85), null, !Settings.AnimationsEnabled);
    }

    // ---- the scroll mark ----

    /// <summary>The scroll mark's name: a layer over the page, its words compared with none under it (Dev.LayoutCheck.Layers).</summary>
    public const string MoreBelowName = "DeskMoreBelow";

    // The fade of the page under the mark, and the band of the desk's ground the mark's line stands on, in DIP.
    private const double MoreFade = 16;

    // Where the page runs on past the desk's foot (DeskPageRules.MoreBelow; UI QA pass of step 2, F3; the coordinator's
    // rule, the owner's to overrule): at the desk's foot, over the page, a muted line in the eyebrows' style, "MORE BELOW ▾",
    // on the desk's ground with a short fade of the page under it; the scroll bar's lane at the right left free. It never
    // takes the pointer, and it shows and goes without motion (the desk moves three ways and no more).
    private Grid MoreBelowMark()
    {
        var ground = (Windows.UI.Color)Application.Current.Resources["GroundColor"];
        var clear = Windows.UI.Color.FromArgb(0, ground.R, ground.G, ground.B);
        var mark = new Grid
        {
            Name = MoreBelowName,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 16, 0),
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
        };
        mark.RowDefinitions.Add(new RowDefinition { Height = new GridLength(MoreFade) });
        mark.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var fade = new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0), EndPoint = new Point(0, 1),
                GradientStops = { new GradientStop { Color = clear, Offset = 0 }, new GradientStop { Color = ground, Offset = 1 } },
            },
        };
        mark.Children.Add(fade);
        var line = new Border { Background = Brush("GroundBrush") };
        SetRow(line, 1);
        _moreWords = new TextBlock
        {
            Style = (Style)Application.Current.Resources["EyebrowText"],
            Margin = new Thickness(Gap, 0, Gap, 10),
            TextWrapping = TextWrapping.NoWrap,
        };
        line.Child = _moreWords;
        mark.Children.Add(line);
        return mark;
    }

    // The mark where the page runs on past the desk's foot, gone at the page's end and where it fits; its words as built
    // (a language switch builds the page anew).
    private void ShowMoreBelow()
    {
        var more = _scroll.Visibility == Visibility.Visible && _scroll.ViewportHeight > 0
                   && DeskPageRules.MoreBelow(_scroll.ExtentHeight, _scroll.ViewportHeight, _scroll.VerticalOffset);
        var shown = more ? Visibility.Visible : Visibility.Collapsed;
        if (_more.Visibility != shown)
            _more.Visibility = shown;
    }

    /// <summary>Whether the scroll mark stands at the desk's foot (the developer view and the layout check).</summary>
    public bool MoreBelowShown => _more.Visibility == Visibility.Visible;

    /// <summary>The page in screens (DeskPageRules.ScreenOffsets), for the layout check: the offsets to scroll it to, the
    /// first its top; one where it fits or isn't up.</summary>
    public IReadOnlyList<double> Screens()
    {
        if (_scroll.Visibility != Visibility.Visible || _scroll.ViewportHeight <= 0)
            return [0];
        var band = _more.ActualHeight > 0 ? _more.ActualHeight : MoreFade + 24;
        return DeskPageRules.ScreenOffsets(_scroll.ExtentHeight, _scroll.ViewportHeight, band);
    }

    /// <summary>How far the page is scrolled down.</summary>
    public double ScrolledTo => _scroll.VerticalOffset;

    /// <summary>Scrolls the page to an offset at once and waits for the view to settle there (a second at the most), with
    /// the scroll mark following it: the layout check's screens.</summary>
    public async Task ScrollToAsync(double offset)
    {
        if (_scroll.Visibility != Visibility.Visible)
            return;
        var settled = new TaskCompletionSource();
        void Done(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            if (!e.IsIntermediate)
                settled.TrySetResult();
        }
        _scroll.ViewChanged += Done;
        try
        {
            if (!_scroll.ChangeView(null, offset, null, disableAnimation: true) || Math.Abs(_scroll.VerticalOffset - offset) < 0.5)
                settled.TrySetResult();
            await Task.WhenAny(settled.Task, Task.Delay(1000));
        }
        finally
        {
            _scroll.ViewChanged -= Done;
        }
        _scroll.UpdateLayout();
        ShowMoreBelow();
    }

    private FrameworkElement? PanelOf(DeskCellView cell) => _links.FirstOrDefault(l => l.Panel.Tag is string key && key == cell.Region.Key)?.Panel;

    private double DeskWidth() => ActualWidth > 0 ? ActualWidth : 516;

    // The page's room under the head line: the desk's height less the line and its margins (one line until it is laid
    // out); unknown before the desk is, so the width alone decides.
    private double DeskHeight() =>
        ActualHeight > 0 ? ActualHeight - (_head.ActualHeight > 0 ? _head.ActualHeight : Math.Ceiling(TextScale.Drawn(12) * 1.4)) - HeadTop - HeadBottom
        : double.PositiveInfinity;

    // The size text is drawn at against the size written, for the heads' and lines' heights.
    private static double TextFactor => TextScale.Drawn(12) / 12;

    // The cell's size and the columns for the page as it stands now (DeskPageRules.Layout), each head with the room of its
    // dated form whether it is dated or not, so a date never changes the layout (UI QA pass of step 2, F1).
    private DeskPageRules.Arrangement LayoutNow(DeskView view) =>
        DeskPageRules.Layout(view.Sheet.Regions.Select(r => r.Region).ToList(), DeskWidth(), DeskHeight(), TextFactor,
            r => DeskPageRules.Head(r, view.Sheet.Page));

    // ---- the page ----

    // The page anew: its columns, regions and placeholders, and cells; the cells of the entering regions, in reading order.
    private List<DeskCellView> Build(IReadOnlySet<string>? entering)
    {
        _columns.Children.Clear();
        _columns.ColumnDefinitions.Clear();
        _stems.Children.Clear();
        _links.Clear();
        _cellViews.Clear();
        _boxes.Clear();
        var entered = new List<DeskCellView>();
        if (_view is not { Sheet.Regions.Count: > 0 } view)
        {
            _head.Visibility = Visibility.Collapsed;
            _scroll.Visibility = Visibility.Collapsed;
            _more.Visibility = Visibility.Collapsed;
            // Said with each build: a language switch builds the page anew (MainWindow.RetellDesk).
            _empty.Text = AppTexts.DeskEmpty;
            _empty.Visibility = Visibility.Visible;
            _builtShape = "";
            return entered;
        }
        _head.Visibility = Visibility.Visible;
        _scroll.Visibility = Visibility.Visible;
        _empty.Visibility = Visibility.Collapsed;
        if (_moreWords is not null)
            _moreWords.Text = RuleTexts.DeskMoreBelow;

        var now = DateTimeOffset.Now;
        var page = view.Sheet.Page;
        var sheets = view.Sheet.Regions.ToDictionary(r => r.Region.Key, StringComparer.Ordinal);
        var arrangement = LayoutNow(view);
        var size = arrangement.Cell;
        _builtShape = arrangement.Shape;
        // The columns: LOADOUT, the middle column, the stash, the windows' where they stand beside it (DeskPageRules.Arrange).
        var panels = new StackPanel[arrangement.ColumnWidths.Count];
        for (var c = 0; c < panels.Length; c++)
        {
            _columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(arrangement.ColumnWidths[c]) });
            panels[c] = new StackPanel();
            SetColumn(panels[c], c);
            _columns.Children.Add(panels[c]);
        }

        var windows = new List<(DeskRegion Window, FrameworkElement Head, FrameworkElement Panel, Border Light)>();
        StackPanel? pair = null;
        for (var b = 0; b < arrangement.Blocks.Count; b++)
        {
            var block = arrangement.Blocks[b];
            var column = arrangement.Columns[b];
            var panel = panels[column];
            var top = panel.Children.Count == 0 ? 0 : DeskPageRules.BlockGap;
            // POCKETS and SPECIAL SLOTS on one row, as the game has them, each under its own head and as wide as its grid.
            var paired = block.Kind is DeskRegionKind.Pockets or DeskRegionKind.SpecialSlots;
            var width = paired ? DeskPageRules.BlockWidth(block, size) : arrangement.ColumnWidths[column];
            FrameworkElement element;
            if (block.Region is { } region && sheets.TryGetValue(region.Key, out var sheet))
            {
                var (made, head, light) = RegionBlock(sheet, page, now, size, width, entering, entered);
                element = made;
                if (region is { Kind: DeskRegionKind.ContainerWindow, AttachedTo: not null })
                    windows.Add((region, head, made, light));
            }
            else
                element = Placeholder(block, size, width);
            if (block.Kind == DeskRegionKind.Pockets)
            {
                pair = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DeskPageRules.RowGap, Margin = new Thickness(0, top, 0, 0) };
                pair.Children.Add(element);
                panel.Children.Add(pair);
            }
            else if (block.Kind == DeskRegionKind.SpecialSlots && pair is not null)
            {
                pair.Children.Add(element);
                Level(pair);
            }
            else
            {
                element.Margin = new Thickness(0, top, 0, 0);
                panel.Children.Add(element);
            }
        }

        // Each attached window and its container's cell.
        foreach (var (window, head, panel, light) in windows)
        {
            if (_cellViews.FirstOrDefault(c => window.AttachedTo!.Is(c.Region, c.Sheet.Cell)) is not { } anchor)
                continue;
            var stem = new Path { Stroke = Brush("LineStrongBrush"), StrokeThickness = 1, IsHitTestVisible = false };
            _stems.Children.Add(stem);
            var link = new Link(head, panel, light, anchor, stem);
            _links.Add(link);
            anchor.PointerEntered += (_, _) => Light(link, true);
            anchor.PointerExited += (_, _) => Light(link, false);
            panel.PointerEntered += (_, _) => Light(link, true);
            panel.PointerExited += (_, _) => Light(link, false);
        }
        _stemsDue = true;
        foreach (var cell in _cellViews)
            cell.Pinned = _pinned.Contains(cell.Key);
        return entered;
    }

    // A region under its head (dated where the last screenshot didn't show it, the block then a step dimmer): the muted
    // "Not attached" line of a window attached to none; LOADOUT's slot grid; a container's slot box beside its grid; a grid.
    private (FrameworkElement Block, FrameworkElement Head, Border Light) RegionBlock(DeskRegionSheet sheet, DeskPage page, DateTimeOffset now, double size,
        double width, IReadOnlySet<string>? entering, List<DeskCellView> entered)
    {
        var region = sheet.Region;
        var block = new StackPanel
        {
            Spacing = DeskPageRules.HeadGap, Tag = region.Key, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
        };
        var head = Head(DeskPageRules.HeadOf(region, page, now), size, width);
        // The linked highlight's gold behind the head of a window whose cell is pointed at, as wide as the words.
        var light = new Border { Background = Brush("LinkBrush"), Opacity = 0, IsHitTestVisible = false, Margin = new Thickness(-4, -2, -4, -2) };
        var lit = new Grid { HorizontalAlignment = HorizontalAlignment.Left };
        lit.Children.Add(light);
        lit.Children.Add(head);
        block.Children.Add(HeadCell(DeskPageRules.Head(region, page), lit, size, width));
        if (DeskPageRules.NotAttached(sheet) is { } line)
            block.Children.Add(new TextBlock { Style = (Style)Application.Current.Resources["NoteText"], Text = line, MaxWidth = width });

        var comes = entering?.Contains(region.Key) == true;
        if (region.Kind == DeskRegionKind.Loadout)
            block.Children.Add(Loadout(region, sheet, size, comes, entered, dashed: false));
        else if (DeskPageRules.HasBox(region.Kind))
        {
            // The container's own slot box at the left and its grid right of it, on one row, as the game draws them.
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DeskPageRules.BoxGap(size) };
            var box = Box(region, sheet.Cells.FirstOrDefault(c => c.Cell.IsSlot), DeskPageRules.Box(size), DeskPageRules.Box(size), size, comes, entered,
                dashed: false);
            row.Children.Add(box);
            if (GridOf(region, sheet, size, comes, entered) is { } grid)
            {
                row.Children.Add(grid);
                _boxes[grid] = box;
            }
            block.Children.Add(row);
        }
        else if (GridOf(region, sheet, size, comes, entered) is { } grid)
            block.Children.Add(grid);
        // A region the last screenshot didn't show: whole, a step dimmer; its cards and clicks as ever.
        if (DeskPageRules.Kept(region, page))
            block.Opacity = DeskPageRules.KeptOpacity;
        return (block, head, light);
    }

    // POCKETS and SPECIAL SLOTS side by side: their heads' cells as tall as the taller, so their grids stand level as the
    // game's do where one head takes more lines than the other (SPECIAL SLOTS, three cells wide, dated "· 1 JAN 14:02").
    private static void Level(StackPanel pair)
    {
        var heads = pair.Children.OfType<Panel>().Select(b => b.Children.FirstOrDefault() as Grid).OfType<Grid>()
            .Select(cell => (Cell: cell, Texts: Texts(cell).ToList())).Where(h => h.Texts.Count > 0).ToList();
        if (heads.Count < 2)
            return;
        void Even()
        {
            var tallest = heads.Max(h => h.Texts.Max(t => t.ActualHeight));
            foreach (var (cell, _) in heads)
                cell.MinHeight = tallest;
        }
        foreach (var text in heads.SelectMany(h => h.Texts))
            text.SizeChanged += (_, _) => Even();

        static IEnumerable<TextBlock> Texts(Panel panel) =>
            panel.Children.SelectMany(c => c is Panel inner ? Texts(inner) : c is TextBlock t ? [t] : Enumerable.Empty<TextBlock>());
    }

    // A region's head, in the eyebrows' style at the page's size, wrapping in its column.
    private static TextBlock Head(string text, double size, double width) => new()
    {
        Style = (Style)Application.Current.Resources["EyebrowText"],
        Margin = new Thickness(0),
        Text = text,
        FontSize = DeskPageRules.HeadSize(size),
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = width,
    };

    // A head's cell (UI QA pass of step 2, F1: SPECIAL SLOTS' head, dated by reading 2, wrapped to a second line and moved
    // every row under it 13 px down): the head as drawn, and under it, unseen, the same head in its dated form at its
    // widest (DeskPageRules.Reserved: "SPECIAL SLOTS · 28 SEP 23:58"), wrapping alike, so the cell takes the dated head's
    // lines from the first reading, at the size the text is drawn, and nothing under a head moves when its date comes. The
    // copy is transparent, takes no pointer and says nothing to a screen reader; the layout check passes over it. The head
    // as drawn stands at the cell's foot, right over its grid, the room it keeps above it (UI QA re-check, 2026-10-25: at
    // the cell's top POCKETS and SPECIAL SLOTS stood 23 px over their grids on the first reading, where every other head
    // stands 10 to 11, and read as cut off from them): where the date wraps a head, the name's line goes up into the room
    // and the date's line stands where the name stood.
    private static Grid HeadCell(string head, FrameworkElement drawn, double size, double width)
    {
        var cell = new Grid { HorizontalAlignment = HorizontalAlignment.Left };
        var room = Head(DeskPageRules.Reserved(head), size, width);
        room.Opacity = 0;
        room.IsHitTestVisible = false;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAccessibilityView(room, Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Raw);
        cell.Children.Add(room);
        drawn.VerticalAlignment = VerticalAlignment.Bottom;
        cell.Children.Add(drawn);
        return cell;
    }

    // A fixed region no reading showed (owner, 2026-10-25): its head, and in its place a dashed frame of five cells by two
    // (POCKETS four, SPECIAL SLOTS three; DeskPageRules.Placeholder) with "NOT IN A SCREENSHOT YET" (a container's empty
    // slot box beside it); LOADOUT its slot grid of dashed boxes under the words.
    private FrameworkElement Placeholder(DeskBlock block, double size, double width)
    {
        var panel = new StackPanel
        {
            Spacing = DeskPageRules.HeadGap, Tag = block.Key, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
        };
        var name = DeskPageRules.Name(block.Kind);
        panel.Children.Add(HeadCell(name, Head(name, size, width), size, width));
        if (block.Kind == DeskRegionKind.Loadout)
        {
            panel.Children.Add(new TextBlock { Style = (Style)Application.Current.Resources["NoteText"], Text = RuleTexts.DeskNotRead, MaxWidth = width });
            panel.Children.Add(Loadout(null, null, size, false, [], dashed: true));
        }
        else if (DeskPageRules.HasBox(block.Kind))
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DeskPageRules.BoxGap(size) };
            row.Children.Add(EmptyBox(DeskPageRules.Box(size), DeskPageRules.Box(size), dashed: false));
            row.Children.Add(NotRead(block.Kind, size));
            panel.Children.Add(row);
        }
        else
            panel.Children.Add(NotRead(block.Kind, size));
        return panel;
    }

    // The dashed frame of a region never read, the words in it.
    private static FrameworkElement NotRead(DeskRegionKind kind, double size)
    {
        var (cols, rows) = DeskPageRules.Placeholder(kind);
        var frame = new Grid
        {
            Width = cols * size + 1, Height = rows * size + 1,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
        };
        frame.Children.Add(Dashed());
        frame.Children.Add(new TextBlock
        {
            Style = (Style)Application.Current.Resources["NoteText"],
            Text = RuleTexts.DeskNotRead,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6),
        });
        return frame;
    }

    private static Rectangle Dashed() => new()
    {
        Stroke = Brush("LineStrongBrush"), StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 4, 3 }, IsHitTestVisible = false,
    };

    // A slot's box with nothing read in it: the cell's ground in its frame, as the game's empty slot; dashed, a slot never
    // read (LOADOUT's placeholder).
    private static FrameworkElement EmptyBox(double width, double height, bool dashed)
    {
        var box = new Grid { Width = width, Height = height, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        box.Children.Add(dashed ? Dashed() : new Border { Background = Brush("CellBrush"), BorderBrush = Brush("LineStrongBrush"), BorderThickness = new Thickness(1) });
        return box;
    }

    // A slot's box: its cell at the box's size, or an empty box.
    private FrameworkElement Box(DeskRegion region, DeskCellSheet? slot, double width, double height, double size, bool comes, List<DeskCellView> entered,
        bool dashed)
    {
        if (slot is null)
            return EmptyBox(width, height, dashed);
        var cell = new DeskCellView(region, slot, size, this, default, new Size(width, height)) { VerticalAlignment = VerticalAlignment.Top };
        _cellViews.Add(cell);
        if (comes)
            entered.Add(cell);
        return cell;
    }

    // LOADOUT as the game's GEAR tab draws it (DeskPageRules.LoadoutBoxes): each slot's box under its name, fitted to the
    // box's width (DeskPageRules.SlotHead), its item drawn as a cell at the box's size, an empty box where the slot is
    // empty; dashed boxes for a LOADOUT never read.
    private Canvas Loadout(DeskRegion? region, DeskRegionSheet? sheet, double size, bool comes, List<DeskCellView> entered, bool dashed)
    {
        var scale = TextFactor;
        var room = DeskPageRules.SlotHeadRoom(size, scale);
        var canvas = new Canvas
        {
            Width = DeskPageRules.LoadoutCells * size + 1, Height = DeskPageRules.LoadoutHeight(size, scale), HorizontalAlignment = HorizontalAlignment.Left,
        };
        var rows = new Dictionary<double, List<TextBlock>>();
        foreach (var place in DeskPageRules.LoadoutBoxes(size, scale))
        {
            var name = DeskCards.SlotName(place.Slot) ?? "";
            var (type, spacing) = DeskPageRules.SlotHead(name, place.Width, size, TextScale.Drawn, (at, ems) => NameWidth(name, at, ems));
            var eyebrow = new TextBlock
            {
                Style = (Style)Application.Current.Resources["EyebrowText"],
                Margin = new Thickness(0),
                Text = name,
                FontSize = type,
                // A name without its spacing keeps a thousandth of an em: WinUI lays a text of no spacing out otherwise, its
                // baseline a pixel lower than its row's at the same size and its BaselineOffset saying 0.2 DIP higher
                // (measured, 2026-10-25), so it could stand on no common baseline (UI QA pass of step 2, F5).
                CharacterSpacing = Math.Max(1, spacing),
                TextWrapping = TextWrapping.NoWrap,
            };
            if (type < DeskPageRules.SlotHeadSize(size) || spacing == 0)
                Shturmap.Session.AppLog.Debug(FormattableString.Invariant(
                    $"Desk slot name {place.Slot} \"{name}\": {type:0.###} px as written, drawn {TextScale.Drawn(type):0.##}, spacing {spacing / 1000.0:0.##} em, over a box of {place.Width:0} DIP"));
            Canvas.SetLeft(eyebrow, place.X);
            Canvas.SetTop(eyebrow, place.Y - room);
            canvas.Children.Add(eyebrow);
            if (!rows.TryGetValue(place.Y, out var row))
                rows[place.Y] = row = [];
            row.Add(eyebrow);
            var slot = sheet?.Cells.FirstOrDefault(c => c.Cell.IsSlot && c.Cell.Slot == place.Slot);
            var box = region is null ? EmptyBox(place.Width, place.Height, dashed) : Box(region, slot, place.Width, place.Height, size, comes, entered, dashed);
            Canvas.SetLeft(box, place.X);
            Canvas.SetTop(box, place.Y);
            canvas.Children.Add(box);
        }
        foreach (var (y, row) in rows)
        {
            foreach (var eyebrow in row)
                eyebrow.SizeChanged += (_, _) => OnOneBaseline(row, y - room);
        }
        return canvas;
    }

    // The names of a row of boxes on one baseline whatever their size (UI QA pass of step 2, F5: "GESICHTSBEDECKUNG", set
    // smaller to fit its box, stood a pixel lower than its row's names at 100 % and two higher at 125 %): each name's top
    // moved down by what its baseline stands above the row's lowest, measured as drawn, so the names of the row's size stay
    // where they were. Again whenever a name's size changes (the window's text size).
    private static void OnOneBaseline(IReadOnlyList<TextBlock> row, double top)
    {
        if (row.Any(t => t.BaselineOffset <= 0))
            return;
        var baseline = row.Max(t => t.BaselineOffset);
        foreach (var eyebrow in row)
        {
            var at = top + baseline - eyebrow.BaselineOffset;
            if (Math.Abs(Canvas.GetTop(eyebrow) - at) > 0.01)
                Canvas.SetTop(eyebrow, at);
        }
    }

    // A slot's name's width as drawn, at a size as drawn and a spacing in ems (DeskPageRules.SlotHead's fit): measured on a
    // copy in the eyebrows' style, which the window's text size doesn't scale again.
    private static double NameWidth(string name, double drawn, double ems)
    {
        var probe = new TextBlock
        {
            Style = (Style)Application.Current.Resources["EyebrowText"],
            Margin = new Thickness(0),
            Text = name,
            FontSize = drawn,
            CharacterSpacing = Math.Max(1, (int)Math.Round(ems * 1000)),
            TextWrapping = TextWrapping.NoWrap,
            IsTextScaleFactorEnabled = false,
        };
        probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return probe.DesiredSize.Width;
    }

    // A region's grid: its compartments, its empty cells the panel's ground with hairlines, its items at their places; null
    // where it has none.
    private Canvas? GridOf(DeskRegion region, DeskRegionSheet sheet, double size, bool comes, List<DeskCellView> entered)
    {
        var (cols, rows) = DeskPageRules.Grid(region);
        if (cols <= 0 || rows <= 0)
            return null;
        var grid = new Canvas
        {
            Width = cols * size + 1, Height = rows * size + 1, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
        };
        foreach (var part in region.Parts.Count > 0 ? region.Parts : [new DeskRect(0, 0, cols, rows)])
            grid.Children.Add(PartOf(part, size, Thickness(DeskPageRules.Inset(region, part, size)), region.Parts.Count > 1));
        foreach (var item in sheet.Cells.Where(c => !c.Cell.IsSlot).OrderBy(c => c.Cell.J).ThenBy(c => c.Cell.I))
        {
            var inset = Thickness(DeskPageRules.Inset(region, item.Cell.Rect, size));
            var cell = new DeskCellView(region, item, size, this, inset);
            Canvas.SetLeft(cell, item.Cell.I * size + inset.Left);
            Canvas.SetTop(cell, item.Cell.J * size + inset.Top);
            grid.Children.Add(cell);
            _cellViews.Add(cell);
            if (comes)
                entered.Add(cell);
        }
        return grid;
    }

    internal static int SlotOrder(string? slot) => DeskSlots.Order(slot ?? "") is var order and >= 0 ? order : 100;

    internal static Thickness Thickness((double Left, double Top, double Right, double Bottom) inset) =>
        new(inset.Left, inset.Top, inset.Right, inset.Bottom);

    // A compartment's empty cells: the cell's ground, a hairline round it and between its cells; one of several stands in
    // by its inset, framed a step stronger, so the compartments read apart as the game draws them.
    internal static FrameworkElement PartOf(DeskRect part, double size, Thickness inset, bool apart)
    {
        double w = part.W * size + 1 - inset.Left - inset.Right, h = part.H * size + 1 - inset.Top - inset.Bottom;
        var geometry = new GeometryGroup();
        for (var i = 1; i < part.W; i++)
            geometry.Children.Add(new LineGeometry { StartPoint = new Point(i * size - inset.Left + 0.5, 0), EndPoint = new Point(i * size - inset.Left + 0.5, h) });
        for (var j = 1; j < part.H; j++)
            geometry.Children.Add(new LineGeometry { StartPoint = new Point(0, j * size - inset.Top + 0.5), EndPoint = new Point(w, j * size - inset.Top + 0.5) });
        var frame = new Grid { Width = w, Height = h };
        frame.Children.Add(new Rectangle { Fill = Brush("CardBrush"), Stroke = Brush(apart ? "LineStrongBrush" : "LineBrush"), StrokeThickness = 1 });
        frame.Children.Add(new Path { Data = geometry, Stroke = Brush("LineBrush"), StrokeThickness = 1 });
        Canvas.SetLeft(frame, part.I * size + inset.Left);
        Canvas.SetTop(frame, part.J * size + inset.Top);
        return frame;
    }

    // ---- the head line ----

    private void HeadAt((double OnYou, double Stash) totals)
    {
        _shown = totals;
        if (_view is { } view)
            _head.Text = DeskPageRules.HeadLine(view.Sheet.Totals, totals.OnYou, totals.Stash);
    }

    // The totals count up once, from what the head line said to the new totals, eased out.
    private void CountUpTick()
    {
        if (_view is not { } view)
        {
            _countUp.Stop();
            return;
        }
        var t = Math.Clamp((DateTime.Now - _countStart).TotalSeconds / CountUpLength.TotalSeconds, 0, 1);
        var eased = 1 - Math.Pow(1 - t, 3);
        var to = (view.Sheet.Totals.OnYou, view.Sheet.Totals.Stash);
        HeadAt((_from.OnYou + (to.OnYou - _from.OnYou) * eased, _from.Stash + (to.Stash - _from.Stash) * eased));
        if (t >= 1)
        {
            _countUp.Stop();
            HeadAt(to);
        }
    }

    // ---- motion ----

    // A reading's cells come in one after another, 50 ms apart, each over 0.2 s, as the kit's pictures do. A still taken
    // meanwhile (RenderTargetBitmap: --snapshot, the developer script's "snapshot") draws every cell whole: the storyboard's
    // opacity runs on the compositor, which that render doesn't take, while the window's thread reads it as it goes
    // (measured 2026-10-25: the 26th and the 52nd cells at 0 for 1.3 s and 2.4 s, the still at 1 s showing them whole), so
    // the entry is checked by eye only (UI QA pass of step 2, F2).
    private static void Enter(IReadOnlyList<DeskCellView> cells)
    {
        var story = new Storyboard();
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        for (var i = 0; i < cells.Count; i++)
        {
            var at = i * 0.05;
            cells[i].Opacity = 0;
            var frames = new DoubleAnimationUsingKeyFrames();
            frames.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.Zero, Value = 0 });
            frames.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(at), Value = 0 });
            frames.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(at + 0.2), Value = 1, EasingFunction = ease });
            Storyboard.SetTarget(frames, cells[i]);
            Storyboard.SetTargetProperty(frames, "Opacity");
            story.Children.Add(frames);
        }
        story.Begin();
    }

    // The linked highlight between a window and its cell (docs/DESIGN.md, "Visual language"): gold at 18 % behind the
    // window's head and over the cell, the stem in amber; easing over 0.18 s, at once with the effects off.
    private static void Light(Link link, bool on)
    {
        link.Stem.Stroke = Brush(on ? "AmberBrush" : "LineStrongBrush");
        Fade(link.HeadLight, on ? 1 : 0);
        Fade(link.Anchor.Light, on ? 1 : 0);
    }

    private static readonly Windows.UI.ViewManagement.UISettings Settings = new();

    private static void Fade(UIElement element, double to)
    {
        if (!Settings.AnimationsEnabled)
        {
            element.Opacity = to;
            return;
        }
        var fade = new DoubleAnimation { To = to, Duration = Ease, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };
        Storyboard.SetTarget(fade, element);
        Storyboard.SetTargetProperty(fade, "Opacity");
        new Storyboard { Children = { fade } }.Begin();
    }

    // ---- the stems ----

    // From the container's cell to its window's head, along the grid's lines and the page's margins: out of a grid's cell
    // by its way through the grid (DeskPageRules.StemRoute: the middle of its side at the grid's edge, else along the lines
    // no item covers, its lower corner first), round a container's slot box where it leaves the grid on the box's side, out
    // of a slot at its side (its middle at the column's edge, else its lower corner); then, the window in the cell's own
    // column, down the column's margin; in the column right of the cell's, down the gap between them; two or more columns
    // right (the Zhuk of the middle column's backpack in the windows' column right of the stash), along the gap right of
    // the cell's column up to the lane over the columns, across it over the heads between, and down the gap left of the
    // window's column; to the head's middle and into its near end.
    private void DrawStems()
    {
        _stemsDue = false;
        foreach (var link in _links)
        {
            if (link.Anchor.XamlRoot is null || link.Head.XamlRoot is null || link.Anchor.ActualWidth <= 0)
                continue;
            // In the stems' own room, which stands inside the page's padding.
            var a = link.Anchor.TransformToVisual(_stems).TransformBounds(new Rect(0, 0, link.Anchor.ActualWidth, link.Anchor.ActualHeight));
            var h = link.Head.TransformToVisual(_stems).TransformBounds(new Rect(0, 0, link.Head.ActualWidth, link.Head.ActualHeight));
            var own = ColumnOf(link.Anchor);
            var theirs = ColumnOf(link.Panel);
            var column = theirs is null ? link.Panel.TransformToVisual(_stems).TransformPoint(new Point(0, 0)).X
                : theirs.TransformToVisual(_stems).TransformPoint(new Point(0, 0)).X;
            var cellLeft = own is null ? column : own.TransformToVisual(_stems).TransformPoint(new Point(0, 0)).X;
            var cellRight = own is null ? column : cellLeft + own.ActualWidth;
            var apart = own is null || theirs is null ? 0 : GetColumn(theirs) - GetColumn(own);
            var left = apart < 0;
            var right = apart > 0;
            var way = GridWay(link.Anchor, a, toLeft: !right);
            if (way is null)
            {
                if (right)
                    way = [a.Right >= cellRight - link.Anchor.Size ? new Point(a.Right, Math.Round(a.Top + a.Height / 2) + 0.5) : new Point(a.Right - 0.5, a.Bottom - 0.5)];
                else
                    way = [a.Left <= cellLeft + link.Anchor.Size ? new Point(a.Left, Math.Round(a.Top + a.Height / 2) + 0.5) : new Point(a.Left + 0.5, a.Bottom - 0.5)];
            }
            else if (!right)
                RoundTheBox(link.Anchor, way);
            var y = Math.Round(h.Top + h.Height / 2) + 0.5;
            var into = left ? h.Right + 4 : h.Left - 4;
            var figure = new PathFigure { StartPoint = way[0], IsClosed = false, IsFilled = false };
            foreach (var point in way.Skip(1))
                figure.Segments.Add(new LineSegment { Point = point });
            double margin;
            if (apart > 1)
            {
                // Over the columns between: up the gap right of the cell's column, along the lane, down the window's gap.
                var gap = Math.Round(cellRight + DeskPageRules.ColumnGap / 2) + 0.5;
                var lane = Math.Round(-DeskPageRules.Lane / 2) + 0.5;
                margin = Math.Round(column - DeskPageRules.ColumnGap / 2) + 0.5;
                figure.Segments.Add(new LineSegment { Point = new Point(gap, way[^1].Y) });
                figure.Segments.Add(new LineSegment { Point = new Point(gap, lane) });
                figure.Segments.Add(new LineSegment { Point = new Point(margin, lane) });
            }
            else
            {
                margin = left ? Math.Round(cellLeft - DeskPageRules.ColumnGap / 2) + 0.5
                    : right ? Math.Round(column - DeskPageRules.ColumnGap / 2) + 0.5
                    : column - 9.5;
                figure.Segments.Add(new LineSegment { Point = new Point(margin, way[^1].Y) });
            }
            figure.Segments.Add(new LineSegment { Point = new Point(margin, y) });
            figure.Segments.Add(new LineSegment { Point = new Point(into, y) });
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            link.Stem.Data = geometry;
            Shturmap.Session.AppLog.Debug(FormattableString.Invariant(
                $"Desk stem: cell {a.Left:0},{a.Top:0} {a.Width:0}×{a.Height:0}, from {string.Join(" via ", way.Select(p => FormattableString.Invariant($"{p.X:0.#},{p.Y:0.#}")))} by the margin at {margin:0.#} to {into:0.#},{y:0.#}"));
        }
    }

    // A stem leaving a container's grid on the side of its slot box, level with the box: along the gap between the box and
    // the grid, down to under the box, so it never crosses the box's picture.
    private void RoundTheBox(DeskCellView anchor, List<Point> way)
    {
        if (anchor.Parent is not Canvas grid || !_boxes.TryGetValue(grid, out var box) || box.ActualWidth <= 0)
            return;
        var b = box.TransformToVisual(_stems).TransformBounds(new Rect(0, 0, box.ActualWidth, box.ActualHeight));
        var end = way[^1];
        if (end.Y < b.Top - 0.5 || end.Y > b.Bottom + 0.5)
            return;
        var gridLeft = grid.TransformToVisual(_stems).TransformPoint(new Point(0, 0)).X;
        var x = Math.Round((b.Right + gridLeft) / 2) + 0.5;
        var under = Math.Round(b.Bottom + (gridLeft - b.Right) / 2) + 0.5;
        way.Add(new Point(x, end.Y));
        way.Add(new Point(x, under));
    }

    // A grid cell's way through its grid to the grid's edge on the window's side (DeskPageRules.StemRoute), in the stems'
    // room: the lattice's lines on the grid's hairlines, the first point on the cell's own frame along the first leg (a
    // cell in a compartment stands in by its inset); a cell at that edge from the middle of its side. Null for a slot,
    // which stands in no grid, and where the grid gives no way.
    private List<Point>? GridWay(DeskCellView anchor, Rect a, bool toLeft)
    {
        var cell = anchor.Sheet.Cell;
        if (cell.IsSlot || anchor.Parent is not Canvas grid || DeskPageRules.StemRoute(anchor.Region, cell.Rect, toLeft) is not { } route)
            return null;
        if (route.Count == 1)
            return [new Point(toLeft ? a.Left : a.Right, Math.Round(a.Top + a.Height / 2) + 0.5)];
        var origin = grid.TransformToVisual(_stems).TransformPoint(new Point(0, 0));
        var size = anchor.Size;
        var way = route.Select(p => new Point(origin.X + p.I * size + 0.5, origin.Y + p.J * size + 0.5)).ToList();
        var (first, second) = (route[0], route[1]);
        way[0] = first.J == second.J
            ? new Point(first.I == cell.I ? a.Left + 0.5 : first.I == cell.I + cell.W ? a.Right - 0.5 : way[0].X, way[0].Y)
            : new Point(way[0].X, first.J == cell.J ? a.Top + 0.5 : first.J == cell.J + cell.H ? a.Bottom - 0.5 : way[0].Y);
        return way;
    }

    // The column of the page an element stands in (one of _columns' panels), or null.
    private FrameworkElement? ColumnOf(FrameworkElement element)
    {
        DependencyObject? at = element;
        while (at is not null && !(at is FrameworkElement { Parent: var parent } && ReferenceEquals(parent, _columns)))
            at = VisualTreeHelper.GetParent(at);
        return at as FrameworkElement;
    }

    internal static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

    internal void OnCellEntered(DeskCellView cell) => CellPointed?.Invoke(cell);

    internal void OnCellClicked(DeskCellView cell) => CellClicked?.Invoke(cell);

    internal void OnCellPinned(DeskCellView cell) => CellPinToggled?.Invoke(cell);

    /// <summary>The cell of a place on the page, or null.</summary>
    public DeskCellView? CellOf(DeskCellKey key) => _cellViews.FirstOrDefault(c => key.Is(c.Region, c.Sheet.Cell));

    /// <summary>The cells whose cards are pinned: each carries the pin's tab on its top edge.</summary>
    public void ShowPinned(IReadOnlySet<DeskCellKey> pinned)
    {
        _pinned = pinned;
        foreach (var cell in _cellViews)
            cell.Pinned = pinned.Contains(cell.Key);
    }

    /// <summary>
    /// A copy's fun (*Fun*; "Design system", *Motion*): the cell's frame flashes gold once, in over 0.08 s and out over
    /// 0.6 s; the figure's decode is the window's (<c>MainWindow.Decode</c>). With the effects off nothing moves.
    /// </summary>
    public static void Flash(DeskCellView cell)
    {
        if (!Settings.AnimationsEnabled)
            return;
        var frames = new DoubleAnimationUsingKeyFrames();
        frames.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.Zero, Value = 0 });
        frames.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.08), Value = 1 });
        frames.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.68), Value = 0, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } });
        Storyboard.SetTarget(frames, cell.FlashFrame);
        Storyboard.SetTargetProperty(frames, "Opacity");
        new Storyboard { Children = { frames } }.Begin();
    }
}
