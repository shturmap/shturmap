using SkiaSharp;

namespace Shturmap.Game.Screenshots;

/// <summary>
/// A menu screenshot's picture, opened once for the readings of the menus (the operational tasks' table, then the item
/// grids; docs/DESIGN.md §2, "Menu screenshots"): read-only, shared with the game that may still be writing it, and only
/// once it is written to its end (a PNG's IEND chunk). The game creates the file and then writes it, so a picture that
/// can't be decoded yet is tried again. Nothing of it is written anywhere.
/// </summary>
public static class MenuPicture
{
    /// <summary>The decoded picture, or null when the file isn't a whole picture after the tries, or is gone.</summary>
    /// <param name="tries">How often to try a file that isn't a whole picture yet.</param>
    /// <param name="wait">How long to wait between tries.</param>
    public static async Task<SKBitmap?> OpenAsync(string path, int tries = 8, TimeSpan? wait = null, CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (await OpenOnceAsync(path, ct) is { } picture)
                    return picture;
            }
            catch (FileNotFoundException)
            {
                return null; // deleted in the meantime
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
            catch (Exception e) when (e is not OperationCanceledException and not OutOfMemoryException)
            {
                // Not a whole picture yet (the decoder says so in several ways), or still held by the game.
            }
            if (attempt >= tries)
                return null;
            await Task.Delay(wait ?? TimeSpan.FromMilliseconds(400), ct);
        }
    }

    private static async Task<SKBitmap?> OpenOnceAsync(string path, CancellationToken ct)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (!ExitListReader.IsWhole(file))
            return null;
        ct.ThrowIfCancellationRequested();
        return StashPicture.Decode(file) ?? throw new InvalidDataException("Not a picture");
    }
}

/// <summary>What one menu screenshot showed: the operational tasks' table, or a trader's TASKS tab (its operational task
/// and its list's countdowns, owner, 2026-10-25), or the item grids; Stash is null when the picture went to the tasks' reading or nothing needed looking for; Desk is the
/// stash desk's reading of the same picture (null when it wasn't asked for, or the picture went to the tasks).</summary>
public sealed record MenuReading(TaskPage? Tasks, StashReading? Stash, DeskReading? Desk = null)
{
    /// <summary>What the readings saw on the way, for the readings log (<see cref="ReadingLog"/>); null from a reader that
    /// doesn't say (a test's).</summary>
    public MenuSeen? Seen { get; init; }
}

/// <summary>
/// What the menus' readers saw of one picture beyond what they hand on, for the readings log (owner, 2026-10-25;
/// <see cref="ReadingLog"/>): the picture's size, the Tasks screen the tasks' reading told and each of its bands' votes
/// as the reading heard them, and the desk's look (the layout in pixels; per cell its place, the label as the recognition
/// read it and its fit). Never a pixel of the picture.
/// </summary>
public sealed record MenuSeen(int Width, int Height)
{
    /// <summary>The Tasks screen the tasks' reading told (None for a picture of no Tasks screen), or null when there was no
    /// tasks reader.</summary>
    public TaskScreen? TasksScreen { get; init; }

    /// <summary>Each band's votes in the order read, as <see cref="TasksReader.ReadPageAsync"/>'s note gives them.</summary>
    public IReadOnlyList<string> Bands { get; init; } = [];

    /// <summary>The desk's look at the picture, when it was read for the desk.</summary>
    internal DeskLook? Desk { get; init; }
}

/// <summary>
/// The menus' readings of one screenshot, its picture opened once (owner, 2026-10-19: from a panel of four, B): the
/// tasks reader looks first (the picture's left two thirds, about 110 ms), and a picture that shows the Tasks screen
/// (the operational table, another Tasks table, a trader's tasks) goes to it alone; the stash reader never sees it, and
/// sees every other picture as before. Either may be missing (this Windows has no recognition for it).
/// </summary>
public sealed class MenuScreenshotReader(TasksReader? tasks, StashReader? stash, DeskReader? desk = null)
{
    /// <summary>The language the item cells are read in, or null without a stash reader.</summary>
    public string? StashLanguage => stash?.LanguageTag;

    /// <summary>The language the tasks' table is read in, or null without a tasks reader.</summary>
    public string? TasksLanguage => tasks?.LanguageTag;

    /// <summary>
    /// What the screenshot shows: null when the file isn't a whole picture after the tries, or is gone. The item grids
    /// are read only when there are candidates to look for (the stash's reading needs them; the table's doesn't).
    /// </summary>
    public Task<MenuReading?> ReadAsync(string path, IReadOnlyList<StashCandidate> candidates, CancellationToken ct = default) =>
        ReadAsync(path, candidates, null, default, ct);

    /// <summary>
    /// The same, and the stash desk's reading of the picture against <paramref name="catalogue"/> (owner, 2026-10-19;
    /// docs/DESIGN.md §2, "For the stash desk") when one is given: the picture opened once, the stash reader's look (the
    /// items the quests need, for What You Have) and the desk's (the whole catalogue) one after the other; each reads its
    /// cells' labels itself, the desk at its own fit. What the readings saw on the way goes along for the readings log
    /// (<see cref="MenuReading.Seen"/>): the bands' votes, the desk's look.
    /// </summary>
    /// <param name="at">The screenshot's time, the desk page's.</param>
    public async Task<MenuReading?> ReadAsync(string path, IReadOnlyList<StashCandidate> candidates, DeskCatalogue? catalogue, DateTimeOffset at,
        CancellationToken ct = default)
    {
        using var picture = await MenuPicture.OpenAsync(path, ct: ct);
        if (picture is null)
            return null;
        var bands = new List<string>();
        var seen = new MenuSeen(picture.Width, picture.Height) { Bands = bands };
        if (tasks is not null)
        {
            var page = await tasks.ReadPageAsync(picture, note: bands.Add, ct: ct);
            seen = seen with { TasksScreen = page.Screen };
            if (page.Screen != TaskScreen.None)
                return new MenuReading(page, null) { Seen = seen };
        }
        var stashReading = stash is not null && candidates.Count > 0 ? await stash.ReadAsync(picture, candidates, ct) : null;
        DeskReading? deskReading = null;
        if (desk is not null && catalogue is not null && stashReading?.IsTradingScreen != true)
        {
            // The desk's reading as DeskReader.ReadAsync makes it, its look kept for the readings log.
            var look = await desk.LookAsync(picture, catalogue, at, ct, raid: false);
            deskReading = new DeskReading(look.Layout.Screen, look.Page, look.TotalMs);
            seen = seen with { Desk = look };
        }
        return new MenuReading(null, stashReading, deskReading) { Seen = seen };
    }
}
