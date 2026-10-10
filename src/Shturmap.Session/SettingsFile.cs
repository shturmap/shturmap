using System.Globalization;
using Microsoft.Data.Sqlite;
using Shturmap.Data.Progress;

namespace Shturmap.Session;

/// <summary>
/// Opens shturmap.db at a start, also when it can't be read (review of 2026-10-09: the session didn't start, and the
/// app ran on without quests, picks or settings, and without a word). A file SQLite finds damaged, or no database at
/// all, is moved aside with the time in its name ("shturmap.db.unreadable-20261009-1530"), its -wal and -shm with it,
/// and a fresh one is opened: picks, ticks and settings start over; the quest history is read back from the game's
/// logs, as at every start. A file that can't be opened for another reason (another program holds it, a lock that
/// didn't pass within SQLite's wait, the folder can't be written) is left as it is, and the session keeps what it saves
/// in memory until it closes. A second Shturmap opens the same file without trouble (WAL), so it never comes here; and
/// Windows moves no file another Shturmap has open.
/// </summary>
public static class SettingsFile
{
    /// <summary>What the session says when the file was set aside.</summary>
    public static string SetAsideNotice => SessionTexts.SettingsFileSetAside;

    /// <summary>What it says when the file was left as it is, and nothing is kept this time.</summary>
    public static string NotKeptNotice => SessionTexts.SettingsFileNotKept;

    // SQLite's result codes that say the file's content is bad: SQLITE_CORRUPT and SQLITE_NOTADB. Only these set a
    // file aside; a busy, locked, read-only or full one is a good file in a bad moment.
    private const int Corrupt = 11;
    private const int NotADatabase = 26;

    /// <summary>The store, and what to say about it (null when the file opened as it should).</summary>
    /// <param name="now">The time the name of a file set aside carries.</param>
    public static (ProgressStore Store, string? Notice) Open(string path, DateTime now)
    {
        try
        {
            return (new ProgressStore(path), null);
        }
        catch (SqliteException e) when (e.SqliteErrorCode is Corrupt or NotADatabase)
        {
            try
            {
                var aside = SetAside(path, now);
                var fresh = new ProgressStore(path);
                AppLog.Warn($"Settings file couldn't be read ({e.Message}): set aside as {Path.GetFileName(aside)}, and a fresh one started");
                return (fresh, SetAsideNotice);
            }
            catch (Exception again) when (again is SqliteException or IOException or UnauthorizedAccessException)
            {
                AppLog.Warn($"Settings file couldn't be read ({e.Message}) nor set aside: settings kept in memory this time", again);
                return (new ProgressStore(ProgressStore.InMemoryPath), NotKeptNotice);
            }
        }
        catch (Exception e) when (e is SqliteException or IOException or UnauthorizedAccessException)
        {
            AppLog.Warn("Settings file couldn't be opened: settings kept in memory this time", e);
            return (new ProgressStore(ProgressStore.InMemoryPath), NotKeptNotice);
        }
    }

    /// <summary>
    /// Moves the database, then its -wal and -shm, to the same names with ".unreadable-yyyyMMdd-HHmm" (and a number when
    /// that is taken). The database goes first: when it can't be moved, nothing has changed.
    /// </summary>
    /// <returns>Where the database went.</returns>
    public static string SetAside(string path, DateTime now)
    {
        string[] files = [path, path + "-wal", path + "-shm"];
        var stamp = ".unreadable-" + now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        var suffix = stamp;
        for (var n = 2; files.Any(f => File.Exists(f + suffix)); n++)
            suffix = $"{stamp}-{n}";
        foreach (var file in files.Where(File.Exists))
            File.Move(file, file + suffix);
        return path + suffix;
    }
}
