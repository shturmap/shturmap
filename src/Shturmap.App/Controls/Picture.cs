using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Shturmap.Data.Images;

namespace Shturmap.App.Controls;

/// <summary>
/// A trader portrait or an item icon, square in a dark cell like the game's inventory. Shows a glyph until the
/// picture has been fetched, and keeps the glyph if it can't be (offline, unknown id). Set either TraderId or ItemId;
/// with ItemId, PictureId may name the item whose icon pictures it (a gun's default preset).
/// </summary>
public sealed partial class Picture : Grid
{
    public static readonly DependencyProperty TraderIdProperty = DependencyProperty.Register(
        nameof(TraderId), typeof(string), typeof(Picture), new PropertyMetadata(null, (d, _) => ((Picture)d).Update()));

    public static readonly DependencyProperty ItemIdProperty = DependencyProperty.Register(
        nameof(ItemId), typeof(string), typeof(Picture), new PropertyMetadata(null, (d, _) => ((Picture)d).Update()));

    public static readonly DependencyProperty PictureIdProperty = DependencyProperty.Register(
        nameof(PictureId), typeof(string), typeof(Picture), new PropertyMetadata(null, (d, _) => ((Picture)d).Update()));

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(double), typeof(Picture), new PropertyMetadata(20.0, (d, _) => ((Picture)d).Update()));

    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(string), typeof(Picture), new PropertyMetadata(null, (d, _) => ((Picture)d).Update()));

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(Picture), new PropertyMetadata(null, (d, e) => ToolTipService.SetToolTip(d, e.NewValue)));

    public static readonly DependencyProperty HeldProperty = DependencyProperty.Register(
        nameof(Held), typeof(bool), typeof(Picture), new PropertyMetadata(false, (d, _) => ((Picture)d).Frame()));

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

    /// <summary>
    /// The item whose icon pictures <see cref="ItemId"/> when it isn't its own: a gun's default preset, the assembled
    /// gun (owner, 2026-10-19; Data.TarkovDev.ItemSources.PictureId). Tried first; if it can't be had, the item's own
    /// icon, then the glyph. Null or the item's own id: the item's icon.
    /// </summary>
    public string? PictureId
    {
        get => (string?)GetValue(PictureIdProperty);
        set => SetValue(PictureIdProperty, value);
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
    /// Framed in amber, the held look (a held card's border): a trader's portrait in the strip under the MAP list while
    /// its focus is held (owner, 2026-10-17). The frame lies over the picture, so the portrait keeps its size.
    /// </summary>
    public bool Held
    {
        get => (bool)GetValue(HeldProperty);
        set => SetValue(HeldProperty, value);
    }

    private bool IsTrader => TraderId is not null;

    // Over the picture: the cell's frame in the held look, and nothing otherwise (the cell's own frame shows).
    private readonly Border _held = new() { IsHitTestVisible = false, BorderThickness = new Thickness(1.5) };

    private void Frame() => _held.BorderBrush = Held ? Brush("AmberBrush") : null;

    private void Update()
    {
        Width = Height = Size;
        var kind = IsTrader ? ArtKind.Trader : ArtKind.Item;
        // The pictures to try in turn: a gun's preset, then the gun's own (never for a trader).
        string[] ids = IsTrader ? [TraderId!]
            : ItemId is null ? []
            : PictureId is { Length: > 0 } picture && picture != ItemId ? [picture, ItemId] : [ItemId];
        var shown = string.Join("/", ids);
        _shown = shown;
        Children.Clear();

        // Square like the game's trader portraits and inventory cells.
        Children.Add(new Border
        {
            Background = Brush(IsTrader ? "CardBrush" : "CellBrush"),
            BorderBrush = Brush("LineStrongBrush"),
            BorderThickness = new Thickness(1),
        });
        Children.Add(_held);
        Canvas.SetZIndex(_held, 2);
        Frame();
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

        if (ids.Length == 0 || Art() is not { } art)
            return;
        if (art.Cached(kind, ids[0]) is { } path)
            Show(path, fallback);
        else
            _ = LoadAsync(art, kind, ids, shown, fallback);
    }

    private async Task LoadAsync(GameArt art, ArtKind kind, string[] ids, string shown, FontIcon fallback)
    {
        foreach (var id in ids)
        {
            var path = await art.GetAsync(kind, id);
            if (_shown != shown)
                return;
            if (path is not null)
            {
                Show(path, fallback);
                return;
            }
        }
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
