using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Shturmap.App.Rules;
using Shturmap.Core.Desk;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Shturmap.App.Controls;

/// <summary>
/// The stash desk's card in the rail's 351 DIP (owner, 2026-10-19, from a panel: C1; owner, 2026-10-20: the sparkline,
/// every trader, the keep lines, pinning, and "UI elements for setting what is copied clearly visible"; docs/DESIGN.md
/// §4, *The stash desk*, *The card* and *The click*), decision first: the item with its offers' dot and flea level and the
/// pin at its head; the gold figure with its 7-day change; the plate "A CLICK COPIES" with the rule's − and + and what a
/// click on the cell copies now, or why nothing (the gold light above the best net); the flea's net with the fee and the
/// 48 h change; the condition; KEEP. Then the 30 days' band from the lowest offer to the average with the 7-day mark, the
/// day's bar, the traders, the crafts and barters, the prices' age. A "?" cell's card is its candidates, a click picks
/// one. Its words come from <see cref="DeskCards"/>; built in code, as the desk's page is.
/// </summary>
public sealed partial class DeskCardView : UserControl
{
    private readonly Border _frame = new();

    /// <summary>The card's width: the rail's inner width.</summary>
    public const double CardWidth = 351;

    private const double Pad = 14;
    private const double Inner = CardWidth - 2 * Pad - 2;
    private const double BandHeight = 64;

    /// <summary>The pin at the card's head was clicked.</summary>
    public event Action<DeskCardView>? PinToggled;

    /// <summary>The rule's − (−1) or + (+1) was clicked.</summary>
    public event Action<int>? RuleStepped;

    /// <summary>A "?" card's candidate was clicked: its item, to pick for the cell.</summary>
    public event Action<DeskCardView, string>? Picked;

    public DeskCardView(DeskCellKey key, DeskCardWords words, bool pinned)
    {
        Key = key;
        Width = CardWidth;
        HorizontalAlignment = HorizontalAlignment.Left;
        _frame.Background = Brush("CardBrush");
        Content = _frame;
        Pinned = pinned;
        Show(words);
    }

    /// <summary>The card's cell on the page.</summary>
    public DeskCellKey Key { get; }

    public DeskCardWords Words { get; private set; } = null!;

    /// <summary>A pinned card stands framed in amber, as a held card does.</summary>
    public bool Pinned
    {
        get => _pinned;
        private set
        {
            _pinned = value;
            _frame.BorderBrush = Brush(value ? "AmberBrush" : "LineStrongBrush");
            _frame.BorderThickness = new Thickness(value ? 1.5 : 1);
        }
    }

    private bool _pinned;

    /// <summary>The card anew, with these words.</summary>
    public void Show(DeskCardWords words)
    {
        Words = words;
        var body = new StackPanel { Padding = new Thickness(Pad), Spacing = 6 };
        body.Children.Add(Head(words));
        if (words.Plate == DeskPlate.Pick && words.Candidates.Count > 0)
        {
            body.Children.Add(Note(words.PlateLine));
            foreach (var candidate in words.Candidates)
                body.Children.Add(Candidate(candidate));
        }
        else
        {
            if (words.Figure is not null)
                body.Children.Add(Pair(new TextBlock { Text = words.Figure, Style = StyleOf("FigureText"), FontSize = 22, HorizontalAlignment = HorizontalAlignment.Left },
                    words.SevenDays));
            body.Children.Add(Plate(words));
            if (words.FleaNet is not null)
                body.Children.Add(Pair(Note(words.FleaNet, ink: true), words.TwoDays));
            foreach (var note in words.Notes)
                body.Children.Add(Note(note));
            if (words.Keep.Count > 0)
            {
                body.Children.Add(Eyebrow(RuleTexts.DeskCardKeep));
                foreach (var line in words.Keep)
                    body.Children.Add(KeepLine(line));
            }
            body.Children.Add(Eyebrow(RuleTexts.DeskCardThirtyDays));
            body.Children.Add(BandOf(words));
            if (words.Day is { } day)
            {
                body.Children.Add(Eyebrow(RuleTexts.DeskCardDay));
                body.Children.Add(DayOf(words, day));
                if (words.Outlier is { } outlier)
                    body.Children.Add(Note(outlier));
            }
            if (words.Traders.Count > 0)
            {
                body.Children.Add(Eyebrow(RuleTexts.DeskCardTraders));
                foreach (var trader in words.Traders)
                    body.Children.Add(TraderLine(trader));
            }
            if (words.Sources.Count > 0)
            {
                body.Children.Add(Eyebrow(RuleTexts.DeskCardCraftsBarters));
                foreach (var source in words.Sources)
                    body.Children.Add(SourceLine(source));
            }
        }
        if (words.Age is { } age)
        {
            var line = Note(age);
            line.Margin = new Thickness(0, 6, 0, 0);
            body.Children.Add(line);
        }
        _frame.Child = body;
    }

    // ---- the head ----

    // The item's icon and name (a slot's over it in muted capitals), the offers' dot, the offers and the flea level; the
    // pin at the right.
    private FrameworkElement Head(DeskCardWords words)
    {
        var head = new Grid { ColumnSpacing = 10 };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var picture = new Picture { ItemId = words.ItemId, Size = 40, Opacity = words.Plate == DeskPlate.Pick ? 0.5 : 1 };
        head.Children.Add(picture);
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        if (words.Slot is { } slot)
            text.Children.Add(new TextBlock { Text = slot, Style = StyleOf("StatusText"), FontSize = 11, CharacterSpacing = 140, Foreground = Brush("MutedBrush") });
        text.Children.Add(new TextBlock { Text = words.Name, Style = StyleOf("TitleText") });
        if (words.Offers.Length > 0)
        {
            var offers = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            if (Dot(words.Liquidity) is { } dot)
                offers.Children.Add(dot);
            offers.Children.Add(new TextBlock { Text = words.Offers, Style = StyleOf("StatusText"), FontSize = 11, Foreground = Brush("MutedBrush"), TextWrapping = TextWrapping.Wrap });
            text.Children.Add(offers);
        }
        Grid.SetColumn(text, 1);
        head.Children.Add(text);
        var pin = new Button
        {
            Width = 28,
            Height = 28,
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Top,
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            Content = PinGlyph(Pinned),
        };
        var tip = Pinned ? ViewTexts.DeskUnpinTip : ViewTexts.DeskPinTip;
        ToolTipService.SetToolTip(pin, tip);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(pin, tip);
        pin.Click += (_, _) => PinToggled?.Invoke(this);
        Grid.SetColumn(pin, 2);
        head.Children.Add(pin);
        return head;
    }

    /// <summary>
    /// The pin's glyph ("Visual language", *One symbol, one meaning*): a cell's outline with the pin's tab on its top edge,
    /// the mark a pinned cell carries on the page; the tab amber while pinned, muted with the outline while not. Never the
    /// pushpin, which is the Place quest type.
    /// </summary>
    public static FrameworkElement PinGlyph(bool pinned)
    {
        var glyph = new Grid { Width = 14, Height = 13 };
        glyph.Children.Add(new Rectangle
        {
            Stroke = Brush(pinned ? "InkBrush" : "MutedBrush"), StrokeThickness = 1, Width = 14, Height = 11, VerticalAlignment = VerticalAlignment.Bottom,
        });
        glyph.Children.Add(new Rectangle
        {
            Fill = Brush(pinned ? "AmberBrush" : "MutedBrush"), Width = 8, Height = 3, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Center,
        });
        return glyph;
    }

    // The liquidity dot: full from 50 offers, half from 10 to 49, a ring under 10; none unknown.
    private static FrameworkElement? Dot(DeskLiquidity liquidity)
    {
        if (liquidity == DeskLiquidity.Unknown)
            return null;
        const double d = 7;
        var dot = new Grid { Width = d, Height = d, VerticalAlignment = VerticalAlignment.Center };
        dot.Children.Add(new Ellipse { Stroke = Brush("InkBrush"), StrokeThickness = 1, Fill = liquidity == DeskLiquidity.Full ? Brush("InkBrush") : null });
        if (liquidity == DeskLiquidity.Half)
        {
            var half = new PathFigure { StartPoint = new Point(d / 2, 0), IsClosed = true };
            half.Segments.Add(new ArcSegment { Point = new Point(d / 2, d), Size = new Size(d / 2, d / 2), SweepDirection = SweepDirection.Counterclockwise });
            var geometry = new PathGeometry();
            geometry.Figures.Add(half);
            dot.Children.Add(new Path { Data = geometry, Fill = Brush("InkBrush") });
        }
        return dot;
    }

    // ---- the plate ----

    // "A CLICK COPIES" with the rule's − and + (the current rule in amber between them), then what a click copies now, the
    // gold light where it is over the best net, or why it copies nothing; then the last low and the best trader, muted.
    private FrameworkElement Plate(DeskCardWords words)
    {
        var plate = new StackPanel
        {
            Spacing = 4,
            Padding = new Thickness(10, 8, 10, 9),
            Background = Brush("RaisedBrush"),
            BorderBrush = Brush("LineStrongBrush"),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 2, 0, 2),
        };
        if (words.Plate != DeskPlate.Pick)
        {
            var top = new Grid { ColumnSpacing = 8 };
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            top.Children.Add(new TextBlock
            {
                Text = RuleTexts.DeskCardCopyHead, Style = StyleOf("StatusText"), FontSize = 11, CharacterSpacing = 140, Foreground = Brush("MutedBrush"),
                TextWrapping = TextWrapping.Wrap,
            });
            var stepper = RuleStepper(words.Rule, step => RuleStepped?.Invoke(step));
            Grid.SetColumn(stepper, 1);
            top.Children.Add(stepper);
            plate.Children.Add(top);
        }
        var line = new Grid { ColumnSpacing = 6 };
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        if (words.Plate == DeskPlate.Warning)
        {
            // The status bar's gold light: needs attention.
            line.Children.Add(new Rectangle { Width = 6, Height = 6, Fill = Brush("AmberBrush"), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 6, 0, 0) });
        }
        var said = new TextBlock
        {
            Text = words.PlateLine,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush(words.Plate is DeskPlate.Nothing or DeskPlate.Pick ? "MutedBrush" : "InkBrush"),
            FontWeight = words.Plate is DeskPlate.Copies or DeskPlate.Warning ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
        };
        Grid.SetColumn(said, 1);
        line.Children.Add(said);
        plate.Children.Add(line);
        if (words.PlateReason is { } reason)
            plate.Children.Add(Note(reason));
        return plate;
    }

    /// <summary>The rule's control: − and + around the rule's words in amber ("3% UNDER"), one percent a step, from 20 %
    /// under to 20 % over (<see cref="DeskCopyRule.Step"/>). The card's plate and settings' row show the same one.</summary>
    public static FrameworkElement RuleStepper(string rule, Action<int> stepped)
    {
        var stepper = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        Button Step(string glyph, int by, string tip)
        {
            var button = new Button
            {
                Width = 26,
                Height = 24,
                Padding = new Thickness(0),
                Content = new TextBlock { Text = glyph, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center },
            };
            ToolTipService.SetToolTip(button, tip);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, tip);
            button.Click += (_, _) => stepped(by);
            return button;
        }
        stepper.Children.Add(Step("−", -1, ViewTexts.DeskRuleLower));
        stepper.Children.Add(new TextBlock
        {
            Text = rule, Style = StyleOf("StatusText"), FontSize = 12, Foreground = Brush("AmberBrush"), MinWidth = 92, TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        stepper.Children.Add(Step("+", 1, ViewTexts.DeskRuleHigher));
        return stepper;
    }

    // ---- KEEP ----

    private static FrameworkElement KeepLine(DeskCardKeepLine line)
    {
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(Notch(line.Notch));
        var text = new TextBlock { Text = line.Text, TextWrapping = TextWrapping.Wrap };
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        return row;
    }

    // The notch as the cell draws it: a filled triangle (now; in ink a hideout module's), an open one (next), a bracket (later).
    private static FrameworkElement Notch(DeskNotch notch)
    {
        const double s = 10;
        var figure = new PathFigure { StartPoint = notch == DeskNotch.Later ? new Point(0.75, s) : new Point(0.75, 0.75) };
        if (notch == DeskNotch.Later)
        {
            figure.Segments.Add(new LineSegment { Point = new Point(0.75, 0.75) });
            figure.Segments.Add(new LineSegment { Point = new Point(s, 0.75) });
        }
        else
        {
            figure.Segments.Add(new LineSegment { Point = new Point(s, 0.75) });
            figure.Segments.Add(new LineSegment { Point = new Point(0.75, s) });
            figure.IsClosed = true;
        }
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        var colour = Brush(notch == DeskNotch.Hideout ? "InkBrush" : "AmberBrush");
        var filled = notch is DeskNotch.Now or DeskNotch.Hideout;
        return new Path
        {
            Data = geometry, Fill = filled ? colour : null, Stroke = colour, StrokeThickness = filled ? 0.5 : 1.5, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 4, 0, 0),
        };
    }

    // ---- 30 days ----

    // The band from the lowest offer up to the average, a point every 2 h over 30 days, the average's line on its top, a
    // hairline 7 days back; under it the high and the low in words. While the history comes, a quiet "…".
    private static FrameworkElement BandOf(DeskCardWords words)
    {
        if (words.Band == DeskBandState.Loading)
            return Note(RuleTexts.DeskCardHistoryLoading);
        if (words.Band == DeskBandState.None || words.BandPoints.Count < 2)
            return Note(RuleTexts.DeskCardNoHistory);
        var points = words.BandPoints;
        var first = points[0].At;
        var last = points[^1].At;
        var span = Math.Max(1, (last - first).TotalSeconds);
        var high = points.Max(p => p.Avg);
        var low = points.Min(p => p.Min);
        var range = Math.Max(1, high - low);
        double X(DateTimeOffset at) => Math.Round((at - first).TotalSeconds / span * Inner, 2);
        double Y(double price) => Math.Round(2 + (high - price) / range * (BandHeight - 4), 2);
        var canvas = new Canvas { Width = Inner, Height = BandHeight };
        canvas.Children.Add(new Rectangle { Width = Inner, Height = BandHeight, Fill = Brush("GroundBrush"), Opacity = 0.5 });
        // The band: along the averages, back along the lowest offers.
        var band = new PathFigure { StartPoint = new Point(X(points[0].At), Y(points[0].Avg)), IsClosed = true };
        foreach (var p in points.Skip(1))
            band.Segments.Add(new LineSegment { Point = new Point(X(p.At), Y(p.Avg)) });
        foreach (var p in points.Reverse())
            band.Segments.Add(new LineSegment { Point = new Point(X(p.At), Y(p.Min)) });
        var bandGeometry = new PathGeometry();
        bandGeometry.Figures.Add(band);
        canvas.Children.Add(new Path { Data = bandGeometry, Fill = new SolidColorBrush((Windows.UI.Color)Application.Current.Resources["AmberColor"]) { Opacity = 0.22 } });
        var line = new PathFigure { StartPoint = new Point(X(points[0].At), Y(points[0].Avg)) };
        foreach (var p in points.Skip(1))
            line.Segments.Add(new LineSegment { Point = new Point(X(p.At), Y(p.Avg)) });
        var lineGeometry = new PathGeometry();
        lineGeometry.Figures.Add(line);
        canvas.Children.Add(new Path { Data = lineGeometry, Stroke = Brush("AmberBrush"), StrokeThickness = 1.25 });
        // 7 days back: a hairline and its mark.
        var week = last.AddDays(-7);
        if (week > first)
        {
            var x = Math.Round(X(week)) + 0.5;
            canvas.Children.Add(new Line { X1 = x, X2 = x, Y1 = 0, Y2 = BandHeight, Stroke = Brush("LineStrongBrush"), StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 2, 2 } });
            var mark = new TextBlock { Text = "7 D", Style = StyleOf("StatusText"), FontSize = 9.5, Foreground = Brush("MutedBrush") };
            Canvas.SetLeft(mark, x + 3);
            Canvas.SetTop(mark, 1);
            canvas.Children.Add(mark);
        }
        var block = new StackPanel { Spacing = 3 };
        block.Children.Add(canvas);
        if (words.HighLow is { } said)
            block.Children.Add(new TextBlock { Text = said, Style = StyleOf("StatusText"), FontSize = 11, Foreground = Brush("MutedBrush"), TextWrapping = TextWrapping.Wrap });
        block.Children.Add(Note(RuleTexts.DeskCardBandNote));
        return block;
    }

    // ---- 24 h ----

    // The day's range: a track from its low to its end, the last low's tick in amber, the average's in ink; the marks'
    // words over it, the ends under it.
    private static FrameworkElement DayOf(DeskCardWords words, DeskDayRange day)
    {
        var block = new StackPanel { Spacing = 3 };
        if (words.DayMarks is { } marks)
            block.Children.Add(new TextBlock { Text = marks, Style = StyleOf("StatusText"), FontSize = 11, Foreground = Brush("MutedBrush"), TextWrapping = TextWrapping.Wrap });
        const double h = 14;
        var bar = new Canvas { Width = Inner, Height = h };
        var track = new Rectangle { Width = Inner, Height = 4, Fill = Brush("LineStrongBrush") };
        Canvas.SetTop(track, (h - 4) / 2);
        bar.Children.Add(track);
        void Tick(double price, string brush, double width)
        {
            var tick = new Rectangle { Width = width, Height = h, Fill = Brush(brush) };
            Canvas.SetLeft(tick, Math.Clamp(Math.Round(day.At(price) * Inner - width / 2), 0, Inner - width));
            bar.Children.Add(tick);
        }
        if (day.Avg is { } avg)
            Tick(avg, "InkBrush", 1.5);
        if (day.LastLow is { } last)
            Tick(last, "AmberBrush", 2.5);
        block.Children.Add(bar);
        var ends = new Grid();
        ends.Children.Add(new TextBlock { Text = words.DayLow, Style = StyleOf("StatusText"), FontSize = 11, Foreground = Brush("MutedBrush") });
        ends.Children.Add(new TextBlock
        {
            Text = words.DayHigh + (day.Outlier ? " ›" : ""), Style = StyleOf("StatusText"), FontSize = 11, Foreground = Brush("MutedBrush"), HorizontalAlignment = HorizontalAlignment.Right,
        });
        block.Children.Add(ends);
        return block;
    }

    // ---- traders, crafts and barters ----

    private static FrameworkElement TraderLine(DeskCardTrader trader)
    {
        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(new Picture { TraderId = trader.TraderId, Size = 20 });
        var name = new TextBlock { Text = trader.Name, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        Grid.SetColumn(name, 1);
        row.Children.Add(name);
        var price = new TextBlock { Text = trader.Price, VerticalAlignment = VerticalAlignment.Center, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        Grid.SetColumn(price, 2);
        row.Children.Add(price);
        return row;
    }

    private static FrameworkElement SourceLine(DeskCardSource source)
    {
        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        if (source.TraderId is { } trader)
            row.Children.Add(new Picture { TraderId = trader, Size = 20 });
        else
        {
            row.Children.Add(new FontIcon
            {
                // The craft's wrench, as on the item card.
                Glyph = char.ConvertFromUtf32(0xE90F), FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = 14, Foreground = Brush("MutedBrush"),
                VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0),
            });
        }
        var text = new TextBlock { Text = source.Text, TextWrapping = TextWrapping.Wrap };
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        return row;
    }

    // ---- a "?" cell's candidates ----

    private FrameworkElement Candidate(DeskCardCandidate candidate)
    {
        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(new Picture { ItemId = candidate.ItemId, Size = 32 });
        var name = new TextBlock { Text = candidate.Name, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(name, 1);
        row.Children.Add(name);
        var figure = new TextBlock { Text = candidate.Figure, Style = StyleOf("FigureText"), FontSize = 15, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(figure, 2);
        row.Children.Add(figure);
        var button = new Button
        {
            Content = row,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(6, 4, 6, 4),
            Tag = candidate.ItemId,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, candidate.Name);
        button.Click += (_, _) => Picked?.Invoke(this, candidate.ItemId);
        return button;
    }

    // ---- parts ----

    // A text with its change at the right, muted ("↑ +1.7% · 7 D").
    private static FrameworkElement Pair(TextBlock left, string? right)
    {
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        left.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(left);
        if (right is not null)
        {
            var change = new TextBlock { Text = right, Style = StyleOf("StatusText"), FontSize = 11, Foreground = Brush("MutedBrush"), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(change, 1);
            row.Children.Add(change);
        }
        return row;
    }

    private static TextBlock Eyebrow(string text) => new() { Text = text, Style = StyleOf("EyebrowText"), Margin = new Thickness(0, 10, 0, 0) };

    private static TextBlock Note(string text, bool ink = false)
    {
        var note = new TextBlock { Text = text, Style = StyleOf("NoteText") };
        if (ink)
            note.Foreground = Brush("InkBrush");
        return note;
    }

    private static Style StyleOf(string key) => (Style)Application.Current.Resources[key];

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
}
