using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Shturmap.Data.Images;

namespace Shturmap.App.Controls;

/// <summary>
/// A trader portrait or an item icon, square in a dark cell like the game's inventory. Shows a glyph until the
/// picture has been fetched, and keeps the glyph if it can't be (offline, unknown id). Set either TraderId or ItemId.
/// </summary>
public sealed partial class Picture : Grid
{
    public static readonly DependencyProperty TraderIdProperty = DependencyProperty.Register(
        nameof(TraderId), typeof(string), typeof(Picture), new PropertyMetadata(null, (d, _) => ((Picture)d).Update()));

    public static readonly DependencyProperty ItemIdProperty = DependencyProperty.Register(
        nameof(ItemId), typeof(string), typeof(Picture), new PropertyMetadata(null, (d, _) => ((Picture)d).Update()));

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(double), typeof(Picture), new PropertyMetadata(20.0, (d, _) => ((Picture)d).Update()));

    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(string), typeof(Picture), new PropertyMetadata(null, (d, _) => ((Picture)d).Update()));

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(Picture), new PropertyMetadata(null, (d, e) => ToolTipService.SetToolTip(d, e.NewValue)));

    public static readonly DependencyProperty HandoverProperty = DependencyProperty.Register(
        nameof(Handover), typeof(bool), typeof(Picture), new PropertyMetadata(false, (d, _) => ((Picture)d).Update()));

    /// <summary>Where pictures come from; set once by the main window.</summary>
    public static Func<GameArt?> Art { get; set; } = () => null;

    private string? _shown;

    // Over the cell and its picture: where a linked cell is tinted. Linked paints backgrounds, and this one's is
    // covered by the cell's own fill and by the picture, which comes with a ground of its own.
    private readonly Border _tint = new() { IsHitTestVisible = false };

    /// <summary>The linked highlight's tint on the cell, or null.</summary>
    public Brush? Tint
    {
        get => _tint.Background;
        set => _tint.Background = value;
    }

    public Picture()
    {
        VerticalAlignment = VerticalAlignment.Top;
        Update();
    }

    public string? TraderId
    {
        get => (string?)GetValue(TraderIdProperty);
        set => SetValue(TraderIdProperty, value);
    }

    public string? ItemId
    {
        get => (string?)GetValue(ItemIdProperty);
        set => SetValue(ItemIdProperty, value);
    }

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>Segoe Fluent Icons character shown until (or instead of) the picture.</summary>
    public string? Glyph
    {
        get => (string?)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    /// <summary>Tooltip text, e.g. the trader's name.</summary>
    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>
    /// The item goes to the trader after the raid (<see cref="Shturmap.Data.TarkovDev.Handovers"/>): a small
    /// handshake in the cell's lower right corner, where the game marks an item found in raid. Its card says it in words.
    /// </summary>
    public bool Handover
    {
        get => (bool)GetValue(HandoverProperty);
        set => SetValue(HandoverProperty, value);
    }

    private bool IsTrader => TraderId is not null;

    private void Update()
    {
        Width = Height = Size;
        var kind = IsTrader ? ArtKind.Trader : ArtKind.Item;
        var id = TraderId ?? ItemId;
        _shown = id;
        Children.Clear();

        // Square like the game's trader portraits and inventory cells.
        Children.Add(new Border
        {
            Background = Brush(IsTrader ? "CardBrush" : "CellBrush"),
            BorderBrush = Brush("LineStrongBrush"),
            BorderThickness = new Thickness(1),
        });
        Children.Add(_tint);
        Canvas.SetZIndex(_tint, 1);
        var fallback = new FontIcon
        {
            Glyph = Glyph ?? char.ConvertFromUtf32(IsTrader ? 0xE77B : 0xE7B8),
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = Math.Max(10, Size * 0.5),
            Foreground = Brush("MutedBrush"),
        };
        Children.Add(fallback);
        if (Handover)
        {
            var mark = HandoverMark.Plate(Size);
            Canvas.SetZIndex(mark, 2);
            Children.Add(mark);
        }

        if (id is null || Art() is not { } art)
            return;
        if (art.Cached(kind, id) is { } path)
            Show(path, fallback);
        else
            _ = LoadAsync(art, kind, id, fallback);
    }

    private async Task LoadAsync(GameArt art, ArtKind kind, string id, FontIcon fallback)
    {
        var path = await art.GetAsync(kind, id);
        if (path is not null && _shown == id)
            Show(path, fallback);
    }

    private void Show(string path, FontIcon fallback)
    {
        var scale = XamlRoot?.RasterizationScale ?? 1.5;
        var bitmap = new BitmapImage(new Uri(path)) { DecodePixelWidth = (int)Math.Ceiling(Size * scale) };
        fallback.Visibility = Visibility.Collapsed;
        Children.Add(IsTrader
            ? new Image { Source = bitmap, Margin = new Thickness(1), Stretch = Stretch.UniformToFill }
            : new Image { Source = bitmap, Margin = new Thickness(Size > 24 ? 3 : 1), Stretch = Stretch.Uniform });
    }

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
}
