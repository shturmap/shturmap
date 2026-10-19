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
/// addendum: A(2) · B1 · C1a · Da; docs/DESIGN.md §4, "Screen anatomy", *The stash desk*): the head line over the page,
/// then every region of the page in hand under its own head, in reading order, in one column, or where both fit at 56 DIP
/// or more in the game's two (the stash right), the cells as large as the desk's width lets them be, 40 to 80 DIP
/// (<see cref="DeskPageRules.Layout"/>; owner, 2026-10-20); regions a reading didn't change folded into one line
/// ("UNCHANGED · POUCH ▸ · STASH ▸"). A click on a cell copies its list price, a right-click pins its card. Each cell mirrors the screenshot's, drawn from the catalogue's art with its figure, count, keep notch,
/// change and tint (<see cref="DeskCells"/>); a container's open window stands under "ZHUK · IN THE BACKPACK", stemmed to
/// its cell, and pointing at either lights the other. A reading's page comes in cell by cell, 50 ms apart, as the kit's
/// pictures do, and its totals count up once; nothing else moves. Built in code, as the map's panels are: the page is
/// rebuilt for each snapshot that changes it.
/// </summary>
public sealed partial class DeskBoard : Grid
{
    /// <summary>The pointer came onto a cell: the rail shows its card.</summary>
    public event Action<DeskCellView>? CellPointed;

    /// <summary>A name in a fold line was clicked: its region's key, to open until the next reading.</summary>
    public event Action<string>? Unfolded;

    /// <summary>A cell was clicked: the rail copies its list price (*The click*).</summary>
    public event Action<DeskCellView>? CellClicked;

    /// <summary>A cell was right-clicked: its card is pinned in the rail, or unpinned.</summary>
    public event Action<DeskCellView>? CellPinToggled;

    // The cells whose cards are pinned in the rail: their tab on the page.
    private IReadOnlySet<DeskCellKey> _pinned = new HashSet<DeskCellKey>();

    private static readonly TimeSpan Ease = TimeSpan.FromSeconds(0.18);
    private static readonly TimeSpan CountUpLength = TimeSpan.FromSeconds(0.9);
    private const double Gap = 18;

    private readonly TextBlock _head;
    private readonly TextBlock _empty;
    private readonly ScrollViewer _scroll;
    private readonly Grid _page;
    private readonly Grid _columns;
    private readonly Canvas _stems;
    private readonly List<Link> _links = [];
    private readonly List<DeskCellView> _cellViews = [];
    private readonly DispatcherQueueTimer _countUp;

    private DeskView? _view;
    private IReadOnlySet<string> _open = new HashSet<string>();
    private double _builtCell;
    private int _builtBreak = -2;
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
            Margin = new Thickness(Gap, 14, Gap, 10),
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
        _page = new Grid { Padding = new Thickness(Gap, 0, Gap, 24) };
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
        SizeChanged += (_, _) =>
        {
            // The cell's size and the columns follow the desk's width (DeskPageRules): a new layout only when they change.
            if (_view is { } view && DeskPageRules.Layout(view.Sheet.Regions.Select(r => r.Region).ToList(), DeskWidth()) != (_builtCell, _builtBreak))
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

    /// <summary>The page on screen and its fold, for the layout check and the developer view.</summary>
    public DeskView? View => _view;

    /// <summary>
    /// Shows a page with its fold. With <paramref name="motion"/>, the cells of <paramref name="entering"/>'s regions
    /// come in, 50 ms apart, and the totals count up from what the head line said (from nothing on the page's first
    /// showing); without, the page stands at once (animation effects off, a snapshot run).
    /// </summary>
    public void Show(DeskView? view, IReadOnlySet<string> open, IReadOnlySet<string> entering, bool motion)
    {
        var had = _view is not null;
        _view = view;
        _open = open;
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

    private FrameworkElement? PanelOf(DeskCellView cell) => _links.FirstOrDefault(l => l.Panel.Tag is string key && key == cell.Region.Key)?.Panel;

    private double DeskWidth() => ActualWidth > 0 ? ActualWidth : 516;

    // ---- the page ----

    // The page anew: its columns, regions, fold lines and cells; the cells of the entering regions, in reading order.
    private List<DeskCellView> Build(IReadOnlySet<string>? entering)
    {
        _columns.Children.Clear();
        _columns.ColumnDefinitions.Clear();
        _stems.Children.Clear();
        _links.Clear();
        _cellViews.Clear();
        var entered = new List<DeskCellView>();
        if (_view is not { Sheet.Regions.Count: > 0 } view)
        {
            _head.Visibility = Visibility.Collapsed;
            _scroll.Visibility = Visibility.Collapsed;
            // Said with each build: a language switch builds the page anew (MainWindow.RetellDesk).
            _empty.Text = AppTexts.DeskEmpty;
            _empty.Visibility = Visibility.Visible;
            _builtCell = 0;
            _builtBreak = -2;
            return entered;
        }
        _head.Visibility = Visibility.Visible;
        _scroll.Visibility = Visibility.Visible;
        _empty.Visibility = Visibility.Collapsed;

        var width = DeskWidth();
        var regions = view.Sheet.Regions;
        var keys = regions.Select(r => r.Region.Key).ToList();
        var (size, columnBreak) = DeskPageRules.Layout(regions.Select(r => r.Region).ToList(), width);
        _builtCell = size;
        _builtBreak = columnBreak;
        var columns = columnBreak > 0 ? 2 : 1;
        var panels = new StackPanel[columns];
        for (var c = 0; c < columns; c++)
        {
            var range = c == 0 ? regions.Take(columnBreak > 0 ? columnBreak : regions.Count) : regions.Skip(columnBreak);
            // As wide as its widest grid, and never under the stash's ten cells in one column (400 DIP at 40).
            var columnWidth = DeskPageRules.ColumnWidth(range.Select(r => r.Region).ToList(), size, columns == 1 || c == 1 ? 10 : 6);
            _columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(columnWidth) });
            panels[c] = new StackPanel();
            SetColumn(panels[c], c);
            _columns.Children.Add(panels[c]);
        }

        var windows = new List<(DeskRegion Window, FrameworkElement Head, FrameworkElement Panel, Border Light)>();
        StackPanel? row = null;
        DeskRegionKind? before = null;
        foreach (var block in DeskFold.Blocks(keys, _open, columnBreak))
        {
            var first = block switch
            {
                DeskBlock.Region r => keys.IndexOf(r.Key),
                DeskBlock.Fold f => keys.IndexOf(f.Keys[0]),
                _ => 0,
            };
            var panel = panels[columnBreak > 0 && first >= columnBreak ? 1 : 0];
            var top = panel.Children.Count == 0 ? 0 : 20;
            if (block is DeskBlock.Fold fold)
            {
                row = null;
                before = null;
                panel.Children.Add(FoldLine(fold, regions, top, size));
                continue;
            }
            var sheet = regions[first];
            var (element, head, light) = RegionBlock(sheet, view.Sheet.Page, size, _columns.ColumnDefinitions[panel == panels[0] ? 0 : 1].Width.Value, entering, entered);
            // POCKETS and SPECIAL SLOTS on one row, as the game has them, each under its own head.
            if (row is not null && before is { } kind && DeskPageRules.SameRow(kind, sheet.Region.Kind))
                row.Children.Add(element);
            else
            {
                row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DeskPageRules.RowGap, Margin = new Thickness(0, top, 0, 0) };
                row.Children.Add(element);
                panel.Children.Add(row);
            }
            before = sheet.Region.Kind;
            if (sheet.Region is { Kind: DeskRegionKind.ContainerWindow, AttachedTo: not null })
                windows.Add((sheet.Region, head, element, light));
        }

        // Each attached window and its container's cell, where both are on the page (an anchor in a folded region has
        // no cell to stem to).
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

    // A region under its head: the muted "Not attached" line of a window attached to none, its slots in a strip (the
    // loadout's in the game's order, a middle region's own container), its grid.
    private (FrameworkElement Block, FrameworkElement Head, Border Light) RegionBlock(DeskRegionSheet sheet, DeskPage page, double size, double column,
        IReadOnlySet<string>? entering, List<DeskCellView> entered)
    {
        var region = sheet.Region;
        var block = new StackPanel { Spacing = 6, Tag = region.Key, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
        var head = new TextBlock
        {
            Style = (Style)Application.Current.Resources["EyebrowText"],
            Margin = new Thickness(0),
            Text = DeskPageRules.Head(region, page),
            FontSize = DeskPageRules.HeadSize(size),
            HorizontalAlignment = HorizontalAlignment.Left,
            TextWrapping = TextWrapping.Wrap,
        };
        // The linked highlight's gold behind the head of a window whose cell is pointed at.
        var light = new Border { Background = Brush("LinkBrush"), Opacity = 0, IsHitTestVisible = false, Margin = new Thickness(-4, -2, -4, -2) };
        var headCell = new Grid { HorizontalAlignment = HorizontalAlignment.Left };
        headCell.Children.Add(light);
        headCell.Children.Add(head);
        block.Children.Add(headCell);
        if (DeskPageRules.NotAttached(sheet) is { } line)
            block.Children.Add(new TextBlock { Style = (Style)Application.Current.Resources["NoteText"], Text = line, MaxWidth = column });

        var comes = entering?.Contains(region.Key) == true;
        var slots = sheet.Cells.Where(c => c.Cell.IsSlot).OrderBy(c => SlotOrder(c.Cell.Slot)).ToList();
        if (slots.Count > 0)
        {
            // The strip wraps at the column's width.
            var strip = new WrapRow { HorizontalSpacing = 4, VerticalSpacing = 4, MaxWidth = column };
            foreach (var slot in slots)
            {
                var cell = new DeskCellView(region, slot, size, this);
                strip.Children.Add(cell);
                _cellViews.Add(cell);
                if (comes)
                    entered.Add(cell);
            }
            block.Children.Add(strip);
        }
        // LOADOUT draws no grid, whatever a reading says (DeskPageRules.Grid); a grid's compartments stand apart.
        var (cols, rows) = DeskPageRules.Grid(region);
        if (cols > 0 && rows > 0)
        {
            var grid = new Canvas { Width = cols * size + 1, Height = rows * size + 1, HorizontalAlignment = HorizontalAlignment.Left };
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
            block.Children.Add(grid);
        }
        return (block, head, light);
    }

    private static int SlotOrder(string? slot) => DeskSlots.Order(slot ?? "") is var order and >= 0 ? order : 100;

    private static Thickness Thickness((double Left, double Top, double Right, double Bottom) inset) =>
        new(inset.Left, inset.Top, inset.Right, inset.Bottom);

    // A compartment's empty cells: the cell's ground, a hairline round it and between its cells; one of several stands in
    // by its inset, framed a step stronger, so the compartments read apart as the game draws them.
    private static FrameworkElement PartOf(DeskRect part, double size, Thickness inset, bool apart)
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

    // "UNCHANGED · LOADOUT ▸ · POCKETS ▸": the folded neighbours' names, each a link that opens its region. Each "·" goes
    // with the name after it, so a line that wraps breaks before the separator, never after it (QA pass of 2026-10-19,
    // F8: "POCKETS ▸ ·" ended a line).
    private FrameworkElement FoldLine(DeskBlock.Fold fold, IReadOnlyList<DeskRegionSheet> regions, double top, double size)
    {
        var line = new WrapRow { HorizontalSpacing = 6, VerticalSpacing = 2, Margin = new Thickness(0, top, 0, 0) };
        var words = DeskPageRules.HeadSize(size);
        line.Children.Add(Word(DeskPageRules.Unchanged, "MutedBrush", words));
        foreach (var key in fold.Keys)
        {
            if (regions.FirstOrDefault(r => r.Region.Key == key) is not { } sheet)
                continue;
            var link = new HyperlinkButton { Padding = new Thickness(0), Content = Word(DeskPageRules.FoldName(sheet.Region), "AmberBrush", words), Tag = key };
            link.Click += (sender, _) =>
            {
                if (sender is FrameworkElement { Tag: string region })
                    Unfolded?.Invoke(region);
            };
            var named = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            named.Children.Add(Word("·", "MutedBrush", words));
            named.Children.Add(link);
            line.Children.Add(named);
        }
        return line;
    }

    private static TextBlock Word(string text, string brush, double size) => new()
    {
        Style = (Style)Application.Current.Resources["StatusText"],
        FontSize = size,
        Text = text,
        Foreground = Brush(brush),
    };

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

    // A reading's cells come in one after another, 50 ms apart, each over 0.2 s, as the kit's pictures do.
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

    // From the container's cell to its window's head, along the grid's lines and the page's margin: out of the cell at
    // its left edge (its middle in the grid's first column, however tall the cell; its lower corner elsewhere, where the
    // line runs between the rows), left to the margin, down to the head's middle, and into it.
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
            var column = link.Panel.TransformToVisual(_stems).TransformPoint(new Point(0, 0)).X;
            var margin = column - 9.5;
            var atEdge = a.Left <= column + link.Anchor.Size;
            var start = atEdge ? new Point(a.Left, Math.Round(a.Top + a.Height / 2) + 0.5) : new Point(a.Left + 0.5, a.Bottom - 0.5);
            var y = Math.Round(h.Top + h.Height / 2) + 0.5;
            var figure = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };
            figure.Segments.Add(new LineSegment { Point = new Point(margin, start.Y) });
            figure.Segments.Add(new LineSegment { Point = new Point(margin, y) });
            figure.Segments.Add(new LineSegment { Point = new Point(h.Left - 4, y) });
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            link.Stem.Data = geometry;
            Shturmap.Session.AppLog.Debug(FormattableString.Invariant(
                $"Desk stem: cell {a.Left:0},{a.Top:0} {a.Width:0}×{a.Height:0}, from {start.X:0.#},{start.Y:0.#} by the margin at {margin:0.#} to {h.Left - 4:0.#},{y:0.#}"));
        }
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
