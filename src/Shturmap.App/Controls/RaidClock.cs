using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Shturmap.App.Rules;

namespace Shturmap.App.Controls;

/// <summary>
/// The raid's time in the raid card: the minutes left as the card's largest figure, over a rule of the raid's length
/// with the time gone dark and the time left light (owner, 2026-10-05: "The remaining time is a crucial piece of
/// information and should be more visible. I would make it a prominent item in the UI and maybe even have a cool
/// animation that is in-line with the style of the app. It should not be too crazy though to not steer away the
/// attention through its movement"). What it shows is <see cref="RaidTime"/>'s reading.
/// Nothing in it moves: at a new minute the figure and the rule just change (owner, 2026-10-09: the figure's decode
/// and the notch along the rule at each minute were removed), so it never pulls at the eye from the second monitor.
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

    // The rule: 4 px, with a mark for now that stands over it, and a tick under it every ten minutes.
    private const double RuleHeight = 4;
    private const double RuleBox = 16;
    private const int TickMinutes = 10;

    private readonly TextBlock _figure;
    private readonly TextBlock _unit;
    private readonly TextBlock _other;
    private readonly Grid _rule = new() { Height = RuleBox, Margin = new Thickness(0, 5, 0, 0) };
    private readonly Rectangle _track = new() { Height = RuleHeight, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 0, 0) };
    private readonly Rectangle _left = new() { Height = RuleHeight, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 4, 0, 0) };
    private readonly Rectangle _now = new() { Width = 2, Height = RuleHeight + 8, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Canvas _ticks = new() { Height = RuleBox };
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
        _unit = Word(HorizontalAlignment.Left, new Thickness(8, 0, 0, 0));
        _other = Word(HorizontalAlignment.Right, new Thickness(8, 0, 0, 0));
        SetColumn(_unit, 1);
        SetColumn(_other, 2);
        Children.Add(_figure);
        Children.Add(_unit);
        Children.Add(_other);

        _track.Fill = Brush("LineStrongBrush");
        _rule.Children.Add(_ticks);
        _rule.Children.Add(_track);
        _rule.Children.Add(_left);
        _rule.Children.Add(_now);
        SetRow(_rule, 1);
        SetColumnSpan(_rule, 3);
        Children.Add(_rule);
        _rule.SizeChanged += (_, _) => LayRule();
    }

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

    private static TextBlock Word(HorizontalAlignment side, Thickness margin) => new()
    {
        Style = (Style)Application.Current.Resources["StatusText"], TextLineBounds = TextLineBounds.Tight,
        VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = side, Margin = margin, TextTrimming = TextTrimming.CharacterEllipsis,
    };

    private void Show()
    {
        if (Reading is not { } time)
        {
            Visibility = Visibility.Collapsed;
            return;
        }
        Visibility = Visibility.Visible;
        ToolTipService.SetToolTip(this, time.Tip);
        AutomationProperties.SetName(this, time.Text);

        // The time left where it is known; else the time in the raid, said as that.
        var strong = Brush(time.Low ? "RedBrush" : "InkBrush");
        _figure.Text = (time.Left ?? time.In).ToString(CultureInfo.CurrentCulture);
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
}
