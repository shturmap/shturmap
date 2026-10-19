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

/// <summary>What one menu screenshot showed: the operational tasks' table, or a trader's tasks (told, nothing read), or
/// the item grids; Stash is null when the picture went to the tasks' reading or nothing needed looking for.</summary>
public sealed record MenuReading(TaskPage? Tasks, StashReading? Stash);

/// <summary>
/// The menus' readings of one screenshot, its picture opened once (owner, 2026-10-19: from a panel of four, B): the
/// tasks reader looks first (the picture's left two thirds, about 110 ms), and a picture that shows the Tasks screen
/// (the operational table, another Tasks table, a trader's tasks) goes to it alone; the stash reader never sees it, and
/// sees every other picture as before. Either may be missing (this Windows has no recognition for it).
/// </summary>
public sealed class MenuScreenshotReader(TasksReader? tasks, StashReader? stash)
{
    /// <summary>The language the item cells are read in, or null without a stash reader.</summary>
    public string? StashLanguage => stash?.LanguageTag;

    /// <summary>The language the tasks' table is read in, or null without a tasks reader.</summary>
    public string? TasksLanguage => tasks?.LanguageTag;

    /// <summary>
    /// What the screenshot shows: null when the file isn't a whole picture after the tries, or is gone. The item grids
    /// are read only when there are candidates to look for (the stash's reading needs them; the table's doesn't).
    /// </summary>
    public async Task<MenuReading?> ReadAsync(string path, IReadOnlyList<StashCandidate> candidates, CancellationToken ct = default)
    {
        using var picture = await MenuPicture.OpenAsync(path, ct: ct);
        if (picture is null)
            return null;
        if (tasks is not null && await tasks.ReadPageAsync(picture, ct: ct) is { Screen: not TaskScreen.None } page)
            return new MenuReading(page, null);
        if (stash is null || candidates.Count == 0)
            return new MenuReading(null, null);
        return new MenuReading(null, await stash.ReadAsync(picture, candidates, ct));
    }
}
