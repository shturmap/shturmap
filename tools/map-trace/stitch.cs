#:package SkiaSharp
// Lays the tiles of one zoom level of a tile map side by side as one picture, for tools\map-trace\trace.cs
// (docs/MAP-TRACE.md). The tiles are the app's own cached files: <cache>\map-tiles\<map>\<layer>\<zoom>\<x>_<y>.png.
// A tile that isn't there stays transparent. The picture is Battlestate's art as rendered by tarkov.dev: keep it
// out of the repository.
//   dotnet run tools/map-trace/stitch.cs -- <folder with x_y.png> <out.png>
// It prints the tile range; trace.cs needs the smallest x and y.
using SkiaSharp;

var tiles = Directory.GetFiles(args[0], "*.png")
    .Select(f => { var p = Path.GetFileNameWithoutExtension(f).Split('_'); return (X: int.Parse(p[0]), Y: int.Parse(p[1]), File: f); })
    .ToList();
if (tiles.Count == 0) throw new InvalidOperationException($"No tiles in {args[0]}.");
int minX = tiles.Min(t => t.X), maxX = tiles.Max(t => t.X), minY = tiles.Min(t => t.Y), maxY = tiles.Max(t => t.Y);
int size;
using (var first = SKBitmap.Decode(tiles[0].File))
    size = first.Width;
using var whole = new SKBitmap((maxX - minX + 1) * size, (maxY - minY + 1) * size, SKColorType.Rgba8888, SKAlphaType.Unpremul);
using (var canvas = new SKCanvas(whole))
{
    canvas.Clear(SKColors.Transparent);
    foreach (var t in tiles)
    {
        using var image = SKImage.FromEncodedData(t.File);
        if (image is null)
        {
            Console.WriteLine($"not an image, left out: {t.File}");
            continue;
        }
        canvas.DrawImage(image, (t.X - minX) * size, (t.Y - minY) * size, new SKSamplingOptions(SKFilterMode.Nearest));
    }
}
using (var data = SKImage.FromBitmap(whole).Encode(SKEncodedImageFormat.Png, 100))
    File.WriteAllBytes(args[1], data.ToArray());
int missing = (maxX - minX + 1) * (maxY - minY + 1) - tiles.Count;
Console.WriteLine($"{tiles.Count} tiles of {size} px, x {minX}..{maxX}, y {minY}..{maxY}, {missing} not there");
Console.WriteLine($"{args[1]}: {whole.Width}x{whole.Height}");
