using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Shturmap.Map;

namespace Shturmap.App.Controls;

/// <summary>
/// The raid replay's band at the map's foot (owner, 2026-10-07: "B" and "Replay timeline 1"; docs/DESIGN.md "Map
/// drawing", *The raid replay*): RAID OVER with the map and the raid's length, how many positions have been drawn, and
/// the timeline. The timeline is the raid's length as a rule with a tick every ten minutes, as the raid card's clock;
/// its played part is drawn in the pen's colours (<see cref="ReplayInk"/>), so the rule is the colour key. Above it a dot
/// per position, below it a check per objective ticked during the raid and a green triangle where the extract list was
/// read; the playhead runs along it.
/// </summary>
public sealed partial class ReplayBand : Grid
{
    /// <summary>The band's height.</summary>
    public const double BandHeight = 84;

    /// <summary>How far the band stands above the map's foot: the WIKI MAP link and the artwork's credit stay below it.</summary>
    public const double AboveFoot = 46;

    /// <summary>What the band covers from the map's foot up: the map's tags stay above it (<see cref="MapScene.ReplayFoot"/>).</summary>
    public const double Foot = BandHeight + AboveFoot;

    private const double Side = 24;
    private const double RuleTop = 26;
    private const double RuleHeight = 4;

    private readonly TextBlock _title;
    private readonly TextBlock _map;
    private readonly TextBlock _count;
    private readonly TextBlock _end;
    private readonly Canvas _line = new() { Height = 44 };
    private readonly Rectangle _track = new() { Height = RuleHeight };
    private readonly Rectangle _played = new() { Height = RuleHeight };
    private readonly Rectangle _head = new() { Width = 2, Height = 14 };
    private readonly List<(Ellipse Dot, double Minute)> _dots = [];
    private readonly List<(FrameworkElement Mark, double Minute)> _marks = [];
    private RaidReplay? _replay;
    private double _minute;

    public ReplayBand()
    {
        Height = BandHeight;
        VerticalAlignment = VerticalAlignment.Bottom;
        IsHitTestVisible = false;
        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xEB, 0x0B, 0x0C, 0x0B));
        BorderBrush = Brush("LineStrongBrush");
        BorderThickness = new Thickness(0, 1, 0, 0);
        Padding = new Thickness(Side, 13, Side, 0);
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // The words in the big cue's eyebrow: semi-condensed, semibold, spaced; RAID OVER in amber, the raid in ink.
        var font = (FontFamily)Application.Current.Resources["UiFont"];
        TextBlock Eyebrow(string brush) => new()
        {
            FontFamily = font, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontStretch = Windows.UI.Text.FontStretch.SemiCondensed,
            CharacterSpacing = 300, Foreground = Brush(brush), VerticalAlignment = VerticalAlignment.Center,
        };
        _title = Eyebrow("AmberBrush");
        _title.Text = "RAID OVER";
        _map = Eyebrow("InkBrush");
        var words = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
        words.Children.Add(_title);
        words.Children.Add(_map);
        _count = new TextBlock
        {
            Style = (Style)Application.Current.Resources["StatusText"], FontSize = 11, Foreground = Brush("MutedBrush"),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Children.Add(words);
        Children.Add(_count);

        _track.Fill = Brush("LineStrongBrush");
        _head.Fill = Brush("InkBrush");
        _end = new TextBlock
        {
            Style = (Style)Application.Current.Resources["StatusText"], FontSize = 10, Foreground = Brush("MutedBrush"),
        };
        SetRow(_line, 1);
        Children.Add(_line);
        _line.SizeChanged += (_, _) => Lay();
    }

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

    private static Windows.UI.Color Color(SkiaSharp.SKColor c) => Windows.UI.Color.FromArgb(c.Alpha, c.Red, c.Green, c.Blue);

    /// <summary>Shows a raid's replay, its playhead at the start.</summary>
    /// <param name="mapWords">The raid as the cue's eyebrow says it: "CUSTOMS · 33 MIN".</param>
    public void Show(RaidReplay replay, string mapWords)
    {
        _replay = replay;
        _map.Text = mapWords;
        _end.Text = $"{(int)replay.Minutes} MIN";
        _line.Children.Clear();
        _dots.Clear();
        _marks.Clear();
        _line.Children.Add(_track);
        _line.Children.Add(_played);
        // Every ten minutes, and at the end.
        for (var m = 0; m <= replay.Minutes; m += 10)
            _marks.Add((Tick(), m));
        _marks.Add((Tick(), replay.Minutes));
        foreach (var fix in replay.Fixes)
        {
            var dot = new Ellipse { Width = 5.2, Height = 5.2, StrokeThickness = 1 };
            _dots.Add((dot, fix.Minute));
            _line.Children.Add(dot);
        }
        // A check per objective ticked during the raid (the done objective's symbol), a green triangle where the
        // extract list was read (the PMC extract's shape and colour).
        foreach (var tick in replay.Ticks.Distinct())
        {
            var check = new FontIcon { Glyph = "", FontSize = 8, Foreground = Brush("InkBrush") };
            _marks.Add((check, tick));
        }
        if (replay.ListRead is { } read)
        {
            var triangle = new Polygon { Fill = Brush("GreenBrush"), Points = { new(0, 8), new(4.5, 0), new(9, 8) } };
            _marks.Add((triangle, read));
        }
        foreach (var (mark, _) in _marks)
            _line.Children.Add(mark);
        _line.Children.Add(_head);
        _line.Children.Add(_end);
        // The played part takes the pen's colours along the whole rule, so whatever is played shows its own.
        _played.Fill = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            GradientStops = new GradientStopCollection(),
        };
        foreach (var i in Enumerable.Range(0, 9))
            ((LinearGradientBrush)_played.Fill).GradientStops.Add(new GradientStop { Color = Color(ReplayInk.At(i / 8.0)), Offset = i / 8.0 });
        SetMinute(0);
    }

    private Rectangle Tick() => new() { Width = 1, Height = 4, Fill = Brush("LineStrongBrush") };

    /// <summary>Moves the playhead to a minute of the raid: what is played up to it shows.</summary>
    public void SetMinute(double minute)
    {
        _minute = minute;
        if (_replay is not { } replay)
            return;
        var shown = replay.Fixes.Count(f => f.Minute <= minute + 1e-9);
        _count.Text = $"{shown} OF {replay.Fixes.Count} {(replay.Fixes.Count == 1 ? "POSITION" : "POSITIONS")}";
        Lay();
    }

    private void Lay()
    {
        if (_replay is not { } replay || _line.ActualWidth <= 0)
            return;
        var width = _line.ActualWidth;
        double X(double minute) => width * Math.Clamp(minute / replay.Minutes, 0, 1);
        _track.Width = width;
        Canvas.SetTop(_track, RuleTop);
        _played.Width = Math.Max(0, X(_minute));
        Canvas.SetTop(_played, RuleTop);
        if (_played.Fill is LinearGradientBrush brush)
        {
            brush.StartPoint = new Windows.Foundation.Point(0, 0);
            brush.EndPoint = new Windows.Foundation.Point(width, 0);
        }
        Canvas.SetLeft(_head, X(_minute) - 1);
        Canvas.SetTop(_head, RuleTop - 5);
        foreach (var (dot, minute) in _dots)
        {
            var played = minute <= _minute + 1e-9;
            dot.Fill = played ? new SolidColorBrush(Color(ReplayInk.At(minute / replay.Minutes))) : null;
            dot.Stroke = played ? null : Brush("LineStrongBrush");
            Canvas.SetLeft(dot, X(minute) - dot.Width / 2);
            Canvas.SetTop(dot, RuleTop - 9.5);
        }
        foreach (var (mark, minute) in _marks)
        {
            switch (mark)
            {
                case Rectangle tick:
                    Canvas.SetLeft(tick, Math.Min(width - 1, X(minute)));
                    Canvas.SetTop(tick, RuleTop + RuleHeight + 2);
                    break;
                case FontIcon check:
                    check.Visibility = minute <= _minute ? Visibility.Visible : Visibility.Collapsed;
                    Canvas.SetLeft(check, X(minute) - 4);
                    Canvas.SetTop(check, RuleTop + RuleHeight + 6);
                    break;
                case Polygon triangle:
                    triangle.Visibility = minute <= _minute ? Visibility.Visible : Visibility.Collapsed;
                    Canvas.SetLeft(triangle, X(minute) - 1);
                    Canvas.SetTop(triangle, RuleTop + RuleHeight + 6);
                    break;
            }
        }
        _end.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(_end, width - _end.DesiredSize.Width);
        Canvas.SetTop(_end, RuleTop + RuleHeight + 6);
    }
}
