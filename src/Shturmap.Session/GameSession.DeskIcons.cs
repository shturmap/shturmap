using Shturmap.Data.Images;
using Shturmap.Game.Screenshots;

namespace Shturmap.Session;

// A "one of N" cell told apart by its picture (owner, 2026-10-25, "go ham"; docs/DESIGN.md §2, "For the stash desk"): the
// desk's reading compares the cell's art with its candidates' grid images, tarkov.dev's art the view draws the cells with,
// from the download cache only (cache\game-art). An image not there yet is asked for through the same art path, for the
// next reading; a reading never waits for it. The desk's view fetches the grid image of every cell it shows (a "?" cell's
// likeliest candidate's), so a stash read once has most of its art.
public sealed partial class GameSession
{
    private DeskIcons? _deskIcons;

    // One a run, so each image is made once; null before the art is there (tests without it: the label alone).
    private DeskIcons? DeskIconsNow() =>
        _deskIcons ??= Art is { } art ? new DeskIcons(id => CachedGrid(art, id), id => _ = art.GetAsync(ArtKind.Grid, id)) : null;

    // A grid image the cache holds, or null (none yet, or another Shturmap replacing it this moment).
    private static byte[]? CachedGrid(GameArt art, string id)
    {
        if (art.Cached(ArtKind.Grid, id) is not { } path)
            return null;
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
