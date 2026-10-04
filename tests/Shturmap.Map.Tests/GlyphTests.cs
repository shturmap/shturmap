using SkiaSharp;
using Shturmap.Core.Quests;

namespace Shturmap.Map.Tests;

// The quest types' icons are filled shapes of the app's own build (owner, 2026-10-04, chosen from a panel of sets):
// each type has one, drawn at the size asked for whatever box its path was made in.
public class GlyphTests
{
    public static TheoryData<ObjectiveKind> Kinds() => new(Enum.GetValues<ObjectiveKind>());

    [Fact]
    public void Every_type_has_a_shape_of_its_own()
    {
        var paths = Enum.GetValues<ObjectiveKind>().Select(Glyphs.Path).ToList();
        Assert.All(paths, p => Assert.False(SKPath.ParseSvgPathData(p).IsEmpty));
        Assert.Equal(paths.Count, paths.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void A_shape_is_drawn_at_the_size_asked_for_around_its_point(ObjectiveKind kind)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(64, 64, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
            Glyphs.Draw(canvas, kind, new SKPoint(32, 32), 20, SKColors.White);
        int left = 64, top = 64, right = -1, bottom = -1;
        for (var y = 0; y < 64; y++)
            for (var x = 0; x < 64; x++)
                if (bitmap.GetPixel(x, y).Alpha > 60)
                {
                    left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y);
                }
        Assert.True(right >= 0, $"{kind}: nothing drawn");
        var (width, height) = (right - left + 1, bottom - top + 1);
        // 20 px along the longer side, and centred on the point.
        Assert.InRange(Math.Max(width, height), 19, 21);
        Assert.InRange((left + right + 1) / 2.0, 31, 33);
        Assert.InRange((top + bottom + 1) / 2.0, 31, 33);
        // A filled shape, not a hairline: a good part of its box is covered.
        var covered = Enumerable.Range(top, height).Sum(y => Enumerable.Range(left, width).Count(x => bitmap.GetPixel(x, y).Alpha > 128));
        Assert.True(covered > 0.2 * width * height, $"{kind}: only {covered} of {width * height} pixels");
    }
}
