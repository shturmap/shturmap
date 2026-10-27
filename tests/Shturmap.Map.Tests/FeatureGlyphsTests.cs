using SkiaSharp;

namespace Shturmap.Map.Tests;

// What's New's pictures of 0.7.0's features, drawn by the map's code in the card's 56×34 place (owner, 2026-10-27, D4 B:
// "each feature should have a icon and a appropriate picture"; nothing bundled, docs/DESIGN.md §3).
public class FeatureGlyphsTests
{
    [Theory]
    [MemberData(nameof(Glyphs))]
    public void Every_picture_draws_inside_the_cards_place(FeatureGlyph glyph)
    {
        using var bitmap = FeatureGlyphs.Draw(glyph, 2);
        Assert.Equal((112, 68), (bitmap.Width, bitmap.Height));
        var drawn = bitmap.Pixels.Count(p => p.Alpha > 0);
        Assert.True(drawn > 200, $"{glyph}: {drawn} pixels drawn");
        var edge = Enumerable.Range(0, bitmap.Width).SelectMany(x => new[] { bitmap.GetPixel(x, 0), bitmap.GetPixel(x, bitmap.Height - 1) })
            .Concat(Enumerable.Range(0, bitmap.Height).SelectMany(y => new[] { bitmap.GetPixel(0, y), bitmap.GetPixel(bitmap.Width - 1, y) }));
        Assert.True(edge.All(p => p.Alpha < 40), $"{glyph} touches the edge of its place");
    }

    // Five features, five pictures, none the same as another.
    [Fact]
    public void No_two_pictures_are_alike()
    {
        var pictures = Enum.GetValues<FeatureGlyph>().Select(g =>
        {
            using var bitmap = FeatureGlyphs.Draw(g, 1);
            return string.Concat(bitmap.Pixels.Select(p => p.Alpha > 0 ? '#' : '.'));
        }).ToList();
        Assert.Equal(pictures.Count, pictures.Distinct().Count());
    }

    // The text size preview's plate: each step's panel as large as its text, so LARGEST reads larger than NORMAL.
    [Fact]
    public void The_text_size_plate_grows_with_each_step()
    {
        FeatureGlyphs.TextStepSample normal = new("NORMAL", 14, 11.5f, 11), largest = new("LARGEST", 19, 16, 15.5f);
        using var one = FeatureGlyphs.TextSizes([normal], "Ballet Lover", "Ragman", "Crossroads", 1);
        using var two = FeatureGlyphs.TextSizes([normal, largest], "Ballet Lover", "Ragman", "Crossroads", 1);
        Assert.True(two.Width > 2 * one.Width, $"{two.Width} DIP for both, {one.Width} for NORMAL alone");
        Assert.True(two.Height > one.Height);
        Assert.True(two.Pixels.Count(p => p.Alpha > 0) > 1000);
    }

    public static TheoryData<FeatureGlyph> Glyphs() => new(Enum.GetValues<FeatureGlyph>());
}
