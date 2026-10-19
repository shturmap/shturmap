using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Shturmap.App.Rules;
using Shturmap.Core.Desk;
using Shturmap.Data.Images;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Shturmap.App.Controls;

/// <summary>
/// One cell of the stash desk (docs/DESIGN.md §4, *The cell*, B1): the game's layout, with the price where the game
/// prints the short name. The item's art from the catalogue (tarkov.dev's grid image at the footprint read, turned where
/// the cell is; a slot's item icon), at half strength for a cell that fits several items; the ink tint over it by value
/// per slot (the grid images bring the game's own ground, so the tint can't lie behind the art); a dark band across the
/// top where the grid image prints the short name, the figure on it at the right after its source; the keep notch and
/// the "?" plate at the upper left; the count or condition at the lower right and the change at the lower left, each on
/// a dark plate. Marks and their room: <see cref="DeskCells.Of"/>.
/// </summary>
public sealed partial class DeskCellView : Grid
{
    private readonly DeskBoard _board;

    /// <param name="inset">How far it stands in from its place: a grid of several compartments sets them apart
    /// (<see cref="DeskPageRules.Inset"/>).</param>
    public DeskCellView(DeskRegion region, DeskCellSheet sheet, double size, DeskBoard board, Thickness inset = default)
    {
        Region = region;
        Sheet = sheet;
        Size = size;
        _board = board;
        var cell = sheet.Cell;
        Width = DeskCells.Width(cell, size) - inset.Left - inset.Right;
        Height = DeskCells.Height(cell, size) - inset.Top - inset.Bottom;
        // The marks' room by the size the window's text size draws them at (QA pass of 2026-10-19, F6).
        Marks = DeskCells.Of(sheet, size, TextScale.Drawn, inset.Left + inset.Right);
        Background = new SolidColorBrush(Colors.Transparent);

        Children.Add(new Border { Background = DeskBoard.Brush("CellBrush"), BorderBrush = DeskBoard.Brush("LineStrongBrush"), BorderThickness = new Thickness(1) });
        var art = cell.ItemId ?? cell.Ids.FirstOrDefault();
        if (art is not null)
        {
            var canvas = new Canvas { IsHitTestVisible = false, Opacity = Marks.ArtOpacity };
            Children.Add(canvas);
            Load(canvas, art, grid: !cell.IsSlot);
        }
        if (Marks.Tint > 0)
            Children.Add(new Border { Background = Ink(Marks.Tint), Margin = new Thickness(1), IsHitTestVisible = false });
        if (art is not null || Marks.Figure is not null)
        {
            // Where the grid image prints the short name: the figure stands here, on the ground's dark.
            Children.Add(new Rectangle
            {
                Fill = Ground(0.88), Height = Band, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(1, 1, 1, 0), IsHitTestVisible = false,
            });
        }
        if (Marks.Notch != DeskNotch.None)
            Children.Add(Notch(Marks.Notch));
        if (Marks.Plate)
            Children.Add(Plate(Marks.PlateLow ? 2 : Marks.Notch != DeskNotch.None ? DeskCells.NotchSize + 2 : 2, Marks.PlateLow));
        if (Marks.Figure is { } figure)
            Children.Add(FigureRow(figure));
        if (Marks.Count is { } count)
            Children.Add(Corner(count, HorizontalAlignment.Right));
        if (Marks.Change is { } change)
            Children.Add(Corner(change, HorizontalAlignment.Left));
        // The linked highlight: gold at 18 % over the cell while its window's head is pointed at (DeskBoard).
        Light = new Border { Background = DeskBoard.Brush("LinkBrush"), BorderBrush = DeskBoard.Brush("AmberBrush"), BorderThickness = new Thickness(1), Opacity = 0, IsHitTestVisible = false };
        Children.Add(Light);
        PointerEntered += (_, _) => _board.OnCellEntered(this);
    }

    public DeskRegion Region { get; }

    public DeskCellSheet Sheet { get; }

    /// <summary>The page's cell size, 40 or 56 DIP.</summary>
    public double Size { get; }

    public DeskCellMarks Marks { get; }

    /// <summary>The gold over the cell while its window is pointed at.</summary>
    public Border Light { get; }

    // The band's height: the figure's line, taller where the window's text size draws the figure larger.
    private double Band => Math.Max(Size >= DeskCells.Large ? 16 : 13.5, Math.Ceiling(TextScale.Drawn(Marks.FigureSize) + 3));

    private FrameworkElement FigureRow(string figure)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 1, 3, 0),
            Height = Band,
            IsHitTestVisible = false,
        };
        var large = Size >= DeskCells.Large;
        switch (Marks.Source)
        {
            case DeskSourceMark.Portrait:
                row.Children.Add(new Picture { TraderId = Marks.TraderId, Size = 14, VerticalAlignment = VerticalAlignment.Center });
                break;
            case DeskSourceMark.Flea or DeskSourceMark.Trader:
                row.Children.Add(new FontIcon
                {
                    // The flea's price tag, a trader's person ("Visual language", "One symbol, one meaning").
                    Glyph = char.ConvertFromUtf32(Marks.Source == DeskSourceMark.Flea ? 0xE8EC : 0xE77B),
                    FontFamily = new FontFamily("Segoe Fluent Icons"),
                    FontSize = large ? 11 : 9,
                    Foreground = DeskBoard.Brush("MutedBrush"),
                    VerticalAlignment = VerticalAlignment.Center,
                });
                break;
        }
        var words = new TextBlock
        {
            FontSize = Marks.FigureSize,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontStretch = Windows.UI.Text.FontStretch.SemiCondensed,
            Foreground = DeskBoard.Brush("InkBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            // One line as tall as the band: Segoe UI's "~" would raise it past the band.
            LineHeight = Band - 1,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
        };
        // "~" in Segoe UI's fuller wave: Bahnschrift's, at the figure's 10.5 px, reads as a minus.
        if (figure.StartsWith('~'))
        {
            words.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = "~", FontFamily = new FontFamily("Segoe UI"), FontWeight = Microsoft.UI.Text.FontWeights.Bold });
            words.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = figure[1..] });
        }
        else
            words.Text = figure;
        row.Children.Add(words);
        return row;
    }

    // The count or condition, and the change: small, in ink, each on a plate of the ground's dark at its corner.
    private FrameworkElement Corner(string text, HorizontalAlignment side) => new Border
    {
        Background = Ground(0.7),
        Padding = new Thickness(2, 0, 2, 0),
        Margin = new Thickness(1),
        HorizontalAlignment = side,
        VerticalAlignment = VerticalAlignment.Bottom,
        IsHitTestVisible = false,
        Child = new TextBlock
        {
            Text = text,
            FontSize = DeskCells.SmallSizeAt(Size),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontStretch = Windows.UI.Text.FontStretch.SemiCondensed,
            Foreground = DeskBoard.Brush("InkBrush"),
        },
    };

    // The keep notch, 11 DIP at the upper left: a filled corner triangle (now; in ink for a hideout module), an open
    // one (next), a corner bracket (later).
    private static FrameworkElement Notch(DeskNotch notch)
    {
        const double s = DeskCells.NotchSize;
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
        var colour = DeskBoard.Brush(notch == DeskNotch.Hideout ? "InkBrush" : "AmberBrush");
        var filled = notch is DeskNotch.Now or DeskNotch.Hideout;
        return new Path
        {
            Data = geometry,
            Fill = filled ? colour : null,
            Stroke = colour,
            StrokeThickness = filled ? 0.5 : 1.5,
            Margin = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false,
        };
    }

    // The "?" plate: the small dark plate with a gold hairline, "one of N items" (as the map's "maybe here").
    private static FrameworkElement Plate(double left, bool low) => new Border
    {
        Width = DeskCells.PlateWidth,
        Height = DeskCells.PlateWidth,
        Margin = low ? new Thickness(left, 0, 0, 2) : new Thickness(left, 2, 0, 0),
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = low ? VerticalAlignment.Bottom : VerticalAlignment.Top,
        Background = DeskBoard.Brush("GroundBrush"),
        BorderBrush = DeskBoard.Brush("AmberBrush"),
        BorderThickness = new Thickness(1),
        IsHitTestVisible = false,
        Child = new TextBlock
        {
            Text = "?",
            FontSize = 9,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = DeskBoard.Brush("AmberBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            LineHeight = 10,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
        },
    };

    // The art: a grid cell's grid image (its footprint; turned a quarter where the cell stands the other way), a slot's
    // item icon, fitted. The grid image falls back to the icon; with neither, the cell stays its ground.
    private async void Load(Canvas canvas, string id, bool grid)
    {
        if (Picture.Art() is not { } art)
            return;
        try
        {
            var kind = grid ? ArtKind.Grid : ArtKind.Item;
            var path = art.Cached(kind, id) ?? await art.GetAsync(kind, id);
            if (path is null && grid)
                path = art.Cached(ArtKind.Item, id) ?? await art.GetAsync(ArtKind.Item, id);
            if (path is null)
                return;
            var w = Width - 2;
            var h = Height - 2;
            var scale = XamlRoot?.RasterizationScale ?? 1.5;
            var bitmap = new BitmapImage(new Uri(path)) { DecodePixelWidth = (int)Math.Ceiling(Math.Max(w, h) * scale) };
            var image = new Image { Source = bitmap, Stretch = Stretch.Uniform, Width = w, Height = h };
            Canvas.SetLeft(image, 1);
            Canvas.SetTop(image, 1);
            bitmap.ImageOpened += (_, _) =>
            {
                // A cell turned in the game (a 2×1 standing as 1×2): its picture turns a quarter with it.
                var wide = bitmap.PixelWidth > bitmap.PixelHeight * 1.2;
                var tall = bitmap.PixelHeight > bitmap.PixelWidth * 1.2;
                if (!grid || !(wide && h > w * 1.2 || tall && w > h * 1.2))
                    return;
                image.Width = h;
                image.Height = w;
                Canvas.SetLeft(image, 1 + (w - h) / 2);
                Canvas.SetTop(image, 1 + (h - w) / 2);
                image.RenderTransformOrigin = new Point(0.5, 0.5);
                image.RenderTransform = new RotateTransform { Angle = 90 };
            };
            canvas.Children.Add(image);
        }
        catch (Exception e)
        {
            Shturmap.Session.AppLog.Debug("Desk cell art failed: " + e.Message);
        }
    }

    private static SolidColorBrush Ink(double opacity) => new((Windows.UI.Color)Application.Current.Resources["InkColor"]) { Opacity = opacity };

    private static SolidColorBrush Ground(double opacity) => new((Windows.UI.Color)Application.Current.Resources["GroundColor"]) { Opacity = opacity };
}
