using Shturmap.Game.Screenshots;
using SkiaSharp;

namespace Shturmap.Game.Tests;

// A "one of N" cell told apart by its picture (owner, 2026-10-25, "go ham"; docs/DESIGN.md §2, "For the stash desk"): the
// cell's art against each candidate's grid image, taken as the likeliest only when every image was at hand, the likeliest
// is like the cell at all (DeskIcons.Least) and clearly ahead of the next where the images differ (DeskIcons.Margin);
// else "one of N", the likeliest first. The tests draw their own art: one model in two colours (a plate carrier's, an
// armband's), the cell a cell of a 1440p stash (84 px) on a dark field. The real screenshots are measured with
// shturmap-cli desk (docs/NEXT.md, step 1c).
public sealed class DeskIconsTests
{
    private const int Pitch = 84;
    private static readonly SKRectI At = SKRectI.Create(100, 100, Pitch, Pitch);

    private static string Id(int n) => n.ToString("D24");

    private static DeskCandidate Item(int n) => new(Id(n), "Plate", $"Plate carrier {n}", 1, 1);

    // One model: stripes across, its left half one colour and its right half another; "swapped" is the same model in the
    // other colourway, so the two differ by colour and by the halves' light alone.
    private static SKBitmap Art(bool swapped)
    {
        var bitmap = new SKBitmap(new SKImageInfo(63, 63, SKColorType.Bgra8888, SKAlphaType.Premul));
        SKColor red = new(200, 70, 40), blue = new(40, 60, 170);
        for (var y = 0; y < 63; y++)
        {
            for (var x = 0; x < 63; x++)
            {
                var c = (x < 32) != swapped ? red : blue;
                var light = (y / 7) % 2 == 0 ? 40 : 0;
                bitmap.SetPixel(x, y, new SKColor((byte)Math.Min(255, c.Red + light), (byte)Math.Min(255, c.Green + light), (byte)Math.Min(255, c.Blue + light)));
            }
        }
        return bitmap;
    }

    private static byte[] Png(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    // The picture: a dark field with the cell's art drawn over the cell, as the game draws it (each given art at its share
    // of the light, so two halves are a blend of both).
    private static SKBitmap Picture(params (SKBitmap Art, float Share)[] arts)
    {
        var picture = new SKBitmap(new SKImageInfo(300, 300, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(picture);
        canvas.Clear(new SKColor(18, 20, 18));
        foreach (var (art, share) in arts)
        {
            using var image = SKImage.FromBitmap(art);
            using var paint = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(255 * share)) };
            canvas.DrawImage(image, SKRect.Create(At.Left, At.Top, At.Width, At.Height), StashPicture.Sampling, paint);
        }
        return picture;
    }

    private static DeskIcons Icons(Dictionary<string, byte[]> images, List<string>? asked = null) =>
        new(id => images.GetValueOrDefault(id), id => asked?.Add(id));

    [Fact]
    public void A_cell_is_its_candidate_when_that_ones_image_is_clearly_the_likeliest()
    {
        using SKBitmap one = Art(false), other = Art(true);
        var icons = Icons(new() { [Id(1)] = Png(one), [Id(2)] = Png(other) });
        using var picture = Picture((one, 1));
        var (candidates, scores) = icons.Settle(picture, At, Pitch, [Item(2), Item(1)], DeskIconPlace.Footprint);
        Assert.Equal(Id(1), Assert.Single(candidates).ItemId);
        // Both compared, the likeliest first, far over the least likeness and ahead where their images differ.
        Assert.Equal([Id(1), Id(2)], scores.Select(s => s.ItemId));
        Assert.True(scores[0].Score >= DeskIcons.Least);
        Assert.True(scores[0].Contrast - scores[1].Contrast >= DeskIcons.Margin);
    }

    [Fact]
    public void A_cell_stays_one_of_n_when_the_likeliest_isnt_ahead_by_the_margin()
    {
        // Half of each colourway's light: the cell's art is as like the one as the other.
        using SKBitmap one = Art(false), other = Art(true);
        var icons = Icons(new() { [Id(1)] = Png(one), [Id(2)] = Png(other) });
        using var picture = Picture((one, 1), (other, 0.5f));
        var (candidates, scores) = icons.Settle(picture, At, Pitch, [Item(1), Item(2)], DeskIconPlace.Footprint);
        Assert.All(scores, s => Assert.NotNull(s.Contrast));
        Assert.True(Math.Abs(scores[0].Contrast!.Value - scores[1].Contrast!.Value) < DeskIcons.Margin);
        Assert.Equal(2, candidates.Count);
        // The likeliest first.
        Assert.Equal(scores.Select(s => s.ItemId), candidates.Select(c => c.ItemId));
    }

    [Fact]
    public void A_cell_stays_one_of_n_while_a_candidates_image_isnt_at_hand_and_that_image_is_asked_for()
    {
        using SKBitmap one = Art(false), other = Art(true);
        var asked = new List<string>();
        var icons = Icons(new() { [Id(1)] = Png(one), [Id(2)] = Png(other) }, asked);
        using var picture = Picture((one, 1));
        var (candidates, scores) = icons.Settle(picture, At, Pitch, [Item(3), Item(2), Item(1)], DeskIconPlace.Footprint);
        // Not settled though the cell is plainly the first: the third might be likelier still.
        Assert.Equal([Id(1), Id(2), Id(3)], candidates.Select(c => c.ItemId));
        Assert.Null(scores.Single(s => s.ItemId == Id(3)).Score);
        // Asked for, for the next reading; this one didn't wait for it.
        Assert.Equal([Id(3)], asked);
    }

    [Fact]
    public void A_cell_stays_one_of_n_when_its_art_is_like_none_of_the_images()
    {
        // A tooltip's text over the cell, or art tarkov.dev no longer draws so: speckles of no pattern.
        using SKBitmap one = Art(false), other = Art(true);
        var icons = Icons(new() { [Id(1)] = Png(one), [Id(2)] = Png(other) });
        using var speckled = new SKBitmap(new SKImageInfo(63, 63, SKColorType.Bgra8888, SKAlphaType.Premul));
        var random = new Random(7);
        for (var y = 0; y < 63; y++)
        {
            for (var x = 0; x < 63; x++)
                speckled.SetPixel(x, y, random.Next(2) == 0 ? new SKColor(200, 200, 200) : new SKColor(30, 30, 30));
        }
        using var picture = Picture((speckled, 1));
        var (candidates, scores) = icons.Settle(picture, At, Pitch, [Item(1), Item(2)], DeskIconPlace.Footprint);
        Assert.True(scores.Max(s => s.Score) < DeskIcons.Least);
        Assert.Equal(2, candidates.Count);
    }

    [Fact]
    public void Items_drawn_with_one_art_stay_one_of_n()
    {
        // Two keys of one look: their images differ nowhere, so the picture says nothing of which it is.
        using var one = Art(false);
        var png = Png(one);
        var icons = Icons(new() { [Id(1)] = png, [Id(2)] = png });
        using var picture = Picture((one, 1));
        var (candidates, scores) = icons.Settle(picture, At, Pitch, [Item(1), Item(2)], DeskIconPlace.Footprint);
        Assert.Equal(2, candidates.Count);
        Assert.All(scores, s => Assert.Null(s.Contrast));
    }

    [Fact]
    public void A_cell_of_one_candidate_or_none_is_left_as_the_label_left_it()
    {
        using var one = Art(false);
        var asked = new List<string>();
        var icons = Icons([], asked);
        using var picture = Picture((one, 1));
        var (candidates, scores) = icons.Settle(picture, At, Pitch, [Item(1)], DeskIconPlace.Footprint);
        Assert.Equal(Id(1), Assert.Single(candidates).ItemId);
        Assert.Empty(scores);
        Assert.Empty(icons.Settle(picture, At, Pitch, [], DeskIconPlace.Footprint).Candidates);
        Assert.Empty(asked);
        // No images at all (tests without the art, --no-icons): every cell as its label left it.
        Assert.Equal(2, DeskIcons.None.Settle(picture, At, Pitch, [Item(1), Item(2)], DeskIconPlace.Footprint).Candidates.Count);
    }
}
