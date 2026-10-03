using System.Globalization;

namespace Shturmap.Session;

/// <summary>
/// Removing Shturmap from its help panel (owner, 2026-10-03; docs/DESIGN.md §8, "Distribution"): Velopack's own
/// uninstaller, and, only when the player ticks "Also delete my Shturmap data", this install's data folder. The app
/// leaves a note in its install folder (<see cref="IntentFile"/>) and closes; the uninstaller then starts the exe with
/// its uninstall hook (Program.Main), which deletes the data folder only on a fresh note, after the app has let go of
/// its files. An uninstall from Windows' Settings finds no note and keeps the data, as it always did.
/// </summary>
public static class Uninstall
{
    /// <summary>The note in the install folder that asks the uninstall hook to delete the data folder.</summary>
    public const string IntentFile = "delete-data-on-uninstall";

    /// <summary>How old the note may be: a later uninstall from Windows' Settings never deletes data on an old tick.</summary>
    public static readonly TimeSpan IntentLifetime = TimeSpan.FromMinutes(5);

    /// <summary>How long the hook waits for the closing app's last files (database, log) to be let go.</summary>
    public static readonly TimeSpan DeletePatience = TimeSpan.FromSeconds(20);

    /// <summary>Offered only in an install Velopack made: the release or the dev build.</summary>
    public static bool Offered(string? installedAppId) => KindFor(installedAppId) is not null;

    /// <summary>The data folder an install keeps (as <see cref="Distribution.DataFolderFor"/> without --data);
    /// null for anything that isn't one of Shturmap's installs.</summary>
    public static DataFolderKind? KindFor(string? installedAppId) => installedAppId switch
    {
        Distribution.PackId => DataFolderKind.Release,
        Distribution.DeveloperPackId => DataFolderKind.Dev,
        _ => null,
    };

    /// <summary>
    /// The one folder a data deletion may remove: %LOCALAPPDATA%\Shturmap for the release (with the download cache in
    /// it), %LOCALAPPDATA%\Shturmap-dev for the dev build (the download cache it shares stays in the release's folder).
    /// A folder chosen with --data is never deleted.
    /// </summary>
    public static string? DataFolder(string? installedAppId, string localAppData) => KindFor(installedAppId) switch
    {
        DataFolderKind.Release => Path.Combine(localAppData, "Shturmap"),
        DataFolderKind.Dev => Path.Combine(localAppData, "Shturmap-dev"),
        _ => null,
    };

    /// <summary>
    /// Strict: exactly this install's data folder, directly in %LOCALAPPDATA%, and not a link to somewhere else.
    /// Anything else is refused, whatever the path looks like.
    /// </summary>
    public static bool MayDelete(string path, string? installedAppId, string localAppData)
    {
        if (DataFolder(installedAppId, localAppData) is not { } expected || string.IsNullOrWhiteSpace(path))
            return false;
        try
        {
            var full = Trim(Path.GetFullPath(path));
            if (!string.Equals(full, Trim(Path.GetFullPath(expected)), StringComparison.OrdinalIgnoreCase))
                return false;
            if (!string.Equals(Path.GetDirectoryName(full), Trim(Path.GetFullPath(localAppData)), StringComparison.OrdinalIgnoreCase))
                return false;
            var folder = new DirectoryInfo(full);
            return !folder.Exists || !folder.Attributes.HasFlag(FileAttributes.ReparsePoint);
        }
        catch (Exception e) when (e is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string Trim(string path) => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    /// <summary>Leaves the note (the player ticked the box), or removes an old one (they didn't).</summary>
    public static void SetIntent(string installFolder, bool deleteData, DateTime now)
    {
        var path = Path.Combine(installFolder, IntentFile);
        if (deleteData)
            File.WriteAllText(path, now.ToString("O", CultureInfo.InvariantCulture));
        else if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>A note written within <see cref="IntentLifetime"/> before <paramref name="now"/>.</summary>
    public static bool IntentFresh(string installFolder, DateTime now)
    {
        try
        {
            var path = Path.Combine(installFolder, IntentFile);
            if (!File.Exists(path))
                return false;
            var at = DateTime.Parse(File.ReadAllText(path).Trim(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            var age = now - at;
            return age >= TimeSpan.FromMinutes(-1) && age <= IntentLifetime;
        }
        catch (Exception e) when (e is IOException or FormatException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Deletes the data folder after <see cref="MayDelete"/> agrees, retrying while the closing app still holds a
    /// file, up to <paramref name="patience"/>. True when the folder is gone.
    /// </summary>
    public static bool DeleteData(string path, string? installedAppId, string localAppData, TimeSpan patience)
    {
        if (!MayDelete(path, installedAppId, localAppData))
            return false;
        var until = DateTime.UtcNow + patience;
        while (true)
        {
            try
            {
                // Links inside are removed as links, never followed (.NET's recursive delete).
                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                if (DateTime.UtcNow >= until)
                    return false;
                Thread.Sleep(500);
            }
        }
    }
}
