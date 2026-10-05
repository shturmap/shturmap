using System.Globalization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Shturmap.App.Rules;

namespace Shturmap.App.Controls;

/// <summary>
/// The raid's time in the raid card: the minutes left as the card's largest figure, over a rule of the raid's length
/// with the time gone dark and the time left light (owner, 2026-10-05: "The remaining time is a crucial piece of
/// information and should be more visible. I would make it a prominent item in the UI and maybe even have a cool
/// animation that is in-line with the style of the app. It should not be too crazy though to not steer away the
/// attention through its movement"). What it shows is <see cref="RaidTime"/>'s reading.
/// It moves only when it changes, once a minute: the figure decodes as the big cue's title does (its digits flicker
/// in gold for a moment and settle), and one dark notch runs along what is left of the rule. Nothing moves in between,
/// so it never pulls at the eye from the second monitor; with Windows' animation effects off it doesn't move at all.
/// The last ten minutes are red, as the game's own timer is. docs/DESIGN.md §4, "Screen anatomy".
/// </summary>
public sealed partial class RaidClock : Grid
{
    public static readonly DependencyProperty ReadingProperty = DependencyProperty.Register(
        nameof(Reading), typeof(RaidTime.Reading), typeof(RaidClock), new PropertyMetadata(null, (d, _) => ((RaidClock)d).Show()));

    /// <summary>The raid's time now, or null outside a raid: then nothing is shown.</summary>
    public RaidTime.Reading? Reading
    {
        get => (RaidTime.Reading?)GetValue(ReadingProperty);
        set => SetValue(ReadingProperty, value);
    }

    /// <summary>Set for snapshot and demo pictures: nothing moves, so a picture never catches a figure half decoded.</summary>
    public static bool Still { get; set; }

    // The rule: 4 px, with a mark for now that stands over it, and a tick under it every ten minutes.
    private const double RuleHeight = 4;
    private const double RuleBox = 16;
    private const int TickMinutes = 10;
    private static readonly TimeSpan Decode = TimeSpan.FromMilliseconds(520);

    private readonly Run _settled = new();
    private readonly Run _flicker = new();
    private readonly TextBlock _figure;
    private readonly TextBlock _unit;
    private readonly TextBlock _other;
    private readonly Grid _rule = new() { Height = RuleBox, Margin = new Thickness(0, 5, 0, 0) };
    private readonly Rectangle _track = new() { Height = RuleHeight, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 0, 0) };
    private readonly Rectangle _left = new() { Height = RuleHeight, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 4, 0, 0) };
    private readonly Rectangle _now = new() { Width = 2, Height = RuleHeight + 8, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Rectangle _glint = new() { Width = 34, Height = RuleHeight, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 0), Opacity = 0, IsHitTestVisible = false };
    private readonly TranslateTransform _glintAt = new();
    private readonly Canvas _ticks = new() { Height = RuleBox };
    private DispatcherQueueTimer? _decode;
    private Storyboard? _sweep;
    private string _shownFigure = "";
    private int _ticksFor = -1;
    private double _ticksWidth = -1;

    public RaidClock()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Margin = new Thickness(0, 6, 0, 4);
        Visibility = Visibility.Collapsed;
        // Tooltips need something to point at between the words.
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        var font = (FontFamily)Application.Current.Resources["UiFont"];
        // The figure: the card's largest type, on the tight box of its digits so the words beside it sit on its foot.
        _figure = new TextBlock
        {
            FontFamily = font, FontSize = 44, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontStretch = Windows.UI.Text.FontStretch.SemiCondensed,
            TextLineBounds = TextLineBounds.Tight, VerticalAlignment = VerticalAlignment.Bottom,
        };
        _figure.Inlines.Add(_settled);
        _figure.Inlines.Add(_flicker);
        _flicker.Foreground = Brush("AmberBrush");
        _unit = Word(HorizontalAlignment.Left, new Thickness(8, 0, 0, 0));
        _other = Word(HorizontalAlignment.Right, new Thickness(8, 0, 0, 0));
        SetColumn(_unit, 1);
        SetColumn(_other, 2);
        Children.Add(_figure);
        Children.Add(_unit);
        Children.Add(_other);

        _track.Fill = Brush("LineStrongBrush");
        _glint.RenderTransform = _glintAt;
        _glint.Fill = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0), EndPoint = new Windows.Foundation.Point(1, 0),
            GradientStops =
            {
                new GradientStop { Color = Microsoft.UI.Colors.Transparent, Offset = 0 },
                new GradientStop { Color = ((SolidColorBrush)Brush("GroundBrush")).Color, Offset = 0.5 },
                new GradientStop { Color = Microsoft.UI.Colors.Transparent, Offset = 1 },
            },
        };
        _rule.Children.Add(_ticks);
        _rule.Children.Add(_track);
        _rule.Children.Add(_left);
        _rule.Children.Add(_glint);
        _rule.Children.Add(_now);
        SetRow(_rule, 1);
        SetColumnSpan(_rule, 3);
        Children.Add(_rule);
        _rule.SizeChanged += (_, _) => LayRule();
        Unloaded += (_, _) => StopMoving();
    }

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

    private static TextBlock Word(HorizontalAlignment side, Thickness margin) => new()
    {
        Style = (Style)Application.Current.Resources["StatusText"], TextLineBounds = TextLineBounds.Tight,
        VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = side, Margin = margin, TextTrimming = TextTrimming.CharacterEllipsis,
    };

    private void StopMoving()
    {
        _decode?.Stop();
        _sweep?.Stop();
        _glint.Opacity = 0;
        _settled.Text = _shownFigure;
        _flicker.Text = "";
    }

    private void Show()
    {
        if (Reading is not { } time)
        {
            Visibility = Visibility.Collapsed;
            StopMoving();
            _shownFigure = "";
            return;
        }
        Visibility = Visibility.Visible;
        ToolTipService.SetToolTip(this, time.Tip);
        AutomationProperties.SetName(this, time.Text);

        // The time left where it is known; else the time in the raid, said as that.
        var strong = Brush(time.Low ? "RedBrush" : "InkBrush");
        var figure = (time.Left ?? time.In).ToString(CultureInfo.CurrentCulture);
        _figure.Foreground = strong;
        _unit.Text = time.Left is null ? "MIN IN" : "MIN LEFT";
        _unit.Foreground = strong;
        _other.Text = time.Left is not null ? $"{time.In} MIN IN"
            : time.Over ? $"PAST THE RAID'S {time.RaidMinutes} MIN"
            : time.Scav ? "TIME LEFT NOT KNOWN" : "";
        _other.Foreground = Brush(time.Over ? "RedBrush" : "MutedBrush");
        _rule.Visibility = time.RaidMinutes > 0 ? Visibility.Visible : Visibility.Collapsed;
        _left.Fill = strong;
        _now.Fill = strong;
        LayRule();

        var changed = figure != _shownFigure;
        var first = _shownFigure.Length == 0;
        _shownFigure = figure;
        if (!changed)
            return;
        if (Still || !new Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
        {
            StopMoving();
            return;
        }
        DecodeFigure(figure);
        // The first figure of a raid just decodes; a new minute also sends one glint along what is left.
        if (!first && time.Left is not null)
            Sweep();
    }

    // The rule's parts by the share of the raid that is gone: the mark for now, the light part right of it, the ticks.
    private void LayRule()
    {
        if (Reading is not { RaidMinutes: > 0 } time || _rule.ActualWidth <= 0)
            return;
        var width = _rule.ActualWidth;
        var at = Math.Round(time.Gone * (width - _now.Width));
        _now.Margin = new Thickness(at, 0, 0, 0);
        _left.Width = Math.Max(0, width - at - _now.Width);
        _rule.Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, width, RuleBox) };
        if (_ticksFor == time.RaidMinutes && _ticksWidth == width)
            return;
        _ticksFor = time.RaidMinutes;
        _ticksWidth = width;
        _ticks.Children.Clear();
        // A tick every ten minutes of the raid, and one at each end: the rule reads as a scale, like the map's.
        for (var minute = 0; minute <= time.RaidMinutes; minute += TickMinutes)
            AddTick(minute, time.RaidMinutes, width);
        if (time.RaidMinutes % TickMinutes != 0)
            AddTick(time.RaidMinutes, time.RaidMinutes, width);
    }

    private void AddTick(int minute, int of, double width)
    {
        var tick = new Rectangle { Width = 1, Height = 4, Fill = Brush("LineStrongBrush") };
        Canvas.SetLeft(tick, Math.Round((double)minute / of * (width - 1)));
        Canvas.SetTop(tick, 4 + RuleHeight + 2);
        _ticks.Children.Add(tick);
    }

    // The figure decodes as the big cue's title does: digits not yet settled flicker in gold, settling left to right.
    private void DecodeFigure(string figure)
    {
        const string digits = "0123456789";
        _decode?.Stop();
        var started = DateTime.UtcNow;
        var timer = _decode = DispatcherQueue.GetForCurrentThread().CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(40);
        timer.Tick += (_, _) =>
        {
            if (!ReferenceEquals(timer, _decode))
                return;
            var p = Math.Min(1, (DateTime.UtcNow - started) / Decode);
            var settled = p >= 1 ? figure.Length : (int)Math.Floor(p * figure.Length);
            _settled.Text = figure[..settled];
            _flicker.Text = string.Concat(figure[settled..].Select(_ => digits[Random.Shared.Next(digits.Length)]));
            if (p >= 1)
                timer.Stop();
        };
        _settled.Text = "";
        _flicker.Text = string.Concat(figure.Select(_ => digits[Random.Shared.Next(digits.Length)]));
        timer.Start();
    }

    // One glint from the mark for now to the rule's end, over the time that is left.
    private void Sweep()
    {
        if (_rule.ActualWidth <= 0 || _left.Width < _glint.Width)
            return;
        _sweep?.Stop();
        var from = _now.Margin.Left - _glint.Width / 2;
        var story = new Storyboard();
        var move = new DoubleAnimation
        {
            From = from, To = _rule.ActualWidth, Duration = TimeSpan.FromSeconds(1.1),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
        };
        Storyboard.SetTarget(move, _glintAt);
        Storyboard.SetTargetProperty(move, "X");
        var fade = new DoubleAnimationUsingKeyFrames();
        fade.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.Zero, Value = 0 });
        fade.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.15), Value = 1 });
        fade.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.9), Value = 1 });
        fade.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(1.1), Value = 0 });
        Storyboard.SetTarget(fade, _glint);
        Storyboard.SetTargetProperty(fade, "Opacity");
        story.Children.Add(move);
        story.Children.Add(fade);
        _sweep = story;
        story.Begin();
    }
}
